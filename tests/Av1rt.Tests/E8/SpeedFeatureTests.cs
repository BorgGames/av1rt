using System;
using System.Collections.Generic;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aomcx;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.rd;
using static Av1rt.Aom.ratectrl;
using static Av1rt.Aom.speed_features;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.E8;

// Runs av1_set_speed_features_framesize_independent / _framesize_dependent / _qindex_dependent
// on the same encoder state in libaom and in the port and compares every SPEED_FEATURES field,
// WinnerModeParams, the selected sub-pixel search and the sequence header flags they update.
[Trait("Category", "Native")]
public class SpeedFeatureTests
{
    private static readonly (int W, int H)[] Resolutions =
    {
        (352, 288), (640, 360), (854, 480), (1280, 720), (1920, 1080), (3840, 2160),
    };

    // Serialises in the order of speed_features.h (generated from it; same order as shim_e8.c).
    private static long[] Serialize(AV1_COMP cpi)
    {
        var o = s_out = new List<long>(512);
        SPEED_FEATURES sf = cpi.sf;
        W(sf.hl_sf.frame_parameter_update);
        W(sf.hl_sf.recode_loop);
        W(sf.hl_sf.recode_tolerance);
        W(sf.hl_sf.high_precision_mv_usage);
        W(sf.hl_sf.static_segmentation);
        W(sf.hl_sf.superres_auto_search_type);
        W(sf.hl_sf.disable_extra_sc_testing);
        W(sf.hl_sf.second_alt_ref_filtering);
        W(sf.hl_sf.adjust_num_frames_for_arf_filtering);
        W(sf.hl_sf.accurate_bit_estimate);
        W(sf.hl_sf.weight_calc_level_in_tf);
        W(sf.hl_sf.allow_sub_blk_me_in_tf);
        W(sf.hl_sf.ref_frame_mvs_lvl);
        W(sf.hl_sf.screen_detection_mode2_fast_detection);
        W(sf.hl_sf.weighted_chroma_distortion);
        W(sf.fp_sf.reduce_mv_step_param);
        W(sf.fp_sf.skip_motion_search_threshold);
        W(sf.fp_sf.disable_recon);
        W(sf.fp_sf.skip_zeromv_motion_search);
        W(sf.tpl_sf.gop_length_decision_method);
        W(sf.tpl_sf.prune_intra_modes);
        W(sf.tpl_sf.reduce_first_step_size);
        W(sf.tpl_sf.skip_alike_starting_mv);
        W(sf.tpl_sf.subpel_force_stop);
        W(sf.tpl_sf.search_method);
        W(sf.tpl_sf.prune_starting_mv);
        W(sf.tpl_sf.prune_ref_frames_in_tpl);
        W(sf.tpl_sf.allow_compound_pred);
        W(sf.tpl_sf.use_y_only_rate_distortion);
        W(sf.tpl_sf.use_sad_for_mode_decision);
        W(sf.tpl_sf.reduce_num_frames);
        W(sf.gm_sf.gm_search_type);
        W(sf.gm_sf.prune_ref_frame_for_gm_search);
        W(sf.gm_sf.prune_zero_mv_with_sse);
        W(sf.gm_sf.disable_gm_search_based_on_stats);
        W(sf.gm_sf.downsample_level);
        W(sf.gm_sf.num_refinement_steps);
        W(sf.gm_sf.gm_erroradv_tr_level);
        W(sf.part_sf.partition_search_type);
        W(sf.part_sf.fixed_partition_size);
        W(sf.part_sf.prune_ext_partition_types_search_level);
        W(sf.part_sf.prune_part4_search);
        W(sf.part_sf.ml_prune_partition);
        W(sf.part_sf.ml_early_term_after_part_split_level);
        W(sf.part_sf.less_rectangular_check_level);
        W(sf.part_sf.use_square_partition_only_threshold);
        W(sf.part_sf.auto_max_partition_based_on_simple_motion);
        W(sf.part_sf.default_min_partition_size);
        W(sf.part_sf.default_max_partition_size);
        W(sf.part_sf.adjust_var_based_rd_partitioning);
        W(sf.part_sf.partition_search_breakout_dist_thr);
        W(sf.part_sf.partition_search_breakout_rate_thr);
        for (int i = 0; i < PARTITION_BLOCK_SIZES; ++i) WF(sf.part_sf.ml_partition_search_breakout_thresh[i]);
        W(sf.part_sf.ml_partition_search_breakout_model_index);
        W(sf.part_sf.ml_4_partition_search_level_index);
        W(sf.part_sf.simple_motion_search_prune_agg);
        W(sf.part_sf.simple_motion_search_prune_rect);
        W(sf.part_sf.simple_motion_search_split);
        W(sf.part_sf.simple_motion_search_early_term_none);
        W(sf.part_sf.simple_motion_search_reduce_search_steps);
        W(sf.part_sf.max_intra_bsize);
        W(sf.part_sf.intra_cnn_based_part_prune_level);
        W(sf.part_sf.ext_partition_eval_thresh);
        W(sf.part_sf.ext_part_eval_based_on_cur_best);
        W(sf.part_sf.rect_partition_eval_thresh);
        W(sf.part_sf.prune_ext_part_using_split_info);
        W(sf.part_sf.prune_rectangular_split_based_on_qidx);
        W(sf.part_sf.prune_rect_part_using_4x4_var_deviation);
        W(sf.part_sf.prune_rect_part_using_none_pred_mode);
        W(sf.part_sf.early_term_after_none_split);
        W(sf.part_sf.ml_predict_breakout_level);
        W(sf.part_sf.prune_sub_8x8_partition_level);
        W(sf.part_sf.simple_motion_search_rect_split);
        W(sf.part_sf.reuse_prev_rd_results_for_part_ab);
        W(sf.part_sf.reuse_best_prediction_for_part_ab);
        W(sf.part_sf.use_best_rd_for_pruning);
        W(sf.part_sf.skip_non_sq_part_based_on_none);
        W(sf.part_sf.disable_8x8_part_based_on_qidx);
        W(sf.part_sf.prune_h_or_v_4part_using_sms_info);
        W(sf.part_sf.split_partition_penalty_level);
        W(sf.mv_sf.search_method);
        W(sf.mv_sf.use_bsize_dependent_search_method);
        W(sf.mv_sf.auto_mv_step_size);
        W(sf.mv_sf.subpel_search_method);
        W(sf.mv_sf.subpel_iters_per_step);
        W(sf.mv_sf.subpel_force_stop);
        W(sf.mv_sf.simple_motion_subpel_force_stop);
        W(sf.mv_sf.use_accurate_subpel_search);
        W(sf.mv_sf.exhaustive_searches_thresh);
        for (int i = 0; i < MAX_MESH_STEP; ++i) { W(sf.mv_sf.mesh_patterns[i].range); W(sf.mv_sf.mesh_patterns[i].interval); }
        for (int i = 0; i < MAX_MESH_STEP; ++i) { W(sf.mv_sf.intrabc_mesh_patterns[i].range); W(sf.mv_sf.intrabc_mesh_patterns[i].interval); }
        W(sf.mv_sf.reduce_search_range);
        W(sf.mv_sf.prune_mesh_search);
        W(sf.mv_sf.use_fullpel_costlist);
        W(sf.mv_sf.obmc_full_pixel_search_level);
        W(sf.mv_sf.full_pixel_search_level);
        W(sf.mv_sf.use_intrabc);
        W(sf.mv_sf.prune_intrabc_candidate_block_hash_search);
        W(sf.mv_sf.intrabc_search_level);
        W(sf.mv_sf.hash_max_8x8_intrabc_blocks);
        W(sf.mv_sf.use_downsampled_sad);
        W(sf.mv_sf.disable_extensive_joint_motion_search);
        W(sf.mv_sf.disable_second_mv);
        W(sf.mv_sf.skip_fullpel_search_using_startmv_refmv);
        W(sf.mv_sf.warp_search_method);
        W(sf.mv_sf.warp_search_iters);
        W(sf.inter_sf.inter_mode_rd_model_estimation);
        for (int i = 0; i < TX_SEARCH_CASES; ++i) W(sf.inter_sf.txfm_rd_gate_level[i]);
        W(sf.inter_sf.reduce_inter_modes);
        W(sf.inter_sf.adaptive_rd_thresh);
        W(sf.inter_sf.prune_inter_modes_if_skippable);
        W(sf.inter_sf.selective_ref_frame);
        W(sf.inter_sf.retain_last2_in_partial_high_motion);
        W(sf.inter_sf.prune_ref_frame_for_rect_partitions);
        W(sf.inter_sf.alt_ref_search_fp);
        W(sf.inter_sf.prune_single_ref);
        W(sf.inter_sf.prune_comp_ref_frames);
        W(sf.inter_sf.skip_newmv_in_drl);
        W(sf.inter_sf.skip_repeated_ref_mv);
        W(sf.inter_sf.perform_best_rd_based_gating_for_chroma);
        W(sf.inter_sf.reuse_inter_intra_mode);
        W(sf.inter_sf.prune_comp_type_by_model_rd);
        W(sf.inter_sf.prune_comp_type_by_comp_avg);
        W(sf.inter_sf.prune_comp_search_by_single_result);
        W(sf.inter_sf.prune_mode_search_simple_translation);
        W(sf.inter_sf.prune_compound_using_single_ref);
        W(sf.inter_sf.prune_ext_comp_using_neighbors);
        W(sf.inter_sf.skip_ext_comp_nearmv_mode);
        W(sf.inter_sf.prune_comp_using_best_single_mode_ref);
        W(sf.inter_sf.prune_nearest_near_mv_using_refmv_weight);
        W(sf.inter_sf.prune_ref_mv_idx_search);
        W(sf.inter_sf.disable_onesided_comp);
        W(sf.inter_sf.prune_obmc_prob_thresh);
        W(sf.inter_sf.prune_warped_prob_thresh);
        W(sf.inter_sf.disable_interintra_wedge_var_thresh);
        W(sf.inter_sf.disable_interinter_wedge_var_thresh);
        W(sf.inter_sf.fast_interintra_wedge_search);
        W(sf.inter_sf.fast_wedge_sign_estimate);
        W(sf.inter_sf.disable_interinter_wedge_newmv_search);
        W(sf.inter_sf.use_dist_wtd_comp_flag);
        W(sf.inter_sf.mv_cost_upd_level);
        W(sf.inter_sf.coeff_cost_upd_level);
        W(sf.inter_sf.mode_cost_upd_level);
        W(sf.inter_sf.prune_inter_modes_based_on_tpl);
        W(sf.inter_sf.prune_nearmv_using_neighbors);
        W(sf.inter_sf.model_based_post_interp_filter_breakout);
        W(sf.inter_sf.reuse_compound_type_decision);
        W(sf.inter_sf.disable_masked_comp);
        W(sf.inter_sf.enable_fast_compound_mode_search);
        W(sf.inter_sf.reuse_mask_search_results);
        W(sf.inter_sf.enable_fast_wedge_mask_search);
        W(sf.inter_sf.inter_mode_txfm_breakout);
        W(sf.inter_sf.limit_inter_mode_cands);
        W(sf.inter_sf.limit_txfm_eval_per_mode);
        W(sf.inter_sf.extra_prune_warped);
        W(sf.inter_sf.skip_arf_compound);
        WF(sf.inter_sf.bias_warp_mode_rd_scale_pct);
        WF(sf.inter_sf.bias_obmc_mode_rd_scale_pct);
        WF(sf.inter_sf.bias_gm_mode_rd_scale_pct);
        W(sf.inter_sf.skip_cmp_using_top_cmp_avg_est_rd_lvl);
        W(sf.inter_sf.skip_interinter_wedge_search_based_on_mse);
        W(sf.inter_sf.enable_comp_wedge_search_using_model_rd);
        W(sf.inter_sf.skip_interintra_using_top_smooth_rd_lvl);
        W(sf.interp_sf.use_fast_interpolation_filter_search);
        W(sf.interp_sf.disable_dual_filter);
        W(sf.interp_sf.use_interp_filter);
        W(sf.interp_sf.skip_sharp_interp_filter_search);
        W(sf.interp_sf.cb_pred_filter_search);
        W(sf.interp_sf.adaptive_interp_filter_search);
        W(sf.interp_sf.skip_interp_filter_search);
        W(sf.interp_sf.use_more_sharp_interp);
        W(sf.interp_sf.skip_model_rd_uv);
        for (int i = 0; i < TX_SIZES; ++i) W(sf.intra_sf.intra_y_mode_mask[i]);
        for (int i = 0; i < TX_SIZES; ++i) W(sf.intra_sf.intra_uv_mode_mask[i]);
        W(sf.intra_sf.skip_intra_in_interframe);
        W(sf.intra_sf.intra_pruning_with_hog);
        W(sf.intra_sf.chroma_intra_pruning_with_hog);
        W(sf.intra_sf.disable_smooth_intra);
        W(sf.intra_sf.prune_smooth_intra_mode_for_chroma);
        W(sf.intra_sf.prune_filter_intra_level);
        W(sf.intra_sf.prune_palette_search_level);
        W(sf.intra_sf.prune_luma_palette_size_search_level);
        W(sf.intra_sf.prune_chroma_modes_using_luma_winner);
        W(sf.intra_sf.dv_cost_upd_level);
        W(sf.intra_sf.cfl_search_range);
        W(sf.intra_sf.top_intra_model_count_allowed);
        W(sf.intra_sf.adapt_top_model_rd_count_using_neighbors);
        W(sf.intra_sf.prune_luma_odd_delta_angles_in_intra);
        W(sf.intra_sf.early_term_chroma_palette_size_search);
        W(sf.intra_sf.skip_filter_intra_in_inter_frames);
        W(sf.tx_sf.inter_tx_size_search_init_depth_sqr);
        W(sf.tx_sf.inter_tx_size_search_init_depth_rect);
        W(sf.tx_sf.intra_tx_size_search_init_depth_sqr);
        W(sf.tx_sf.intra_tx_size_search_init_depth_rect);
        W(sf.tx_sf.tx_size_search_lgr_block);
        W(sf.tx_sf.tx_type_search.prune_2d_txfm_mode);
        W(sf.tx_sf.tx_type_search.fast_intra_tx_type_search);
        W(sf.tx_sf.tx_type_search.fast_inter_tx_type_prob_thresh);
        W(sf.tx_sf.tx_type_search.use_reduced_intra_txset);
        W(sf.tx_sf.tx_type_search.use_skip_flag_prediction);
        W(sf.tx_sf.tx_type_search.ml_tx_split_thresh);
        W(sf.tx_sf.tx_type_search.skip_tx_search);
        W(sf.tx_sf.tx_type_search.prune_tx_type_using_stats);
        W(sf.tx_sf.tx_type_search.prune_tx_type_est_rd);
        W(sf.tx_sf.tx_type_search.winner_mode_tx_type_pruning);
        W(sf.tx_sf.txb_split_cap);
        W(sf.tx_sf.adaptive_txb_search_level);
        W(sf.tx_sf.model_based_prune_tx_search_level);
        W(sf.tx_sf.refine_fast_tx_search_results);
        W(sf.tx_sf.prune_tx_size_level);
        W(sf.tx_sf.prune_intra_tx_depths_using_nn);
        W(sf.tx_sf.use_rd_based_breakout_for_intra_tx_search);
        W(sf.tx_sf.prune_inter_tx_split_rd_eval_lvl);
        W(sf.tx_sf.use_chroma_trellis_rd_mult);
        W(sf.rd_sf.simple_model_rd_from_var);
        W(sf.rd_sf.tx_domain_dist_level);
        W(sf.rd_sf.tx_domain_dist_thres_level);
        W(sf.rd_sf.optimize_coefficients);
        W(sf.rd_sf.use_mb_rd_hash);
        W(sf.rd_sf.perform_coeff_opt);
        W(sf.winner_mode_sf.enable_winner_mode_for_coeff_opt);
        W(sf.winner_mode_sf.enable_winner_mode_for_tx_size_srch);
        W(sf.winner_mode_sf.tx_size_search_level);
        W(sf.winner_mode_sf.enable_winner_mode_for_use_tx_domain_dist);
        W(sf.winner_mode_sf.multi_winner_mode_type);
        W(sf.winner_mode_sf.motion_mode_for_winner_cand);
        W(sf.winner_mode_sf.dc_blk_pred_level);
        W(sf.winner_mode_sf.winner_mode_ifs);
        W(sf.winner_mode_sf.prune_winner_mode_eval_level);
        W(sf.lpf_sf.lpf_pick);
        W(sf.lpf_sf.use_coarse_filter_level_search);
        W(sf.lpf_sf.adaptive_luma_loop_filter_skip);
        W(sf.lpf_sf.skip_loop_filter_using_filt_error);
        W(sf.lpf_sf.cdef_pick_method);
        W(sf.lpf_sf.zero_low_cdef_strengths);
        W(sf.lpf_sf.adaptive_cdef_mode);
        W(sf.lpf_sf.dual_sgr_penalty_level);
        W(sf.lpf_sf.switchable_lr_with_bias_level);
        W(sf.lpf_sf.enable_sgr_ep_pruning);
        W(sf.lpf_sf.disable_loop_restoration_chroma);
        W(sf.lpf_sf.disable_loop_restoration_luma);
        W(sf.lpf_sf.min_lr_unit_size);
        W(sf.lpf_sf.max_lr_unit_size);
        W(sf.lpf_sf.prune_wiener_based_on_src_var);
        W(sf.lpf_sf.prune_sgr_based_on_wiener);
        W(sf.lpf_sf.reduce_wiener_window_size);
        W(sf.lpf_sf.disable_wiener_filter);
        W(sf.lpf_sf.disable_sgr_filter);
        W(sf.lpf_sf.disable_wiener_coeff_refine_search);
        W(sf.lpf_sf.use_downsampled_wiener_stats);
        W(sf.rt_sf.check_intra_pred_nonrd);
        W(sf.rt_sf.skip_intra_pred);
        W(sf.rt_sf.estimate_motion_for_var_based_partition);
        W(sf.rt_sf.nonrd_check_partition_merge_mode);
        W(sf.rt_sf.nonrd_check_partition_split);
        W(sf.rt_sf.mode_search_skip_flags);
        W(sf.rt_sf.nonrd_prune_ref_frame_search);
        W(sf.rt_sf.use_nonrd_pick_mode);
        W(sf.rt_sf.discount_color_cost);
        W(sf.rt_sf.use_nonrd_altref_frame);
        W(sf.rt_sf.use_comp_ref_nonrd);
        for (int i = 0; i < 3; ++i) W(sf.rt_sf.ref_frame_comp_nonrd[i]);
        W(sf.rt_sf.use_real_time_ref_set);
        W(sf.rt_sf.short_circuit_low_temp_var);
        W(sf.rt_sf.reuse_inter_pred_nonrd);
        W(sf.rt_sf.num_inter_modes_for_tx_search);
        W(sf.rt_sf.use_nonrd_filter_search);
        W(sf.rt_sf.use_simple_rd_model);
        W(sf.rt_sf.hybrid_intra_pickmode);
        W(sf.rt_sf.prune_palette_search_nonrd);
        W(sf.rt_sf.source_metrics_sb_nonrd);
        W(sf.rt_sf.overshoot_detection_cbr);
        W(sf.rt_sf.check_scene_detection);
        W(sf.rt_sf.rc_adjust_keyframe);
        W(sf.rt_sf.rc_compute_spatial_var_sc_kf);
        W(sf.rt_sf.prefer_large_partition_blocks);
        W(sf.rt_sf.use_temporal_noise_estimate);
        W(sf.rt_sf.fullpel_search_step_param);
        for (int i = 0; i < BLOCK_SIZES; ++i) W(sf.rt_sf.intra_y_mode_bsize_mask_nrd[i]);
        W(sf.rt_sf.prune_hv_pred_modes_using_src_sad);
        W(sf.rt_sf.nonrd_aggressive_skip);
        W(sf.rt_sf.skip_cdef_sb);
        W(sf.rt_sf.selective_cdf_update);
        W(sf.rt_sf.rt_use_intrabc);
        W(sf.rt_sf.rt_prune_intrabc_nonrd);
        W(sf.rt_sf.rt_intrabc_miss_mode);
        W(sf.rt_sf.force_only_last_ref);
        W(sf.rt_sf.force_large_partition_blocks_intra);
        W(sf.rt_sf.use_fast_fixed_part);
        W(sf.rt_sf.increase_source_sad_thresh);
        W(sf.rt_sf.skip_tx_no_split_var_based_partition);
        W(sf.rt_sf.skip_newmv_mode_based_on_sse);
        W(sf.rt_sf.gf_length_lvl);
        W(sf.rt_sf.prune_inter_modes_with_golden_ref);
        W(sf.rt_sf.prune_inter_modes_wrt_gf_arf_based_on_sad);
        W(sf.rt_sf.prune_inter_modes_using_temp_var);
        W(sf.rt_sf.reduce_mv_pel_precision_highmotion);
        W(sf.rt_sf.reduce_mv_pel_precision_lowcomplex);
        W(sf.rt_sf.prune_intra_mode_based_on_mv_range);
        W(sf.rt_sf.var_part_split_threshold_shift);
        W(sf.rt_sf.var_part_based_on_qidx);
        W(sf.rt_sf.gf_refresh_based_on_qp);
        W(sf.rt_sf.use_rtc_tf);
        W(sf.rt_sf.use_idtx_nonrd);
        W(sf.rt_sf.prune_idtx_nonrd);
        W(sf.rt_sf.dct_only_palette_nonrd);
        W(sf.rt_sf.skip_lf_screen);
        W(sf.rt_sf.thresh_active_maps_skip_lf_cdef);
        W(sf.rt_sf.part_early_exit_zeromv);
        W(sf.rt_sf.sse_early_term_inter_search);
        W(sf.rt_sf.sad_based_adp_altref_lag);
        W(sf.rt_sf.partition_direct_merging);
        W(sf.rt_sf.tx_size_level_based_on_qstep);
        W(sf.rt_sf.vbp_prune_16x16_split_using_min_max_sub_blk_var);
        W(sf.rt_sf.screen_content_cdef_filter_qindex_thresh);
        W(sf.rt_sf.prune_compoundmode_with_singlecompound_var);
        W(sf.rt_sf.frame_level_mode_cost_update);
        W(sf.rt_sf.prune_h_pred_using_best_mode_so_far);
        W(sf.rt_sf.enable_intra_mode_pruning_using_neighbors);
        W(sf.rt_sf.prune_intra_mode_using_best_sad_so_far);
        W(sf.rt_sf.check_only_zero_zeromv_on_large_blocks);
        W(sf.rt_sf.disable_cdf_update_non_reference_frame);
        W(sf.rt_sf.prune_compoundmode_with_singlemode_var);
        W(sf.rt_sf.skip_compound_based_on_var);
        W(sf.rt_sf.set_zeromv_skip_based_on_source_sad);
        W(sf.rt_sf.use_adaptive_subpel_search);
        W(sf.rt_sf.enable_ref_short_signaling);
        W(sf.rt_sf.check_globalmv_on_single_ref);
        W(sf.rt_sf.increase_color_thresh_palette);
        W(sf.rt_sf.higher_thresh_scene_detection);
        W(sf.rt_sf.skip_newmv_flat_blocks_screen);
        W(sf.rt_sf.skip_encoding_non_reference_slide_change);
        W(sf.rt_sf.rc_faster_convergence_static);
        W(sf.rt_sf.skip_newmv_mode_sad_screen);
        WinnerModeParams wmp = cpi.winner_mode_params;
        for (int i = 0; i < MODE_EVAL_TYPES; ++i)
        {
            W(wmp.coeff_opt_thresholds[i][0]);
            W(wmp.coeff_opt_thresholds[i][1]);
            W(wmp.tx_size_search_methods[i]);
            W(wmp.use_transform_domain_distortion[i]);
            W(wmp.tx_domain_dist_threshold[i]);
            W(wmp.skip_txfm_level[i]);
            W(wmp.predict_dc_level[i]);
        }
        W(Array.IndexOf(fractional_mv_search, cpi.mv_search_params.find_fractional_mv_step));
        SequenceHeader seq = cpi.common.seq_params;
        W(seq.enable_masked_compound);
        W(seq.enable_interintra_compound);
        W(seq.enable_dual_filter);
        W(seq.enable_restoration);
        W(seq.order_hint_info.enable_dist_wtd_comp);
        return o.ToArray();
    }

    [ThreadStatic] private static List<long> s_out;
    private static void W(long v) => s_out.Add(v);
    private static void W(bool v) => s_out.Add(v ? 1 : 0);
    private static void WF(float v) => s_out.Add(BitConverter.SingleToInt32Bits(v));

    private static int s_fieldCount = -1;

    private static void Check(int[] p)
    {
        var expected = new long[1024];
        int n = E8Shim.aomshim_e8_speed_features(p, expected, expected.Length);
        Assert.InRange(n, 1, expected.Length);
        AV1_COMP cpi = E8Context.Create(p);
        E8Context.SetSpeedFeatures(cpi);
        long[] actual = Serialize(cpi);
        Assert.True(n == actual.Length, $"field count: C {n}, C# {actual.Length}");
        s_fieldCount = n;
        for (int i = 0; i < n; ++i)
        {
            if (expected[i] != actual[i])
                Assert.Fail($"{P.Describe(p)}: value #{i} ({FieldName(i)}) C {expected[i]} C# {actual[i]}");
        }
    }

    private static string FieldName(int index) => index < FieldNames.Length ? FieldNames[index] : "winner_mode_params / subpel / seq";

    [Fact]
    public void AllRealtimeSpeedsAndResolutionsMatchLibaom()
    {
        (int ft, int ut)[] frames = { (KEY_FRAME, KF_UPDATE), (INTER_FRAME, LF_UPDATE), (INTER_FRAME, GF_UPDATE), (INTER_FRAME, ARF_UPDATE) };
        for (int speed = 5; speed <= 11; ++speed)
            foreach (var (w, h) in Resolutions)
                foreach (var (ft, ut) in frames)
                    foreach (int q in new[] { 0, 60, 120, 121, 140, 141, 150, 151, 170, 190, 191, 255 })
                    {
                        int[] p = P.Default(speed, w, h);
                        p[P.FRAME_TYPE] = ft;
                        p[P.UPDATE_TYPE] = ut;
                        p[P.BASE_QINDEX] = q;
                        Check(p);
                    }
        Assert.True(s_fieldCount > 300);
    }

    // Each configuration knob the realtime setters read, across speeds and resolutions.
    [Fact]
    public void ConfigurationVariantsMatchLibaom()
    {
        var variants = new List<Action<int[]>>
        {
            p => p[P.AQ_MODE] = 0,
            p => { p[P.RC_MODE] = 0; p[P.FRAME_TYPE] = KEY_FRAME; p[P.UPDATE_TYPE] = KF_UPDATE; },
            p => p[P.RC_MODE] = 0,
            p => { p[P.NUM_WORKERS] = 4; p[P.ROW_MT] = 1; },
            p => { p[P.NUM_WORKERS] = 8; p[P.ROW_MT] = 0; },
            p => p[P.KEY_FREQ_MAX] = 0,
            p => p[P.ENABLE_TX_SIZE_SEARCH] = 0,
            p => p[P.GF_CBR_BOOST_PCT] = 20,
            p => p[P.DISABLE_TRELLIS_QUANT] = 0,
            p => p[P.DISABLE_TRELLIS_QUANT] = 1,
            p => p[P.DISABLE_TRELLIS_QUANT] = 2,
            p => p[P.RTC_EXTERNAL_RATECTRL] = 1,
            p => p[P.LAG_IN_FRAMES] = 10,
            p => { p[P.LAG_IN_FRAMES] = 10; p[P.ALT_REF_FRAME] = 1; },
            p => { p[P.LAG_IN_FRAMES] = 10; p[P.IS_SRC_FRAME_ALT_REF] = 1; },
            p => p[P.ACTIVE_MAP] = 1,
            p => p[P.ENABLE_SUPERRES] = 1,
            p => p[P.CALC_PSNR] = 1,
            p => { p[P.CALC_PSNR] = 1; p[P.CFG_WIDTH] = p[P.WIDTH] * 2; },
            p => { p[P.CALC_PSNR] = 1; p[P.CFG_HEIGHT] = p[P.HEIGHT] + 8; p[P.SHOW_FRAME] = 0; },
            p => p[P.TUNING] = AOM_TUNE_IQ,
            p => { p[P.TUNING] = AOM_TUNE_SSIMULACRA2; p[P.BASE_QINDEX] = 100; },
            p => p[P.TUNING] = AOM_TUNE_SSIM,
            p => p[P.LOSSLESS] = 1,
            p => { p[P.LOSSLESS] = 1; p[P.DISABLE_TRELLIS_QUANT] = 0; },
            p => p[P.SEQ_PARAMS_LOCKED] = 1,
        };
        foreach (Action<int[]> variant in variants)
            for (int speed = 5; speed <= 11; ++speed)
                foreach (var (w, h) in Resolutions)
                {
                    int[] p = P.Default(speed, w, h);
                    variant(p);
                    Check(p);
                }
    }

    private static readonly string[] FieldNames = BuildFieldNames();

    private static string[] BuildFieldNames()
    {
        var names = new List<string>();
        names.Add("hl_sf.frame_parameter_update");
        names.Add("hl_sf.recode_loop");
        names.Add("hl_sf.recode_tolerance");
        names.Add("hl_sf.high_precision_mv_usage");
        names.Add("hl_sf.static_segmentation");
        names.Add("hl_sf.superres_auto_search_type");
        names.Add("hl_sf.disable_extra_sc_testing");
        names.Add("hl_sf.second_alt_ref_filtering");
        names.Add("hl_sf.adjust_num_frames_for_arf_filtering");
        names.Add("hl_sf.accurate_bit_estimate");
        names.Add("hl_sf.weight_calc_level_in_tf");
        names.Add("hl_sf.allow_sub_blk_me_in_tf");
        names.Add("hl_sf.ref_frame_mvs_lvl");
        names.Add("hl_sf.screen_detection_mode2_fast_detection");
        names.Add("hl_sf.weighted_chroma_distortion");
        names.Add("fp_sf.reduce_mv_step_param");
        names.Add("fp_sf.skip_motion_search_threshold");
        names.Add("fp_sf.disable_recon");
        names.Add("fp_sf.skip_zeromv_motion_search");
        names.Add("tpl_sf.gop_length_decision_method");
        names.Add("tpl_sf.prune_intra_modes");
        names.Add("tpl_sf.reduce_first_step_size");
        names.Add("tpl_sf.skip_alike_starting_mv");
        names.Add("tpl_sf.subpel_force_stop");
        names.Add("tpl_sf.search_method");
        names.Add("tpl_sf.prune_starting_mv");
        names.Add("tpl_sf.prune_ref_frames_in_tpl");
        names.Add("tpl_sf.allow_compound_pred");
        names.Add("tpl_sf.use_y_only_rate_distortion");
        names.Add("tpl_sf.use_sad_for_mode_decision");
        names.Add("tpl_sf.reduce_num_frames");
        names.Add("gm_sf.gm_search_type");
        names.Add("gm_sf.prune_ref_frame_for_gm_search");
        names.Add("gm_sf.prune_zero_mv_with_sse");
        names.Add("gm_sf.disable_gm_search_based_on_stats");
        names.Add("gm_sf.downsample_level");
        names.Add("gm_sf.num_refinement_steps");
        names.Add("gm_sf.gm_erroradv_tr_level");
        names.Add("part_sf.partition_search_type");
        names.Add("part_sf.fixed_partition_size");
        names.Add("part_sf.prune_ext_partition_types_search_level");
        names.Add("part_sf.prune_part4_search");
        names.Add("part_sf.ml_prune_partition");
        names.Add("part_sf.ml_early_term_after_part_split_level");
        names.Add("part_sf.less_rectangular_check_level");
        names.Add("part_sf.use_square_partition_only_threshold");
        names.Add("part_sf.auto_max_partition_based_on_simple_motion");
        names.Add("part_sf.default_min_partition_size");
        names.Add("part_sf.default_max_partition_size");
        names.Add("part_sf.adjust_var_based_rd_partitioning");
        names.Add("part_sf.partition_search_breakout_dist_thr");
        names.Add("part_sf.partition_search_breakout_rate_thr");
        for (int i = 0; i < PARTITION_BLOCK_SIZES; ++i) names.Add($"part_sf.ml_partition_search_breakout_thresh[{i}]");
        names.Add("part_sf.ml_partition_search_breakout_model_index");
        names.Add("part_sf.ml_4_partition_search_level_index");
        names.Add("part_sf.simple_motion_search_prune_agg");
        names.Add("part_sf.simple_motion_search_prune_rect");
        names.Add("part_sf.simple_motion_search_split");
        names.Add("part_sf.simple_motion_search_early_term_none");
        names.Add("part_sf.simple_motion_search_reduce_search_steps");
        names.Add("part_sf.max_intra_bsize");
        names.Add("part_sf.intra_cnn_based_part_prune_level");
        names.Add("part_sf.ext_partition_eval_thresh");
        names.Add("part_sf.ext_part_eval_based_on_cur_best");
        names.Add("part_sf.rect_partition_eval_thresh");
        names.Add("part_sf.prune_ext_part_using_split_info");
        names.Add("part_sf.prune_rectangular_split_based_on_qidx");
        names.Add("part_sf.prune_rect_part_using_4x4_var_deviation");
        names.Add("part_sf.prune_rect_part_using_none_pred_mode");
        names.Add("part_sf.early_term_after_none_split");
        names.Add("part_sf.ml_predict_breakout_level");
        names.Add("part_sf.prune_sub_8x8_partition_level");
        names.Add("part_sf.simple_motion_search_rect_split");
        names.Add("part_sf.reuse_prev_rd_results_for_part_ab");
        names.Add("part_sf.reuse_best_prediction_for_part_ab");
        names.Add("part_sf.use_best_rd_for_pruning");
        names.Add("part_sf.skip_non_sq_part_based_on_none");
        names.Add("part_sf.disable_8x8_part_based_on_qidx");
        names.Add("part_sf.prune_h_or_v_4part_using_sms_info");
        names.Add("part_sf.split_partition_penalty_level");
        names.Add("mv_sf.search_method");
        names.Add("mv_sf.use_bsize_dependent_search_method");
        names.Add("mv_sf.auto_mv_step_size");
        names.Add("mv_sf.subpel_search_method");
        names.Add("mv_sf.subpel_iters_per_step");
        names.Add("mv_sf.subpel_force_stop");
        names.Add("mv_sf.simple_motion_subpel_force_stop");
        names.Add("mv_sf.use_accurate_subpel_search");
        names.Add("mv_sf.exhaustive_searches_thresh");
        for (int i = 0; i < MAX_MESH_STEP; ++i) { names.Add($"mv_sf.mesh_patterns[{i}].range"); names.Add($"mv_sf.mesh_patterns[{i}].interval"); }
        for (int i = 0; i < MAX_MESH_STEP; ++i) { names.Add($"mv_sf.intrabc_mesh_patterns[{i}].range"); names.Add($"mv_sf.intrabc_mesh_patterns[{i}].interval"); }
        names.Add("mv_sf.reduce_search_range");
        names.Add("mv_sf.prune_mesh_search");
        names.Add("mv_sf.use_fullpel_costlist");
        names.Add("mv_sf.obmc_full_pixel_search_level");
        names.Add("mv_sf.full_pixel_search_level");
        names.Add("mv_sf.use_intrabc");
        names.Add("mv_sf.prune_intrabc_candidate_block_hash_search");
        names.Add("mv_sf.intrabc_search_level");
        names.Add("mv_sf.hash_max_8x8_intrabc_blocks");
        names.Add("mv_sf.use_downsampled_sad");
        names.Add("mv_sf.disable_extensive_joint_motion_search");
        names.Add("mv_sf.disable_second_mv");
        names.Add("mv_sf.skip_fullpel_search_using_startmv_refmv");
        names.Add("mv_sf.warp_search_method");
        names.Add("mv_sf.warp_search_iters");
        names.Add("inter_sf.inter_mode_rd_model_estimation");
        for (int i = 0; i < TX_SEARCH_CASES; ++i) names.Add($"inter_sf.txfm_rd_gate_level[{i}]");
        names.Add("inter_sf.reduce_inter_modes");
        names.Add("inter_sf.adaptive_rd_thresh");
        names.Add("inter_sf.prune_inter_modes_if_skippable");
        names.Add("inter_sf.selective_ref_frame");
        names.Add("inter_sf.retain_last2_in_partial_high_motion");
        names.Add("inter_sf.prune_ref_frame_for_rect_partitions");
        names.Add("inter_sf.alt_ref_search_fp");
        names.Add("inter_sf.prune_single_ref");
        names.Add("inter_sf.prune_comp_ref_frames");
        names.Add("inter_sf.skip_newmv_in_drl");
        names.Add("inter_sf.skip_repeated_ref_mv");
        names.Add("inter_sf.perform_best_rd_based_gating_for_chroma");
        names.Add("inter_sf.reuse_inter_intra_mode");
        names.Add("inter_sf.prune_comp_type_by_model_rd");
        names.Add("inter_sf.prune_comp_type_by_comp_avg");
        names.Add("inter_sf.prune_comp_search_by_single_result");
        names.Add("inter_sf.prune_mode_search_simple_translation");
        names.Add("inter_sf.prune_compound_using_single_ref");
        names.Add("inter_sf.prune_ext_comp_using_neighbors");
        names.Add("inter_sf.skip_ext_comp_nearmv_mode");
        names.Add("inter_sf.prune_comp_using_best_single_mode_ref");
        names.Add("inter_sf.prune_nearest_near_mv_using_refmv_weight");
        names.Add("inter_sf.prune_ref_mv_idx_search");
        names.Add("inter_sf.disable_onesided_comp");
        names.Add("inter_sf.prune_obmc_prob_thresh");
        names.Add("inter_sf.prune_warped_prob_thresh");
        names.Add("inter_sf.disable_interintra_wedge_var_thresh");
        names.Add("inter_sf.disable_interinter_wedge_var_thresh");
        names.Add("inter_sf.fast_interintra_wedge_search");
        names.Add("inter_sf.fast_wedge_sign_estimate");
        names.Add("inter_sf.disable_interinter_wedge_newmv_search");
        names.Add("inter_sf.use_dist_wtd_comp_flag");
        names.Add("inter_sf.mv_cost_upd_level");
        names.Add("inter_sf.coeff_cost_upd_level");
        names.Add("inter_sf.mode_cost_upd_level");
        names.Add("inter_sf.prune_inter_modes_based_on_tpl");
        names.Add("inter_sf.prune_nearmv_using_neighbors");
        names.Add("inter_sf.model_based_post_interp_filter_breakout");
        names.Add("inter_sf.reuse_compound_type_decision");
        names.Add("inter_sf.disable_masked_comp");
        names.Add("inter_sf.enable_fast_compound_mode_search");
        names.Add("inter_sf.reuse_mask_search_results");
        names.Add("inter_sf.enable_fast_wedge_mask_search");
        names.Add("inter_sf.inter_mode_txfm_breakout");
        names.Add("inter_sf.limit_inter_mode_cands");
        names.Add("inter_sf.limit_txfm_eval_per_mode");
        names.Add("inter_sf.extra_prune_warped");
        names.Add("inter_sf.skip_arf_compound");
        names.Add("inter_sf.bias_warp_mode_rd_scale_pct");
        names.Add("inter_sf.bias_obmc_mode_rd_scale_pct");
        names.Add("inter_sf.bias_gm_mode_rd_scale_pct");
        names.Add("inter_sf.skip_cmp_using_top_cmp_avg_est_rd_lvl");
        names.Add("inter_sf.skip_interinter_wedge_search_based_on_mse");
        names.Add("inter_sf.enable_comp_wedge_search_using_model_rd");
        names.Add("inter_sf.skip_interintra_using_top_smooth_rd_lvl");
        names.Add("interp_sf.use_fast_interpolation_filter_search");
        names.Add("interp_sf.disable_dual_filter");
        names.Add("interp_sf.use_interp_filter");
        names.Add("interp_sf.skip_sharp_interp_filter_search");
        names.Add("interp_sf.cb_pred_filter_search");
        names.Add("interp_sf.adaptive_interp_filter_search");
        names.Add("interp_sf.skip_interp_filter_search");
        names.Add("interp_sf.use_more_sharp_interp");
        names.Add("interp_sf.skip_model_rd_uv");
        for (int i = 0; i < TX_SIZES; ++i) names.Add($"intra_sf.intra_y_mode_mask[{i}]");
        for (int i = 0; i < TX_SIZES; ++i) names.Add($"intra_sf.intra_uv_mode_mask[{i}]");
        names.Add("intra_sf.skip_intra_in_interframe");
        names.Add("intra_sf.intra_pruning_with_hog");
        names.Add("intra_sf.chroma_intra_pruning_with_hog");
        names.Add("intra_sf.disable_smooth_intra");
        names.Add("intra_sf.prune_smooth_intra_mode_for_chroma");
        names.Add("intra_sf.prune_filter_intra_level");
        names.Add("intra_sf.prune_palette_search_level");
        names.Add("intra_sf.prune_luma_palette_size_search_level");
        names.Add("intra_sf.prune_chroma_modes_using_luma_winner");
        names.Add("intra_sf.dv_cost_upd_level");
        names.Add("intra_sf.cfl_search_range");
        names.Add("intra_sf.top_intra_model_count_allowed");
        names.Add("intra_sf.adapt_top_model_rd_count_using_neighbors");
        names.Add("intra_sf.prune_luma_odd_delta_angles_in_intra");
        names.Add("intra_sf.early_term_chroma_palette_size_search");
        names.Add("intra_sf.skip_filter_intra_in_inter_frames");
        names.Add("tx_sf.inter_tx_size_search_init_depth_sqr");
        names.Add("tx_sf.inter_tx_size_search_init_depth_rect");
        names.Add("tx_sf.intra_tx_size_search_init_depth_sqr");
        names.Add("tx_sf.intra_tx_size_search_init_depth_rect");
        names.Add("tx_sf.tx_size_search_lgr_block");
        names.Add("tx_sf.tx_type_search.prune_2d_txfm_mode");
        names.Add("tx_sf.tx_type_search.fast_intra_tx_type_search");
        names.Add("tx_sf.tx_type_search.fast_inter_tx_type_prob_thresh");
        names.Add("tx_sf.tx_type_search.use_reduced_intra_txset");
        names.Add("tx_sf.tx_type_search.use_skip_flag_prediction");
        names.Add("tx_sf.tx_type_search.ml_tx_split_thresh");
        names.Add("tx_sf.tx_type_search.skip_tx_search");
        names.Add("tx_sf.tx_type_search.prune_tx_type_using_stats");
        names.Add("tx_sf.tx_type_search.prune_tx_type_est_rd");
        names.Add("tx_sf.tx_type_search.winner_mode_tx_type_pruning");
        names.Add("tx_sf.txb_split_cap");
        names.Add("tx_sf.adaptive_txb_search_level");
        names.Add("tx_sf.model_based_prune_tx_search_level");
        names.Add("tx_sf.refine_fast_tx_search_results");
        names.Add("tx_sf.prune_tx_size_level");
        names.Add("tx_sf.prune_intra_tx_depths_using_nn");
        names.Add("tx_sf.use_rd_based_breakout_for_intra_tx_search");
        names.Add("tx_sf.prune_inter_tx_split_rd_eval_lvl");
        names.Add("tx_sf.use_chroma_trellis_rd_mult");
        names.Add("rd_sf.simple_model_rd_from_var");
        names.Add("rd_sf.tx_domain_dist_level");
        names.Add("rd_sf.tx_domain_dist_thres_level");
        names.Add("rd_sf.optimize_coefficients");
        names.Add("rd_sf.use_mb_rd_hash");
        names.Add("rd_sf.perform_coeff_opt");
        names.Add("winner_mode_sf.enable_winner_mode_for_coeff_opt");
        names.Add("winner_mode_sf.enable_winner_mode_for_tx_size_srch");
        names.Add("winner_mode_sf.tx_size_search_level");
        names.Add("winner_mode_sf.enable_winner_mode_for_use_tx_domain_dist");
        names.Add("winner_mode_sf.multi_winner_mode_type");
        names.Add("winner_mode_sf.motion_mode_for_winner_cand");
        names.Add("winner_mode_sf.dc_blk_pred_level");
        names.Add("winner_mode_sf.winner_mode_ifs");
        names.Add("winner_mode_sf.prune_winner_mode_eval_level");
        names.Add("lpf_sf.lpf_pick");
        names.Add("lpf_sf.use_coarse_filter_level_search");
        names.Add("lpf_sf.adaptive_luma_loop_filter_skip");
        names.Add("lpf_sf.skip_loop_filter_using_filt_error");
        names.Add("lpf_sf.cdef_pick_method");
        names.Add("lpf_sf.zero_low_cdef_strengths");
        names.Add("lpf_sf.adaptive_cdef_mode");
        names.Add("lpf_sf.dual_sgr_penalty_level");
        names.Add("lpf_sf.switchable_lr_with_bias_level");
        names.Add("lpf_sf.enable_sgr_ep_pruning");
        names.Add("lpf_sf.disable_loop_restoration_chroma");
        names.Add("lpf_sf.disable_loop_restoration_luma");
        names.Add("lpf_sf.min_lr_unit_size");
        names.Add("lpf_sf.max_lr_unit_size");
        names.Add("lpf_sf.prune_wiener_based_on_src_var");
        names.Add("lpf_sf.prune_sgr_based_on_wiener");
        names.Add("lpf_sf.reduce_wiener_window_size");
        names.Add("lpf_sf.disable_wiener_filter");
        names.Add("lpf_sf.disable_sgr_filter");
        names.Add("lpf_sf.disable_wiener_coeff_refine_search");
        names.Add("lpf_sf.use_downsampled_wiener_stats");
        names.Add("rt_sf.check_intra_pred_nonrd");
        names.Add("rt_sf.skip_intra_pred");
        names.Add("rt_sf.estimate_motion_for_var_based_partition");
        names.Add("rt_sf.nonrd_check_partition_merge_mode");
        names.Add("rt_sf.nonrd_check_partition_split");
        names.Add("rt_sf.mode_search_skip_flags");
        names.Add("rt_sf.nonrd_prune_ref_frame_search");
        names.Add("rt_sf.use_nonrd_pick_mode");
        names.Add("rt_sf.discount_color_cost");
        names.Add("rt_sf.use_nonrd_altref_frame");
        names.Add("rt_sf.use_comp_ref_nonrd");
        for (int i = 0; i < 3; ++i) names.Add($"rt_sf.ref_frame_comp_nonrd[{i}]");
        names.Add("rt_sf.use_real_time_ref_set");
        names.Add("rt_sf.short_circuit_low_temp_var");
        names.Add("rt_sf.reuse_inter_pred_nonrd");
        names.Add("rt_sf.num_inter_modes_for_tx_search");
        names.Add("rt_sf.use_nonrd_filter_search");
        names.Add("rt_sf.use_simple_rd_model");
        names.Add("rt_sf.hybrid_intra_pickmode");
        names.Add("rt_sf.prune_palette_search_nonrd");
        names.Add("rt_sf.source_metrics_sb_nonrd");
        names.Add("rt_sf.overshoot_detection_cbr");
        names.Add("rt_sf.check_scene_detection");
        names.Add("rt_sf.rc_adjust_keyframe");
        names.Add("rt_sf.rc_compute_spatial_var_sc_kf");
        names.Add("rt_sf.prefer_large_partition_blocks");
        names.Add("rt_sf.use_temporal_noise_estimate");
        names.Add("rt_sf.fullpel_search_step_param");
        for (int i = 0; i < BLOCK_SIZES; ++i) names.Add($"rt_sf.intra_y_mode_bsize_mask_nrd[{i}]");
        names.Add("rt_sf.prune_hv_pred_modes_using_src_sad");
        names.Add("rt_sf.nonrd_aggressive_skip");
        names.Add("rt_sf.skip_cdef_sb");
        names.Add("rt_sf.selective_cdf_update");
        names.Add("rt_sf.rt_use_intrabc");
        names.Add("rt_sf.rt_prune_intrabc_nonrd");
        names.Add("rt_sf.rt_intrabc_miss_mode");
        names.Add("rt_sf.force_only_last_ref");
        names.Add("rt_sf.force_large_partition_blocks_intra");
        names.Add("rt_sf.use_fast_fixed_part");
        names.Add("rt_sf.increase_source_sad_thresh");
        names.Add("rt_sf.skip_tx_no_split_var_based_partition");
        names.Add("rt_sf.skip_newmv_mode_based_on_sse");
        names.Add("rt_sf.gf_length_lvl");
        names.Add("rt_sf.prune_inter_modes_with_golden_ref");
        names.Add("rt_sf.prune_inter_modes_wrt_gf_arf_based_on_sad");
        names.Add("rt_sf.prune_inter_modes_using_temp_var");
        names.Add("rt_sf.reduce_mv_pel_precision_highmotion");
        names.Add("rt_sf.reduce_mv_pel_precision_lowcomplex");
        names.Add("rt_sf.prune_intra_mode_based_on_mv_range");
        names.Add("rt_sf.var_part_split_threshold_shift");
        names.Add("rt_sf.var_part_based_on_qidx");
        names.Add("rt_sf.gf_refresh_based_on_qp");
        names.Add("rt_sf.use_rtc_tf");
        names.Add("rt_sf.use_idtx_nonrd");
        names.Add("rt_sf.prune_idtx_nonrd");
        names.Add("rt_sf.dct_only_palette_nonrd");
        names.Add("rt_sf.skip_lf_screen");
        names.Add("rt_sf.thresh_active_maps_skip_lf_cdef");
        names.Add("rt_sf.part_early_exit_zeromv");
        names.Add("rt_sf.sse_early_term_inter_search");
        names.Add("rt_sf.sad_based_adp_altref_lag");
        names.Add("rt_sf.partition_direct_merging");
        names.Add("rt_sf.tx_size_level_based_on_qstep");
        names.Add("rt_sf.vbp_prune_16x16_split_using_min_max_sub_blk_var");
        names.Add("rt_sf.screen_content_cdef_filter_qindex_thresh");
        names.Add("rt_sf.prune_compoundmode_with_singlecompound_var");
        names.Add("rt_sf.frame_level_mode_cost_update");
        names.Add("rt_sf.prune_h_pred_using_best_mode_so_far");
        names.Add("rt_sf.enable_intra_mode_pruning_using_neighbors");
        names.Add("rt_sf.prune_intra_mode_using_best_sad_so_far");
        names.Add("rt_sf.check_only_zero_zeromv_on_large_blocks");
        names.Add("rt_sf.disable_cdf_update_non_reference_frame");
        names.Add("rt_sf.prune_compoundmode_with_singlemode_var");
        names.Add("rt_sf.skip_compound_based_on_var");
        names.Add("rt_sf.set_zeromv_skip_based_on_source_sad");
        names.Add("rt_sf.use_adaptive_subpel_search");
        names.Add("rt_sf.enable_ref_short_signaling");
        names.Add("rt_sf.check_globalmv_on_single_ref");
        names.Add("rt_sf.increase_color_thresh_palette");
        names.Add("rt_sf.higher_thresh_scene_detection");
        names.Add("rt_sf.skip_newmv_flat_blocks_screen");
        names.Add("rt_sf.skip_encoding_non_reference_slide_change");
        names.Add("rt_sf.rc_faster_convergence_static");
        names.Add("rt_sf.skip_newmv_mode_sad_screen");
        return names.ToArray();
    }
}
