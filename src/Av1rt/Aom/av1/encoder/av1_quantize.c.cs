// Port of libaom av1/encoder/av1_quantize.c

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class av1_quantize
{
    // [port] Never executed (x->seg_skip_block needs SEG_LVL_SKIP, which the subset never
    // enables) but called from av1_quant() (encodemb.h) on a data-dependent branch; trivial.
    internal static void av1_quantize_skip(int n_coeffs, Ptr<tran_low_t> qcoeff_ptr,
                                           Ptr<tran_low_t> dqcoeff_ptr, Ptr<ushort> eob_ptr)
    {
        memset(qcoeff_ptr, 0, n_coeffs);
        memset(dqcoeff_ptr, 0, n_coeffs);
        eob_ptr.Value = 0;
    }

    // [subset] av1_quantize_fp_no_qmatrix, quantize_fp_helper_c, av1_quantize_fp_c,
    // av1_quantize_fp_32x32_c, av1_quantize_fp_64x64_c, av1_quantize_fp_qm_c,
    // av1_quantize_b_qm_c and av1_quantize_fp_facade: the encode path uses AV1_XFORM_QUANT_B
    // (trellis off, USE_B_QUANT_NO_TRELLIS) without quantization matrices, and the nonrd RD
    // estimate uses av1_quantize_lp; none of these ever ran.
    // [subset] highbd_quantize_fp_helper_c: high bitdepth.

    internal static void av1_quantize_lp_c(Ptr<short> coeff_ptr, int n_coeffs,
                                           Ptr<short> round_ptr, Ptr<short> quant_ptr,
                                           Ptr<short> qcoeff_ptr, Ptr<short> dqcoeff_ptr,
                                           Ptr<short> dequant_ptr, Ptr<ushort> eob_ptr,
                                           Ptr<short> scan, Ptr<short> iscan)
    {
        _ = iscan;
        int eob = -1;

        memset(qcoeff_ptr, (short)0, n_coeffs);
        memset(dqcoeff_ptr, (short)0, n_coeffs);

        // Quantization pass: All coefficients with index >= zero_flag are
        // skippable. Note: zero_flag can be zero.
        for (int i = 0; i < n_coeffs; i++)
        {
            int rc = scan[i];
            int coeff = coeff_ptr[rc];
            int coeff_sign = AOMSIGN(coeff);
            int abs_coeff = (coeff ^ coeff_sign) - coeff_sign;

            int tmp = clamp(abs_coeff + round_ptr[B(rc != 0)], short.MinValue, short.MaxValue);
            tmp = (tmp * quant_ptr[B(rc != 0)]) >> 16;

            qcoeff_ptr[rc] = (short)((tmp ^ coeff_sign) - coeff_sign);
            dqcoeff_ptr[rc] = (short)(qcoeff_ptr[rc] * dequant_ptr[B(rc != 0)]);

            if (tmp != 0) eob = i;
        }
        eob_ptr.Value = (ushort)(eob + 1);
    }

    // av1_rtcd.h dispatched name.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_quantize_lp(Ptr<short> coeff_ptr, int n_coeffs,
                                         Ptr<short> round_ptr, Ptr<short> quant_ptr,
                                         Ptr<short> qcoeff_ptr, Ptr<short> dqcoeff_ptr,
                                         Ptr<short> dequant_ptr, Ptr<ushort> eob_ptr,
                                         Ptr<short> scan, Ptr<short> iscan)
    {
        if (Simd.UseAvx2)
            av1_quantize_lp_avx2(coeff_ptr, n_coeffs, round_ptr, quant_ptr, qcoeff_ptr,
                                 dqcoeff_ptr, dequant_ptr, eob_ptr, scan, iscan);
        else
            av1_quantize_lp_c(coeff_ptr, n_coeffs, round_ptr, quant_ptr, qcoeff_ptr,
                              dqcoeff_ptr, dequant_ptr, eob_ptr, scan, iscan);
    }

    internal static void av1_quantize_b_facade(Ptr<tran_low_t> coeff_ptr, int n_coeffs,
                                               macroblock_plane p, Ptr<tran_low_t> qcoeff_ptr,
                                               Ptr<tran_low_t> dqcoeff_ptr, Ptr<ushort> eob_ptr,
                                               SCAN_ORDER sc, in QUANT_PARAM qparam)
    {
        Ptr<qm_val_t> qm_ptr = qparam.qmatrix;
        Ptr<qm_val_t> iqm_ptr = qparam.iqmatrix;
        // [subset] qparam->use_quant_b_adapt block is compiled out (!CONFIG_REALTIME_ONLY).

        if (!qm_ptr.IsNull && !iqm_ptr.IsNull)
        {
            // [subset] av1_quantize_b_qm: quantization matrices are disabled in the subset
            // (the flat level NUM_QM_LEVELS - 1 has NULL matrices).
            throw new NotSupportedException("av1_quantize_b_qm (quantization matrices)");
        }
        else
        {
            switch (qparam.log_scale)
            {
                case 0:
                    aom_quantize_b(coeff_ptr, n_coeffs, p.zbin_QTX, p.round_QTX,
                                   p.quant_QTX, p.quant_shift_QTX, qcoeff_ptr,
                                   dqcoeff_ptr, p.dequant_QTX, eob_ptr, sc.scan,
                                   sc.iscan);
                    break;
                case 1:
                    aom_quantize_b_32x32(coeff_ptr, n_coeffs, p.zbin_QTX, p.round_QTX,
                                         p.quant_QTX, p.quant_shift_QTX, qcoeff_ptr,
                                         dqcoeff_ptr, p.dequant_QTX, eob_ptr, sc.scan,
                                         sc.iscan);
                    break;
                case 2:
                    // [subset] aom_quantize_b_64x64: TX64 is disabled.
                    throw new NotSupportedException("aom_quantize_b_64x64 (TX64)");
                default: Debug.Assert(false); break;
            }
        }
    }

    // [subset] quantize_dc and av1_quantize_dc_facade: AV1_XFORM_QUANT_DC is never used.
    // [subset] av1_highbd_quantize_* facades and helpers: high bitdepth.

    private static void invert_quant(ref short quant, ref short shift, int d)
    {
        uint t;
        int l, m;
        t = (uint)d;
        l = get_msb(t);
        m = 1 + (1 << (16 + l)) / d;
        quant = (short)(m - (1 << 16));
        shift = (short)(1 << (16 - l));
    }

    private static int get_qzbin_factor(int q, aom_bit_depth_t bit_depth)
    {
        int quant = av1_dc_quant_QTX(q, 0, bit_depth);
        switch (bit_depth)
        {
            case AOM_BITS_8: return q == 0 ? 64 : (quant < 148 ? 84 : 80);
            case AOM_BITS_10: return q == 0 ? 64 : (quant < 592 ? 84 : 80);
            case AOM_BITS_12: return q == 0 ? 64 : (quant < 2368 ? 84 : 80);
            default:
                Debug.Assert(false, "bit_depth should be AOM_BITS_8, AOM_BITS_10 or AOM_BITS_12");
                return -1;
        }
    }

    internal static void av1_build_quantizer(aom_bit_depth_t bit_depth, int y_dc_delta_q,
                                             int u_dc_delta_q, int u_ac_delta_q,
                                             int v_dc_delta_q, int v_ac_delta_q,
                                             QUANTS quants, Dequants deq, int sharpness)
    {
        int i, q, quant_QTX;
        int sharpness_adjustment = 16 * (7 - sharpness) / 7;

        for (q = 0; q < QINDEX_RANGE; q++)
        {
            int qzbin_factor = get_qzbin_factor(q, bit_depth);
            int qrounding_factor = q == 0 ? 64 : 48;

            for (i = 0; i < 2; ++i)
            {
                int qrounding_factor_fp = 64;

                if (sharpness != 0 && q != 0)
                {
                    qrounding_factor = 64 - sharpness_adjustment;
                    qrounding_factor_fp = 64 - sharpness_adjustment;
                }

                // y quantizer with TX scale
                quant_QTX = i == 0 ? av1_dc_quant_QTX(q, y_dc_delta_q, bit_depth)
                                   : av1_ac_quant_QTX(q, 0, bit_depth);
                invert_quant(ref quants.y_quant[q][i], ref quants.y_quant_shift[q][i],
                             quant_QTX);
                quants.y_quant_fp[q][i] = (short)((1 << 16) / quant_QTX);
                quants.y_round_fp[q][i] = (short)((qrounding_factor_fp * quant_QTX) >> 7);
                quants.y_zbin[q][i] = (short)ROUND_POWER_OF_TWO(qzbin_factor * quant_QTX, 7);
                quants.y_round[q][i] = (short)((qrounding_factor * quant_QTX) >> 7);
                deq.y_dequant_QTX[q][i] = (short)quant_QTX;

                // u quantizer with TX scale
                quant_QTX = i == 0 ? av1_dc_quant_QTX(q, u_dc_delta_q, bit_depth)
                                   : av1_ac_quant_QTX(q, u_ac_delta_q, bit_depth);
                invert_quant(ref quants.u_quant[q][i], ref quants.u_quant_shift[q][i],
                             quant_QTX);
                quants.u_quant_fp[q][i] = (short)((1 << 16) / quant_QTX);
                quants.u_round_fp[q][i] = (short)((qrounding_factor_fp * quant_QTX) >> 7);
                quants.u_zbin[q][i] = (short)ROUND_POWER_OF_TWO(qzbin_factor * quant_QTX, 7);
                quants.u_round[q][i] = (short)((qrounding_factor * quant_QTX) >> 7);
                deq.u_dequant_QTX[q][i] = (short)quant_QTX;

                // v quantizer with TX scale
                quant_QTX = i == 0 ? av1_dc_quant_QTX(q, v_dc_delta_q, bit_depth)
                                   : av1_ac_quant_QTX(q, v_ac_delta_q, bit_depth);
                invert_quant(ref quants.v_quant[q][i], ref quants.v_quant_shift[q][i],
                             quant_QTX);
                quants.v_quant_fp[q][i] = (short)((1 << 16) / quant_QTX);
                quants.v_round_fp[q][i] = (short)((qrounding_factor_fp * quant_QTX) >> 7);
                quants.v_zbin[q][i] = (short)ROUND_POWER_OF_TWO(qzbin_factor * quant_QTX, 7);
                quants.v_round[q][i] = (short)((qrounding_factor * quant_QTX) >> 7);
                deq.v_dequant_QTX[q][i] = (short)quant_QTX;
            }

            for (i = 2; i < 8; i++)
            {  // 8: SIMD width
                quants.y_quant[q][i] = quants.y_quant[q][1];
                quants.y_quant_fp[q][i] = quants.y_quant_fp[q][1];
                quants.y_round_fp[q][i] = quants.y_round_fp[q][1];
                quants.y_quant_shift[q][i] = quants.y_quant_shift[q][1];
                quants.y_zbin[q][i] = quants.y_zbin[q][1];
                quants.y_round[q][i] = quants.y_round[q][1];
                deq.y_dequant_QTX[q][i] = deq.y_dequant_QTX[q][1];

                quants.u_quant[q][i] = quants.u_quant[q][1];
                quants.u_quant_fp[q][i] = quants.u_quant_fp[q][1];
                quants.u_round_fp[q][i] = quants.u_round_fp[q][1];
                quants.u_quant_shift[q][i] = quants.u_quant_shift[q][1];
                quants.u_zbin[q][i] = quants.u_zbin[q][1];
                quants.u_round[q][i] = quants.u_round[q][1];
                deq.u_dequant_QTX[q][i] = deq.u_dequant_QTX[q][1];

                quants.v_quant[q][i] = quants.v_quant[q][1];
                quants.v_quant_fp[q][i] = quants.v_quant_fp[q][1];
                quants.v_round_fp[q][i] = quants.v_round_fp[q][1];
                quants.v_quant_shift[q][i] = quants.v_quant_shift[q][1];
                quants.v_zbin[q][i] = quants.v_zbin[q][1];
                quants.v_round[q][i] = quants.v_round[q][1];
                deq.v_dequant_QTX[q][i] = deq.v_dequant_QTX[q][1];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool deltaq_params_have_changed(
        in DeltaQuantParams prev_deltaq_params,
        CommonQuantParams quant_params)
    {
        return (prev_deltaq_params.y_dc_delta_q != quant_params.y_dc_delta_q ||
                prev_deltaq_params.u_dc_delta_q != quant_params.u_dc_delta_q ||
                prev_deltaq_params.v_dc_delta_q != quant_params.v_dc_delta_q ||
                prev_deltaq_params.u_ac_delta_q != quant_params.u_ac_delta_q ||
                prev_deltaq_params.v_ac_delta_q != quant_params.v_ac_delta_q ||
                prev_deltaq_params.sharpness != quant_params.sharpness);
    }

    internal static void av1_init_quantizer(EncQuantDequantParams enc_quant_dequant_params,
                                            CommonQuantParams quant_params,
                                            aom_bit_depth_t bit_depth, int sharpness)
    {
        ref DeltaQuantParams prev_deltaq_params =
            ref enc_quant_dequant_params.prev_deltaq_params;
        quant_params.sharpness = sharpness;

        // Re-initialize the quantizer only if any of the dc/ac deltaq parameters
        // change.
        if (!deltaq_params_have_changed(in prev_deltaq_params, quant_params)) return;
        QUANTS quants = enc_quant_dequant_params.quants;
        Dequants dequants = enc_quant_dequant_params.dequants;
        av1_build_quantizer(bit_depth, quant_params.y_dc_delta_q,
                            quant_params.u_dc_delta_q, quant_params.u_ac_delta_q,
                            quant_params.v_dc_delta_q, quant_params.v_ac_delta_q,
                            quants, dequants, sharpness);

        // Record the state of deltaq parameters.
        prev_deltaq_params.y_dc_delta_q = quant_params.y_dc_delta_q;
        prev_deltaq_params.u_dc_delta_q = quant_params.u_dc_delta_q;
        prev_deltaq_params.v_dc_delta_q = quant_params.v_dc_delta_q;
        prev_deltaq_params.u_ac_delta_q = quant_params.u_ac_delta_q;
        prev_deltaq_params.v_ac_delta_q = quant_params.v_ac_delta_q;
        prev_deltaq_params.sharpness = sharpness;
    }

    /*!\brief Update quantize parameters in MACROBLOCK
     *
     * \param[in]  enc_quant_dequant_params This parameter cached the quantize and
     *                                      dequantize parameters for all q
     *                                      indices.
     * \param[in]  qindex                   Quantize index used for the current
     *                                      superblock.
     * \param[out] x                        A superblock data structure for
     *                                      encoder.
     */
    private static void set_q_index(EncQuantDequantParams enc_quant_dequant_params,
                                    int qindex, MACROBLOCK x)
    {
        QUANTS quants = enc_quant_dequant_params.quants;
        Dequants dequants = enc_quant_dequant_params.dequants;
        x.qindex = qindex;
        x.seg_skip_block =
            0;  // TODO(angiebird): Find a proper place to init this variable.

        // Y
        x.plane[0].quant_QTX = quants.y_quant[qindex];
        x.plane[0].quant_fp_QTX = quants.y_quant_fp[qindex];
        x.plane[0].round_fp_QTX = quants.y_round_fp[qindex];
        x.plane[0].quant_shift_QTX = quants.y_quant_shift[qindex];
        x.plane[0].zbin_QTX = quants.y_zbin[qindex];
        x.plane[0].round_QTX = quants.y_round[qindex];
        x.plane[0].dequant_QTX = dequants.y_dequant_QTX[qindex];

        // U
        x.plane[1].quant_QTX = quants.u_quant[qindex];
        x.plane[1].quant_fp_QTX = quants.u_quant_fp[qindex];
        x.plane[1].round_fp_QTX = quants.u_round_fp[qindex];
        x.plane[1].quant_shift_QTX = quants.u_quant_shift[qindex];
        x.plane[1].zbin_QTX = quants.u_zbin[qindex];
        x.plane[1].round_QTX = quants.u_round[qindex];
        x.plane[1].dequant_QTX = dequants.u_dequant_QTX[qindex];

        // V
        x.plane[2].quant_QTX = quants.v_quant[qindex];
        x.plane[2].quant_fp_QTX = quants.v_quant_fp[qindex];
        x.plane[2].round_fp_QTX = quants.v_round_fp[qindex];
        x.plane[2].quant_shift_QTX = quants.v_quant_shift[qindex];
        x.plane[2].zbin_QTX = quants.v_zbin[qindex];
        x.plane[2].round_QTX = quants.v_round[qindex];
        x.plane[2].dequant_QTX = dequants.v_dequant_QTX[qindex];
    }

    /*!\brief Update quantize matrix in MACROBLOCKD based on segment id
     *
     * \param[in]  quant_params  Quantize parameters used by encoder and decoder
     * \param[in]  segment_id    Segment id.
     * \param[out] xd            A superblock data structure used by encoder and
     * decoder.
     */
    private static void set_qmatrix(CommonQuantParams quant_params, int segment_id,
                                    MACROBLOCKD xd)
    {
        int use_qmatrix = B(av1_use_qmatrix(quant_params, xd, segment_id));
        int qmlevel_y =
            use_qmatrix != 0 ? quant_params.qmatrix_level_y : NUM_QM_LEVELS - 1;
        int qmlevel_u =
            use_qmatrix != 0 ? quant_params.qmatrix_level_u : NUM_QM_LEVELS - 1;
        int qmlevel_v =
            use_qmatrix != 0 ? quant_params.qmatrix_level_v : NUM_QM_LEVELS - 1;
        ReadOnlySpan<int> qmlevel_ls = stackalloc int[MAX_MB_PLANE] { qmlevel_y, qmlevel_u, qmlevel_v };
        for (int i = 0; i < MAX_MB_PLANE; ++i)
        {
            int qmlevel = qmlevel_ls[i];
            // [port] The rows only depend on qmlevel (av1_qm_init fills them deterministically),
            // so skip the copy when this segment already holds them.
            if (xd.plane[i].seg_qm_level[segment_id] == qmlevel) continue;
            xd.plane[i].seg_qm_level[segment_id] = qmlevel;
            copy_array(xd.plane[i].seg_qmatrix[segment_id],
                       quant_params.gqmatrix[qmlevel][i]);
            copy_array(xd.plane[i].seg_iqmatrix[segment_id],
                       quant_params.giqmatrix[qmlevel][i]);
        }
    }

    internal static void av1_init_plane_quantizers(AV1_COMP cpi, MACROBLOCK x,
                                                   int segment_id, int do_update)
    {
        AV1_COMMON cm = cpi.common;
        CommonQuantParams quant_params = cm.quant_params;
        GF_GROUP gf_group = cpi.ppi.gf_group;
        int boost_index = AOMMIN(15, (cpi.ppi.p_rc.gfu_boost / 100));
        int layer_depth = AOMMIN(gf_group.layer_depth[cpi.gf_frame_index], 6);
        FRAME_TYPE frame_type = cm.current_frame.frame_type;
        int qindex_rd;

        int current_qindex =
            clamp(cm.delta_q_info.delta_q_present_flag != 0
                      ? quant_params.base_qindex + x.delta_qindex
                      : quant_params.base_qindex,
                  0, QINDEX_RANGE - 1);
        int qindex = av1_get_qindex(cm.seg, segment_id, current_qindex);

        if (cpi.oxcf.sb_qp_sweep != 0)
        {
            // [subset] sb_qp_sweep is a test-only option (0 in the subset).
            throw new NotSupportedException("sb_qp_sweep");
        }
        else
        {
            if (cpi.cb_delta_rdmult_enabled != 0)
            {
                // [subset] cb_delta_rdmult_enabled is only set for deltaq modes (deltaq_mode 0
                // in the subset).
                throw new NotSupportedException("cb_delta_rdmult_enabled");
            }
            else
            {
                qindex_rd = qindex;
            }
        }

        int qindex_rdmult = qindex_rd + quant_params.y_dc_delta_q;
        int rdmult = av1_compute_rd_mult(
            qindex_rdmult, cm.seq_params.bit_depth,
            cpi.ppi.gf_group.update_type[cpi.gf_frame_index], layer_depth,
            boost_index, frame_type, cpi.oxcf.q_cfg.use_fixed_qp_offsets,
            is_stat_consumption_stage(cpi), cpi.oxcf.tune_cfg.tuning,
            cpi.oxcf.mode);

        int qindex_change = B(x.qindex != qindex);
        if (qindex_change != 0 || do_update != 0)
        {
            set_q_index(cpi.enc_quant_dequant_params, qindex, x);
        }

        MACROBLOCKD xd = x.e_mbd;
        if ((segment_id != x.prev_segment_id) ||
            av1_use_qmatrix(quant_params, xd, segment_id))
        {
            set_qmatrix(quant_params, segment_id, xd);
        }

        x.seg_skip_block = segfeature_active(cm.seg, (byte)segment_id, SEG_LVL_SKIP);

        av1_set_error_per_bit(ref x.errorperbit, rdmult);
        av1_set_sad_per_bit(cpi, ref x.sadperbit, qindex_rd);

        x.prev_segment_id = segment_id;
    }

    internal static void av1_frame_init_quantizer(AV1_COMP cpi)
    {
        MACROBLOCK x = cpi.td.mb;
        MACROBLOCKD xd = x.e_mbd;
        x.prev_segment_id = -1;
        av1_init_plane_quantizers(cpi, x, xd.mi[0].segment_id, 1);
    }

    // [subset] adjust_hdr_cb_deltaq / adjust_hdr_cr_deltaq: enable_hdr_deltaq is 0.

    internal static void av1_set_quantizer(AV1_COMMON cm, int min_qmlevel, int max_qmlevel,
                                           int q, int enable_chroma_deltaq,
                                           int enable_hdr_deltaq, bool is_allintra,
                                           aom_tune_metric tuning)
    {
        // quantizer has to be reinitialized with av1_init_quantizer() if any
        // delta_q changes.
        CommonQuantParams quant_params = cm.quant_params;
        quant_params.base_qindex = AOMMAX(cm.delta_q_info.delta_q_present_flag, q);
        quant_params.y_dc_delta_q = 0;

        // Disable deltaq in lossless mode.
        if (enable_chroma_deltaq != 0 && q != 0)
        {
            // [subset] enable_chroma_deltaq is 0 in the subset (both the tune IQ /
            // SSIMULACRA2 chroma formulas and the fixed +2 delta are dropped).
            throw new NotSupportedException("enable_chroma_deltaq");
        }
        else
        {
            quant_params.u_dc_delta_q = 0;
            quant_params.u_ac_delta_q = 0;
            quant_params.v_dc_delta_q = 0;
            quant_params.v_ac_delta_q = 0;
        }

        // following section 8.3.2 in T-REC-H.Sup15 document
        // to apply to AV1 qindex in the range of [0, 255]
        if (enable_hdr_deltaq != 0 && q != 0)
        {
            // [subset] enable_hdr_deltaq is 0 in the subset.
            throw new NotSupportedException("enable_hdr_deltaq");
        }

        // Select the best luma and chroma QM formulas based on encoding mode and
        // tuning
        // [subset] tune IQ / SSIMULACRA2 (aom_get_qmlevel_luma_ssimulacra2,
        // aom_get_qmlevel_444_chroma, aom_get_qmlevel_allintra) and all-intra mode are not
        // in the subset: get_luma_qmlevel = get_chroma_qmlevel = aom_get_qmlevel.
        if (tuning == AOM_TUNE_IQ || tuning == AOM_TUNE_SSIMULACRA2 || is_allintra)
        {
            throw new NotSupportedException("tune IQ/SSIMULACRA2 or all-intra QM levels");
        }

        quant_params.qmatrix_level_y =
            aom_get_qmlevel(quant_params.base_qindex, min_qmlevel, max_qmlevel);
        quant_params.qmatrix_level_u =
            aom_get_qmlevel(quant_params.base_qindex + quant_params.u_ac_delta_q,
                            min_qmlevel, max_qmlevel);

        if (cm.seq_params.separate_uv_delta_q != 0)
        {
            quant_params.qmatrix_level_v = aom_get_qmlevel(
                quant_params.base_qindex + quant_params.v_ac_delta_q, min_qmlevel,
                max_qmlevel);
        }
        else
        {
            quant_params.qmatrix_level_v = quant_params.qmatrix_level_u;
        }
    }

    // Table that converts 0-63 Q-range values passed in outside to the Qindex
    // range used internally.
    private static readonly int[] quantizer_to_qindex = {
        0,   4,   8,   12,  16,  20,  24,  28,  32,  36,  40,  44,  48,
        52,  56,  60,  64,  68,  72,  76,  80,  84,  88,  92,  96,  100,
        104, 108, 112, 116, 120, 124, 128, 132, 136, 140, 144, 148, 152,
        156, 160, 164, 168, 172, 176, 180, 184, 188, 192, 196, 200, 204,
        208, 212, 216, 220, 224, 228, 232, 236, 240, 244, 249, 255,
    };

    internal static int av1_quantizer_to_qindex(int quantizer)
    {
        return quantizer_to_qindex[quantizer];
    }

    internal static int av1_qindex_to_quantizer(int qindex)
    {
        int quantizer;

        for (quantizer = 0; quantizer < 64; ++quantizer)
            if (quantizer_to_qindex[quantizer] >= qindex) return quantizer;

        return 63;
    }
}
