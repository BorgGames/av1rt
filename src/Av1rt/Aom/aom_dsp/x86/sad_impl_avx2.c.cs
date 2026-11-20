// Port of libaom aom_dsp/x86/sad_impl_avx2.c

global using static Av1rt.Aom.sad_impl_avx2;

namespace Av1rt.Aom;

internal static class sad_impl_avx2
{
    private static uint sad32x32(Ptr<byte> src_ptr, int src_stride,
                                 Ptr<byte> ref_ptr, int ref_stride)
    {
        __m256i s1, s2, r1, r2;
        __m256i sum = _mm256_setzero_si256();
        __m128i sum_i128;
        int i;

        for (i = 0; i < 16; ++i)
        {
            r1 = _mm256_loadu_si256(ref_ptr);
            r2 = _mm256_loadu_si256(ref_ptr + ref_stride);
            s1 = _mm256_sad_epu8(r1, _mm256_loadu_si256(src_ptr));
            s2 = _mm256_sad_epu8(r2, _mm256_loadu_si256(src_ptr + src_stride));
            sum = _mm256_add_epi32(sum, _mm256_add_epi32(s1, s2));
            ref_ptr += ref_stride << 1;
            src_ptr += src_stride << 1;
        }

        sum = _mm256_add_epi32(sum, _mm256_srli_si256(sum, 8));
        sum_i128 = _mm_add_epi32(_mm256_extracti128_si256(sum, 1),
                                 _mm256_castsi256_si128(sum));
        return (uint)_mm_cvtsi128_si32(sum_i128);
    }

    private static uint sad64x32(Ptr<byte> src_ptr, int src_stride,
                                 Ptr<byte> ref_ptr, int ref_stride)
    {
        int half_width = 32;  // unsigned int half_width = 32
        uint sum = sad32x32(src_ptr, src_stride, ref_ptr, ref_stride);
        src_ptr += half_width;
        ref_ptr += half_width;
        sum += sad32x32(src_ptr, src_stride, ref_ptr, ref_stride);
        return sum;
    }

    private static uint sad64x64(Ptr<byte> src_ptr, int src_stride,
                                 Ptr<byte> ref_ptr, int ref_stride)
    {
        uint sum = sad64x32(src_ptr, src_stride, ref_ptr, ref_stride);
        src_ptr += src_stride << 5;
        ref_ptr += ref_stride << 5;
        sum += sad64x32(src_ptr, src_stride, ref_ptr, ref_stride);
        return sum;
    }

    internal static uint aom_sad128x64_avx2(Ptr<byte> src_ptr, int src_stride,
                                            Ptr<byte> ref_ptr, int ref_stride)
    {
        int half_width = 64;  // unsigned int half_width = 64
        uint sum = sad64x64(src_ptr, src_stride, ref_ptr, ref_stride);
        src_ptr += half_width;
        ref_ptr += half_width;
        sum += sad64x64(src_ptr, src_stride, ref_ptr, ref_stride);
        return sum;
    }

    internal static uint aom_sad64x128_avx2(Ptr<byte> src_ptr, int src_stride,
                                            Ptr<byte> ref_ptr, int ref_stride)
    {
        uint sum = sad64x64(src_ptr, src_stride, ref_ptr, ref_stride);
        src_ptr += src_stride << 6;
        ref_ptr += ref_stride << 6;
        sum += sad64x64(src_ptr, src_stride, ref_ptr, ref_stride);
        return sum;
    }

    internal static uint aom_sad128x128_avx2(Ptr<byte> src_ptr, int src_stride,
                                             Ptr<byte> ref_ptr, int ref_stride)
    {
        uint sum = aom_sad128x64_avx2(src_ptr, src_stride, ref_ptr, ref_stride);
        src_ptr += src_stride << 6;
        ref_ptr += ref_stride << 6;
        sum += aom_sad128x64_avx2(src_ptr, src_stride, ref_ptr, ref_stride);
        return sum;
    }

    internal static uint aom_sad_skip_128x64_avx2(Ptr<byte> src_ptr, int src_stride,
                                                  Ptr<byte> ref_ptr, int ref_stride)
    {
        const int half_width = 64;  // const uint32_t half_width = 64
        uint sum = sad64x32(src_ptr, src_stride * 2, ref_ptr, ref_stride * 2);
        src_ptr += half_width;
        ref_ptr += half_width;
        sum += sad64x32(src_ptr, src_stride * 2, ref_ptr, ref_stride * 2);
        return 2 * sum;
    }

    internal static uint aom_sad_skip_64x128_avx2(Ptr<byte> src_ptr, int src_stride,
                                                  Ptr<byte> ref_ptr, int ref_stride)
    {
        uint sum =
            sad64x64(src_ptr, 2 * src_stride, ref_ptr, 2 * ref_stride);
        return 2 * sum;
    }

    internal static uint aom_sad_skip_128x128_avx2(Ptr<byte> src_ptr, int src_stride,
                                                   Ptr<byte> ref_ptr, int ref_stride)
    {
        uint sum =
            aom_sad128x64_avx2(src_ptr, 2 * src_stride, ref_ptr, 2 * ref_stride);
        return 2 * sum;
    }

    private static uint sad_w64_avg_avx2(Ptr<byte> src_ptr, int src_stride,
                                         Ptr<byte> ref_ptr, int ref_stride,
                                         int h, Ptr<byte> second_pred,
                                         int second_pred_stride)
    {
        int i;
        __m256i sad1_reg, sad2_reg, ref1_reg, ref2_reg;
        __m256i sum_sad = _mm256_setzero_si256();
        __m256i sum_sad_h;
        __m128i sum_sad128;
        for (i = 0; i < h; i++)
        {
            ref1_reg = _mm256_loadu_si256(ref_ptr);
            ref2_reg = _mm256_loadu_si256(ref_ptr + 32);
            ref1_reg = _mm256_avg_epu8(ref1_reg, _mm256_loadu_si256(second_pred));
            ref2_reg = _mm256_avg_epu8(ref2_reg, _mm256_loadu_si256(second_pred + 32));
            sad1_reg = _mm256_sad_epu8(ref1_reg, _mm256_loadu_si256(src_ptr));
            sad2_reg = _mm256_sad_epu8(ref2_reg, _mm256_loadu_si256(src_ptr + 32));
            sum_sad = _mm256_add_epi32(sum_sad, _mm256_add_epi32(sad1_reg, sad2_reg));
            ref_ptr += ref_stride;
            src_ptr += src_stride;
            second_pred += second_pred_stride;
        }
        sum_sad_h = _mm256_srli_si256(sum_sad, 8);
        sum_sad = _mm256_add_epi32(sum_sad, sum_sad_h);
        sum_sad128 = _mm256_extracti128_si256(sum_sad, 1);
        sum_sad128 = _mm_add_epi32(_mm256_castsi256_si128(sum_sad), sum_sad128);
        return (uint)_mm_cvtsi128_si32(sum_sad128);
    }

    internal static uint aom_sad64x128_avg_avx2(Ptr<byte> src_ptr, int src_stride,
                                                Ptr<byte> ref_ptr, int ref_stride,
                                                Ptr<byte> second_pred)
    {
        uint sum = sad_w64_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, 64,
                                    second_pred, 64);
        src_ptr += src_stride << 6;
        ref_ptr += ref_stride << 6;
        second_pred += 64 << 6;
        sum += sad_w64_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, 64,
                                second_pred, 64);
        return sum;
    }

    internal static uint aom_sad128x64_avg_avx2(Ptr<byte> src_ptr, int src_stride,
                                                Ptr<byte> ref_ptr, int ref_stride,
                                                Ptr<byte> second_pred)
    {
        int half_width = 64;  // unsigned int half_width = 64
        uint sum = sad_w64_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, 64,
                                    second_pred, 128);
        src_ptr += half_width;
        ref_ptr += half_width;
        second_pred += half_width;
        sum += sad_w64_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride, 64,
                                second_pred, 128);
        return sum;
    }

    internal static uint aom_sad128x128_avg_avx2(Ptr<byte> src_ptr, int src_stride,
                                                 Ptr<byte> ref_ptr, int ref_stride,
                                                 Ptr<byte> second_pred)
    {
        uint sum = aom_sad128x64_avg_avx2(src_ptr, src_stride, ref_ptr,
                                          ref_stride, second_pred);
        src_ptr += src_stride << 6;
        ref_ptr += ref_stride << 6;
        second_pred += 128 << 6;
        sum += aom_sad128x64_avg_avx2(src_ptr, src_stride, ref_ptr, ref_stride,
                                      second_pred);
        return sum;
    }
}
