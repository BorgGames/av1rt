// Port of libaom av1/encoder/nonrd_pickmode.c
// [port] nonrd_pickmode.c has no header of its own (its entry points are declared in
// rdopt.h), so the class and its global using live here.
global using static Av1rt.Aom.nonrd_pickmode;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class nonrd_pickmode
{
    // PORT-NOTE: `NONE` in C (best_pickmode->best_second_ref_frame = NONE) is not NONE_FRAME but
    // the ACTIVATION enumerator `enum { NONE, RELU, SOFTSIGN, SIGMOID }` from config/av1_rtcd.h,
    // i.e. 0 (== INTRA_FRAME). Kept as such for bit-exactness.
    private const int NONE = 0;

    // C: `static const double early_term_thresh[4][4]` / `early_term_thresh_newmv_nearestmv[4]`
    // (function-local tables, hoisted).
    private static readonly double[][] early_term_thresh = {
        new[] { 0.65, 0.65, 0.65, 0.7 },
        new[] { 0.6, 0.65, 0.85, 0.9 },
        new[] { 0.5, 0.5, 0.55, 0.6 },
        new[] { 0.6, 0.75, 0.85, 0.85 },
    };
    private static readonly double[] early_term_thresh_newmv_nearestmv = { 0.3, 0.3, 0.3, 0.3 };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int early_term_inter_search_with_sse(int early_term_idx,
                                                        BLOCK_SIZE bsize,
                                                        long this_sse,
                                                        long best_sse,
                                                        PREDICTION_MODE this_mode)
    {
        // Aggressiveness to terminate inter mode search early is adjusted based on
        // speed and block size.
        int size_group = size_group_lookup[bsize];
        Debug.Assert(size_group < 4);
        Debug.Assert((early_term_idx > 0) && (early_term_idx < EARLY_TERM_INDICES));
        double threshold =
            ((early_term_idx == EARLY_TERM_IDX_4) &&
             (this_mode == NEWMV || this_mode == NEARESTMV))
                ? early_term_thresh_newmv_nearestmv[size_group]
                : early_term_thresh[early_term_idx - 1][size_group];

        // Terminate inter mode search early based on best sse so far.
        if ((early_term_idx > 0) && (threshold * this_sse > best_sse))
        {
            return 1;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void init_best_pickmode(BEST_PICKMODE bp)
    {
        bp.best_sse = long.MaxValue;
        bp.best_mode = NEARESTMV;
        bp.best_ref_frame = LAST_FRAME;
        bp.best_second_ref_frame = NONE_FRAME;
        bp.best_tx_size = TX_8X8;
        bp.tx_type = DCT_DCT;
        bp.best_pred_filter = av1_broadcast_interp_filter(EIGHTTAP_REGULAR);
        bp.best_mode_skip_txfm = 0;
        bp.best_mode_initial_skip_flag = 0;
        bp.best_pred = null;
        bp.best_motion_mode = SIMPLE_TRANSLATION;
        bp.num_proj_ref = 0;
        bp.wm_params = default;  // av1_zero(bp->wm_params)
        bp.pmi = default;        // av1_zero(bp->pmi)
    }

    // Copy best inter mode parameters to best_pickmode
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void update_search_state_nonrd(
        InterModeSearchStateNonrd search_state, MB_MODE_INFO mi,
        TxfmSearchInfo txfm_info, in RD_STATS nonskip_rdc,
        PREDICTION_MODE this_best_mode, long sse_y)
    {
        _ = txfm_info;
        BEST_PICKMODE best_pickmode = search_state.best_pickmode;

        best_pickmode.best_sse = sse_y;
        best_pickmode.best_mode = this_best_mode;
        best_pickmode.best_motion_mode = mi.motion_mode;
        best_pickmode.wm_params = mi.wm_params;
        best_pickmode.num_proj_ref = mi.num_proj_ref;
        best_pickmode.best_pred_filter = mi.interp_filters;
        best_pickmode.best_tx_size = mi.tx_size;
        best_pickmode.best_ref_frame = mi.ref_frame[0];
        best_pickmode.best_second_ref_frame = mi.ref_frame[1];
        best_pickmode.best_mode_skip_txfm = search_state.this_rdc.skip_txfm;
        best_pickmode.best_mode_initial_skip_flag =
            (byte)B(nonskip_rdc.rate == int.MaxValue && search_state.this_rdc.skip_txfm != 0);
    }

    // C: `const int th_vals[2][3]` (function-local table, hoisted).
    private static readonly int[][] th_vals = { new[] { 4, 8, 10 }, new[] { 4, 6, 8 } };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int subpel_select(AV1_COMP cpi, MACROBLOCK x, BLOCK_SIZE bsize,
                                     in int_mv mv, MV ref_mv, FULLPEL_MV start_mv,
                                     bool fullpel_performed_well)
    {
        int frame_lowmotion = cpi.rc.avg_frame_low_motion;
        int reduce_mv_pel_precision_highmotion =
            cpi.sf.rt_sf.reduce_mv_pel_precision_highmotion;

        // Reduce MV precision for higher int MV value & frame-level motion
        if (reduce_mv_pel_precision_highmotion >= 3)
        {
            int mv_thresh = 4;
            int is_low_resoln =
                B(cpi.common.width * cpi.common.height <= 320 * 240);
            mv_thresh = (bsize > BLOCK_32X32) ? 2 : (bsize > BLOCK_16X16) ? 4 : 6;
            if (frame_lowmotion > 0 && frame_lowmotion < 40) mv_thresh = 12;
            mv_thresh = (is_low_resoln != 0) ? mv_thresh >> 1 : mv_thresh;
            if (Math.Abs((int)mv.as_fullmv.row) >= mv_thresh ||
                Math.Abs((int)mv.as_fullmv.col) >= mv_thresh)
                return HALF_PEL;
        }
        else if (reduce_mv_pel_precision_highmotion >= 1)
        {
            int mv_thresh;
            int th_idx = reduce_mv_pel_precision_highmotion - 1;
            Debug.Assert(th_idx >= 0 && th_idx < 2);
            if (frame_lowmotion > 0 && frame_lowmotion < 40)
                mv_thresh = 12;
            else
                mv_thresh = (bsize >= BLOCK_32X32) ? th_vals[th_idx][0]
                            : (bsize >= BLOCK_16X16) ? th_vals[th_idx][1]
                                                     : th_vals[th_idx][2];
            if (Math.Abs((int)mv.as_fullmv.row) >= (mv_thresh << 1) ||
                Math.Abs((int)mv.as_fullmv.col) >= (mv_thresh << 1))
                return FULL_PEL;
            else if (Math.Abs((int)mv.as_fullmv.row) >= mv_thresh ||
                     Math.Abs((int)mv.as_fullmv.col) >= mv_thresh)
                return HALF_PEL;
        }
        // Reduce MV precision for relatively static (e.g. background), low-complex
        // large areas
        if (cpi.sf.rt_sf.reduce_mv_pel_precision_lowcomplex >= 2)
        {
            int qband = x.qindex >> (QINDEX_BITS - 2);
            Debug.Assert(qband < 4);
            if (x.content_state_sb.source_sad_nonrd <= kVeryLowSad &&
                bsize > BLOCK_16X16 && qband != 0)
            {
                if (x.source_variance < 500)
                    return FULL_PEL;
                else if (x.source_variance < 5000)
                    return HALF_PEL;
            }
        }
        else if (cpi.sf.rt_sf.reduce_mv_pel_precision_lowcomplex >= 1)
        {
            if (fullpel_performed_well && ref_mv.row == 0 && ref_mv.col == 0 &&
                start_mv.row == 0 && start_mv.col == 0)
                return HALF_PEL;
        }
        return cpi.sf.mv_sf.subpel_force_stop;
    }

    private static bool use_aggressive_subpel_search_method(MACROBLOCK x,
                                                            bool use_adaptive_subpel_search,
                                                            bool fullpel_performed_well)
    {
        if (!use_adaptive_subpel_search) return false;
        int qband = x.qindex >> (QINDEX_BITS - 2);
        Debug.Assert(qband < 4);
        if ((qband > 0) && (fullpel_performed_well ||
                            (x.content_state_sb.source_sad_nonrd <= kLowSad) ||
                            (x.source_variance < 100)))
            return true;
        return false;
    }

    // Runs Motion Estimation for a specific block and specific ref frame.
    //
    // Finds the best Motion Vector by running Motion Estimation for a specific
    // block and a specific reference frame. Exits early if RDCost of Full Pel part
    // exceeds best RD Cost fund so far
    // cpi                      Top-level encoder structure
    // x                        Pointer to structure holding all the
    //                          data for the current macroblock
    // bsize                    Current block size
    // tmp_mv                   Pointer to best found New MV
    // rate_mv                  Pointer to Rate of the best new MV
    // best_rd_sofar            RD Cost of the best mode found so far
    // use_base_mv              Flag, indicating that tmp_mv holds
    //                          specific MV to start the search with
    //
    // Returns 0 if ME was terminated after Full Pel Search because too
    // high RD Cost. Otherwise returns 1. Best New MV is placed into tmp_mv.
    // Rate estimation for this vector is placed to rate_mv
    private static int combined_motion_search(AV1_COMP cpi, MACROBLOCK x,
                                              BLOCK_SIZE bsize, ref int_mv tmp_mv,
                                              ref int rate_mv, long best_rd_sofar,
                                              int use_base_mv)
    {
        MACROBLOCKD xd = x.e_mbd;
        AV1_COMMON cm = cpi.common;
        SPEED_FEATURES sf = cpi.sf;
        MB_MODE_INFO mi = xd.mi[0];
        int step_param = (sf.rt_sf.fullpel_search_step_param != 0)
                             ? sf.rt_sf.fullpel_search_step_param
                             : cpi.mv_search_params.mv_step_param;
        FULLPEL_MV start_mv;
        int @ref = mi.ref_frame[0];
        MV ref_mv = av1_get_ref_mv(x, mi.ref_mv_idx).as_mv;
        MV center_mv;
        int dis = 0;
        int rv = 0;
        // int cost_list[5] (per-thread scratch; written by the search when used)
        int[] cost_list = x.nonrd_scratch.cost_list;
        int search_subpel = 1;

        if (av1_is_scaled(get_ref_scale_factors_const(cm, @ref)) != 0)
        {
            YV12_BUFFER_CONFIG scaled_ref = av1_get_scaled_ref_frame(cpi, @ref);
            Debug.Assert(scaled_ref != null);
            Debug.Assert(scaled_ref.y_crop_width == cm.width &&
                         scaled_ref.y_crop_height == cm.height);
        }

        start_mv = get_fullmv_from_mv(in ref_mv);

        if (use_base_mv == 0)
            center_mv = ref_mv;
        else
            center_mv = tmp_mv.as_mv;

        SEARCH_METHODS search_method =
            av1_get_default_mv_search_method(x, cpi.sf.mv_sf, bsize);
        search_site_config[] src_search_sites =
            av1_get_search_site_config(cpi, x, search_method);
        FULLPEL_MOTION_SEARCH_PARAMS full_ms_params = default;
        FULLPEL_MV_STATS best_mv_stats = default;
        av1_make_default_fullpel_ms_params(ref full_ms_params, cpi, x, bsize, in center_mv,
                                           start_mv, src_search_sites, search_method,
                                           /*fine_search_interval=*/0);

        int full_var_rd = av1_full_pixel_search(
            start_mv, in full_ms_params, step_param, cond_cost_list(cpi, cost_list),
            ref tmp_mv.as_fullmv, ref best_mv_stats, Ptr<FULLPEL_MV>.Null);

        // calculate the bit cost on motion vector
        MV mvp_full = get_mv_from_fullmv(in tmp_mv.as_fullmv);

        rate_mv = av1_mv_bit_cost(in mvp_full, in ref_mv, x.mv_costs.nmv_joint_cost,
                                  x.mv_costs.mv_cost_stack, MV_COST_WEIGHT);

        // TODO(kyslov) Account for Rate Mode!
        rv = B(!(RDCOST(x.rdmult, rate_mv, 0) > best_rd_sofar));

        if (rv != 0 && search_subpel != 0)
        {
            SUBPEL_MOTION_SEARCH_PARAMS ms_params = default;
            av1_make_default_subpel_ms_params(ref ms_params, cpi, x, bsize, in ref_mv,
                                              cost_list);
            bool fullpel_performed_well =
                (bsize == BLOCK_64X64 && (long)full_var_rd * 40 < 62267 * 7) ||
                (bsize == BLOCK_32X32 && (long)full_var_rd * 8 < 42380) ||
                (bsize == BLOCK_16X16 && (long)full_var_rd * 8 < 10127);
            if (sf.rt_sf.reduce_mv_pel_precision_highmotion != 0 ||
                sf.rt_sf.reduce_mv_pel_precision_lowcomplex != 0)
                ms_params.forced_stop = subpel_select(cpi, x, bsize, in tmp_mv, ref_mv,
                                                      start_mv, fullpel_performed_well);

            MV subpel_start_mv = get_mv_from_fullmv(in tmp_mv.as_fullmv);
            Debug.Assert(av1_is_subpelmv_in_range(in ms_params.mv_limits, subpel_start_mv) != 0);
            // adaptively downgrade subpel search method based on block properties
            if (use_aggressive_subpel_search_method(
                    x, sf.rt_sf.use_adaptive_subpel_search, fullpel_performed_well))
                av1_find_best_sub_pixel_tree_pruned_more(
                    xd, cm, in ms_params, subpel_start_mv, best_mv_stats, ref tmp_mv.as_mv,
                    ref dis, ref x.pred_sse[@ref], Ptr<int_mv>.Null);
            else
                cpi.mv_search_params.find_fractional_mv_step(
                    xd, cm, in ms_params, subpel_start_mv, best_mv_stats, ref tmp_mv.as_mv,
                    ref dis, ref x.pred_sse[@ref], Ptr<int_mv>.Null);
            rate_mv =
                av1_mv_bit_cost(in tmp_mv.as_mv, in ref_mv, x.mv_costs.nmv_joint_cost,
                                x.mv_costs.mv_cost_stack, MV_COST_WEIGHT);
        }
        // The final MV can not be equal to the reference MV as this will trigger an
        // assert later. This can happen if both NEAREST and NEAR modes were skipped.
        rv = B(tmp_mv.as_mv.col != ref_mv.col || tmp_mv.as_mv.row != ref_mv.row);
        return rv;
    }

    // Searches for the best New Motion Vector.
    //
    // Finds the best Motion Vector by doing Motion Estimation. Uses reduced
    // complexity ME for non-LAST frames or calls combined_motion_search
    // for LAST reference frame
    // cpi                      Top-level encoder structure
    // x                        Pointer to structure holding all the
    //                          data for the current macroblock
    // frame_mv                 Array that holds MVs for all modes
    //                          and ref frames
    // ref_frame                Reference frame for which to find
    //                          the best New MVs
    // gf_temporal_ref          Flag, indicating temporal reference
    //                          for GOLDEN frame
    // bsize                    Current block size
    // mi_row                   Row index in 4x4 units
    // mi_col                   Column index in 4x4 units
    // rate_mv                  Pointer to Rate of the best new MV
    // best_rdc                 Pointer to the RD Cost for the best
    //                          mode found so far
    //
    // Returns -1 if the search was not done, otherwise returns 0.
    // Best New MV is placed into frame_mv array, Rate estimation for this
    // vector is placed to rate_mv
    private static int search_new_mv(AV1_COMP cpi, MACROBLOCK x,
                                     int_mv[][] frame_mv /* [][REF_FRAMES] */,
                                     MV_REFERENCE_FRAME ref_frame, int gf_temporal_ref,
                                     BLOCK_SIZE bsize, int mi_row, int mi_col, ref int rate_mv,
                                     in RD_STATS best_rdc)
    {
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mi = xd.mi[0];
        AV1_COMMON cm = cpi.common;
        ref int_mv this_ref_frm_newmv = ref frame_mv[NEWMV][ref_frame];
        uint y_sad_zero = 0;
        if (ref_frame > LAST_FRAME && cpi.oxcf.rc_cfg.mode == AOM_CBR &&
            (cpi.ref_frame_flags & AOM_LAST_FLAG) != 0 && gf_temporal_ref != 0 &&
            is_one_pass_rt_lag_params(cpi) == 0)
        {
            int tmp_sad;
            int dis = 0;

            if (bsize < BLOCK_16X16) return -1;

            int me_search_size_col = block_size_wide[bsize] >> 1;
            int me_search_size_row = block_size_high[bsize] >> 1;
            MV ref_mv = av1_get_ref_mv(x, 0).as_mv;
            tmp_sad = (int)av1_int_pro_motion_estimation(
                cpi, x, bsize, mi_row, mi_col, in ref_mv, ref y_sad_zero, me_search_size_col,
                me_search_size_row, 0, 0);

            if (tmp_sad > x.pred_mv_sad[LAST_FRAME]) return -1;

            this_ref_frm_newmv.as_int = mi.mv[0].as_int;
            int_mv best_mv = mi.mv[0];
            best_mv.as_mv.row >>= 3;
            best_mv.as_mv.col >>= 3;
            this_ref_frm_newmv.as_mv.row >>= 3;
            this_ref_frm_newmv.as_mv.col >>= 3;

            SUBPEL_MOTION_SEARCH_PARAMS ms_params = default;
            av1_make_default_subpel_ms_params(ref ms_params, cpi, x, bsize, in ref_mv,
                                              Ptr<int>.Null);
            if (cpi.sf.rt_sf.reduce_mv_pel_precision_highmotion != 0 ||
                cpi.sf.rt_sf.reduce_mv_pel_precision_lowcomplex != 0)
            {
                // [port] C names this inner local `start_mv` too (it shadows nothing in C, but
                // C# rejects the clash with the `MV start_mv` declared below).
                FULLPEL_MV start_mv_fp = default;  // { .row = 0, .col = 0 }
                ms_params.forced_stop =
                    subpel_select(cpi, x, bsize, in best_mv, ref_mv, start_mv_fp, false);
            }
            MV start_mv = get_mv_from_fullmv(in best_mv.as_fullmv);
            Debug.Assert(av1_is_subpelmv_in_range(in ms_params.mv_limits, start_mv) != 0);
            cpi.mv_search_params.find_fractional_mv_step(
                xd, cm, in ms_params, start_mv, null, ref best_mv.as_mv, ref dis,
                ref x.pred_sse[ref_frame], Ptr<int_mv>.Null);
            this_ref_frm_newmv.as_int = best_mv.as_int;

            // When NEWMV is same as ref_mv from the drl, it is preferred to code the
            // MV as NEARESTMV or NEARMV. In this case, NEWMV needs to be skipped to
            // avoid an assert failure at a later stage. The scenario can occur if
            // NEARESTMV was not evaluated for ALTREF.
            if (this_ref_frm_newmv.as_mv.col == ref_mv.col &&
                this_ref_frm_newmv.as_mv.row == ref_mv.row)
                return -1;

            rate_mv = av1_mv_bit_cost(in this_ref_frm_newmv.as_mv, in ref_mv,
                                      x.mv_costs.nmv_joint_cost,
                                      x.mv_costs.mv_cost_stack, MV_COST_WEIGHT);
        }
        else if (combined_motion_search(cpi, x, bsize, ref frame_mv[NEWMV][ref_frame],
                                        ref rate_mv, best_rdc.rdcost, 0) == 0)
        {
            return -1;
        }

        return 0;
    }

    private static void estimate_single_ref_frame_costs(AV1_COMMON cm,
                                                        MACROBLOCKD xd,
                                                        ModeCosts mode_costs,
                                                        int segment_id, BLOCK_SIZE bsize,
                                                        uint[] ref_costs_single,
                                                        int is_one_pass_rt_lag)
    {
        int seg_ref_active =
            segfeature_active(cm.seg, (byte)segment_id, SEG_LVL_REF_FRAME);
        if (seg_ref_active != 0)
        {
            Array.Clear(ref_costs_single, 0, REF_FRAMES);
        }
        else
        {
            int intra_inter_ctx = av1_get_intra_inter_context(xd);
            ref_costs_single[INTRA_FRAME] =
                (uint)mode_costs.intra_inter_cost[intra_inter_ctx][0];
            uint base_cost = (uint)mode_costs.intra_inter_cost[intra_inter_ctx][1];
            if (cm.current_frame.reference_mode == REFERENCE_MODE_SELECT &&
                is_comp_ref_allowed(bsize) != 0)
            {
                int comp_ref_type_ctx = av1_get_comp_reference_type_context(xd);
                base_cost += (uint)mode_costs.comp_ref_type_cost[comp_ref_type_ctx][1];
            }
            ref_costs_single[LAST_FRAME] = base_cost;
            ref_costs_single[GOLDEN_FRAME] = base_cost;
            ref_costs_single[ALTREF_FRAME] = base_cost;
            // add cost for last, golden, altref
            ref_costs_single[LAST_FRAME] += (uint)mode_costs.single_ref_cost[0][0][0];
            ref_costs_single[GOLDEN_FRAME] += (uint)mode_costs.single_ref_cost[0][0][1];
            ref_costs_single[GOLDEN_FRAME] += (uint)mode_costs.single_ref_cost[0][1][0];
            ref_costs_single[ALTREF_FRAME] += (uint)mode_costs.single_ref_cost[0][0][1];
            ref_costs_single[ALTREF_FRAME] += (uint)mode_costs.single_ref_cost[0][2][0];
            if (is_one_pass_rt_lag != 0)
            {
                ref_costs_single[ALTREF2_FRAME] = base_cost;
                ref_costs_single[BWDREF_FRAME] = base_cost;
                ref_costs_single[LAST2_FRAME] = base_cost;
                ref_costs_single[LAST3_FRAME] = base_cost;
                ref_costs_single[LAST2_FRAME] += (uint)mode_costs.single_ref_cost[0][0][0];
                ref_costs_single[LAST3_FRAME] += (uint)mode_costs.single_ref_cost[0][0][0];
                ref_costs_single[BWDREF_FRAME] += (uint)mode_costs.single_ref_cost[0][0][1];
                ref_costs_single[BWDREF_FRAME] += (uint)mode_costs.single_ref_cost[0][1][0];
                ref_costs_single[ALTREF2_FRAME] += (uint)mode_costs.single_ref_cost[0][0][1];
                ref_costs_single[ALTREF2_FRAME] += (uint)mode_costs.single_ref_cost[0][2][0];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_force_skip_flag(AV1_COMP cpi,
                                            MACROBLOCK x, uint sse,
                                            ref int force_skip)
    {
        if (x.txfm_search_params.tx_mode_search_type == TX_MODE_SELECT &&
            cpi.sf.rt_sf.tx_size_level_based_on_qstep != 0 &&
            cpi.sf.rt_sf.tx_size_level_based_on_qstep >= 2)
        {
            int qstep = x.plane[AOM_PLANE_Y].dequant_QTX[1] >> (x.e_mbd.bd - 5);
            uint qstep_sq = (uint)(qstep * qstep);
            // If the sse is low for low source variance blocks, mark those as
            // transform skip.
            // Note: Though qstep_sq is based on ac qstep, the threshold is kept
            // low so that reliable early estimate of tx skip can be obtained
            // through its comparison with sse.
            if (sse < qstep_sq && x.source_variance < qstep_sq &&
                x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] == 0 &&
                x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] == 0)
                force_skip = 1;
        }
    }

    // [port] CAP_TX_SIZE_FOR_BSIZE_GT32 / TX_SIZE_FOR_BSIZE_GT32 are redefined here in C with
    // the same values as nonrd_opt.h; the nonrd_opt.h.cs definitions are used.

    // C: `const int mult[4]` (function-local table, hoisted).
    private static readonly int[] calculate_tx_size_mult = { 8, 7, 6, 5 };

    private static TX_SIZE calculate_tx_size(AV1_COMP cpi, BLOCK_SIZE bsize,
                                             MACROBLOCK x, uint var,
                                             uint sse, ref int force_skip)
    {
        MACROBLOCKD xd = x.e_mbd;
        TX_SIZE tx_size;
        TxfmSearchParams txfm_params = x.txfm_search_params;
        if (txfm_params.tx_mode_search_type == TX_MODE_SELECT)
        {
            int multiplier = 8;
            uint var_thresh = 0;
            uint is_high_var = 1;
            // Use quantizer based thresholds to determine transform size.
            if (cpi.sf.rt_sf.tx_size_level_based_on_qstep != 0)
            {
                int qband = x.qindex >> (QINDEX_BITS - 2);
                Debug.Assert(qband < 4);
                multiplier = calculate_tx_size_mult[qband];
                int qstep = x.plane[AOM_PLANE_Y].dequant_QTX[1] >> (xd.bd - 5);
                uint qstep_sq = (uint)(qstep * qstep);
                var_thresh = qstep_sq * 2;
                if (cpi.sf.rt_sf.tx_size_level_based_on_qstep >= 2)
                {
                    // If the sse is low for low source variance blocks, mark those as
                    // transform skip.
                    // Note: Though qstep_sq is based on ac qstep, the threshold is kept
                    // low so that reliable early estimate of tx skip can be obtained
                    // through its comparison with sse.
                    if (sse < qstep_sq && x.source_variance < qstep_sq &&
                        x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] == 0 &&
                        x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] == 0)
                        force_skip = 1;
                    // Further lower transform size based on aq mode only if residual
                    // variance is high.
                    is_high_var = (uint)B(var >= var_thresh);
                }
            }
            // Choose larger transform size for blocks where dc component is dominant or
            // the ac component is low.
            if (sse > (((ulong)var * (ulong)multiplier) >> 2) || (var < var_thresh))
                tx_size =
                    AOMMIN(max_txsize_lookup[bsize],
                           tx_mode_to_biggest_tx_size[txfm_params.tx_mode_search_type]);
            else
                tx_size = TX_8X8;

            if (cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ &&
                cyclic_refresh_segment_id_boosted(xd.mi[0].segment_id) != 0 &&
                is_high_var != 0)
                tx_size = TX_8X8;
            else if (tx_size > TX_16X16)
                tx_size = TX_16X16;
        }
        else
        {
            tx_size =
                AOMMIN(max_txsize_lookup[bsize],
                       tx_mode_to_biggest_tx_size[txfm_params.tx_mode_search_type]);
        }

        if (CAP_TX_SIZE_FOR_BSIZE_GT32(txfm_params.tx_mode_search_type, bsize))
            tx_size = TX_SIZE_FOR_BSIZE_GT32;

        return AOMMIN(tx_size, TX_16X16);
    }

    private static void block_variance(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride, int w, int h,
                                       ref uint sse, ref int sum, int block_size,
                                       Ptr<uint> sse8x8, Ptr<int> sum8x8, Ptr<uint> var8x8)
    {
        int k = 0;
        sse = 0;
        sum = 0;

        // This function is called for block sizes >= BLOCK_32x32. As per the design
        // the aom_get_var_sse_sum_8x8_quad() processes four 8x8 blocks (in a 8x32)
        // per call. Hence the width and height of the block need to be at least 8 and
        // 32 samples respectively.
        Debug.Assert(w >= 32);
        Debug.Assert(h >= 8);
        for (int row = 0; row < h; row += block_size)
        {
            for (int col = 0; col < w; col += 32)
            {
                aom_get_var_sse_sum_8x8_quad(src + (src_stride * row + col), src_stride,
                                             @ref + (ref_stride * row + col), ref_stride,
                                             sse8x8 + k, sum8x8 + k, ref sse, ref sum,
                                             var8x8 + k);
                k += 4;
            }
        }
    }

    private static void block_variance_16x16_dual(Ptr<byte> src, int src_stride,
                                                  Ptr<byte> @ref, int ref_stride, int w,
                                                  int h, ref uint sse, ref int sum,
                                                  int block_size, Ptr<uint> sse16x16,
                                                  Ptr<uint> var16x16)
    {
        int k = 0;
        sse = 0;
        sum = 0;
        // This function is called for block sizes >= BLOCK_32x32. As per the design
        // the aom_get_var_sse_sum_16x16_dual() processes four 16x16 blocks (in a
        // 16x32) per call. Hence the width and height of the block need to be at
        // least 16 and 32 samples respectively.
        Debug.Assert(w >= 32);
        Debug.Assert(h >= 16);
        for (int row = 0; row < h; row += block_size)
        {
            for (int col = 0; col < w; col += 32)
            {
                aom_get_var_sse_sum_16x16_dual(src + (src_stride * row + col), src_stride,
                                               @ref + (ref_stride * row + col), ref_stride,
                                               sse16x16 + k, ref sse, ref sum, var16x16 + k);
                k += 2;
            }
        }
    }

    private static void calculate_variance(int bw, int bh, TX_SIZE tx_size,
                                           Ptr<uint> sse_i, Ptr<int> sum_i,
                                           Ptr<uint> var_o, Ptr<uint> sse_o,
                                           Ptr<int> sum_o)
    {
        BLOCK_SIZE unit_size = txsize_to_bsize[tx_size];
        int nw = 1 << (bw - b_width_log2_lookup[unit_size]);
        int nh = 1 << (bh - b_height_log2_lookup[unit_size]);
        int row, col, k = 0;

        for (row = 0; row < nh; row += 2)
        {
            for (col = 0; col < nw; col += 2)
            {
                sse_o[k] = sse_i[row * nw + col] + sse_i[row * nw + col + 1] +
                           sse_i[(row + 1) * nw + col] + sse_i[(row + 1) * nw + col + 1];
                sum_o[k] = sum_i[row * nw + col] + sum_i[row * nw + col + 1] +
                           sum_i[(row + 1) * nw + col] + sum_i[(row + 1) * nw + col + 1];
                var_o[k] = sse_o[k] - (uint)(((long)sum_o[k] * sum_o[k]) >>
                                             (b_width_log2_lookup[unit_size] +
                                              b_height_log2_lookup[unit_size] + 6));
                k++;
            }
        }
    }

    // Adjust the ac_thr according to speed, width, height and normalized sum
    private static int ac_thr_factor(int speed, int width, int height, int norm_sum)
    {
        if (speed >= 8 && norm_sum < 5)
        {
            if (width <= 640 && height <= 480)
                return 4;
            else
                return 2;
        }
        return 1;
    }

    // Sets early_term flag based on chroma planes prediction
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_early_term_based_on_uv_plane(
        AV1_COMP cpi, MACROBLOCK x, BLOCK_SIZE bsize, MACROBLOCKD xd, int mi_row,
        int mi_col, ref int early_term, int num_blk, Ptr<uint> sse_tx,
        Ptr<uint> var_tx, int sum, uint var, uint sse)
    {
        AV1_COMMON cm = cpi.common;
        macroblock_plane p = x.plane[AOM_PLANE_Y];
        uint dc_quant = (uint)p.dequant_QTX[0];
        uint ac_quant = (uint)p.dequant_QTX[1];
        long dc_thr = dc_quant * dc_quant >> 6;
        long ac_thr = ac_quant * ac_quant >> 6;
        int bw = b_width_log2_lookup[bsize];
        int bh = b_height_log2_lookup[bsize];
        int ac_test = 1;
        int dc_test = 1;
        int norm_sum = Math.Abs(sum) >> (bw + bh);

        // [subset] CONFIG_AV1_TEMPORAL_DENOISING == 0: av1_scale_acskip_thresh branch.
        ac_thr *= ac_thr_factor(cpi.oxcf.speed, cm.width, cm.height, norm_sum);

        if (cpi.sf.rt_sf.increase_source_sad_thresh != 0)
        {
            dc_thr = dc_thr << 1;
            ac_thr = ac_thr << 2;
        }

        if (cpi.common.width * cpi.common.height >= 1280 * 720 &&
            cpi.oxcf.tune_cfg.content != AOM_CONTENT_SCREEN &&
            x.content_state_sb.source_sad_nonrd > kLowSad &&
            (sse >> (bw + bh)) > 1000)
        {
            dc_thr = dc_thr >> 4;
            ac_thr = ac_thr >> 4;
        }

        for (int k = 0; k < num_blk; k++)
        {
            // Check if all ac coefficients can be quantized to zero.
            if (!(var_tx[k] < ac_thr || var == 0))
            {
                ac_test = 0;
                break;
            }
            // Check if dc coefficient can be quantized to zero.
            if (!(sse_tx[k] - var_tx[k] < dc_thr || sse == var))
            {
                dc_test = 0;
                break;
            }
        }

        // Check if chroma can be skipped based on ac and dc test flags.
        if (ac_test != 0 && dc_test != 0)
        {
            Span<int> skip_uv = stackalloc int[2];
            skip_uv.Clear();  // int skip_uv[2] = { 0 }
            Span<uint> var_uv = stackalloc uint[2];
            Span<uint> sse_uv = stackalloc uint[2];
            // Transform skipping test in UV planes.
            for (int plane = AOM_PLANE_U; plane <= AOM_PLANE_V; plane++)
            {
                int j = plane - 1;
                skip_uv[j] = 1;
                if (x.color_sensitivity[COLOR_SENS_IDX(plane)] != 0)
                {
                    skip_uv[j] = 0;
                    macroblock_plane puv = x.plane[plane];
                    macroblockd_plane puvd = xd.plane[plane];
                    BLOCK_SIZE uv_bsize = get_plane_block_size(
                        bsize, puvd.subsampling_x, puvd.subsampling_y);
                    // Adjust these thresholds for UV.
                    int shift_ac = cpi.sf.rt_sf.increase_source_sad_thresh != 0 ? 5 : 3;
                    int shift_dc = cpi.sf.rt_sf.increase_source_sad_thresh != 0 ? 4 : 3;
                    long uv_dc_thr =
                        (puv.dequant_QTX[0] * puv.dequant_QTX[0]) >> shift_dc;
                    long uv_ac_thr =
                        (puv.dequant_QTX[1] * puv.dequant_QTX[1]) >> shift_ac;
                    av1_enc_build_inter_predictor(cm, xd, mi_row, mi_col, Ptr<BUFFER_SET>.Null,
                                                  bsize, plane, plane);
                    var_uv[j] = cpi.ppi.fn_ptr[uv_bsize].vf(puv.src.buf, puv.src.stride,
                                                            puvd.dst.buf,
                                                            puvd.dst.stride, ref sse_uv[j]);
                    if ((var_uv[j] < uv_ac_thr || var_uv[j] == 0) &&
                        (sse_uv[j] - var_uv[j] < uv_dc_thr || sse_uv[j] == var_uv[j]))
                        skip_uv[j] = 1;
                    else
                        break;
                }
            }
            if ((skip_uv[0] & skip_uv[1]) != 0)
            {
                early_term = 1;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void calc_rate_dist_block_param(AV1_COMP cpi, MACROBLOCK x,
                                                   ref RD_STATS rd_stats,
                                                   int calculate_rd, ref int early_term,
                                                   BLOCK_SIZE bsize,
                                                   uint sse)
    {
        if (calculate_rd != 0)
        {
            if (early_term == 0)
            {
                int bw = block_size_wide[bsize];
                int bh = block_size_high[bsize];

                // [subset] model_rd_with_curvfit (model_rd.h): every caller in the nonrd path
                // passes calculate_rd = 0, so this is never reached.
                _ = cpi;
                _ = x;
                _ = bw * bh;
                throw new NotSupportedException(
                    "calc_rate_dist_block_param: model_rd_with_curvfit");
            }

            if (early_term != 0)
            {
                rd_stats.rate = 0;
                rd_stats.dist = sse << 4;
            }
        }
    }

    // PORT-NOTE: `unsigned int *var_output` may be NULL in C; every caller (the nonrd inter mode
    // search) passes a valid pointer, so it is a `ref uint`.
    private static void model_skip_for_sb_y_large_64(AV1_COMP cpi, BLOCK_SIZE bsize,
                                                     int mi_row, int mi_col, MACROBLOCK x,
                                                     MACROBLOCKD xd, ref RD_STATS rd_stats,
                                                     ref int early_term, int calculate_rd,
                                                     long best_sse,
                                                     ref uint var_output,
                                                     uint var_prune_threshold)
    {
        // Note our transform coeffs are 8 times an orthogonal transform.
        // Hence quantizer step is also 8 times. To get effective quantizer
        // we need to divide by 8 before sending to modeling function.
        uint sse = 0;
        macroblock_plane p = x.plane[AOM_PLANE_Y];
        macroblockd_plane pd = xd.plane[AOM_PLANE_Y];
        int test_skip = 1;
        uint var;
        int sum = 0;
        int bw = b_width_log2_lookup[bsize];
        int bh = b_height_log2_lookup[bsize];
        NONRD_SCRATCH scratch = x.nonrd_scratch;
        uint[] sse16x16 = scratch.sse16x16;  // unsigned int sse16x16[64] = { 0 }
        uint[] var16x16 = scratch.var16x16;  // unsigned int var16x16[64] = { 0 }
        Array.Clear(sse16x16);
        Array.Clear(var16x16);
        Debug.Assert(xd.mi[0].tx_size == TX_16X16);
        Debug.Assert(bsize > BLOCK_32X32);

        // Calculate variance for whole partition, and also save 16x16 blocks'
        // variance to be used in following transform skipping test.
        block_variance_16x16_dual(p.src.buf, p.src.stride, pd.dst.buf,
                                  pd.dst.stride, 4 << bw, 4 << bh, ref sse, ref sum, 16,
                                  sse16x16, var16x16);

        var = sse - (uint)(((long)sum * sum) >> (bw + bh + 4));
        // if (var_output) -- always non-NULL here (see PORT-NOTE)
        {
            var_output = var;
            if (var_output > var_prune_threshold)
            {
                return;
            }
        }

        rd_stats.sse = sse;
        // Skipping test
        early_term = 0;
        set_force_skip_flag(cpi, x, sse, ref early_term);
        // The code below for setting skip flag assumes transform size of at least
        // 8x8, so force this lower limit on transform.
        MB_MODE_INFO mi = xd.mi[0];
        if (calculate_rd == 0 && cpi.sf.rt_sf.sse_early_term_inter_search != 0 &&
            early_term_inter_search_with_sse(
                cpi.sf.rt_sf.sse_early_term_inter_search, bsize, sse, best_sse,
                mi.mode) != 0)
            test_skip = 0;

        if (early_term != 0) test_skip = 0;

        // Evaluate if the partition block is a skippable block in Y plane.
        if (test_skip != 0)
        {
            Ptr<uint> sse_tx = sse16x16;
            Ptr<uint> var_tx = var16x16;
            uint num_block = (uint)((1 << (bw + bh - 2)) >> 2);
            set_early_term_based_on_uv_plane(cpi, x, bsize, xd, mi_row, mi_col,
                                             ref early_term, (int)num_block, sse_tx, var_tx,
                                             sum, var, sse);
        }
        calc_rate_dist_block_param(cpi, x, ref rd_stats, calculate_rd, ref early_term, bsize,
                                   sse);
    }

    // PORT-NOTE: `unsigned int *var_output` may be NULL in C; every caller (the nonrd inter mode
    // search) passes a valid pointer, so it is a `ref uint`.
    private static void model_skip_for_sb_y_large(AV1_COMP cpi, BLOCK_SIZE bsize,
                                                  int mi_row, int mi_col, MACROBLOCK x,
                                                  MACROBLOCKD xd, ref RD_STATS rd_stats,
                                                  ref int early_term, int calculate_rd,
                                                  long best_sse,
                                                  ref uint var_output,
                                                  uint var_prune_threshold)
    {
        if (x.force_zeromv_skip_for_blk != 0)
        {
            early_term = 1;
            rd_stats.rate = 0;
            rd_stats.dist = 0;
            rd_stats.sse = 0;
            return;
        }

        // For block sizes greater than 32x32, the transform size is always 16x16.
        // This function avoids calling calculate_variance() for tx_size 16x16 cases
        // by directly populating variance at tx_size level from
        // block_variance_16x16_dual() function.
        TxfmSearchParams txfm_params = x.txfm_search_params;
        if (CAP_TX_SIZE_FOR_BSIZE_GT32(txfm_params.tx_mode_search_type, bsize))
        {
            xd.mi[0].tx_size = TX_SIZE_FOR_BSIZE_GT32;
            model_skip_for_sb_y_large_64(cpi, bsize, mi_row, mi_col, x, xd, ref rd_stats,
                                         ref early_term, calculate_rd, best_sse, ref var_output,
                                         var_prune_threshold);
            return;
        }

        // Note our transform coeffs are 8 times an orthogonal transform.
        // Hence quantizer step is also 8 times. To get effective quantizer
        // we need to divide by 8 before sending to modeling function.
        uint sse = 0;
        macroblock_plane p = x.plane[AOM_PLANE_Y];
        macroblockd_plane pd = xd.plane[AOM_PLANE_Y];
        int test_skip = 1;
        uint var;
        int sum = 0;

        int bw = b_width_log2_lookup[bsize];
        int bh = b_height_log2_lookup[bsize];
        NONRD_SCRATCH scratch = x.nonrd_scratch;
        uint[] sse8x8 = scratch.sse8x8;  // unsigned int sse8x8[256] = { 0 }
        int[] sum8x8 = scratch.sum8x8;   // int sum8x8[256] = { 0 }
        uint[] var8x8 = scratch.var8x8;  // unsigned int var8x8[256] = { 0 }
        Array.Clear(sse8x8);
        Array.Clear(sum8x8);
        Array.Clear(var8x8);
        TX_SIZE tx_size;

        // Calculate variance for whole partition, and also save 8x8 blocks' variance
        // to be used in following transform skipping test.
        block_variance(p.src.buf, p.src.stride, pd.dst.buf, pd.dst.stride,
                       4 << bw, 4 << bh, ref sse, ref sum, 8, sse8x8, sum8x8, var8x8);
        var = sse - (uint)(((long)sum * sum) >> (bw + bh + 4));
        // if (var_output) -- always non-NULL here (see PORT-NOTE)
        {
            var_output = var;
            if (var_output > var_prune_threshold)
            {
                return;
            }
        }

        rd_stats.sse = sse;
        // Skipping test
        early_term = 0;
        tx_size = calculate_tx_size(cpi, bsize, x, var, sse, ref early_term);
        Debug.Assert(tx_size <= TX_16X16);
        // The code below for setting skip flag assumes transform size of at least
        // 8x8, so force this lower limit on transform.
        if (tx_size < TX_8X8) tx_size = TX_8X8;
        xd.mi[0].tx_size = tx_size;

        MB_MODE_INFO mi = xd.mi[0];
        if (calculate_rd == 0 && cpi.sf.rt_sf.sse_early_term_inter_search != 0 &&
            early_term_inter_search_with_sse(
                cpi.sf.rt_sf.sse_early_term_inter_search, bsize, sse, best_sse,
                mi.mode) != 0)
            test_skip = 0;

        if (early_term != 0) test_skip = 0;

        // Evaluate if the partition block is a skippable block in Y plane.
        if (test_skip != 0)
        {
            uint[] sse16x16 = scratch.sse16x16;  // unsigned int sse16x16[64] = { 0 }
            int[] sum16x16 = scratch.sum16x16;   // int sum16x16[64] = { 0 }
            uint[] var16x16 = scratch.var16x16;  // unsigned int var16x16[64] = { 0 }
            Array.Clear(sse16x16);
            Array.Clear(sum16x16);
            Array.Clear(var16x16);
            Ptr<uint> sse_tx = sse8x8;
            Ptr<uint> var_tx = var8x8;
            uint num_blks = 1u << (bw + bh - 2);

            if (tx_size >= TX_16X16)
            {
                calculate_variance(bw, bh, TX_8X8, sse8x8, sum8x8, var16x16, sse16x16,
                                   sum16x16);
                sse_tx = sse16x16;
                var_tx = var16x16;
                num_blks = num_blks >> 2;
            }
            set_early_term_based_on_uv_plane(cpi, x, bsize, xd, mi_row, mi_col,
                                             ref early_term, (int)num_blks, sse_tx, var_tx,
                                             sum, var, sse);
        }
        calc_rate_dist_block_param(cpi, x, ref rd_stats, calculate_rd, ref early_term, bsize,
                                   sse);
    }

    // PORT-NOTE: `unsigned int *var_out` and `int *early_term` may be NULL in C (NULL only from
    // the temporal denoiser, CONFIG_AV1_TEMPORAL_DENOISING == 0); every caller here passes valid
    // pointers, so they are `ref` parameters.
    private static void model_rd_for_sb_y(AV1_COMP cpi, BLOCK_SIZE bsize,
                                          MACROBLOCK x, MACROBLOCKD xd,
                                          ref RD_STATS rd_stats, ref uint var_out,
                                          int calculate_rd, ref int early_term)
    {
        if (x.force_zeromv_skip_for_blk != 0 /* && early_term != NULL */)
        {
            early_term = 1;
            rd_stats.rate = 0;
            rd_stats.dist = 0;
            rd_stats.sse = 0;
        }

        // Note our transform coeffs are 8 times an orthogonal transform.
        // Hence quantizer step is also 8 times. To get effective quantizer
        // we need to divide by 8 before sending to modeling function.
        int @ref = xd.mi[0].ref_frame[0];

        Debug.Assert(bsize < BLOCK_SIZES_ALL);

        macroblock_plane p = x.plane[AOM_PLANE_Y];
        macroblockd_plane pd = xd.plane[AOM_PLANE_Y];
        uint sse = 0;
        int rate;
        long dist;

        uint var = cpi.ppi.fn_ptr[bsize].vf(
            p.src.buf, p.src.stride, pd.dst.buf, pd.dst.stride, ref sse);
        int force_skip = 0;
        xd.mi[0].tx_size = calculate_tx_size(cpi, bsize, x, var, sse, ref force_skip);
        // if (var_out) -- always non-NULL here (see PORT-NOTE)
        {
            var_out = var;
        }

        if (calculate_rd != 0 && (force_skip == 0 || @ref == INTRA_FRAME))
        {
            int bwide = block_size_wide[bsize];
            int bhigh = block_size_high[bsize];
            // [subset] model_rd_with_curvfit (model_rd.h): every caller in the nonrd path
            // passes calculate_rd = 0, so this is never reached.
            _ = bwide * bhigh;
            throw new NotSupportedException("model_rd_for_sb_y: model_rd_with_curvfit");
        }
        else
        {
            rate = int.MaxValue;  // this will be overwritten later with av1_block_yrd
            dist = int.MaxValue;
        }
        rd_stats.sse = sse;
        x.pred_sse[@ref] = sse;  // (unsigned int)AOMMIN(sse, UINT_MAX)

        if (force_skip != 0 && @ref > INTRA_FRAME)
        {
            rate = 0;
            dist = (long)sse << 4;
        }

        Debug.Assert(rate >= 0);

        rd_stats.skip_txfm = (byte)B(rate == 0);
        rate = AOMMIN(rate, int.MaxValue);
        rd_stats.rate = rate;
        rd_stats.dist = dist;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int get_drl_cost(PREDICTION_MODE this_mode, int ref_mv_idx,
                                    MB_MODE_INFO_EXT mbmi_ext,
                                    int[][] drl_mode_cost0 /* [][2] */,
                                    sbyte ref_frame_type)
    {
        int cost = 0;
        if (this_mode == NEWMV || this_mode == NEW_NEWMV)
        {
            for (int idx = 0; idx < 2; ++idx)
            {
                if (mbmi_ext.ref_mv_count[ref_frame_type] > idx + 1)
                {
                    byte drl_ctx = av1_drl_ctx(mbmi_ext.weight[ref_frame_type], idx);
                    cost += drl_mode_cost0[drl_ctx][B(ref_mv_idx != idx)];
                    if (ref_mv_idx == idx) return cost;
                }
            }
            return cost;
        }

        if (have_nearmv_in_inter_mode(this_mode) != 0)
        {
            for (int idx = 1; idx < 3; ++idx)
            {
                if (mbmi_ext.ref_mv_count[ref_frame_type] > idx + 1)
                {
                    byte drl_ctx = av1_drl_ctx(mbmi_ext.weight[ref_frame_type], idx);
                    cost += drl_mode_cost0[drl_ctx][B(ref_mv_idx != (idx - 1))];
                    if (ref_mv_idx == (idx - 1)) return cost;
                }
            }
            return cost;
        }
        return cost;
    }

    private static int cost_mv_ref(ModeCosts mode_costs, PREDICTION_MODE mode,
                                   short mode_context)
    {
        if (is_inter_compound_mode(mode) != 0)
        {
            return mode_costs
                .inter_compound_mode_cost[mode_context][INTER_COMPOUND_OFFSET(mode)];
        }

        int mode_cost = 0;
        int mode_ctx = (short)(mode_context & NEWMV_CTX_MASK);

        Debug.Assert(is_inter_mode(mode) != 0);

        if (mode == NEWMV)
        {
            mode_cost = mode_costs.newmv_mode_cost[mode_ctx][0];
            return mode_cost;
        }
        else
        {
            mode_cost = mode_costs.newmv_mode_cost[mode_ctx][1];
            mode_ctx = (short)((mode_context >> GLOBALMV_OFFSET) & GLOBALMV_CTX_MASK);

            if (mode == GLOBALMV)
            {
                mode_cost += mode_costs.zeromv_mode_cost[mode_ctx][0];
                return mode_cost;
            }
            else
            {
                mode_cost += mode_costs.zeromv_mode_cost[mode_ctx][1];
                mode_ctx = (short)((mode_context >> REFMV_OFFSET) & REFMV_CTX_MASK);
                mode_cost += mode_costs.refmv_mode_cost[mode_ctx][B(mode != NEARESTMV)];
                return mode_cost;
            }
        }
    }

    private static void newmv_diff_bias(MACROBLOCKD xd, PREDICTION_MODE this_mode,
                                        ref RD_STATS this_rdc, BLOCK_SIZE bsize, int mv_row,
                                        int mv_col, int speed, uint spatial_variance,
                                        CONTENT_STATE_SB content_state_sb)
    {
        // Bias against MVs associated with NEWMV mode that are very different from
        // top/left neighbors.
        if (this_mode == NEWMV)
        {
            int al_mv_average_row;
            int al_mv_average_col;
            int row_diff, col_diff;
            int above_mv_valid = 0;
            int left_mv_valid = 0;
            int above_row = INVALID_MV_ROW_COL, above_col = INVALID_MV_ROW_COL;
            int left_row = INVALID_MV_ROW_COL, left_col = INVALID_MV_ROW_COL;
            if (bsize >= BLOCK_64X64 && content_state_sb.source_sad_nonrd != kHighSad &&
                spatial_variance < 300 &&
                (mv_row > 16 || mv_row < -16 || mv_col > 16 || mv_col < -16))
            {
                this_rdc.rdcost = this_rdc.rdcost << 2;
                return;
            }
            if (xd.above_mbmi != null)
            {
                above_mv_valid = B(xd.above_mbmi.mv[0].as_int != INVALID_MV);
                above_row = xd.above_mbmi.mv[0].as_mv.row;
                above_col = xd.above_mbmi.mv[0].as_mv.col;
            }
            if (xd.left_mbmi != null)
            {
                left_mv_valid = B(xd.left_mbmi.mv[0].as_int != INVALID_MV);
                left_row = xd.left_mbmi.mv[0].as_mv.row;
                left_col = xd.left_mbmi.mv[0].as_mv.col;
            }
            if (above_mv_valid != 0 && left_mv_valid != 0)
            {
                al_mv_average_row = (above_row + left_row + 1) >> 1;
                al_mv_average_col = (above_col + left_col + 1) >> 1;
            }
            else if (above_mv_valid != 0)
            {
                al_mv_average_row = above_row;
                al_mv_average_col = above_col;
            }
            else if (left_mv_valid != 0)
            {
                al_mv_average_row = left_row;
                al_mv_average_col = left_col;
            }
            else
            {
                al_mv_average_row = al_mv_average_col = 0;
            }
            row_diff = al_mv_average_row - mv_row;
            col_diff = al_mv_average_col - mv_col;
            if (row_diff > 80 || row_diff < -80 || col_diff > 80 || col_diff < -80)
            {
                if (bsize >= BLOCK_32X32)
                    this_rdc.rdcost = this_rdc.rdcost << 1;
                else
                    this_rdc.rdcost = 5 * this_rdc.rdcost >> 2;
            }
        }
        else
        {
            // Bias for speed >= 8 for low spatial variance.
            if (speed >= 8 && spatial_variance < 150 &&
                (mv_row > 64 || mv_row < -64 || mv_col > 64 || mv_col < -64))
                this_rdc.rdcost = 5 * this_rdc.rdcost >> 2;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void update_thresh_freq_fact(AV1_COMP cpi, MACROBLOCK x,
                                                BLOCK_SIZE bsize,
                                                MV_REFERENCE_FRAME ref_frame,
                                                THR_MODES best_mode_idx,
                                                PREDICTION_MODE mode)
    {
        THR_MODES thr_mode_idx = mode_idx[ref_frame][mode_offset(mode)];
        BLOCK_SIZE min_size = AOMMAX(bsize - 3, BLOCK_4X4);
        BLOCK_SIZE max_size = AOMMIN(bsize + 6, BLOCK_128X128);
        for (BLOCK_SIZE bs = min_size; bs <= max_size; bs += 3)
        {
            ref int freq_fact = ref x.thresh_freq_fact[bs][thr_mode_idx];
            if (thr_mode_idx == best_mode_idx)
            {
                freq_fact -= (freq_fact >> 4);
            }
            else
            {
                freq_fact =
                    AOMMIN(freq_fact + RD_THRESH_INC,
                           cpi.sf.inter_sf.adaptive_rd_thresh * RD_THRESH_MAX_FACT);
            }
        }
    }

    // [subset] av1_pickmode_ctx_den_update, recheck_zeromv_after_denoising:
    // CONFIG_AV1_TEMPORAL_DENOISING == 0.

    // [subset] search_filter_ref: the nonrd interpolation filter search
    // (rt_sf.use_nonrd_filter_search) is 0 at speed 11 for every resolution, so
    // is_filter_search_enabled_blk() returns 0 and it is never called.
    // (constant at speed 11)

    // [subset] is_warped_mode_allowed, calc_num_proj_ref, search_motion_mode:
    // !CONFIG_REALTIME_ONLY.

    // [subset] print_stage_time, print_time: COLLECT_NONRD_PICK_MODE_STAT == 0.

    private static bool should_prune_intra_modes_using_neighbors(
        MACROBLOCKD xd, bool enable_intra_mode_pruning_using_neighbors,
        PREDICTION_MODE this_mode, PREDICTION_MODE above_mode,
        PREDICTION_MODE left_mode)
    {
        if (!enable_intra_mode_pruning_using_neighbors) return false;

        // Avoid pruning of DC_PRED as it is the most probable mode to win as per the
        // statistics generated for nonrd intra mode evaluations.
        if (this_mode == DC_PRED) return false;

        // Enable the pruning for current mode only if it is not the winner mode of
        // both the neighboring blocks (left/top).
        return xd.up_available && this_mode != above_mode && xd.left_available &&
               this_mode != left_mode;
    }

    // [subset] av1_search_intrabc_nonrd: intra block copy (rt_sf.rt_use_intrabc) is only
    // enabled for screen content.

    internal static void av1_nonrd_pick_intra_mode(AV1_COMP cpi, MACROBLOCK x, ref RD_STATS rd_cost,
                                                   BLOCK_SIZE bsize, PICK_MODE_CONTEXT ctx)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mi = xd.mi[0];
        // [port] `RD_STATS this_rdc` lives in a per-thread 1-element array because args.rdc
        // points at it (estimate_block_intra_args.rdc is a Ptr<RD_STATS>).
        RD_STATS[] this_rdc_buf = x.nonrd_scratch.intra_this_rdc;
        ref RD_STATS this_rdc = ref this_rdc_buf[0];
        RD_STATS best_rdc = default;
        estimate_block_intra_args args = x.nonrd_scratch.intra_args;
        init_estimate_block_intra_args(args, cpi, x);
        TxfmSearchParams txfm_params = x.txfm_search_params;
        int mi_row = xd.mi_row;
        int mi_col = xd.mi_col;
        mi.tx_size =
            AOMMIN(max_txsize_lookup[bsize],
                   tx_mode_to_biggest_tx_size[txfm_params.tx_mode_search_type]);
        // For flat blocks at high qp, along the top/left boundary, cap the
        // transform size to 16x16. This is avoid chessboard artifact that
        // can occur for flat input at high QP.
        if (cm.quant_params.base_qindex > 150 && x.source_variance == 0 &&
            (mi_row == 0 || mi_col == 0) && mi.tx_size > TX_16X16)
        {
            mi.tx_size = TX_16X16;
        }
        Debug.Assert(xd.lossless[mi.segment_id] == 0 || mi.tx_size == TX_4X4);
        BLOCK_SIZE tx_bsize = txsize_to_bsize[mi.tx_size];

        // If the current block size is the same as the transform block size, enable
        // mode pruning based on the best SAD so far.
        if (cpi.sf.rt_sf.prune_intra_mode_using_best_sad_so_far && bsize == tx_bsize)
            args.prune_mode_based_on_sad = true;

        int[] bmode_costs;
        PREDICTION_MODE best_mode = DC_PRED;
        MB_MODE_INFO above_mi = xd.above_mbmi;
        MB_MODE_INFO left_mi = xd.left_mbmi;
        PREDICTION_MODE A = av1_above_block_mode(above_mi);
        PREDICTION_MODE L = av1_left_block_mode(left_mi);
        int above_ctx = intra_mode_context[A];
        int left_ctx = intra_mode_context[L];
        uint source_variance = x.source_variance;
        bmode_costs = x.mode_costs.y_mode_costs[above_ctx][left_ctx];
        // Use this flag to signal large flat blocks that may need special
        // treatment: in the current case H/V/SMOOTH may not be skipped if
        // DC has nonzero distortion and skippable is set. This is to remove
        // visual artifacts observed for screen in realtime mode.
        bool flat_blocks_screen =
            cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN &&
            cpi.oxcf.mode == REALTIME && x.source_variance == 0 &&
            bsize >= BLOCK_32X32;
        av1_invalid_rd_stats(ref best_rdc);
        av1_invalid_rd_stats(ref this_rdc);

        init_mbmi_nonrd(mi, DC_PRED, INTRA_FRAME, NONE_FRAME, cm);
        mi.mv[0].as_int = mi.mv[1].as_int = INVALID_MV;

        bool allow_skip_nondc = true;
        bool palette_selected = false;
        // Change the limit of this loop to add other intra prediction
        // mode tests.
        for (int mode_index = 0; mode_index < RTC_INTRA_MODES; ++mode_index)
        {
            PREDICTION_MODE this_mode = intra_mode_list[mode_index];

            // Force DC for spatially flat block for large bsize, on top-left corner.
            // This removed potential artifact observed in gray scale image for high Q.
            if (x.source_variance == 0 && mi_col == 0 && mi_row == 0 &&
                bsize >= BLOCK_32X32 && this_mode > 0)
                continue;

            // As per the statistics generated for intra mode evaluation in the nonrd
            // path, it is found that the probability of H_PRED mode being the winner is
            // very low when the best mode so far is V_PRED (out of DC_PRED and V_PRED).
            // If V_PRED is the winner mode out of DC_PRED and V_PRED, it could imply
            // the presence of a vertically dominant pattern. Hence, H_PRED mode is not
            // evaluated.
            if (cpi.sf.rt_sf.prune_h_pred_using_best_mode_so_far &&
                this_mode == H_PRED && best_mode == V_PRED && allow_skip_nondc)
                continue;

            if (should_prune_intra_modes_using_neighbors(
                    xd, cpi.sf.rt_sf.enable_intra_mode_pruning_using_neighbors,
                    this_mode, A, L))
            {
                // Prune V_PRED and H_PRED if source variance of the block is less than
                // or equal to 50. The source variance threshold is obtained empirically.
                if ((this_mode == V_PRED || this_mode == H_PRED) &&
                    source_variance <= 50 && allow_skip_nondc)
                    continue;

                // As per the statistics, probability of SMOOTH_PRED being the winner is
                // low when best mode so far is DC_PRED (out of DC_PRED, V_PRED and
                // H_PRED). Hence, SMOOTH_PRED mode is not evaluated.
                if (best_mode == DC_PRED && this_mode == SMOOTH_PRED && allow_skip_nondc)
                    continue;
            }

            this_rdc.dist = this_rdc.rate = 0;
            args.mode = this_mode;
            args.skippable = 1;
            args.rdc = this_rdc_buf;
            mi.mode = this_mode;
            av1_foreach_transformed_block_in_plane(xd, bsize, AOM_PLANE_Y,
                                                   s_estimate_block_intra, args);

            if (this_rdc.rate == int.MaxValue) continue;

            int skip_ctx = av1_get_skip_txfm_context(xd);
            if (args.skippable != 0)
            {
                this_rdc.rate = x.mode_costs.skip_txfm_cost[skip_ctx][1];
            }
            else
            {
                this_rdc.rate += x.mode_costs.skip_txfm_cost[skip_ctx][0];
            }
            this_rdc.rate += bmode_costs[this_mode];
            this_rdc.rdcost = RDCOST(x.rdmult, this_rdc.rate, this_rdc.dist);
            if (this_rdc.rdcost < best_rdc.rdcost)
            {
                best_rdc = this_rdc;
                best_mode = this_mode;
            }
            if (this_mode == DC_PRED)
            {
                if (flat_blocks_screen && args.skippable != 0 && this_rdc.dist > 0)
                    allow_skip_nondc = false;
            }
        }

        uint thresh_sad =
            cpi.sf.rt_sf.prune_palette_search_nonrd > 1 ? 100u : 20u;
        uint best_sad_norm =
            args.best_sad >>
            (b_width_log2_lookup[bsize] + b_height_log2_lookup[bsize]);

        // Try palette if it's enabled.
        bool try_palette =
            cpi.oxcf.tool_cfg.enable_palette &&
            av1_allow_palette(B(cpi.common.features.allow_screen_content_tools),
                              mi.bsize) != 0;
        if (cpi.sf.rt_sf.prune_palette_search_nonrd > 0)
        {
            bool prune =
                (!args.prune_mode_based_on_sad || best_sad_norm > thresh_sad) &&
                bsize <= BLOCK_16X16 && x.source_variance > 200;
            try_palette &= prune;
        }
        if (try_palette)
        {
            // [subset] av1_search_palette_mode_luma: av1_allow_palette() needs
            // features.allow_screen_content_tools, which is only set for screen content.
            _ = palette_selected;
            throw new NotSupportedException("av1_nonrd_pick_intra_mode: palette");
        }

        bool try_intrabc =
            cpi.sf.rt_sf.rt_use_intrabc != 0 && av1_allow_intrabc(cm) != 0 &&
            bsize <= BLOCK_16X16 &&
            (cpi.sf.rt_sf.rt_prune_intrabc_nonrd == 0 || palette_selected);

        if (try_intrabc)
        {
            // [subset] av1_search_intrabc_nonrd: intra block copy is disabled in the subset.
            throw new NotSupportedException("av1_nonrd_pick_intra_mode: intrabc");
        }

        mi.mode = best_mode;
        // Keep DC for UV since mode test is based on Y channel only.
        mi.uv_mode = UV_DC_PRED;
        rd_cost = best_rdc;

        // For lossless: always force the skip flags off.
        // Even though the blk_skip is set to 0 above in the rdcost comparison,
        // do it here again in case the above logic changes.
        if (is_lossless_requested(in cpi.oxcf.rc_cfg) != 0)
        {
            x.txfm_search_info.skip_txfm = 0;
        }

        store_coding_context_nonrd(x, ctx);
    }

    // [port] Cached delegate for av1_estimate_block_intra (nonrd_opt.c), avoiding a delegate
    // allocation per call of av1_foreach_transformed_block_in_plane.
    private static readonly foreach_transformed_block_visitor s_estimate_block_intra =
        av1_estimate_block_intra;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int is_same_gf_and_last_scale(AV1_COMMON cm)
    {
        scale_factors sf_last = get_ref_scale_factors(cm, LAST_FRAME);
        scale_factors sf_golden =
            get_ref_scale_factors(cm, GOLDEN_FRAME);
        return B((sf_last.x_scale_fp == sf_golden.x_scale_fp) &&
                 (sf_last.y_scale_fp == sf_golden.y_scale_fp));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void get_ref_frame_use_mask(AV1_COMP cpi, MACROBLOCK x,
                                               MB_MODE_INFO mi, int mi_row,
                                               int mi_col, BLOCK_SIZE bsize,
                                               int gf_temporal_ref,
                                               int[] use_ref_frame,
                                               ref int force_skip_low_temp_var)
    {
        AV1_COMMON cm = cpi.common;
        segmentation seg = cm.seg;
        int is_small_sb = B(cm.seq_params.sb_size == BLOCK_64X64);

        // When the ref_frame_config is used to set the reference frame structure
        // then the usage of alt_ref is determined by the ref_frame_flags
        // (and not the speed feature use_nonrd_altref_frame).
        int use_alt_ref_frame = B(cpi.ppi.rtc_ref.set_ref_frame_config != 0 ||
                                  cpi.sf.rt_sf.use_nonrd_altref_frame != 0);

        int use_golden_ref_frame = 1;
        int use_last_ref_frame = 1;

        // When the ref_frame_config is used to set the reference frame structure:
        // check if LAST is used as a reference. And only remove golden and altref
        // references below if last is used as a reference.
        if (cpi.ppi.rtc_ref.set_ref_frame_config != 0)
            use_last_ref_frame =
                (cpi.ref_frame_flags & AOM_LAST_FLAG) != 0 ? use_last_ref_frame : 0;

        // frame_since_golden is not used when user sets the referene structure.
        if (cpi.ppi.rtc_ref.set_ref_frame_config == 0 && use_last_ref_frame != 0 &&
            cpi.rc.frames_since_golden == 0 && gf_temporal_ref != 0)
        {
            use_golden_ref_frame = 0;
        }

        if (use_last_ref_frame != 0 && cpi.sf.rt_sf.short_circuit_low_temp_var != 0 &&
            x.nonrd_prune_ref_frame_search != 0)
        {
            if (is_small_sb != 0)
                force_skip_low_temp_var = av1_get_force_skip_low_temp_var_small_sb(
                    x.part_search_info.variance_low, mi_row, mi_col, bsize);
            else
                force_skip_low_temp_var = av1_get_force_skip_low_temp_var(
                    x.part_search_info.variance_low, mi_row, mi_col, bsize);
            // If force_skip_low_temp_var is set, skip golden reference.
            if (force_skip_low_temp_var != 0)
            {
                use_golden_ref_frame = 0;
                use_alt_ref_frame = 0;
            }
        }

        if (use_last_ref_frame != 0 &&
            (x.nonrd_prune_ref_frame_search > 2 || x.force_zeromv_skip_for_blk != 0 ||
             (x.nonrd_prune_ref_frame_search > 1 && bsize > BLOCK_64X64)))
        {
            use_golden_ref_frame = 0;
            use_alt_ref_frame = 0;
        }

        if (segfeature_active(seg, mi.segment_id, SEG_LVL_REF_FRAME) != 0 &&
            get_segdata(seg, mi.segment_id, SEG_LVL_REF_FRAME) == GOLDEN_FRAME)
        {
            use_ref_frame[GOLDEN_FRAME] = 1;
            use_ref_frame[ALTREF_FRAME] = 0;
            return;
        }
        else if (segfeature_active(seg, mi.segment_id, SEG_LVL_REF_FRAME) != 0 &&
                 get_segdata(seg, mi.segment_id, SEG_LVL_REF_FRAME) ==
                     ALTREF_FRAME)
        {
            use_ref_frame[GOLDEN_FRAME] = 0;
            use_ref_frame[ALTREF_FRAME] = 1;
            return;
        }

        // Skip golden/altref reference if color is set, on flat blocks with motion.
        // For screen: always skip golden/alt (if color_sensitivity_sb_g/alt is set)
        // except when x->nonrd_prune_ref_frame_search = 0. This latter flag
        // may be set in the variance partition when golden is a much better
        // reference than last, in which case it may not be worth skipping
        // golden/altref completely.
        // Condition on use_last_ref to make sure there remains at least one
        // reference.
        if (use_last_ref_frame != 0 &&
            ((cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN &&
              x.nonrd_prune_ref_frame_search != 0) ||
             (x.source_variance < 200 &&
              x.content_state_sb.source_sad_nonrd >= kLowSad)))
        {
            if (x.color_sensitivity_sb_g[COLOR_SENS_IDX(AOM_PLANE_U)] == 1 ||
                x.color_sensitivity_sb_g[COLOR_SENS_IDX(AOM_PLANE_V)] == 1)
                use_golden_ref_frame = 0;
            if (x.color_sensitivity_sb_alt[COLOR_SENS_IDX(AOM_PLANE_U)] == 1 ||
                x.color_sensitivity_sb_alt[COLOR_SENS_IDX(AOM_PLANE_V)] == 1)
                use_alt_ref_frame = 0;
        }

        // For non-screen: if golden and altref are not being selected as references
        // (use_golden_ref_frame/use_alt_ref_frame = 0) check to allow golden back
        // based on the sad of nearest/nearmv of LAST ref. If this block sad is large,
        // keep golden as reference. Only do this for the agrressive pruning mode and
        // avoid it when color is set for golden reference.
        if (cpi.oxcf.tune_cfg.content != AOM_CONTENT_SCREEN &&
            (cpi.ref_frame_flags & AOM_LAST_FLAG) != 0 && use_golden_ref_frame == 0 &&
            use_alt_ref_frame == 0 && x.pred_mv_sad[LAST_FRAME] != int.MaxValue &&
            x.nonrd_prune_ref_frame_search > 2 &&
            x.color_sensitivity_sb_g[COLOR_SENS_IDX(AOM_PLANE_U)] == 0 &&
            x.color_sensitivity_sb_g[COLOR_SENS_IDX(AOM_PLANE_V)] == 0)
        {
            int thr = (cm.width * cm.height > RESOLUTION_288P) ? 100 : 150;
            int pred = x.pred_mv_sad[LAST_FRAME] >>
                       (b_width_log2_lookup[bsize] + b_height_log2_lookup[bsize]);
            if (pred > thr) use_golden_ref_frame = 1;
        }

        use_alt_ref_frame =
            (cpi.ref_frame_flags & AOM_ALT_FLAG) != 0 ? use_alt_ref_frame : 0;
        use_golden_ref_frame =
            (cpi.ref_frame_flags & AOM_GOLD_FLAG) != 0 ? use_golden_ref_frame : 0;

        // For spatial layers: enable golden ref if it is set by user and
        // corresponds to the lower spatial layer.
        if (cpi.svc.spatial_layer_id > 0 && (cpi.ref_frame_flags & AOM_GOLD_FLAG) != 0 &&
            x.content_state_sb.source_sad_nonrd < kHighSad)
        {
            int buffslot_golden =
                cpi.ppi.rtc_ref.ref_idx[GOLDEN_FRAME - LAST_FRAME];
            if (cpi.ppi.rtc_ref.buffer_time_index[buffslot_golden] ==
                cpi.svc.current_superframe)
                use_golden_ref_frame = 1;
        }

        use_ref_frame[ALTREF_FRAME] = use_alt_ref_frame;
        use_ref_frame[GOLDEN_FRAME] = use_golden_ref_frame;
        use_ref_frame[LAST_FRAME] = use_last_ref_frame;
        // Keep this assert on, as only 3 references are used in nonrd_pickmode
        // (LAST, GOLDEN, ALTREF), and if all 3 are not set by user then this
        // frame must be an intra-only frame and hence should never enter the
        // pickmode here for inter frames.
        Debug.Assert(use_last_ref_frame != 0 || use_golden_ref_frame != 0 ||
                     use_alt_ref_frame != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int is_filter_search_enabled_blk(AV1_COMP cpi, MACROBLOCK x,
                                                    int mi_row, int mi_col,
                                                    BLOCK_SIZE bsize, int segment_id,
                                                    int cb_pred_filter_search,
                                                    ref InterpFilter filt_select)
    {
        AV1_COMMON cm = cpi.common;
        // filt search disabled
        if (cpi.sf.rt_sf.use_nonrd_filter_search == 0) return 0;
        // filt search purely based on mode properties
        if (cb_pred_filter_search == 0) return 1;
        MACROBLOCKD xd = x.e_mbd;
        int enable_interp_search = 0;
        if (!(xd.left_mbmi != null && xd.above_mbmi != null))
        {
            // neighbors info unavailable
            enable_interp_search = 2;
        }
        else if (!(is_inter_block(xd.left_mbmi) != 0 &&
                   is_inter_block(xd.above_mbmi) != 0))
        {
            // neighbor is INTRA
            enable_interp_search = 2;
        }
        else if (xd.left_mbmi.interp_filters.as_int !=
                 xd.above_mbmi.interp_filters.as_int)
        {
            // filters are different
            enable_interp_search = 2;
        }
        else if ((cb_pred_filter_search == 1) &&
                 (xd.left_mbmi.interp_filters.as_filters.x_filter !=
                  EIGHTTAP_REGULAR))
        {
            // not regular
            enable_interp_search = 2;
        }
        else
        {
            // enable prediction based on chessboard pattern
            if (xd.left_mbmi.interp_filters.as_filters.x_filter == EIGHTTAP_SMOOTH)
                filt_select = EIGHTTAP_SMOOTH;
            int bsl = mi_size_wide_log2[bsize];
            enable_interp_search =
                B(((((mi_row + mi_col) >> bsl) +
                    get_chessboard_index((int)cm.current_frame.frame_number)) &
                   0x1) != 0);
            if (cyclic_refresh_segment_id_boosted(segment_id) != 0) enable_interp_search = 1;
        }
        return enable_interp_search;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int skip_mode_by_threshold(PREDICTION_MODE mode,
                                              MV_REFERENCE_FRAME ref_frame,
                                              int_mv mv, int frames_since_golden,
                                              int[] rd_threshes,
                                              int[] rd_thresh_freq_fact,
                                              long best_cost, int best_skip,
                                              int extra_shift)
    {
        int skip_this_mode = 0;
        THR_MODES mode_index = mode_idx[ref_frame][INTER_OFFSET(mode)];
        long mode_rd_thresh =
            best_skip != 0 ? ((long)rd_threshes[mode_index]) << (extra_shift + 1)
                           : ((long)rd_threshes[mode_index]) << extra_shift;

        // Increase mode_rd_thresh value for non-LAST for improved encoding
        // speed
        if (ref_frame != LAST_FRAME)
        {
            mode_rd_thresh = mode_rd_thresh << 1;
            if (ref_frame == GOLDEN_FRAME && frames_since_golden > 4)
                mode_rd_thresh = mode_rd_thresh << (extra_shift + 1);
        }

        if (rd_less_than_thresh(best_cost, mode_rd_thresh,
                                rd_thresh_freq_fact[mode_index]) != 0)
            if (mv.as_int != 0) skip_this_mode = 1;

        return skip_this_mode;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int skip_mode_by_low_temp(
        PREDICTION_MODE mode, MV_REFERENCE_FRAME ref_frame, BLOCK_SIZE bsize,
        CONTENT_STATE_SB content_state_sb, int_mv mv, int force_skip_low_temp_var)
    {
        // Skip non-zeromv mode search for non-LAST frame if force_skip_low_temp_var
        // is set. If nearestmv for golden frame is 0, zeromv mode will be skipped
        // later.
        if (force_skip_low_temp_var != 0 && ref_frame != LAST_FRAME && mv.as_int != 0)
        {
            return 1;
        }

        if (content_state_sb.source_sad_nonrd != kHighSad && bsize >= BLOCK_64X64 &&
            force_skip_low_temp_var != 0 && mode == NEWMV)
        {
            return 1;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int skip_mode_by_bsize_and_ref_frame(
        PREDICTION_MODE mode, MV_REFERENCE_FRAME ref_frame, BLOCK_SIZE bsize,
        int extra_prune, uint sse_zeromv_norm, int more_prune,
        int skip_nearmv)
    {
        const uint thresh_skip_golden = 500;

        if (ref_frame != LAST_FRAME && sse_zeromv_norm < thresh_skip_golden &&
            mode == NEWMV)
            return 1;

        if ((bsize == BLOCK_128X128 && mode == NEWMV) ||
            (skip_nearmv != 0 && mode == NEARMV))
            return 1;

        // Skip testing non-LAST if this flag is set.
        if (extra_prune != 0)
        {
            if (extra_prune > 1 && ref_frame != LAST_FRAME &&
                (bsize > BLOCK_16X16 && mode == NEWMV))
                return 1;

            if (ref_frame != LAST_FRAME && mode == NEARMV) return 1;

            if (more_prune != 0 && bsize >= BLOCK_32X32 && mode == NEARMV) return 1;
        }
        return 0;
    }

    private static void set_block_source_sad(AV1_COMP cpi, MACROBLOCK x, BLOCK_SIZE bsize,
                                             in buf_2d yv12_mb)
    {
        macroblock_plane p = x.plane[0];
        int y_sad = (int)cpi.ppi.fn_ptr[bsize].sdf(p.src.buf, p.src.stride,
                                                   yv12_mb.buf, yv12_mb.stride);
        if (y_sad == 0) x.block_is_zero_sad = 1;
    }

    private static void set_color_sensitivity(AV1_COMP cpi, MACROBLOCK x,
                                              BLOCK_SIZE bsize, int y_sad,
                                              uint source_variance,
                                              buf_2d[] yv12_mb /* [MAX_MB_PLANE] */)
    {
        int subsampling_x = cpi.common.seq_params.subsampling_x;
        int subsampling_y = cpi.common.seq_params.subsampling_y;
        int source_sad_nonrd = x.content_state_sb.source_sad_nonrd;
        int high_res = B(cpi.common.width * cpi.common.height >= 640 * 360);
        if (bsize == cpi.common.seq_params.sb_size &&
            x.force_color_check_block_level == 0)
        {
            // At superblock level color_sensitivity is already set to 0, 1, or 2.
            // 2 is middle/uncertain level. To avoid additional sad
            // computations when bsize = sb_size force level 2 to 1 (certain color)
            // for motion areas. Avoid this shortcut if x->force_color_check_block_level
            // is set.
            if (x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] == 2)
            {
                x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] =
                    (byte)(source_sad_nonrd >= kMedSad ? 1 : 0);
            }
            if (x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] == 2)
            {
                x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] =
                    (byte)(source_sad_nonrd >= kMedSad ? 1 : 0);
            }
            return;
        }
        // Divide factor for comparing uv_sad to y_sad.
        int shift = 3;
        // Threshold for the block spatial source variance.
        uint source_var_thr = 50;
        // Thresholds for normalized uv_sad, the first one is used for
        // low source_varaince.
        int norm_uv_sad_thresh = 100;
        int norm_uv_sad_thresh2 = 40;
        if (source_sad_nonrd >= kMedSad && x.source_variance > 0 && high_res != 0)
            shift = 4;
        if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN)
        {
            if (cpi.rc.high_source_sad != 0) shift = 6;
            if (source_sad_nonrd > kMedSad)
            {
                source_var_thr = 1200;
                norm_uv_sad_thresh = 10;
            }
            if (cpi.rc.percent_blocks_with_motion > 90 &&
                cpi.rc.frame_source_sad > 10000 && source_sad_nonrd > kLowSad)
            {
                // Aggressive setting for color_sensitivity for this content.
                shift = 10;
                norm_uv_sad_thresh = 0;
                norm_uv_sad_thresh2 = 0;
            }
        }
        NOISE_LEVEL noise_level = kLow;
        int norm_sad =
            y_sad >> (b_width_log2_lookup[bsize] + b_height_log2_lookup[bsize]);
        uint thresh_spatial = (cpi.common.width > 1920) ? 5000u : 1000u;
        // If the spatial source variance is high and the normalized y_sad
        // is low, then y-channel is likely good for mode estimation, so keep
        // color_sensitivity off. For low noise content for now, since there is
        // some bdrate regression for noisy color clip.
        if (cpi.noise_estimate.enabled != 0)
            noise_level = av1_noise_estimate_extract_level(cpi.noise_estimate);
        if (noise_level == kLow && source_variance > thresh_spatial &&
            cpi.oxcf.tune_cfg.content != AOM_CONTENT_SCREEN && norm_sad < 50)
        {
            x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] = 0;
            x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] = 0;
            return;
        }
        int num_planes = av1_num_planes(cpi.common);

        for (int plane = AOM_PLANE_U; plane < num_planes; ++plane)
        {
            // Always check if level = 2. If level = 0 check again for
            // motion areas for higher resolns, where color artifacts
            // are more noticeable. Always check if
            // x->force_color_check_block_level is set.
            if (x.color_sensitivity[COLOR_SENS_IDX(plane)] == 2 ||
                x.force_color_check_block_level != 0 ||
                (x.color_sensitivity[COLOR_SENS_IDX(plane)] == 0 &&
                 source_sad_nonrd >= kMedSad && high_res != 0))
            {
                macroblock_plane p = x.plane[plane];
                BLOCK_SIZE bs =
                    get_plane_block_size(bsize, subsampling_x, subsampling_y);

                int uv_sad = (int)cpi.ppi.fn_ptr[bs].sdf(
                    p.src.buf, p.src.stride, yv12_mb[plane].buf, yv12_mb[plane].stride);

                int norm_uv_sad =
                    uv_sad >> (b_width_log2_lookup[bs] + b_height_log2_lookup[bs]);
                x.color_sensitivity[COLOR_SENS_IDX(plane)] =
                    (byte)B(uv_sad > (y_sad >> shift) && norm_uv_sad > norm_uv_sad_thresh2);
                if (source_variance < source_var_thr && norm_uv_sad > norm_uv_sad_thresh)
                    x.color_sensitivity[COLOR_SENS_IDX(plane)] = 1;
            }
        }
    }

    // [subset] setup_compound_prediction, set_compound_mode: compound prediction is never
    // set up (see setup_compound_params_from_comp_idx).

    // Prune compound mode if the single mode variance is lower than a fixed
    // percentage of the median value.
    private static bool skip_comp_based_on_var(
        uint[][] single_vars /* [][REF_FRAMES] */, BLOCK_SIZE bsize)
    {
        uint best_var = uint.MaxValue;
        for (int cur_mode_idx = 0; cur_mode_idx < RTC_INTER_MODES; cur_mode_idx++)
        {
            for (int ref_idx = 0; ref_idx < REF_FRAMES; ref_idx++)
            {
                best_var = AOMMIN(best_var, single_vars[cur_mode_idx][ref_idx]);
            }
        }
        uint thresh_64 = (uint)(0.57356805f * 8659);
        uint thresh_32 = (uint)(0.23964763f * 4281);

        // Currently, the thresh for 128 and 16 are not well-tuned. We are using the
        // results from 64 and 32 as an heuristic.
        switch (bsize)
        {
            case BLOCK_128X128: return best_var < 4 * thresh_64;
            case BLOCK_64X64: return best_var < thresh_64;
            case BLOCK_32X32: return best_var < thresh_32;
            case BLOCK_16X16: return best_var < thresh_32 / 4;
            default: return false;
        }
    }

    // [port] `const MV_REFERENCE_FRAME rf[2] = { ref_frame, NONE_FRAME }` locals of
    // fill_single_inter_mode_costs() / is_globalmv_better(): read-only, so one shared table
    // indexed by ref_frame replaces them.
    private static readonly MV_REFERENCE_FRAME[][] single_ref_rf = make_single_ref_rf();

    private static MV_REFERENCE_FRAME[][] make_single_ref_rf()
    {
        var t = new MV_REFERENCE_FRAME[REF_FRAMES][];
        for (int i = 0; i < REF_FRAMES; i++) t[i] = new MV_REFERENCE_FRAME[] { i, NONE_FRAME };
        return t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fill_single_inter_mode_costs(
        int[][] single_inter_mode_costs /* [][REF_FRAMES] */, int num_inter_modes,
        REF_MODE[] reference_mode_set, ModeCosts mode_costs,
        short[] mode_context)
    {
        Span<bool> ref_frame_used = stackalloc bool[REF_FRAMES];
        ref_frame_used.Clear();  // bool ref_frame_used[REF_FRAMES] = { false }
        for (int idx = 0; idx < num_inter_modes; idx++)
        {
            ref_frame_used[reference_mode_set[idx].ref_frame] = true;
        }

        for (int this_ref_frame = LAST_FRAME; this_ref_frame < REF_FRAMES;
             this_ref_frame++)
        {
            if (!ref_frame_used[this_ref_frame])
            {
                continue;
            }

            MV_REFERENCE_FRAME[] rf = single_ref_rf[this_ref_frame];
            short mode_ctx = av1_mode_context_analyzer(mode_context, rf);
            for (PREDICTION_MODE this_mode = NEARESTMV; this_mode <= NEWMV;
                 this_mode++)
            {
                single_inter_mode_costs[INTER_OFFSET(this_mode)][this_ref_frame] =
                    cost_mv_ref(mode_costs, this_mode, mode_ctx);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool is_globalmv_better(
        PREDICTION_MODE this_mode, MV_REFERENCE_FRAME ref_frame, int rate_mv,
        ModeCosts mode_costs,
        int[][] single_inter_mode_costs /* [][REF_FRAMES] */,
        MB_MODE_INFO_EXT mbmi_ext)
    {
        int globalmv_mode_cost =
            single_inter_mode_costs[INTER_OFFSET(GLOBALMV)][ref_frame];
        int this_mode_cost =
            rate_mv + single_inter_mode_costs[INTER_OFFSET(this_mode)][ref_frame];
        if (this_mode == NEWMV || this_mode == NEARMV)
        {
            MV_REFERENCE_FRAME[] rf = single_ref_rf[ref_frame];
            this_mode_cost += get_drl_cost(
                NEWMV, 0, mbmi_ext, mode_costs.drl_mode_cost0, av1_ref_frame_type(rf));
        }
        return this_mode_cost > globalmv_mode_cost;
    }

    // Set up the mv/ref_frames etc based on the comp_index. Returns 1 if it
    // succeeds, 0 if it fails.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int setup_compound_params_from_comp_idx(
        AV1_COMP cpi, MACROBLOCK x, buf_2d[][] yv12_mb /* [8][MAX_MB_PLANE] */,
        ref PREDICTION_MODE this_mode, ref MV_REFERENCE_FRAME ref_frame,
        ref MV_REFERENCE_FRAME ref_frame2, int_mv[][] frame_mv /* [MB_MODE_COUNT][REF_FRAMES] */,
        int[] use_ref_frame_mask, int comp_index,
        bool comp_use_zero_zeromv_only, ref MV_REFERENCE_FRAME last_comp_ref_frame,
        BLOCK_SIZE bsize)
    {
        COMP_REF_MODE comp_ref_mode =
            is_one_pass_rt_lag_params(cpi) != 0
                ? comp_ref_mode_set_full[comp_index]
                : comp_ref_mode_set[comp_index];
        int skip_gf = 0;
        int skip_alt = 0;
        if (x.source_variance < 50 && bsize > BLOCK_16X16)
        {
            if (x.color_sensitivity_sb_g[COLOR_SENS_IDX(AOM_PLANE_U)] == 1 ||
                x.color_sensitivity_sb_g[COLOR_SENS_IDX(AOM_PLANE_V)] == 1)
                skip_gf = 1;
            if (x.color_sensitivity_sb_alt[COLOR_SENS_IDX(AOM_PLANE_U)] == 1 ||
                x.color_sensitivity_sb_alt[COLOR_SENS_IDX(AOM_PLANE_V)] == 1)
                skip_alt = 1;
        }
        if (comp_use_zero_zeromv_only && this_mode != GLOBAL_GLOBALMV)
        {
            return 0;
        }
        if (ref_frame2 == GOLDEN_FRAME &&
            (cpi.sf.rt_sf.ref_frame_comp_nonrd[0] == 0 || skip_gf != 0 ||
             (cpi.ref_frame_flags & AOM_GOLD_FLAG) == 0))
        {
            return 0;
        }
        else if (ref_frame2 == LAST2_FRAME &&
                 (cpi.sf.rt_sf.ref_frame_comp_nonrd[1] == 0 ||
                  (cpi.ref_frame_flags & AOM_LAST2_FLAG) == 0))
        {
            return 0;
        }
        else if (ref_frame2 == ALTREF_FRAME &&
                 (cpi.sf.rt_sf.ref_frame_comp_nonrd[2] == 0 || skip_alt != 0 ||
                  (cpi.ref_frame_flags & AOM_ALT_FLAG) == 0))
        {
            return 0;
        }
        // [subset] setup_compound_prediction / set_compound_mode and the rest of this
        // function: compound prediction is never executed in the subset. The second
        // reference of every candidate pair is rejected above: GOLDEN and LAST2 by
        // rt_sf.ref_frame_comp_nonrd (constant {0, 0, 1} at speed 11), ALTREF by skip_alt /
        // ref_frame_flags (coverage: setup_compound_prediction and set_compound_mode are
        // never reached).
        _ = comp_ref_mode;
        _ = yv12_mb;
        _ = ref_frame;
        _ = frame_mv;
        _ = use_ref_frame_mask;
        _ = last_comp_ref_frame;
        throw new NotSupportedException("setup_compound_params_from_comp_idx: compound");
    }

    // [subset] previous_mode_performed_poorly, prune_compoundmode_with_singlemode_var:
    // rt_sf.prune_compoundmode_with_singlemode_var is false at speed 11 (and compound
    // prediction is never set up). (constant at speed 11)

    // Function to setup parameters used for inter mode evaluation in non-rd.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_params_nonrd_pick_inter_mode(
        AV1_COMP cpi, MACROBLOCK x, InterModeSearchStateNonrd search_state,
        ref RD_STATS rd_cost, ref int force_skip_low_temp_var, int mi_row, int mi_col,
        int gf_temporal_ref, byte segment_id, BLOCK_SIZE bsize)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        TxfmSearchInfo txfm_info = x.txfm_search_info;
        MB_MODE_INFO mi = xd.mi[0];
        ModeCosts mode_costs = x.mode_costs;
        int skip_pred_mv = 0;

        // Initialize variance and distortion (chroma) for all modes and reference
        // frames
        for (int idx = 0; idx < RTC_INTER_MODES; idx++)
        {
            for (int @ref = 0; @ref < REF_FRAMES; @ref++)
            {
                search_state.vars[idx][@ref] = uint.MaxValue;
                search_state.uv_dist[idx][@ref] = long.MaxValue;
            }
        }

        // Initialize values of color sensitivity with sb level color sensitivity
        av1_copy(x.color_sensitivity, x.color_sensitivity_sb);

        init_best_pickmode(search_state.best_pickmode);

        // Estimate cost for single reference frames
        estimate_single_ref_frame_costs(cm, xd, mode_costs, segment_id, bsize,
                                        search_state.ref_costs_single,
                                        is_one_pass_rt_lag_params(cpi));

        // Reset flag to indicate modes evaluated
        av1_zero(search_state.mode_checked);

        txfm_info.skip_txfm = 0;

        // Initialize mode decisions
        av1_invalid_rd_stats(ref search_state.best_rdc);
        av1_invalid_rd_stats(ref search_state.this_rdc);
        av1_invalid_rd_stats(ref rd_cost);
        for (int ref_idx = 0; ref_idx < REF_FRAMES; ++ref_idx)
        {
            x.warp_sample_info[ref_idx].num = -1;
        }

        mi.bsize = bsize;
        mi.ref_frame[0] = NONE_FRAME;
        mi.ref_frame[1] = NONE_FRAME;

        // [subset] CONFIG_AV1_TEMPORAL_DENOISING == 0: av1_denoiser_reset_frame_stats.

        // Populate predicated motion vectors for LAST_FRAME
        if ((cpi.ref_frame_flags & AOM_LAST_FLAG) != 0)
        {
            find_predictors(cpi, x, LAST_FRAME, search_state.frame_mv,
                            search_state.yv12_mb, bsize, force_skip_low_temp_var,
                            x.force_zeromv_skip_for_blk,
                            ref search_state.use_scaled_ref_frame[LAST_FRAME]);
        }
        // Update mask to use all reference frame
        if (is_one_pass_rt_lag_params(cpi) == 0)
        {
            get_ref_frame_use_mask(cpi, x, mi, mi_row, mi_col, bsize, gf_temporal_ref,
                                   search_state.use_ref_frame_mask,
                                   ref force_skip_low_temp_var);
        }
        else
        {
            for (MV_REFERENCE_FRAME ref_frame_iter = LAST_FRAME;
                 ref_frame_iter <= ALTREF_FRAME; ++ref_frame_iter)
            {
                search_state.use_ref_frame_mask[ref_frame_iter] =
                    cpi.ref_frame_flags & (1 << (ref_frame_iter - 1));
            }
        }

        skip_pred_mv = B(x.force_zeromv_skip_for_blk != 0 ||
                         (x.nonrd_prune_ref_frame_search > 2 &&
                          x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] != 2 &&
                          x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] != 2));

        // Populate predicated motion vectors for other single reference frame
        // Start at LAST_FRAME + 1.
        for (MV_REFERENCE_FRAME ref_frame_iter = LAST_FRAME + 1;
             ref_frame_iter <= ALTREF_FRAME; ++ref_frame_iter)
        {
            if (search_state.use_ref_frame_mask[ref_frame_iter] != 0)
            {
                find_predictors(cpi, x, ref_frame_iter, search_state.frame_mv,
                                search_state.yv12_mb, bsize, force_skip_low_temp_var,
                                skip_pred_mv,
                                ref search_state.use_scaled_ref_frame[ref_frame_iter]);
            }
        }
    }

    // Points the prediction planes and scale factors of the block at the given
    // reference frames.
    // PORT-NOTE: `const struct scale_factors *sf_no_scale` points at a local of
    // av1_nonrd_pick_inter_mode_sb(); scale_factors is a class, so the per-thread instance is
    // assigned by reference like the C pointer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_pred_ref_frames(
        AV1_COMMON cm, MACROBLOCKD xd,
        InterModeSearchStateNonrd search_state,
        scale_factors sf_no_scale, MV_REFERENCE_FRAME ref_frame,
        MV_REFERENCE_FRAME ref_frame2)
    {
        int is_single_pred = B(ref_frame2 <= INTRA_FRAME);
        for (int plane = 0; plane < MAX_MB_PLANE; plane++)
        {
            xd.plane[plane].pre[0] = search_state.yv12_mb[ref_frame][plane];
            if (is_single_pred == 0)
                xd.plane[plane].pre[1] = search_state.yv12_mb[ref_frame2][plane];
        }
        set_ref_ptrs(cm, xd, ref_frame, ref_frame2);

        // Check if the scaled reference frame should be used. This is set in the
        // find_predictors() for each usable reference. If so, set the
        // block_ref_scale_factors[] to no reference scaling.
        if (search_state.use_scaled_ref_frame[ref_frame])
        {
            xd.block_ref_scale_factors[0] = sf_no_scale;
        }
        if (is_single_pred == 0 && search_state.use_scaled_ref_frame[ref_frame2])
        {
            xd.block_ref_scale_factors[1] = sf_no_scale;
        }
    }

    // Function to check the inter mode can be skipped based on mode statistics and
    // speed features settings.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool skip_inter_mode_nonrd(
        AV1_COMP cpi, MACROBLOCK x, InterModeSearchStateNonrd search_state,
        ref long thresh_sad_pred, ref int force_mv_inter_layer, ref int is_single_pred,
        ref PREDICTION_MODE this_mode, ref MV_REFERENCE_FRAME last_comp_ref_frame,
        ref MV_REFERENCE_FRAME ref_frame, ref MV_REFERENCE_FRAME ref_frame2, int idx,
        int_mv svc_mv, int force_skip_low_temp_var, uint sse_zeromv_norm,
        int num_inter_modes, byte segment_id, BLOCK_SIZE bsize,
        bool comp_use_zero_zeromv_only, bool check_globalmv)
    {
        AV1_COMMON cm = cpi.common;
        segmentation seg = cm.seg;
        SVC svc = cpi.svc;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mi = xd.mi[0];
        REAL_TIME_SPEED_FEATURES rt_sf = cpi.sf.rt_sf;

        // Skip compound mode based on reference frame mask and type of the mode and
        // for allowed compound modes, setup ref mv stack and reference frame.
        if (idx >= num_inter_modes)
        {
            int comp_index = idx - num_inter_modes;
            COMP_REF_MODE[] comp_modes = is_one_pass_rt_lag_params(cpi) != 0
                                             ? comp_ref_mode_set_full
                                             : comp_ref_mode_set;
            COMP_REF_MODE comp_mode = comp_modes[comp_index];
            this_mode = comp_mode.pred_mode;
            ref_frame = comp_mode.ref_frame[0];
            ref_frame2 = comp_mode.ref_frame[1];
            Debug.Assert(ref_frame == LAST_FRAME);
            Debug.Assert(this_mode == GLOBAL_GLOBALMV || this_mode == NEAREST_NEARESTMV);
            if (setup_compound_params_from_comp_idx(
                    cpi, x, search_state.yv12_mb, ref this_mode, ref ref_frame, ref ref_frame2,
                    search_state.frame_mv, search_state.use_ref_frame_mask,
                    comp_index, comp_use_zero_zeromv_only, ref last_comp_ref_frame,
                    bsize) == 0)
            {
                return true;
            }
            is_single_pred = 0;
        }
        else
        {
            ref_frame2 = NONE_FRAME;
            REF_MODE[] single_modes =
                is_one_pass_rt_lag_params(cpi) != 0 ? ref_mode_set_full : ref_mode_set;
            this_mode = single_modes[idx].pred_mode;
            ref_frame = single_modes[idx].ref_frame;
        }

        if (is_one_pass_rt_lag_params(cpi) != 0 && cpi.rc.is_src_frame_alt_ref != 0)
        {
            if (this_mode != GLOBALMV || ref_frame != ALTREF_FRAME) return true;
        }

        if (cpi.sf.rt_sf.skip_newmv_mode_sad_screen != 0 && cpi.rc.high_source_sad != 0 &&
            x.content_state_sb.source_sad_nonrd >= kMedSad && bsize <= BLOCK_16X16 &&
            x.sb_me_block == 0 && (ref_frame != LAST_FRAME || this_mode == NEWMV))
            return true;

        if (segfeature_active(cm.seg, segment_id, SEG_LVL_SKIP) != 0 &&
            (this_mode != GLOBALMV || ref_frame != LAST_FRAME))
            return true;

        // Skip the mode if use reference frame mask flag is not set.
        if (search_state.use_ref_frame_mask[ref_frame] == 0) return true;

        force_mv_inter_layer = 0;
        if (cpi.ppi.use_svc != 0 && svc.spatial_layer_id > 0 &&
            ((ref_frame == LAST_FRAME && svc.skip_mvsearch_last != 0) ||
             (ref_frame == GOLDEN_FRAME && svc.skip_mvsearch_gf != 0) ||
             (ref_frame == ALTREF_FRAME && svc.skip_mvsearch_altref != 0)))
        {
            // Only test mode if NEARESTMV/NEARMV is (svc_mv.mv.col, svc_mv.mv.row),
            // otherwise set NEWMV to (svc_mv.mv.col, svc_mv.mv.row).
            // Skip newmv and filter search.
            force_mv_inter_layer = 1;
            if (this_mode == NEWMV)
            {
                search_state.frame_mv[this_mode][ref_frame] = svc_mv;
            }
            else if (search_state.frame_mv[this_mode][ref_frame].as_int !=
                     svc_mv.as_int)
            {
                return true;
            }
        }

        // If the segment reference frame feature is enabled then do nothing if the
        // current ref frame is not allowed.
        if (segfeature_active(seg, segment_id, SEG_LVL_REF_FRAME) != 0)
        {
            if (get_segdata(seg, segment_id, SEG_LVL_REF_FRAME) != (int)(ref_frame))
                return true;
            return false;
        }

        // Don't skip non_last references if LAST is not used a reference.
        if ((cpi.ref_frame_flags & AOM_LAST_FLAG) == 0 &&
            (ref_frame == GOLDEN_FRAME || ref_frame == ALTREF_FRAME))
            return false;

        // Skip mode for some modes and reference frames when
        // force_zeromv_skip_for_blk flag is true.
        if (x.force_zeromv_skip_for_blk != 0 &&
            ((!(this_mode == NEARESTMV &&
                search_state.frame_mv[this_mode][ref_frame].as_int == 0) &&
              this_mode != GLOBALMV) ||
             ref_frame != LAST_FRAME))
            return true;

        if (x.sb_me_block != 0 && ref_frame == LAST_FRAME)
        {
            // We want to make sure to test the superblock MV:
            // so don't skip (return false) for NEAREST_LAST or NEAR_LAST if they
            // have this sb MV. And don't skip NEWMV_LAST: this will be set to
            // sb MV in handle_inter_mode_nonrd(), in case NEAREST or NEAR don't
            // have it.
            if (this_mode == NEARESTMV &&
                search_state.frame_mv[NEARESTMV][LAST_FRAME].as_int ==
                    x.sb_me_mv.as_int)
            {
                return false;
            }
            if (this_mode == NEARMV &&
                search_state.frame_mv[NEARMV][LAST_FRAME].as_int ==
                    x.sb_me_mv.as_int)
            {
                return false;
            }
            if (this_mode == NEWMV)
            {
                return false;
            }
        }

        // Skip the single reference mode for which mode check flag is set.
        if (is_single_pred != 0 && search_state.mode_checked[this_mode][ref_frame] != 0)
        {
            return true;
        }

        // Skip GLOBALMV mode if check_globalmv flag is not enabled.
        if (!check_globalmv && this_mode == GLOBALMV)
        {
            return true;
        }

        mi.mode = this_mode;
        mi.ref_frame[0] = ref_frame;
        mi.ref_frame[1] = ref_frame2;

        // Skip compound mode based on variance of previously evaluated single
        // reference modes.
        if (rt_sf.prune_compoundmode_with_singlemode_var && is_single_pred == 0 &&
            is_one_pass_rt_lag_params(cpi) == 0)
        {
            // [subset] prune_compoundmode_with_singlemode_var: false at speed 11 and compound
            // prediction is never set up. (constant at speed 11)
            throw new NotSupportedException(
                "skip_inter_mode_nonrd: prune_compoundmode_with_singlemode_var");
        }

        // For screen content: skip mode testing based on source_sad.
        if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN &&
            x.force_zeromv_skip_for_blk == 0)
        {
            // If source_sad is computed: skip non-zero motion
            // check for stationary (super)blocks. Otherwise if superblock
            // has motion skip the modes with zero motion on last reference
            // for flat blocks, and color is not set.
            // For the latter condition: the same condition should apply
            // to newmv if (0, 0), so this latter condition is repeated
            // below after search_new_mv.
            if (rt_sf.source_metrics_sb_nonrd != 0)
            {
                if ((search_state.frame_mv[this_mode][ref_frame].as_int != 0 &&
                     x.content_state_sb.source_sad_nonrd == kZeroSad) ||
                    (search_state.frame_mv[this_mode][ref_frame].as_int == 0 &&
                     x.block_is_zero_sad == 0 && ref_frame == LAST_FRAME &&
                     ((x.color_sensitivity_sb[COLOR_SENS_IDX(AOM_PLANE_U)] == 0 &&
                       x.color_sensitivity_sb[COLOR_SENS_IDX(AOM_PLANE_V)] == 0) ||
                      cpi.rc.high_source_sad != 0) &&
                     x.source_variance == 0))
                    return true;
            }
            // Skip NEWMV search for flat blocks.
            if (rt_sf.skip_newmv_flat_blocks_screen != 0 && this_mode == NEWMV &&
                x.source_variance < 100)
                return true;
            // Skip non-LAST for color on flat blocks.
            if (ref_frame > LAST_FRAME && x.source_variance == 0 &&
                (x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] == 1 ||
                 x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] == 1))
                return true;
        }

        if (is_one_pass_rt_lag_params(cpi) != 0) return false;

        // Skip mode based on block size, reference frame mode and other block
        // properties.
        if (skip_mode_by_bsize_and_ref_frame(
                this_mode, ref_frame, bsize, x.nonrd_prune_ref_frame_search,
                sse_zeromv_norm, rt_sf.nonrd_aggressive_skip,
                rt_sf.increase_source_sad_thresh) != 0)
            return true;

        // Skip mode based on low temporal variance and souce sad.
        if (skip_mode_by_low_temp(this_mode, ref_frame, bsize, x.content_state_sb,
                                  search_state.frame_mv[this_mode][ref_frame],
                                  force_skip_low_temp_var) != 0)
            return true;

        // Disable this drop out case if the ref frame segment level feature is
        // enabled for this segment. This is to prevent the possibility that we
        // end up unable to pick any mode.
        if (segfeature_active(seg, segment_id, SEG_LVL_REF_FRAME) == 0)
        {
            // Check for skipping GOLDEN and ALTREF based pred_mv_sad.
            if (rt_sf.nonrd_prune_ref_frame_search > 0 &&
                x.pred_mv_sad[ref_frame] != int.MaxValue && ref_frame != LAST_FRAME)
            {
                if ((long)(x.pred_mv_sad[ref_frame]) > thresh_sad_pred) return true;
            }
        }

        // Check for skipping NEARMV based on pred_mv_sad.
        if (this_mode == NEARMV && x.pred_mv1_sad[ref_frame] != int.MaxValue &&
            x.pred_mv1_sad[ref_frame] > (x.pred_mv0_sad[ref_frame] << 1))
            return true;

        // Skip single reference mode based on rd threshold.
        if (is_single_pred != 0)
        {
            if (skip_mode_by_threshold(
                    this_mode, ref_frame,
                    search_state.frame_mv[this_mode][ref_frame],
                    cpi.rc.frames_since_golden, cpi.rd.threshes[segment_id][bsize],
                    x.thresh_freq_fact[bsize], search_state.best_rdc.rdcost,
                    search_state.best_pickmode.best_mode_skip_txfm,
                    (rt_sf.nonrd_aggressive_skip != 0 ? 1 : 0)) != 0)
                return true;
        }
        return false;
    }

    // Function to perform inter mode evaluation for non-rd
    // PORT-NOTE: `PRED_BUFFER **this_mode_pred` is `ref PRED_BUFFER`, `PRED_BUFFER *tmp_buffer`
    // the caller's PRED_BUFFER[4]; `InterPredParams inter_pred_params_sr` is passed by value as
    // in C (a struct copy per call).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool handle_inter_mode_nonrd(
        AV1_COMP cpi, MACROBLOCK x, InterModeSearchStateNonrd search_state,
        PICK_MODE_CONTEXT ctx, ref PRED_BUFFER this_mode_pred,
        PRED_BUFFER[] tmp_buffer, InterPredParams inter_pred_params_sr,
        ref int best_early_term, ref uint sse_zeromv_norm, ref bool check_globalmv,
        int idx, int force_mv_inter_layer, int is_single_pred, int gf_temporal_ref,
        int use_model_yrd_large, int filter_search_enabled_blk, BLOCK_SIZE bsize,
        PREDICTION_MODE this_mode, InterpFilter filt_select,
        int cb_pred_filter_search, int reuse_inter_pred,
        ref int sb_me_has_been_tested)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mi = xd.mi[0];
        MB_MODE_INFO_EXT mbmi_ext = x.mbmi_ext;
        int mi_row = xd.mi_row;
        int mi_col = xd.mi_col;
        macroblockd_plane pd = xd.plane[AOM_PLANE_Y];
        int bw = block_size_wide[bsize];
        InterpFilter filter_ref = cm.features.interp_filter;
        const InterpFilter default_interp_filter = EIGHTTAP_REGULAR;
        TxfmSearchInfo txfm_info = x.txfm_search_info;
        ModeCosts mode_costs = x.mode_costs;
        REAL_TIME_SPEED_FEATURES rt_sf = cpi.sf.rt_sf;
        BEST_PICKMODE best_pickmode = search_state.best_pickmode;

        MV_REFERENCE_FRAME ref_frame = mi.ref_frame[0];
        MV_REFERENCE_FRAME ref_frame2 = mi.ref_frame[1];
        ref int_mv this_mv = ref search_state.frame_mv[this_mode][ref_frame];
        uint var = uint.MaxValue;
        int this_early_term = 0;
        int rate_mv = 0;
        int is_skippable = 0;
        int skip_this_mv = 0;
        uint var_threshold = uint.MaxValue;
        PREDICTION_MODE this_best_mode;
        RD_STATS nonskip_rdc = default;
        av1_invalid_rd_stats(ref nonskip_rdc);

        _ = ctx;

        if (x.sb_me_block != 0 && this_mode == NEWMV && ref_frame == LAST_FRAME)
        {
            // Set the NEWMV_LAST to the sb MV.
            search_state.frame_mv[NEWMV][LAST_FRAME].as_int = x.sb_me_mv.as_int;
        }
        else if (this_mode == NEWMV && force_mv_inter_layer == 0)
        {
            // Find the best motion vector for single/compound mode.
            bool skip_newmv = search_new_mv(
                cpi, x, search_state.frame_mv, ref_frame, gf_temporal_ref, bsize,
                mi_row, mi_col, ref rate_mv, in search_state.best_rdc) != 0;
            // Skip NEWMV mode,
            //   (i). For bsize smaller than 16X16
            //  (ii). Based on sad of the predicted mv w.r.t LAST_FRAME
            // (iii). When motion vector is same as that of reference mv
            if (skip_newmv)
            {
                return true;
            }
        }

        if (this_mode == NEWMV &&
            (force_mv_inter_layer != 0 || (x.sb_me_block != 0 && ref_frame == LAST_FRAME)))
        {
            // When NEWMV reuses the superblock ME vector (x->sb_me_mv, searched
            // relative to (0, 0)) or an inter-layer SVC motion vector, search_new_mv()
            // is bypassed. Clamp the motion vector to the valid subpel search range
            // relative to the block's reference MV predictor (ref_mv) so that the MV
            // delta stays within [MV_LOW, MV_UPP], skip NEWMV if the clamped vector
            // equals ref_mv, and compute its MV bit cost.
            int_mv ref_mv = av1_get_ref_mv(x, 0);
            SubpelMvLimits mv_limits = default;
            av1_set_subpel_mv_search_range(ref mv_limits, in x.mv_limits, in ref_mv.as_mv);
            clamp_mv(ref this_mv.as_mv, in mv_limits);
            if (this_mv.as_int == ref_mv.as_int) return true;
            rate_mv = av1_mv_bit_cost(in this_mv.as_mv, in ref_mv.as_mv,
                                      x.mv_costs.nmv_joint_cost,
                                      x.mv_costs.mv_cost_stack, MV_COST_WEIGHT);
        }

        // Check the current motion vector is same as that of previously evaluated
        // motion vectors.
        for (PREDICTION_MODE inter_mv_mode = NEARESTMV; inter_mv_mode <= NEWMV;
             inter_mv_mode++)
        {
            if (inter_mv_mode == this_mode) continue;
            if (is_single_pred != 0 &&
                search_state.mode_checked[inter_mv_mode][ref_frame] != 0 &&
                this_mv.as_int ==
                    search_state.frame_mv[inter_mv_mode][ref_frame].as_int)
            {
                skip_this_mv = 1;
                break;
            }
        }

        // Skip single mode if current motion vector is same that of previously
        // evaluated motion vectors.
        if (skip_this_mv != 0 && is_single_pred != 0) return true;

        // For screen: for spatially flat blocks with non-zero motion,
        // skip newmv if the motion vector is (0, 0)-LAST, and color is not set.
        if (this_mode == NEWMV && cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN &&
            cpi.svc.spatial_layer_id == 0 && rt_sf.source_metrics_sb_nonrd != 0)
        {
            if (this_mv.as_int == 0 && ref_frame == LAST_FRAME &&
                x.block_is_zero_sad == 0 &&
                ((x.color_sensitivity_sb[COLOR_SENS_IDX(AOM_PLANE_U)] == 0 &&
                  x.color_sensitivity_sb[COLOR_SENS_IDX(AOM_PLANE_V)] == 0) ||
                 cpi.rc.high_source_sad != 0) &&
                x.source_variance == 0)
                return true;
        }

        mi.mode = this_mode;
        mi.mv[0].as_int = this_mv.as_int;
        mi.mv[1].as_int = 0;
        if (is_single_pred == 0)
            mi.mv[1].as_int = search_state.frame_mv[this_mode][ref_frame2].as_int;

        // Set buffers to store predicted samples for reuse
        if (reuse_inter_pred != 0)
        {
            if (this_mode_pred == null)
            {
                this_mode_pred = tmp_buffer[3];
            }
            else
            {
                this_mode_pred = tmp_buffer[get_pred_buffer(tmp_buffer, 3)];
                pd.dst.buf = this_mode_pred.data;
                pd.dst.stride = bw;
            }
        }

        mi.motion_mode = SIMPLE_TRANSLATION;
        // [subset] calc_num_proj_ref (allow_warped_motion): !CONFIG_REALTIME_ONLY.

        // set variance threshold for compound mode pruning
        if (rt_sf.prune_compoundmode_with_singlecompound_var && is_single_pred == 0 &&
            use_model_yrd_large != 0)
        {
            // [subset] compound_ref0_mode / compound_ref1_mode: compound prediction is never
            // set up (setup_compound_params_from_comp_idx), so is_single_pred is always 1.
            throw new NotSupportedException(
                "handle_inter_mode_nonrd: prune_compoundmode_with_singlecompound_var");
        }

        // decide interpolation filter, build prediction signal, get sse
        bool is_mv_subpel =
            (mi.mv[0].as_mv.row & 0x07) != 0 || (mi.mv[0].as_mv.col & 0x07) != 0;
        bool enable_filt_search_this_mode =
            (filter_search_enabled_blk == 2)
                ? true
                : (filter_search_enabled_blk != 0 && force_mv_inter_layer == 0 &&
                   is_single_pred != 0 &&
                   (ref_frame == LAST_FRAME || x.nonrd_prune_ref_frame_search == 0));
        if (is_mv_subpel && enable_filt_search_this_mode)
        {
            // [subset] search_filter_ref: filter_search_enabled_blk is always 0 at speed 11
            // (rt_sf.use_nonrd_filter_search == 0). (constant at speed 11)
            throw new NotSupportedException("handle_inter_mode_nonrd: search_filter_ref");
        }
        // [subset] search_motion_mode (allow_warped_motion && NEWMV): !CONFIG_REALTIME_ONLY.
        else
        {
            mi.interp_filters =
                (filter_ref == SWITCHABLE)
                    ? av1_broadcast_interp_filter(default_interp_filter)
                    : av1_broadcast_interp_filter(filter_ref);
            if (force_mv_inter_layer != 0)
                mi.interp_filters = av1_broadcast_interp_filter(EIGHTTAP_REGULAR);

            // If it is sub-pel motion and cb_pred_filter_search is enabled, select
            // the pre-decided filter
            if (is_mv_subpel && cb_pred_filter_search != 0)
                mi.interp_filters = av1_broadcast_interp_filter(filt_select);

            if (is_single_pred != 0)
            {
                SubpelParams subpel_params = default;
                // Initialize inter mode level params for single reference mode.
                init_inter_mode_params(in mi.mv[0].as_mv, ref inter_pred_params_sr,
                                       ref subpel_params, xd.block_ref_scale_factors[0],
                                       pd.pre[0].width, pd.pre[0].height);
                av1_enc_build_inter_predictor_y_nonrd(xd, ref inter_pred_params_sr,
                                                      in subpel_params);
            }
            else
            {
                av1_enc_build_inter_predictor(cm, xd, mi_row, mi_col, Ptr<BUFFER_SET>.Null,
                                              bsize, AOM_PLANE_Y, AOM_PLANE_Y);
            }

            if (use_model_yrd_large != 0)
            {
                model_skip_for_sb_y_large(cpi, bsize, mi_row, mi_col, x, xd,
                                          ref search_state.this_rdc, ref this_early_term, 0,
                                          best_pickmode.best_sse, ref var, var_threshold);
            }
            else
            {
                model_rd_for_sb_y(cpi, bsize, x, xd, ref search_state.this_rdc, ref var, 0,
                                  ref this_early_term);
            }
        }

        // update variance for single mode
        if (is_single_pred != 0)
        {
            search_state.vars[INTER_OFFSET(this_mode)][ref_frame] = var;
            if (this_mv.as_int == 0)
            {
                search_state.vars[INTER_OFFSET(GLOBALMV)][ref_frame] = var;
            }
        }
        // prune compound mode based on single mode var threshold
        if (is_single_pred == 0 && var > var_threshold)
        {
            if (reuse_inter_pred != 0) free_pred_buffer(this_mode_pred);
            return true;
        }

        if (ref_frame == LAST_FRAME && this_mv.as_int == 0)
        {
            sse_zeromv_norm = (uint)(search_state.this_rdc.sse >>
                                     (b_width_log2_lookup[bsize] +
                                      b_height_log2_lookup[bsize]));
        }

        // Perform early termination based on sse.
        if (rt_sf.sse_early_term_inter_search != 0 &&
            early_term_inter_search_with_sse(rt_sf.sse_early_term_inter_search,
                                             bsize, search_state.this_rdc.sse,
                                             best_pickmode.best_sse, this_mode) != 0)
        {
            if (reuse_inter_pred != 0) free_pred_buffer(this_mode_pred);
            return true;
        }

        int skip_ctx = av1_get_skip_txfm_context(xd);
        int skip_txfm_cost = mode_costs.skip_txfm_cost[skip_ctx][1];
        int no_skip_txfm_cost = mode_costs.skip_txfm_cost[skip_ctx][0];
        long sse_y = search_state.this_rdc.sse;

        if (this_early_term != 0)
        {
            search_state.this_rdc.skip_txfm = 1;
            search_state.this_rdc.rate = skip_txfm_cost;
            search_state.this_rdc.dist = search_state.this_rdc.sse << 4;
        }
        else
        {
            // Calculates RD Cost using Hadamard transform.
            av1_block_yrd(x, ref search_state.this_rdc, ref is_skippable, bsize,
                          mi.tx_size);
            if (search_state.this_rdc.skip_txfm != 0 ||
                RDCOST(x.rdmult, search_state.this_rdc.rate,
                       search_state.this_rdc.dist) >=
                    RDCOST(x.rdmult, 0, search_state.this_rdc.sse))
            {
                if (search_state.this_rdc.skip_txfm == 0)
                {
                    // Need to store "real" rdc for possible future use if UV rdc
                    // disallows tx skip
                    nonskip_rdc = search_state.this_rdc;
                    nonskip_rdc.rate += no_skip_txfm_cost;
                }
                search_state.this_rdc.rate = skip_txfm_cost;
                search_state.this_rdc.skip_txfm = 1;
                search_state.this_rdc.dist = search_state.this_rdc.sse;
            }
            else
            {
                search_state.this_rdc.rate += no_skip_txfm_cost;
            }

            // Populate predicted sample for chroma planes based on color sensitivity.
            if ((x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] != 0 ||
                 x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] != 0))
            {
                RD_STATS rdc_uv = default;
                BLOCK_SIZE uv_bsize =
                    get_plane_block_size(bsize, xd.plane[AOM_PLANE_U].subsampling_x,
                                         xd.plane[AOM_PLANE_U].subsampling_y);
                if (x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] != 0)
                {
                    av1_enc_build_inter_predictor(cm, xd, mi_row, mi_col, Ptr<BUFFER_SET>.Null,
                                                  bsize, AOM_PLANE_U, AOM_PLANE_U);
                }
                if (x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] != 0)
                {
                    av1_enc_build_inter_predictor(cm, xd, mi_row, mi_col, Ptr<BUFFER_SET>.Null,
                                                  bsize, AOM_PLANE_V, AOM_PLANE_V);
                }
                // Compute sse for chroma planes.
                long sse_uv = av1_model_rd_for_sb_uv(
                    cpi, uv_bsize, x, xd, ref rdc_uv, AOM_PLANE_U, AOM_PLANE_V);
                if (rdc_uv.dist < x.min_dist_inter_uv)
                    x.min_dist_inter_uv = rdc_uv.dist;
                search_state.this_rdc.sse += sse_uv;
                // Restore Y rdc if UV rdc disallows txfm skip
                if (search_state.this_rdc.skip_txfm != 0 && rdc_uv.skip_txfm == 0 &&
                    nonskip_rdc.rate != int.MaxValue)
                    search_state.this_rdc = nonskip_rdc;
                if (is_single_pred != 0)
                {
                    search_state.uv_dist[INTER_OFFSET(this_mode)][ref_frame] = rdc_uv.dist;
                }
                search_state.this_rdc.rate += rdc_uv.rate;
                search_state.this_rdc.dist += rdc_uv.dist;
                search_state.this_rdc.skip_txfm =
                    (byte)B(search_state.this_rdc.skip_txfm != 0 && rdc_uv.skip_txfm != 0);
            }
        }

        this_best_mode = this_mode;
        // TODO(kyslov) account for UV prediction cost
        search_state.this_rdc.rate += rate_mv;
        if (is_single_pred == 0)
        {
            short mode_ctx =
                av1_mode_context_analyzer(mbmi_ext.mode_context, mi.ref_frame);
            search_state.this_rdc.rate += cost_mv_ref(mode_costs, this_mode, mode_ctx);
        }
        else
        {
            // If the current mode has zeromv but is not GLOBALMV, compare the rate
            // cost. If GLOBALMV is cheaper, use GLOBALMV instead.
            if (this_mode != GLOBALMV &&
                this_mv.as_int == search_state.frame_mv[GLOBALMV][ref_frame].as_int)
            {
                if (is_globalmv_better(this_mode, ref_frame, rate_mv, mode_costs,
                                       search_state.single_inter_mode_costs, mbmi_ext))
                {
                    this_best_mode = GLOBALMV;
                }
            }

            search_state.this_rdc.rate +=
                search_state
                    .single_inter_mode_costs[INTER_OFFSET(this_best_mode)][ref_frame];
        }

        if (is_single_pred != 0 && this_mv.as_int == 0 && var < uint.MaxValue)
        {
            search_state.vars[INTER_OFFSET(GLOBALMV)][ref_frame] = var;
        }

        search_state.this_rdc.rate += (int)search_state.ref_costs_single[ref_frame];

        search_state.this_rdc.rdcost = RDCOST(x.rdmult, search_state.this_rdc.rate,
                                              search_state.this_rdc.dist);
        if (cpi.oxcf.rc_cfg.mode == AOM_CBR && is_single_pred != 0)
        {
            newmv_diff_bias(xd, this_best_mode, ref search_state.this_rdc, bsize,
                            search_state.frame_mv[this_best_mode][ref_frame].as_mv.row,
                            search_state.frame_mv[this_best_mode][ref_frame].as_mv.col,
                            cpi.speed, x.source_variance, x.content_state_sb);
        }

        // [subset] CONFIG_AV1_TEMPORAL_DENOISING == 0: av1_denoiser_update_frame_stats.
        _ = sse_y;

        search_state.mode_checked[this_mode][ref_frame] = 1;
        search_state.mode_checked[this_best_mode][ref_frame] = 1;

        if (check_globalmv)
        {
            int abs_mv =
                Math.Abs((int)search_state.frame_mv[this_best_mode][ref_frame].as_mv.row) +
                Math.Abs((int)search_state.frame_mv[this_best_mode][ref_frame].as_mv.col);
            // Early exit check: if the magnitude of this_best_mode's mv is small
            // enough, we skip GLOBALMV check in the next loop iteration.
            if (abs_mv < 2)
            {
                check_globalmv = false;
            }
        }

        if (x.sb_me_block != 0 && ref_frame == LAST_FRAME &&
            search_state.frame_mv[this_best_mode][ref_frame].as_int ==
                x.sb_me_mv.as_int)
            sb_me_has_been_tested = 1;

        // Copy best mode params to search state
        if (search_state.this_rdc.rdcost < search_state.best_rdc.rdcost)
        {
            search_state.best_rdc = search_state.this_rdc;
            best_early_term = this_early_term;
            update_search_state_nonrd(search_state, mi, txfm_info, in nonskip_rdc,
                                      this_best_mode, sse_y);

            // This is needed for the compound modes.
            search_state.frame_mv_best[this_best_mode][ref_frame].as_int =
                search_state.frame_mv[this_best_mode][ref_frame].as_int;
            if (ref_frame2 > NONE_FRAME)
            {
                search_state.frame_mv_best[this_best_mode][ref_frame2].as_int =
                    search_state.frame_mv[this_best_mode][ref_frame2].as_int;
            }

            if (reuse_inter_pred != 0)
            {
                free_pred_buffer(best_pickmode.best_pred);
                best_pickmode.best_pred = this_mode_pred;
            }
        }
        else
        {
            if (reuse_inter_pred != 0) free_pred_buffer(this_mode_pred);
        }

        if (best_early_term != 0 && (idx > 0 || rt_sf.nonrd_aggressive_skip != 0))
        {
            txfm_info.skip_txfm = 1;
            if (x.sb_me_block == 0 || sb_me_has_been_tested != 0) return false;
        }
        return true;
    }

    // Function to perform screen content mode evaluation for non-rd
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void handle_screen_content_mode_nonrd(
        AV1_COMP cpi, MACROBLOCK x, InterModeSearchStateNonrd search_state,
        PRED_BUFFER this_mode_pred, PICK_MODE_CONTEXT ctx,
        PRED_BUFFER[] tmp_buffer, in buf_2d orig_dst, int skip_idtx_palette,
        bool try_palette, BLOCK_SIZE bsize, int reuse_inter_pred, int mi_col,
        int mi_row)
    {
        AV1_COMMON cm = cpi.common;
        REAL_TIME_SPEED_FEATURES rt_sf = cpi.sf.rt_sf;
        BEST_PICKMODE best_pickmode = search_state.best_pickmode;

        // TODO(marpan): Only allow for 8 bit-depth for now, re-enable for 10/12 bit
        // when issue 3359 is fixed.
        if (cm.seq_params.bit_depth == 8 && rt_sf.use_idtx_nonrd != 0 &&
            skip_idtx_palette == 0 && !cpi.oxcf.txfm_cfg.use_inter_dct_only &&
            x.force_zeromv_skip_for_blk == 0 &&
            is_inter_mode(best_pickmode.best_mode) != 0 &&
            best_pickmode.best_pred != null &&
            (rt_sf.prune_idtx_nonrd == 0 ||
             (rt_sf.prune_idtx_nonrd != 0 && bsize <= BLOCK_32X32 &&
              best_pickmode.best_mode_skip_txfm != 1 && x.source_variance > 200)))
        {
            // [subset] identity transform search (av1_block_yrd_idtx): rt_sf.use_idtx_nonrd is
            // only enabled for screen content. (constant at speed 11)
            _ = this_mode_pred;
            _ = ctx;
            _ = tmp_buffer;
            _ = mi_col;
            _ = mi_row;
            throw new NotSupportedException("handle_screen_content_mode_nonrd: idtx");
        }

        if (!try_palette) return;

        // [subset] av1_search_palette_mode_luma (palette search in inter frames):
        // enable_palette() returns false outside screen content (av1_allow_palette() needs
        // features.allow_screen_content_tools).
        _ = orig_dst;
        _ = reuse_inter_pred;
        throw new NotSupportedException("handle_screen_content_mode_nonrd: palette");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool enable_palette(AV1_COMP cpi, bool is_mode_intra,
                                       BLOCK_SIZE bsize,
                                       uint source_variance,
                                       int force_zeromv_skip, int skip_idtx_palette,
                                       int force_palette_test,
                                       uint best_intra_sad_norm)
    {
        uint sad_thresh =
            cpi.sf.rt_sf.prune_palette_search_nonrd > 2
                ? (cpi.oxcf.frm_dim_cfg.width * cpi.oxcf.frm_dim_cfg.height <=
                   1280 * 720)
                      ? 6u
                      : 12u
                : 10u;
        if (!cpi.oxcf.tool_cfg.enable_palette) return false;
        if (av1_allow_palette(B(cpi.common.features.allow_screen_content_tools),
                              bsize) == 0)
        {
            return false;
        }
        if (skip_idtx_palette != 0) return false;

        if (cpi.sf.rt_sf.prune_palette_search_nonrd > 1 &&
            ((cpi.rc.high_source_sad != 0 && cpi.ppi.rtc_ref.non_reference_frame != 0) ||
             bsize > BLOCK_16X16))
        {
            return false;
        }

        if (prune_palette_testing_inter(cpi, source_variance) &&
            best_intra_sad_norm < sad_thresh)
            return false;

        if ((is_mode_intra || force_palette_test != 0) && source_variance > 0 &&
            force_zeromv_skip == 0 &&
            (cpi.rc.high_source_sad != 0 || source_variance > 300))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    // PORT-NOTE: never executed in the subset (SEG_LVL_SKIP is not used: cyclic refresh only
    // sets SEG_LVL_ALT_Q). Ported because it is cheap and partition_search.c calls it on a
    // data-dependent branch; the calls it makes that only matter for OBMC / warped motion are
    // [subset] throws.
    internal static void av1_nonrd_pick_inter_mode_sb_seg_skip(AV1_COMP cpi, MACROBLOCK x,
                                                               ref RD_STATS rd_cost,
                                                               BLOCK_SIZE bsize,
                                                               PICK_MODE_CONTEXT ctx)
    {
        AV1_COMMON cm = cpi.common;
        FeatureFlags features = cm.features;
        segmentation seg = cm.seg;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mi = xd.mi[0];
        byte segment_id = mi.segment_id;

        Debug.Assert(segfeature_active(seg, segment_id, SEG_LVL_SKIP) != 0);

        // The decoder infers the mode, the reference and skip for this segment.
        MV_REFERENCE_FRAME ref_frame =
            segfeature_active(seg, segment_id, SEG_LVL_REF_FRAME) != 0
                ? (MV_REFERENCE_FRAME)get_segdata(seg, segment_id, SEG_LVL_REF_FRAME)
                : LAST_FRAME;
        Debug.Assert(ref_frame > INTRA_FRAME);

        // av1_update_state() expects the block size of this pick.
        mi.bsize = bsize;
        init_mbmi_nonrd(mi, GLOBALMV, ref_frame, NONE_FRAME, cm);
        mi.skip_mode = 0;
        mi.mv[0].as_int =
            gm_get_motion_vector(in cm.global_motion[ref_frame],
                                 B(features.allow_high_precision_mv), bsize, xd.mi_col,
                                 xd.mi_row, B(features.cur_frame_force_integer_mv))
                .as_int;
        mi.angle_delta[PLANE_TYPE_Y] = 0;
        mi.angle_delta[PLANE_TYPE_UV] = 0;

        // The decoder still reads the motion mode, so count what it counts.
        if (features.switchable_motion_mode)
        {
            // [subset] av1_count_overlappable_neighbors (OBMC / warped motion):
            // switchable_motion_mode stays 0 for RT nonrd.
            throw new NotSupportedException(
                "av1_nonrd_pick_inter_mode_sb_seg_skip: switchable_motion_mode");
        }
        mi.num_proj_ref = 0;
        if (warped_motion_update_num_proj_ref(cpi, mi) != 0)
        {
            // [subset] av1_findSamples: warped motion is disabled in the subset.
            throw new NotSupportedException(
                "av1_nonrd_pick_inter_mode_sb_seg_skip: av1_findSamples");
        }

        // encode_superblock() sets the final transform size of skipped blocks.
        mi.tx_size = max_txsize_lookup[bsize];
        Array.Fill(mi.inter_tx_size, mi.tx_size);
        memset(ctx.tx_type_map, (byte)DCT_DCT, ctx.num_4x4_blk);
        x.txfm_search_info.skip_txfm = 1;
        av1_init_rd_stats(ref rd_cost);

        store_coding_context_nonrd(x, ctx);
    }

    // AV1 inter mode selection based on Non-RD optimized model.
    //
    // Top level function for Non-RD optimized inter mode selection.
    // This finction will loop over subset of inter modes and select the best one
    // based on calculated modelled RD cost. While making decisions which modes to
    // check, this function applies heuristics based on previously checked modes,
    // block residual variance, block size, and other factors to prune certain
    // modes and reference frames. Currently only single reference frame modes
    // are checked. Additional heuristics are applied to decide if intra modes
    // need to be checked.
    //
    // cpi            Top-level encoder structure
    // tile_data      Pointer to struct holding adaptive
    //                data/contexts/models for the tile during
    //                encoding
    // x              Pointer to structure holding all the data for
    //                the current macroblock
    // rd_cost        Struct to keep track of the RD information
    // bsize          Current block size
    // ctx            Structure to hold snapshot of coding context
    //                during the mode picking process
    //
    // Nothing is returned. Instead, the MB_MODE_INFO struct inside x
    // is modified to store information about the best mode computed
    // in this function. The rd_cost struct is also updated with the RD stats
    // corresponding to the best mode found.
    internal static void av1_nonrd_pick_inter_mode_sb(AV1_COMP cpi, TileDataEnc tile_data,
                                                      MACROBLOCK x, ref RD_STATS rd_cost,
                                                      BLOCK_SIZE bsize, PICK_MODE_CONTEXT ctx)
    {
        AV1_COMMON cm = cpi.common;
        SVC svc = cpi.svc;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mi = xd.mi[0];
        segmentation seg = cm.seg;
        macroblockd_plane pd = xd.plane[AOM_PLANE_Y];
        MB_MODE_INFO_EXT mbmi_ext = x.mbmi_ext;
        MV_REFERENCE_FRAME ref_frame = 0, ref_frame2 = 0;
        byte segment_id = mi.segment_id;
        int best_early_term = 0;
        int force_skip_low_temp_var = 0;
        uint sse_zeromv_norm = uint.MaxValue;
        int num_inter_modes =
            is_one_pass_rt_lag_params(cpi) != 0 ? NUM_INTER_MODES_FULL : NUM_INTER_MODES;
        REAL_TIME_SPEED_FEATURES rt_sf = cpi.sf.rt_sf;
        bool check_globalmv = rt_sf.check_globalmv_on_single_ref;
        NONRD_SCRATCH scratch = x.nonrd_scratch;
        // PRED_BUFFER tmp_buffer[4] / uint8_t pred_buf[MAX_MB_PLANE * MAX_SB_SQUARE]
        // (per-thread scratch; uninitialised in C unless reuse_inter_pred).
        PRED_BUFFER[] tmp_buffer = scratch.tmp_buffer;
        byte[] pred_buf = scratch.pred_buf;
        PRED_BUFFER this_mode_pred = null;
        int reuse_inter_pred =
            B(rt_sf.reuse_inter_pred_nonrd != 0 && cm.seq_params.bit_depth == AOM_BITS_8);
        // InterModeSearchStateNonrd search_state (per-thread scratch). C only zeroes the two
        // masks below; every other field is initialised in set_params_nonrd_pick_inter_mode()
        // / find_predictors() / fill_single_inter_mode_costs() before it is read.
        InterModeSearchStateNonrd search_state = scratch.search_state;
        av1_zero(search_state.use_ref_frame_mask);
        av1_zero(search_state.use_scaled_ref_frame);
        BEST_PICKMODE best_pickmode = search_state.best_pickmode;
        _ = tile_data;

        int bh = block_size_high[bsize];
        int bw = block_size_wide[bsize];
        int pixels_in_block = bh * bw;
        buf_2d orig_dst = pd.dst;
        TxfmSearchParams txfm_params = x.txfm_search_params;
        TxfmSearchInfo txfm_info = x.txfm_search_info;
        long thresh_sad_pred = long.MaxValue;
        int mi_row = xd.mi_row;
        int mi_col = xd.mi_col;
        int_mv svc_mv = default;  // { .as_int = 0 }
        int force_mv_inter_layer = 0;
        bool comp_use_zero_zeromv_only = false;
        int tot_num_comp_modes = is_one_pass_rt_lag_params(cpi) != 0
                                     ? NUM_COMP_INTER_MODES_RT_FULL
                                     : NUM_COMP_INTER_MODES_RT;
        // [subset] CONFIG_AV1_TEMPORAL_DENOISING == 0: denoiser locals.
        ModeCosts mode_costs = x.mode_costs;
        scale_factors sf_no_scale = scratch.sf_no_scale;
        av1_setup_scale_factors_for_frame(sf_no_scale, cm.width, cm.height,
                                          cm.width, cm.height);
        if (reuse_inter_pred != 0)
        {
            for (int buf_idx = 0; buf_idx < 3; buf_idx++)
            {
                tmp_buffer[buf_idx].data = new Ptr<byte>(pred_buf, pixels_in_block * buf_idx);
                tmp_buffer[buf_idx].stride = bw;
                tmp_buffer[buf_idx].in_use = 0;
            }
            tmp_buffer[3].data = pd.dst.buf;
            tmp_buffer[3].stride = pd.dst.stride;
            tmp_buffer[3].in_use = 0;
        }

        int gf_temporal_ref = is_same_gf_and_last_scale(cm);

        // If the lower spatial layer uses an averaging filter for downsampling
        // (phase = 8), the target decimated pixel is shifted by (1/2, 1/2) relative
        // to source, so use subpel motion vector to compensate. The nonzero motion
        // is half pixel shifted to left and top, so (-4, -4). This has more effect
        // on higher resolutions, so condition it on that for now.
        // Exclude quality layers, which have the same resolution and hence no shift.
        if (cpi.ppi.use_svc != 0 && svc.spatial_layer_id > 0 &&
            svc.has_lower_quality_layer == 0 &&
            svc.downsample_filter_phase[svc.spatial_layer_id - 1] == 8 &&
            cm.width * cm.height > 640 * 480)
        {
            svc_mv.as_mv.row = -4;
            svc_mv.as_mv.col = -4;
        }

        // Setup parameters used for inter mode evaluation.
        set_params_nonrd_pick_inter_mode(cpi, x, search_state, ref rd_cost,
                                         ref force_skip_low_temp_var, mi_row, mi_col,
                                         gf_temporal_ref, segment_id, bsize);

        if (rt_sf.use_comp_ref_nonrd != 0 && is_comp_ref_allowed(bsize) != 0)
        {
            // Only search compound if bsize \gt BLOCK_16X16.
            if (bsize > BLOCK_16X16)
            {
                comp_use_zero_zeromv_only = rt_sf.check_only_zero_zeromv_on_large_blocks;
            }
            else
            {
                tot_num_comp_modes = 0;
            }
        }
        else
        {
            tot_num_comp_modes = 0;
        }

        // No compound if SEG_LVL_REF_FRAME is set.
        if (segfeature_active(seg, segment_id, SEG_LVL_REF_FRAME) != 0)
            tot_num_comp_modes = 0;

        if (x.pred_mv_sad[LAST_FRAME] != int.MaxValue)
        {
            thresh_sad_pred = ((long)x.pred_mv_sad[LAST_FRAME]) << 1;
            // Increase threshold for less aggressive pruning.
            if (rt_sf.nonrd_prune_ref_frame_search == 1)
                thresh_sad_pred += (x.pred_mv_sad[LAST_FRAME] >> 2);
        }

        int use_model_yrd_large = get_model_rd_flag(cpi, xd, bsize);

        // decide block-level interp filter search flags:
        // filter_search_enabled_blk:
        // 0: disabled
        // 1: filter search depends on mode properties
        // 2: filter search forced since prediction is unreliable
        // cb_pred_filter_search 0: disabled cb prediction
        InterpFilter filt_select = EIGHTTAP_REGULAR;
        int cb_pred_filter_search =
            x.content_state_sb.source_sad_nonrd > kVeryLowSad
                ? cpi.sf.interp_sf.cb_pred_filter_search
                : 0;
        int filter_search_enabled_blk =
            is_filter_search_enabled_blk(cpi, x, mi_row, mi_col, bsize, segment_id,
                                         cb_pred_filter_search, ref filt_select);

        init_mbmi_nonrd(mi, DC_PRED, NONE_FRAME, NONE_FRAME, cm);
        mi.tx_size = AOMMIN(
            AOMMIN(max_txsize_lookup[bsize],
                   tx_mode_to_biggest_tx_size[txfm_params.tx_mode_search_type]),
            TX_16X16);

        if (is_one_pass_rt_lag_params(cpi) == 0)
            fill_single_inter_mode_costs(search_state.single_inter_mode_costs,
                                         num_inter_modes, ref_mode_set, mode_costs,
                                         mbmi_ext.mode_context);
        else
            fill_single_inter_mode_costs(search_state.single_inter_mode_costs,
                                         num_inter_modes, ref_mode_set_full, mode_costs,
                                         mbmi_ext.mode_context);

        MV_REFERENCE_FRAME last_comp_ref_frame = NONE_FRAME;

        // Initialize inter prediction params at block level for single reference
        // mode.
        InterPredParams inter_pred_params_sr = default;
        init_inter_block_params(ref inter_pred_params_sr, pd.width, pd.height,
                                mi_row * MI_SIZE, mi_col * MI_SIZE, pd.subsampling_x,
                                pd.subsampling_y, xd.bd, is_cur_buf_hbd(xd),
                                /*is_intrabc=*/0);
        inter_pred_params_sr.conv_params =
            get_conv_params(/*do_average=*/0, AOM_PLANE_Y, xd.bd);

        x.block_is_zero_sad = B(x.content_state_sb.source_sad_nonrd == kZeroSad ||
                                segfeature_active(cm.seg, segment_id, SEG_LVL_SKIP) != 0);
        if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN &&
            x.force_zeromv_skip_for_blk == 0 &&
            x.content_state_sb.source_sad_nonrd != kZeroSad &&
            x.source_variance == 0 && bsize < cm.seq_params.sb_size &&
            search_state.use_ref_frame_mask[LAST_FRAME] != 0 &&
            search_state.yv12_mb[LAST_FRAME][0].width == cm.width &&
            search_state.yv12_mb[LAST_FRAME][0].height == cm.height)
        {
            set_block_source_sad(cpi, x, bsize, in search_state.yv12_mb[LAST_FRAME][0]);
        }

        int sb_me_has_been_tested = 0;
        x.sb_me_block = x.sb_me_partition;
        // Only use this feature (force testing of superblock motion) if coding
        // block size is large.
        if (x.sb_me_block != 0)
        {
            if (cm.seq_params.sb_size == BLOCK_128X128 && bsize < BLOCK_64X64)
                x.sb_me_block = 0;
            else if (cm.seq_params.sb_size == BLOCK_64X64 && bsize < BLOCK_32X32)
                x.sb_me_block = 0;
        }

        x.min_dist_inter_uv = long.MaxValue;
        for (int idx = 0; idx < num_inter_modes + tot_num_comp_modes; ++idx)
        {
            // If we are at the first compound mode, and the single modes already
            // perform well, then end the search.
            if (rt_sf.skip_compound_based_on_var && idx == num_inter_modes &&
                skip_comp_based_on_var(search_state.vars, bsize))
            {
                break;
            }

            int is_single_pred = 1;
            PREDICTION_MODE this_mode = 0;

            if (idx == 0 && x.force_zeromv_skip_for_blk == 0)
            {
                // Set color sensitivity on first tested mode only.
                // Use y-sad already computed in find_predictors: take the sad with motion
                // vector closest to 0; the uv-sad computed below in set_color_sensitivity
                // is for zeromv.
                // For screen: first check if golden reference is being used, if so,
                // force color_sensitivity on (=1) if the color sensitivity for sb_g is 1.
                // The check in set_color_sensitivity() will then follow and check for
                // setting the flag if the level is still 2 or 0.
                if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN &&
                    search_state.use_ref_frame_mask[GOLDEN_FRAME] != 0)
                {
                    if (x.color_sensitivity_sb_g[COLOR_SENS_IDX(AOM_PLANE_U)] == 1)
                        x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] = 1;
                    if (x.color_sensitivity_sb_g[COLOR_SENS_IDX(AOM_PLANE_V)] == 1)
                        x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] = 1;
                }
                if (search_state.use_ref_frame_mask[LAST_FRAME] != 0 &&
                    x.pred_mv0_sad[LAST_FRAME] != int.MaxValue)
                {
                    int y_sad = x.pred_mv0_sad[LAST_FRAME];
                    if (x.pred_mv1_sad[LAST_FRAME] != int.MaxValue &&
                        (Math.Abs((int)search_state.frame_mv[NEARMV][LAST_FRAME].as_mv.col) +
                         Math.Abs((int)search_state.frame_mv[NEARMV][LAST_FRAME].as_mv.row)) <
                            (Math.Abs((int)search_state.frame_mv[NEARESTMV][LAST_FRAME].as_mv.col) +
                             Math.Abs((int)search_state.frame_mv[NEARESTMV][LAST_FRAME].as_mv.row)))
                        y_sad = x.pred_mv1_sad[LAST_FRAME];
                    set_color_sensitivity(cpi, x, bsize, y_sad, x.source_variance,
                                          search_state.yv12_mb[LAST_FRAME]);
                }
            }

            // Check the inter mode can be skipped based on mode statistics and speed
            // features settings.
            if (skip_inter_mode_nonrd(cpi, x, search_state, ref thresh_sad_pred,
                                      ref force_mv_inter_layer, ref is_single_pred,
                                      ref this_mode, ref last_comp_ref_frame, ref ref_frame,
                                      ref ref_frame2, idx, svc_mv, force_skip_low_temp_var,
                                      sse_zeromv_norm, num_inter_modes, segment_id,
                                      bsize, comp_use_zero_zeromv_only, check_globalmv))
                continue;

            // Select prediction reference frames.
            mi.ref_frame[0] = ref_frame;
            mi.ref_frame[1] = ref_frame2;
            set_pred_ref_frames(cm, xd, search_state, sf_no_scale, ref_frame,
                                ref_frame2);

            // Perform inter mode evaluation for non-rd
            if (!handle_inter_mode_nonrd(
                    cpi, x, search_state, ctx, ref this_mode_pred, tmp_buffer,
                    inter_pred_params_sr, ref best_early_term, ref sse_zeromv_norm,
                    ref check_globalmv,
                    idx, force_mv_inter_layer, is_single_pred, gf_temporal_ref,
                    use_model_yrd_large, filter_search_enabled_blk, bsize, this_mode,
                    filt_select, cb_pred_filter_search, reuse_inter_pred,
                    ref sb_me_has_been_tested))
            {
                break;
            }
        }

        // Restore mode data of best inter mode
        mi.mode = best_pickmode.best_mode;
        mi.motion_mode = best_pickmode.best_motion_mode;
        mi.wm_params = best_pickmode.wm_params;
        mi.num_proj_ref = (byte)best_pickmode.num_proj_ref;
        mi.interp_filters = best_pickmode.best_pred_filter;
        mi.tx_size = best_pickmode.best_tx_size;
        Array.Fill(mi.inter_tx_size, mi.tx_size);
        mi.ref_frame[0] = best_pickmode.best_ref_frame;
        mi.ref_frame[1] = best_pickmode.best_second_ref_frame;
        // PORT-NOTE: frame_mv_best is not initialised in C either; for an inter best mode
        // the entry read here was written when that mode became best.
        mi.mv[0].as_int = search_state
                              .frame_mv_best[best_pickmode.best_mode]
                                            [best_pickmode.best_ref_frame]
                              .as_int;
        mi.mv[1].as_int = 0;
        if (best_pickmode.best_second_ref_frame > INTRA_FRAME)
        {
            mi.mv[1].as_int = search_state
                                  .frame_mv_best[best_pickmode.best_mode]
                                                [best_pickmode.best_second_ref_frame]
                                  .as_int;
        }
        // The chroma checks below predict from the planes of the best mode.
        if (search_state.best_rdc.rdcost != long.MaxValue &&
            (x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] != 0 ||
             x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] != 0))
        {
            set_pred_ref_frames(cm, xd, search_state, sf_no_scale,
                                best_pickmode.best_ref_frame,
                                best_pickmode.best_second_ref_frame);
        }
        // Perform intra prediction search, if the best SAD is above a certain
        // threshold.
        mi.angle_delta[PLANE_TYPE_Y] = 0;
        mi.angle_delta[PLANE_TYPE_UV] = 0;
        mi.filter_intra_mode_info.use_filter_intra = 0;

        int force_palette_test = 0;
        if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN &&
            x.content_state_sb.source_sad_nonrd != kZeroSad &&
            bsize <= BLOCK_16X16)
        {
            uint thresh_sse = cpi.rc.high_source_sad != 0 ? 15000u : 100000u;
            uint thresh_source_var = cpi.rc.high_source_sad != 0 ? 50u : 200u;
            uint best_sse_inter_motion =
                (uint)(search_state.best_rdc.sse >>
                       (b_width_log2_lookup[bsize] +
                        b_height_log2_lookup[bsize]));
            if (best_sse_inter_motion > thresh_sse &&
                x.source_variance > thresh_source_var)
                force_palette_test = 1;
        }

        // For the SEG_LVL_REF_FRAME inter_mode must be selected if reference set is
        // not INTRA_FRAME, so skip all intra mode (and palette below).
        int inter_forced_on_segment =
            B(segfeature_active(seg, segment_id, SEG_LVL_REF_FRAME) != 0 &&
              get_segdata(seg, segment_id, SEG_LVL_REF_FRAME) != INTRA_FRAME);

        // Evaluate Intra modes in inter frame
        uint best_intra_sad_norm = uint.MaxValue;
        if (x.force_zeromv_skip_for_blk == 0 && inter_forced_on_segment == 0)
            av1_estimate_intra_mode(cpi, x, bsize, best_early_term,
                                    search_state.ref_costs_single[INTRA_FRAME],
                                    reuse_inter_pred, in orig_dst, tmp_buffer,
                                    ref this_mode_pred, ref search_state.best_rdc,
                                    best_pickmode, ref best_intra_sad_norm);

        int skip_idtx_palette = B((x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] != 0 ||
                                   x.color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] != 0) &&
                                  x.content_state_sb.source_sad_nonrd != kZeroSad &&
                                  cpi.rc.high_source_sad == 0 &&
                                  (cpi.rc.high_motion_content_screen_rtc != 0 ||
                                   cpi.rc.frame_source_sad < 1000));

        bool try_palette = enable_palette(
            cpi, is_mode_intra(best_pickmode.best_mode) != 0, bsize, x.source_variance,
            x.force_zeromv_skip_for_blk, skip_idtx_palette, force_palette_test,
            best_intra_sad_norm);

        if (try_palette && prune_palette_testing_inter(cpi, x.source_variance))
            x.color_palette_thresh =
                cpi.sf.rt_sf.prune_palette_search_nonrd > 2 ? 20 : 32;

        if (inter_forced_on_segment == 0)
        {
            // Perform screen content mode evaluation for non-rd
            handle_screen_content_mode_nonrd(cpi, x, search_state, this_mode_pred, ctx,
                                             tmp_buffer, in orig_dst, skip_idtx_palette,
                                             try_palette, bsize, reuse_inter_pred,
                                             mi_col, mi_row);
        }

        pd.dst = orig_dst;
        // Best mode is finalized. Restore the mode data to mbmi
        if (try_palette) mi.palette_mode_info = best_pickmode.pmi;
        mi.mode = best_pickmode.best_mode;
        mi.ref_frame[0] = best_pickmode.best_ref_frame;
        mi.ref_frame[1] = best_pickmode.best_second_ref_frame;
        // For lossless: always force the skip flags off.
        if (is_lossless_requested(in cpi.oxcf.rc_cfg) != 0)
        {
            txfm_info.skip_txfm = 0;
        }
        else
        {
            txfm_info.skip_txfm = best_pickmode.best_mode_skip_txfm;
        }
        if (has_second_ref(mi) != 0)
        {
            mi.comp_group_idx = 0;
            mi.compound_idx = 1;
            mi.interinter_comp.type = COMPOUND_AVERAGE;
        }

        if (is_inter_block(mi) == 0)
        {
            mi.interp_filters = av1_broadcast_interp_filter(SWITCHABLE_FILTERS);
        }
        else
        {
            // If inter mode is selected and ref_frame was one that uses the
            // scaled reference frame, then we can't use reuse_inter_pred.
            if (search_state.use_scaled_ref_frame[best_pickmode.best_ref_frame] ||
                (has_second_ref(mi) != 0 &&
                 search_state
                     .use_scaled_ref_frame[best_pickmode.best_second_ref_frame]))
                x.reuse_inter_pred = false;
        }

        // Restore the predicted samples of best mode to final buffer
        if (reuse_inter_pred != 0 && best_pickmode.best_pred != null)
        {
            PRED_BUFFER best_pred = best_pickmode.best_pred;
            if (best_pred.data != orig_dst.buf && is_inter_mode(mi.mode) != 0)
            {
                aom_convolve_copy(best_pred.data, best_pred.stride, pd.dst.buf,
                                  pd.dst.stride, bw, bh);
            }
        }

        // [subset] CONFIG_AV1_TEMPORAL_DENOISING == 0: av1_denoiser_denoise /
        // recheck_zeromv_after_denoising.

        // Update the factors used for RD thresholding for all modes.
        if (cpi.sf.inter_sf.adaptive_rd_thresh != 0 && has_second_ref(mi) == 0)
        {
            THR_MODES best_mode_idx =
                mode_idx[best_pickmode.best_ref_frame][mode_offset(mi.mode)];
            if (best_pickmode.best_ref_frame == INTRA_FRAME)
            {
                // Only consider the modes that are included in the intra_mode_list.
                int intra_modes = intra_mode_list.Length;
                for (int mode_index = 0; mode_index < intra_modes; mode_index++)
                {
                    update_thresh_freq_fact(cpi, x, bsize, INTRA_FRAME, best_mode_idx,
                                            intra_mode_list[mode_index]);
                }
            }
            else
            {
                PREDICTION_MODE this_mode;
                for (this_mode = NEARESTMV; this_mode <= NEWMV; ++this_mode)
                {
                    update_thresh_freq_fact(cpi, x, bsize, best_pickmode.best_ref_frame,
                                            best_mode_idx, this_mode);
                }
            }
        }

        store_coding_context_nonrd(x, ctx);

        rd_cost = search_state.best_rdc;

        mi.num_proj_ref = 0;
        if (warped_motion_update_num_proj_ref(cpi, mi) != 0)
        {
            // [subset] av1_findSamples: warped motion is disabled in the subset.
            throw new NotSupportedException("av1_nonrd_pick_inter_mode_sb: av1_findSamples");
        }

        // Reset the xd->block_ref_scale_factors[i], as they may have
        // been set to pointer &sf_no_scale, which becomes invalid afer
        // this function.
        set_ref_ptrs(cm, xd, mi.ref_frame[0], mi.ref_frame[1]);
    }
}
