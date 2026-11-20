// Port of libaom av1/encoder/x86/encodetxb_sse2.c

global using static Av1rt.Aom.encodetxb_sse2;

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Av1rt.Aom;

// [port] The file-local helpers take the caller's `const ptrdiff_t offsets[3]` as three ints, and
// the `__m128i level[5]` / `pos_to_offset[]` stack arrays are locals.
// Unlike av1_get_nz_map_contexts_c, which only writes the contexts of the first eob scan
// positions, this writes the whole width x height context block (coeff_contexts must hold
// MAX_TX_SQUARE entries); the extra entries are never read.
internal static partial class encodetxb_sse2
{
    // [port] C passes `__m128i level[5]` and `const ptrdiff_t *offsets` through memory; here the
    // five level vectors are values and the offsets three ints, so nothing leaves registers.
    // load_8bit_4x4_to_1_reg_sse2 on a span: four 4-byte rows.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i load_levels_4x4(ReadOnlySpan<byte> src, int pos, int stride)
    {
        return Vector128.Create(BinaryPrimitives.ReadInt32LittleEndian(src.Slice(pos, 4)),
                                BinaryPrimitives.ReadInt32LittleEndian(src.Slice(pos + stride, 4)),
                                BinaryPrimitives.ReadInt32LittleEndian(src.Slice(pos + 2 * stride, 4)),
                                BinaryPrimitives.ReadInt32LittleEndian(src.Slice(pos + 3 * stride, 4)))
            .AsInt64();
    }

    // load_8bit_8x2_to_1_reg_sse2 on a span: two 8-byte rows.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i load_levels_8x2(ReadOnlySpan<byte> src, int pos, int stride)
    {
        return Vector128.Create(BinaryPrimitives.ReadInt64LittleEndian(src.Slice(pos, 8)),
                                BinaryPrimitives.ReadInt64LittleEndian(src.Slice(pos + stride, 8)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i get_coeff_contexts_4x4x5_sse2(ReadOnlySpan<byte> src, int pos, int stride,
                                                         int o0, int o1, int o2)
    {
        return get_coeff_contexts_kernel_sse2(
            load_levels_4x4(src, pos + 1, stride),
            load_levels_4x4(src, pos + stride, stride),
            load_levels_4x4(src, pos + o0, stride),
            load_levels_4x4(src, pos + o1, stride),
            load_levels_4x4(src, pos + o2, stride));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i get_coeff_contexts_8x2x5_sse2(ReadOnlySpan<byte> src, int pos, int stride,
                                                         int o0, int o1, int o2)
    {
        return get_coeff_contexts_kernel_sse2(
            load_levels_8x2(src, pos + 1, stride),
            load_levels_8x2(src, pos + stride, stride),
            load_levels_8x2(src, pos + o0, stride),
            load_levels_8x2(src, pos + o1, stride),
            load_levels_8x2(src, pos + o2, stride));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i get_coeff_contexts_16x1x5_sse2(ReadOnlySpan<byte> src, int pos, int stride,
                                                          int o0, int o1, int o2)
    {
        return get_coeff_contexts_kernel_sse2(
            _mm_loadu_si128(src.Slice(pos + 1, 16)),
            _mm_loadu_si128(src.Slice(pos + stride, 16)),
            _mm_loadu_si128(src.Slice(pos + o0, 16)),
            _mm_loadu_si128(src.Slice(pos + o1, 16)),
            _mm_loadu_si128(src.Slice(pos + o2, 16)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i get_coeff_contexts_kernel_sse2(__m128i level0, __m128i level1,
                                                          __m128i level2, __m128i level3,
                                                          __m128i level4)
    {
        __m128i const_3 = _mm_set1_epi8(3);
        __m128i const_4 = _mm_set1_epi8(4);
        __m128i count;

        count = _mm_min_epu8(level0, const_3);
        level1 = _mm_min_epu8(level1, const_3);
        level2 = _mm_min_epu8(level2, const_3);
        level3 = _mm_min_epu8(level3, const_3);
        level4 = _mm_min_epu8(level4, const_3);
        count = _mm_add_epi8(count, level1);
        count = _mm_add_epi8(count, level2);
        count = _mm_add_epi8(count, level3);
        count = _mm_add_epi8(count, level4);
        count = _mm_avg_epu8(count, _mm_setzero_si128());
        count = _mm_min_epu8(count, const_4);
        return count;
    }

    private static void get_4_nz_map_contexts_2d(ReadOnlySpan<byte> levels,
                                                 int width,
                                                 int o0, int o1, int o2,
                                                 Span<sbyte> coeff_contexts)
    {
        const int stride = 4 + TX_PAD_HOR;
        int lp = 0;
        __m128i pos_to_offset_large = _mm_set1_epi8(21);
        __m128i pos_to_offset =
            (width == 4)
                ? _mm_setr_epi8(0, 1, 6, 6, 1, 6, 6, 21, 6, 6, 21, 21, 6, 21, 21, 21)
                : _mm_setr_epi8(0, 16, 16, 16, 16, 16, 16, 16, 6, 6, 21, 21, 6, 21,
                                21, 21);
        __m128i count;
        int cc = 0;
        int col = width;

        Debug.Assert((width % 4) == 0);

        do
        {
            count = get_coeff_contexts_4x4x5_sse2(levels, lp, stride, o0, o1, o2);
            count = _mm_add_epi8(count, pos_to_offset);
            _mm_store_si128(coeff_contexts.Slice(cc, 16), count);
            pos_to_offset = pos_to_offset_large;
            lp += 4 * stride;
            cc += 16;
            col -= 4;
        } while (col != 0);

        coeff_contexts[0] = 0;
    }

    private static void get_4_nz_map_contexts_ver(ReadOnlySpan<byte> levels,
                                                  int width,
                                                  int o0, int o1, int o2,
                                                  Span<sbyte> coeff_contexts)
    {
        int lp = 0;
        int cc = 0;
        const int stride = 4 + TX_PAD_HOR;
        __m128i pos_to_offset =
            _mm_setr_epi8(SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 5,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 5,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 5,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 5,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10);
        __m128i count;
        int col = width;

        Debug.Assert((width % 4) == 0);

        do
        {
            count = get_coeff_contexts_4x4x5_sse2(levels, lp, stride, o0, o1, o2);
            count = _mm_add_epi8(count, pos_to_offset);
            _mm_store_si128(coeff_contexts.Slice(cc, 16), count);
            lp += 4 * stride;
            cc += 16;
            col -= 4;
        } while (col != 0);
    }

    private static void get_4_nz_map_contexts_hor(ReadOnlySpan<byte> levels,
                                                  int width,
                                                  int o0, int o1, int o2,
                                                  Span<sbyte> coeff_contexts)
    {
        int lp = 0;
        int cc = 0;
        const int stride = 4 + TX_PAD_HOR;
        __m128i pos_to_offset_large = _mm_set1_epi8(SIG_COEF_CONTEXTS_2D + 10);
        __m128i pos_to_offset =
            _mm_setr_epi8(SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 0,
                          SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 0,
                          SIG_COEF_CONTEXTS_2D + 5, SIG_COEF_CONTEXTS_2D + 5,
                          SIG_COEF_CONTEXTS_2D + 5, SIG_COEF_CONTEXTS_2D + 5,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10);
        __m128i count;
        int col = width;

        Debug.Assert((width % 4) == 0);

        do
        {
            count = get_coeff_contexts_4x4x5_sse2(levels, lp, stride, o0, o1, o2);
            count = _mm_add_epi8(count, pos_to_offset);
            _mm_store_si128(coeff_contexts.Slice(cc, 16), count);
            pos_to_offset = pos_to_offset_large;
            lp += 4 * stride;
            cc += 16;
            col -= 4;
        } while (col != 0);
    }

    private static void get_8_coeff_contexts_2d(ReadOnlySpan<byte> levels,
                                                int width,
                                                int o0, int o1, int o2,
                                                Span<sbyte> coeff_contexts)
    {
        const int stride = 8 + TX_PAD_HOR;
        int lp = 0;
        int cc = 0;
        int col = width;
        __m128i count;
        __m128i pos_to_offset_0, pos_to_offset_1, pos_to_offset_2;

        Debug.Assert((width % 2) == 0);

        if (width == 8)
        {
            pos_to_offset_0 =
                _mm_setr_epi8(0, 1, 6, 6, 21, 21, 21, 21, 1, 6, 6, 21, 21, 21, 21, 21);
            pos_to_offset_1 = _mm_setr_epi8(6, 6, 21, 21, 21, 21, 21, 21, 6, 21, 21,
                                             21, 21, 21, 21, 21);
        }
        else if (width < 8)
        {
            pos_to_offset_0 = _mm_setr_epi8(0, 11, 6, 6, 21, 21, 21, 21, 11, 11, 6, 21,
                                             21, 21, 21, 21);
            pos_to_offset_1 = _mm_setr_epi8(11, 11, 21, 21, 21, 21, 21, 21, 11, 11, 21,
                                             21, 21, 21, 21, 21);
        }
        else
        {
            pos_to_offset_0 = _mm_setr_epi8(0, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16,
                                             16, 16, 16, 16, 16);
            pos_to_offset_1 = _mm_setr_epi8(6, 6, 21, 21, 21, 21, 21, 21, 6, 21, 21,
                                             21, 21, 21, 21, 21);
        }
        pos_to_offset_2 = _mm_set1_epi8(21);

        do
        {
            count = get_coeff_contexts_8x2x5_sse2(levels, lp, stride, o0, o1, o2);
            count = _mm_add_epi8(count, pos_to_offset_0);
            _mm_store_si128(coeff_contexts.Slice(cc, 16), count);
            pos_to_offset_0 = pos_to_offset_1;
            pos_to_offset_1 = pos_to_offset_2;
            lp += 2 * stride;
            cc += 16;
            col -= 2;
        } while (col != 0);

        coeff_contexts[0] = 0;
    }

    private static void get_8_coeff_contexts_ver(ReadOnlySpan<byte> levels,
                                                 int width,
                                                 int o0, int o1, int o2,
                                                 Span<sbyte> coeff_contexts)
    {
        int lp = 0;
        int cc = 0;
        const int stride = 8 + TX_PAD_HOR;
        __m128i pos_to_offset =
            _mm_setr_epi8(SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 5,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 5,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10);
        int col = width;
        __m128i count;

        Debug.Assert((width % 2) == 0);

        do
        {
            count = get_coeff_contexts_8x2x5_sse2(levels, lp, stride, o0, o1, o2);
            count = _mm_add_epi8(count, pos_to_offset);
            _mm_store_si128(coeff_contexts.Slice(cc, 16), count);
            lp += 2 * stride;
            cc += 16;
            col -= 2;
        } while (col != 0);
    }

    private static void get_8_coeff_contexts_hor(ReadOnlySpan<byte> levels,
                                                 int width,
                                                 int o0, int o1, int o2,
                                                 Span<sbyte> coeff_contexts)
    {
        int lp = 0;
        int cc = 0;
        const int stride = 8 + TX_PAD_HOR;
        __m128i pos_to_offset_large = _mm_set1_epi8(SIG_COEF_CONTEXTS_2D + 10);
        __m128i pos_to_offset =
            _mm_setr_epi8(SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 0,
                          SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 0,
                          SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 0,
                          SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 0,
                          SIG_COEF_CONTEXTS_2D + 5, SIG_COEF_CONTEXTS_2D + 5,
                          SIG_COEF_CONTEXTS_2D + 5, SIG_COEF_CONTEXTS_2D + 5,
                          SIG_COEF_CONTEXTS_2D + 5, SIG_COEF_CONTEXTS_2D + 5,
                          SIG_COEF_CONTEXTS_2D + 5, SIG_COEF_CONTEXTS_2D + 5);
        int col = width;
        __m128i count;

        Debug.Assert((width % 2) == 0);

        do
        {
            count = get_coeff_contexts_8x2x5_sse2(levels, lp, stride, o0, o1, o2);
            count = _mm_add_epi8(count, pos_to_offset);
            _mm_store_si128(coeff_contexts.Slice(cc, 16), count);
            pos_to_offset = pos_to_offset_large;
            lp += 2 * stride;
            cc += 16;
            col -= 2;
        } while (col != 0);
    }

    private static void get_16n_coeff_contexts_2d(ReadOnlySpan<byte> levels,
                                                  int real_width,
                                                  int real_height,
                                                  int width, int height,
                                                  int o0, int o1, int o2,
                                                  Span<sbyte> coeff_contexts)
    {
        int lp = 0;
        int stride = height + TX_PAD_HOR;
        int cc = 0;
        int col = width;
        __m128i pos_to_offset_0, pos_to_offset_1, pos_to_offset_2, pos_to_offset_3, pos_to_offset_4;
        __m128i pos_to_offset_large_0, pos_to_offset_large_1, pos_to_offset_large_2;
        __m128i count;

        Debug.Assert((height % 16) == 0);

        pos_to_offset_large_2 = _mm_set1_epi8(21);
        if (real_width == real_height)
        {
            pos_to_offset_0 = _mm_setr_epi8(0, 1, 6, 6, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21);
            pos_to_offset_1 = _mm_setr_epi8(1, 6, 6, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset_2 = _mm_setr_epi8(6, 6, 21, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset_3 = _mm_setr_epi8(6, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset_4 = pos_to_offset_large_0 = pos_to_offset_large_1 =
                pos_to_offset_large_2;
        }
        else if (real_width < real_height)
        {
            pos_to_offset_0 = _mm_setr_epi8(0, 11, 6, 6, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset_1 = _mm_setr_epi8(11, 11, 6, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset_2 = pos_to_offset_3 = pos_to_offset_4 = _mm_setr_epi8(
                11, 11, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21);
            pos_to_offset_large_0 = pos_to_offset_large_1 = pos_to_offset_large_2;
        }
        else
        {  // real_width > real_height
            pos_to_offset_0 = pos_to_offset_1 = _mm_setr_epi8(
                16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16);
            pos_to_offset_2 = _mm_setr_epi8(6, 6, 21, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset_3 = _mm_setr_epi8(6, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset_4 = pos_to_offset_large_2;
            pos_to_offset_large_0 = pos_to_offset_large_1 = _mm_set1_epi8(16);
        }

        do
        {
            int h = height;

            do
            {
                count = get_coeff_contexts_16x1x5_sse2(levels, lp, stride, o0, o1, o2);
                count = _mm_add_epi8(count, pos_to_offset_0);
                _mm_store_si128(coeff_contexts.Slice(cc, 16), count);
                lp += 16;
                cc += 16;
                h -= 16;
                pos_to_offset_0 = pos_to_offset_large_0;
            } while (h != 0);

            pos_to_offset_0 = pos_to_offset_1;
            pos_to_offset_1 = pos_to_offset_2;
            pos_to_offset_2 = pos_to_offset_3;
            pos_to_offset_3 = pos_to_offset_4;
            pos_to_offset_large_0 = pos_to_offset_large_1;
            pos_to_offset_large_1 = pos_to_offset_large_2;
            lp += TX_PAD_HOR;
        } while (--col != 0);

        coeff_contexts[0] = 0;
    }

    private static void get_16n_coeff_contexts_ver(ReadOnlySpan<byte> levels,
                                                   int width, int height,
                                                   int o0, int o1, int o2,
                                                   Span<sbyte> coeff_contexts)
    {
        int lp = 0;
        int cc = 0;
        int stride = height + TX_PAD_HOR;
        __m128i pos_to_offset_large =
            _mm_setr_epi8(SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                          SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10);
        __m128i count;
        int col = width;

        Debug.Assert((height % 16) == 0);

        do
        {
            __m128i pos_to_offset =
                _mm_setr_epi8(SIG_COEF_CONTEXTS_2D + 0, SIG_COEF_CONTEXTS_2D + 5,
                              SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                              SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                              SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                              SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                              SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                              SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10,
                              SIG_COEF_CONTEXTS_2D + 10, SIG_COEF_CONTEXTS_2D + 10);
            int h = height;

            do
            {
                count = get_coeff_contexts_16x1x5_sse2(levels, lp, stride, o0, o1, o2);
                count = _mm_add_epi8(count, pos_to_offset);
                _mm_store_si128(coeff_contexts.Slice(cc, 16), count);
                pos_to_offset = pos_to_offset_large;
                lp += 16;
                cc += 16;
                h -= 16;
            } while (h != 0);

            lp += TX_PAD_HOR;
        } while (--col != 0);
    }

    private static void get_16n_coeff_contexts_hor(ReadOnlySpan<byte> levels,
                                                   int width, int height,
                                                   int o0, int o1, int o2,
                                                   Span<sbyte> coeff_contexts)
    {
        int lp = 0;
        int cc = 0;
        int stride = height + TX_PAD_HOR;
        __m128i pos_to_offset_0, pos_to_offset_1, pos_to_offset_2;
        __m128i count;
        int col = width;

        Debug.Assert((height % 16) == 0);

        pos_to_offset_0 = _mm_set1_epi8(SIG_COEF_CONTEXTS_2D + 0);
        pos_to_offset_1 = _mm_set1_epi8(SIG_COEF_CONTEXTS_2D + 5);
        pos_to_offset_2 = _mm_set1_epi8(SIG_COEF_CONTEXTS_2D + 10);

        do
        {
            int h = height;

            do
            {
                count = get_coeff_contexts_16x1x5_sse2(levels, lp, stride, o0, o1, o2);
                count = _mm_add_epi8(count, pos_to_offset_0);
                _mm_store_si128(coeff_contexts.Slice(cc, 16), count);
                lp += 16;
                cc += 16;
                h -= 16;
            } while (h != 0);

            pos_to_offset_0 = pos_to_offset_1;
            pos_to_offset_1 = pos_to_offset_2;
            lp += TX_PAD_HOR;
        } while (--col != 0);
    }

    // Note: levels[] must be in the range [0, 127], inclusive.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void av1_get_nz_map_contexts_sse2(Ptr<byte> levels,
                                                      Ptr<short> scan, int eob,
                                                      TX_SIZE tx_size,
                                                      TX_CLASS tx_class,
                                                      Ptr<sbyte> coeff_contexts)
    {
        int last_idx = eob - 1;
        if (last_idx == 0)
        {
            coeff_contexts[0] = 0;
            return;
        }

        int real_width = tx_size_wide[tx_size];
        int real_height = tx_size_high[tx_size];
        int width = get_txb_wide(tx_size);
        int height = get_txb_high(tx_size);
        int stride = height + TX_PAD_HOR;
        int o0, o1, o2;
        ReadOnlySpan<byte> lv = levels.AsSpan();
        Span<sbyte> ccs = coeff_contexts.AsSpan();

        /* coeff_contexts must be 16 byte aligned. */
        // [port] (alignment is irrelevant for the bounds-checked stores)

        if (tx_class == TX_CLASS_2D)
        {
            o0 = 0 * stride + 2;
            o1 = 1 * stride + 1;
            o2 = 2 * stride + 0;

            if (height == 4)
            {
                get_4_nz_map_contexts_2d(lv, width, o0, o1, o2, ccs);
            }
            else if (height == 8)
            {
                get_8_coeff_contexts_2d(lv, width, o0, o1, o2, ccs);
            }
            else if (height == 16)
            {
                get_16n_coeff_contexts_2d(lv, real_width, real_height, width, height,
                                          o0, o1, o2, ccs);
            }
            else
            {
                get_16n_coeff_contexts_2d(lv, real_width, real_height, width, height,
                                          o0, o1, o2, ccs);
            }
        }
        else if (tx_class == TX_CLASS_HORIZ)
        {
            o0 = 2 * stride;
            o1 = 3 * stride;
            o2 = 4 * stride;
            if (height == 4)
            {
                get_4_nz_map_contexts_hor(lv, width, o0, o1, o2, ccs);
            }
            else if (height == 8)
            {
                get_8_coeff_contexts_hor(lv, width, o0, o1, o2, ccs);
            }
            else
            {
                get_16n_coeff_contexts_hor(lv, width, height, o0, o1, o2,
                                           ccs);
            }
        }
        else
        {  // TX_CLASS_VERT
            o0 = 2;
            o1 = 3;
            o2 = 4;
            if (height == 4)
            {
                get_4_nz_map_contexts_ver(lv, width, o0, o1, o2, ccs);
            }
            else if (height == 8)
            {
                get_8_coeff_contexts_ver(lv, width, o0, o1, o2, ccs);
            }
            else
            {
                get_16n_coeff_contexts_ver(lv, width, height, o0, o1, o2,
                                           ccs);
            }
        }

        int bhl = get_txb_bhl(tx_size);
        int pos = scan[last_idx];
        if (last_idx <= (width << bhl) / 8)
            coeff_contexts[pos] = 1;
        else if (last_idx <= (width << bhl) / 4)
            coeff_contexts[pos] = 2;
        else
            coeff_contexts[pos] = 3;
    }
}
