// Port of libaom aom_dsp/x86/sad_avx2.c

global using static Av1rt.Aom.sad_avx2;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] The !CONFIG_HIGHWAY intrinsics versions. The FSAD64 / FSADAVG64 / FSAD32 / FSADAVG32
// macro instantiations are written out.
internal static partial class sad_avx2
{
    // SAD, SAD_SKIP and SAD_AVG for 64xh blocks
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint sad64xh_avx2(Ptr<byte> src_ptr, int src_stride,
                                     Ptr<byte> ref_ptr, int ref_stride,
                                     int h)
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
            sad1_reg = _mm256_sad_epu8(ref1_reg, _mm256_loadu_si256(src_ptr));
            sad2_reg = _mm256_sad_epu8(ref2_reg, _mm256_loadu_si256(src_ptr + 32));
            sum_sad = _mm256_add_epi32(sum_sad, _mm256_add_epi32(sad1_reg, sad2_reg));
            ref_ptr += ref_stride;
            src_ptr += src_stride;
        }
        sum_sad_h = _mm256_srli_si256(sum_sad, 8);
        sum_sad = _mm256_add_epi32(sum_sad, sum_sad_h);
        sum_sad128 = _mm256_extracti128_si256(sum_sad, 1);
        sum_sad128 = _mm_add_epi32(_mm256_castsi256_si128(sum_sad), sum_sad128);
        uint res = (uint)_mm_cvtsi128_si32(sum_sad128);
        _mm256_zeroupper();
        return res;
    }

    // FSAD64_H(64), FSAD64_H(32)
    internal static uint aom_sad64x64_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad64xh_avx2(src_ptr, src_stride, ref_ptr, ref_stride, 64);

    internal static uint aom_sad64x32_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad64xh_avx2(src_ptr, src_stride, ref_ptr, ref_stride, 32);

    // FSADS64_H(64), FSADS64_H(32)
    internal static uint aom_sad_skip_64x64_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        2 * sad64xh_avx2(src_ptr, src_stride * 2, ref_ptr, ref_stride * 2, 64 / 2);

    internal static uint aom_sad_skip_64x32_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        2 * sad64xh_avx2(src_ptr, src_stride * 2, ref_ptr, ref_stride * 2, 32 / 2);

    // [port] The body of FSADAVG64_H(h).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FSADAVG64_H(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                    int ref_stride, Ptr<byte> second_pred, int h)
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
            second_pred += 64;
        }
        sum_sad_h = _mm256_srli_si256(sum_sad, 8);
        sum_sad = _mm256_add_epi32(sum_sad, sum_sad_h);
        sum_sad128 = _mm256_extracti128_si256(sum_sad, 1);
        sum_sad128 = _mm_add_epi32(_mm256_castsi256_si128(sum_sad), sum_sad128);
        uint res = (uint)_mm_cvtsi128_si32(sum_sad128);
        _mm256_zeroupper();
        return res;
    }

    internal static uint aom_sad64x64_avg_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                               int ref_stride, Ptr<byte> second_pred) =>
        FSADAVG64_H(src_ptr, src_stride, ref_ptr, ref_stride, second_pred, 64);

    internal static uint aom_sad64x32_avg_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                               int ref_stride, Ptr<byte> second_pred) =>
        FSADAVG64_H(src_ptr, src_stride, ref_ptr, ref_stride, second_pred, 32);

    // SAD, SAD_SKIP and SAD_AVG for 32xh blocks
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint sad32xh_avx2(Ptr<byte> src_ptr, int src_stride,
                                     Ptr<byte> ref_ptr, int ref_stride,
                                     int h)
    {
        int i;
        __m256i sad1_reg, sad2_reg, ref1_reg, ref2_reg;
        __m256i sum_sad = _mm256_setzero_si256();
        __m256i sum_sad_h;
        __m128i sum_sad128;
        int ref2_stride = ref_stride << 1;
        int src2_stride = src_stride << 1;
        int max = h >> 1;
        for (i = 0; i < max; i++)
        {
            ref1_reg = _mm256_loadu_si256(ref_ptr);
            ref2_reg = _mm256_loadu_si256(ref_ptr + ref_stride);
            sad1_reg = _mm256_sad_epu8(ref1_reg, _mm256_loadu_si256(src_ptr));
            sad2_reg = _mm256_sad_epu8(ref2_reg, _mm256_loadu_si256(src_ptr + src_stride));
            sum_sad = _mm256_add_epi32(sum_sad, _mm256_add_epi32(sad1_reg, sad2_reg));
            ref_ptr += ref2_stride;
            src_ptr += src2_stride;
        }
        sum_sad_h = _mm256_srli_si256(sum_sad, 8);
        sum_sad = _mm256_add_epi32(sum_sad, sum_sad_h);
        sum_sad128 = _mm256_extracti128_si256(sum_sad, 1);
        sum_sad128 = _mm_add_epi32(_mm256_castsi256_si128(sum_sad), sum_sad128);
        uint res = (uint)_mm_cvtsi128_si32(sum_sad128);
        _mm256_zeroupper();
        return res;
    }

    // FSAD32_H(64), FSAD32_H(32), FSAD32_H(16)
    internal static uint aom_sad32x64_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad32xh_avx2(src_ptr, src_stride, ref_ptr, ref_stride, 64);

    internal static uint aom_sad32x32_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad32xh_avx2(src_ptr, src_stride, ref_ptr, ref_stride, 32);

    internal static uint aom_sad32x16_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad32xh_avx2(src_ptr, src_stride, ref_ptr, ref_stride, 16);

    // FSADS32_H(64), FSADS32_H(32), FSADS32_H(16)
    internal static uint aom_sad_skip_32x64_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        2 * sad32xh_avx2(src_ptr, src_stride * 2, ref_ptr, ref_stride * 2, 64 / 2);

    internal static uint aom_sad_skip_32x32_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        2 * sad32xh_avx2(src_ptr, src_stride * 2, ref_ptr, ref_stride * 2, 32 / 2);

    internal static uint aom_sad_skip_32x16_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        2 * sad32xh_avx2(src_ptr, src_stride * 2, ref_ptr, ref_stride * 2, 16 / 2);

    // [port] The body of FSADAVG32_H(h).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FSADAVG32_H(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                    int ref_stride, Ptr<byte> second_pred, int h)
    {
        int i;
        __m256i sad1_reg, sad2_reg, ref1_reg, ref2_reg;
        __m256i sum_sad = _mm256_setzero_si256();
        __m256i sum_sad_h;
        __m128i sum_sad128;
        int ref2_stride = ref_stride << 1;
        int src2_stride = src_stride << 1;
        int max = h >> 1;
        for (i = 0; i < max; i++)
        {
            ref1_reg = _mm256_loadu_si256(ref_ptr);
            ref2_reg = _mm256_loadu_si256(ref_ptr + ref_stride);
            ref1_reg = _mm256_avg_epu8(ref1_reg, _mm256_loadu_si256(second_pred));
            ref2_reg = _mm256_avg_epu8(ref2_reg, _mm256_loadu_si256(second_pred + 32));
            sad1_reg = _mm256_sad_epu8(ref1_reg, _mm256_loadu_si256(src_ptr));
            sad2_reg = _mm256_sad_epu8(ref2_reg, _mm256_loadu_si256(src_ptr + src_stride));
            sum_sad = _mm256_add_epi32(sum_sad, _mm256_add_epi32(sad1_reg, sad2_reg));
            ref_ptr += ref2_stride;
            src_ptr += src2_stride;
            second_pred += 64;
        }
        sum_sad_h = _mm256_srli_si256(sum_sad, 8);
        sum_sad = _mm256_add_epi32(sum_sad, sum_sad_h);
        sum_sad128 = _mm256_extracti128_si256(sum_sad, 1);
        sum_sad128 = _mm_add_epi32(_mm256_castsi256_si128(sum_sad), sum_sad128);
        uint res = (uint)_mm_cvtsi128_si32(sum_sad128);
        _mm256_zeroupper();
        return res;
    }

    internal static uint aom_sad32x64_avg_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                               int ref_stride, Ptr<byte> second_pred) =>
        FSADAVG32_H(src_ptr, src_stride, ref_ptr, ref_stride, second_pred, 64);

    internal static uint aom_sad32x32_avg_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                               int ref_stride, Ptr<byte> second_pred) =>
        FSADAVG32_H(src_ptr, src_stride, ref_ptr, ref_stride, second_pred, 32);

    internal static uint aom_sad32x16_avg_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                               int ref_stride, Ptr<byte> second_pred) =>
        FSADAVG32_H(src_ptr, src_stride, ref_ptr, ref_stride, second_pred, 16);
}
