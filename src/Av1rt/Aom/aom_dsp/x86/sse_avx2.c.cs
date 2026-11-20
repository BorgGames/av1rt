// Port of libaom aom_dsp/x86/sse_avx2.c

global using static Av1rt.Aom.sse_avx2;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [subset] aom_highbd_sse_avx2 and its helpers: CONFIG_AV1_HIGHBITDEPTH == 0.
internal static class sse_avx2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sse_w32_avx2(ref __m256i sum, Ptr<byte> a,
                                     Ptr<byte> b)
    {
        __m256i v_a0 = yy_loadu_256(a);
        __m256i v_b0 = yy_loadu_256(b);
        __m256i zero = _mm256_setzero_si256();
        __m256i v_a00_w = _mm256_unpacklo_epi8(v_a0, zero);
        __m256i v_a01_w = _mm256_unpackhi_epi8(v_a0, zero);
        __m256i v_b00_w = _mm256_unpacklo_epi8(v_b0, zero);
        __m256i v_b01_w = _mm256_unpackhi_epi8(v_b0, zero);
        __m256i v_d00_w = _mm256_sub_epi16(v_a00_w, v_b00_w);
        __m256i v_d01_w = _mm256_sub_epi16(v_a01_w, v_b01_w);
        sum = _mm256_add_epi32(sum, _mm256_madd_epi16(v_d00_w, v_d00_w));
        sum = _mm256_add_epi32(sum, _mm256_madd_epi16(v_d01_w, v_d01_w));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long summary_all_avx2(in __m256i sum_all)
    {
        long sum;
        __m256i zero = _mm256_setzero_si256();
        __m256i sum0_4x64 = _mm256_unpacklo_epi32(sum_all, zero);
        __m256i sum1_4x64 = _mm256_unpackhi_epi32(sum_all, zero);
        __m256i sum_4x64 = _mm256_add_epi64(sum0_4x64, sum1_4x64);
        __m128i sum_2x64 = _mm_add_epi64(_mm256_castsi256_si128(sum_4x64),
                                         _mm256_extracti128_si256(sum_4x64, 1));
        __m128i sum_1x64 = _mm_add_epi64(sum_2x64, _mm_srli_si128(sum_2x64, 8));
        sum = _mm_cvtsi128_si64(sum_1x64);  // xx_storel_64(&sum, sum_1x64)
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sse_w4x4_avx2(Ptr<byte> a, int a_stride,
                                      Ptr<byte> b, int b_stride, ref __m256i sum)
    {
        __m128i v_a0 = xx_loadl_32(a);
        __m128i v_a1 = xx_loadl_32(a + a_stride);
        __m128i v_a2 = xx_loadl_32(a + a_stride * 2);
        __m128i v_a3 = xx_loadl_32(a + a_stride * 3);
        __m128i v_b0 = xx_loadl_32(b);
        __m128i v_b1 = xx_loadl_32(b + b_stride);
        __m128i v_b2 = xx_loadl_32(b + b_stride * 2);
        __m128i v_b3 = xx_loadl_32(b + b_stride * 3);
        __m128i v_a0123 = _mm_unpacklo_epi64(_mm_unpacklo_epi32(v_a0, v_a1),
                                             _mm_unpacklo_epi32(v_a2, v_a3));
        __m128i v_b0123 = _mm_unpacklo_epi64(_mm_unpacklo_epi32(v_b0, v_b1),
                                             _mm_unpacklo_epi32(v_b2, v_b3));
        __m256i v_a_w = _mm256_cvtepu8_epi16(v_a0123);
        __m256i v_b_w = _mm256_cvtepu8_epi16(v_b0123);
        __m256i v_d_w = _mm256_sub_epi16(v_a_w, v_b_w);
        sum = _mm256_add_epi32(sum, _mm256_madd_epi16(v_d_w, v_d_w));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sse_w8x2_avx2(Ptr<byte> a, int a_stride,
                                      Ptr<byte> b, int b_stride, ref __m256i sum)
    {
        __m128i v_a0 = xx_loadl_64(a);
        __m128i v_a1 = xx_loadl_64(a + a_stride);
        __m128i v_b0 = xx_loadl_64(b);
        __m128i v_b1 = xx_loadl_64(b + b_stride);
        __m256i v_a_w = _mm256_cvtepu8_epi16(_mm_unpacklo_epi64(v_a0, v_a1));
        __m256i v_b_w = _mm256_cvtepu8_epi16(_mm_unpacklo_epi64(v_b0, v_b1));
        __m256i v_d_w = _mm256_sub_epi16(v_a_w, v_b_w);
        sum = _mm256_add_epi32(sum, _mm256_madd_epi16(v_d_w, v_d_w));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static long aom_sse_avx2(Ptr<byte> a, int a_stride, Ptr<byte> b,
                                      int b_stride, int width, int height)
    {
        int y = 0;
        long sse = 0;
        __m256i sum = _mm256_setzero_si256();
        __m256i zero = _mm256_setzero_si256();
        switch (width)
        {
            case 4:
                do
                {
                    sse_w4x4_avx2(a, a_stride, b, b_stride, ref sum);
                    a += a_stride << 2;
                    b += b_stride << 2;
                    y += 4;
                } while (y < height);
                sse = summary_all_avx2(sum);
                break;
            case 8:
                do
                {
                    sse_w8x2_avx2(a, a_stride, b, b_stride, ref sum);
                    a += a_stride << 1;
                    b += b_stride << 1;
                    y += 2;
                } while (y < height);
                sse = summary_all_avx2(sum);
                break;
            case 16:
                do
                {
                    __m128i v_a0 = xx_loadu_128(a);
                    __m128i v_a1 = xx_loadu_128(a + a_stride);
                    __m128i v_b0 = xx_loadu_128(b);
                    __m128i v_b1 = xx_loadu_128(b + b_stride);
                    __m256i v_a =
                        _mm256_insertf128_si256(_mm256_castsi128_si256(v_a0), v_a1, 0x01);
                    __m256i v_b =
                        _mm256_insertf128_si256(_mm256_castsi128_si256(v_b0), v_b1, 0x01);
                    __m256i v_al = _mm256_unpacklo_epi8(v_a, zero);
                    __m256i v_au = _mm256_unpackhi_epi8(v_a, zero);
                    __m256i v_bl = _mm256_unpacklo_epi8(v_b, zero);
                    __m256i v_bu = _mm256_unpackhi_epi8(v_b, zero);
                    __m256i v_asub = _mm256_sub_epi16(v_al, v_bl);
                    __m256i v_bsub = _mm256_sub_epi16(v_au, v_bu);
                    __m256i temp =
                        _mm256_add_epi32(_mm256_madd_epi16(v_asub, v_asub),
                                         _mm256_madd_epi16(v_bsub, v_bsub));
                    sum = _mm256_add_epi32(sum, temp);
                    a += a_stride << 1;
                    b += b_stride << 1;
                    y += 2;
                } while (y < height);
                sse = summary_all_avx2(sum);
                break;
            case 32:
                do
                {
                    sse_w32_avx2(ref sum, a, b);
                    a += a_stride;
                    b += b_stride;
                    y += 1;
                } while (y < height);
                sse = summary_all_avx2(sum);
                break;
            case 64:
                do
                {
                    sse_w32_avx2(ref sum, a, b);
                    sse_w32_avx2(ref sum, a + 32, b + 32);
                    a += a_stride;
                    b += b_stride;
                    y += 1;
                } while (y < height);
                sse = summary_all_avx2(sum);
                break;
            case 128:
                do
                {
                    sse_w32_avx2(ref sum, a, b);
                    sse_w32_avx2(ref sum, a + 32, b + 32);
                    sse_w32_avx2(ref sum, a + 64, b + 64);
                    sse_w32_avx2(ref sum, a + 96, b + 96);
                    a += a_stride;
                    b += b_stride;
                    y += 1;
                } while (y < height);
                sse = summary_all_avx2(sum);
                break;
            default:
                if ((width & 0x07) == 0)
                {
                    do
                    {
                        int i = 0;
                        do
                        {
                            sse_w8x2_avx2(a + i, a_stride, b + i, b_stride, ref sum);
                            i += 8;
                        } while (i < width);
                        a += a_stride << 1;
                        b += b_stride << 1;
                        y += 2;
                    } while (y < height);
                }
                else
                {
                    do
                    {
                        int i = 0;
                        do
                        {
                            sse_w8x2_avx2(a + i, a_stride, b + i, b_stride, ref sum);
                            Ptr<byte> a2 = a + i + (a_stride << 1);
                            Ptr<byte> b2 = b + i + (b_stride << 1);
                            sse_w8x2_avx2(a2, a_stride, b2, b_stride, ref sum);
                            i += 8;
                        } while (i + 4 < width);
                        sse_w4x4_avx2(a + i, a_stride, b + i, b_stride, ref sum);
                        a += a_stride << 2;
                        b += b_stride << 2;
                        y += 4;
                    } while (y < height);
                }
                sse = summary_all_avx2(sum);
                break;
        }

        return sse;
    }
}
