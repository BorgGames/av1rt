// Port of libaom aom_dsp/quantize.c

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class quantize
{
    // [subset] aom_quantize_b_adaptive_helper_c, aom_quantize_b_adaptive_c and the
    // *_32x32/*_64x64 adaptive variants are compiled out (!CONFIG_REALTIME_ONLY); the highbd
    // variants are compiled out (CONFIG_AV1_HIGHBITDEPTH=0).

    internal static void aom_quantize_b_helper_c(Ptr<tran_low_t> coeff_ptr, int n_coeffs,
                                                 Ptr<short> zbin_ptr, Ptr<short> round_ptr,
                                                 Ptr<short> quant_ptr,
                                                 Ptr<short> quant_shift_ptr,
                                                 Ptr<tran_low_t> qcoeff_ptr,
                                                 Ptr<tran_low_t> dqcoeff_ptr,
                                                 Ptr<short> dequant_ptr, Ptr<ushort> eob_ptr,
                                                 Ptr<short> scan, Ptr<short> iscan,
                                                 Ptr<qm_val_t> qm_ptr, Ptr<qm_val_t> iqm_ptr,
                                                 int log_scale)
    {
        ReadOnlySpan<int> zbins = stackalloc int[2] {
            ROUND_POWER_OF_TWO(zbin_ptr[0], log_scale),
            ROUND_POWER_OF_TWO(zbin_ptr[1], log_scale) };
        ReadOnlySpan<int> nzbins = stackalloc int[2] { zbins[0] * -1, zbins[1] * -1 };
        int i, non_zero_count = n_coeffs, eob = -1;
        _ = iscan;

        memset(qcoeff_ptr, 0, n_coeffs);
        memset(dqcoeff_ptr, 0, n_coeffs);

        // Pre-scan pass
        for (i = n_coeffs - 1; i >= 0; i--)
        {
            int rc = scan[i];
            qm_val_t wt = !qm_ptr.IsNull ? qm_ptr[rc] : (qm_val_t)(1 << AOM_QM_BITS);
            int coeff = coeff_ptr[rc] * wt;

            if (coeff < (zbins[B(rc != 0)] * (1 << AOM_QM_BITS)) &&
                coeff > (nzbins[B(rc != 0)] * (1 << AOM_QM_BITS)))
                non_zero_count--;
            else
                break;
        }

        // Quantization pass: All coefficients with index >= zero_flag are
        // skippable. Note: zero_flag can be zero.
        for (i = 0; i < non_zero_count; i++)
        {
            int rc = scan[i];
            int coeff = coeff_ptr[rc];
            int coeff_sign = AOMSIGN(coeff);
            int abs_coeff = (coeff ^ coeff_sign) - coeff_sign;
            int tmp32;

            qm_val_t wt = !qm_ptr.IsNull ? qm_ptr[rc] : (qm_val_t)(1 << AOM_QM_BITS);
            if (abs_coeff * wt >= (zbins[B(rc != 0)] << AOM_QM_BITS))
            {
                long tmp =
                    clamp(abs_coeff + ROUND_POWER_OF_TWO(round_ptr[B(rc != 0)], log_scale),
                          short.MinValue, short.MaxValue);
                tmp *= wt;
                tmp32 = (int)(((((tmp * quant_ptr[B(rc != 0)]) >> 16) + tmp) *
                               quant_shift_ptr[B(rc != 0)]) >>
                              (16 - log_scale + AOM_QM_BITS));  // quantization
                qcoeff_ptr[rc] = (tmp32 ^ coeff_sign) - coeff_sign;
                int iwt = !iqm_ptr.IsNull ? iqm_ptr[rc] : (1 << AOM_QM_BITS);
                int dequant =
                    (dequant_ptr[B(rc != 0)] * iwt + (1 << (AOM_QM_BITS - 1))) >>
                    AOM_QM_BITS;
                tran_low_t abs_dqcoeff = (tmp32 * dequant) >> log_scale;
                dqcoeff_ptr[rc] = (tran_low_t)((abs_dqcoeff ^ coeff_sign) - coeff_sign);

                if (tmp32 != 0) eob = i;
            }
        }
        eob_ptr.Value = (ushort)(eob + 1);
    }

    // av1_rtcd.h dispatched name.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_quantize_b_helper(Ptr<tran_low_t> coeff_ptr, int n_coeffs,
                                               Ptr<short> zbin_ptr, Ptr<short> round_ptr,
                                               Ptr<short> quant_ptr, Ptr<short> quant_shift_ptr,
                                               Ptr<tran_low_t> qcoeff_ptr,
                                               Ptr<tran_low_t> dqcoeff_ptr,
                                               Ptr<short> dequant_ptr, Ptr<ushort> eob_ptr,
                                               Ptr<short> scan, Ptr<short> iscan,
                                               Ptr<qm_val_t> qm_ptr, Ptr<qm_val_t> iqm_ptr,
                                               int log_scale) =>
        aom_quantize_b_helper_c(coeff_ptr, n_coeffs, zbin_ptr, round_ptr, quant_ptr,
                                quant_shift_ptr, qcoeff_ptr, dqcoeff_ptr, dequant_ptr, eob_ptr,
                                scan, iscan, qm_ptr, iqm_ptr, log_scale);

    // [subset] aom_highbd_quantize_b_helper_c: high bitdepth.

    internal static void aom_quantize_b_c(Ptr<tran_low_t> coeff_ptr, int n_coeffs,
                                          Ptr<short> zbin_ptr, Ptr<short> round_ptr,
                                          Ptr<short> quant_ptr, Ptr<short> quant_shift_ptr,
                                          Ptr<tran_low_t> qcoeff_ptr, Ptr<tran_low_t> dqcoeff_ptr,
                                          Ptr<short> dequant_ptr, Ptr<ushort> eob_ptr,
                                          Ptr<short> scan, Ptr<short> iscan)
    {
        aom_quantize_b_helper_c(coeff_ptr, n_coeffs, zbin_ptr, round_ptr, quant_ptr,
                                quant_shift_ptr, qcoeff_ptr, dqcoeff_ptr, dequant_ptr,
                                eob_ptr, scan, iscan, Ptr<qm_val_t>.Null, Ptr<qm_val_t>.Null, 0);
    }

    // aom_dsp_rtcd.h dispatched name.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_quantize_b(Ptr<tran_low_t> coeff_ptr, int n_coeffs,
                                        Ptr<short> zbin_ptr, Ptr<short> round_ptr,
                                        Ptr<short> quant_ptr, Ptr<short> quant_shift_ptr,
                                        Ptr<tran_low_t> qcoeff_ptr, Ptr<tran_low_t> dqcoeff_ptr,
                                        Ptr<short> dequant_ptr, Ptr<ushort> eob_ptr,
                                        Ptr<short> scan, Ptr<short> iscan)
    {
        if (Simd.UseAvx2)
            aom_quantize_b_avx2(coeff_ptr, n_coeffs, zbin_ptr, round_ptr, quant_ptr,
                                quant_shift_ptr, qcoeff_ptr, dqcoeff_ptr, dequant_ptr, eob_ptr,
                                scan, iscan);
        else
            aom_quantize_b_c(coeff_ptr, n_coeffs, zbin_ptr, round_ptr, quant_ptr,
                             quant_shift_ptr, qcoeff_ptr, dqcoeff_ptr, dequant_ptr, eob_ptr,
                             scan, iscan);
    }

    internal static void aom_quantize_b_32x32_c(Ptr<tran_low_t> coeff_ptr, int n_coeffs,
                                                Ptr<short> zbin_ptr, Ptr<short> round_ptr,
                                                Ptr<short> quant_ptr,
                                                Ptr<short> quant_shift_ptr,
                                                Ptr<tran_low_t> qcoeff_ptr,
                                                Ptr<tran_low_t> dqcoeff_ptr,
                                                Ptr<short> dequant_ptr, Ptr<ushort> eob_ptr,
                                                Ptr<short> scan, Ptr<short> iscan)
    {
        aom_quantize_b_helper_c(coeff_ptr, n_coeffs, zbin_ptr, round_ptr, quant_ptr,
                                quant_shift_ptr, qcoeff_ptr, dqcoeff_ptr, dequant_ptr,
                                eob_ptr, scan, iscan, Ptr<qm_val_t>.Null, Ptr<qm_val_t>.Null, 1);
    }

    // aom_dsp_rtcd.h dispatched name.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_quantize_b_32x32(Ptr<tran_low_t> coeff_ptr, int n_coeffs,
                                              Ptr<short> zbin_ptr, Ptr<short> round_ptr,
                                              Ptr<short> quant_ptr, Ptr<short> quant_shift_ptr,
                                              Ptr<tran_low_t> qcoeff_ptr,
                                              Ptr<tran_low_t> dqcoeff_ptr,
                                              Ptr<short> dequant_ptr, Ptr<ushort> eob_ptr,
                                              Ptr<short> scan, Ptr<short> iscan)
    {
        if (Simd.UseAvx2)
            aom_quantize_b_32x32_avx2(coeff_ptr, n_coeffs, zbin_ptr, round_ptr, quant_ptr,
                                      quant_shift_ptr, qcoeff_ptr, dqcoeff_ptr, dequant_ptr,
                                      eob_ptr, scan, iscan);
        else
            aom_quantize_b_32x32_c(coeff_ptr, n_coeffs, zbin_ptr, round_ptr, quant_ptr,
                                   quant_shift_ptr, qcoeff_ptr, dqcoeff_ptr, dequant_ptr,
                                   eob_ptr, scan, iscan);
    }

    // [subset] aom_quantize_b_64x64_c: TX64 is disabled (log_scale 2 never happens).
    // [subset] aom_highbd_quantize_b_c / _32x32_c / _64x64_c: high bitdepth.
}
