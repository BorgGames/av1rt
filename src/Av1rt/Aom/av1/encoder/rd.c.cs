// Port of libaom av1/encoder/rd.c
// [subset] Not ported (never executed in the realtime subset):
//   av1_fill_lr_rates, av1_get_adaptive_rdmult (!CONFIG_REALTIME_ONLY, compiled out);
//   av1_get_deltaq_offset, av1_adjust_q_from_delta_q_res (deltaq modes are off);
//   av1_fill_dv_costs (intra block copy is off, IntraBCMVCosts is not ported);
//   interp_cubic, av1_interp_cubic_rate_dist_c, sse_norm_curvfit_model_cat_lookup,
//   av1_model_rd_curvfit (curvfit model rd, rd mode search only);
//   av1_get_switchable_rate (rd mode search only);
//   update_thr_fact, av1_update_rd_thresh_fact (rd mode search only).

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class rd
{
    private const double RD_THRESH_POW = 1.25;

    // The baseline rd thresholds for breaking out of the rd loop for
    // certain modes are assumed to be based on 8x8 blocks.
    // This table is used to correct for block size.
    // The factors here are << 2 (2 = x0.5, 32 = x8 etc).
    private static readonly byte[] rd_thresh_block_size_factor = {
        2, 3, 3, 4, 6, 6, 8, 12, 12, 16, 24, 24, 32, 48, 48, 64, 4, 4, 8, 8, 16, 16
    };

    private static readonly int[][] use_intra_ext_tx_for_txsize = {
        new[] { 1, 1, 1, 1 },  // unused
        new[] { 1, 1, 0, 0 },
        new[] { 0, 0, 1, 0 },
    };

    private static readonly int[][] use_inter_ext_tx_for_txsize = {
        new[] { 1, 1, 1, 1 },  // unused
        new[] { 1, 1, 0, 0 },
        new[] { 0, 0, 1, 0 },
        new[] { 0, 1, 1, 1 },
    };

    // C: [2][AOMMAX(EXT_TX_SETS_INTRA, EXT_TX_SETS_INTER)]; the intra row's 4th entry is 0.
    private static readonly int[][] av1_ext_tx_set_idx_to_type = {
        new[] {
            // Intra
            EXT_TX_SET_DCTONLY,
            EXT_TX_SET_DTT4_IDTX_1DDCT,
            EXT_TX_SET_DTT4_IDTX,
            0,
        },
        new[] {
            // Inter
            EXT_TX_SET_DCTONLY,
            EXT_TX_SET_ALL16,
            EXT_TX_SET_DTT9_IDTX_1DDCT,
            EXT_TX_SET_DCT_IDTX,
        },
    };

    internal static void av1_fill_mode_rates(AV1_COMMON cm, ModeCosts mode_costs,
                                             FRAME_CONTEXT fc)
    {
        int i, j;

        for (i = 0; i < PARTITION_CONTEXTS; ++i)
            av1_cost_tokens_from_cdf(mode_costs.partition_cost[i],
                                     fc.partition_cdf[i], Ptr<int>.Null);

        if (cm.current_frame.skip_mode_info.skip_mode_flag != 0)
        {
            for (i = 0; i < SKIP_MODE_CONTEXTS; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.skip_mode_cost[i],
                                         fc.skip_mode_cdfs[i], Ptr<int>.Null);
            }
        }
        else
        {
            av1_zero(mode_costs.skip_mode_cost);
        }

        for (i = 0; i < SKIP_CONTEXTS; ++i)
        {
            av1_cost_tokens_from_cdf(mode_costs.skip_txfm_cost[i],
                                     fc.skip_txfm_cdfs[i], Ptr<int>.Null);
        }

        for (i = 0; i < KF_MODE_CONTEXTS; ++i)
            for (j = 0; j < KF_MODE_CONTEXTS; ++j)
                av1_cost_tokens_from_cdf(mode_costs.y_mode_costs[i][j],
                                         fc.kf_y_cdf[i][j], Ptr<int>.Null);

        for (i = 0; i < BLOCK_SIZE_GROUPS; ++i)
            av1_cost_tokens_from_cdf(mode_costs.mbmode_cost[i], fc.y_mode_cdf[i],
                                     Ptr<int>.Null);
        for (i = 0; i < CFL_ALLOWED_TYPES; ++i)
            for (j = 0; j < INTRA_MODES; ++j)
                av1_cost_tokens_from_cdf(mode_costs.intra_uv_mode_cost[i][j],
                                         fc.uv_mode_cdf[i][j], Ptr<int>.Null);

        av1_cost_tokens_from_cdf(mode_costs.filter_intra_mode_cost,
                                 fc.filter_intra_mode_cdf, Ptr<int>.Null);
        for (i = 0; i < BLOCK_SIZES_ALL; ++i)
        {
            if (av1_filter_intra_allowed_bsize(cm, i) != 0)
                av1_cost_tokens_from_cdf(mode_costs.filter_intra_cost[i],
                                         fc.filter_intra_cdfs[i], Ptr<int>.Null);
        }

        for (i = 0; i < SWITCHABLE_FILTER_CONTEXTS; ++i)
            av1_cost_tokens_from_cdf(mode_costs.switchable_interp_costs[i],
                                     fc.switchable_interp_cdf[i], Ptr<int>.Null);

        for (i = 0; i < PALATTE_BSIZE_CTXS; ++i)
        {
            av1_cost_tokens_from_cdf(mode_costs.palette_y_size_cost[i],
                                     fc.palette_y_size_cdf[i], Ptr<int>.Null);
            av1_cost_tokens_from_cdf(mode_costs.palette_uv_size_cost[i],
                                     fc.palette_uv_size_cdf[i], Ptr<int>.Null);
            for (j = 0; j < PALETTE_Y_MODE_CONTEXTS; ++j)
            {
                av1_cost_tokens_from_cdf(mode_costs.palette_y_mode_cost[i][j],
                                         fc.palette_y_mode_cdf[i][j], Ptr<int>.Null);
            }
        }

        for (i = 0; i < PALETTE_UV_MODE_CONTEXTS; ++i)
        {
            av1_cost_tokens_from_cdf(mode_costs.palette_uv_mode_cost[i],
                                     fc.palette_uv_mode_cdf[i], Ptr<int>.Null);
        }

        for (i = 0; i < PALETTE_SIZES; ++i)
        {
            for (j = 0; j < PALETTE_COLOR_INDEX_CONTEXTS; ++j)
            {
                av1_cost_tokens_from_cdf(mode_costs.palette_y_color_cost[i][j],
                                         fc.palette_y_color_index_cdf[i][j], Ptr<int>.Null);
                av1_cost_tokens_from_cdf(mode_costs.palette_uv_color_cost[i][j],
                                         fc.palette_uv_color_index_cdf[i][j], Ptr<int>.Null);
            }
        }

        int[] sign_cost = s_sign_cost ??= new int[CFL_JOINT_SIGNS];
        av1_cost_tokens_from_cdf(sign_cost, fc.cfl_sign_cdf, Ptr<int>.Null);
        for (int joint_sign = 0; joint_sign < CFL_JOINT_SIGNS; joint_sign++)
        {
            int[] cost_u = mode_costs.cfl_cost[joint_sign][CFL_PRED_U];
            int[] cost_v = mode_costs.cfl_cost[joint_sign][CFL_PRED_V];
            if (CFL_SIGN_U(joint_sign) == CFL_SIGN_ZERO)
            {
                memset<int>(cost_u, 0, CFL_ALPHABET_SIZE);
            }
            else
            {
                ushort[] cdf_u = fc.cfl_alpha_cdf[CFL_CONTEXT_U(joint_sign)];
                av1_cost_tokens_from_cdf(cost_u, cdf_u, Ptr<int>.Null);
            }
            if (CFL_SIGN_V(joint_sign) == CFL_SIGN_ZERO)
            {
                memset<int>(cost_v, 0, CFL_ALPHABET_SIZE);
            }
            else
            {
                ushort[] cdf_v = fc.cfl_alpha_cdf[CFL_CONTEXT_V(joint_sign)];
                av1_cost_tokens_from_cdf(cost_v, cdf_v, Ptr<int>.Null);
            }
            for (int u = 0; u < CFL_ALPHABET_SIZE; u++)
                cost_u[u] += sign_cost[joint_sign];
        }

        for (i = 0; i < MAX_TX_CATS; ++i)
            for (j = 0; j < TX_SIZE_CONTEXTS; ++j)
                av1_cost_tokens_from_cdf(mode_costs.tx_size_cost[i][j],
                                         fc.tx_size_cdf[i][j], Ptr<int>.Null);

        for (i = 0; i < TXFM_PARTITION_CONTEXTS; ++i)
        {
            av1_cost_tokens_from_cdf(mode_costs.txfm_partition_cost[i],
                                     fc.txfm_partition_cdf[i], Ptr<int>.Null);
        }

        for (i = TX_4X4; i < EXT_TX_SIZES; ++i)
        {
            int s;
            for (s = 1; s < EXT_TX_SETS_INTER; ++s)
            {
                if (use_inter_ext_tx_for_txsize[s][i] != 0)
                {
                    av1_cost_tokens_from_cdf(
                        mode_costs.inter_tx_type_costs[s][i], fc.inter_ext_tx_cdf[s][i],
                        av1_ext_tx_inv[av1_ext_tx_set_idx_to_type[1][s]]);
                }
            }
            for (s = 1; s < EXT_TX_SETS_INTRA; ++s)
            {
                if (use_intra_ext_tx_for_txsize[s][i] != 0)
                {
                    for (j = 0; j < INTRA_MODES; ++j)
                    {
                        av1_cost_tokens_from_cdf(
                            mode_costs.intra_tx_type_costs[s][i][j],
                            fc.intra_ext_tx_cdf[s][i][j],
                            av1_ext_tx_inv[av1_ext_tx_set_idx_to_type[0][s]]);
                    }
                }
            }
        }
        for (i = 0; i < DIRECTIONAL_MODES; ++i)
        {
            av1_cost_tokens_from_cdf(mode_costs.angle_delta_cost[i],
                                     fc.angle_delta_cdf[i], Ptr<int>.Null);
        }
        av1_cost_tokens_from_cdf(mode_costs.intrabc_cost, fc.intrabc_cdf, Ptr<int>.Null);

        for (i = 0; i < SPATIAL_PREDICTION_PROBS; ++i)
        {
            av1_cost_tokens_from_cdf(mode_costs.spatial_pred_cost[i],
                                     fc.seg.spatial_pred_seg_cdf[i], Ptr<int>.Null);
        }

        for (i = 0; i < SEG_TEMPORAL_PRED_CTXS; ++i)
        {
            av1_cost_tokens_from_cdf(mode_costs.tmp_pred_cost[i], fc.seg.pred_cdf[i],
                                     Ptr<int>.Null);
        }

        if (frame_is_intra_only(cm) == 0)
        {
            for (i = 0; i < COMP_INTER_CONTEXTS; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.comp_inter_cost[i],
                                         fc.comp_inter_cdf[i], Ptr<int>.Null);
            }

            for (i = 0; i < REF_CONTEXTS; ++i)
            {
                for (j = 0; j < SINGLE_REFS - 1; ++j)
                {
                    av1_cost_tokens_from_cdf(mode_costs.single_ref_cost[i][j],
                                             fc.single_ref_cdf[i][j], Ptr<int>.Null);
                }
            }

            for (i = 0; i < COMP_REF_TYPE_CONTEXTS; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.comp_ref_type_cost[i],
                                         fc.comp_ref_type_cdf[i], Ptr<int>.Null);
            }

            for (i = 0; i < UNI_COMP_REF_CONTEXTS; ++i)
            {
                for (j = 0; j < UNIDIR_COMP_REFS - 1; ++j)
                {
                    av1_cost_tokens_from_cdf(mode_costs.uni_comp_ref_cost[i][j],
                                             fc.uni_comp_ref_cdf[i][j], Ptr<int>.Null);
                }
            }

            for (i = 0; i < REF_CONTEXTS; ++i)
            {
                for (j = 0; j < FWD_REFS - 1; ++j)
                {
                    av1_cost_tokens_from_cdf(mode_costs.comp_ref_cost[i][j],
                                             fc.comp_ref_cdf[i][j], Ptr<int>.Null);
                }
            }

            for (i = 0; i < REF_CONTEXTS; ++i)
            {
                for (j = 0; j < BWD_REFS - 1; ++j)
                {
                    av1_cost_tokens_from_cdf(mode_costs.comp_bwdref_cost[i][j],
                                             fc.comp_bwdref_cdf[i][j], Ptr<int>.Null);
                }
            }

            for (i = 0; i < INTRA_INTER_CONTEXTS; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.intra_inter_cost[i],
                                         fc.intra_inter_cdf[i], Ptr<int>.Null);
            }

            for (i = 0; i < NEWMV_MODE_CONTEXTS; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.newmv_mode_cost[i], fc.newmv_cdf[i],
                                         Ptr<int>.Null);
            }

            for (i = 0; i < GLOBALMV_MODE_CONTEXTS; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.zeromv_mode_cost[i],
                                         fc.zeromv_cdf[i], Ptr<int>.Null);
            }

            for (i = 0; i < REFMV_MODE_CONTEXTS; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.refmv_mode_cost[i], fc.refmv_cdf[i],
                                         Ptr<int>.Null);
            }

            for (i = 0; i < DRL_MODE_CONTEXTS; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.drl_mode_cost0[i], fc.drl_cdf[i],
                                         Ptr<int>.Null);
            }
            for (i = 0; i < INTER_MODE_CONTEXTS; ++i)
                av1_cost_tokens_from_cdf(mode_costs.inter_compound_mode_cost[i],
                                         fc.inter_compound_mode_cdf[i], Ptr<int>.Null);
            for (i = 0; i < BLOCK_SIZES_ALL; ++i)
                av1_cost_tokens_from_cdf(mode_costs.compound_type_cost[i],
                                         fc.compound_type_cdf[i], Ptr<int>.Null);
            for (i = 0; i < BLOCK_SIZES_ALL; ++i)
            {
                if (av1_is_wedge_used(i) != 0)
                {
                    av1_cost_tokens_from_cdf(mode_costs.wedge_idx_cost[i],
                                             fc.wedge_idx_cdf[i], Ptr<int>.Null);
                }
            }
            for (i = 0; i < BLOCK_SIZE_GROUPS; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.interintra_cost[i],
                                         fc.interintra_cdf[i], Ptr<int>.Null);
                av1_cost_tokens_from_cdf(mode_costs.interintra_mode_cost[i],
                                         fc.interintra_mode_cdf[i], Ptr<int>.Null);
            }
            for (i = 0; i < BLOCK_SIZES_ALL; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.wedge_interintra_cost[i],
                                         fc.wedge_interintra_cdf[i], Ptr<int>.Null);
            }
            for (i = BLOCK_8X8; i < BLOCK_SIZES_ALL; i++)
            {
                av1_cost_tokens_from_cdf(mode_costs.motion_mode_cost[i],
                                         fc.motion_mode_cdf[i], Ptr<int>.Null);
            }
            for (i = BLOCK_8X8; i < BLOCK_SIZES_ALL; i++)
            {
                av1_cost_tokens_from_cdf(mode_costs.motion_mode_cost1[i],
                                         fc.obmc_cdf[i], Ptr<int>.Null);
            }
            for (i = 0; i < COMP_INDEX_CONTEXTS; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.comp_idx_cost[i],
                                         fc.compound_index_cdf[i], Ptr<int>.Null);
            }
            for (i = 0; i < COMP_GROUP_IDX_CONTEXTS; ++i)
            {
                av1_cost_tokens_from_cdf(mode_costs.comp_group_idx_cost[i],
                                         fc.comp_group_idx_cdf[i], Ptr<int>.Null);
            }
        }
    }

    // C: int sign_cost[CFL_JOINT_SIGNS] on the stack of av1_fill_mode_rates (a leaf).
    [ThreadStatic] private static int[] s_sign_cost;

    // [subset] av1_fill_lr_rates: !CONFIG_REALTIME_ONLY.

    // Values are now correlated to quantizer.
    private static readonly int[] sad_per_bit_lut_8 = new int[QINDEX_RANGE];
    // [subset] sad_per_bit_lut_10 / sad_per_bit_lut_12: CONFIG_AV1_HIGHBITDEPTH is 0, so
    // seq_params->bit_depth is always AOM_BITS_8 and these tables are never read. C fills them in
    // init_me_luts(); filling them here would only call av1_convert_qindex_to_q() with 10/12 bit.

    private static void init_me_luts_bd(int[] bit16lut, int range,
                                        aom_bit_depth_t bit_depth)
    {
        int i;
        // Initialize the sad lut tables using a formulaic calculation for now.
        // This is to make it easier to resolve the impact of experimental changes
        // to the quantizer tables.
        for (i = 0; i < range; i++)
        {
            double q = av1_convert_qindex_to_q(i, bit_depth);
            bit16lut[i] = (int)(0.0418 * q + 2.4107);
        }
    }

    private static void init_me_luts()
    {
        init_me_luts_bd(sad_per_bit_lut_8, QINDEX_RANGE, AOM_BITS_8);
        // [subset] init_me_luts_bd(sad_per_bit_lut_10, QINDEX_RANGE, AOM_BITS_10) and
        // init_me_luts_bd(sad_per_bit_lut_12, QINDEX_RANGE, AOM_BITS_12): see above.
    }

    private static readonly Action s_init_me_luts = init_me_luts;

    internal static void av1_init_me_luts() { aom_once(s_init_me_luts); }

    private static readonly int[] rd_boost_factor = { 64, 32, 32, 32, 24, 16, 12, 12,
                                                      8,  8,  4,  4,  2,  2,  1,  0 };

    private static readonly int[] rd_layer_depth_factor = {
        160, 160, 160, 160, 192, 208, 224
    };

    // Returns the default rd multiplier for inter frames for a given qindex.
    // The function here is a first pass estimate based on data from
    // a previous Vizer run
    private static double def_inter_rd_multiplier(int qindex)
    {
        return 3.2 + (0.0015 * (double)qindex);
    }

    // Returns the default rd multiplier for ARF/Golden Frames for a given qindex.
    // The function here is a first pass estimate based on data from
    // a previous Vizer run
    private static double def_arf_rd_multiplier(int qindex)
    {
        return 3.25 + (0.0015 * (double)qindex);
    }

    // Returns the default rd multiplier for key frames for a given qindex.
    // The function here is a first pass estimate based on data from
    // a previous Vizer run
    private static double def_kf_rd_multiplier(int qindex)
    {
        return 3.3 + (0.0015 * (double)qindex);
    }

    internal static int av1_compute_rd_mult_based_on_qindex(aom_bit_depth_t bit_depth,
                                                            FRAME_UPDATE_TYPE update_type,
                                                            int qindex, aom_tune_metric tuning,
                                                            MODE mode)
    {
        int q = av1_dc_quant_QTX(qindex, 0, bit_depth);
        long rdmult = q * q;
        if (update_type == KF_UPDATE)
        {
            double def_rd_q_mult = def_kf_rd_multiplier(q);
            rdmult = (long)((double)rdmult * def_rd_q_mult);
        }
        else if ((update_type == GF_UPDATE) || (update_type == ARF_UPDATE))
        {
            double def_rd_q_mult = def_arf_rd_multiplier(q);
            rdmult = (long)((double)rdmult * def_rd_q_mult);
        }
        else
        {
            double def_rd_q_mult = def_inter_rd_multiplier(q);
            rdmult = (long)((double)rdmult * def_rd_q_mult);
        }

        if (tuning == AOM_TUNE_IQ || tuning == AOM_TUNE_SSIMULACRA2)
        {
            int weight;

            // Weight terms were determined by iteratively testing various weights
            // on CID22 and Daala's subset1, and observing its effects on visual
            // quality and SSIMULACRA 2 scores along the usable (0-100) range.
            if (mode == REALTIME)
            {
                // Realtime mode: further multiply rdmult by a fourth (32/128 = 0.25)
                // to improve image quality.
                // The most noticeable effect is that for inter frames, there's a
                // stronger bias towards choosing inter prediction modes with encoded
                // coefficient residuals (i.e. no skip mode).
                weight = 32;
            }
            else
            {
                // All-intra and good-quality modes: Further multiply rdmult (by up to
                // 200/128 = 1.5625) to improve image quality.
                // (See libaom for the full rationale.)
                weight = clamp(((255 - qindex) * 3) / 4, 0, 72) + 128;
            }
            rdmult = (long)((double)rdmult * weight / 128.0);
        }

        switch (bit_depth)
        {
            case AOM_BITS_8: break;
            case AOM_BITS_10: rdmult = ROUND_POWER_OF_TWO(rdmult, 4); break;
            case AOM_BITS_12: rdmult = ROUND_POWER_OF_TWO(rdmult, 8); break;
            default:
                Debug.Assert(false, "bit_depth should be AOM_BITS_8, AOM_BITS_10 or AOM_BITS_12");
                return -1;
        }
        return rdmult > 0 ? (int)AOMMIN(rdmult, int.MaxValue) : 1;
    }

    internal static int av1_compute_rd_mult(int qindex, aom_bit_depth_t bit_depth,
                                            FRAME_UPDATE_TYPE update_type,
                                            int layer_depth, int boost_index,
                                            FRAME_TYPE frame_type,
                                            int use_fixed_qp_offsets,
                                            int is_stat_consumption_stage,
                                            aom_tune_metric tuning, MODE mode)
    {
        long rdmult = av1_compute_rd_mult_based_on_qindex(bit_depth, update_type,
                                                          qindex, tuning, mode);
        if (is_stat_consumption_stage != 0 && (use_fixed_qp_offsets == 0) &&
            (frame_type != KEY_FRAME))
        {
            // Layer depth adjustment
            rdmult = (rdmult * rd_layer_depth_factor[layer_depth]) >> 7;
            // ARF boost adjustment
            rdmult += ((rdmult * rd_boost_factor[boost_index]) >> 7);
        }
        return rdmult > 0 ? (int)AOMMIN(rdmult, int.MaxValue) : 1;
    }

    // [subset] av1_get_deltaq_offset, av1_adjust_q_from_delta_q_res: deltaq modes are off.
    // [subset] av1_get_adaptive_rdmult: !CONFIG_REALTIME_ONLY.

    private static int compute_rd_thresh_factor(int qindex, aom_bit_depth_t bit_depth)
    {
        double q;
        switch (bit_depth)
        {
            case AOM_BITS_8: q = av1_dc_quant_QTX(qindex, 0, AOM_BITS_8) / 4.0; break;
            case AOM_BITS_10:
                q = av1_dc_quant_QTX(qindex, 0, AOM_BITS_10) / 16.0;
                break;
            case AOM_BITS_12:
                q = av1_dc_quant_QTX(qindex, 0, AOM_BITS_12) / 64.0;
                break;
            default:
                Debug.Assert(false, "bit_depth should be AOM_BITS_8, AOM_BITS_10 or AOM_BITS_12");
                return -1;
        }
        // TODO(debargha): Adjust the function below.
        return AOMMAX((int)(Math.Pow(q, RD_THRESH_POW) * 5.12), 8);
    }

    internal static void av1_set_sad_per_bit(AV1_COMP cpi, ref int sadperbit, int qindex)
    {
        switch (cpi.common.seq_params.bit_depth)
        {
            case AOM_BITS_8: sadperbit = sad_per_bit_lut_8[qindex]; break;
            case AOM_BITS_10:
            case AOM_BITS_12:
                // [subset] sad_per_bit_lut_10 / sad_per_bit_lut_12: CONFIG_AV1_HIGHBITDEPTH is 0.
                throw new NotSupportedException("av1_set_sad_per_bit: high bit depth");
            default:
                Debug.Assert(false, "bit_depth should be AOM_BITS_8, AOM_BITS_10 or AOM_BITS_12");
                break;
        }
    }

    // C: THR_MODES mode_indices[RTC_REFS * RTC_MODES] on the stack of set_block_thresholds.
    private const int MODE_INDICES_SIZE = RTC_REFS * RTC_MODES;

    private static void set_block_thresholds(AV1_COMMON cm, RD_OPT rd,
                                             int use_nonrd_pick_mode)
    {
        int i, bsize, segment_id;
        Span<THR_MODES> mode_indices = stackalloc THR_MODES[MODE_INDICES_SIZE];
        int num_modes_count = use_nonrd_pick_mode != 0 ? 0 : MAX_MODES;

        if (use_nonrd_pick_mode != 0)
        {
            for (int r_idx = 0; r_idx < RTC_REFS; r_idx++)
            {
                MV_REFERENCE_FRAME @ref = real_time_ref_combos[r_idx][0];
                if (@ref != INTRA_FRAME)
                {
                    for (i = 0; i < RTC_INTER_MODES; i++)
                        mode_indices[num_modes_count++] =
                            mode_idx[@ref][mode_offset(inter_mode_list[i])];
                }
                else
                {
                    for (i = 0; i < RTC_INTRA_MODES; i++)
                        mode_indices[num_modes_count++] =
                            mode_idx[@ref][mode_offset(intra_mode_list[i])];
                }
            }
        }

        for (segment_id = 0; segment_id < MAX_SEGMENTS; ++segment_id)
        {
            int qindex = clamp(
                av1_get_qindex(cm.seg, segment_id, cm.quant_params.base_qindex) +
                    cm.quant_params.y_dc_delta_q,
                0, MAXQ);
            int q = compute_rd_thresh_factor(qindex, cm.seq_params.bit_depth);

            for (bsize = 0; bsize < BLOCK_SIZES_ALL; ++bsize)
            {
                // Threshold here seems unnecessarily harsh but fine given actual
                // range of values used for cpi->sf.thresh_mult[].
                int t = q * rd_thresh_block_size_factor[bsize];
                int thresh_max = int.MaxValue / t;

                for (i = 0; i < num_modes_count; ++i)
                {
                    int mode_index = use_nonrd_pick_mode != 0 ? mode_indices[i] : i;
                    rd.threshes[segment_id][bsize][mode_index] =
                        rd.thresh_mult[mode_index] < thresh_max
                            ? rd.thresh_mult[mode_index] * t / 4
                            : int.MaxValue;
                }
            }
        }
    }

    // C: int br_rate_all[LEVEL_CONTEXTS][BR_CDF_SIZE] on the stack of av1_fill_coeff_costs (a
    // leaf), kept flat: row ctx starts at ctx * BR_CDF_SIZE.
    [ThreadStatic] private static int[] s_br_rate_all;

    // In real-time mode, fill the coeff cost tables with the batched/SIMD
    // specializations; otherwise use the generic scalar path.
    // PORT-NOTE: C calls av1_cost_tokens_from_cdf_{2,4}_batch once over a whole contiguous
    // [ctx][n] table. The port's cost and CDF tables are jagged (one array per ctx), so the batch
    // function is called once per ctx with n = 1. av1_cost_tokens_from_cdf_{2,4}_batch_c is a
    // plain loop of av1_cost_tokens_from_cdf_{2,4} over the rows, so the result is identical.
    internal static void av1_fill_coeff_costs(CoeffCosts coeff_costs, FRAME_CONTEXT fc,
                                              int num_planes, int is_realtime)
    {
        int nplanes = AOMMIN(num_planes, PLANE_TYPES);
        for (int eob_multi_size = 0; eob_multi_size < 7; ++eob_multi_size)
        {
            for (int plane = 0; plane < nplanes; ++plane)
            {
                LV_MAP_EOB_COST pcost = coeff_costs.eob_costs[eob_multi_size][plane];

                for (int ctx = 0; ctx < 2; ++ctx)
                {
                    ushort[] pcdf;
                    switch (eob_multi_size)
                    {
                        case 0: pcdf = fc.eob_flag_cdf16[plane][ctx]; break;
                        case 1: pcdf = fc.eob_flag_cdf32[plane][ctx]; break;
                        case 2: pcdf = fc.eob_flag_cdf64[plane][ctx]; break;
                        case 3: pcdf = fc.eob_flag_cdf128[plane][ctx]; break;
                        case 4: pcdf = fc.eob_flag_cdf256[plane][ctx]; break;
                        case 5: pcdf = fc.eob_flag_cdf512[plane][ctx]; break;
                        case 6:
                        default: pcdf = fc.eob_flag_cdf1024[plane][ctx]; break;
                    }
                    av1_cost_tokens_from_cdf(pcost.eob_cost[ctx], pcdf, Ptr<int>.Null);
                }
            }
        }
        int[] br_rate_all = s_br_rate_all ??= new int[LEVEL_CONTEXTS * BR_CDF_SIZE];
        for (int tx_size = 0; tx_size < TX_SIZES; ++tx_size)
        {
            for (int plane = 0; plane < nplanes; ++plane)
            {
                LV_MAP_COEFF_COST pcost = coeff_costs.coeff_costs[tx_size][plane];

                if (is_realtime != 0)
                {
                    for (int ctx = 0; ctx < TXB_SKIP_CONTEXTS; ++ctx)
                        av1_cost_tokens_from_cdf_2_batch(pcost.txb_skip_cost[ctx], 2,
                                                         fc.txb_skip_cdf[tx_size][ctx],
                                                         CDF_SIZE(2), 1);
                }
                else
                {
                    for (int ctx = 0; ctx < TXB_SKIP_CONTEXTS; ++ctx)
                        av1_cost_tokens_from_cdf(pcost.txb_skip_cost[ctx],
                                                 fc.txb_skip_cdf[tx_size][ctx], Ptr<int>.Null);
                }

                for (int ctx = 0; ctx < SIG_COEF_CONTEXTS_EOB; ++ctx)
                    av1_cost_tokens_from_cdf(pcost.base_eob_cost[ctx],
                                             fc.coeff_base_eob_cdf[tx_size][plane][ctx],
                                             Ptr<int>.Null);
                // base_cost[ctx] is int[8]; only [0..3] are filled here (stride 8).
                if (is_realtime != 0)
                {
                    for (int ctx = 0; ctx < SIG_COEF_CONTEXTS; ++ctx)
                        av1_cost_tokens_from_cdf_4_batch(pcost.base_cost[ctx], 8,
                                                         fc.coeff_base_cdf[tx_size][plane][ctx],
                                                         CDF_SIZE(4), 1);
                }
                else
                {
                    for (int ctx = 0; ctx < SIG_COEF_CONTEXTS; ++ctx)
                        av1_cost_tokens_from_cdf(pcost.base_cost[ctx],
                                                 fc.coeff_base_cdf[tx_size][plane][ctx],
                                                 Ptr<int>.Null);
                }

                for (int ctx = 0; ctx < SIG_COEF_CONTEXTS; ++ctx)
                {
                    pcost.base_cost[ctx][4] = 0;
                    pcost.base_cost[ctx][5] = pcost.base_cost[ctx][1] +
                                              av1_cost_literal(1) -
                                              pcost.base_cost[ctx][0];
                    pcost.base_cost[ctx][6] =
                        pcost.base_cost[ctx][2] - pcost.base_cost[ctx][1];
                    pcost.base_cost[ctx][7] =
                        pcost.base_cost[ctx][3] - pcost.base_cost[ctx][2];
                }

                if (is_realtime != 0)
                {
                    for (int ctx = 0; ctx < EOB_COEF_CONTEXTS; ++ctx)
                        av1_cost_tokens_from_cdf_2_batch(pcost.eob_extra_cost[ctx], 2,
                                                         fc.eob_extra_cdf[tx_size][plane][ctx],
                                                         CDF_SIZE(2), 1);
                }
                else
                {
                    for (int ctx = 0; ctx < EOB_COEF_CONTEXTS; ++ctx)
                        av1_cost_tokens_from_cdf(pcost.eob_extra_cost[ctx],
                                                 fc.eob_extra_cdf[tx_size][plane][ctx],
                                                 Ptr<int>.Null);
                }

                for (int ctx = 0; ctx < DC_SIGN_CONTEXTS; ++ctx)
                    av1_cost_tokens_from_cdf(pcost.dc_sign_cost[ctx],
                                             fc.dc_sign_cdf[plane][ctx], Ptr<int>.Null);

                if (is_realtime != 0)
                {
                    for (int ctx = 0; ctx < LEVEL_CONTEXTS; ++ctx)
                        av1_cost_tokens_from_cdf_4_batch(
                            new Ptr<int>(br_rate_all, ctx * BR_CDF_SIZE), BR_CDF_SIZE,
                            fc.coeff_br_cdf[AOMMIN(tx_size, TX_32X32)][plane][ctx],
                            CDF_SIZE(BR_CDF_SIZE), 1);
                }
                else
                {
                    for (int ctx = 0; ctx < LEVEL_CONTEXTS; ++ctx)
                        av1_cost_tokens_from_cdf(
                            new Ptr<int>(br_rate_all, ctx * BR_CDF_SIZE),
                            fc.coeff_br_cdf[AOMMIN(tx_size, TX_32X32)][plane][ctx], Ptr<int>.Null);
                }
                for (int ctx = 0; ctx < LEVEL_CONTEXTS; ++ctx)
                {
                    Ptr<int> br_rate = new Ptr<int>(br_rate_all, ctx * BR_CDF_SIZE);
                    int prev_cost = 0;
                    int i, j;
                    for (i = 0; i < COEFF_BASE_RANGE; i += BR_CDF_SIZE - 1)
                    {
                        for (j = 0; j < BR_CDF_SIZE - 1; j++)
                        {
                            pcost.lps_cost[ctx][i + j] = prev_cost + br_rate[j];
                        }
                        prev_cost += br_rate[j];
                    }
                    pcost.lps_cost[ctx][i] = prev_cost;
                }
                for (int ctx = 0; ctx < LEVEL_CONTEXTS; ++ctx)
                {
                    pcost.lps_cost[ctx][0 + COEFF_BASE_RANGE + 1] =
                        pcost.lps_cost[ctx][0];
                    for (int i = 1; i <= COEFF_BASE_RANGE; ++i)
                    {
                        pcost.lps_cost[ctx][i + COEFF_BASE_RANGE + 1] =
                            pcost.lps_cost[ctx][i] - pcost.lps_cost[ctx][i - 1];
                    }
                }
            }
        }
    }

    internal static void av1_fill_mv_costs(nmv_context nmvc, int integer_mv, int usehp,
                                           MvCosts mv_costs)
    {
        // Avoid accessing 'mv_costs' when it is not allocated.
        if (mv_costs == null) return;

        mv_costs.nmv_cost[0] = new Ptr<int>(mv_costs.nmv_cost_alloc[0], MV_MAX);
        mv_costs.nmv_cost[1] = new Ptr<int>(mv_costs.nmv_cost_alloc[1], MV_MAX);
        mv_costs.nmv_cost_hp[0] = new Ptr<int>(mv_costs.nmv_cost_hp_alloc[0], MV_MAX);
        mv_costs.nmv_cost_hp[1] = new Ptr<int>(mv_costs.nmv_cost_hp_alloc[1], MV_MAX);
        if (integer_mv != 0)
        {
            mv_costs.mv_cost_stack = mv_costs.nmv_cost;
            av1_build_nmv_cost_table(mv_costs.nmv_joint_cost, mv_costs.mv_cost_stack,
                                     nmvc, MV_SUBPEL_NONE);
        }
        else
        {
            mv_costs.mv_cost_stack =
                usehp != 0 ? mv_costs.nmv_cost_hp : mv_costs.nmv_cost;
            av1_build_nmv_cost_table(mv_costs.nmv_joint_cost, mv_costs.mv_cost_stack,
                                     nmvc, usehp);
        }
    }

    // [subset] av1_fill_dv_costs: intra block copy is off (IntraBCMVCosts is not ported).

    // Mapping of entropy cost update frequency from the encoder's codec control
    // settings of type COST_UPDATE_TYPE to speed features of type
    // INTERNAL_COST_UPDATE_TYPE. (C: function-local static table.)
    private static readonly INTERNAL_COST_UPDATE_TYPE[] map_cost_upd_to_internal_cost_upd = {
        INTERNAL_COST_UPD_SB, INTERNAL_COST_UPD_SBROW, INTERNAL_COST_UPD_TILE,
        INTERNAL_COST_UPD_OFF
    };

    // Populates speed features based on codec control settings (of type
    // COST_UPDATE_TYPE) and expected speed feature settings (of type
    // INTERNAL_COST_UPDATE_TYPE) by considering the least frequent cost update.
    // The populated/updated speed features are used for cost updates in the
    // encoder.
    // WARNING: Population of unified cost update frequency needs to be taken care
    // accordingly, in case of any modifications/additions to the enum
    // COST_UPDATE_TYPE/INTERNAL_COST_UPDATE_TYPE.
    private static void populate_unified_cost_update_freq(
        in CostUpdateFreq cost_upd_freq, SPEED_FEATURES sf)
    {
        INTER_MODE_SPEED_FEATURES inter_sf = sf.inter_sf;

        inter_sf.mv_cost_upd_level =
            AOMMIN(inter_sf.mv_cost_upd_level,
                   map_cost_upd_to_internal_cost_upd[cost_upd_freq.mv]);
        inter_sf.coeff_cost_upd_level =
            AOMMIN(inter_sf.coeff_cost_upd_level,
                   map_cost_upd_to_internal_cost_upd[cost_upd_freq.coeff]);
        inter_sf.mode_cost_upd_level =
            AOMMIN(inter_sf.mode_cost_upd_level,
                   map_cost_upd_to_internal_cost_upd[cost_upd_freq.mode]);
        sf.intra_sf.dv_cost_upd_level =
            AOMMIN(sf.intra_sf.dv_cost_upd_level,
                   map_cost_upd_to_internal_cost_upd[cost_upd_freq.dv]);
    }

    // Checks if entropy costs should be initialized/updated at frame level or not.
    private static int is_frame_level_cost_upd_freq_set(
        AV1_COMMON cm, INTERNAL_COST_UPDATE_TYPE cost_upd_level,
        int use_nonrd_pick_mode, int frames_since_key)
    {
        int fill_costs = B(
            frame_is_intra_only(cm) != 0 ||
            (use_nonrd_pick_mode != 0 ? frames_since_key < 2
                                      : (cm.current_frame.frame_number & 0x07) == 1));
        // Nonrd frames without CDF updates get no per superblock cost update.
        int update_per_frame =
            B(use_nonrd_pick_mode == 0 || cm.features.disable_cdf_update);
        return B((update_per_frame != 0 && cost_upd_level != INTERNAL_COST_UPD_OFF) ||
                 cost_upd_level == INTERNAL_COST_UPD_TILE || fill_costs != 0);
    }

    // Decide whether we want to update the mode entropy cost for the current frame.
    // The logit is currently inherited from selective_disable_cdf_rtc.
    private static bool should_force_mode_cost_update(AV1_COMP cpi)
    {
        REAL_TIME_SPEED_FEATURES rt_sf = cpi.sf.rt_sf;
        if (!rt_sf.frame_level_mode_cost_update)
        {
            return false;
        }

        if (cpi.oxcf.algo_cfg.cdf_update_mode == 2)
        {
            return cpi.frames_since_last_update == 1;
        }
        else if (cpi.oxcf.algo_cfg.cdf_update_mode == 1)
        {
            if (cpi.svc.number_spatial_layers == 1 &&
                cpi.svc.number_temporal_layers == 1)
            {
                AV1_COMMON cm = cpi.common;
                RATE_CONTROL rc = cpi.rc;

                return frame_is_intra_only(cm) != 0 || is_frame_resize_pending(cpi) != 0 ||
                       rc.high_source_sad != 0 || rc.frames_since_key < 10 ||
                       cpi.cyclic_refresh.counter_encode_maxq_scene_change < 10 ||
                       cm.current_frame.frame_number % 8 == 0;
            }
            else if (cpi.svc.number_temporal_layers > 1)
            {
                return cpi.svc.temporal_layer_id != cpi.svc.number_temporal_layers - 1;
            }
        }

        return false;
    }

    internal static void av1_initialize_rd_consts(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCK x = cpi.td.mb;
        SPEED_FEATURES sf = cpi.sf;
        RD_OPT rd = cpi.rd;
        int use_nonrd_pick_mode = cpi.sf.rt_sf.use_nonrd_pick_mode;
        int frames_since_key = cpi.rc.frames_since_key;

        GF_GROUP gf_group = cpi.ppi.gf_group;
        int boost_index = AOMMIN(15, (cpi.ppi.p_rc.gfu_boost / 100));
        int layer_depth = AOMMIN(gf_group.layer_depth[cpi.gf_frame_index], 6);
        FRAME_TYPE frame_type = cm.current_frame.frame_type;

        int qindex_rdmult =
            cm.quant_params.base_qindex + cm.quant_params.y_dc_delta_q;
        rd.RDMULT = av1_compute_rd_mult(
            qindex_rdmult, cm.seq_params.bit_depth,
            cpi.ppi.gf_group.update_type[cpi.gf_frame_index], layer_depth,
            boost_index, frame_type, cpi.oxcf.q_cfg.use_fixed_qp_offsets,
            is_stat_consumption_stage(cpi), cpi.oxcf.tune_cfg.tuning,
            cpi.oxcf.mode);
        // (CONFIG_RD_COMMAND is 0.)

        av1_set_error_per_bit(ref x.errorperbit, rd.RDMULT);

        set_block_thresholds(cm, rd, cpi.sf.rt_sf.use_nonrd_pick_mode);

        populate_unified_cost_update_freq(cpi.oxcf.cost_upd_freq, sf);
        INTER_MODE_SPEED_FEATURES inter_sf = cpi.sf.inter_sf;
        // Frame level mv cost update
        if (is_frame_level_cost_upd_freq_set(cm, inter_sf.mv_cost_upd_level,
                                             use_nonrd_pick_mode, frames_since_key) != 0)
        {
            // Also build the table for the other precision. A later frame that skips
            // this update can switch to it in av1_pick_and_set_high_precision_mv().
            if (!cm.features.cur_frame_force_integer_mv)
            {
                av1_fill_mv_costs(cm.fc.nmvc, 0, B(!cm.features.allow_high_precision_mv),
                                  x.mv_costs);
            }
            av1_fill_mv_costs(cm.fc.nmvc, B(cm.features.cur_frame_force_integer_mv),
                              B(cm.features.allow_high_precision_mv), x.mv_costs);
        }

        // Frame level coefficient cost update
        if (is_frame_level_cost_upd_freq_set(cm, inter_sf.coeff_cost_upd_level,
                                             use_nonrd_pick_mode, frames_since_key) != 0)
            av1_fill_coeff_costs(x.coeff_costs, cm.fc, av1_num_planes(cm),
                                 B(cpi.oxcf.mode == REALTIME));

        // Frame level mode cost update
        if (should_force_mode_cost_update(cpi) ||
            is_frame_level_cost_upd_freq_set(cm, inter_sf.mode_cost_upd_level,
                                             use_nonrd_pick_mode, frames_since_key) != 0)
            av1_fill_mode_rates(cm, x.mode_costs, cm.fc);

        // Frame level dv cost update
        if (av1_need_dv_costs(cpi))
        {
            // [subset] dv costs (IntraBCMVCosts, av1_fill_dv_costs): intra block copy is off
            // (allow_intrabc needs screen content and rt_use_intrabc / the rd path).
            throw new NotSupportedException("av1_initialize_rd_consts: dv costs");
        }
    }

    // NOTE: The tables below must be of the same size.

    // The functions described below are sampled at the four most significant
    // bits of x^2 + 8 / 256.

    // Normalized rate:
    // This table models the rate for a Laplacian source with given variance
    // when quantized with a uniform quantizer with given stepsize. The
    // closed form expression is:
    // Rn(x) = H(sqrt(r)) + sqrt(r)*[1 + H(r)/(1 - r)],
    // where r = exp(-sqrt(2) * x) and x = qpstep / sqrt(variance),
    // and H(x) is the binary entropy function.
    private static readonly int[] rate_tab_q10 = {
        65536, 6086, 5574, 5275, 5063, 4899, 4764, 4651, 4553, 4389, 4255, 4142,
        4044,  3958, 3881, 3811, 3748, 3635, 3538, 3453, 3376, 3307, 3244, 3186,
        3133,  3037, 2952, 2877, 2809, 2747, 2690, 2638, 2589, 2501, 2423, 2353,
        2290,  2232, 2179, 2130, 2084, 2001, 1928, 1862, 1802, 1748, 1698, 1651,
        1608,  1530, 1460, 1398, 1342, 1290, 1243, 1199, 1159, 1086, 1021, 963,
        911,   864,  821,  781,  745,  680,  623,  574,  530,  490,  455,  424,
        395,   345,  304,  269,  239,  213,  190,  171,  154,  126,  104,  87,
        73,    61,   52,   44,   38,   28,   21,   16,   12,   10,   8,    6,
        5,     3,    2,    1,    1,    1,    0,    0,
    };
    // Normalized distortion:
    // This table models the normalized distortion for a Laplacian source
    // with given variance when quantized with a uniform quantizer
    // with given stepsize. The closed form expression is:
    // Dn(x) = 1 - 1/sqrt(2) * x / sinh(x/sqrt(2))
    // where x = qpstep / sqrt(variance).
    // Note the actual distortion is Dn * variance.
    private static readonly int[] dist_tab_q10 = {
        0,    0,    1,    1,    1,    2,    2,    2,    3,    3,    4,    5,
        5,    6,    7,    7,    8,    9,    11,   12,   13,   15,   16,   17,
        18,   21,   24,   26,   29,   31,   34,   36,   39,   44,   49,   54,
        59,   64,   69,   73,   78,   88,   97,   106,  115,  124,  133,  142,
        151,  167,  184,  200,  215,  231,  245,  260,  274,  301,  327,  351,
        375,  397,  418,  439,  458,  495,  528,  559,  587,  613,  637,  659,
        680,  717,  749,  777,  801,  823,  842,  859,  874,  899,  919,  936,
        949,  960,  969,  977,  983,  994,  1001, 1006, 1010, 1013, 1015, 1017,
        1018, 1020, 1022, 1022, 1023, 1023, 1023, 1024,
    };
    private static readonly int[] xsq_iq_q10 = {
        0,      4,      8,      12,     16,     20,     24,     28,     32,
        40,     48,     56,     64,     72,     80,     88,     96,     112,
        128,    144,    160,    176,    192,    208,    224,    256,    288,
        320,    352,    384,    416,    448,    480,    544,    608,    672,
        736,    800,    864,    928,    992,    1120,   1248,   1376,   1504,
        1632,   1760,   1888,   2016,   2272,   2528,   2784,   3040,   3296,
        3552,   3808,   4064,   4576,   5088,   5600,   6112,   6624,   7136,
        7648,   8160,   9184,   10208,  11232,  12256,  13280,  14304,  15328,
        16352,  18400,  20448,  22496,  24544,  26592,  28640,  30688,  32736,
        36832,  40928,  45024,  49120,  53216,  57312,  61408,  65504,  73696,
        81888,  90080,  98272,  106464, 114656, 122848, 131040, 147424, 163808,
        180192, 196576, 212960, 229344, 245728,
    };

    // (C: the three tables above are function-local statics of model_rd_norm.)
    private static void model_rd_norm(int xsq_q10, ref int r_q10, ref int d_q10)
    {
        int tmp = (xsq_q10 >> 2) + 8;
        int k = get_msb(tmp) - 3;
        int xq = (k << 3) + ((tmp >> k) & 0x7);
        int one_q10 = 1 << 10;
        int a_q10 = ((xsq_q10 - xsq_iq_q10[xq]) << 10) >> (2 + k);
        int b_q10 = one_q10 - a_q10;
        r_q10 = (rate_tab_q10[xq] * b_q10 + rate_tab_q10[xq + 1] * a_q10) >> 10;
        d_q10 = (dist_tab_q10[xq] * b_q10 + dist_tab_q10[xq + 1] * a_q10) >> 10;
    }

    private const uint MAX_XSQ_Q10 = 245727;

    internal static void av1_model_rd_from_var_lapndz(long var, uint n_log2,
                                                      uint qstep, ref int rate,
                                                      ref long dist)
    {
        // This function models the rate and distortion for a Laplacian
        // source with given variance when quantized with a uniform quantizer
        // with given stepsize. The closed form expressions are in:
        // Hang and Chen, "Source Model for transform video coder and its
        // application - Part I: Fundamental Theory", IEEE Trans. Circ.
        // Sys. for Video Tech., April 1997.
        if (var == 0)
        {
            rate = 0;
            dist = 0;
        }
        else
        {
            int d_q10 = 0, r_q10 = 0;
            ulong xsq_q10_64 =
                ((((ulong)qstep * qstep) << (int)(n_log2 + 10)) + (ulong)(var >> 1)) / (ulong)var;
            int xsq_q10 = (int)AOMMIN(xsq_q10_64, (ulong)MAX_XSQ_Q10);
            model_rd_norm(xsq_q10, ref r_q10, ref d_q10);
            rate = ROUND_POWER_OF_TWO(r_q10 << (int)n_log2, 10 - AV1_PROB_COST_SHIFT);
            dist = (var * (long)d_q10 + 512) >> 10;
        }
    }

    // [subset] interp_cubic, av1_interp_cubic_rate_dist_c, sse_norm_curvfit_model_cat_lookup,
    // av1_model_rd_curvfit: curvfit model rd is only used by the rd mode search.

    // Note: The clamp below is not necessary, but it prevents
    // assertion failures in the rd functions on the edges
    // In the worst case, this requires a border of
    //   max_block_width + 2*AOM_INTERP_EXTEND = 128 + 2*4 = 136 pixels
    // around the frame edges.
    private static void enc_clamp_mv(AV1_COMMON cm, MACROBLOCKD xd, ref MV mv)
    {
        int bw = xd.width << MI_SIZE_LOG2;
        int bh = xd.height << MI_SIZE_LOG2;

        int px_to_left_edge = xd.mi_col << MI_SIZE_LOG2;
        int px_to_right_edge = (cm.mi_params.mi_cols - xd.mi_col) << MI_SIZE_LOG2;
        int px_to_top_edge = xd.mi_row << MI_SIZE_LOG2;
        int px_to_bottom_edge = (cm.mi_params.mi_rows - xd.mi_row) << MI_SIZE_LOG2;

        SubpelMvLimits mv_limits = new SubpelMvLimits
        {
            col_min = -GET_MV_SUBPEL(px_to_left_edge + bw + AOM_INTERP_EXTEND),
            col_max = GET_MV_SUBPEL(px_to_right_edge + AOM_INTERP_EXTEND),
            row_min = -GET_MV_SUBPEL(px_to_top_edge + bh + AOM_INTERP_EXTEND),
            row_max = GET_MV_SUBPEL(px_to_bottom_edge + AOM_INTERP_EXTEND)
        };
        clamp_mv(ref mv, in mv_limits);
    }

    // C: const MV_REFERENCE_FRAME ref_frames[2] on the stack of av1_mv_pred (a leaf).
    [ThreadStatic] private static MV_REFERENCE_FRAME[] s_mv_pred_ref_frames;

    internal static void av1_mv_pred(AV1_COMP cpi, MACROBLOCK x, Ptr<byte> ref_y_buffer,
                                     int ref_y_stride, int ref_frame, BLOCK_SIZE block_size)
    {
        MV_REFERENCE_FRAME[] ref_frames = s_mv_pred_ref_frames ??= new MV_REFERENCE_FRAME[2];
        ref_frames[0] = ref_frame;
        ref_frames[1] = NONE_FRAME;
        int_mv ref_mv =
            av1_get_ref_mv_from_stack(0, ref_frames, 0, x.mbmi_ext);
        int_mv ref_mv1 =
            av1_get_ref_mv_from_stack(0, ref_frames, 1, x.mbmi_ext);
        Span<MV> pred_mv = stackalloc MV[MAX_MV_REF_CANDIDATES + 1];
        int num_mv_refs = 0;
        pred_mv[num_mv_refs++] = ref_mv.as_mv;
        if (ref_mv.as_int != ref_mv1.as_int)
        {
            pred_mv[num_mv_refs++] = ref_mv1.as_mv;
        }

        Debug.Assert(num_mv_refs <= pred_mv.Length);

        Ptr<byte> src_y_ptr = x.plane[0].src.buf;
        int zero_seen = 0;
        int best_sad = int.MaxValue;
        int max_mv = 0;
        // Get the sad for each candidate reference mv.
        for (int i = 0; i < num_mv_refs; ++i)
        {
            ref MV this_mv = ref pred_mv[i];
            enc_clamp_mv(cpi.common, x.e_mbd, ref this_mv);

            int fp_row = (this_mv.row + 3 + B(this_mv.row >= 0)) >> 3;
            int fp_col = (this_mv.col + 3 + B(this_mv.col >= 0)) >> 3;
            max_mv = AOMMAX(max_mv, AOMMAX(Math.Abs((int)this_mv.row), Math.Abs((int)this_mv.col)) >> 3);

            if (fp_row == 0 && fp_col == 0 && zero_seen != 0) continue;
            zero_seen |= B(fp_row == 0 && fp_col == 0);

            Ptr<byte> ref_y_ptr =
                ref_y_buffer + (ref_y_stride * fp_row + fp_col);
            // Find sad for current vector.
            int this_sad = (int)cpi.ppi.fn_ptr[block_size].sdf(
                src_y_ptr, x.plane[0].src.stride, ref_y_ptr, ref_y_stride);
            // Note if it is the best so far.
            if (this_sad < best_sad)
            {
                best_sad = this_sad;
            }
            if (i == 0)
                x.pred_mv0_sad[ref_frame] = this_sad;
            else if (i == 1)
                x.pred_mv1_sad[ref_frame] = this_sad;
        }

        // Note the index of the mv that worked best in the reference list.
        x.max_mv_context[ref_frame] = (uint)max_mv;
        x.pred_mv_sad[ref_frame] = best_sad;
    }

    // C: `struct buf_2d dst[MAX_MB_PLANE]` (decays to a pointer).
    internal static void av1_setup_pred_block(MACROBLOCKD xd, Ptr<buf_2d> dst,
                                              YV12_BUFFER_CONFIG src,
                                              scale_factors scale,
                                              scale_factors scale_uv,
                                              int num_planes)
    {
        dst[0].buf = src.y_buffer;
        dst[0].stride = src.y_stride;
        dst[1].buf = src.u_buffer;
        dst[2].buf = src.v_buffer;
        dst[1].stride = dst[2].stride = src.uv_stride;

        int mi_row = xd.mi_row;
        int mi_col = xd.mi_col;
        for (int i = 0; i < num_planes; ++i)
        {
            setup_pred_plane(ref dst[i], xd.mi[0].bsize, dst[i].buf,
                             i != 0 ? src.uv_crop_width : src.y_crop_width,
                             i != 0 ? src.uv_crop_height : src.y_crop_height,
                             dst[i].stride, mi_row, mi_col, i != 0 ? scale_uv : scale,
                             xd.plane[i].subsampling_x, xd.plane[i].subsampling_y);
        }
    }

    internal static YV12_BUFFER_CONFIG av1_get_scaled_ref_frame(AV1_COMP cpi,
                                                                int ref_frame)
    {
        Debug.Assert(ref_frame >= LAST_FRAME && ref_frame <= ALTREF_FRAME);
        RefCntBuffer scaled_buf = cpi.scaled_ref_buf[ref_frame - 1];
        RefCntBuffer ref_buf =
            get_ref_frame_buf(cpi.common, ref_frame);
        return (scaled_buf != ref_buf && scaled_buf != null) ? scaled_buf.buf
                                                             : null;
    }

    // [subset] av1_get_switchable_rate: rd mode search only.

    internal static void av1_set_rd_speed_thresholds(AV1_COMP cpi)
    {
        RD_OPT rd = cpi.rd;

        // Set baseline threshold values.
        av1_zero(rd.thresh_mult);

        rd.thresh_mult[THR_NEARESTMV] = 300;
        rd.thresh_mult[THR_NEARESTL2] = 300;
        rd.thresh_mult[THR_NEARESTL3] = 300;
        rd.thresh_mult[THR_NEARESTB] = 300;
        rd.thresh_mult[THR_NEARESTA2] = 300;
        rd.thresh_mult[THR_NEARESTA] = 300;
        rd.thresh_mult[THR_NEARESTG] = 300;

        rd.thresh_mult[THR_NEWMV] = 1000;
        rd.thresh_mult[THR_NEWL2] = 1000;
        rd.thresh_mult[THR_NEWL3] = 1000;
        rd.thresh_mult[THR_NEWB] = 1000;
        rd.thresh_mult[THR_NEWA2] = 1100;
        rd.thresh_mult[THR_NEWA] = 1000;
        rd.thresh_mult[THR_NEWG] = 1000;

        rd.thresh_mult[THR_NEARMV] = 1000;
        rd.thresh_mult[THR_NEARL2] = 1000;
        rd.thresh_mult[THR_NEARL3] = 1000;
        rd.thresh_mult[THR_NEARB] = 1000;
        rd.thresh_mult[THR_NEARA2] = 1000;
        rd.thresh_mult[THR_NEARA] = 1000;
        rd.thresh_mult[THR_NEARG] = 1000;

        rd.thresh_mult[THR_GLOBALMV] = 2200;
        rd.thresh_mult[THR_GLOBALL2] = 2000;
        rd.thresh_mult[THR_GLOBALL3] = 2000;
        rd.thresh_mult[THR_GLOBALB] = 2400;
        rd.thresh_mult[THR_GLOBALA2] = 2000;
        rd.thresh_mult[THR_GLOBALG] = 2000;
        rd.thresh_mult[THR_GLOBALA] = 2400;

        rd.thresh_mult[THR_COMP_NEAREST_NEARESTLA] = 1100;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTL2A] = 1000;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTL3A] = 800;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTGA] = 900;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTLB] = 1000;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTL2B] = 1000;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTL3B] = 1000;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTGB] = 1000;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTLA2] = 1000;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTL2A2] = 1000;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTL3A2] = 1000;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTGA2] = 1000;

        rd.thresh_mult[THR_COMP_NEAREST_NEARESTLL2] = 2000;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTLL3] = 2000;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTLG] = 2000;
        rd.thresh_mult[THR_COMP_NEAREST_NEARESTBA] = 2000;

        rd.thresh_mult[THR_COMP_NEAR_NEARLA] = 1200;
        rd.thresh_mult[THR_COMP_NEAREST_NEWLA] = 1500;
        rd.thresh_mult[THR_COMP_NEW_NEARESTLA] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWLA] = 1530;
        rd.thresh_mult[THR_COMP_NEW_NEARLA] = 1870;
        rd.thresh_mult[THR_COMP_NEW_NEWLA] = 2400;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALLA] = 2750;

        rd.thresh_mult[THR_COMP_NEAR_NEARL2A] = 1200;
        rd.thresh_mult[THR_COMP_NEAREST_NEWL2A] = 1500;
        rd.thresh_mult[THR_COMP_NEW_NEARESTL2A] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWL2A] = 1870;
        rd.thresh_mult[THR_COMP_NEW_NEARL2A] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEWL2A] = 1800;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALL2A] = 2500;

        rd.thresh_mult[THR_COMP_NEAR_NEARL3A] = 1200;
        rd.thresh_mult[THR_COMP_NEAREST_NEWL3A] = 1500;
        rd.thresh_mult[THR_COMP_NEW_NEARESTL3A] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWL3A] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEARL3A] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEWL3A] = 2000;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALL3A] = 3000;

        rd.thresh_mult[THR_COMP_NEAR_NEARGA] = 1320;
        rd.thresh_mult[THR_COMP_NEAREST_NEWGA] = 1500;
        rd.thresh_mult[THR_COMP_NEW_NEARESTGA] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWGA] = 2040;
        rd.thresh_mult[THR_COMP_NEW_NEARGA] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEWGA] = 2000;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALGA] = 2250;

        rd.thresh_mult[THR_COMP_NEAR_NEARLB] = 1200;
        rd.thresh_mult[THR_COMP_NEAREST_NEWLB] = 1500;
        rd.thresh_mult[THR_COMP_NEW_NEARESTLB] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWLB] = 1360;
        rd.thresh_mult[THR_COMP_NEW_NEARLB] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEWLB] = 2400;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALLB] = 2250;

        rd.thresh_mult[THR_COMP_NEAR_NEARL2B] = 1200;
        rd.thresh_mult[THR_COMP_NEAREST_NEWL2B] = 1500;
        rd.thresh_mult[THR_COMP_NEW_NEARESTL2B] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWL2B] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEARL2B] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEWL2B] = 2000;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALL2B] = 2500;

        rd.thresh_mult[THR_COMP_NEAR_NEARL3B] = 1200;
        rd.thresh_mult[THR_COMP_NEAREST_NEWL3B] = 1500;
        rd.thresh_mult[THR_COMP_NEW_NEARESTL3B] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWL3B] = 1870;
        rd.thresh_mult[THR_COMP_NEW_NEARL3B] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEWL3B] = 2000;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALL3B] = 2500;

        rd.thresh_mult[THR_COMP_NEAR_NEARGB] = 1200;
        rd.thresh_mult[THR_COMP_NEAREST_NEWGB] = 1500;
        rd.thresh_mult[THR_COMP_NEW_NEARESTGB] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWGB] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEARGB] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEWGB] = 2000;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALGB] = 2500;

        rd.thresh_mult[THR_COMP_NEAR_NEARLA2] = 1200;
        rd.thresh_mult[THR_COMP_NEAREST_NEWLA2] = 1800;
        rd.thresh_mult[THR_COMP_NEW_NEARESTLA2] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWLA2] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEARLA2] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEWLA2] = 2000;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALLA2] = 2500;

        rd.thresh_mult[THR_COMP_NEAR_NEARL2A2] = 1200;
        rd.thresh_mult[THR_COMP_NEAREST_NEWL2A2] = 1500;
        rd.thresh_mult[THR_COMP_NEW_NEARESTL2A2] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWL2A2] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEARL2A2] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEWL2A2] = 2000;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALL2A2] = 2500;

        rd.thresh_mult[THR_COMP_NEAR_NEARL3A2] = 1440;
        rd.thresh_mult[THR_COMP_NEAREST_NEWL3A2] = 1500;
        rd.thresh_mult[THR_COMP_NEW_NEARESTL3A2] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWL3A2] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEARL3A2] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEWL3A2] = 2000;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALL3A2] = 2500;

        rd.thresh_mult[THR_COMP_NEAR_NEARGA2] = 1200;
        rd.thresh_mult[THR_COMP_NEAREST_NEWGA2] = 1500;
        rd.thresh_mult[THR_COMP_NEW_NEARESTGA2] = 1500;
        rd.thresh_mult[THR_COMP_NEAR_NEWGA2] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEARGA2] = 1700;
        rd.thresh_mult[THR_COMP_NEW_NEWGA2] = 2000;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALGA2] = 2750;

        rd.thresh_mult[THR_COMP_NEAR_NEARLL2] = 1600;
        rd.thresh_mult[THR_COMP_NEAREST_NEWLL2] = 2000;
        rd.thresh_mult[THR_COMP_NEW_NEARESTLL2] = 2000;
        rd.thresh_mult[THR_COMP_NEAR_NEWLL2] = 2640;
        rd.thresh_mult[THR_COMP_NEW_NEARLL2] = 2200;
        rd.thresh_mult[THR_COMP_NEW_NEWLL2] = 2400;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALLL2] = 3200;

        rd.thresh_mult[THR_COMP_NEAR_NEARLL3] = 1600;
        rd.thresh_mult[THR_COMP_NEAREST_NEWLL3] = 2000;
        rd.thresh_mult[THR_COMP_NEW_NEARESTLL3] = 1800;
        rd.thresh_mult[THR_COMP_NEAR_NEWLL3] = 2200;
        rd.thresh_mult[THR_COMP_NEW_NEARLL3] = 2200;
        rd.thresh_mult[THR_COMP_NEW_NEWLL3] = 2400;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALLL3] = 3200;

        rd.thresh_mult[THR_COMP_NEAR_NEARLG] = 1760;
        rd.thresh_mult[THR_COMP_NEAREST_NEWLG] = 2400;
        rd.thresh_mult[THR_COMP_NEW_NEARESTLG] = 2000;
        rd.thresh_mult[THR_COMP_NEAR_NEWLG] = 1760;
        rd.thresh_mult[THR_COMP_NEW_NEARLG] = 2640;
        rd.thresh_mult[THR_COMP_NEW_NEWLG] = 2400;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALLG] = 3200;

        rd.thresh_mult[THR_COMP_NEAR_NEARBA] = 1600;
        rd.thresh_mult[THR_COMP_NEAREST_NEWBA] = 2000;
        rd.thresh_mult[THR_COMP_NEW_NEARESTBA] = 2000;
        rd.thresh_mult[THR_COMP_NEAR_NEWBA] = 2200;
        rd.thresh_mult[THR_COMP_NEW_NEARBA] = 1980;
        rd.thresh_mult[THR_COMP_NEW_NEWBA] = 2640;
        rd.thresh_mult[THR_COMP_GLOBAL_GLOBALBA] = 3200;

        rd.thresh_mult[THR_DC] = 1000;
        rd.thresh_mult[THR_PAETH] = 1000;
        rd.thresh_mult[THR_SMOOTH] = 2200;
        rd.thresh_mult[THR_SMOOTH_V] = 2000;
        rd.thresh_mult[THR_SMOOTH_H] = 2000;
        rd.thresh_mult[THR_H_PRED] = 2000;
        rd.thresh_mult[THR_V_PRED] = 1800;
        rd.thresh_mult[THR_D135_PRED] = 2500;
        rd.thresh_mult[THR_D203_PRED] = 2000;
        rd.thresh_mult[THR_D157_PRED] = 2500;
        rd.thresh_mult[THR_D67_PRED] = 2000;
        rd.thresh_mult[THR_D113_PRED] = 2500;
        rd.thresh_mult[THR_D45_PRED] = 2500;
    }

    // [subset] update_thr_fact, av1_update_rd_thresh_fact: rd mode search only.

    internal static int av1_get_intra_cost_penalty(int qindex, int qdelta,
                                                   aom_bit_depth_t bit_depth)
    {
        int q = av1_dc_quant_QTX(qindex, qdelta, bit_depth);
        switch (bit_depth)
        {
            case AOM_BITS_8: return 20 * q;
            case AOM_BITS_10: return 5 * q;
            case AOM_BITS_12: return ROUND_POWER_OF_TWO(5 * q, 2);
            default:
                Debug.Assert(false, "bit_depth should be AOM_BITS_8, AOM_BITS_10 or AOM_BITS_12");
                return -1;
        }
    }
}
