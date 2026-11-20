// Port of libaom av1/encoder/rdopt_utils.h
global using static Av1rt.Aom.rdopt_utils;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class rdopt_utils
{
    internal const int MAX_REF_MV_SEARCH = 3;
    internal const int MAX_TX_RD_GATE_LEVEL = 5;

    // [subset] MODE_DEFINITION / av1_mode_defs, winner_mode_count_allowed and the rd-path
    // helpers (restore_dst_buf, swap_dst_buf, get_rd_thresh_from_best_rd,
    // get_prediction_mode_idx, inter_mode_data_block_idx, get_visible_dimensions,
    // bsize_to_num_blk, check_txfm_eval, bypass_winner_mode_processing,
    // is_winner_mode_processing_enabled, get_global_mv_mode_bias, increase_motion_mode_rate,
    // store_cfl_required_rdo, store_winner_mode_stats, get_txfm_rd_gate_level, ...) are only
    // used by the rd mode search (rdopt.c), which is outside the subset.

    // C: `static TX_MODE select_tx_mode(...)` (not inline).
    internal static TX_MODE select_tx_mode(
        AV1_COMMON cm, TX_SIZE_SEARCH_METHOD tx_size_search_method)
    {
        if (cm.features.coded_lossless) return ONLY_4X4;
        if (tx_size_search_method == USE_LARGESTALL)
        {
            return TX_MODE_LARGEST;
        }
        else
        {
            Debug.Assert(tx_size_search_method == USE_FULL_RD ||
                         tx_size_search_method == USE_FAST_RD);
            return TX_MODE_SELECT;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_tx_size_search_method(
        AV1_COMMON cm, WinnerModeParams winner_mode_params,
        TxfmSearchParams txfm_params, int enable_winner_mode_for_tx_size_srch,
        int is_winner_mode)
    {
        // Populate transform size search method/transform mode appropriately
        txfm_params.tx_size_search_method =
            winner_mode_params.tx_size_search_methods[DEFAULT_EVAL];
        if (enable_winner_mode_for_tx_size_srch != 0)
        {
            if (is_winner_mode != 0)
                txfm_params.tx_size_search_method =
                    winner_mode_params.tx_size_search_methods[WINNER_MODE_EVAL];
            else
                txfm_params.tx_size_search_method =
                    winner_mode_params.tx_size_search_methods[MODE_EVAL];
        }
        txfm_params.tx_mode_search_type =
            select_tx_mode(cm, txfm_params.tx_size_search_method);
    }

    private static readonly int[][] prune_mode = {
        new int[] { TX_TYPE_PRUNE_3, TX_TYPE_PRUNE_0 },
        new int[] { TX_TYPE_PRUNE_4, TX_TYPE_PRUNE_0 },
        new int[] { TX_TYPE_PRUNE_5, TX_TYPE_PRUNE_2 },
        new int[] { TX_TYPE_PRUNE_5, TX_TYPE_PRUNE_3 },
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_tx_type_prune(SPEED_FEATURES sf,
                                           TxfmSearchParams txfm_params,
                                           int winner_mode_tx_type_pruning,
                                           int is_winner_mode)
    {
        // Populate prune transform mode appropriately
        txfm_params.prune_2d_txfm_mode = sf.tx_sf.tx_type_search.prune_2d_txfm_mode;
        if (winner_mode_tx_type_pruning == 0) return;

        // C: function-local `const int prune_mode[4][2]` (hoisted to a static table).
        txfm_params.prune_2d_txfm_mode =
            prune_mode[winner_mode_tx_type_pruning - 1][is_winner_mode];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_tx_domain_dist_params(
        WinnerModeParams winner_mode_params, TxfmSearchParams txfm_params,
        int enable_winner_mode_for_tx_domain_dist, int is_winner_mode)
    {
        if (txfm_params.use_qm_dist_metric != 0)
        {
            // QM-weighted PSNR is computed in transform space, so we need to forcibly
            // enable the use of tx domain distortion.
            txfm_params.use_transform_domain_distortion = 1;
            txfm_params.tx_domain_dist_threshold = 0;
            return;
        }

        if (enable_winner_mode_for_tx_domain_dist == 0)
        {
            txfm_params.use_transform_domain_distortion =
                winner_mode_params.use_transform_domain_distortion[DEFAULT_EVAL];
            txfm_params.tx_domain_dist_threshold =
                winner_mode_params.tx_domain_dist_threshold[DEFAULT_EVAL];
            return;
        }

        if (is_winner_mode != 0)
        {
            txfm_params.use_transform_domain_distortion =
                winner_mode_params.use_transform_domain_distortion[WINNER_MODE_EVAL];
            txfm_params.tx_domain_dist_threshold =
                winner_mode_params.tx_domain_dist_threshold[WINNER_MODE_EVAL];
        }
        else
        {
            txfm_params.use_transform_domain_distortion =
                winner_mode_params.use_transform_domain_distortion[MODE_EVAL];
            txfm_params.tx_domain_dist_threshold =
                winner_mode_params.tx_domain_dist_threshold[MODE_EVAL];
        }
    }

    // This function sets mode parameters for different mode evaluation stages
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_mode_eval_params(AV1_COMP cpi,
                                              MACROBLOCK x,
                                              MODE_EVAL_TYPE mode_eval_type)
    {
        AV1_COMMON cm = cpi.common;
        SPEED_FEATURES sf = cpi.sf;
        WinnerModeParams winner_mode_params = cpi.winner_mode_params;
        TxfmSearchParams txfm_params = x.txfm_search_params;

        txfm_params.use_qm_dist_metric =
            B(cpi.oxcf.tune_cfg.dist_metric == AOM_DIST_METRIC_QM_PSNR);

        switch (mode_eval_type)
        {
            case DEFAULT_EVAL:
                txfm_params.default_inter_tx_type_prob_thresh = int.MaxValue;
                txfm_params.use_default_intra_tx_type = 0;
                txfm_params.use_derived_intra_tx_type_set = 0;
                txfm_params.skip_txfm_level =
                    winner_mode_params.skip_txfm_level[DEFAULT_EVAL];
                txfm_params.predict_dc_level =
                    winner_mode_params.predict_dc_level[DEFAULT_EVAL];
                // Set default transform domain distortion type
                set_tx_domain_dist_params(winner_mode_params, txfm_params, 0, 0);

                // Get default threshold for R-D optimization of coefficients
                get_rd_opt_coeff_thresh(winner_mode_params.coeff_opt_thresholds,
                                        txfm_params, 0, 0);

                // Set default transform size search method
                set_tx_size_search_method(cm, winner_mode_params, txfm_params, 0, 0);
                // Set default transform type prune
                set_tx_type_prune(sf, txfm_params, 0, 0);
                break;
            case MODE_EVAL:
                txfm_params.use_default_intra_tx_type =
                    B(cpi.sf.tx_sf.tx_type_search.fast_intra_tx_type_search == 2 ||
                      cpi.oxcf.txfm_cfg.use_intra_default_tx_only);
                txfm_params.use_derived_intra_tx_type_set =
                    B(cpi.sf.tx_sf.tx_type_search.fast_intra_tx_type_search == 1);
                txfm_params.default_inter_tx_type_prob_thresh =
                    cpi.sf.tx_sf.tx_type_search.fast_inter_tx_type_prob_thresh;
                txfm_params.skip_txfm_level =
                    winner_mode_params.skip_txfm_level[MODE_EVAL];
                txfm_params.predict_dc_level =
                    winner_mode_params.predict_dc_level[MODE_EVAL];
                // Set transform domain distortion type for mode evaluation
                set_tx_domain_dist_params(
                    winner_mode_params, txfm_params,
                    sf.winner_mode_sf.enable_winner_mode_for_use_tx_domain_dist, 0);

                // Get threshold for R-D optimization of coefficients during mode
                // evaluation
                get_rd_opt_coeff_thresh(
                    winner_mode_params.coeff_opt_thresholds, txfm_params,
                    sf.winner_mode_sf.enable_winner_mode_for_coeff_opt, 0);

                // Set the transform size search method for mode evaluation
                set_tx_size_search_method(
                    cm, winner_mode_params, txfm_params,
                    sf.winner_mode_sf.enable_winner_mode_for_tx_size_srch, 0);
                // Set transform type prune for mode evaluation
                set_tx_type_prune(sf, txfm_params,
                                  sf.tx_sf.tx_type_search.winner_mode_tx_type_pruning,
                                  0);
                break;
            case WINNER_MODE_EVAL:
                txfm_params.default_inter_tx_type_prob_thresh = int.MaxValue;
                txfm_params.use_default_intra_tx_type = 0;
                txfm_params.use_derived_intra_tx_type_set = 0;
                txfm_params.skip_txfm_level =
                    winner_mode_params.skip_txfm_level[WINNER_MODE_EVAL];
                txfm_params.predict_dc_level =
                    winner_mode_params.predict_dc_level[WINNER_MODE_EVAL];

                // Set transform domain distortion type for winner mode evaluation
                set_tx_domain_dist_params(
                    winner_mode_params, txfm_params,
                    sf.winner_mode_sf.enable_winner_mode_for_use_tx_domain_dist, 1);

                // Get threshold for R-D optimization of coefficients for winner mode
                // evaluation
                get_rd_opt_coeff_thresh(
                    winner_mode_params.coeff_opt_thresholds, txfm_params,
                    sf.winner_mode_sf.enable_winner_mode_for_coeff_opt, 1);

                // Set the transform size search method for winner mode evaluation
                set_tx_size_search_method(
                    cm, winner_mode_params, txfm_params,
                    sf.winner_mode_sf.enable_winner_mode_for_tx_size_srch, 1);
                // Set default transform type prune mode for winner mode evaluation
                set_tx_type_prune(sf, txfm_params,
                                  sf.tx_sf.tx_type_search.winner_mode_tx_type_pruning,
                                  1);
                break;
            default: Debug.Assert(false); break;
        }

        // Rd record collected at a specific mode evaluation stage can not be used
        // across other evaluation stages as the transform parameters are different.
        // Hence, reset mb rd record whenever mode evaluation stage type changes.
        if (txfm_params.mode_eval_type != mode_eval_type)
        {
            // [subset] reset_mb_rd_record(x->txfm_search_info.mb_rd_record): the mb rd
            // record (sf.rd_sf.use_mb_rd_hash) is dropped; it is NULL in the subset, where
            // reset_mb_rd_record() does nothing.
        }

        txfm_params.mode_eval_type = mode_eval_type;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_mode_intra(PREDICTION_MODE mode)
    {
        return B(mode < INTRA_MODE_END);
    }

    // This function will copy usable ref_mv_stack[ref_frame][4] and
    // weight[ref_frame][4] information from ref_mv_stack[ref_frame][8] and
    // weight[ref_frame][8].
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_copy_usable_ref_mv_stack_and_weight(
        MACROBLOCKD xd, MB_MODE_INFO_EXT mbmi_ext,
        MV_REFERENCE_FRAME ref_frame)
    {
        memcpy<ushort>(mbmi_ext.weight[ref_frame], xd.weight[ref_frame],
                       USABLE_REF_MV_STACK_SIZE);
        memcpy<CANDIDATE_MV>(mbmi_ext.ref_mv_stack[ref_frame], xd.ref_mv_stack[ref_frame],
                             USABLE_REF_MV_STACK_SIZE);
    }
}
