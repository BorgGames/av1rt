// Port of libaom av1/common/convolve.c

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class convolve
{
    // [subset] av1_convolve_horiz_rs_c: superres only (not in the subset).

    // [port] `int16_t im_block[(MAX_SB_SIZE + MAX_FILTER_TAP - 1) * MAX_SB_SIZE]`: per-thread
    // scratch (leaf function).
    [ThreadStatic] private static short[] s_im_block;

    internal static void av1_convolve_2d_sr_c(Ptr<byte> src, int src_stride, Ptr<byte> dst,
                                              int dst_stride, int w, int h,
                                              InterpFilterParams filter_params_x,
                                              InterpFilterParams filter_params_y,
                                              int subpel_x_qn, int subpel_y_qn,
                                              ref ConvolveParams conv_params)
    {
        Ptr<short> im_block =
            s_im_block ??= new short[(MAX_SB_SIZE + MAX_FILTER_TAP - 1) * MAX_SB_SIZE];
        int im_h = h + filter_params_y.taps - 1;
        int im_stride = w;
        Debug.Assert(w <= MAX_SB_SIZE && h <= MAX_SB_SIZE);
        int fo_vert = filter_params_y.taps / 2 - 1;
        int fo_horiz = filter_params_x.taps / 2 - 1;
        const int bd = 8;
        int bits =
            FILTER_BITS * 2 - conv_params.round_0 - conv_params.round_1;

        // horizontal filter
        Ptr<byte> src_horiz = src - fo_vert * src_stride;
        Ptr<short> x_filter = av1_get_interp_filter_subpel_kernel(
            filter_params_x, subpel_x_qn & SUBPEL_MASK);
        for (int y = 0; y < im_h; ++y)
        {
            for (int x = 0; x < w; ++x)
            {
                int sum = (1 << (bd + FILTER_BITS - 1));
                for (int k = 0; k < filter_params_x.taps; ++k)
                {
                    sum += x_filter[k] * src_horiz[y * src_stride + x - fo_horiz + k];
                }

                // TODO(aomedia:3393): for 12-tap filter, in extreme cases, the result can
                // be beyond the following range. For better prediction, a clamping can be
                // added for 12 tap filter to ensure the horizontal filtering result is
                // within 16 bit. The same applies to the vertical filtering.
                Debug.Assert(filter_params_x.taps > 8 ||
                             (0 <= sum && sum < (1 << (bd + FILTER_BITS + 1))));
                im_block[y * im_stride + x] =
                    (short)ROUND_POWER_OF_TWO(sum, conv_params.round_0);
            }
        }

        // vertical filter
        Ptr<short> src_vert = im_block + fo_vert * im_stride;
        Ptr<short> y_filter = av1_get_interp_filter_subpel_kernel(
            filter_params_y, subpel_y_qn & SUBPEL_MASK);
        int offset_bits = bd + 2 * FILTER_BITS - conv_params.round_0;
        for (int y = 0; y < h; ++y)
        {
            for (int x = 0; x < w; ++x)
            {
                int sum = 1 << offset_bits;
                for (int k = 0; k < filter_params_y.taps; ++k)
                {
                    sum += y_filter[k] * src_vert[(y - fo_vert + k) * im_stride + x];
                }
                Debug.Assert(filter_params_y.taps > 8 ||
                             (0 <= sum && sum < (1 << (offset_bits + 2))));
                short res = (short)(ROUND_POWER_OF_TWO(sum, conv_params.round_1) -
                                    ((1 << (offset_bits - conv_params.round_1)) +
                                     (1 << (offset_bits - conv_params.round_1 - 1))));
                dst[y * dst_stride + x] = clip_pixel(ROUND_POWER_OF_TWO(res, bits));
            }
        }
    }

    internal static void av1_convolve_2d_sr(Ptr<byte> src, int src_stride, Ptr<byte> dst,
                                            int dst_stride, int w, int h,
                                            InterpFilterParams filter_params_x,
                                            InterpFilterParams filter_params_y,
                                            int subpel_x_qn, int subpel_y_qn,
                                            ref ConvolveParams conv_params) =>
        av1_convolve_2d_sr_c(src, src_stride, dst, dst_stride, w, h, filter_params_x,
                             filter_params_y, subpel_x_qn, subpel_y_qn, ref conv_params);

    internal static void av1_convolve_y_sr_c(Ptr<byte> src, int src_stride, Ptr<byte> dst,
                                             int dst_stride, int w, int h,
                                             InterpFilterParams filter_params_y,
                                             int subpel_y_qn)
    {
        int fo_vert = filter_params_y.taps / 2 - 1;

        // vertical filter
        Ptr<short> y_filter = av1_get_interp_filter_subpel_kernel(
            filter_params_y, subpel_y_qn & SUBPEL_MASK);
        for (int y = 0; y < h; ++y)
        {
            for (int x = 0; x < w; ++x)
            {
                int res = 0;
                for (int k = 0; k < filter_params_y.taps; ++k)
                {
                    res += y_filter[k] * src[(y - fo_vert + k) * src_stride + x];
                }
                dst[y * dst_stride + x] =
                    clip_pixel(ROUND_POWER_OF_TWO(res, FILTER_BITS));
            }
        }
    }

    internal static void av1_convolve_y_sr(Ptr<byte> src, int src_stride, Ptr<byte> dst,
                                           int dst_stride, int w, int h,
                                           InterpFilterParams filter_params_y,
                                           int subpel_y_qn) =>
        av1_convolve_y_sr_c(src, src_stride, dst, dst_stride, w, h, filter_params_y,
                            subpel_y_qn);

    internal static void av1_convolve_x_sr_c(Ptr<byte> src, int src_stride, Ptr<byte> dst,
                                             int dst_stride, int w, int h,
                                             InterpFilterParams filter_params_x,
                                             int subpel_x_qn, ref ConvolveParams conv_params)
    {
        int fo_horiz = filter_params_x.taps / 2 - 1;
        int bits = FILTER_BITS - conv_params.round_0;

        Debug.Assert(bits >= 0);
        Debug.Assert((FILTER_BITS - conv_params.round_1) >= 0 ||
                     ((conv_params.round_0 + conv_params.round_1) == 2 * FILTER_BITS));

        // horizontal filter
        Ptr<short> x_filter = av1_get_interp_filter_subpel_kernel(
            filter_params_x, subpel_x_qn & SUBPEL_MASK);

        for (int y = 0; y < h; ++y)
        {
            for (int x = 0; x < w; ++x)
            {
                int res = 0;
                for (int k = 0; k < filter_params_x.taps; ++k)
                {
                    res += x_filter[k] * src[y * src_stride + x - fo_horiz + k];
                }
                res = ROUND_POWER_OF_TWO(res, conv_params.round_0);
                dst[y * dst_stride + x] = clip_pixel(ROUND_POWER_OF_TWO(res, bits));
            }
        }
    }

    internal static void av1_convolve_x_sr(Ptr<byte> src, int src_stride, Ptr<byte> dst,
                                           int dst_stride, int w, int h,
                                           InterpFilterParams filter_params_x,
                                           int subpel_x_qn, ref ConvolveParams conv_params) =>
        av1_convolve_x_sr_c(src, src_stride, dst, dst_stride, w, h, filter_params_x,
                            subpel_x_qn, ref conv_params);

    // [subset] av1_convolve_2d_sr_intrabc_c, av1_convolve_y_sr_intrabc_c,
    // av1_convolve_x_sr_intrabc_c: intra block copy is not used in the subset.
    // [subset] av1_dist_wtd_convolve_2d_c, av1_dist_wtd_convolve_y_c,
    // av1_dist_wtd_convolve_x_c, av1_dist_wtd_convolve_2d_copy_c, convolve_2d_facade_compound:
    // compound prediction is never built in the subset.
    // [subset] av1_convolve_2d_scale_c, convolve_2d_scale_wrapper: reference frames are never
    // scaled in the subset (no resize / superres).

    private static void convolve_2d_facade_single(
        Ptr<byte> src, int src_stride, Ptr<byte> dst, int dst_stride, int w,
        int h, InterpFilterParams filter_params_x,
        InterpFilterParams filter_params_y, int subpel_x_qn,
        int subpel_y_qn, ref ConvolveParams conv_params)
    {
        bool need_x = subpel_x_qn != 0;
        bool need_y = subpel_y_qn != 0;
        if (!need_x && !need_y)
        {
            aom_convolve_copy(src, src_stride, dst, dst_stride, w, h);
        }
        else if (need_x && !need_y)
        {
            av1_convolve_x_sr(src, src_stride, dst, dst_stride, w, h, filter_params_x,
                              subpel_x_qn, ref conv_params);
        }
        else if (!need_x && need_y)
        {
            av1_convolve_y_sr(src, src_stride, dst, dst_stride, w, h, filter_params_y,
                              subpel_y_qn);
        }
        else
        {
            Debug.Assert(need_x && need_y);
            av1_convolve_2d_sr(src, src_stride, dst, dst_stride, w, h, filter_params_x,
                               filter_params_y, subpel_x_qn, subpel_y_qn, ref conv_params);
        }
    }

    // PORT-NOTE: `const InterpFilterParams *interp_filters[2]` is passed as a span (see
    // InterPredParams.interp_filter_params in reconinter.h.cs).
    internal static void av1_convolve_2d_facade(Ptr<byte> src, int src_stride, Ptr<byte> dst,
                                                int dst_stride, int w, int h,
                                                ReadOnlySpan<InterpFilterParams> interp_filters,
                                                int subpel_x_qn, int x_step_q4,
                                                int subpel_y_qn, int y_step_q4, int scaled,
                                                ref ConvolveParams conv_params)
    {
        InterpFilterParams filter_params_x = interp_filters[0];
        InterpFilterParams filter_params_y = interp_filters[1];

        // TODO(jingning, yunqing): Add SIMD support to 2-tap filter case.
        // 2-tap filter indicates that it is for IntraBC.
        if (filter_params_x.taps == 2 || filter_params_y.taps == 2)
        {
            // [subset] IntraBC (2-tap bilinear) prediction: intra block copy is not used in the
            // subset.
            throw new NotSupportedException("av1_convolve_2d_facade: intrabc");
        }

        if (scaled != 0)
        {
            // [subset] Scaled prediction (convolve_2d_scale_wrapper): references are never
            // scaled in the subset.
            throw new NotSupportedException("av1_convolve_2d_facade: scaled reference");
        }
        else if (conv_params.is_compound != 0)
        {
            // [subset] Compound prediction (convolve_2d_facade_compound): never built in the
            // subset.
            throw new NotSupportedException("av1_convolve_2d_facade: compound");
        }
        else
        {
            convolve_2d_facade_single(src, src_stride, dst, dst_stride, w, h,
                                      filter_params_x, filter_params_y, subpel_x_qn,
                                      subpel_y_qn, ref conv_params);
        }
    }

    // [subset] av1_highbd_*: high bitdepth is compiled out.
    // [subset] horz_scalar_product, highbd_vert_scalar_product, get_filter_base,
    // get_filter_offset, convolve_add_src_horiz_hip, convolve_add_src_vert_hip,
    // av1_wiener_convolve_add_src_c: loop restoration is off.
}
