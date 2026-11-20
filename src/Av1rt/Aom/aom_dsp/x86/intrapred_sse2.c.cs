// Port of libaom aom_dsp/x86/intrapred_sse2.c
// [port] The `#if !CONFIG_REALTIME_ONLY || CONFIG_AV1_DECODER` blocks are live (the oracle
// configuration has CONFIG_AV1_DECODER=1), so the 1:4 sizes are ported too.

global using static Av1rt.Aom.intrapred_sse2;

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static class intrapred_sse2
{
    // [port] `*(uint32_t *)dst = dc;`
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void dc_store_4xh(uint dc, int height, Ptr<byte> dst, int stride)
    {
        for (int i = 0; i < height; i += 2)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(dst.AsSpan(4), dc);
            dst += stride;
            BinaryPrimitives.WriteUInt32LittleEndian(dst.AsSpan(4), dc);
            dst += stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void dc_store_8xh(in __m128i row, int height, Ptr<byte> dst, int stride)
    {
        int i;
        for (i = 0; i < height; ++i)
        {
            _mm_storel_epi64(dst, row);
            dst += stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void dc_store_16xh(in __m128i row, int height, Ptr<byte> dst, int stride)
    {
        int i;
        for (i = 0; i < height; ++i)
        {
            _mm_store_si128(dst, row);
            dst += stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void dc_store_32xh(in __m128i row, int height, Ptr<byte> dst, int stride)
    {
        int i;
        for (i = 0; i < height; ++i)
        {
            _mm_store_si128(dst, row);
            _mm_store_si128(dst + 16, row);
            dst += stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void dc_store_64xh(in __m128i row, int height, Ptr<byte> dst, int stride)
    {
        for (int i = 0; i < height; ++i)
        {
            _mm_store_si128(dst, row);
            _mm_store_si128(dst + 16, row);
            _mm_store_si128(dst + 32, row);
            _mm_store_si128(dst + 48, row);
            dst += stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i dc_sum_4(Ptr<byte> @ref)
    {
        __m128i x = _mm_loadl_epi64(@ref);
        __m128i zero = _mm_setzero_si128();
        x = _mm_unpacklo_epi8(x, zero);
        return _mm_sad_epu8(x, zero);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i dc_sum_8(Ptr<byte> @ref)
    {
        __m128i x = _mm_loadl_epi64(@ref);
        __m128i zero = _mm_setzero_si128();
        return _mm_sad_epu8(x, zero);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i dc_sum_64(Ptr<byte> @ref)
    {
        __m128i x0 = _mm_load_si128(@ref);
        __m128i x1 = _mm_load_si128(@ref + 16);
        __m128i x2 = _mm_load_si128(@ref + 32);
        __m128i x3 = _mm_load_si128(@ref + 48);
        __m128i zero = _mm_setzero_si128();
        x0 = _mm_sad_epu8(x0, zero);
        x1 = _mm_sad_epu8(x1, zero);
        x2 = _mm_sad_epu8(x2, zero);
        x3 = _mm_sad_epu8(x3, zero);
        x0 = _mm_add_epi16(x0, x1);
        x2 = _mm_add_epi16(x2, x3);
        x0 = _mm_add_epi16(x0, x2);
        __m128i high = _mm_unpackhi_epi64(x0, x0);
        return _mm_add_epi16(x0, high);
    }

    private const int DC_MULTIPLIER_1X2 = 0x5556;
    private const int DC_MULTIPLIER_1X4 = 0x3334;

    private const int DC_SHIFT2 = 16;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int divide_using_multiply_shift(int num, int shift1, int multiplier)
    {
        int interm = num >> shift1;
        return interm * multiplier >> DC_SHIFT2;
    }

    // -----------------------------------------------------------------------------
    // DC_PRED

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_4x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_8(left);
        __m128i sum_above = dc_sum_4(above);
        sum_above = _mm_add_epi16(sum_left, sum_above);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 6;
        sum = (uint)divide_using_multiply_shift((int)sum, 2, DC_MULTIPLIER_1X2);

        __m128i row = _mm_set1_epi8((sbyte)sum);
        uint pred = (uint)_mm_cvtsi128_si32(row);
        dc_store_4xh(pred, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_4x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_16_sse2(left);
        __m128i sum_above = dc_sum_4(above);
        sum_above = _mm_add_epi16(sum_left, sum_above);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 10;
        sum = (uint)divide_using_multiply_shift((int)sum, 2, DC_MULTIPLIER_1X4);

        __m128i row = _mm_set1_epi8((sbyte)sum);
        uint pred = (uint)_mm_cvtsi128_si32(row);
        dc_store_4xh(pred, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_8x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_4(left);
        __m128i sum_above = dc_sum_8(above);
        sum_above = _mm_add_epi16(sum_above, sum_left);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 6;
        sum = (uint)divide_using_multiply_shift((int)sum, 2, DC_MULTIPLIER_1X2);

        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_8xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_8x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_16_sse2(left);
        __m128i sum_above = dc_sum_8(above);
        sum_above = _mm_add_epi16(sum_above, sum_left);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 12;
        sum = (uint)divide_using_multiply_shift((int)sum, 3, DC_MULTIPLIER_1X2);
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_8xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_8x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_32_sse2(left);
        __m128i sum_above = dc_sum_8(above);
        sum_above = _mm_add_epi16(sum_above, sum_left);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 20;
        sum = (uint)divide_using_multiply_shift((int)sum, 3, DC_MULTIPLIER_1X4);
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_8xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_16x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_4(left);
        __m128i sum_above = dc_sum_16_sse2(above);
        sum_above = _mm_add_epi16(sum_above, sum_left);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 10;
        sum = (uint)divide_using_multiply_shift((int)sum, 2, DC_MULTIPLIER_1X4);
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_16xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_16x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_8(left);
        __m128i sum_above = dc_sum_16_sse2(above);
        sum_above = _mm_add_epi16(sum_above, sum_left);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 12;
        sum = (uint)divide_using_multiply_shift((int)sum, 3, DC_MULTIPLIER_1X2);
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_16xh(row, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_16x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_32_sse2(left);
        __m128i sum_above = dc_sum_16_sse2(above);
        sum_above = _mm_add_epi16(sum_left, sum_above);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 24;
        sum = (uint)divide_using_multiply_shift((int)sum, 4, DC_MULTIPLIER_1X2);
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_16xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_16x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_64(left);
        __m128i sum_above = dc_sum_16_sse2(above);
        sum_above = _mm_add_epi16(sum_left, sum_above);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 40;
        sum = (uint)divide_using_multiply_shift((int)sum, 4, DC_MULTIPLIER_1X4);
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_16xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_32x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_32_sse2(above);
        __m128i sum_left = dc_sum_8(left);
        sum_above = _mm_add_epi16(sum_above, sum_left);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 20;
        sum = (uint)divide_using_multiply_shift((int)sum, 3, DC_MULTIPLIER_1X4);
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_32xh(row, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_32x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_32_sse2(above);
        __m128i sum_left = dc_sum_16_sse2(left);
        sum_above = _mm_add_epi16(sum_above, sum_left);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 24;
        sum = (uint)divide_using_multiply_shift((int)sum, 4, DC_MULTIPLIER_1X2);
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_32xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_32x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_32_sse2(above);
        __m128i sum_left = dc_sum_64(left);
        sum_above = _mm_add_epi16(sum_above, sum_left);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 48;
        sum = (uint)divide_using_multiply_shift((int)sum, 5, DC_MULTIPLIER_1X2);
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_32xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_64x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_64(above);
        __m128i sum_left = dc_sum_64(left);
        sum_above = _mm_add_epi16(sum_above, sum_left);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 64;
        sum /= 128;
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_64xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_64x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_64(above);
        __m128i sum_left = dc_sum_32_sse2(left);
        sum_above = _mm_add_epi16(sum_above, sum_left);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 48;
        sum = (uint)divide_using_multiply_shift((int)sum, 5, DC_MULTIPLIER_1X2);
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_64xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_64x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_64(above);
        __m128i sum_left = dc_sum_16_sse2(left);
        sum_above = _mm_add_epi16(sum_above, sum_left);

        uint sum = (uint)_mm_cvtsi128_si32(sum_above);
        sum += 40;
        sum = (uint)divide_using_multiply_shift((int)sum, 4, DC_MULTIPLIER_1X4);
        __m128i row = _mm_set1_epi8((sbyte)sum);
        dc_store_64xh(row, 16, dst, stride);
    }

    // -----------------------------------------------------------------------------
    // DC_TOP

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_4x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_4(above);
        __m128i two = _mm_set1_epi16(2);
        sum_above = _mm_add_epi16(sum_above, two);
        sum_above = _mm_srai_epi16(sum_above, 2);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        sum_above = _mm_packus_epi16(sum_above, sum_above);

        uint pred = (uint)_mm_cvtsi128_si32(sum_above);
        dc_store_4xh(pred, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_4x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_4(above);
        __m128i two = _mm_set1_epi16(2);
        sum_above = _mm_add_epi16(sum_above, two);
        sum_above = _mm_srai_epi16(sum_above, 2);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        sum_above = _mm_packus_epi16(sum_above, sum_above);

        uint pred = (uint)_mm_cvtsi128_si32(sum_above);
        dc_store_4xh(pred, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_8x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_8(above);
        __m128i four = _mm_set1_epi16(4);
        sum_above = _mm_add_epi16(sum_above, four);
        sum_above = _mm_srai_epi16(sum_above, 3);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        __m128i row = _mm_shufflelo_epi16(sum_above, 0);
        dc_store_8xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_8x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_8(above);
        __m128i four = _mm_set1_epi16(4);
        sum_above = _mm_add_epi16(sum_above, four);
        sum_above = _mm_srai_epi16(sum_above, 3);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        __m128i row = _mm_shufflelo_epi16(sum_above, 0);
        dc_store_8xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_8x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_8(above);
        __m128i four = _mm_set1_epi16(4);
        sum_above = _mm_add_epi16(sum_above, four);
        sum_above = _mm_srai_epi16(sum_above, 3);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        __m128i row = _mm_shufflelo_epi16(sum_above, 0);
        dc_store_8xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_16x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_16_sse2(above);
        __m128i eight = _mm_set1_epi16(8);
        sum_above = _mm_add_epi16(sum_above, eight);
        sum_above = _mm_srai_epi16(sum_above, 4);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        __m128i row = _mm_unpacklo_epi64(sum_above, sum_above);
        dc_store_16xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_16x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_16_sse2(above);
        __m128i eight = _mm_set1_epi16(8);
        sum_above = _mm_add_epi16(sum_above, eight);
        sum_above = _mm_srai_epi16(sum_above, 4);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        __m128i row = _mm_unpacklo_epi64(sum_above, sum_above);
        dc_store_16xh(row, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_16x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_16_sse2(above);
        __m128i eight = _mm_set1_epi16(8);
        sum_above = _mm_add_epi16(sum_above, eight);
        sum_above = _mm_srai_epi16(sum_above, 4);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        __m128i row = _mm_unpacklo_epi64(sum_above, sum_above);
        dc_store_16xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_16x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_16_sse2(above);
        __m128i eight = _mm_set1_epi16(8);
        sum_above = _mm_add_epi16(sum_above, eight);
        sum_above = _mm_srai_epi16(sum_above, 4);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        __m128i row = _mm_unpacklo_epi64(sum_above, sum_above);
        dc_store_16xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_32x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_32_sse2(above);
        __m128i sixteen = _mm_set1_epi16(16);
        sum_above = _mm_add_epi16(sum_above, sixteen);
        sum_above = _mm_srai_epi16(sum_above, 5);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        __m128i row = _mm_unpacklo_epi64(sum_above, sum_above);
        dc_store_32xh(row, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_32x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_32_sse2(above);
        __m128i sixteen = _mm_set1_epi16(16);
        sum_above = _mm_add_epi16(sum_above, sixteen);
        sum_above = _mm_srai_epi16(sum_above, 5);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        __m128i row = _mm_unpacklo_epi64(sum_above, sum_above);
        dc_store_32xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_32x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_32_sse2(above);
        __m128i sixteen = _mm_set1_epi16(16);
        sum_above = _mm_add_epi16(sum_above, sixteen);
        sum_above = _mm_srai_epi16(sum_above, 5);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        __m128i row = _mm_unpacklo_epi64(sum_above, sum_above);
        dc_store_32xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_64x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_64(above);
        __m128i thirtytwo = _mm_set1_epi16(32);
        sum_above = _mm_add_epi16(sum_above, thirtytwo);
        sum_above = _mm_srai_epi16(sum_above, 6);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        __m128i row = _mm_unpacklo_epi64(sum_above, sum_above);
        dc_store_64xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_64x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_64(above);
        __m128i thirtytwo = _mm_set1_epi16(32);
        sum_above = _mm_add_epi16(sum_above, thirtytwo);
        sum_above = _mm_srai_epi16(sum_above, 6);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        __m128i row = _mm_unpacklo_epi64(sum_above, sum_above);
        dc_store_64xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_64x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_above = dc_sum_64(above);
        __m128i thirtytwo = _mm_set1_epi16(32);
        sum_above = _mm_add_epi16(sum_above, thirtytwo);
        sum_above = _mm_srai_epi16(sum_above, 6);
        sum_above = _mm_unpacklo_epi8(sum_above, sum_above);
        sum_above = _mm_shufflelo_epi16(sum_above, 0);
        __m128i row = _mm_unpacklo_epi64(sum_above, sum_above);
        dc_store_64xh(row, 16, dst, stride);
    }

    // -----------------------------------------------------------------------------
    // DC_LEFT

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_4x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_8(left);
        __m128i four = _mm_set1_epi16(4);
        sum_left = _mm_add_epi16(sum_left, four);
        sum_left = _mm_srai_epi16(sum_left, 3);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        sum_left = _mm_packus_epi16(sum_left, sum_left);

        uint pred = (uint)_mm_cvtsi128_si32(sum_left);
        dc_store_4xh(pred, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_4x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_16_sse2(left);
        __m128i eight = _mm_set1_epi16(8);
        sum_left = _mm_add_epi16(sum_left, eight);
        sum_left = _mm_srai_epi16(sum_left, 4);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        sum_left = _mm_packus_epi16(sum_left, sum_left);

        uint pred = (uint)_mm_cvtsi128_si32(sum_left);
        dc_store_4xh(pred, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_8x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_4(left);
        __m128i two = _mm_set1_epi16(2);
        sum_left = _mm_add_epi16(sum_left, two);
        sum_left = _mm_srai_epi16(sum_left, 2);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        __m128i row = _mm_shufflelo_epi16(sum_left, 0);
        dc_store_8xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_8x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_16_sse2(left);
        __m128i eight = _mm_set1_epi16(8);
        sum_left = _mm_add_epi16(sum_left, eight);
        sum_left = _mm_srai_epi16(sum_left, 4);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        __m128i row = _mm_shufflelo_epi16(sum_left, 0);
        dc_store_8xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_8x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_32_sse2(left);
        __m128i sixteen = _mm_set1_epi16(16);
        sum_left = _mm_add_epi16(sum_left, sixteen);
        sum_left = _mm_srai_epi16(sum_left, 5);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        __m128i row = _mm_shufflelo_epi16(sum_left, 0);
        dc_store_8xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_16x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_4(left);
        __m128i two = _mm_set1_epi16(2);
        sum_left = _mm_add_epi16(sum_left, two);
        sum_left = _mm_srai_epi16(sum_left, 2);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        __m128i row = _mm_unpacklo_epi64(sum_left, sum_left);
        dc_store_16xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_16x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_8(left);
        __m128i four = _mm_set1_epi16(4);
        sum_left = _mm_add_epi16(sum_left, four);
        sum_left = _mm_srai_epi16(sum_left, 3);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        __m128i row = _mm_unpacklo_epi64(sum_left, sum_left);
        dc_store_16xh(row, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_16x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_32_sse2(left);
        __m128i sixteen = _mm_set1_epi16(16);
        sum_left = _mm_add_epi16(sum_left, sixteen);
        sum_left = _mm_srai_epi16(sum_left, 5);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        __m128i row = _mm_unpacklo_epi64(sum_left, sum_left);
        dc_store_16xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_16x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_64(left);
        __m128i thirtytwo = _mm_set1_epi16(32);
        sum_left = _mm_add_epi16(sum_left, thirtytwo);
        sum_left = _mm_srai_epi16(sum_left, 6);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        __m128i row = _mm_unpacklo_epi64(sum_left, sum_left);
        dc_store_16xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_32x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_8(left);
        __m128i four = _mm_set1_epi16(4);
        sum_left = _mm_add_epi16(sum_left, four);
        sum_left = _mm_srai_epi16(sum_left, 3);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        __m128i row = _mm_unpacklo_epi64(sum_left, sum_left);
        dc_store_32xh(row, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_32x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_16_sse2(left);
        __m128i eight = _mm_set1_epi16(8);
        sum_left = _mm_add_epi16(sum_left, eight);
        sum_left = _mm_srai_epi16(sum_left, 4);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        __m128i row = _mm_unpacklo_epi64(sum_left, sum_left);
        dc_store_32xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_32x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_64(left);
        __m128i thirtytwo = _mm_set1_epi16(32);
        sum_left = _mm_add_epi16(sum_left, thirtytwo);
        sum_left = _mm_srai_epi16(sum_left, 6);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        __m128i row = _mm_unpacklo_epi64(sum_left, sum_left);
        dc_store_32xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_64x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_64(left);
        __m128i thirtytwo = _mm_set1_epi16(32);
        sum_left = _mm_add_epi16(sum_left, thirtytwo);
        sum_left = _mm_srai_epi16(sum_left, 6);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        __m128i row = _mm_unpacklo_epi64(sum_left, sum_left);
        dc_store_64xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_64x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_32_sse2(left);
        __m128i sixteen = _mm_set1_epi16(16);
        sum_left = _mm_add_epi16(sum_left, sixteen);
        sum_left = _mm_srai_epi16(sum_left, 5);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        __m128i row = _mm_unpacklo_epi64(sum_left, sum_left);
        dc_store_64xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_64x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum_left = dc_sum_16_sse2(left);
        __m128i eight = _mm_set1_epi16(8);
        sum_left = _mm_add_epi16(sum_left, eight);
        sum_left = _mm_srai_epi16(sum_left, 4);
        sum_left = _mm_unpacklo_epi8(sum_left, sum_left);
        sum_left = _mm_shufflelo_epi16(sum_left, 0);
        __m128i row = _mm_unpacklo_epi64(sum_left, sum_left);
        dc_store_64xh(row, 16, dst, stride);
    }

    // -----------------------------------------------------------------------------
    // DC_128

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_4x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        const uint pred = 0x80808080;
        dc_store_4xh(pred, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_4x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        const uint pred = 0x80808080;
        dc_store_4xh(pred, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_8x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_8xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_8x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_8xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_8x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_8xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_16x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_16xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_16x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_16xh(row, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_16x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_16xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_16x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_16xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_32x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_32xh(row, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_32x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_32xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_32x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_32xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_64x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_64xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_64x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_64xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_64x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_set1_epi8(unchecked((sbyte)128));
        dc_store_64xh(row, 16, dst, stride);
    }

    // -----------------------------------------------------------------------------
    // V_PRED

    // [port] `*(uint32_t *)above`.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_4x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        uint pred = BinaryPrimitives.ReadUInt32LittleEndian(above.AsSpan(4));
        dc_store_4xh(pred, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_4x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        uint pred = BinaryPrimitives.ReadUInt32LittleEndian(above.AsSpan(4));
        dc_store_4xh(pred, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_8x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_loadl_epi64(above);
        dc_store_8xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_8x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_loadl_epi64(above);
        dc_store_8xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_8x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_loadl_epi64(above);
        dc_store_8xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_16x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_load_si128(above);
        dc_store_16xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_16x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_load_si128(above);
        dc_store_16xh(row, 8, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_16x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_load_si128(above);
        dc_store_16xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_16x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i row = _mm_load_si128(above);
        dc_store_16xh(row, 64, dst, stride);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void v_predictor_32xh(Ptr<byte> dst, int stride, Ptr<byte> above, int height)
    {
        __m128i row0 = _mm_load_si128(above);
        __m128i row1 = _mm_load_si128(above + 16);
        for (int i = 0; i < height; ++i)
        {
            _mm_store_si128(dst, row0);
            _mm_store_si128(dst + 16, row1);
            dst += stride;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_32x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        v_predictor_32xh(dst, stride, above, 8);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_32x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        v_predictor_32xh(dst, stride, above, 16);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_32x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        v_predictor_32xh(dst, stride, above, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void v_predictor_64xh(Ptr<byte> dst, int stride, Ptr<byte> above, int height)
    {
        __m128i row0 = _mm_load_si128(above);
        __m128i row1 = _mm_load_si128(above + 16);
        __m128i row2 = _mm_load_si128(above + 32);
        __m128i row3 = _mm_load_si128(above + 48);
        for (int i = 0; i < height; ++i)
        {
            _mm_store_si128(dst, row0);
            _mm_store_si128(dst + 16, row1);
            _mm_store_si128(dst + 32, row2);
            _mm_store_si128(dst + 48, row3);
            dst += stride;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_64x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        v_predictor_64xh(dst, stride, above, 64);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_64x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        v_predictor_64xh(dst, stride, above, 32);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_64x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        v_predictor_64xh(dst, stride, above, 16);
    }

    // -----------------------------------------------------------------------------
    // H_PRED

    // [port] `*(int *)dst = _mm_cvtsi128_si32(row)` is _mm_storeu_si32(dst, row).
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_4x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i left_col = _mm_loadl_epi64(left);
        left_col = _mm_unpacklo_epi8(left_col, left_col);
        __m128i row0 = _mm_shufflelo_epi16(left_col, 0);
        __m128i row1 = _mm_shufflelo_epi16(left_col, 0x55);
        __m128i row2 = _mm_shufflelo_epi16(left_col, 0xaa);
        __m128i row3 = _mm_shufflelo_epi16(left_col, 0xff);
        _mm_storeu_si32(dst, row0);
        dst += stride;
        _mm_storeu_si32(dst, row1);
        dst += stride;
        _mm_storeu_si32(dst, row2);
        dst += stride;
        _mm_storeu_si32(dst, row3);
        dst += stride;
        left_col = _mm_unpackhi_epi64(left_col, left_col);
        row0 = _mm_shufflelo_epi16(left_col, 0);
        row1 = _mm_shufflelo_epi16(left_col, 0x55);
        row2 = _mm_shufflelo_epi16(left_col, 0xaa);
        row3 = _mm_shufflelo_epi16(left_col, 0xff);
        _mm_storeu_si32(dst, row0);
        dst += stride;
        _mm_storeu_si32(dst, row1);
        dst += stride;
        _mm_storeu_si32(dst, row2);
        dst += stride;
        _mm_storeu_si32(dst, row3);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_4x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i left_col = _mm_load_si128(left);
        __m128i left_col_low = _mm_unpacklo_epi8(left_col, left_col);
        __m128i left_col_high = _mm_unpackhi_epi8(left_col, left_col);

        __m128i row0 = _mm_shufflelo_epi16(left_col_low, 0);
        __m128i row1 = _mm_shufflelo_epi16(left_col_low, 0x55);
        __m128i row2 = _mm_shufflelo_epi16(left_col_low, 0xaa);
        __m128i row3 = _mm_shufflelo_epi16(left_col_low, 0xff);
        _mm_storeu_si32(dst, row0);
        dst += stride;
        _mm_storeu_si32(dst, row1);
        dst += stride;
        _mm_storeu_si32(dst, row2);
        dst += stride;
        _mm_storeu_si32(dst, row3);
        dst += stride;

        left_col_low = _mm_unpackhi_epi64(left_col_low, left_col_low);
        row0 = _mm_shufflelo_epi16(left_col_low, 0);
        row1 = _mm_shufflelo_epi16(left_col_low, 0x55);
        row2 = _mm_shufflelo_epi16(left_col_low, 0xaa);
        row3 = _mm_shufflelo_epi16(left_col_low, 0xff);
        _mm_storeu_si32(dst, row0);
        dst += stride;
        _mm_storeu_si32(dst, row1);
        dst += stride;
        _mm_storeu_si32(dst, row2);
        dst += stride;
        _mm_storeu_si32(dst, row3);
        dst += stride;

        row0 = _mm_shufflelo_epi16(left_col_high, 0);
        row1 = _mm_shufflelo_epi16(left_col_high, 0x55);
        row2 = _mm_shufflelo_epi16(left_col_high, 0xaa);
        row3 = _mm_shufflelo_epi16(left_col_high, 0xff);
        _mm_storeu_si32(dst, row0);
        dst += stride;
        _mm_storeu_si32(dst, row1);
        dst += stride;
        _mm_storeu_si32(dst, row2);
        dst += stride;
        _mm_storeu_si32(dst, row3);
        dst += stride;

        left_col_high = _mm_unpackhi_epi64(left_col_high, left_col_high);
        row0 = _mm_shufflelo_epi16(left_col_high, 0);
        row1 = _mm_shufflelo_epi16(left_col_high, 0x55);
        row2 = _mm_shufflelo_epi16(left_col_high, 0xaa);
        row3 = _mm_shufflelo_epi16(left_col_high, 0xff);
        _mm_storeu_si32(dst, row0);
        dst += stride;
        _mm_storeu_si32(dst, row1);
        dst += stride;
        _mm_storeu_si32(dst, row2);
        dst += stride;
        _mm_storeu_si32(dst, row3);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_8x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i left_col = _mm_loadl_epi64(left);
        left_col = _mm_unpacklo_epi8(left_col, left_col);
        __m128i row0 = _mm_shufflelo_epi16(left_col, 0);
        __m128i row1 = _mm_shufflelo_epi16(left_col, 0x55);
        __m128i row2 = _mm_shufflelo_epi16(left_col, 0xaa);
        __m128i row3 = _mm_shufflelo_epi16(left_col, 0xff);
        _mm_storel_epi64(dst, row0);
        dst += stride;
        _mm_storel_epi64(dst, row1);
        dst += stride;
        _mm_storel_epi64(dst, row2);
        dst += stride;
        _mm_storel_epi64(dst, row3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void h_predictor_8x16xc(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left,
                                           int count)
    {
        for (int i = 0; i < count; ++i)
        {
            __m128i left_col = _mm_load_si128(left);
            __m128i left_col_low = _mm_unpacklo_epi8(left_col, left_col);
            __m128i left_col_high = _mm_unpackhi_epi8(left_col, left_col);

            __m128i row0 = _mm_shufflelo_epi16(left_col_low, 0);
            __m128i row1 = _mm_shufflelo_epi16(left_col_low, 0x55);
            __m128i row2 = _mm_shufflelo_epi16(left_col_low, 0xaa);
            __m128i row3 = _mm_shufflelo_epi16(left_col_low, 0xff);
            _mm_storel_epi64(dst, row0);
            dst += stride;
            _mm_storel_epi64(dst, row1);
            dst += stride;
            _mm_storel_epi64(dst, row2);
            dst += stride;
            _mm_storel_epi64(dst, row3);
            dst += stride;

            left_col_low = _mm_unpackhi_epi64(left_col_low, left_col_low);
            row0 = _mm_shufflelo_epi16(left_col_low, 0);
            row1 = _mm_shufflelo_epi16(left_col_low, 0x55);
            row2 = _mm_shufflelo_epi16(left_col_low, 0xaa);
            row3 = _mm_shufflelo_epi16(left_col_low, 0xff);
            _mm_storel_epi64(dst, row0);
            dst += stride;
            _mm_storel_epi64(dst, row1);
            dst += stride;
            _mm_storel_epi64(dst, row2);
            dst += stride;
            _mm_storel_epi64(dst, row3);
            dst += stride;

            row0 = _mm_shufflelo_epi16(left_col_high, 0);
            row1 = _mm_shufflelo_epi16(left_col_high, 0x55);
            row2 = _mm_shufflelo_epi16(left_col_high, 0xaa);
            row3 = _mm_shufflelo_epi16(left_col_high, 0xff);
            _mm_storel_epi64(dst, row0);
            dst += stride;
            _mm_storel_epi64(dst, row1);
            dst += stride;
            _mm_storel_epi64(dst, row2);
            dst += stride;
            _mm_storel_epi64(dst, row3);
            dst += stride;

            left_col_high = _mm_unpackhi_epi64(left_col_high, left_col_high);
            row0 = _mm_shufflelo_epi16(left_col_high, 0);
            row1 = _mm_shufflelo_epi16(left_col_high, 0x55);
            row2 = _mm_shufflelo_epi16(left_col_high, 0xaa);
            row3 = _mm_shufflelo_epi16(left_col_high, 0xff);
            _mm_storel_epi64(dst, row0);
            dst += stride;
            _mm_storel_epi64(dst, row1);
            dst += stride;
            _mm_storel_epi64(dst, row2);
            dst += stride;
            _mm_storel_epi64(dst, row3);
            dst += stride;
            left += 16;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_8x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        h_predictor_8x16xc(dst, stride, above, left, 1);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_8x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        h_predictor_8x16xc(dst, stride, above, left, 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void h_pred_store_16xh(ReadOnlySpan<__m128i> row, int h, Ptr<byte> dst, int stride)
    {
        int i;
        for (i = 0; i < h; ++i)
        {
            _mm_store_si128(dst, row[i]);
            dst += stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void repeat_low_4pixels(in __m128i x, Span<__m128i> row)
    {
        __m128i u0 = _mm_shufflelo_epi16(x, 0);
        __m128i u1 = _mm_shufflelo_epi16(x, 0x55);
        __m128i u2 = _mm_shufflelo_epi16(x, 0xaa);
        __m128i u3 = _mm_shufflelo_epi16(x, 0xff);

        row[0] = _mm_unpacklo_epi64(u0, u0);
        row[1] = _mm_unpacklo_epi64(u1, u1);
        row[2] = _mm_unpacklo_epi64(u2, u2);
        row[3] = _mm_unpacklo_epi64(u3, u3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void repeat_high_4pixels(in __m128i x, Span<__m128i> row)
    {
        __m128i u0 = _mm_shufflehi_epi16(x, 0);
        __m128i u1 = _mm_shufflehi_epi16(x, 0x55);
        __m128i u2 = _mm_shufflehi_epi16(x, 0xaa);
        __m128i u3 = _mm_shufflehi_epi16(x, 0xff);

        row[0] = _mm_unpackhi_epi64(u0, u0);
        row[1] = _mm_unpackhi_epi64(u1, u1);
        row[2] = _mm_unpackhi_epi64(u2, u2);
        row[3] = _mm_unpackhi_epi64(u3, u3);
    }

    // Process 16x8, first 4 rows
    // Use first 8 bytes of left register: xxxxxxxx33221100
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void h_prediction_16x8_1(in __m128i left, Ptr<byte> dst, int stride)
    {
        Arr4<__m128i> row = default;
        repeat_low_4pixels(left, row);
        h_pred_store_16xh(row, 4, dst, stride);
    }

    // Process 16x8, second 4 rows
    // Use second 8 bytes of left register: 77665544xxxxxxxx
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void h_prediction_16x8_2(in __m128i left, Ptr<byte> dst, int stride)
    {
        Arr4<__m128i> row = default;
        repeat_high_4pixels(left, row);
        h_pred_store_16xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_16x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i left_col = _mm_loadl_epi64(left);
        __m128i left_col_8p = _mm_unpacklo_epi8(left_col, left_col);
        h_prediction_16x8_1(left_col_8p, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_16x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i left_col = _mm_loadl_epi64(left);
        __m128i left_col_8p = _mm_unpacklo_epi8(left_col, left_col);
        h_prediction_16x8_1(left_col_8p, dst, stride);
        dst += stride << 2;
        h_prediction_16x8_2(left_col_8p, dst, stride);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void h_predictor_16xh(Ptr<byte> dst, int stride, Ptr<byte> left, int count)
    {
        int i = 0;
        do
        {
            __m128i left_col = _mm_load_si128(left);
            __m128i left_col_8p_lo = _mm_unpacklo_epi8(left_col, left_col);
            h_prediction_16x8_1(left_col_8p_lo, dst, stride);
            dst += stride << 2;
            h_prediction_16x8_2(left_col_8p_lo, dst, stride);
            dst += stride << 2;

            __m128i left_col_8p_hi = _mm_unpackhi_epi8(left_col, left_col);
            h_prediction_16x8_1(left_col_8p_hi, dst, stride);
            dst += stride << 2;
            h_prediction_16x8_2(left_col_8p_hi, dst, stride);
            dst += stride << 2;

            left += 16;
            i++;
        } while (i < count);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_16x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        h_predictor_16xh(dst, stride, left, 2);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_16x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        h_predictor_16xh(dst, stride, left, 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void h_pred_store_32xh(ReadOnlySpan<__m128i> row, int h, Ptr<byte> dst, int stride)
    {
        int i;
        for (i = 0; i < h; ++i)
        {
            _mm_store_si128(dst, row[i]);
            _mm_store_si128(dst + 16, row[i]);
            dst += stride;
        }
    }

    // Process 32x8, first 4 rows
    // Use first 8 bytes of left register: xxxxxxxx33221100
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void h_prediction_32x8_1(in __m128i left, Ptr<byte> dst, int stride)
    {
        Arr4<__m128i> row = default;
        repeat_low_4pixels(left, row);
        h_pred_store_32xh(row, 4, dst, stride);
    }

    // Process 32x8, second 4 rows
    // Use second 8 bytes of left register: 77665544xxxxxxxx
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void h_prediction_32x8_2(in __m128i left, Ptr<byte> dst, int stride)
    {
        Arr4<__m128i> row = default;
        repeat_high_4pixels(left, row);
        h_pred_store_32xh(row, 4, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_32x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i left_col, left_col_8p;

        left_col = _mm_load_si128(left);

        left_col_8p = _mm_unpacklo_epi8(left_col, left_col);
        h_prediction_32x8_1(left_col_8p, dst, stride);
        dst += stride << 2;
        h_prediction_32x8_2(left_col_8p, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_32x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i left_col, left_col_8p;

        left_col = _mm_load_si128(left);

        left_col_8p = _mm_unpacklo_epi8(left_col, left_col);
        h_prediction_32x8_1(left_col_8p, dst, stride);
        dst += stride << 2;
        h_prediction_32x8_2(left_col_8p, dst, stride);
        dst += stride << 2;

        left_col_8p = _mm_unpackhi_epi8(left_col, left_col);
        h_prediction_32x8_1(left_col_8p, dst, stride);
        dst += stride << 2;
        h_prediction_32x8_2(left_col_8p, dst, stride);
    }

    // [port] `_mm_cvtsi32_si128(((int *)left)[0])` is _mm_loadu_si32(left).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void h_predictor_32xh(Ptr<byte> dst, int stride, Ptr<byte> left, int height)
    {
        int i = height >> 2;
        do
        {
            __m128i left4 = _mm_loadu_si32(left);
            left4 = _mm_unpacklo_epi8(left4, left4);
            left4 = _mm_unpacklo_epi8(left4, left4);
            __m128i r0 = _mm_shuffle_epi32(left4, 0x0);
            __m128i r1 = _mm_shuffle_epi32(left4, 0x55);
            _mm_store_si128(dst, r0);
            _mm_store_si128(dst + 16, r0);
            _mm_store_si128(dst + stride, r1);
            _mm_store_si128(dst + stride + 16, r1);
            __m128i r2 = _mm_shuffle_epi32(left4, 0xaa);
            __m128i r3 = _mm_shuffle_epi32(left4, 0xff);
            _mm_store_si128(dst + stride * 2, r2);
            _mm_store_si128(dst + stride * 2 + 16, r2);
            _mm_store_si128(dst + stride * 3, r3);
            _mm_store_si128(dst + stride * 3 + 16, r3);
            left += 4;
            dst += stride * 4;
        } while (--i != 0);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_32x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        h_predictor_32xh(dst, stride, left, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void h_predictor_64xh(Ptr<byte> dst, int stride, Ptr<byte> left, int height)
    {
        int i = height >> 2;
        do
        {
            __m128i left4 = _mm_loadu_si32(left);
            left4 = _mm_unpacklo_epi8(left4, left4);
            left4 = _mm_unpacklo_epi8(left4, left4);
            __m128i r0 = _mm_shuffle_epi32(left4, 0x0);
            __m128i r1 = _mm_shuffle_epi32(left4, 0x55);
            _mm_store_si128(dst, r0);
            _mm_store_si128(dst + 16, r0);
            _mm_store_si128(dst + 32, r0);
            _mm_store_si128(dst + 48, r0);
            _mm_store_si128(dst + stride, r1);
            _mm_store_si128(dst + stride + 16, r1);
            _mm_store_si128(dst + stride + 32, r1);
            _mm_store_si128(dst + stride + 48, r1);
            __m128i r2 = _mm_shuffle_epi32(left4, 0xaa);
            __m128i r3 = _mm_shuffle_epi32(left4, 0xff);
            _mm_store_si128(dst + stride * 2, r2);
            _mm_store_si128(dst + stride * 2 + 16, r2);
            _mm_store_si128(dst + stride * 2 + 32, r2);
            _mm_store_si128(dst + stride * 2 + 48, r2);
            _mm_store_si128(dst + stride * 3, r3);
            _mm_store_si128(dst + stride * 3 + 16, r3);
            _mm_store_si128(dst + stride * 3 + 32, r3);
            _mm_store_si128(dst + stride * 3 + 48, r3);
            left += 4;
            dst += stride * 4;
        } while (--i != 0);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_64x64_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        h_predictor_64xh(dst, stride, left, 64);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_64x32_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        h_predictor_64xh(dst, stride, left, 32);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_64x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        h_predictor_64xh(dst, stride, left, 16);
    }
}
