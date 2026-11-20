// Port of libaom aom_scale/generic/yv12extend.c

global using static Av1rt.Aom.yv12extend;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] Every _c function is followed by its aom_scale_rtcd.h dispatched name as a forwarder.
// [subset] Not ported:
// - extend_plane_high, memcpy_short_addr and the YV12_FLAG_HIGHBITDEPTH branches:
//   CONFIG_AV1_HIGHBITDEPTH == 0 (frame buffers are always 8-bit).
// - aom_yv12_copy_frame_c, aom_yv12_partial_copy_{y,u,v}_c, aom_yv12_partial_coloc_copy_*_c and
//   aom_yv12_realloc_with_new_border_c: never executed in the subset (loop restoration search,
//   superres and border reallocation are off).
// extend_frame / aom_extend_frame_borders_c never run in the subset either; they are kept because
// they are tiny and encoder.c calls aom_extend_frame_borders for scaled references.
internal static partial class yv12extend
{
    private static void extend_plane(Ptr<byte> src, int src_stride, int width,
                                     int height, int extend_top, int extend_left,
                                     int extend_bottom, int extend_right, int v_start,
                                     int v_end)
    {
        Debug.Assert(!src.IsNull);
        int i;
        int linesize = extend_left + extend_right + width;
        Debug.Assert(linesize <= src_stride);

        /* copy the left and right most columns out */
        Ptr<byte> src_ptr1 = src + v_start * src_stride;
        Ptr<byte> src_ptr2 = src + v_start * src_stride + width - 1;
        Ptr<byte> dst_ptr1 = src + v_start * src_stride - extend_left;
        Ptr<byte> dst_ptr2 = src_ptr2 + 1;

        for (i = v_start; i < v_end; ++i)
        {
            memset(dst_ptr1, src_ptr1[0], extend_left);
            memset(dst_ptr2, src_ptr2[0], extend_right);
            src_ptr1 += src_stride;
            src_ptr2 += src_stride;
            dst_ptr1 += src_stride;
            dst_ptr2 += src_stride;
        }

        /* Now copy the top and bottom lines into each line of the respective
         * borders
         */
        src_ptr1 = src - extend_left;
        dst_ptr1 = src_ptr1 + src_stride * -extend_top;

        for (i = 0; i < extend_top; ++i)
        {
            memcpy(dst_ptr1, src_ptr1, linesize);
            dst_ptr1 += src_stride;
        }

        src_ptr2 = src_ptr1 + src_stride * (height - 1);
        dst_ptr2 = src_ptr2;

        for (i = 0; i < extend_bottom; ++i)
        {
            dst_ptr2 += src_stride;
            memcpy(dst_ptr2, src_ptr2, linesize);
        }
    }

    internal static void aom_extend_frame_borders_plane_row_c(YV12_BUFFER_CONFIG ybf,
                                                              int plane, int v_start, int v_end)
    {
        int ext_size = ybf.border;
        int ss_x = ybf.subsampling_x;
        int ss_y = ybf.subsampling_y;

        Debug.Assert(ybf.y_height - ybf.y_crop_height < 16);
        Debug.Assert(ybf.y_width - ybf.y_crop_width < 16);
        Debug.Assert(ybf.y_height - ybf.y_crop_height >= 0);
        Debug.Assert(ybf.y_width - ybf.y_crop_width >= 0);

        int is_uv = B(plane > 0);
        int top = ext_size >> (is_uv != 0 ? ss_y : 0);
        int left = ext_size >> (is_uv != 0 ? ss_x : 0);
        int bottom = top + ybf.heights[is_uv] - ybf.crop_heights[is_uv];
        int right = left + ybf.widths[is_uv] - ybf.crop_widths[is_uv];
        int extend_top_border = B(v_start == 0);
        int extend_bottom_border = B(v_end == ybf.crop_heights[is_uv]);

        // [subset] YV12_FLAG_HIGHBITDEPTH (extend_plane_high): CONFIG_AV1_HIGHBITDEPTH == 0.
        Debug.Assert((ybf.flags & YV12_FLAG_HIGHBITDEPTH) == 0);

        extend_plane(ybf.buffers[plane], ybf.strides[is_uv],
                     ybf.crop_widths[is_uv], ybf.crop_heights[is_uv],
                     extend_top_border != 0 ? top : 0, left,
                     extend_bottom_border != 0 ? bottom : 0, right, v_start, v_end);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_extend_frame_borders_plane_row(YV12_BUFFER_CONFIG ybf, int plane,
                                                            int v_start, int v_end) =>
        aom_extend_frame_borders_plane_row_c(ybf, plane, v_start, v_end);

    private static void extend_frame(YV12_BUFFER_CONFIG ybf, int ext_size,
                                     int num_planes)
    {
        int ss_x = ybf.subsampling_x;
        int ss_y = ybf.subsampling_y;

        Debug.Assert(ybf.y_height - ybf.y_crop_height < 16);
        Debug.Assert(ybf.y_width - ybf.y_crop_width < 16);
        Debug.Assert(ybf.y_height - ybf.y_crop_height >= 0);
        Debug.Assert(ybf.y_width - ybf.y_crop_width >= 0);

        // [subset] YV12_FLAG_HIGHBITDEPTH (extend_plane_high): CONFIG_AV1_HIGHBITDEPTH == 0.
        Debug.Assert((ybf.flags & YV12_FLAG_HIGHBITDEPTH) == 0);

        for (int plane = 0; plane < num_planes; ++plane)
        {
            int is_uv = B(plane > 0);
            int top = ext_size >> (is_uv != 0 ? ss_y : 0);
            int left = ext_size >> (is_uv != 0 ? ss_x : 0);
            int bottom = top + ybf.heights[is_uv] - ybf.crop_heights[is_uv];
            int right = left + ybf.widths[is_uv] - ybf.crop_widths[is_uv];
            extend_plane(ybf.buffers[plane], ybf.strides[is_uv],
                         ybf.crop_widths[is_uv], ybf.crop_heights[is_uv], top, left,
                         bottom, right, 0, ybf.crop_heights[is_uv]);
        }
    }

    internal static void aom_extend_frame_borders_c(YV12_BUFFER_CONFIG ybf, int num_planes)
    {
        extend_frame(ybf, ybf.border, num_planes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_extend_frame_borders(YV12_BUFFER_CONFIG ybf, int num_planes) =>
        aom_extend_frame_borders_c(ybf, num_planes);

    internal static void aom_yv12_copy_y_c(YV12_BUFFER_CONFIG src_ybc,
                                           YV12_BUFFER_CONFIG dst_ybc, int use_crop)
    {
        int row;
        int width = use_crop != 0 ? src_ybc.y_crop_width : src_ybc.y_width;
        int height = use_crop != 0 ? src_ybc.y_crop_height : src_ybc.y_height;
        Ptr<byte> src = src_ybc.y_buffer;
        Ptr<byte> dst = dst_ybc.y_buffer;

        // [subset] YV12_FLAG_HIGHBITDEPTH: CONFIG_AV1_HIGHBITDEPTH == 0.
        Debug.Assert((src_ybc.flags & YV12_FLAG_HIGHBITDEPTH) == 0);

        for (row = 0; row < height; ++row)
        {
            memcpy(dst, src, width);
            src += src_ybc.y_stride;
            dst += dst_ybc.y_stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_yv12_copy_y(YV12_BUFFER_CONFIG src_ybc, YV12_BUFFER_CONFIG dst_ybc,
                                         int use_crop) =>
        aom_yv12_copy_y_c(src_ybc, dst_ybc, use_crop);

    internal static void aom_yv12_copy_u_c(YV12_BUFFER_CONFIG src_bc,
                                           YV12_BUFFER_CONFIG dst_bc, int use_crop)
    {
        int row;
        int width = use_crop != 0 ? src_bc.uv_crop_width : src_bc.uv_width;
        int height = use_crop != 0 ? src_bc.uv_crop_height : src_bc.uv_height;
        Ptr<byte> src = src_bc.u_buffer;
        Ptr<byte> dst = dst_bc.u_buffer;
        // [subset] YV12_FLAG_HIGHBITDEPTH: CONFIG_AV1_HIGHBITDEPTH == 0.
        Debug.Assert((src_bc.flags & YV12_FLAG_HIGHBITDEPTH) == 0);
        for (row = 0; row < height; ++row)
        {
            memcpy(dst, src, width);
            src += src_bc.uv_stride;
            dst += dst_bc.uv_stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_yv12_copy_u(YV12_BUFFER_CONFIG src_bc, YV12_BUFFER_CONFIG dst_bc,
                                         int use_crop) =>
        aom_yv12_copy_u_c(src_bc, dst_bc, use_crop);

    internal static void aom_yv12_copy_v_c(YV12_BUFFER_CONFIG src_bc,
                                           YV12_BUFFER_CONFIG dst_bc, int use_crop)
    {
        int row;
        int width = use_crop != 0 ? src_bc.uv_crop_width : src_bc.uv_width;
        int height = use_crop != 0 ? src_bc.uv_crop_height : src_bc.uv_height;
        Ptr<byte> src = src_bc.v_buffer;
        Ptr<byte> dst = dst_bc.v_buffer;
        // [subset] YV12_FLAG_HIGHBITDEPTH: CONFIG_AV1_HIGHBITDEPTH == 0.
        Debug.Assert((src_bc.flags & YV12_FLAG_HIGHBITDEPTH) == 0);
        for (row = 0; row < height; ++row)
        {
            memcpy(dst, src, width);
            src += src_bc.uv_stride;
            dst += dst_bc.uv_stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_yv12_copy_v(YV12_BUFFER_CONFIG src_bc, YV12_BUFFER_CONFIG dst_bc,
                                         int use_crop) =>
        aom_yv12_copy_v_c(src_bc, dst_bc, use_crop);
}
