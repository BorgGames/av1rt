// Port of libaom aom_dsp/x86/intrapred_avx2.c

global using static Av1rt.Aom.intrapred_avx2;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [subset] Only the lowbd dc / dc_top / dc_left / dc_128 / v / h predictors for which avx2 is
// the best x86 variant (rtcd_best.txt) are ported. The highbd predictors (CONFIG_AV1_HIGHBITDEPTH
// is off), PAETH, the directional z1/z2/z3 predictors and their transpose helpers are not ported:
// those modes are never dispatched by the port.
internal static class intrapred_avx2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i dc_sum_64(Ptr<byte> @ref)
    {
        __m256i x0 = _mm256_loadu_si256(@ref);
        __m256i x1 = _mm256_loadu_si256(@ref + 32);
        __m256i zero = _mm256_setzero_si256();
        __m256i y0 = _mm256_sad_epu8(x0, zero);
        __m256i y1 = _mm256_sad_epu8(x1, zero);
        y0 = _mm256_add_epi64(y0, y1);
        __m256i u0 = _mm256_permute2x128_si256(y0, y0, 1);
        y0 = _mm256_add_epi64(u0, y0);
        u0 = _mm256_unpackhi_epi64(y0, y0);
        return _mm256_add_epi16(y0, u0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i dc_sum_32(Ptr<byte> @ref)
    {
        __m256i x = _mm256_loadu_si256(@ref);
        __m256i zero = _mm256_setzero_si256();
        __m256i y = _mm256_sad_epu8(x, zero);
        __m256i u = _mm256_permute2x128_si256(y, y, 1);
        y = _mm256_add_epi64(u, y);
        u = _mm256_unpackhi_epi64(y, y);
        return _mm256_add_epi16(y, u);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void row_store_32xh(in __m256i r, int height, Ptr<byte> dst, int stride)
    {
        for (int i = 0; i < height; ++i)
        {
            _mm256_storeu_si256(dst, r);
            dst += stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void row_store_32x2xh(in __m256i r0, in __m256i r1, int height, Ptr<byte> dst,
                                         int stride)
    {
        for (int i = 0; i < height; ++i)
        {
            _mm256_storeu_si256(dst, r0);
            _mm256_storeu_si256(dst + 32, r1);
            dst += stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void row_store_64xh(in __m256i r, int height, Ptr<byte> dst, int stride)
    {
        for (int i = 0; i < height; ++i)
        {
            _mm256_storeu_si256(dst, r);
            _mm256_storeu_si256(dst + 32, r);
            dst += stride;
        }
    }

    // [subset] HighbdLoadMaskx .. aom_highbd_*_avx2: CONFIG_AV1_HIGHBITDEPTH is off.

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_32x32_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum_above = dc_sum_32(above);
        __m256i sum_left = dc_sum_32(left);
        sum_left = _mm256_add_epi16(sum_left, sum_above);
        __m256i thirtytwo = _mm256_set1_epi16(32);
        sum_left = _mm256_add_epi16(sum_left, thirtytwo);
        sum_left = _mm256_srai_epi16(sum_left, 6);
        __m256i zero = _mm256_setzero_si256();
        __m256i row = _mm256_shuffle_epi8(sum_left, zero);
        row_store_32xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_32x32_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum = dc_sum_32(above);

        __m256i sixteen = _mm256_set1_epi16(16);
        sum = _mm256_add_epi16(sum, sixteen);
        sum = _mm256_srai_epi16(sum, 5);
        __m256i zero = _mm256_setzero_si256();
        __m256i row = _mm256_shuffle_epi8(sum, zero);
        row_store_32xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_32x32_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum = dc_sum_32(left);

        __m256i sixteen = _mm256_set1_epi16(16);
        sum = _mm256_add_epi16(sum, sixteen);
        sum = _mm256_srai_epi16(sum, 5);
        __m256i zero = _mm256_setzero_si256();
        __m256i row = _mm256_shuffle_epi8(sum, zero);
        row_store_32xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_32x32_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row = _mm256_set1_epi8(unchecked((sbyte)0x80));
        row_store_32xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_32x32_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row = _mm256_loadu_si256(above);
        row_store_32xh(row, 32, dst, stride);
    }

    // There are 32 rows togeter. This function does line:
    // 0,1,2,3, and 16,17,18,19. The next call would do
    // 4,5,6,7, and 20,21,22,23. So 4 times of calling
    // would finish 32 rows.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void h_predictor_32x8line(in __m256i row, Ptr<byte> dst, int stride)
    {
        Arr4<__m256i> t = default;
        __m256i m = _mm256_setzero_si256();
        __m256i inc = _mm256_set1_epi8(4);
        int i;

        for (i = 0; i < 4; i++)
        {
            t[i] = _mm256_shuffle_epi8(row, m);
            __m256i r0 = _mm256_permute2x128_si256(t[i], t[i], 0);
            __m256i r1 = _mm256_permute2x128_si256(t[i], t[i], 0x11);
            _mm256_storeu_si256(dst, r0);
            _mm256_storeu_si256(dst + (stride << 4), r1);
            dst += stride;
            m = _mm256_add_epi8(m, inc);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_h_predictor_32x32_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i left_col = _mm256_loadu_si256(left);

        __m256i u = _mm256_unpacklo_epi8(left_col, left_col);

        __m256i v = _mm256_unpacklo_epi8(u, u);
        h_predictor_32x8line(v, dst, stride);
        dst += stride << 2;

        v = _mm256_unpackhi_epi8(u, u);
        h_predictor_32x8line(v, dst, stride);
        dst += stride << 2;

        u = _mm256_unpackhi_epi8(left_col, left_col);

        v = _mm256_unpacklo_epi8(u, u);
        h_predictor_32x8line(v, dst, stride);
        dst += stride << 2;

        v = _mm256_unpackhi_epi8(u, u);
        h_predictor_32x8line(v, dst, stride);
    }

    // -----------------------------------------------------------------------------
    // Rectangle
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_32x16_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i top_sum = dc_sum_32_sse2(above);
        __m128i left_sum = dc_sum_16_sse2(left);
        left_sum = _mm_add_epi16(top_sum, left_sum);
        ushort sum = (ushort)_mm_cvtsi128_si32(left_sum);
        sum += 24;
        sum /= 48;
        __m256i row = _mm256_set1_epi8((sbyte)sum);
        row_store_32xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_32x64_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum_above = dc_sum_32(above);
        __m256i sum_left = dc_sum_64(left);
        sum_left = _mm256_add_epi16(sum_left, sum_above);
        ushort sum = (ushort)_mm_cvtsi128_si32(_mm256_castsi256_si128(sum_left));
        sum += 48;
        sum /= 96;
        __m256i row = _mm256_set1_epi8((sbyte)sum);
        row_store_32xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_64x64_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum_above = dc_sum_64(above);
        __m256i sum_left = dc_sum_64(left);
        sum_left = _mm256_add_epi16(sum_left, sum_above);
        ushort sum = (ushort)_mm_cvtsi128_si32(_mm256_castsi256_si128(sum_left));
        sum += 64;
        sum /= 128;
        __m256i row = _mm256_set1_epi8((sbyte)sum);
        row_store_64xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_64x32_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum_above = dc_sum_64(above);
        __m256i sum_left = dc_sum_32(left);
        sum_left = _mm256_add_epi16(sum_left, sum_above);
        ushort sum = (ushort)_mm_cvtsi128_si32(_mm256_castsi256_si128(sum_left));
        sum += 48;
        sum /= 96;
        __m256i row = _mm256_set1_epi8((sbyte)sum);
        row_store_64xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_predictor_64x16_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum_above = dc_sum_64(above);
        __m256i sum_left = _mm256_castsi128_si256(dc_sum_16_sse2(left));
        sum_left = _mm256_add_epi16(sum_left, sum_above);
        ushort sum = (ushort)_mm_cvtsi128_si32(_mm256_castsi256_si128(sum_left));
        sum += 40;
        sum /= 80;
        __m256i row = _mm256_set1_epi8((sbyte)sum);
        row_store_64xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_32x16_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum = dc_sum_32(above);

        __m256i sixteen = _mm256_set1_epi16(16);
        sum = _mm256_add_epi16(sum, sixteen);
        sum = _mm256_srai_epi16(sum, 5);
        __m256i zero = _mm256_setzero_si256();
        __m256i row = _mm256_shuffle_epi8(sum, zero);
        row_store_32xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_32x64_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum = dc_sum_32(above);

        __m256i sixteen = _mm256_set1_epi16(16);
        sum = _mm256_add_epi16(sum, sixteen);
        sum = _mm256_srai_epi16(sum, 5);
        __m256i zero = _mm256_setzero_si256();
        __m256i row = _mm256_shuffle_epi8(sum, zero);
        row_store_32xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_64x64_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum = dc_sum_64(above);

        __m256i thirtytwo = _mm256_set1_epi16(32);
        sum = _mm256_add_epi16(sum, thirtytwo);
        sum = _mm256_srai_epi16(sum, 6);
        __m256i zero = _mm256_setzero_si256();
        __m256i row = _mm256_shuffle_epi8(sum, zero);
        row_store_64xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_64x32_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum = dc_sum_64(above);

        __m256i thirtytwo = _mm256_set1_epi16(32);
        sum = _mm256_add_epi16(sum, thirtytwo);
        sum = _mm256_srai_epi16(sum, 6);
        __m256i zero = _mm256_setzero_si256();
        __m256i row = _mm256_shuffle_epi8(sum, zero);
        row_store_64xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_top_predictor_64x16_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum = dc_sum_64(above);

        __m256i thirtytwo = _mm256_set1_epi16(32);
        sum = _mm256_add_epi16(sum, thirtytwo);
        sum = _mm256_srai_epi16(sum, 6);
        __m256i zero = _mm256_setzero_si256();
        __m256i row = _mm256_shuffle_epi8(sum, zero);
        row_store_64xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_32x16_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum = dc_sum_16_sse2(left);

        __m128i eight = _mm_set1_epi16(8);
        sum = _mm_add_epi16(sum, eight);
        sum = _mm_srai_epi16(sum, 4);
        __m128i zero = _mm_setzero_si128();
        __m128i r = _mm_shuffle_epi8(sum, zero);
        __m256i row = _mm256_inserti128_si256(_mm256_castsi128_si256(r), r, 1);
        row_store_32xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_32x64_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum = dc_sum_64(left);

        __m256i thirtytwo = _mm256_set1_epi16(32);
        sum = _mm256_add_epi16(sum, thirtytwo);
        sum = _mm256_srai_epi16(sum, 6);
        __m256i zero = _mm256_setzero_si256();
        __m256i row = _mm256_shuffle_epi8(sum, zero);
        row_store_32xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_64x64_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum = dc_sum_64(left);

        __m256i thirtytwo = _mm256_set1_epi16(32);
        sum = _mm256_add_epi16(sum, thirtytwo);
        sum = _mm256_srai_epi16(sum, 6);
        __m256i zero = _mm256_setzero_si256();
        __m256i row = _mm256_shuffle_epi8(sum, zero);
        row_store_64xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_64x32_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i sum = dc_sum_32(left);

        __m256i sixteen = _mm256_set1_epi16(16);
        sum = _mm256_add_epi16(sum, sixteen);
        sum = _mm256_srai_epi16(sum, 5);
        __m256i zero = _mm256_setzero_si256();
        __m256i row = _mm256_shuffle_epi8(sum, zero);
        row_store_64xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_left_predictor_64x16_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i sum = dc_sum_16_sse2(left);

        __m128i eight = _mm_set1_epi16(8);
        sum = _mm_add_epi16(sum, eight);
        sum = _mm_srai_epi16(sum, 4);
        __m128i zero = _mm_setzero_si128();
        __m128i r = _mm_shuffle_epi8(sum, zero);
        __m256i row = _mm256_inserti128_si256(_mm256_castsi128_si256(r), r, 1);
        row_store_64xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_32x16_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row = _mm256_set1_epi8(unchecked((sbyte)0x80));
        row_store_32xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_32x64_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row = _mm256_set1_epi8(unchecked((sbyte)0x80));
        row_store_32xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_64x64_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row = _mm256_set1_epi8(unchecked((sbyte)0x80));
        row_store_64xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_64x32_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row = _mm256_set1_epi8(unchecked((sbyte)0x80));
        row_store_64xh(row, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_dc_128_predictor_64x16_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row = _mm256_set1_epi8(unchecked((sbyte)0x80));
        row_store_64xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_32x16_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row = _mm256_loadu_si256(above);
        row_store_32xh(row, 16, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_32x64_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row = _mm256_loadu_si256(above);
        row_store_32xh(row, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_64x64_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row0 = _mm256_loadu_si256(above);
        __m256i row1 = _mm256_loadu_si256(above + 32);
        row_store_32x2xh(row0, row1, 64, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_64x32_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row0 = _mm256_loadu_si256(above);
        __m256i row1 = _mm256_loadu_si256(above + 32);
        row_store_32x2xh(row0, row1, 32, dst, stride);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_v_predictor_64x16_avx2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m256i row0 = _mm256_loadu_si256(above);
        __m256i row1 = _mm256_loadu_si256(above + 32);
        row_store_32x2xh(row0, row1, 16, dst, stride);
    }

    // [subset] PAETH_PRED, the directional (z1/z2/z3) predictors and their transpose helpers:
    // those modes are never dispatched by the port.
}
