// Port of libaom aom_dsp/x86/quantize_avx2.c

global using static Av1rt.Aom.quantize_avx2;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [subset] aom_quantize_b_64x64_avx2: TX64 is disabled (log_scale 2 never happens; the C
// port has no aom_quantize_b_64x64 either). quantize_x86.h is included by the C file but
// nothing from it is used by the functions below.
internal static partial class quantize_avx2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void load_b_values_avx2(Ptr<short> zbin_ptr, out __m256i zbin,
                                           Ptr<short> round_ptr, out __m256i round,
                                           Ptr<short> quant_ptr, out __m256i quant,
                                           Ptr<short> dequant_ptr,
                                           out __m256i dequant,
                                           Ptr<short> shift_ptr, out __m256i shift,
                                           int log_scale)
    {
        zbin = _mm256_castsi128_si256(_mm_load_si128(zbin_ptr));
        zbin = _mm256_permute4x64_epi64(zbin, 0x54);
        if (log_scale > 0)
        {
            __m256i rnd = _mm256_set1_epi16((short)(1 << (log_scale - 1)));
            zbin = _mm256_add_epi16(zbin, rnd);
            zbin = _mm256_srai_epi16(zbin, log_scale);
        }
        // Subtracting 1 here eliminates a _mm256_cmpeq_epi16() instruction when
        // calculating the zbin mask. (See quantize_b_logscale{0,1,2}_16)
        zbin = _mm256_sub_epi16(zbin, _mm256_set1_epi16(1));

        round = _mm256_castsi128_si256(_mm_load_si128(round_ptr));
        round = _mm256_permute4x64_epi64(round, 0x54);
        if (log_scale > 0)
        {
            __m256i rnd = _mm256_set1_epi16((short)(1 << (log_scale - 1)));
            round = _mm256_add_epi16(round, rnd);
            round = _mm256_srai_epi16(round, log_scale);
        }

        quant = _mm256_castsi128_si256(_mm_load_si128(quant_ptr));
        quant = _mm256_permute4x64_epi64(quant, 0x54);
        dequant =
            _mm256_castsi128_si256(_mm_load_si128(dequant_ptr));
        dequant = _mm256_permute4x64_epi64(dequant, 0x54);
        shift = _mm256_castsi128_si256(_mm_load_si128(shift_ptr));
        shift = _mm256_permute4x64_epi64(shift, 0x54);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i load_coefficients_avx2(Ptr<tran_low_t> coeff_ptr)
    {
        __m256i coeff1 = _mm256_load_si256(coeff_ptr);
        __m256i coeff2 = _mm256_load_si256(coeff_ptr + 8);
        return _mm256_packs_epi32(coeff1, coeff2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void store_coefficients_avx2(__m256i coeff_vals,
                                                Ptr<tran_low_t> coeff_ptr)
    {
        __m256i coeff_sign = _mm256_srai_epi16(coeff_vals, 15);
        __m256i coeff_vals_lo = _mm256_unpacklo_epi16(coeff_vals, coeff_sign);
        __m256i coeff_vals_hi = _mm256_unpackhi_epi16(coeff_vals, coeff_sign);
        _mm256_store_si256(coeff_ptr, coeff_vals_lo);
        _mm256_store_si256(coeff_ptr + 8, coeff_vals_hi);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i quantize_b_logscale0_16(
        Ptr<tran_low_t> coeff_ptr, Ptr<tran_low_t> qcoeff_ptr,
        Ptr<tran_low_t> dqcoeff_ptr, ref __m256i v_quant, ref __m256i v_dequant,
        ref __m256i v_round, ref __m256i v_zbin, ref __m256i v_quant_shift)
    {
        __m256i v_coeff = load_coefficients_avx2(coeff_ptr);
        __m256i v_abs_coeff = _mm256_abs_epi16(v_coeff);
        __m256i v_zbin_mask = _mm256_cmpgt_epi16(v_abs_coeff, v_zbin);

        if (_mm256_movemask_epi8(v_zbin_mask) == 0)
        {
            _mm256_store_si256(qcoeff_ptr, _mm256_setzero_si256());
            _mm256_store_si256(dqcoeff_ptr, _mm256_setzero_si256());
            _mm256_store_si256(qcoeff_ptr + 8, _mm256_setzero_si256());
            _mm256_store_si256(dqcoeff_ptr + 8, _mm256_setzero_si256());
            return _mm256_setzero_si256();
        }

        // tmp = v_zbin_mask ? (int64_t)abs_coeff + log_scaled_round : 0
        __m256i v_tmp_rnd =
            _mm256_and_si256(_mm256_adds_epi16(v_abs_coeff, v_round), v_zbin_mask);
        //  tmp32 = (int)(((((tmp * quant_ptr[rc != 0]) >> 16) + tmp) *
        //                 quant_shift_ptr[rc != 0]) >>
        //                (16 - log_scale + AOM_QM_BITS));
        __m256i v_tmp32_a = _mm256_mulhi_epi16(v_tmp_rnd, v_quant);
        __m256i v_tmp32_b = _mm256_add_epi16(v_tmp32_a, v_tmp_rnd);
        __m256i v_tmp32 = _mm256_mulhi_epi16(v_tmp32_b, v_quant_shift);
        __m256i v_nz_mask = _mm256_cmpgt_epi16(v_tmp32, _mm256_setzero_si256());
        __m256i v_qcoeff = _mm256_sign_epi16(v_tmp32, v_coeff);
        __m256i v_dqcoeff = _mm256_mullo_epi16(v_qcoeff, v_dequant);
        store_coefficients_avx2(v_qcoeff, qcoeff_ptr);
        store_coefficients_avx2(v_dqcoeff, dqcoeff_ptr);
        return v_nz_mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i get_max_lane_eob(Ptr<short> iscan, __m256i v_eobmax,
                                            __m256i v_mask)
    {
        __m256i v_iscan = _mm256_loadu_si256(iscan);
        __m256i v_iscan_perm = _mm256_permute4x64_epi64(v_iscan, 0xD8);
        __m256i v_iscan_plus1 = _mm256_sub_epi16(v_iscan_perm, v_mask);
        __m256i v_nz_iscan = _mm256_and_si256(v_iscan_plus1, v_mask);
        return _mm256_max_epi16(v_eobmax, v_nz_iscan);
    }

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

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_quantize_b_avx2(Ptr<tran_low_t> coeff_ptr, int n_coeffs,
                                             Ptr<short> zbin_ptr, Ptr<short> round_ptr,
                                             Ptr<short> quant_ptr,
                                             Ptr<short> quant_shift_ptr, Ptr<tran_low_t> qcoeff_ptr,
                                             Ptr<tran_low_t> dqcoeff_ptr, Ptr<short> dequant_ptr,
                                             Ptr<ushort> eob_ptr, Ptr<short> scan,
                                             Ptr<short> iscan)
    {
        _ = scan;
        __m256i v_zbin, v_round, v_quant, v_dequant, v_quant_shift;
        __m256i v_eobmax = _mm256_setzero_si256();

        load_b_values_avx2(zbin_ptr, out v_zbin, round_ptr, out v_round, quant_ptr,
                           out v_quant, dequant_ptr, out v_dequant, quant_shift_ptr,
                           out v_quant_shift, 0);

        // Do DC and first 15 AC.
        __m256i v_nz_mask =
            quantize_b_logscale0_16(coeff_ptr, qcoeff_ptr, dqcoeff_ptr, ref v_quant,
                                    ref v_dequant, ref v_round, ref v_zbin, ref v_quant_shift);

        v_eobmax = get_max_lane_eob(iscan, v_eobmax, v_nz_mask);

        v_round = _mm256_unpackhi_epi64(v_round, v_round);
        v_quant = _mm256_unpackhi_epi64(v_quant, v_quant);
        v_dequant = _mm256_unpackhi_epi64(v_dequant, v_dequant);
        v_quant_shift = _mm256_unpackhi_epi64(v_quant_shift, v_quant_shift);
        v_zbin = _mm256_unpackhi_epi64(v_zbin, v_zbin);

        for (int count = n_coeffs - 16; count > 0; count -= 16)
        {
            coeff_ptr += 16;
            qcoeff_ptr += 16;
            dqcoeff_ptr += 16;
            iscan += 16;
            v_nz_mask =
                quantize_b_logscale0_16(coeff_ptr, qcoeff_ptr, dqcoeff_ptr, ref v_quant,
                                        ref v_dequant, ref v_round, ref v_zbin, ref v_quant_shift);

            v_eobmax = get_max_lane_eob(iscan, v_eobmax, v_nz_mask);
        }

        eob_ptr.Value = (ushort)accumulate_eob256(v_eobmax);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i quantize_b_logscale_16(
        Ptr<tran_low_t> coeff_ptr, Ptr<tran_low_t> qcoeff_ptr,
        Ptr<tran_low_t> dqcoeff_ptr, ref __m256i v_quant, ref __m256i v_dequant,
        ref __m256i v_round, ref __m256i v_zbin, ref __m256i v_quant_shift, int log_scale)
    {
        __m256i v_coeff = load_coefficients_avx2(coeff_ptr);
        __m256i v_abs_coeff = _mm256_abs_epi16(v_coeff);
        __m256i v_zbin_mask = _mm256_cmpgt_epi16(v_abs_coeff, v_zbin);

        if (_mm256_movemask_epi8(v_zbin_mask) == 0)
        {
            _mm256_store_si256(qcoeff_ptr, _mm256_setzero_si256());
            _mm256_store_si256(dqcoeff_ptr, _mm256_setzero_si256());
            _mm256_store_si256(qcoeff_ptr + 8, _mm256_setzero_si256());
            _mm256_store_si256(dqcoeff_ptr + 8, _mm256_setzero_si256());
            return _mm256_setzero_si256();
        }

        // tmp = v_zbin_mask ? (int64_t)abs_coeff + log_scaled_round : 0
        __m256i v_tmp_rnd =
            _mm256_and_si256(_mm256_adds_epi16(v_abs_coeff, v_round), v_zbin_mask);
        //  tmp32 = (int)(((((tmp * quant_ptr[rc != 0]) >> 16) + tmp) *
        //                 quant_shift_ptr[rc != 0]) >>
        //                (16 - log_scale + AOM_QM_BITS));
        __m256i v_tmp32_a = _mm256_mulhi_epi16(v_tmp_rnd, v_quant);
        __m256i v_tmp32_b = _mm256_add_epi16(v_tmp32_a, v_tmp_rnd);
        __m256i v_tmp32_hi = _mm256_slli_epi16(
            _mm256_mulhi_epi16(v_tmp32_b, v_quant_shift), log_scale);
        __m256i v_tmp32_lo = _mm256_srli_epi16(
            _mm256_mullo_epi16(v_tmp32_b, v_quant_shift), 16 - log_scale);
        __m256i v_tmp32 = _mm256_or_si256(v_tmp32_hi, v_tmp32_lo);
        __m256i v_dqcoeff_hi = _mm256_slli_epi16(
            _mm256_mulhi_epi16(v_tmp32, v_dequant), 16 - log_scale);
        __m256i v_dqcoeff_lo =
            _mm256_srli_epi16(_mm256_mullo_epi16(v_tmp32, v_dequant), log_scale);
        __m256i v_dqcoeff =
            _mm256_sign_epi16(_mm256_or_si256(v_dqcoeff_hi, v_dqcoeff_lo), v_coeff);
        __m256i v_qcoeff = _mm256_sign_epi16(v_tmp32, v_coeff);
        __m256i v_nz_mask = _mm256_cmpgt_epi16(v_tmp32, _mm256_setzero_si256());
        store_coefficients_avx2(v_qcoeff, qcoeff_ptr);
        store_coefficients_avx2(v_dqcoeff, dqcoeff_ptr);
        return v_nz_mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void quantize_b_no_qmatrix_avx2(
        Ptr<tran_low_t> coeff_ptr, int n_coeffs, Ptr<short> zbin_ptr,
        Ptr<short> round_ptr, Ptr<short> quant_ptr,
        Ptr<short> quant_shift_ptr, Ptr<tran_low_t> qcoeff_ptr,
        Ptr<tran_low_t> dqcoeff_ptr, Ptr<short> dequant_ptr, Ptr<ushort> eob_ptr,
        Ptr<short> iscan, int log_scale)
    {
        __m256i v_zbin, v_round, v_quant, v_dequant, v_quant_shift;
        __m256i v_eobmax = _mm256_setzero_si256();

        load_b_values_avx2(zbin_ptr, out v_zbin, round_ptr, out v_round, quant_ptr,
                           out v_quant, dequant_ptr, out v_dequant, quant_shift_ptr,
                           out v_quant_shift, log_scale);

        // Do DC and first 15 AC.
        __m256i v_nz_mask = quantize_b_logscale_16(
            coeff_ptr, qcoeff_ptr, dqcoeff_ptr, ref v_quant, ref v_dequant, ref v_round,
            ref v_zbin, ref v_quant_shift, log_scale);

        v_eobmax = get_max_lane_eob(iscan, v_eobmax, v_nz_mask);

        v_round = _mm256_unpackhi_epi64(v_round, v_round);
        v_quant = _mm256_unpackhi_epi64(v_quant, v_quant);
        v_dequant = _mm256_unpackhi_epi64(v_dequant, v_dequant);
        v_quant_shift = _mm256_unpackhi_epi64(v_quant_shift, v_quant_shift);
        v_zbin = _mm256_unpackhi_epi64(v_zbin, v_zbin);

        for (int count = n_coeffs - 16; count > 0; count -= 16)
        {
            coeff_ptr += 16;
            qcoeff_ptr += 16;
            dqcoeff_ptr += 16;
            iscan += 16;
            v_nz_mask = quantize_b_logscale_16(coeff_ptr, qcoeff_ptr, dqcoeff_ptr,
                                               ref v_quant, ref v_dequant, ref v_round, ref v_zbin,
                                               ref v_quant_shift, log_scale);

            v_eobmax = get_max_lane_eob(iscan, v_eobmax, v_nz_mask);
        }

        eob_ptr.Value = (ushort)accumulate_eob256(v_eobmax);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_quantize_b_32x32_avx2(Ptr<tran_low_t> coeff_ptr, int n_coeffs,
                                                   Ptr<short> zbin_ptr,
                                                   Ptr<short> round_ptr,
                                                   Ptr<short> quant_ptr,
                                                   Ptr<short> quant_shift_ptr,
                                                   Ptr<tran_low_t> qcoeff_ptr, Ptr<tran_low_t> dqcoeff_ptr,
                                                   Ptr<short> dequant_ptr, Ptr<ushort> eob_ptr,
                                                   Ptr<short> scan, Ptr<short> iscan)
    {
        _ = scan;
        quantize_b_no_qmatrix_avx2(coeff_ptr, n_coeffs, zbin_ptr, round_ptr,
                                   quant_ptr, quant_shift_ptr, qcoeff_ptr,
                                   dqcoeff_ptr, dequant_ptr, eob_ptr, iscan, 1);
    }

    // [subset] aom_quantize_b_64x64_avx2: TX64 is disabled.
}
