// Port of libaom aom_dsp/x86/sad_avx2.c

global using static Av1rt.Aom.sad_avx2;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] The !CONFIG_HIGHWAY intrinsics versions.
internal static partial class sad_avx2
{
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

    internal static uint aom_sad32x32_avx2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad32xh_avx2(src_ptr, src_stride, ref_ptr, ref_stride, 32);
}
