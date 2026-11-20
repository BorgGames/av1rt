// Port of libaom aom_dsp/x86/variance_sse2.c

global using static Av1rt.Aom.variance_sse2;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] Only the part the RTCD picks on an AVX2 machine (CONFIG_REALTIME_ONLY,
// !CONFIG_AV1_HIGHBITDEPTH) is ported: aom_variance{4x4,4x8,8x4,8x8,8x16}_sse2 and
// aom_mse{8x8,8x16,16x8}_sse2 (plus aom_variance16x8_sse2, which aom_mse16x8_sse2 calls) and their
// helpers. Every wider variance, aom_mse16x16 and the quad/dual helpers use the AVX2 versions;
// aom_get_mb_ss_sse2 is !CONFIG_REALTIME_ONLY; aom_mse_wxh_16bit_sse2 / aom_mse_16xh_16bit_sse2
// have no C counterpart in the port (not executed in the subset).
internal static class variance_sse2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i load4x2_sse2(Ptr<byte> p, int stride)
    {
        __m128i p0 = _mm_cvtsi32_si128(loadu_int32(p + 0 * stride));
        __m128i p1 = _mm_cvtsi32_si128(loadu_int32(p + 1 * stride));
        return _mm_unpacklo_epi8(_mm_unpacklo_epi32(p0, p1), _mm_setzero_si128());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i load8_8to16_sse2(Ptr<byte> p)
    {
        __m128i p0 = _mm_loadl_epi64(p);
        return _mm_unpacklo_epi8(p0, _mm_setzero_si128());
    }

    // Accumulate 4 32bit numbers in val to 1 32bit number
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint add32x4_sse2(__m128i val)
    {
        val = _mm_add_epi32(val, _mm_srli_si128(val, 8));
        val = _mm_add_epi32(val, _mm_srli_si128(val, 4));
        return (uint)_mm_cvtsi128_si32(val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance_kernel_sse2(__m128i src, __m128i @ref,
                                             ref __m128i sse,
                                             ref __m128i sum)
    {
        __m128i diff = _mm_sub_epi16(src, @ref);
        sse = _mm_add_epi32(sse, _mm_madd_epi16(diff, diff));
        sum = _mm_add_epi16(sum, diff);
    }

    // Can handle 128 pixels' diff sum (such as 8x16 or 16x8)
    // Slightly faster than variance_final_256_pel_sse2()
    // diff sum of 128 pixels can still fit in 16bit integer
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance_final_128_pel_sse2(__m128i vsse, __m128i vsum,
                                                    ref uint sse,
                                                    ref int sum)
    {
        sse = add32x4_sse2(vsse);

        vsum = _mm_add_epi16(vsum, _mm_srli_si128(vsum, 8));
        vsum = _mm_add_epi16(vsum, _mm_srli_si128(vsum, 4));
        vsum = _mm_add_epi16(vsum, _mm_srli_si128(vsum, 2));
        sum = (short)_mm_extract_epi16(vsum, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance4_sse2(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride,
                                       int h, ref __m128i sse,
                                       ref __m128i sum)
    {
        Debug.Assert(h <= 256);  // May overflow for larger height.
        sum = _mm_setzero_si128();

        for (int i = 0; i < h; i += 2)
        {
            __m128i s = load4x2_sse2(src, src_stride);
            __m128i r = load4x2_sse2(@ref, ref_stride);

            variance_kernel_sse2(s, r, ref sse, ref sum);
            src += 2 * src_stride;
            @ref += 2 * ref_stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance8_sse2(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride,
                                       int h, ref __m128i sse,
                                       ref __m128i sum)
    {
        Debug.Assert(h <= 128);  // May overflow for larger height.
        sum = _mm_setzero_si128();
        sse = _mm_setzero_si128();
        for (int i = 0; i < h; i++)
        {
            __m128i s = load8_8to16_sse2(src);
            __m128i r = load8_8to16_sse2(@ref);

            variance_kernel_sse2(s, r, ref sse, ref sum);
            src += src_stride;
            @ref += ref_stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance16_kernel_sse2(Ptr<byte> src,
                                               Ptr<byte> @ref,
                                               ref __m128i sse,
                                               ref __m128i sum)
    {
        __m128i zero = _mm_setzero_si128();
        __m128i s = _mm_loadu_si128(src);
        __m128i r = _mm_loadu_si128(@ref);
        __m128i src0 = _mm_unpacklo_epi8(s, zero);
        __m128i ref0 = _mm_unpacklo_epi8(r, zero);
        __m128i src1 = _mm_unpackhi_epi8(s, zero);
        __m128i ref1 = _mm_unpackhi_epi8(r, zero);

        variance_kernel_sse2(src0, ref0, ref sse, ref sum);
        variance_kernel_sse2(src1, ref1, ref sse, ref sum);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance16_sse2(Ptr<byte> src, int src_stride,
                                        Ptr<byte> @ref, int ref_stride,
                                        int h, ref __m128i sse,
                                        ref __m128i sum)
    {
        Debug.Assert(h <= 64);  // May overflow for larger height.
        sum = _mm_setzero_si128();

        for (int i = 0; i < h; ++i)
        {
            variance16_kernel_sse2(src, @ref, ref sse, ref sum);
            src += src_stride;
            @ref += ref_stride;
        }
    }

    // AOM_VAR_NO_LOOP_SSE2(bw, bh, bits, max_pixels) with max_pixels = 128 (the only value the
    // ported instantiations use).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint AOM_VAR_NO_LOOP_SSE2_128(int bw, int bh, int bits,
                                                 Ptr<byte> src, int src_stride, Ptr<byte> @ref,
                                                 int ref_stride, ref uint sse)
    {
        __m128i vsse = _mm_setzero_si128();
        __m128i vsum = default;
        int sum = 0;
        if (bw == 4) variance4_sse2(src, src_stride, @ref, ref_stride, bh, ref vsse, ref vsum);
        else if (bw == 8) variance8_sse2(src, src_stride, @ref, ref_stride, bh, ref vsse, ref vsum);
        else variance16_sse2(src, src_stride, @ref, ref_stride, bh, ref vsse, ref vsum);
        variance_final_128_pel_sse2(vsse, vsum, ref sse, ref sum);
        Debug.Assert(sum <= 255 * bw * bh);
        Debug.Assert(sum >= -255 * bw * bh);
        return sse - (uint)(((long)sum * sum) >> bits);
    }

    internal static uint aom_variance4x4_sse2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse) =>
        AOM_VAR_NO_LOOP_SSE2_128(4, 4, 4, src, src_stride, @ref, ref_stride, ref sse);
    internal static uint aom_variance4x8_sse2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse) =>
        AOM_VAR_NO_LOOP_SSE2_128(4, 8, 5, src, src_stride, @ref, ref_stride, ref sse);

    internal static uint aom_variance8x4_sse2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse) =>
        AOM_VAR_NO_LOOP_SSE2_128(8, 4, 5, src, src_stride, @ref, ref_stride, ref sse);
    internal static uint aom_variance8x8_sse2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse) =>
        AOM_VAR_NO_LOOP_SSE2_128(8, 8, 6, src, src_stride, @ref, ref_stride, ref sse);
    internal static uint aom_variance8x16_sse2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse) =>
        AOM_VAR_NO_LOOP_SSE2_128(8, 16, 7, src, src_stride, @ref, ref_stride, ref sse);

    internal static uint aom_variance16x8_sse2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse) =>
        AOM_VAR_NO_LOOP_SSE2_128(16, 8, 7, src, src_stride, @ref, ref_stride, ref sse);

    internal static uint aom_mse8x8_sse2(Ptr<byte> src, int src_stride,
                                         Ptr<byte> @ref, int ref_stride,
                                         ref uint sse)
    {
        aom_variance8x8_sse2(src, src_stride, @ref, ref_stride, ref sse);
        return sse;
    }

    internal static uint aom_mse8x16_sse2(Ptr<byte> src, int src_stride,
                                          Ptr<byte> @ref, int ref_stride,
                                          ref uint sse)
    {
        aom_variance8x16_sse2(src, src_stride, @ref, ref_stride, ref sse);
        return sse;
    }

    internal static uint aom_mse16x8_sse2(Ptr<byte> src, int src_stride,
                                          Ptr<byte> @ref, int ref_stride,
                                          ref uint sse)
    {
        aom_variance16x8_sse2(src, src_stride, @ref, ref_stride, ref sse);
        return sse;
    }
}
