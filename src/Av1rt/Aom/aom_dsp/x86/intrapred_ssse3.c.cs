// Port of libaom aom_dsp/x86/intrapred_ssse3.c

global using static Av1rt.Aom.intrapred_ssse3;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [subset] Only SMOOTH_PRED (aom_smooth_predictor_*_ssse3 and their helpers) is ported. PAETH_PRED,
// SMOOTH_V_PRED and SMOOTH_H_PRED (and the smooth_v / smooth_h helpers after
// smooth_predictor_wxh) are never dispatched by the port. The AOM_FORCE_INLINE helpers LoadLo8,
// StoreLo8, cvtepu8_epi16 and cvtepu16_epi32 are shared by smooth_predictor_wxh and the
// smooth_v / smooth_h code; Load4, LoadUnaligned16, Store4, StoreUnaligned16 and cvtepu8_epi32
// are used only by smooth_v / smooth_h and are not ported.
internal static class intrapred_ssse3
{
    // [subset] PAETH_PRED: never dispatched.

    // -----------------------------------------------------------------------------
    // SMOOTH_PRED

    // pixels[0]: above and below_pred interleave vector
    // pixels[1]: left vector
    // pixels[2]: right_pred vector
    // [port] `_mm_cvtsi32_si128(((const int *)p)[0])` is _mm_loadu_si32(p).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void load_pixel_w4(Ptr<byte> above, Ptr<byte> left, int height, Span<__m128i> pixels)
    {
        __m128i d = _mm_loadu_si32(above);
        if (height == 4)
            pixels[1] = _mm_loadu_si32(left);
        else if (height == 8)
            pixels[1] = _mm_loadl_epi64(left);
        else
            pixels[1] = _mm_loadu_si128(left);

        pixels[2] = _mm_set1_epi16((short)above[3]);

        __m128i bp = _mm_set1_epi16((short)left[height - 1]);
        __m128i zero = _mm_setzero_si128();
        d = _mm_unpacklo_epi8(d, zero);
        pixels[0] = _mm_unpacklo_epi16(d, bp);
    }

    // weight_h[0]: weight_h vector
    // weight_h[1]: scale - weight_h vector
    // weight_h[2]: same as [0], second half for height = 16 only
    // weight_h[3]: same as [1], second half for height = 16 only
    // weight_w[0]: weights_w and scale - weights_w interleave vector
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void load_weight_w4(int height, Span<__m128i> weight_h, Span<__m128i> weight_w)
    {
        __m128i zero = _mm_setzero_si128();
        __m128i d = _mm_set1_epi16((short)(1 << SMOOTH_WEIGHT_LOG2_SCALE));
        __m128i t = _mm_loadu_si32(new Ptr<byte>(smooth_weights, 0));
        weight_h[0] = _mm_unpacklo_epi8(t, zero);
        weight_h[1] = _mm_sub_epi16(d, weight_h[0]);
        weight_w[0] = _mm_unpacklo_epi16(weight_h[0], weight_h[1]);

        if (height == 8)
        {
            __m128i weight = _mm_loadl_epi64(new Ptr<byte>(smooth_weights, 4));
            weight_h[0] = _mm_unpacklo_epi8(weight, zero);
            weight_h[1] = _mm_sub_epi16(d, weight_h[0]);
        }
        else if (height == 16)
        {
            __m128i weight = _mm_loadu_si128(new Ptr<byte>(smooth_weights, 12));
            weight_h[0] = _mm_unpacklo_epi8(weight, zero);
            weight_h[1] = _mm_sub_epi16(d, weight_h[0]);
            weight_h[2] = _mm_unpackhi_epi8(weight, zero);
            weight_h[3] = _mm_sub_epi16(d, weight_h[2]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void smooth_pred_4xh(ReadOnlySpan<__m128i> pixel, ReadOnlySpan<__m128i> wh,
                                        ReadOnlySpan<__m128i> ww, int h, Ptr<byte> dst,
                                        int stride, int second_half)
    {
        __m128i round = _mm_set1_epi32((1 << SMOOTH_WEIGHT_LOG2_SCALE));
        __m128i one = _mm_set1_epi16(1);
        __m128i inc = _mm_set1_epi16(0x202);
        __m128i gat = _mm_set1_epi32(0xc080400);
        __m128i rep = second_half != 0 ? _mm_set1_epi16(unchecked((short)0x8008))
                                       : _mm_set1_epi16(unchecked((short)0x8000));
        __m128i d = _mm_set1_epi16(0x100);

        for (int i = 0; i < h; ++i)
        {
            __m128i wg_wg = _mm_shuffle_epi8(wh[0], d);
            __m128i sc_sc = _mm_shuffle_epi8(wh[1], d);
            __m128i wh_sc = _mm_unpacklo_epi16(wg_wg, sc_sc);
            __m128i s = _mm_madd_epi16(pixel[0], wh_sc);

            __m128i b = _mm_shuffle_epi8(pixel[1], rep);
            b = _mm_unpacklo_epi16(b, pixel[2]);
            __m128i sum = _mm_madd_epi16(b, ww[0]);

            sum = _mm_add_epi32(s, sum);
            sum = _mm_add_epi32(sum, round);
            sum = _mm_srai_epi32(sum, 1 + SMOOTH_WEIGHT_LOG2_SCALE);

            sum = _mm_shuffle_epi8(sum, gat);
            _mm_storeu_si32(dst, sum);  // *(int *)dst = _mm_cvtsi128_si32(sum)
            dst += stride;

            rep = _mm_add_epi16(rep, one);
            d = _mm_add_epi16(d, inc);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_4x4_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        Arr3<__m128i> pixels = default;
        load_pixel_w4(above, left, 4, pixels);

        Arr4<__m128i> wh = default;
        Arr2<__m128i> ww = default;
        load_weight_w4(4, wh, ww);

        smooth_pred_4xh(pixels, wh, ww, 4, dst, stride, 0);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_4x8_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        Arr3<__m128i> pixels = default;
        load_pixel_w4(above, left, 8, pixels);

        Arr4<__m128i> wh = default;
        Arr2<__m128i> ww = default;
        load_weight_w4(8, wh, ww);

        smooth_pred_4xh(pixels, wh, ww, 8, dst, stride, 0);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_4x16_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        Arr3<__m128i> pixels = default;
        load_pixel_w4(above, left, 16, pixels);

        Arr4<__m128i> wh = default;
        Arr2<__m128i> ww = default;
        load_weight_w4(16, wh, ww);

        smooth_pred_4xh(pixels, wh, ww, 8, dst, stride, 0);
        dst += stride << 3;
        smooth_pred_4xh(pixels, wh[2..], ww, 8, dst, stride, 1);
    }

    // pixels[0]: above and below_pred interleave vector, first half
    // pixels[1]: above and below_pred interleave vector, second half
    // pixels[2]: left vector
    // pixels[3]: right_pred vector
    // pixels[4]: above and below_pred interleave vector, first half
    // pixels[5]: above and below_pred interleave vector, second half
    // pixels[6]: left vector + 16
    // pixels[7]: right_pred vector
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void load_pixel_w8(Ptr<byte> above, Ptr<byte> left, int height, Span<__m128i> pixels)
    {
        __m128i zero = _mm_setzero_si128();
        __m128i bp = _mm_set1_epi16((short)left[height - 1]);
        __m128i d = _mm_loadl_epi64(above);
        d = _mm_unpacklo_epi8(d, zero);
        pixels[0] = _mm_unpacklo_epi16(d, bp);
        pixels[1] = _mm_unpackhi_epi16(d, bp);

        pixels[3] = _mm_set1_epi16((short)above[7]);

        if (height == 4)
        {
            pixels[2] = _mm_loadu_si32(left);
        }
        else if (height == 8)
        {
            pixels[2] = _mm_loadl_epi64(left);
        }
        else if (height == 16)
        {
            pixels[2] = _mm_load_si128(left);
        }
        else
        {
            pixels[2] = _mm_load_si128(left);
            pixels[4] = pixels[0];
            pixels[5] = pixels[1];
            pixels[6] = _mm_load_si128(left + 16);
            pixels[7] = pixels[3];
        }
    }

    // weight_h[0]: weight_h vector
    // weight_h[1]: scale - weight_h vector
    // weight_h[2]: same as [0], offset 8
    // weight_h[3]: same as [1], offset 8
    // weight_h[4]: same as [0], offset 16
    // weight_h[5]: same as [1], offset 16
    // weight_h[6]: same as [0], offset 24
    // weight_h[7]: same as [1], offset 24
    // weight_w[0]: weights_w and scale - weights_w interleave vector, first half
    // weight_w[1]: weights_w and scale - weights_w interleave vector, second half
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void load_weight_w8(int height, Span<__m128i> weight_h, Span<__m128i> weight_w)
    {
        __m128i zero = _mm_setzero_si128();
        int we_offset = height < 8 ? 0 : 4;
        __m128i we = _mm_loadu_si128(new Ptr<byte>(smooth_weights, we_offset));
        weight_h[0] = _mm_unpacklo_epi8(we, zero);
        __m128i d = _mm_set1_epi16((short)(1 << SMOOTH_WEIGHT_LOG2_SCALE));
        weight_h[1] = _mm_sub_epi16(d, weight_h[0]);

        if (height == 4)
        {
            we = _mm_srli_si128(we, 4);
            __m128i tmp1 = _mm_unpacklo_epi8(we, zero);
            __m128i tmp2 = _mm_sub_epi16(d, tmp1);
            weight_w[0] = _mm_unpacklo_epi16(tmp1, tmp2);
            weight_w[1] = _mm_unpackhi_epi16(tmp1, tmp2);
        }
        else
        {
            weight_w[0] = _mm_unpacklo_epi16(weight_h[0], weight_h[1]);
            weight_w[1] = _mm_unpackhi_epi16(weight_h[0], weight_h[1]);
        }

        if (height == 16)
        {
            we = _mm_loadu_si128(new Ptr<byte>(smooth_weights, 12));
            weight_h[0] = _mm_unpacklo_epi8(we, zero);
            weight_h[1] = _mm_sub_epi16(d, weight_h[0]);
            weight_h[2] = _mm_unpackhi_epi8(we, zero);
            weight_h[3] = _mm_sub_epi16(d, weight_h[2]);
        }
        else if (height == 32)
        {
            __m128i weight_lo = _mm_loadu_si128(new Ptr<byte>(smooth_weights, 28));
            weight_h[0] = _mm_unpacklo_epi8(weight_lo, zero);
            weight_h[1] = _mm_sub_epi16(d, weight_h[0]);
            weight_h[2] = _mm_unpackhi_epi8(weight_lo, zero);
            weight_h[3] = _mm_sub_epi16(d, weight_h[2]);
            __m128i weight_hi = _mm_loadu_si128(new Ptr<byte>(smooth_weights, 28 + 16));
            weight_h[4] = _mm_unpacklo_epi8(weight_hi, zero);
            weight_h[5] = _mm_sub_epi16(d, weight_h[4]);
            weight_h[6] = _mm_unpackhi_epi8(weight_hi, zero);
            weight_h[7] = _mm_sub_epi16(d, weight_h[6]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void smooth_pred_8xh(ReadOnlySpan<__m128i> pixels, ReadOnlySpan<__m128i> wh,
                                        ReadOnlySpan<__m128i> ww, int h, Ptr<byte> dst,
                                        int stride, int second_half)
    {
        __m128i round = _mm_set1_epi32((1 << SMOOTH_WEIGHT_LOG2_SCALE));
        __m128i one = _mm_set1_epi16(1);
        __m128i inc = _mm_set1_epi16(0x202);
        __m128i gat = _mm_set_epi32(0, 0, 0xe0c0a08, 0x6040200);

        __m128i rep = second_half != 0 ? _mm_set1_epi16(unchecked((short)0x8008))
                                       : _mm_set1_epi16(unchecked((short)0x8000));
        __m128i d = _mm_set1_epi16(0x100);

        int i;
        for (i = 0; i < h; ++i)
        {
            __m128i wg_wg = _mm_shuffle_epi8(wh[0], d);
            __m128i sc_sc = _mm_shuffle_epi8(wh[1], d);
            __m128i wh_sc = _mm_unpacklo_epi16(wg_wg, sc_sc);
            __m128i s0 = _mm_madd_epi16(pixels[0], wh_sc);
            __m128i s1 = _mm_madd_epi16(pixels[1], wh_sc);

            __m128i b = _mm_shuffle_epi8(pixels[2], rep);
            b = _mm_unpacklo_epi16(b, pixels[3]);
            __m128i sum0 = _mm_madd_epi16(b, ww[0]);
            __m128i sum1 = _mm_madd_epi16(b, ww[1]);

            s0 = _mm_add_epi32(s0, sum0);
            s0 = _mm_add_epi32(s0, round);
            s0 = _mm_srai_epi32(s0, 1 + SMOOTH_WEIGHT_LOG2_SCALE);

            s1 = _mm_add_epi32(s1, sum1);
            s1 = _mm_add_epi32(s1, round);
            s1 = _mm_srai_epi32(s1, 1 + SMOOTH_WEIGHT_LOG2_SCALE);

            sum0 = _mm_packus_epi16(s0, s1);
            sum0 = _mm_shuffle_epi8(sum0, gat);
            _mm_storel_epi64(dst, sum0);
            dst += stride;

            rep = _mm_add_epi16(rep, one);
            d = _mm_add_epi16(d, inc);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_8x4_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        Arr4<__m128i> pixels = default;
        load_pixel_w8(above, left, 4, pixels);

        Arr4<__m128i> wh = default;
        Arr2<__m128i> ww = default;
        load_weight_w8(4, wh, ww);

        smooth_pred_8xh(pixels, wh, ww, 4, dst, stride, 0);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_8x8_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        Arr4<__m128i> pixels = default;
        load_pixel_w8(above, left, 8, pixels);

        Arr4<__m128i> wh = default;
        Arr2<__m128i> ww = default;
        load_weight_w8(8, wh, ww);

        smooth_pred_8xh(pixels, wh, ww, 8, dst, stride, 0);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_8x16_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        Arr4<__m128i> pixels = default;
        load_pixel_w8(above, left, 16, pixels);

        Arr4<__m128i> wh = default;
        Arr2<__m128i> ww = default;
        load_weight_w8(16, wh, ww);

        smooth_pred_8xh(pixels, wh, ww, 8, dst, stride, 0);
        dst += stride << 3;
        smooth_pred_8xh(pixels, wh[2..], ww, 8, dst, stride, 1);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_8x32_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        Arr8<__m128i> pixels = default;
        load_pixel_w8(above, left, 32, pixels);

        Arr8<__m128i> wh = default;
        Arr2<__m128i> ww = default;
        load_weight_w8(32, wh, ww);

        smooth_pred_8xh(pixels[0..], wh, ww, 8, dst, stride, 0);
        dst += stride << 3;
        smooth_pred_8xh(pixels[0..], wh[2..], ww, 8, dst, stride, 1);
        dst += stride << 3;
        smooth_pred_8xh(pixels[4..], wh[4..], ww, 8, dst, stride, 0);
        dst += stride << 3;
        smooth_pred_8xh(pixels[4..], wh[6..], ww, 8, dst, stride, 1);
    }

    // [port] LIBAOM_RESTRICT is dropped. Load4, LoadUnaligned16, Store4, StoreUnaligned16 and
    // cvtepu8_epi32 are only used by smooth_v / smooth_h (not ported).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i LoadLo8(Ptr<byte> a)
    {
        return _mm_loadl_epi64(a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreLo8(Ptr<byte> a, __m128i v)
    {
        _mm_storel_epi64(a, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i cvtepu8_epi16(__m128i x)
    {
        return _mm_unpacklo_epi8((x), _mm_setzero_si128());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i cvtepu16_epi32(__m128i x)
    {
        return _mm_unpacklo_epi16((x), _mm_setzero_si128());
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void smooth_predictor_wxh(Ptr<byte> dst, int stride, Ptr<byte> top_row,
                                             Ptr<byte> left_column, int width, int height)
    {
        Ptr<byte> sm_weights_h = new Ptr<byte>(smooth_weights, height - 4);
        Ptr<byte> sm_weights_w = new Ptr<byte>(smooth_weights, width - 4);
        __m128i zero = _mm_setzero_si128();
        __m128i scale_value = _mm_set1_epi16(1 << SMOOTH_WEIGHT_LOG2_SCALE);
        __m128i bottom_left = _mm_cvtsi32_si128(left_column[height - 1]);
        __m128i top_right = _mm_set1_epi16(top_row[width - 1]);
        __m128i round = _mm_set1_epi32(1 << SMOOTH_WEIGHT_LOG2_SCALE);
        for (int y = 0; y < height; ++y)
        {
            __m128i weights_y = _mm_cvtsi32_si128(sm_weights_h[y]);
            __m128i left_y = _mm_cvtsi32_si128(left_column[y]);
            __m128i scale_m_weights_y = _mm_sub_epi16(scale_value, weights_y);
            __m128i scaled_bottom_left =
                _mm_mullo_epi16(scale_m_weights_y, bottom_left);
            __m128i weight_left_y =
                _mm_shuffle_epi32(_mm_unpacklo_epi16(weights_y, left_y), 0);
            scaled_bottom_left = _mm_add_epi32(scaled_bottom_left, round);
            scaled_bottom_left = _mm_shuffle_epi32(scaled_bottom_left, 0);
            for (int x = 0; x < width; x += 8)
            {
                __m128i top_x = LoadLo8(top_row + x);
                __m128i weights_x = LoadLo8(sm_weights_w + x);
                __m128i top_weights_x = _mm_unpacklo_epi8(top_x, weights_x);
                __m128i top_weights_x_lo = cvtepu8_epi16(top_weights_x);
                __m128i top_weights_x_hi = _mm_unpackhi_epi8(top_weights_x, zero);

                // Here opposite weights and pixels are multiplied, where the order of
                // interleaving is indicated in the names.
                __m128i pred_lo = _mm_madd_epi16(top_weights_x_lo, weight_left_y);
                __m128i pred_hi = _mm_madd_epi16(top_weights_x_hi, weight_left_y);

                // |scaled_bottom_left| is always scaled by the same weight each row, so
                // we only derive |scaled_top_right| values here.
                __m128i inverted_weights_x =
                    _mm_sub_epi16(scale_value, cvtepu8_epi16(weights_x));
                __m128i scaled_top_right =
                    _mm_mullo_epi16(inverted_weights_x, top_right);
                __m128i scaled_top_right_lo = cvtepu16_epi32(scaled_top_right);
                __m128i scaled_top_right_hi =
                    _mm_unpackhi_epi16(scaled_top_right, zero);
                pred_lo = _mm_add_epi32(pred_lo, scaled_bottom_left);
                pred_hi = _mm_add_epi32(pred_hi, scaled_bottom_left);
                pred_lo = _mm_add_epi32(pred_lo, scaled_top_right_lo);
                pred_hi = _mm_add_epi32(pred_hi, scaled_top_right_hi);

                // The round value for RightShiftWithRounding was added with
                // |scaled_bottom_left|.
                pred_lo = _mm_srli_epi32(pred_lo, (1 + SMOOTH_WEIGHT_LOG2_SCALE));
                pred_hi = _mm_srli_epi32(pred_hi, (1 + SMOOTH_WEIGHT_LOG2_SCALE));
                __m128i pred = _mm_packus_epi16(pred_lo, pred_hi);
                StoreLo8(dst + x, _mm_packus_epi16(pred, pred));
            }
            dst += stride;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_16x4_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 16, 4);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_16x8_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 16, 8);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_16x16_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 16, 16);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_16x32_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 16, 32);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_16x64_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 16, 64);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_32x8_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 32, 8);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_32x16_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 32, 16);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_32x32_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 32, 32);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_32x64_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 32, 64);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_64x16_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 64, 16);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_64x32_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 64, 32);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_smooth_predictor_64x64_ssse3(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        smooth_predictor_wxh(dst, stride, above, left, 64, 64);
    }

    // [subset] Smooth horizontal/vertical helper functions, SMOOTH_V_PRED and SMOOTH_H_PRED:
    // never dispatched.
}
