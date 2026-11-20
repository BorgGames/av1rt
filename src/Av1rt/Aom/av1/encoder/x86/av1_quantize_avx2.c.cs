// Port of libaom av1/encoder/x86/av1_quantize_avx2.c

global using static Av1rt.Aom.av1_quantize_avx2;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [subset] Only av1_quantize_lp_avx2 (and the helpers it uses) is ported. write_zero,
// init_one_qp, init_qp, update_qp, load_coefficients_avx2, store_coefficients_avx2,
// quant_gather_eob, get_max_lane_eob, quantize_fp_16, av1_quantize_fp_avx2,
// quantize_fp_32x32, av1_quantize_fp_32x32_avx2, quantize_fp_64x64,
// av1_quantize_fp_64x64_avx2 and the *_qm_* functions back av1_quantize_fp* /
// av1_quantize_b_qm, which the realtime subset never calls (the encode path uses
// AV1_XFORM_QUANT_B without quantization matrices; see av1_quantize.c.cs); the highbd ones are
// high bitdepth.
internal static partial class av1_quantize_avx2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short accumulate_eob256(__m256i eob256)
    {
        __m128i eob_lo = _mm256_castsi256_si128(eob256);
        __m128i eob_hi = _mm256_extractf128_si256(eob256, 1);
        __m128i eob = _mm_max_epi16(eob_lo, eob_hi);
        __m128i eob_shuffled = _mm_shuffle_epi32(eob, 0xe);
        eob = _mm_max_epi16(eob, eob_shuffled);
        eob_shuffled = _mm_shufflelo_epi16(eob, 0xe);
        eob = _mm_max_epi16(eob, eob_shuffled);
        eob_shuffled = _mm_shufflelo_epi16(eob, 0x1);
        eob = _mm_max_epi16(eob, eob_shuffled);
        return (short)_mm_extract_epi16(eob, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void quantize_lp_16_first(
        Ptr<short> coeff_ptr, Ptr<short> iscan_ptr, Ptr<short> qcoeff_ptr,
        Ptr<short> dqcoeff_ptr, ref __m256i round256, ref __m256i quant256,
        ref __m256i dequant256, ref __m256i eob)
    {
        __m256i coeff = _mm256_loadu_si256(coeff_ptr);
        __m256i abs_coeff = _mm256_abs_epi16(coeff);
        __m256i tmp_rnd = _mm256_adds_epi16(abs_coeff, round256);
        __m256i abs_qcoeff = _mm256_mulhi_epi16(tmp_rnd, quant256);
        __m256i qcoeff = _mm256_sign_epi16(abs_qcoeff, coeff);
        __m256i dqcoeff = _mm256_mullo_epi16(qcoeff, dequant256);
        __m256i nz_mask =
            _mm256_cmpgt_epi16(abs_qcoeff, _mm256_setzero_si256());

        _mm256_storeu_si256(qcoeff_ptr, qcoeff);
        _mm256_storeu_si256(dqcoeff_ptr, dqcoeff);

        __m256i iscan = _mm256_loadu_si256(iscan_ptr);
        __m256i iscan_plus1 = _mm256_sub_epi16(iscan, nz_mask);
        __m256i nz_iscan = _mm256_and_si256(iscan_plus1, nz_mask);
        eob = _mm256_max_epi16(eob, nz_iscan);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void quantize_lp_16(
        Ptr<short> coeff_ptr, int n_coeffs, Ptr<short> iscan_ptr,
        Ptr<short> qcoeff_ptr, Ptr<short> dqcoeff_ptr, ref __m256i round256,
        ref __m256i quant256, ref __m256i dequant256, ref __m256i eob)
    {
        __m256i coeff =
            _mm256_loadu_si256(coeff_ptr + n_coeffs);
        __m256i abs_coeff = _mm256_abs_epi16(coeff);
        __m256i tmp_rnd = _mm256_adds_epi16(abs_coeff, round256);
        __m256i abs_qcoeff = _mm256_mulhi_epi16(tmp_rnd, quant256);
        __m256i qcoeff = _mm256_sign_epi16(abs_qcoeff, coeff);
        __m256i dqcoeff = _mm256_mullo_epi16(qcoeff, dequant256);
        __m256i nz_mask =
            _mm256_cmpgt_epi16(abs_qcoeff, _mm256_setzero_si256());

        _mm256_storeu_si256(qcoeff_ptr + n_coeffs, qcoeff);
        _mm256_storeu_si256(dqcoeff_ptr + n_coeffs, dqcoeff);

        __m256i iscan =
            _mm256_loadu_si256(iscan_ptr + n_coeffs);
        __m256i iscan_plus1 = _mm256_sub_epi16(iscan, nz_mask);
        __m256i nz_iscan = _mm256_and_si256(iscan_plus1, nz_mask);
        eob = _mm256_max_epi16(eob, nz_iscan);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void av1_quantize_lp_avx2(Ptr<short> coeff_ptr, int n_coeffs,
                                              Ptr<short> round_ptr, Ptr<short> quant_ptr,
                                              Ptr<short> qcoeff_ptr, Ptr<short> dqcoeff_ptr,
                                              Ptr<short> dequant_ptr, Ptr<ushort> eob_ptr,
                                              Ptr<short> scan, Ptr<short> iscan)
    {
        _ = scan;
        __m256i eob256 = _mm256_setzero_si256();

        // Setup global values.
        __m256i round256 =
            _mm256_castsi128_si256(_mm_load_si128(round_ptr));
        __m256i quant256 =
            _mm256_castsi128_si256(_mm_load_si128(quant_ptr));
        __m256i dequant256 =
            _mm256_castsi128_si256(_mm_load_si128(dequant_ptr));

        // Populate upper AC values.
        round256 = _mm256_permute4x64_epi64(round256, 0x54);
        quant256 = _mm256_permute4x64_epi64(quant256, 0x54);
        dequant256 = _mm256_permute4x64_epi64(dequant256, 0x54);

        // Process DC and the first 15 AC coeffs.
        quantize_lp_16_first(coeff_ptr, iscan, qcoeff_ptr, dqcoeff_ptr, ref round256,
                             ref quant256, ref dequant256, ref eob256);

        if (n_coeffs > 16)
        {
            // Overwrite the DC constants with AC constants
            dequant256 = _mm256_permute2x128_si256(dequant256, dequant256, 0x31);
            quant256 = _mm256_permute2x128_si256(quant256, quant256, 0x31);
            round256 = _mm256_permute2x128_si256(round256, round256, 0x31);

            // AC only loop.
            for (int idx = 16; idx < n_coeffs; idx += 16)
            {
                quantize_lp_16(coeff_ptr, idx, iscan, qcoeff_ptr, dqcoeff_ptr, ref round256,
                               ref quant256, ref dequant256, ref eob256);
            }
        }

        eob_ptr.Value = (ushort)accumulate_eob256(eob256);
    }
}
