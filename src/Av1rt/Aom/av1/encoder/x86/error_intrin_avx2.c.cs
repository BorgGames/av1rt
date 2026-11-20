// Port of libaom av1/encoder/x86/error_intrin_avx2.c

global using static Av1rt.Aom.error_intrin_avx2;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [subset] read_coeff, av1_block_error_avx2 and av1_block_error_qm_avx2 are not ported: the
// realtime subset only uses av1_block_error_lp (the nonrd RD estimate); av1_block_error is not
// part of the C port either (see rdopt.c.cs).
internal static partial class error_intrin_avx2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void av1_block_error_block_size16_avx2(Ptr<short> coeff,
                                                          Ptr<short> dqcoeff,
                                                          ref __m256i sse_256)
    {
        __m256i _coeff = _mm256_loadu_si256(coeff);
        __m256i _dqcoeff = _mm256_loadu_si256(dqcoeff);
        // d0 d1 d2 d3 d4 d5 d6 d7 d8 d9 d10 d11 d12 d13 d14 d15
        __m256i diff = _mm256_sub_epi16(_dqcoeff, _coeff);
        // r0 r1 r2 r3 r4 r5 r6 r7
        __m256i error = _mm256_madd_epi16(diff, diff);
        // r0+r1 r2+r3 | r0+r1 r2+r3 | r4+r5 r6+r7 | r4+r5 r6+r7
        __m256i error_hi = _mm256_hadd_epi32(error, error);
        // r0+r1 | r2+r3 | r4+r5 | r6+r7
        sse_256 = _mm256_unpacklo_epi32(error_hi, _mm256_setzero_si256());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void av1_block_error_block_size32_avx2(Ptr<short> coeff,
                                                          Ptr<short> dqcoeff,
                                                          ref __m256i sse_256)
    {
        __m256i zero = _mm256_setzero_si256();
        __m256i _coeff_0 = _mm256_loadu_si256(coeff);
        __m256i _dqcoeff_0 = _mm256_loadu_si256(dqcoeff);
        __m256i _coeff_1 = _mm256_loadu_si256(coeff + 16);
        __m256i _dqcoeff_1 =
            _mm256_loadu_si256(dqcoeff + 16);

        // d0 d1 d2 d3 d4 d5 d6 d7 d8 d9 d10 d11 d12 d13 d14 d15
        __m256i diff_0 = _mm256_sub_epi16(_dqcoeff_0, _coeff_0);
        __m256i diff_1 = _mm256_sub_epi16(_dqcoeff_1, _coeff_1);

        // r0 r1 r2 r3 r4 r5 r6 r7
        __m256i error_0 = _mm256_madd_epi16(diff_0, diff_0);
        __m256i error_1 = _mm256_madd_epi16(diff_1, diff_1);
        __m256i err_final_0 = _mm256_add_epi32(error_0, error_1);

        // For extreme input values, the accumulation needs to happen in 64 bit
        // precision to avoid any overflow.
        __m256i exp0_error_lo = _mm256_unpacklo_epi32(err_final_0, zero);
        __m256i exp0_error_hi = _mm256_unpackhi_epi32(err_final_0, zero);
        __m256i sum_temp_0 = _mm256_add_epi64(exp0_error_hi, exp0_error_lo);
        sse_256 = _mm256_add_epi64(sse_256, sum_temp_0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void av1_block_error_block_size64_avx2(Ptr<short> coeff,
                                                          Ptr<short> dqcoeff,
                                                          ref __m256i sse_256,
                                                          int block_size)
    {
        __m256i zero = _mm256_setzero_si256();
        for (int i = 0; i < block_size; i += 64)
        {
            // Load 64 elements for coeff and dqcoeff.
            __m256i _coeff_0 = _mm256_loadu_si256(coeff);
            __m256i _dqcoeff_0 = _mm256_loadu_si256(dqcoeff);
            __m256i _coeff_1 = _mm256_loadu_si256(coeff + 16);
            __m256i _dqcoeff_1 =
                _mm256_loadu_si256(dqcoeff + 16);
            __m256i _coeff_2 = _mm256_loadu_si256(coeff + 32);
            __m256i _dqcoeff_2 =
                _mm256_loadu_si256(dqcoeff + 32);
            __m256i _coeff_3 = _mm256_loadu_si256(coeff + 48);
            __m256i _dqcoeff_3 =
                _mm256_loadu_si256(dqcoeff + 48);

            // d0 d1 d2 d3 d4 d5 d6 d7 d8 d9 d10 d11 d12 d13 d14 d15
            __m256i diff_0 = _mm256_sub_epi16(_dqcoeff_0, _coeff_0);
            __m256i diff_1 = _mm256_sub_epi16(_dqcoeff_1, _coeff_1);
            __m256i diff_2 = _mm256_sub_epi16(_dqcoeff_2, _coeff_2);
            __m256i diff_3 = _mm256_sub_epi16(_dqcoeff_3, _coeff_3);

            // r0 r1 r2 r3 r4 r5 r6 r7
            __m256i error_0 = _mm256_madd_epi16(diff_0, diff_0);
            __m256i error_1 = _mm256_madd_epi16(diff_1, diff_1);
            __m256i error_2 = _mm256_madd_epi16(diff_2, diff_2);
            __m256i error_3 = _mm256_madd_epi16(diff_3, diff_3);
            // r00 r01 r02 r03 r04 r05 r06 r07
            __m256i err_final_0 = _mm256_add_epi32(error_0, error_1);
            // r10 r11 r12 r13 r14 r15 r16 r17
            __m256i err_final_1 = _mm256_add_epi32(error_2, error_3);

            // For extreme input values, the accumulation needs to happen in 64 bit
            // precision to avoid any overflow. r00 r01 r04 r05
            __m256i exp0_error_lo = _mm256_unpacklo_epi32(err_final_0, zero);
            // r02 r03 r06 r07
            __m256i exp0_error_hi = _mm256_unpackhi_epi32(err_final_0, zero);
            // r10 r11 r14 r15
            __m256i exp1_error_lo = _mm256_unpacklo_epi32(err_final_1, zero);
            // r12 r13 r16 r17
            __m256i exp1_error_hi = _mm256_unpackhi_epi32(err_final_1, zero);

            __m256i sum_temp_0 = _mm256_add_epi64(exp0_error_hi, exp0_error_lo);
            __m256i sum_temp_1 = _mm256_add_epi64(exp1_error_hi, exp1_error_lo);
            __m256i sse_256_temp = _mm256_add_epi64(sum_temp_1, sum_temp_0);
            sse_256 = _mm256_add_epi64(sse_256, sse_256_temp);
            coeff += 64;
            dqcoeff += 64;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static long av1_block_error_lp_avx2(Ptr<short> coeff, Ptr<short> dqcoeff,
                                                 int block_size)
    {
        Debug.Assert(block_size % 16 == 0);
        __m256i sse_256 = _mm256_setzero_si256();
        // [port] int64_t sse, stored through (__m128i *)&sse: a one-element span.
        Span<long> sse = stackalloc long[1];

        if (block_size == 16)
            av1_block_error_block_size16_avx2(coeff, dqcoeff, ref sse_256);
        else if (block_size == 32)
            av1_block_error_block_size32_avx2(coeff, dqcoeff, ref sse_256);
        else
            av1_block_error_block_size64_avx2(coeff, dqcoeff, ref sse_256, block_size);

        // Save the higher 64 bit of each 128 bit lane.
        __m256i sse_hi = _mm256_srli_si256(sse_256, 8);
        // Add the higher 64 bit to the low 64 bit.
        sse_256 = _mm256_add_epi64(sse_256, sse_hi);
        // Accumulate the sse_256 register to get final sse
        __m128i sse_128 = _mm_add_epi64(_mm256_castsi256_si128(sse_256),
                                        _mm256_extractf128_si256(sse_256, 1));

        // Store the results.
        _mm_storel_epi64(sse, sse_128);
        return sse[0];
    }
}
