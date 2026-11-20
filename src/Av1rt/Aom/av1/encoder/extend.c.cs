// Port of libaom av1/encoder/extend.c

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
                memcpy(dst_ptr1 + extend_left, src_ptr1, w);
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

    internal static void av1_copy_and_extend_frame(YV12_BUFFER_CONFIG src,
                                                   YV12_BUFFER_CONFIG dst)
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
