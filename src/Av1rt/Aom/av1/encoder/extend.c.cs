// Port of libaom av1/encoder/extend.c

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

// [subset] highbd_copy_and_extend_plane: CONFIG_AV1_HIGHBITDEPTH == 0 (source frames are 8-bit).
internal static partial class extend
{
    private static void copy_and_extend_plane(Ptr<byte> src, int src_pitch,
                                              Ptr<byte> dst, int dst_pitch, int w, int h,
                                              int extend_top, int extend_left,
                                              int extend_bottom, int extend_right,
                                              int chroma_step)
    {
        int i, linesize;
        // copy the left and right most columns out
        Ptr<byte> src_ptr1 = src;
        Ptr<byte> src_ptr2 = src + (w - 1) * chroma_step;
        Ptr<byte> dst_ptr1 = dst - extend_left;
        Ptr<byte> dst_ptr2 = dst + w;

        for (i = 0; i < h; i++)
        {
            memset(dst_ptr1, src_ptr1[0], extend_left);
            if (chroma_step == 1)
            {
                // [av1rt] Skipped when the picture already is in place (see av1_copy_and_extend_frame).
                if (src_ptr1 != dst_ptr1 + extend_left) memcpy(dst_ptr1 + extend_left, src_ptr1, w);
            }
            else
            {
                for (int j = 0; j < w; j++)
                {
                    dst_ptr1[extend_left + j] = src_ptr1[chroma_step * j];
                }
            }
            memset(dst_ptr2, src_ptr2[0], extend_right);
            src_ptr1 += src_pitch;
            src_ptr2 += src_pitch;
            dst_ptr1 += dst_pitch;
            dst_ptr2 += dst_pitch;
        }

        // Now copy the top and bottom lines into each line of the respective
        // borders
        src_ptr1 = dst - extend_left;
        src_ptr2 = dst + dst_pitch * (h - 1) - extend_left;
        dst_ptr1 = dst + dst_pitch * (-extend_top) - extend_left;
        dst_ptr2 = dst + dst_pitch * (h) - extend_left;
        linesize = extend_left + extend_right + w;
        Debug.Assert(linesize <= dst_pitch);

        for (i = 0; i < extend_top; i++)
        {
            memcpy(dst_ptr1, src_ptr1, linesize);
            dst_ptr1 += dst_pitch;
        }

        for (i = 0; i < extend_bottom; i++)
        {
            memcpy(dst_ptr2, src_ptr2, linesize);
            dst_ptr2 += dst_pitch;
        }
    }

    // [av1rt] The planes of a copy_and_extend_frame_mt call.
    private readonly record struct PlaneCopy(Ptr<byte> src, int src_pitch, Ptr<byte> dst,
                                             int dst_pitch, int w, int h, int extend_top,
                                             int extend_left, int extend_bottom,
                                             int extend_right);

    private const int kCopyBand = 64;

    // [port] The multi-threaded form of copy_and_extend_plane for all planes at once: the rows
    // of every plane in bands of 64 (copy plus left/right extension), then the border rows
    // (copies of the extended first and last rows). Every byte written is the same as in the
    // serial loops; two worker launches per frame instead of two per plane. With in_place (the
    // picture already is in dst) only the borders are written.
    private static void copy_and_extend_frame_mt(PlaneCopy[] planes, int threads,
                                                 PrimaryMultiThreadInfo workers, bool in_place)
    {
        Span<int> row_band_start = stackalloc int[planes.Length + 1];
        Span<int> border_band_start = stackalloc int[planes.Length + 1];
        for (int p = 0; p < planes.Length; p++)
        {
            PlaneCopy pc = planes[p];
            Debug.Assert(pc.extend_left + pc.extend_right + pc.w <= pc.dst_pitch);
            row_band_start[p + 1] = row_band_start[p] + (pc.h + kCopyBand - 1) / kCopyBand;
            border_band_start[p + 1] = border_band_start[p] +
                (pc.extend_top + pc.extend_bottom + kCopyBand - 1) / kCopyBand;
        }
        int[] rows = row_band_start.ToArray(), borders = border_band_start.ToArray();

        av1rt_parallel_for(workers, threads, rows[planes.Length], b =>
        {
            int p = 0;
            while (b >= rows[p + 1]) p++;
            PlaneCopy pc = planes[p];
            int r0 = (b - rows[p]) * kCopyBand, r1 = AOMMIN(pc.h, r0 + kCopyBand);
            for (int i = r0; i < r1; i++)
            {
                Ptr<byte> s = pc.src + i * pc.src_pitch;
                Ptr<byte> d = pc.dst + i * pc.dst_pitch;
                memset(d - pc.extend_left, s[0], pc.extend_left);
                if (!in_place) memcpy(d, s, pc.w);
                memset(d + pc.w, s[pc.w - 1], pc.extend_right);
            }
        });
        av1rt_parallel_for(workers, threads, borders[planes.Length], b =>
        {
            int p = 0;
            while (b >= borders[p + 1]) p++;
            PlaneCopy pc = planes[p];
            int linesize = pc.extend_left + pc.extend_right + pc.w;
            Ptr<byte> top = pc.dst - pc.extend_left;
            Ptr<byte> bottom = pc.dst + pc.dst_pitch * (pc.h - 1) - pc.extend_left;
            int border_rows = pc.extend_top + pc.extend_bottom;
            int r0 = (b - borders[p]) * kCopyBand, r1 = AOMMIN(border_rows, r0 + kCopyBand);
            for (int i = r0; i < r1; i++)
            {
                if (i < pc.extend_top)
                    memcpy(pc.dst + pc.dst_pitch * (i - pc.extend_top) - pc.extend_left, top,
                           linesize);
                else
                    memcpy(pc.dst + pc.dst_pitch * (pc.h + i - pc.extend_top) - pc.extend_left,
                           bottom, linesize);
            }
        });
    }

    internal static void av1_copy_and_extend_frame(YV12_BUFFER_CONFIG src,
                                                   YV12_BUFFER_CONFIG dst, int threads = 1,
                                                   PrimaryMultiThreadInfo workers = null)
    {
        // Extend src frame in buffer
        int et_y = dst.border;
        int el_y = dst.border;
        int er_y = AOMMAX(src.y_crop_width + dst.border,
                          ALIGN_POWER_OF_TWO(src.y_crop_width, 6)) -
                   src.y_crop_width;
        int eb_y = AOMMAX(src.y_crop_height + dst.border,
                          ALIGN_POWER_OF_TWO(src.y_crop_height, 6)) -
                   src.y_crop_height;
        int uv_width_subsampling = src.subsampling_x;
        int uv_height_subsampling = src.subsampling_y;
        int et_uv = et_y >> uv_height_subsampling;
        int el_uv = el_y >> uv_width_subsampling;
        int eb_uv = eb_y >> uv_height_subsampling;
        int er_uv = er_y >> uv_width_subsampling;

        // [subset] YV12_FLAG_HIGHBITDEPTH (highbd_copy_and_extend_plane): CONFIG_AV1_HIGHBITDEPTH
        // == 0.
        Debug.Assert((src.flags & YV12_FLAG_HIGHBITDEPTH) == 0);

        // [av1rt] The picture may already be in dst: Av1Encoder.GetInputFrame hands out the
        // lookahead buffer the next push fills, and the caller writes the picture there. Then
        // src and dst are the same planes and only the borders are extended (same bytes).
        bool in_place = src.y_buffer == dst.y_buffer && src.u_buffer == dst.u_buffer &&
                        src.v_buffer == dst.v_buffer && src.y_stride == dst.y_stride &&
                        src.uv_stride == dst.uv_stride;
        Debug.Assert(in_place || !ReferenceEquals(src.y_buffer.Array, dst.y_buffer.Array));

        // [port] threads > 1 (not in C): copy_and_extend_frame_mt, for planar 4:2:0/4:4:4
        // input of at least 256 rows.
        if (threads > 1 && src.monochrome == 0 && !src.v_buffer.IsNull &&
            src.y_crop_height >= 256)
        {
            copy_and_extend_frame_mt(new PlaneCopy[]
            {
                new(src.y_buffer, src.y_stride, dst.y_buffer, dst.y_stride, src.y_crop_width,
                    src.y_crop_height, et_y, el_y, eb_y, er_y),
                new(src.u_buffer, src.uv_stride, dst.u_buffer, dst.uv_stride,
                    src.uv_crop_width, src.uv_crop_height, et_uv, el_uv, eb_uv, er_uv),
                new(src.v_buffer, src.uv_stride, dst.v_buffer, dst.uv_stride,
                    src.uv_crop_width, src.uv_crop_height, et_uv, el_uv, eb_uv, er_uv),
            }, threads, workers, in_place);
            return;
        }
        copy_and_extend_plane(src.y_buffer, src.y_stride, dst.y_buffer,
                              dst.y_stride, src.y_crop_width, src.y_crop_height,
                              et_y, el_y, eb_y, er_y, 1);
        if (src.monochrome == 0)
        {
            // detect nv12 format
            int chroma_step = !src.v_buffer.IsNull ? 1 : 2;
            Ptr<byte> src_v_buffer =
                !src.v_buffer.IsNull ? src.v_buffer : src.u_buffer + 1;
            copy_and_extend_plane(src.u_buffer, src.uv_stride, dst.u_buffer,
                                  dst.uv_stride, src.uv_crop_width,
                                  src.uv_crop_height, et_uv, el_uv, eb_uv, er_uv,
                                  chroma_step);
            copy_and_extend_plane(src_v_buffer, src.uv_stride, dst.v_buffer,
                                  dst.uv_stride, src.uv_crop_width,
                                  src.uv_crop_height, et_uv, el_uv, eb_uv, er_uv,
                                  chroma_step);
        }
    }
}
