// Port of libaom aom_dsp/variance.c

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] variance.c's functions join the variance_h class of variance.h.cs (the class cannot be
// called `variance`: this file defines a function of that name). The VAR / SUBPIX_VAR /
// SUBPIX_AVG_VAR / MSE instantiations are expanded by tools/gen/dsp_sizes.py for the sizes
// variance.c compiles with CONFIG_REALTIME_ONLY; every _c function is followed by its
// aom_dsp_rtcd.h dispatched name as a forwarder. Besides the sizes the subset executes this covers
// every vf / svf / svaf entry of the encoder's fn_ptr table (encoder.c: BFP).
// [subset] Not ported:
// - aom_get_mb_ss_c and the OBMC variances: !CONFIG_REALTIME_ONLY.
// - every highbd_* function: CONFIG_AV1_HIGHBITDEPTH == 0.
// - aom_sse_odd_size, aom_mse_wxh_16bit_c, aom_mse_16xh_16bit_c (CDEF search distortion, unused
//   with the RT CDEF pick method), aom_calc_variance_stat_c (rdopt.c, not in the RT path): never
//   executed in the subset.
// - aom_comp_mask_pred_c and aom_masked_sub_pixel_variance*: masked compound prediction is off in
//   the subset (never executed); the fn_ptr table's msdf/msvf entries (encoder.c: MBFP) also need
//   aom_masked_sad* from sad_av1.c, which is outside every work package.
internal static partial class variance_h
{
    private static void variance(Ptr<byte> a, int a_stride, Ptr<byte> b,
                                 int b_stride, int w, int h, ref uint sse, ref int sum)
    {
        int i, j;
        int tsum = 0;
        uint tsse = 0;

        for (i = 0; i < h; ++i)
        {
            for (j = 0; j < w; ++j)
            {
                int diff = a[j] - b[j];
                tsum += diff;
                tsse += (uint)(diff * diff);
            }

            a += a_stride;
            b += b_stride;
        }
        sum = tsum;
        sse = tsse;
    }

    // Applies a 1-D 2-tap bilinear filter to the source block in either horizontal
    // or vertical direction to produce the filtered output block. Used to implement
    // the first-pass of 2-D separable filter.
    //
    // Produces int16_t output to retain precision for the next pass. Two filter
    // taps should sum to FILTER_WEIGHT. pixel_step defines whether the filter is
    // applied horizontally (pixel_step = 1) or vertically (pixel_step = stride).
    // It defines the offset required to move from one input to the next.
    private static void var_filter_block2d_bil_first_pass_c(
        Ptr<byte> a, Ptr<ushort> b, uint src_pixels_per_line,
        uint pixel_step, uint output_height,
        uint output_width, Ptr<byte> filter)
    {
        uint i, j;

        for (i = 0; i < output_height; ++i)
        {
            for (j = 0; j < output_width; ++j)
            {
                b[(int)j] = (ushort)ROUND_POWER_OF_TWO(
                    (int)a[0] * filter[0] + (int)a[(int)pixel_step] * filter[1], FILTER_BITS);

                ++a;
            }

            a += (int)(src_pixels_per_line - output_width);
            b += (int)output_width;
        }
    }

    // Applies a 1-D 2-tap bilinear filter to the source block in either horizontal
    // or vertical direction to produce the filtered output block. Used to implement
    // the second-pass of 2-D separable filter.
    //
    // Requires 16-bit input as produced by filter_block2d_bil_first_pass. Two
    // filter taps should sum to FILTER_WEIGHT. pixel_step defines whether the filter is
    // applied horizontally (pixel_step = 1) or vertically
    // (pixel_step = stride). It defines the offset required to move from one input
    // to the next. Output is 8-bit.
    private static void var_filter_block2d_bil_second_pass_c(
        Ptr<ushort> a, Ptr<byte> b, uint src_pixels_per_line,
        uint pixel_step, uint output_height,
        uint output_width, Ptr<byte> filter)
    {
        uint i, j;

        for (i = 0; i < output_height; ++i)
        {
            for (j = 0; j < output_width; ++j)
            {
                b[(int)j] = (byte)ROUND_POWER_OF_TWO(
                    (int)a[0] * filter[0] + (int)a[(int)pixel_step] * filter[1], FILTER_BITS);
                ++a;
            }

            a += (int)(src_pixels_per_line - output_width);
            b += (int)output_width;
        }
    }

    // [port] The stack arrays of SUBPIX_VAR / SUBPIX_AVG_VAR (`uint16_t fdata3[(H + 1) * W]`,
    // `uint8_t temp2[H * W]`, `temp3[H * W]`) are per-thread scratch buffers of the largest size
    // (PORTING 4): the functions are leaves and nothing they call re-enters them.
    [ThreadStatic] private static ushort[] s_fdata3;
    [ThreadStatic] private static byte[] s_temp2;
    [ThreadStatic] private static byte[] s_temp3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort[] fdata3_buf() => s_fdata3 ??= new ushort[(MAX_SB_SIZE + 1) * MAX_SB_SIZE];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] temp2_buf() => s_temp2 ??= new byte[MAX_SB_SIZE * MAX_SB_SIZE];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] temp3_buf() => s_temp3 ??= new byte[MAX_SB_SIZE * MAX_SB_SIZE];

    internal static void aom_get_var_sse_sum_8x8_quad_c(Ptr<byte> a, int a_stride,
                                                        Ptr<byte> b, int b_stride,
                                                        Ptr<uint> sse8x8, Ptr<int> sum8x8,
                                                        ref uint tot_sse, ref int tot_sum,
                                                        Ptr<uint> var8x8)
    {
        // Loop over 4 8x8 blocks. Process one 8x32 block.
        for (int k = 0; k < 4; k++)
        {
            variance(a + (k * 8), a_stride, b + (k * 8), b_stride, 8, 8, ref sse8x8[k],
                     ref sum8x8[k]);
        }

        // Calculate variance at 8x8 level and total sse, sum of 8x32 block.
        tot_sse += sse8x8[0] + sse8x8[1] + sse8x8[2] + sse8x8[3];
        tot_sum += sum8x8[0] + sum8x8[1] + sum8x8[2] + sum8x8[3];
        for (int i = 0; i < 4; i++)
            var8x8[i] = sse8x8[i] - (uint)(((long)sum8x8[i] * sum8x8[i]) >> 6);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_get_var_sse_sum_8x8_quad(Ptr<byte> src_ptr, int source_stride,
                                                      Ptr<byte> ref_ptr, int ref_stride,
                                                      Ptr<uint> sse8x8, Ptr<int> sum8x8,
                                                      ref uint tot_sse, ref int tot_sum,
                                                      Ptr<uint> var8x8) =>
        aom_get_var_sse_sum_8x8_quad_c(src_ptr, source_stride, ref_ptr, ref_stride, sse8x8,
                                       sum8x8, ref tot_sse, ref tot_sum, var8x8);

    internal static void aom_get_var_sse_sum_16x16_dual_c(Ptr<byte> src_ptr, int source_stride,
                                                          Ptr<byte> ref_ptr, int ref_stride,
                                                          Ptr<uint> sse16x16, ref uint tot_sse,
                                                          ref int tot_sum, Ptr<uint> var16x16)
    {
        Span<int> sum16x16 = stackalloc int[2];  // int sum16x16[2] = { 0 }
        // Loop over two consecutive 16x16 blocks and process as one 16x32 block.
        for (int k = 0; k < 2; k++)
        {
            variance(src_ptr + (k * 16), source_stride, ref_ptr + (k * 16), ref_stride,
                     16, 16, ref sse16x16[k], ref sum16x16[k]);
        }

        // Calculate variance at 16x16 level and total sse, sum of 16x32 block.
        tot_sse += sse16x16[0] + sse16x16[1];
        tot_sum += sum16x16[0] + sum16x16[1];
        for (int i = 0; i < 2; i++)
            var16x16[i] =
                sse16x16[i] - (uint)(((long)sum16x16[i] * sum16x16[i]) >> 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_get_var_sse_sum_16x16_dual(Ptr<byte> src_ptr, int source_stride,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        Ptr<uint> sse16x16, ref uint tot_sse,
                                                        ref int tot_sum, Ptr<uint> var16x16) =>
        aom_get_var_sse_sum_16x16_dual_c(src_ptr, source_stride, ref_ptr, ref_stride, sse16x16,
                                         ref tot_sse, ref tot_sum, var16x16);

    // <gen:variance_sizes>
    /* All three forms of the variance are available in the same sizes. */
    // VARIANCES(128, 128)
    internal static uint aom_variance128x128_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 128, 128, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (128 * 128));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance128x128(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance128x128_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance128x128_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 128 + 1, 128,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 128, 128, 128, 128,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance128x128_c(temp2, 128, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance128x128(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance128x128_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance128x128_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 128 + 1, 128,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 128, 128, 128, 128,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 128, 128, temp2, 128);

        return aom_variance128x128_c(temp3, 128, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance128x128(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance128x128_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(128, 64)
    internal static uint aom_variance128x64_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 128, 64, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (128 * 64));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance128x64(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance128x64_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance128x64_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 64 + 1, 128,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 128, 128, 64, 128,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance128x64_c(temp2, 128, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance128x64(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance128x64_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance128x64_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 64 + 1, 128,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 128, 128, 64, 128,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 128, 64, temp2, 128);

        return aom_variance128x64_c(temp3, 128, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance128x64(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance128x64_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(64, 128)
    internal static uint aom_variance64x128_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 64, 128, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (64 * 128));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance64x128(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance64x128_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance64x128_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 128 + 1, 64,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 64, 64, 128, 64,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance64x128_c(temp2, 64, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance64x128(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance64x128_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance64x128_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 128 + 1, 64,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 64, 64, 128, 64,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 64, 128, temp2, 64);

        return aom_variance64x128_c(temp3, 64, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance64x128(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance64x128_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(64, 64)
    internal static uint aom_variance64x64_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 64, 64, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (64 * 64));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance64x64(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance64x64_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance64x64_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 64 + 1, 64,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 64, 64, 64, 64,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance64x64_c(temp2, 64, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance64x64(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance64x64_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance64x64_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 64 + 1, 64,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 64, 64, 64, 64,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 64, 64, temp2, 64);

        return aom_variance64x64_c(temp3, 64, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance64x64(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance64x64_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(64, 32)
    internal static uint aom_variance64x32_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 64, 32, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (64 * 32));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance64x32(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance64x32_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance64x32_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 32 + 1, 64,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 64, 64, 32, 64,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance64x32_c(temp2, 64, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance64x32(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance64x32_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance64x32_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 32 + 1, 64,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 64, 64, 32, 64,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 64, 32, temp2, 64);

        return aom_variance64x32_c(temp3, 64, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance64x32(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance64x32_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(32, 64)
    internal static uint aom_variance32x64_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 32, 64, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (32 * 64));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance32x64(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance32x64_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance32x64_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 64 + 1, 32,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 32, 32, 64, 32,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance32x64_c(temp2, 32, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance32x64(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance32x64_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance32x64_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 64 + 1, 32,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 32, 32, 64, 32,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 32, 64, temp2, 32);

        return aom_variance32x64_c(temp3, 32, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance32x64(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance32x64_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(32, 32)
    internal static uint aom_variance32x32_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 32, 32, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (32 * 32));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance32x32(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance32x32_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance32x32_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 32 + 1, 32,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 32, 32, 32, 32,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance32x32_c(temp2, 32, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance32x32(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance32x32_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance32x32_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 32 + 1, 32,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 32, 32, 32, 32,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 32, 32, temp2, 32);

        return aom_variance32x32_c(temp3, 32, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance32x32(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance32x32_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(32, 16)
    internal static uint aom_variance32x16_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 32, 16, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (32 * 16));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance32x16(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance32x16_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance32x16_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 16 + 1, 32,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 32, 32, 16, 32,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance32x16_c(temp2, 32, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance32x16(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance32x16_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance32x16_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 16 + 1, 32,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 32, 32, 16, 32,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 32, 16, temp2, 32);

        return aom_variance32x16_c(temp3, 32, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance32x16(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance32x16_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(16, 32)
    internal static uint aom_variance16x32_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 16, 32, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (16 * 32));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance16x32(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance16x32_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance16x32_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 32 + 1, 16,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 16, 16, 32, 16,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance16x32_c(temp2, 16, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance16x32(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance16x32_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance16x32_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 32 + 1, 16,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 16, 16, 32, 16,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 16, 32, temp2, 16);

        return aom_variance16x32_c(temp3, 16, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance16x32(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance16x32_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(16, 16)
    internal static uint aom_variance16x16_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 16, 16, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (16 * 16));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance16x16(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance16x16_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance16x16_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 16 + 1, 16,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 16, 16, 16, 16,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance16x16_c(temp2, 16, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance16x16(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance16x16_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance16x16_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 16 + 1, 16,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 16, 16, 16, 16,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 16, 16, temp2, 16);

        return aom_variance16x16_c(temp3, 16, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance16x16(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance16x16_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(16, 8)
    internal static uint aom_variance16x8_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 16, 8, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (16 * 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance16x8(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance16x8_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance16x8_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 8 + 1, 16,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 16, 16, 8, 16,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance16x8_c(temp2, 16, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance16x8(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance16x8_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance16x8_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 8 + 1, 16,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 16, 16, 8, 16,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 16, 8, temp2, 16);

        return aom_variance16x8_c(temp3, 16, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance16x8(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance16x8_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(8, 16)
    internal static uint aom_variance8x16_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 8, 16, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (8 * 16));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance8x16(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance8x16_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance8x16_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 16 + 1, 8,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 8, 8, 16, 8,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance8x16_c(temp2, 8, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance8x16(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance8x16_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance8x16_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 16 + 1, 8,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 8, 8, 16, 8,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 8, 16, temp2, 8);

        return aom_variance8x16_c(temp3, 8, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance8x16(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance8x16_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(8, 8)
    internal static uint aom_variance8x8_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 8, 8, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (8 * 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance8x8(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance8x8_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance8x8_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 8 + 1, 8,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 8, 8, 8, 8,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance8x8_c(temp2, 8, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance8x8(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance8x8_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance8x8_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 8 + 1, 8,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 8, 8, 8, 8,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 8, 8, temp2, 8);

        return aom_variance8x8_c(temp3, 8, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance8x8(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance8x8_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(8, 4)
    internal static uint aom_variance8x4_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 8, 4, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (8 * 4));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance8x4(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance8x4_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance8x4_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 4 + 1, 8,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 8, 8, 4, 8,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance8x4_c(temp2, 8, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance8x4(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance8x4_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance8x4_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 4 + 1, 8,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 8, 8, 4, 8,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 8, 4, temp2, 8);

        return aom_variance8x4_c(temp3, 8, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance8x4(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance8x4_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(4, 8)
    internal static uint aom_variance4x8_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 4, 8, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (4 * 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance4x8(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance4x8_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance4x8_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 8 + 1, 4,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 4, 4, 8, 4,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance4x8_c(temp2, 4, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance4x8(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance4x8_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance4x8_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 8 + 1, 4,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 4, 4, 8, 4,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 4, 8, temp2, 4);

        return aom_variance4x8_c(temp3, 4, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance4x8(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance4x8_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    // VARIANCES(4, 4)
    internal static uint aom_variance4x4_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 4, 4, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / (4 * 4));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_variance4x4(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance4x4_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);

    internal static uint aom_sub_pixel_variance4x4_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 4 + 1, 4,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 4, 4, 4, 4,
                                             bilinear_filters_2t[yoffset]);

        return aom_variance4x4_c(temp2, 4, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_variance4x4(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance4x4_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);

    internal static uint aom_sub_pixel_avg_variance4x4_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, 4 + 1, 4,
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, 4, 4, 4, 4,
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, 4, 4, temp2, 4);

        return aom_variance4x4_c(temp3, 4, b, b_stride, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_sub_pixel_avg_variance4x4(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance4x4_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);

    /* Identical to the variance call except it does not calculate the
     * sse - sum^2 / w*h and returns sse in addtion to modifying the passed in
     * variable.
     */
    internal static uint aom_mse16x16_c(Ptr<byte> a, int a_stride,
                                       Ptr<byte> b, int b_stride,
                                       ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 16, 16, ref sse, ref sum);
        return sse;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_mse16x16(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                     int recon_stride, ref uint sse) =>
        aom_mse16x16_c(src_ptr, source_stride, ref_ptr, recon_stride, ref sse);

    internal static uint aom_mse16x8_c(Ptr<byte> a, int a_stride,
                                       Ptr<byte> b, int b_stride,
                                       ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 16, 8, ref sse, ref sum);
        return sse;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_mse16x8(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                     int recon_stride, ref uint sse) =>
        aom_mse16x8_c(src_ptr, source_stride, ref_ptr, recon_stride, ref sse);

    internal static uint aom_mse8x16_c(Ptr<byte> a, int a_stride,
                                       Ptr<byte> b, int b_stride,
                                       ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 8, 16, ref sse, ref sum);
        return sse;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_mse8x16(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                     int recon_stride, ref uint sse) =>
        aom_mse8x16_c(src_ptr, source_stride, ref_ptr, recon_stride, ref sse);

    internal static uint aom_mse8x8_c(Ptr<byte> a, int a_stride,
                                       Ptr<byte> b, int b_stride,
                                       ref uint sse)
    {
        int sum = 0;
        variance(a, a_stride, b, b_stride, 8, 8, ref sse, ref sum);
        return sse;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_mse8x8(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                     int recon_stride, ref uint sse) =>
        aom_mse8x8_c(src_ptr, source_stride, ref_ptr, recon_stride, ref sse);
    // </gen:variance_sizes>

    internal static void aom_comp_avg_pred_c(Ptr<byte> comp_pred, Ptr<byte> pred, int width,
                                             int height, Ptr<byte> @ref, int ref_stride)
    {
        int i, j;

        for (i = 0; i < height; ++i)
        {
            for (j = 0; j < width; ++j)
            {
                int tmp = pred[j] + @ref[j];
                comp_pred[j] = (byte)ROUND_POWER_OF_TWO(tmp, 1);
            }
            comp_pred += width;
            pred += width;
            @ref += ref_stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_comp_avg_pred(Ptr<byte> comp_pred, Ptr<byte> pred, int width,
                                           int height, Ptr<byte> @ref, int ref_stride) =>
        aom_comp_avg_pred_c(comp_pred, pred, width, height, @ref, ref_stride);
}
