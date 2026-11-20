// Port of libaom av1/encoder/x86/encodetxb_sse2.c

global using static Av1rt.Aom.encodetxb_sse2;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] The file-local helpers take `const ptrdiff_t *offsets` (a 3-element stack array of the
// caller) as Span<int>, and `__m128i level[5]` / `pos_to_offset[]` arrays as Span<__m128i>.
// Unlike av1_get_nz_map_contexts_c, which only writes the contexts of the first eob scan
// positions, this writes the whole width x height context block (coeff_contexts must hold
// MAX_TX_SQUARE entries); the extra entries are never read.
internal static partial class encodetxb_sse2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void load_levels_4x4x5_sse2(Ptr<byte> src,
                                               int stride,
                                               Span<int> offsets,
                                               Span<__m128i> level)
    {
        level[0] = load_8bit_4x4_to_1_reg_sse2(src + 1, stride);
        level[1] = load_8bit_4x4_to_1_reg_sse2(src + stride, stride);
        level[2] = load_8bit_4x4_to_1_reg_sse2(src + offsets[0], stride);
        level[3] = load_8bit_4x4_to_1_reg_sse2(src + offsets[1], stride);
        level[4] = load_8bit_4x4_to_1_reg_sse2(src + offsets[2], stride);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void load_levels_8x2x5_sse2(Ptr<byte> src,
                                               int stride,
                                               Span<int> offsets,
                                               Span<__m128i> level)
    {
        level[0] = load_8bit_8x2_to_1_reg_sse2(src + 1, stride);
        level[1] = load_8bit_8x2_to_1_reg_sse2(src + stride, stride);
        level[2] = load_8bit_8x2_to_1_reg_sse2(src + offsets[0], stride);
        level[3] = load_8bit_8x2_to_1_reg_sse2(src + offsets[1], stride);
        level[4] = load_8bit_8x2_to_1_reg_sse2(src + offsets[2], stride);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void load_levels_16x1x5_sse2(Ptr<byte> src,
                                                int stride,
                                                Span<int> offsets,
                                                Span<__m128i> level)
    {
        level[0] = _mm_loadu_si128(src + 1);
        level[1] = _mm_loadu_si128(src + stride);
        level[2] = _mm_loadu_si128(src + offsets[0]);
        level[3] = _mm_loadu_si128(src + offsets[1]);
        level[4] = _mm_loadu_si128(src + offsets[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i get_coeff_contexts_kernel_sse2(Span<__m128i> level)
    {
        __m128i const_3 = _mm_set1_epi8(3);
        __m128i const_4 = _mm_set1_epi8(4);
        __m128i count;

        count = _mm_min_epu8(level[0], const_3);
        level[1] = _mm_min_epu8(level[1], const_3);
        level[2] = _mm_min_epu8(level[2], const_3);
        level[3] = _mm_min_epu8(level[3], const_3);
        level[4] = _mm_min_epu8(level[4], const_3);
        count = _mm_add_epi8(count, level[1]);
        count = _mm_add_epi8(count, level[2]);
        count = _mm_add_epi8(count, level[3]);
        count = _mm_add_epi8(count, level[4]);
        count = _mm_avg_epu8(count, _mm_setzero_si128());
        count = _mm_min_epu8(count, const_4);
        return count;
    }

    private static void get_4_nz_map_contexts_2d(Ptr<byte> levels,
                                                 int width,
                                                 Span<int> offsets,
                                                 Ptr<sbyte> coeff_contexts)
    {
        const int stride = 4 + TX_PAD_HOR;
        __m128i pos_to_offset_large = _mm_set1_epi8(21);
        __m128i pos_to_offset =
            (width == 4)
                ? _mm_setr_epi8(0, 1, 6, 6, 1, 6, 6, 21, 6, 6, 21, 21, 6, 21, 21, 21)
                : _mm_setr_epi8(0, 16, 16, 16, 16, 16, 16, 16, 6, 6, 21, 21, 6, 21,
                                21, 21);
        __m128i count;
        Span<__m128i> level = stackalloc __m128i[5];
        Ptr<sbyte> cc = coeff_contexts;
        int col = width;

        Debug.Assert((width % 4) == 0);

        do
        {
            load_levels_4x4x5_sse2(levels, stride, offsets, level);
            count = get_coeff_contexts_kernel_sse2(level);
            count = _mm_add_epi8(count, pos_to_offset);
            _mm_store_si128(cc, count);
            pos_to_offset = pos_to_offset_large;
            levels += 4 * stride;
            cc += 16;
            col -= 4;
        } while (col != 0);

        coeff_contexts[0] = 0;
    }

    private static void get_4_nz_map_contexts_ver(Ptr<byte> levels,
                                                  int width,
                                                  Span<int> offsets,
                                                  Ptr<sbyte> coeff_contexts)
    {
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
        Span<__m128i> level = stackalloc __m128i[5];
        int col = width;

        Debug.Assert((width % 4) == 0);

        do
        {
            load_levels_4x4x5_sse2(levels, stride, offsets, level);
            count = get_coeff_contexts_kernel_sse2(level);
            count = _mm_add_epi8(count, pos_to_offset);
            _mm_store_si128(coeff_contexts, count);
            levels += 4 * stride;
            coeff_contexts += 16;
            col -= 4;
        } while (col != 0);
    }

    private static void get_4_nz_map_contexts_hor(Ptr<byte> levels,
                                                  int width,
                                                  Span<int> offsets,
                                                  Ptr<sbyte> coeff_contexts)
    {
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
        Span<__m128i> level = stackalloc __m128i[5];
        int col = width;

        Debug.Assert((width % 4) == 0);

        do
        {
            load_levels_4x4x5_sse2(levels, stride, offsets, level);
            count = get_coeff_contexts_kernel_sse2(level);
            count = _mm_add_epi8(count, pos_to_offset);
            _mm_store_si128(coeff_contexts, count);
            pos_to_offset = pos_to_offset_large;
            levels += 4 * stride;
            coeff_contexts += 16;
            col -= 4;
        } while (col != 0);
    }

    private static void get_8_coeff_contexts_2d(Ptr<byte> levels,
                                                int width,
                                                Span<int> offsets,
                                                Ptr<sbyte> coeff_contexts)
    {
        const int stride = 8 + TX_PAD_HOR;
        Ptr<sbyte> cc = coeff_contexts;
        int col = width;
        __m128i count;
        Span<__m128i> level = stackalloc __m128i[5];
        Span<__m128i> pos_to_offset = stackalloc __m128i[3];

        Debug.Assert((width % 2) == 0);

        if (width == 8)
        {
            pos_to_offset[0] =
                _mm_setr_epi8(0, 1, 6, 6, 21, 21, 21, 21, 1, 6, 6, 21, 21, 21, 21, 21);
            pos_to_offset[1] = _mm_setr_epi8(6, 6, 21, 21, 21, 21, 21, 21, 6, 21, 21,
                                             21, 21, 21, 21, 21);
        }
        else if (width < 8)
        {
            pos_to_offset[0] = _mm_setr_epi8(0, 11, 6, 6, 21, 21, 21, 21, 11, 11, 6, 21,
                                             21, 21, 21, 21);
            pos_to_offset[1] = _mm_setr_epi8(11, 11, 21, 21, 21, 21, 21, 21, 11, 11, 21,
                                             21, 21, 21, 21, 21);
        }
        else
        {
            pos_to_offset[0] = _mm_setr_epi8(0, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16,
                                             16, 16, 16, 16, 16);
            pos_to_offset[1] = _mm_setr_epi8(6, 6, 21, 21, 21, 21, 21, 21, 6, 21, 21,
                                             21, 21, 21, 21, 21);
        }
        pos_to_offset[2] = _mm_set1_epi8(21);

        do
        {
            load_levels_8x2x5_sse2(levels, stride, offsets, level);
            count = get_coeff_contexts_kernel_sse2(level);
            count = _mm_add_epi8(count, pos_to_offset[0]);
            _mm_store_si128(cc, count);
            pos_to_offset[0] = pos_to_offset[1];
            pos_to_offset[1] = pos_to_offset[2];
            levels += 2 * stride;
            cc += 16;
            col -= 2;
        } while (col != 0);

        coeff_contexts[0] = 0;
    }

    private static void get_8_coeff_contexts_ver(Ptr<byte> levels,
                                                 int width,
                                                 Span<int> offsets,
                                                 Ptr<sbyte> coeff_contexts)
    {
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
        Span<__m128i> level = stackalloc __m128i[5];

        Debug.Assert((width % 2) == 0);

        do
        {
            load_levels_8x2x5_sse2(levels, stride, offsets, level);
            count = get_coeff_contexts_kernel_sse2(level);
            count = _mm_add_epi8(count, pos_to_offset);
            _mm_store_si128(coeff_contexts, count);
            levels += 2 * stride;
            coeff_contexts += 16;
            col -= 2;
        } while (col != 0);
    }

    private static void get_8_coeff_contexts_hor(Ptr<byte> levels,
                                                 int width,
                                                 Span<int> offsets,
                                                 Ptr<sbyte> coeff_contexts)
    {
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
        Span<__m128i> level = stackalloc __m128i[5];

        Debug.Assert((width % 2) == 0);

        do
        {
            load_levels_8x2x5_sse2(levels, stride, offsets, level);
            count = get_coeff_contexts_kernel_sse2(level);
            count = _mm_add_epi8(count, pos_to_offset);
            _mm_store_si128(coeff_contexts, count);
            pos_to_offset = pos_to_offset_large;
            levels += 2 * stride;
            coeff_contexts += 16;
            col -= 2;
        } while (col != 0);
    }

    private static void get_16n_coeff_contexts_2d(Ptr<byte> levels,
                                                  int real_width,
                                                  int real_height,
                                                  int width, int height,
                                                  Span<int> offsets,
                                                  Ptr<sbyte> coeff_contexts)
    {
        int stride = height + TX_PAD_HOR;
        Ptr<sbyte> cc = coeff_contexts;
        int col = width;
        Span<__m128i> pos_to_offset = stackalloc __m128i[5];
        Span<__m128i> pos_to_offset_large = stackalloc __m128i[3];
        __m128i count;
        Span<__m128i> level = stackalloc __m128i[5];

        Debug.Assert((height % 16) == 0);

        pos_to_offset_large[2] = _mm_set1_epi8(21);
        if (real_width == real_height)
        {
            pos_to_offset[0] = _mm_setr_epi8(0, 1, 6, 6, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21);
            pos_to_offset[1] = _mm_setr_epi8(1, 6, 6, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset[2] = _mm_setr_epi8(6, 6, 21, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset[3] = _mm_setr_epi8(6, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset[4] = pos_to_offset_large[0] = pos_to_offset_large[1] =
                pos_to_offset_large[2];
        }
        else if (real_width < real_height)
        {
            pos_to_offset[0] = _mm_setr_epi8(0, 11, 6, 6, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset[1] = _mm_setr_epi8(11, 11, 6, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset[2] = pos_to_offset[3] = pos_to_offset[4] = _mm_setr_epi8(
                11, 11, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21);
            pos_to_offset_large[0] = pos_to_offset_large[1] = pos_to_offset_large[2];
        }
        else
        {  // real_width > real_height
            pos_to_offset[0] = pos_to_offset[1] = _mm_setr_epi8(
                16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16);
            pos_to_offset[2] = _mm_setr_epi8(6, 6, 21, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset[3] = _mm_setr_epi8(6, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21,
                                             21, 21, 21, 21, 21);
            pos_to_offset[4] = pos_to_offset_large[2];
            pos_to_offset_large[0] = pos_to_offset_large[1] = _mm_set1_epi8(16);
        }

        do
        {
            int h = height;

            do
            {
                load_levels_16x1x5_sse2(levels, stride, offsets, level);
                count = get_coeff_contexts_kernel_sse2(level);
                count = _mm_add_epi8(count, pos_to_offset[0]);
                _mm_store_si128(cc, count);
                levels += 16;
                cc += 16;
                h -= 16;
                pos_to_offset[0] = pos_to_offset_large[0];
            } while (h != 0);

            pos_to_offset[0] = pos_to_offset[1];
            pos_to_offset[1] = pos_to_offset[2];
            pos_to_offset[2] = pos_to_offset[3];
            pos_to_offset[3] = pos_to_offset[4];
            pos_to_offset_large[0] = pos_to_offset_large[1];
            pos_to_offset_large[1] = pos_to_offset_large[2];
            levels += TX_PAD_HOR;
        } while (--col != 0);

        coeff_contexts[0] = 0;
    }

    private static void get_16n_coeff_contexts_ver(Ptr<byte> levels,
                                                   int width, int height,
                                                   Span<int> offsets,
                                                   Ptr<sbyte> coeff_contexts)
    {
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
        Span<__m128i> level = stackalloc __m128i[5];
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
                load_levels_16x1x5_sse2(levels, stride, offsets, level);
                count = get_coeff_contexts_kernel_sse2(level);
                count = _mm_add_epi8(count, pos_to_offset);
                _mm_store_si128(coeff_contexts, count);
                pos_to_offset = pos_to_offset_large;
                levels += 16;
                coeff_contexts += 16;
                h -= 16;
            } while (h != 0);

            levels += TX_PAD_HOR;
        } while (--col != 0);
    }

    private static void get_16n_coeff_contexts_hor(Ptr<byte> levels,
                                                   int width, int height,
                                                   Span<int> offsets,
                                                   Ptr<sbyte> coeff_contexts)
    {
        int stride = height + TX_PAD_HOR;
        Span<__m128i> pos_to_offset = stackalloc __m128i[3];
        __m128i count;
        Span<__m128i> level = stackalloc __m128i[5];
        int col = width;

        Debug.Assert((height % 16) == 0);

        pos_to_offset[0] = _mm_set1_epi8(SIG_COEF_CONTEXTS_2D + 0);
        pos_to_offset[1] = _mm_set1_epi8(SIG_COEF_CONTEXTS_2D + 5);
        pos_to_offset[2] = _mm_set1_epi8(SIG_COEF_CONTEXTS_2D + 10);

        do
        {
            int h = height;

            do
            {
                load_levels_16x1x5_sse2(levels, stride, offsets, level);
                count = get_coeff_contexts_kernel_sse2(level);
                count = _mm_add_epi8(count, pos_to_offset[0]);
                _mm_store_si128(coeff_contexts, count);
                levels += 16;
                coeff_contexts += 16;
                h -= 16;
            } while (h != 0);

            pos_to_offset[0] = pos_to_offset[1];
            pos_to_offset[1] = pos_to_offset[2];
            levels += TX_PAD_HOR;
        } while (--col != 0);
    }

    // Note: levels[] must be in the range [0, 127], inclusive.
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
        Span<int> offsets = stackalloc int[3];

        /* coeff_contexts must be 16 byte aligned. */
        // [port] (alignment is irrelevant for the bounds-checked stores)

        if (tx_class == TX_CLASS_2D)
        {
            offsets[0] = 0 * stride + 2;
            offsets[1] = 1 * stride + 1;
            offsets[2] = 2 * stride + 0;

            if (height == 4)
            {
                get_4_nz_map_contexts_2d(levels, width, offsets, coeff_contexts);
            }
            else if (height == 8)
            {
                get_8_coeff_contexts_2d(levels, width, offsets, coeff_contexts);
            }
            else if (height == 16)
            {
                get_16n_coeff_contexts_2d(levels, real_width, real_height, width, height,
                                          offsets, coeff_contexts);
            }
            else
            {
                get_16n_coeff_contexts_2d(levels, real_width, real_height, width, height,
                                          offsets, coeff_contexts);
            }
        }
        else if (tx_class == TX_CLASS_HORIZ)
        {
            offsets[0] = 2 * stride;
            offsets[1] = 3 * stride;
            offsets[2] = 4 * stride;
            if (height == 4)
            {
                get_4_nz_map_contexts_hor(levels, width, offsets, coeff_contexts);
            }
            else if (height == 8)
            {
                get_8_coeff_contexts_hor(levels, width, offsets, coeff_contexts);
            }
            else
            {
                get_16n_coeff_contexts_hor(levels, width, height, offsets,
                                           coeff_contexts);
            }
        }
        else
        {  // TX_CLASS_VERT
            offsets[0] = 2;
            offsets[1] = 3;
            offsets[2] = 4;
            if (height == 4)
            {
                get_4_nz_map_contexts_ver(levels, width, offsets, coeff_contexts);
            }
            else if (height == 8)
            {
                get_8_coeff_contexts_ver(levels, width, offsets, coeff_contexts);
            }
            else
            {
                get_16n_coeff_contexts_ver(levels, width, height, offsets,
                                           coeff_contexts);
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
