// Port of libaom aom_dsp/x86/subpel_variance_ssse3.asm

global using static Av1rt.Aom.subpel_variance_ssse3;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] libaom's SSSE3 sub-pixel variance kernels are assembly (x86inc SUBPEL_VARIANCE). These
// are intrinsics versions with the asm symbols' names (minus the two unused PIC placeholder
// arguments) that compute exactly what the C path (var_filter_block2d_bil_first_pass /
// _second_pass, then aom_comp_avg_pred_c for _avg, then the variance sum / sse) computes:
// - bilinear taps are the asm's 16-scale ones (bilin_filter_m_ssse3): ((a*f0 + b*f1 + 8) >> 4)
//   == C's ((a*8f0 + b*8f1 + 64) >> 7); offset 4 (half) uses pavgb, (a + b + 1) >> 1, which is
//   the same value; offset 0 is the identity, as C's {128, 0} filter;
// - the horizontal pass runs first and its result is rounded to 8 bits before the vertical
//   pass, as in C (first pass writes uint16 values in [0, 255]);
// - _avg averages the filtered pixel with second_pred by pavgb == ROUND_POWER_OF_TWO(a + b, 1).
// Like the asm, width 16 handles one row per iteration and widths 8 / 4 two rows per register;
// the sum is accumulated in 16-bit lanes (no overflow for w <= 16, h <= 64, as the asm asserts)
// and returned, the sse stored. Reads stay within the (w + 1) x (h + 1) footprint of C.
// Only the variants the RTCD picks on an AVX2 machine are exposed: sub_pixel_variance{8,4}xh and
// sub_pixel_avg_variance{16,8,4}xh.
internal static class subpel_variance_ssse3
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    // bilin_filter_m_ssse3: times 8 db f0, f1 per offset (16 bytes each).
    private static ReadOnlySpan<byte> bilin_filter_m_ssse3 => new byte[] {
        16,  0, 16,  0, 16,  0, 16,  0, 16,  0, 16,  0, 16,  0, 16,  0,
        14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2,
        12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4,
        10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6,
         8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,
         6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,
         4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,
         2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,
    };

    // movx: movd for 4xh, movh for 8xh, movu for 16xh.
    [MethodImpl(AI)]
    private static __m128i movx(Ptr<byte> p, int w)
    {
        if (w == 4) return _mm_cvtsi32_si128(loadu_int32(p));
        if (w == 8) return _mm_loadl_epi64(p);
        return _mm_loadu_si128(p);
    }

    // Two rows of a 4 / 8 wide block in one register (row0 low, row1 above it).
    [MethodImpl(AI)]
    private static __m128i combine_rows(__m128i r0, __m128i r1, int w)
    {
        return w == 4 ? _mm_unpacklo_epi32(r0, r1) : _mm_unpacklo_epi64(r0, r1);
    }

    [MethodImpl(AI)]
    private static __m128i load_rows(Ptr<byte> p, int stride, int w)
    {
        if (w == 16) return _mm_loadu_si128(p);
        return combine_rows(movx(p, w), movx(p + stride, w), w);
    }

    // pmaddubsw / paddw pw_8 / psraw 4 on interleaved (a, b) pixel pairs.
    [MethodImpl(AI)]
    private static __m128i filter_pairs(__m128i ab, __m128i filter, __m128i pw_8)
    {
        return _mm_srai_epi16(_mm_add_epi16(_mm_maddubs_epi16(ab, filter), pw_8), 4);
    }

    // Bilinear filter of the first `n` pixels of a and b (n = 4, 8 or 16), packed back to bytes.
    [MethodImpl(AI)]
    private static __m128i filter_bytes(__m128i a, __m128i b, __m128i filter, __m128i pw_8, int n)
    {
        __m128i lo = filter_pairs(_mm_unpacklo_epi8(a, b), filter, pw_8);
        __m128i hi = n == 16 ? filter_pairs(_mm_unpackhi_epi8(a, b), filter, pw_8) : lo;
        return _mm_packus_epi16(lo, hi);
    }

    // Horizontal pass of one row: x_offset 0 (copy), 4 (pavgb) or bilinear.
    [MethodImpl(AI)]
    private static __m128i hrow(Ptr<byte> src, int w, int x_offset, __m128i filter_x, __m128i pw_8)
    {
        __m128i a = movx(src, w);
        if (x_offset == 0) return a;
        __m128i b = movx(src + 1, w);
        if (x_offset == 4) return _mm_avg_epu8(a, b);
        return filter_bytes(a, b, filter_x, pw_8, w);
    }

    // Vertical pass on registers holding `top` rows and the rows below them (bot).
    [MethodImpl(AI)]
    private static __m128i vfilter(__m128i top, __m128i bot, int y_offset, __m128i filter_y,
                                   __m128i pw_8, int n)
    {
        if (y_offset == 4) return _mm_avg_epu8(top, bot);
        return filter_bytes(top, bot, filter_y, pw_8, n);
    }

    // SUM_SSE on the `n` (8 or 16) filtered bytes against dst.
    [MethodImpl(AI)]
    private static void sum_sse(__m128i pred, __m128i dst, int n, ref __m128i sum, ref __m128i sse)
    {
        __m128i zero = _mm_setzero_si128();
        __m128i d0 = _mm_sub_epi16(_mm_unpacklo_epi8(pred, zero), _mm_unpacklo_epi8(dst, zero));
        sum = _mm_add_epi16(sum, d0);
        sse = _mm_add_epi32(sse, _mm_madd_epi16(d0, d0));
        if (n == 16)
        {
            __m128i d1 = _mm_sub_epi16(_mm_unpackhi_epi8(pred, zero), _mm_unpackhi_epi8(dst, zero));
            sum = _mm_add_epi16(sum, d1);
            sse = _mm_add_epi32(sse, _mm_madd_epi16(d1, d1));
        }
    }

    // STORE_AND_RET: sign-extend the word sums, reduce, store sse, return sum.
    [MethodImpl(AI)]
    private static int store_and_ret(__m128i sum, __m128i sse, ref uint sse_out)
    {
        __m128i sum32 = _mm_add_epi32(_mm_cvtepi16_epi32(sum), _mm_cvtepi16_epi32(_mm_srli_si128(sum, 8)));
        sum32 = _mm_add_epi32(sum32, _mm_srli_si128(sum32, 8));
        sum32 = _mm_add_epi32(sum32, _mm_srli_si128(sum32, 4));
        sse = _mm_add_epi32(sse, _mm_srli_si128(sse, 8));
        sse = _mm_add_epi32(sse, _mm_srli_si128(sse, 4));
        sse_out = (uint)_mm_cvtsi128_si32(sse);
        return _mm_cvtsi128_si32(sum32);
    }

    // SUBPEL_VARIANCE w, avg
    [MethodImpl(AI)]
    private static int SUBPEL_VARIANCE(int w, bool avg, Ptr<byte> src, int src_stride,
                                       int x_offset, int y_offset, Ptr<byte> dst, int dst_stride,
                                       Ptr<byte> sec, int sec_stride, int height, ref uint sse_out)
    {
        System.Diagnostics.Debug.Assert(w <= 16);  // m6 overflows if w > 16
        __m128i sum = _mm_setzero_si128();  // m6
        __m128i sse = _mm_setzero_si128();  // m7
        __m128i pw_8 = _mm_set1_epi16(8);
        __m128i filter_x = x_offset is 0 or 4
            ? _mm_setzero_si128()
            : _mm_loadu_si128(bilin_filter_m_ssse3.Slice(x_offset << 4));
        __m128i filter_y = y_offset is 0 or 4
            ? _mm_setzero_si128()
            : _mm_loadu_si128(bilin_filter_m_ssse3.Slice(y_offset << 4));
        // rows per iteration: 1 for 16xh, 2 for 8xh / 4xh (block_height >>= 1, sec_str <<= 1)
        int rows = w < 16 ? 2 : 1;
        int n = w * rows;  // valid bytes per register: 16, 16, 8

        if (y_offset == 0)
        {
            for (int i = 0; i < height; i += rows)
            {
                __m128i pred = hrow(src, w, x_offset, filter_x, pw_8);
                if (rows == 2)
                    pred = combine_rows(pred, hrow(src + src_stride, w, x_offset, filter_x, pw_8), w);
                if (avg)
                {
                    pred = _mm_avg_epu8(pred, load_rows(sec, sec_stride, w));
                    sec += sec_stride * rows;
                }
                sum_sse(pred, load_rows(dst, dst_stride, w), n, ref sum, ref sse);
                src += src_stride * rows;
                dst += dst_stride * rows;
            }
        }
        else
        {
            __m128i prev = hrow(src, w, x_offset, filter_x, pw_8);
            src += src_stride;
            for (int i = 0; i < height; i += rows)
            {
                __m128i top, bot;
                __m128i cur = hrow(src, w, x_offset, filter_x, pw_8);
                if (rows == 2)
                {
                    __m128i next = hrow(src + src_stride, w, x_offset, filter_x, pw_8);
                    top = combine_rows(prev, cur, w);
                    bot = combine_rows(cur, next, w);
                    prev = next;
                }
                else
                {
                    top = prev;
                    bot = cur;
                    prev = cur;
                }
                __m128i pred = vfilter(top, bot, y_offset, filter_y, pw_8, n);
                if (avg)
                {
                    pred = _mm_avg_epu8(pred, load_rows(sec, sec_stride, w));
                    sec += sec_stride * rows;
                }
                sum_sse(pred, load_rows(dst, dst_stride, w), n, ref sum, ref sse);
                src += src_stride * rows;
                dst += dst_stride * rows;
            }
        }
        return store_and_ret(sum, sse, ref sse_out);
    }

    // INIT_XMM ssse3 / SUBPEL_VARIANCE 4 / SUBPEL_VARIANCE 8
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static int aom_sub_pixel_variance4xh_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                        Ptr<byte> dst, int dst_stride, int height, ref uint sse) =>
        SUBPEL_VARIANCE(4, false, src, src_stride, x_offset, y_offset, dst, dst_stride, default, 0, height, ref sse);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static int aom_sub_pixel_variance8xh_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                        Ptr<byte> dst, int dst_stride, int height, ref uint sse) =>
        SUBPEL_VARIANCE(8, false, src, src_stride, x_offset, y_offset, dst, dst_stride, default, 0, height, ref sse);

    // SUBPEL_VARIANCE 4, 1 / SUBPEL_VARIANCE 8, 1 / SUBPEL_VARIANCE 16, 1
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static int aom_sub_pixel_avg_variance4xh_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                            Ptr<byte> dst, int dst_stride, Ptr<byte> sec, int sec_stride,
                                                            int height, ref uint sse) =>
        SUBPEL_VARIANCE(4, true, src, src_stride, x_offset, y_offset, dst, dst_stride, sec, sec_stride, height, ref sse);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static int aom_sub_pixel_avg_variance8xh_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                            Ptr<byte> dst, int dst_stride, Ptr<byte> sec, int sec_stride,
                                                            int height, ref uint sse) =>
        SUBPEL_VARIANCE(8, true, src, src_stride, x_offset, y_offset, dst, dst_stride, sec, sec_stride, height, ref sse);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static int aom_sub_pixel_avg_variance16xh_ssse3(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                             Ptr<byte> dst, int dst_stride, Ptr<byte> sec, int sec_stride,
                                                             int height, ref uint sse) =>
        SUBPEL_VARIANCE(16, true, src, src_stride, x_offset, y_offset, dst, dst_stride, sec, sec_stride, height, ref sse);
}
