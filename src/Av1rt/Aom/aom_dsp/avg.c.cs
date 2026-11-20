// Port of libaom aom_dsp/avg.c

global using static Av1rt.Aom.avg;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] Every _c function is followed by its aom_dsp_rtcd.h dispatched name as a forwarder.
// [subset] Not ported (never executed in the subset):
// - aom_minmax_8x8_c: var_based_part.c's compute_minmax_8x8 runs only with
//   compute_minmax_variance, which the RT speed features leave off.
// - hadamard_col4, aom_hadamard_4x4_c, aom_hadamard_8x8_c, aom_hadamard_16x16_c,
//   aom_hadamard_32x32_c, aom_satd_c: the tran_low_t variants serve high bitdepth and the
//   non-RT transform search; the 8-bit RT path uses the _lp versions.
// - every highbd_* function: CONFIG_AV1_HIGHBITDEPTH == 0.
internal static partial class avg
{
    internal static uint aom_avg_4x4_c(Ptr<byte> s, int p)
    {
        int i, j;
        int sum = 0;
        for (i = 0; i < 4; ++i, s += p)
            for (j = 0; j < 4; sum += s[j], ++j)
            {
            }

        return (uint)((sum + 8) >> 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_avg_4x4(Ptr<byte> s, int p) =>
        Simd.UseAvx2 ? aom_avg_4x4_sse2(s, p) : aom_avg_4x4_c(s, p);

    internal static uint aom_avg_8x8_c(Ptr<byte> s, int p)
    {
        int i, j;
        int sum = 0;
        for (i = 0; i < 8; ++i, s += p)
            for (j = 0; j < 8; sum += s[j], ++j)
            {
            }

        return (uint)((sum + 32) >> 6);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint aom_avg_8x8(Ptr<byte> s, int p) =>
        Simd.UseAvx2 ? aom_avg_8x8_sse2(s, p) : aom_avg_8x8_c(s, p);

    internal static void aom_avg_8x8_quad_c(Ptr<byte> s, int p, int x16_idx, int y16_idx,
                                            Ptr<int> avg)
    {
        for (int k = 0; k < 4; k++)
        {
            int x8_idx = x16_idx + ((k & 1) << 3);
            int y8_idx = y16_idx + ((k >> 1) << 3);
            Ptr<byte> s_tmp = s + y8_idx * p + x8_idx;
            avg[k] = (int)aom_avg_8x8_c(s_tmp, p);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_avg_8x8_quad(Ptr<byte> s, int p, int x16_idx, int y16_idx,
                                          Ptr<int> avg)
    {
        if (Simd.UseAvx2) aom_avg_8x8_quad_avx2(s, p, x16_idx, y16_idx, avg);
        else aom_avg_8x8_quad_c(s, p, x16_idx, y16_idx, avg);
    }

    // src_diff: first pass, 9 bit, dynamic range [-255, 255]
    //           second pass, 12 bit, dynamic range [-2040, 2040]
    private static void hadamard_col8(Ptr<short> src_diff, int src_stride,
                                      Ptr<short> coeff)
    {
        short b0 = (short)(src_diff[0 * src_stride] + src_diff[1 * src_stride]);
        short b1 = (short)(src_diff[0 * src_stride] - src_diff[1 * src_stride]);
        short b2 = (short)(src_diff[2 * src_stride] + src_diff[3 * src_stride]);
        short b3 = (short)(src_diff[2 * src_stride] - src_diff[3 * src_stride]);
        short b4 = (short)(src_diff[4 * src_stride] + src_diff[5 * src_stride]);
        short b5 = (short)(src_diff[4 * src_stride] - src_diff[5 * src_stride]);
        short b6 = (short)(src_diff[6 * src_stride] + src_diff[7 * src_stride]);
        short b7 = (short)(src_diff[6 * src_stride] - src_diff[7 * src_stride]);

        short c0 = (short)(b0 + b2);
        short c1 = (short)(b1 + b3);
        short c2 = (short)(b0 - b2);
        short c3 = (short)(b1 - b3);
        short c4 = (short)(b4 + b6);
        short c5 = (short)(b5 + b7);
        short c6 = (short)(b4 - b6);
        short c7 = (short)(b5 - b7);

        coeff[0] = (short)(c0 + c4);
        coeff[7] = (short)(c1 + c5);
        coeff[3] = (short)(c2 + c6);
        coeff[4] = (short)(c3 + c7);
        coeff[2] = (short)(c0 - c4);
        coeff[6] = (short)(c1 - c5);
        coeff[1] = (short)(c2 - c6);
        coeff[5] = (short)(c3 - c7);
    }

    // [port] `int16_t buffer[64], buffer2[64]` of aom_hadamard_lp_8x8_c: per-thread scratch
    // buffers (PORTING 4); the function is a leaf (hadamard_col8 only), and its callers
    // (aom_hadamard_lp_8x8_dual_c, aom_hadamard_lp_16x16_c) do not use them.
    [ThreadStatic] private static short[] s_hadamard_buffer;
    [ThreadStatic] private static short[] s_hadamard_buffer2;

    internal static void aom_hadamard_lp_8x8_c(Ptr<short> src_diff, int src_stride,
                                               Ptr<short> coeff)
    {
        short[] buffer = s_hadamard_buffer ??= new short[64];
        short[] buffer2 = s_hadamard_buffer2 ??= new short[64];
        Ptr<short> tmp_buf = buffer;
        for (int idx = 0; idx < 8; ++idx)
        {
            hadamard_col8(src_diff, src_stride, tmp_buf);  // src_diff: 9 bit
                                                           // dynamic range [-255, 255]
            tmp_buf += 8;
            ++src_diff;
        }

        tmp_buf = buffer;
        for (int idx = 0; idx < 8; ++idx)
        {
            hadamard_col8(tmp_buf, 8, new Ptr<short>(buffer2, 8 * idx));  // tmp_buf: 12 bit
            // dynamic range [-2040, 2040]
            // buffer2: 15 bit
            // dynamic range [-16320, 16320]
            ++tmp_buf;
        }

        for (int idx = 0; idx < 64; ++idx) coeff[idx] = buffer2[idx];

        // Extra transpose to match SSE2 behavior(i.e., aom_hadamard_lp_8x8_sse2).
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                coeff[i * 8 + j] = buffer2[j * 8 + i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_hadamard_lp_8x8(Ptr<short> src_diff, int src_stride,
                                             Ptr<short> coeff)
    {
        if (Simd.UseAvx2) aom_hadamard_lp_8x8_sse2(src_diff, src_stride, coeff);
        else aom_hadamard_lp_8x8_c(src_diff, src_stride, coeff);
    }

    internal static void aom_hadamard_lp_8x8_dual_c(Ptr<short> src_diff, int src_stride,
                                                    Ptr<short> coeff)
    {
        for (int i = 0; i < 2; i++)
        {
            aom_hadamard_lp_8x8_c(src_diff + (i * 8), src_stride,
                                  coeff + (i * 64));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_hadamard_lp_8x8_dual(Ptr<short> src_diff, int src_stride,
                                                  Ptr<short> coeff)
    {
        if (Simd.UseAvx2) aom_hadamard_lp_8x8_dual_avx2(src_diff, src_stride, coeff);
        else aom_hadamard_lp_8x8_dual_c(src_diff, src_stride, coeff);
    }

    internal static void aom_hadamard_lp_16x16_c(Ptr<short> src_diff, int src_stride,
                                                 Ptr<short> coeff)
    {
        for (int idx = 0; idx < 4; ++idx)
        {
            // src_diff: 9 bit, dynamic range [-255, 255]
            Ptr<short> src_ptr =
                src_diff + (idx >> 1) * 8 * src_stride + (idx & 0x01) * 8;
            aom_hadamard_lp_8x8_c(src_ptr, src_stride, coeff + idx * 64);
        }

        for (int idx = 0; idx < 64; ++idx)
        {
            short a0 = coeff[0];
            short a1 = coeff[64];
            short a2 = coeff[128];
            short a3 = coeff[192];

            short b0 = (short)((a0 + a1) >> 1);  // (a0 + a1): 16 bit, [-32640, 32640]
            short b1 = (short)((a0 - a1) >> 1);  // b0-b3: 15 bit, dynamic range
            short b2 = (short)((a2 + a3) >> 1);  // [-16320, 16320]
            short b3 = (short)((a2 - a3) >> 1);

            coeff[0] = (short)(b0 + b2);  // 16 bit, [-32640, 32640]
            coeff[64] = (short)(b1 + b3);
            coeff[128] = (short)(b0 - b2);
            coeff[192] = (short)(b1 - b3);

            ++coeff;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_hadamard_lp_16x16(Ptr<short> src_diff, int src_stride,
                                               Ptr<short> coeff)
    {
        if (Simd.UseAvx2) aom_hadamard_lp_16x16_avx2(src_diff, src_stride, coeff);
        else aom_hadamard_lp_16x16_c(src_diff, src_stride, coeff);
    }

    internal static int aom_satd_lp_c(Ptr<short> coeff, int length)
    {
        int satd = 0;
        for (int i = 0; i < length; ++i) satd += Math.Abs((int)coeff[i]);

        // satd: 26 bits, dynamic range [-32640 * 1024, 32640 * 1024]
        return satd;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int aom_satd_lp(Ptr<short> coeff, int length) =>
        Simd.UseAvx2 ? aom_satd_lp_avx2(coeff, length) : aom_satd_lp_c(coeff, length);

    // Integer projection onto row vectors.
    // height: value range {16, 32, 64, 128}.
    internal static void aom_int_pro_row_c(Ptr<short> hbuf, Ptr<byte> @ref, int ref_stride,
                                           int width, int height, int norm_factor)
    {
        Debug.Assert(height >= 2);
        for (int idx = 0; idx < width; ++idx)
        {
            hbuf[idx] = 0;
            // hbuf[idx]: 14 bit, dynamic range [0, 32640].
            for (int i = 0; i < height; ++i) hbuf[idx] += @ref[i * ref_stride];
            // hbuf[idx]: 9 bit, dynamic range [0, 1020].
            hbuf[idx] >>= norm_factor;
            ++@ref;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_int_pro_row(Ptr<short> hbuf, Ptr<byte> @ref, int ref_stride,
                                         int width, int height, int norm_factor)
    {
        if (Simd.UseAvx2) aom_int_pro_row_avx2(hbuf, @ref, ref_stride, width, height, norm_factor);
        else aom_int_pro_row_c(hbuf, @ref, ref_stride, width, height, norm_factor);
    }

    // width: value range {16, 32, 64, 128}.
    internal static void aom_int_pro_col_c(Ptr<short> vbuf, Ptr<byte> @ref, int ref_stride,
                                           int width, int height, int norm_factor)
    {
        for (int ht = 0; ht < height; ++ht)
        {
            short sum = 0;
            // sum: 14 bit, dynamic range [0, 32640]
            for (int idx = 0; idx < width; ++idx) sum += @ref[idx];
            vbuf[ht] = (short)(sum >> norm_factor);
            @ref += ref_stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_int_pro_col(Ptr<short> vbuf, Ptr<byte> @ref, int ref_stride,
                                         int width, int height, int norm_factor)
    {
        if (Simd.UseAvx2) aom_int_pro_col_avx2(vbuf, @ref, ref_stride, width, height, norm_factor);
        else aom_int_pro_col_c(vbuf, @ref, ref_stride, width, height, norm_factor);
    }

    // ref: [0 - 510]
    // src: [0 - 510]
    // bwl: {2, 3, 4, 5}
    internal static int aom_vector_var_c(Ptr<short> @ref, Ptr<short> src, int bwl)
    {
        int i;
        int width = 4 << bwl;
        int sse = 0, mean = 0, var;

        for (i = 0; i < width; ++i)
        {
            int diff = @ref[i] - src[i];  // diff: dynamic range [-510, 510], 10 bits.
            mean += diff;                 // mean: dynamic range 16 bits.
            sse += diff * diff;           // sse:  dynamic range 26 bits.
        }

        // (mean * mean): dynamic range 31 bits.
        // If width == 128, the mean can be 510 * 128 = 65280, and log2(65280 ** 2) ~=
        // 31.99, so it needs to be casted to unsigned int to compute its square.
        uint mean_abs = (uint)Math.Abs(mean);
        var = (int)((uint)sse - ((mean_abs * mean_abs) >> (bwl + 2)));
        return var;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int aom_vector_var(Ptr<short> @ref, Ptr<short> src, int bwl) =>
        Simd.UseAvx2 ? aom_vector_var_avx2(@ref, src, bwl) : aom_vector_var_c(@ref, src, bwl);
}
