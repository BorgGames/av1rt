// Port of libaom aom_dsp/x86/avg_intrin_sse2.c

global using static Av1rt.Aom.avg_intrin_sse2;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] Only the functions the port dispatches to (RTCD picks sse2 for them on an AVX2 machine)
// and their helpers. Not ported:
// - aom_minmax_8x8_sse2, aom_hadamard_4x4_sse2, aom_hadamard_8x8_sse2, aom_hadamard_16x16_sse2,
//   aom_hadamard_32x32_sse2, aom_satd_sse2 (and their helpers sign_extend_16bit_to_32bit_sse2,
//   invert_sign_32_sse2, hadamard_col4_sse2, hadamard_8x8_sse2, hadamard_16x16_sse2): their C
//   versions are not in the port (never executed in the subset, see avg.c.cs).
// - aom_avg_8x8_quad_sse2 / calc_avg_8x8_dual_sse2, aom_hadamard_lp_8x8_dual_sse2,
//   aom_hadamard_lp_16x16_sse2, aom_satd_lp_sse2, aom_int_pro_col_sse2: RTCD picks the avx2
//   versions (avg_intrin_avx2.c.cs).
// aom_int_pro_row_sse2 is ported because aom_int_pro_row_avx2 calls it for widths that are a
// multiple of 16 but not of 32.
internal static partial class avg_intrin_sse2
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_avg_8x8_sse2(Ptr<byte> s, int p)
    {
        __m128i sum0, sum1, s0, s1, s2, s3, u0;
        uint avg = 0;
        u0 = _mm_setzero_si128();
        s0 = loadh_epi64(s + p,
                         _mm_loadl_epi64(s));
        s1 = loadh_epi64(s + 3 * p,
                         _mm_loadl_epi64(s + 2 * p));
        s2 = loadh_epi64(s + 5 * p,
                         _mm_loadl_epi64(s + 4 * p));
        s3 = loadh_epi64(s + 7 * p,
                         _mm_loadl_epi64(s + 6 * p));
        s0 = _mm_sad_epu8(s0, u0);
        s1 = _mm_sad_epu8(s1, u0);
        s2 = _mm_sad_epu8(s2, u0);
        s3 = _mm_sad_epu8(s3, u0);

        sum0 = _mm_add_epi16(s0, s1);
        sum1 = _mm_add_epi16(s2, s3);
        sum0 = _mm_add_epi16(sum0, sum1);
        sum0 = _mm_add_epi16(sum0, _mm_srli_si128(sum0, 8));
        avg = (uint)_mm_cvtsi128_si32(sum0);
        return (avg + 32) >> 6;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_avg_4x4_sse2(Ptr<byte> s, int p)
    {
        __m128i s0, s1, u0;
        uint avg = 0;
        u0 = _mm_setzero_si128();
        s0 = _mm_unpacklo_epi32(xx_loadl_32(s), xx_loadl_32(s + p));
        s1 = _mm_unpacklo_epi32(xx_loadl_32(s + p * 2), xx_loadl_32(s + p * 3));
        s0 = _mm_sad_epu8(s0, u0);
        s1 = _mm_sad_epu8(s1, u0);
        s0 = _mm_add_epi16(s0, s1);
        avg = (uint)_mm_cvtsi128_si32(s0);
        return (avg + 8) >> 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void hadamard_col8_sse2(Span<__m128i> @in, int iter)
    {
        __m128i a0 = @in[0];
        __m128i a1 = @in[1];
        __m128i a2 = @in[2];
        __m128i a3 = @in[3];
        __m128i a4 = @in[4];
        __m128i a5 = @in[5];
        __m128i a6 = @in[6];
        __m128i a7 = @in[7];

        __m128i b0 = _mm_add_epi16(a0, a1);
        __m128i b1 = _mm_sub_epi16(a0, a1);
        __m128i b2 = _mm_add_epi16(a2, a3);
        __m128i b3 = _mm_sub_epi16(a2, a3);
        __m128i b4 = _mm_add_epi16(a4, a5);
        __m128i b5 = _mm_sub_epi16(a4, a5);
        __m128i b6 = _mm_add_epi16(a6, a7);
        __m128i b7 = _mm_sub_epi16(a6, a7);

        a0 = _mm_add_epi16(b0, b2);
        a1 = _mm_add_epi16(b1, b3);
        a2 = _mm_sub_epi16(b0, b2);
        a3 = _mm_sub_epi16(b1, b3);
        a4 = _mm_add_epi16(b4, b6);
        a5 = _mm_add_epi16(b5, b7);
        a6 = _mm_sub_epi16(b4, b6);
        a7 = _mm_sub_epi16(b5, b7);

        if (iter == 0)
        {
            b0 = _mm_add_epi16(a0, a4);
            b7 = _mm_add_epi16(a1, a5);
            b3 = _mm_add_epi16(a2, a6);
            b4 = _mm_add_epi16(a3, a7);
            b2 = _mm_sub_epi16(a0, a4);
            b6 = _mm_sub_epi16(a1, a5);
            b1 = _mm_sub_epi16(a2, a6);
            b5 = _mm_sub_epi16(a3, a7);

            a0 = _mm_unpacklo_epi16(b0, b1);
            a1 = _mm_unpacklo_epi16(b2, b3);
            a2 = _mm_unpackhi_epi16(b0, b1);
            a3 = _mm_unpackhi_epi16(b2, b3);
            a4 = _mm_unpacklo_epi16(b4, b5);
            a5 = _mm_unpacklo_epi16(b6, b7);
            a6 = _mm_unpackhi_epi16(b4, b5);
            a7 = _mm_unpackhi_epi16(b6, b7);

            b0 = _mm_unpacklo_epi32(a0, a1);
            b1 = _mm_unpacklo_epi32(a4, a5);
            b2 = _mm_unpackhi_epi32(a0, a1);
            b3 = _mm_unpackhi_epi32(a4, a5);
            b4 = _mm_unpacklo_epi32(a2, a3);
            b5 = _mm_unpacklo_epi32(a6, a7);
            b6 = _mm_unpackhi_epi32(a2, a3);
            b7 = _mm_unpackhi_epi32(a6, a7);

            @in[0] = _mm_unpacklo_epi64(b0, b1);
            @in[1] = _mm_unpackhi_epi64(b0, b1);
            @in[2] = _mm_unpacklo_epi64(b2, b3);
            @in[3] = _mm_unpackhi_epi64(b2, b3);
            @in[4] = _mm_unpacklo_epi64(b4, b5);
            @in[5] = _mm_unpackhi_epi64(b4, b5);
            @in[6] = _mm_unpacklo_epi64(b6, b7);
            @in[7] = _mm_unpackhi_epi64(b6, b7);
        }
        else
        {
            @in[0] = _mm_add_epi16(a0, a4);
            @in[7] = _mm_add_epi16(a1, a5);
            @in[3] = _mm_add_epi16(a2, a6);
            @in[4] = _mm_add_epi16(a3, a7);
            @in[2] = _mm_sub_epi16(a0, a4);
            @in[6] = _mm_sub_epi16(a1, a5);
            @in[1] = _mm_sub_epi16(a2, a6);
            @in[5] = _mm_sub_epi16(a3, a7);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void hadamard_lp_8x8_sse2(Ptr<short> src_diff,
                                             int src_stride, Ptr<short> coeff)
    {
        Span<__m128i> src = stackalloc __m128i[8];
        src[0] = _mm_load_si128(src_diff);
        src[1] = _mm_load_si128(src_diff += src_stride);
        src[2] = _mm_load_si128(src_diff += src_stride);
        src[3] = _mm_load_si128(src_diff += src_stride);
        src[4] = _mm_load_si128(src_diff += src_stride);
        src[5] = _mm_load_si128(src_diff += src_stride);
        src[6] = _mm_load_si128(src_diff += src_stride);
        src[7] = _mm_load_si128(src_diff + src_stride);

        hadamard_col8_sse2(src, 0);
        hadamard_col8_sse2(src, 1);

        _mm_store_si128(coeff, src[0]);
        coeff += 8;
        _mm_store_si128(coeff, src[1]);
        coeff += 8;
        _mm_store_si128(coeff, src[2]);
        coeff += 8;
        _mm_store_si128(coeff, src[3]);
        coeff += 8;
        _mm_store_si128(coeff, src[4]);
        coeff += 8;
        _mm_store_si128(coeff, src[5]);
        coeff += 8;
        _mm_store_si128(coeff, src[6]);
        coeff += 8;
        _mm_store_si128(coeff, src[7]);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_hadamard_lp_8x8_sse2(Ptr<short> src_diff, int src_stride,
                                                  Ptr<short> coeff)
    {
        hadamard_lp_8x8_sse2(src_diff, src_stride, coeff);
    }

    internal static void aom_int_pro_row_sse2(Ptr<short> hbuf, Ptr<byte> @ref,
                                              int ref_stride, int width,
                                              int height, int norm_factor)
    {
        // SIMD implementation assumes width and height to be multiple of 16 and 2
        // respectively. For any odd width or height, SIMD support needs to be added.
        Debug.Assert(width % 16 == 0 && height % 2 == 0);
        __m128i zero = _mm_setzero_si128();

        for (int wd = 0; wd < width; wd += 16)
        {
            Ptr<byte> ref_tmp = @ref + wd;
            Ptr<short> hbuf_tmp = hbuf + wd;
            __m128i s0 = zero;
            __m128i s1 = zero;
            int idx = 0;
            do
            {
                __m128i src_line = _mm_loadu_si128(ref_tmp);
                __m128i t0 = _mm_unpacklo_epi8(src_line, zero);
                __m128i t1 = _mm_unpackhi_epi8(src_line, zero);
                s0 = _mm_add_epi16(s0, t0);
                s1 = _mm_add_epi16(s1, t1);
                ref_tmp += ref_stride;

                src_line = _mm_loadu_si128(ref_tmp);
                t0 = _mm_unpacklo_epi8(src_line, zero);
                t1 = _mm_unpackhi_epi8(src_line, zero);
                s0 = _mm_add_epi16(s0, t0);
                s1 = _mm_add_epi16(s1, t1);
                ref_tmp += ref_stride;
                idx += 2;
            } while (idx < height);

            s0 = _mm_srai_epi16(s0, norm_factor);
            s1 = _mm_srai_epi16(s1, norm_factor);
            _mm_storeu_si128(hbuf_tmp, s0);
            _mm_storeu_si128(hbuf_tmp + 8, s1);
        }
    }
}
