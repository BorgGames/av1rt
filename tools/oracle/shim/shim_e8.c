/* Shim for package e8 (speed_features.c, rd.c, rdopt.c subset): wrappers around libaom's
 * speed feature setters, rd constants and helpers, for differential tests of the C# port. */
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#include "config/aom_dsp_rtcd.h"
#include "config/av1_rtcd.h"
#include "av1/common/common_data.h"
#include "av1/common/entropy.h"
#include "av1/common/entropymode.h"
#include "av1/common/entropymv.h"
#include "av1/common/quant_common.h"
#include "av1/encoder/aq_cyclicrefresh.h"
#include "av1/encoder/cost.h"
#include "av1/encoder/encodemv.h"
#include "av1/encoder/encoder.h"
#include "av1/encoder/mcomp.h"
#include "av1/encoder/ratectrl.h"
#include "av1/encoder/rd.h"
#include "av1/encoder/rdopt.h"
#include "av1/encoder/speed_features.h"

#define EXPORT __declspec(dllexport)

/* ---- leaf functions ------------------------------------------------------------------ */

EXPORT int64_t aomshim_e8_block_error_lp(const int16_t *coeff, const int16_t *dqcoeff,
                                         int block_size) {
  return av1_block_error_lp_c(coeff, dqcoeff, block_size);
}

EXPORT int aomshim_e8_compute_rd_mult_based_on_qindex(int bit_depth, int update_type,
                                                      int qindex, int tuning, int mode) {
  return av1_compute_rd_mult_based_on_qindex((aom_bit_depth_t)bit_depth,
                                             (FRAME_UPDATE_TYPE)update_type, qindex,
                                             (aom_tune_metric)tuning, (MODE)mode);
}

EXPORT int aomshim_e8_compute_rd_mult(int qindex, int bit_depth, int update_type,
                                      int layer_depth, int boost_index, int frame_type,
                                      int use_fixed_qp_offsets,
                                      int is_stat_consumption_stage, int tuning, int mode) {
  return av1_compute_rd_mult(qindex, (aom_bit_depth_t)bit_depth,
                             (FRAME_UPDATE_TYPE)update_type, layer_depth, boost_index,
                             (FRAME_TYPE)frame_type, use_fixed_qp_offsets,
                             is_stat_consumption_stage, (aom_tune_metric)tuning, (MODE)mode);
}

EXPORT int aomshim_e8_intra_cost_penalty(int qindex, int qdelta, int bit_depth) {
  return av1_get_intra_cost_penalty(qindex, qdelta, (aom_bit_depth_t)bit_depth);
}

EXPORT void aomshim_e8_model_rd_from_var_lapndz(int64_t var, unsigned int n_log2,
                                                unsigned int qstep, int *rate,
                                                int64_t *dist) {
  av1_model_rd_from_var_lapndz(var, n_log2, qstep, rate, dist);
}

/* sad_per_bit_lut_8 is file-static: read it back through av1_set_sad_per_bit(). */
EXPORT void aomshim_e8_sad_per_bit_lut8(int *out /* [QINDEX_RANGE] */) {
  av1_init_me_luts();
  AV1_COMP *cpi = (AV1_COMP *)calloc(1, sizeof(*cpi));
  SequenceHeader *seq = (SequenceHeader *)calloc(1, sizeof(*seq));
  seq->bit_depth = AOM_BITS_8;
  cpi->common.seq_params = seq;
  for (int q = 0; q < QINDEX_RANGE; ++q) av1_set_sad_per_bit(cpi, &out[q], q);
  free(seq);
  free(cpi);
}

/* Dependencies owned by other packages, exported so their C# stubs can be checked locally. */
EXPORT int aomshim_e8_dc_quant_QTX(int qindex, int delta, int bit_depth) {
  return av1_dc_quant_QTX(qindex, delta, (aom_bit_depth_t)bit_depth);
}
EXPORT double aomshim_e8_convert_qindex_to_q(int qindex, int bit_depth) {
  return av1_convert_qindex_to_q(qindex, (aom_bit_depth_t)bit_depth);
}
EXPORT void aomshim_e8_cost_tokens_from_cdf(int *costs, const uint16_t *cdf,
                                            const int *inv_map) {
  av1_init_cost_luts();
  av1_cost_tokens_from_cdf(costs, cdf, inv_map);
}
/* mvcost0/mvcost1 point at the middle (MV_MAX) of their tables; nmvc is an nmv_context as a
 * flat array of aom_cdf_prob in declaration order. */
EXPORT void aomshim_e8_build_nmv_cost_table(int *mvjoint, int *mvcost0, int *mvcost1,
                                            const uint16_t *nmvc, int precision) {
  nmv_context ctx;
  memcpy(&ctx, nmvc, sizeof(ctx));
  int *mvcost[2] = { mvcost0, mvcost1 };
  av1_build_nmv_cost_table(mvjoint, mvcost, &ctx, (MvSubpelPrecision)precision);
}

/* ---- encoder state ------------------------------------------------------------------- */

/* Index of each input parameter in the `p` array (mirrored in the C# test). */
enum {
  P_SPEED,
  P_WIDTH,
  P_HEIGHT,
  P_FRAME_TYPE,
  P_UPDATE_TYPE,
  P_BASE_QINDEX,
  P_AQ_MODE,
  P_RC_MODE,
  P_NUM_WORKERS,
  P_ROW_MT,
  P_KEY_FREQ_MAX,
  P_ENABLE_TX_SIZE_SEARCH,
  P_GF_CBR_BOOST_PCT,
  P_DISABLE_TRELLIS_QUANT,
  P_RTC_EXTERNAL_RATECTRL,
  P_LAG_IN_FRAMES,
  P_ALT_REF_FRAME,
  P_IS_SRC_FRAME_ALT_REF,
  P_ACTIVE_MAP,
  P_ENABLE_SUPERRES,
  P_CALC_PSNR,
  P_CFG_WIDTH,
  P_CFG_HEIGHT,
  P_TUNING,
  P_LOSSLESS,
  P_SEQ_PARAMS_LOCKED,
  P_SHOW_FRAME,
  P_Y_DC_DELTA_Q,
  P_LAYER_DEPTH,
  P_GFU_BOOST,
  P_USE_FIXED_QP_OFFSETS,
  P_FRAMES_SINCE_KEY,
  P_FRAME_NUMBER,
  P_DISABLE_CDF_UPDATE,
  P_ALLOW_HP,
  P_FORCE_INTEGER_MV,
  P_SKIP_MODE_FLAG,
  P_COST_UPD_COEFF,
  P_COST_UPD_MODE,
  P_COST_UPD_MV,
  P_COST_UPD_DV,
  P_CDF_UPDATE_MODE,
  P_FRAMES_SINCE_LAST_UPDATE,
  P_HIGH_SOURCE_SAD,
  P_MAXQ_SCENE_CHANGE,
  P_SEG_ENABLED,
  P_ALLOC_MV_COSTS,
  P_COUNT
};

typedef struct {
  AV1_COMP *cpi;
  AV1_PRIMARY *ppi;
  SequenceHeader *seq;
  FRAME_CONTEXT *fc;
  CYCLIC_REFRESH *cr;
  MvCosts *mv_costs;
} Ctx;

static void ctx_setup(Ctx *c, const int *p) {
  c->cpi = (AV1_COMP *)calloc(1, sizeof(*c->cpi));
  c->ppi = (AV1_PRIMARY *)calloc(1, sizeof(*c->ppi));
  c->seq = (SequenceHeader *)calloc(1, sizeof(*c->seq));
  c->fc = (FRAME_CONTEXT *)calloc(1, sizeof(*c->fc));
  c->cr = (CYCLIC_REFRESH *)calloc(1, sizeof(*c->cr));
  c->mv_costs = p[P_ALLOC_MV_COSTS] ? (MvCosts *)calloc(1, sizeof(*c->mv_costs)) : NULL;
  AV1_COMP *const cpi = c->cpi;
  AV1_COMMON *const cm = &cpi->common;
  cpi->ppi = c->ppi;
  cm->seq_params = c->seq;
  cm->fc = c->fc;
  cpi->cyclic_refresh = c->cr;
  cpi->td.mb.mv_costs = c->mv_costs;

  c->seq->bit_depth = AOM_BITS_8;
  c->seq->enable_masked_compound = 1;
  c->seq->enable_interintra_compound = 1;
  c->seq->enable_dual_filter = 1;
  c->seq->enable_restoration = 1;
  c->seq->order_hint_info.enable_dist_wtd_comp = 1;

  cpi->oxcf.mode = REALTIME;
  cpi->oxcf.pass = AOM_RC_ONE_PASS;
  cpi->speed = p[P_SPEED];
  cm->width = p[P_WIDTH];
  cm->height = p[P_HEIGHT];
  cm->current_frame.frame_type = (FRAME_TYPE)p[P_FRAME_TYPE];
  cpi->gf_frame_index = 0;
  c->ppi->gf_group.update_type[0] = (FRAME_UPDATE_TYPE)p[P_UPDATE_TYPE];
  c->ppi->gf_group.layer_depth[0] = p[P_LAYER_DEPTH];
  cm->quant_params.base_qindex = p[P_BASE_QINDEX];
  cm->quant_params.y_dc_delta_q = p[P_Y_DC_DELTA_Q];
  cpi->oxcf.q_cfg.aq_mode = (AQ_MODE)p[P_AQ_MODE];
  cpi->oxcf.q_cfg.use_fixed_qp_offsets = p[P_USE_FIXED_QP_OFFSETS];
  cpi->oxcf.rc_cfg.mode = (enum aom_rc_mode)p[P_RC_MODE];
  cpi->mt_info.num_workers = p[P_NUM_WORKERS];
  cpi->oxcf.row_mt = p[P_ROW_MT];
  cpi->oxcf.kf_cfg.key_freq_max = p[P_KEY_FREQ_MAX];
  cpi->oxcf.txfm_cfg.enable_tx_size_search = p[P_ENABLE_TX_SIZE_SEARCH];
  cpi->oxcf.rc_cfg.gf_cbr_boost_pct = p[P_GF_CBR_BOOST_PCT];
  cpi->oxcf.algo_cfg.disable_trellis_quant = p[P_DISABLE_TRELLIS_QUANT];
  cpi->oxcf.algo_cfg.cdf_update_mode = (uint8_t)p[P_CDF_UPDATE_MODE];
  cpi->rc.rtc_external_ratectrl = p[P_RTC_EXTERNAL_RATECTRL];
  cpi->oxcf.gf_cfg.lag_in_frames = p[P_LAG_IN_FRAMES];
  cpi->refresh_frame.alt_ref_frame = p[P_ALT_REF_FRAME] != 0;
  cpi->rc.is_src_frame_alt_ref = p[P_IS_SRC_FRAME_ALT_REF];
  cpi->active_map.enabled = p[P_ACTIVE_MAP];
  cpi->oxcf.superres_cfg.enable_superres = p[P_ENABLE_SUPERRES];
  c->ppi->b_calculate_psnr = p[P_CALC_PSNR];
  cpi->oxcf.frm_dim_cfg.width = p[P_CFG_WIDTH];
  cpi->oxcf.frm_dim_cfg.height = p[P_CFG_HEIGHT];
  cpi->oxcf.tune_cfg.tuning = (aom_tune_metric)p[P_TUNING];
  cpi->oxcf.rc_cfg.best_allowed_q = 0;
  cpi->oxcf.rc_cfg.worst_allowed_q = p[P_LOSSLESS] ? 0 : 255;
  c->ppi->seq_params_locked = p[P_SEQ_PARAMS_LOCKED];
  cm->show_frame = p[P_SHOW_FRAME];
  cpi->svc.number_spatial_layers = 1;
  cpi->svc.number_temporal_layers = 1;

  c->ppi->p_rc.gfu_boost = p[P_GFU_BOOST];
  cpi->rc.frames_since_key = p[P_FRAMES_SINCE_KEY];
  cm->current_frame.frame_number = (unsigned int)p[P_FRAME_NUMBER];
  cm->features.disable_cdf_update = p[P_DISABLE_CDF_UPDATE] != 0;
  cm->features.allow_high_precision_mv = p[P_ALLOW_HP] != 0;
  cm->features.cur_frame_force_integer_mv = p[P_FORCE_INTEGER_MV] != 0;
  cm->current_frame.skip_mode_info.skip_mode_flag = p[P_SKIP_MODE_FLAG];
  cpi->oxcf.cost_upd_freq.coeff = (COST_UPDATE_TYPE)p[P_COST_UPD_COEFF];
  cpi->oxcf.cost_upd_freq.mode = (COST_UPDATE_TYPE)p[P_COST_UPD_MODE];
  cpi->oxcf.cost_upd_freq.mv = (COST_UPDATE_TYPE)p[P_COST_UPD_MV];
  cpi->oxcf.cost_upd_freq.dv = (COST_UPDATE_TYPE)p[P_COST_UPD_DV];
  cpi->frames_since_last_update = p[P_FRAMES_SINCE_LAST_UPDATE];
  cpi->rc.high_source_sad = p[P_HIGH_SOURCE_SAD];
  c->cr->counter_encode_maxq_scene_change = p[P_MAXQ_SCENE_CHANGE];
  if (p[P_SEG_ENABLED]) {
    struct segmentation *seg = &cm->seg;
    seg->enabled = 1;
    seg->feature_mask[1] |= 1u << SEG_LVL_ALT_Q;
    seg->feature_data[1][SEG_LVL_ALT_Q] = -20;
    seg->feature_mask[2] |= 1u << SEG_LVL_ALT_Q;
    seg->feature_data[2][SEG_LVL_ALT_Q] = 35;
  }
}

static void ctx_free(Ctx *c) {
  free(c->mv_costs);
  free(c->cr);
  free(c->fc);
  free(c->seq);
  free(c->ppi);
  free(c->cpi);
}

static void set_speed_features(AV1_COMP *cpi) {
  av1_set_speed_features_framesize_independent(cpi, cpi->speed);
  av1_set_speed_features_framesize_dependent(cpi, cpi->speed);
  av1_set_speed_features_qindex_dependent(cpi, cpi->speed);
}

#define W(v)                         \
  do {                               \
    if (n < cap) out[n] = (int64_t)(v); \
    n++;                             \
  } while (0)
#define WF(v)                 \
  do {                        \
    float f_ = (v);           \
    int32_t b_;               \
    memcpy(&b_, &f_, 4);      \
    W(b_);                    \
  } while (0)

/* Runs the three speed feature setters and serialises every SPEED_FEATURES field (in the
 * order of speed_features.h), then WinnerModeParams, the selected sub-pixel search function
 * and the sequence header flags they update. Returns the number of values (int64). */
EXPORT int aomshim_e8_speed_features(const int *p, int64_t *out, int cap) {
  Ctx c;
  ctx_setup(&c, p);
  AV1_COMP *const cpi = c.cpi;
  set_speed_features(cpi);
  const SPEED_FEATURES *const sf = &cpi->sf;
  int n = 0;
  W(sf->hl_sf.frame_parameter_update);
  W(sf->hl_sf.recode_loop);
  W(sf->hl_sf.recode_tolerance);
  W(sf->hl_sf.high_precision_mv_usage);
  W(sf->hl_sf.static_segmentation);
  W(sf->hl_sf.superres_auto_search_type);
  W(sf->hl_sf.disable_extra_sc_testing);
  W(sf->hl_sf.second_alt_ref_filtering);
  W(sf->hl_sf.adjust_num_frames_for_arf_filtering);
  W(sf->hl_sf.accurate_bit_estimate);
  W(sf->hl_sf.weight_calc_level_in_tf);
  W(sf->hl_sf.allow_sub_blk_me_in_tf);
  W(sf->hl_sf.ref_frame_mvs_lvl);
  W(sf->hl_sf.screen_detection_mode2_fast_detection);
  W(sf->hl_sf.weighted_chroma_distortion);
  W(sf->fp_sf.reduce_mv_step_param);
  W(sf->fp_sf.skip_motion_search_threshold);
  W(sf->fp_sf.disable_recon);
  W(sf->fp_sf.skip_zeromv_motion_search);
  W(sf->tpl_sf.gop_length_decision_method);
  W(sf->tpl_sf.prune_intra_modes);
  W(sf->tpl_sf.reduce_first_step_size);
  W(sf->tpl_sf.skip_alike_starting_mv);
  W(sf->tpl_sf.subpel_force_stop);
  W(sf->tpl_sf.search_method);
  W(sf->tpl_sf.prune_starting_mv);
  W(sf->tpl_sf.prune_ref_frames_in_tpl);
  W(sf->tpl_sf.allow_compound_pred);
  W(sf->tpl_sf.use_y_only_rate_distortion);
  W(sf->tpl_sf.use_sad_for_mode_decision);
  W(sf->tpl_sf.reduce_num_frames);
  W(sf->gm_sf.gm_search_type);
  W(sf->gm_sf.prune_ref_frame_for_gm_search);
  W(sf->gm_sf.prune_zero_mv_with_sse);
  W(sf->gm_sf.disable_gm_search_based_on_stats);
  W(sf->gm_sf.downsample_level);
  W(sf->gm_sf.num_refinement_steps);
  W(sf->gm_sf.gm_erroradv_tr_level);
  W(sf->part_sf.partition_search_type);
  W(sf->part_sf.fixed_partition_size);
  W(sf->part_sf.prune_ext_partition_types_search_level);
  W(sf->part_sf.prune_part4_search);
  W(sf->part_sf.ml_prune_partition);
  W(sf->part_sf.ml_early_term_after_part_split_level);
  W(sf->part_sf.less_rectangular_check_level);
  W(sf->part_sf.use_square_partition_only_threshold);
  W(sf->part_sf.auto_max_partition_based_on_simple_motion);
  W(sf->part_sf.default_min_partition_size);
  W(sf->part_sf.default_max_partition_size);
  W(sf->part_sf.adjust_var_based_rd_partitioning);
  W(sf->part_sf.partition_search_breakout_dist_thr);
  W(sf->part_sf.partition_search_breakout_rate_thr);
  for (int i = 0; i < PARTITION_BLOCK_SIZES; ++i) WF(sf->part_sf.ml_partition_search_breakout_thresh[i]);
  W(sf->part_sf.ml_partition_search_breakout_model_index);
  W(sf->part_sf.ml_4_partition_search_level_index);
  W(sf->part_sf.simple_motion_search_prune_agg);
  W(sf->part_sf.simple_motion_search_prune_rect);
  W(sf->part_sf.simple_motion_search_split);
  W(sf->part_sf.simple_motion_search_early_term_none);
  W(sf->part_sf.simple_motion_search_reduce_search_steps);
  W(sf->part_sf.max_intra_bsize);
  W(sf->part_sf.intra_cnn_based_part_prune_level);
  W(sf->part_sf.ext_partition_eval_thresh);
  W(sf->part_sf.ext_part_eval_based_on_cur_best);
  W(sf->part_sf.rect_partition_eval_thresh);
  W(sf->part_sf.prune_ext_part_using_split_info);
  W(sf->part_sf.prune_rectangular_split_based_on_qidx);
  W(sf->part_sf.prune_rect_part_using_4x4_var_deviation);
  W(sf->part_sf.prune_rect_part_using_none_pred_mode);
  W(sf->part_sf.early_term_after_none_split);
  W(sf->part_sf.ml_predict_breakout_level);
  W(sf->part_sf.prune_sub_8x8_partition_level);
  W(sf->part_sf.simple_motion_search_rect_split);
  W(sf->part_sf.reuse_prev_rd_results_for_part_ab);
  W(sf->part_sf.reuse_best_prediction_for_part_ab);
  W(sf->part_sf.use_best_rd_for_pruning);
  W(sf->part_sf.skip_non_sq_part_based_on_none);
  W(sf->part_sf.disable_8x8_part_based_on_qidx);
  W(sf->part_sf.prune_h_or_v_4part_using_sms_info);
  W(sf->part_sf.split_partition_penalty_level);
  W(sf->mv_sf.search_method);
  W(sf->mv_sf.use_bsize_dependent_search_method);
  W(sf->mv_sf.auto_mv_step_size);
  W(sf->mv_sf.subpel_search_method);
  W(sf->mv_sf.subpel_iters_per_step);
  W(sf->mv_sf.subpel_force_stop);
  W(sf->mv_sf.simple_motion_subpel_force_stop);
  W(sf->mv_sf.use_accurate_subpel_search);
  W(sf->mv_sf.exhaustive_searches_thresh);
  for (int i = 0; i < MAX_MESH_STEP; ++i) { W(sf->mv_sf.mesh_patterns[i].range); W(sf->mv_sf.mesh_patterns[i].interval); }
  for (int i = 0; i < MAX_MESH_STEP; ++i) { W(sf->mv_sf.intrabc_mesh_patterns[i].range); W(sf->mv_sf.intrabc_mesh_patterns[i].interval); }
  W(sf->mv_sf.reduce_search_range);
  W(sf->mv_sf.prune_mesh_search);
  W(sf->mv_sf.use_fullpel_costlist);
  W(sf->mv_sf.obmc_full_pixel_search_level);
  W(sf->mv_sf.full_pixel_search_level);
  W(sf->mv_sf.use_intrabc);
  W(sf->mv_sf.prune_intrabc_candidate_block_hash_search);
  W(sf->mv_sf.intrabc_search_level);
  W(sf->mv_sf.hash_max_8x8_intrabc_blocks);
  W(sf->mv_sf.use_downsampled_sad);
  W(sf->mv_sf.disable_extensive_joint_motion_search);
  W(sf->mv_sf.disable_second_mv);
  W(sf->mv_sf.skip_fullpel_search_using_startmv_refmv);
  W(sf->mv_sf.warp_search_method);
  W(sf->mv_sf.warp_search_iters);
  W(sf->inter_sf.inter_mode_rd_model_estimation);
  for (int i = 0; i < TX_SEARCH_CASES; ++i) W(sf->inter_sf.txfm_rd_gate_level[i]);
  W(sf->inter_sf.reduce_inter_modes);
  W(sf->inter_sf.adaptive_rd_thresh);
  W(sf->inter_sf.prune_inter_modes_if_skippable);
  W(sf->inter_sf.selective_ref_frame);
  W(sf->inter_sf.retain_last2_in_partial_high_motion);
  W(sf->inter_sf.prune_ref_frame_for_rect_partitions);
  W(sf->inter_sf.alt_ref_search_fp);
  W(sf->inter_sf.prune_single_ref);
  W(sf->inter_sf.prune_comp_ref_frames);
  W(sf->inter_sf.skip_newmv_in_drl);
  W(sf->inter_sf.skip_repeated_ref_mv);
  W(sf->inter_sf.perform_best_rd_based_gating_for_chroma);
  W(sf->inter_sf.reuse_inter_intra_mode);
  W(sf->inter_sf.prune_comp_type_by_model_rd);
  W(sf->inter_sf.prune_comp_type_by_comp_avg);
  W(sf->inter_sf.prune_comp_search_by_single_result);
  W(sf->inter_sf.prune_mode_search_simple_translation);
  W(sf->inter_sf.prune_compound_using_single_ref);
  W(sf->inter_sf.prune_ext_comp_using_neighbors);
  W(sf->inter_sf.skip_ext_comp_nearmv_mode);
  W(sf->inter_sf.prune_comp_using_best_single_mode_ref);
  W(sf->inter_sf.prune_nearest_near_mv_using_refmv_weight);
  W(sf->inter_sf.prune_ref_mv_idx_search);
  W(sf->inter_sf.disable_onesided_comp);
  W(sf->inter_sf.prune_obmc_prob_thresh);
  W(sf->inter_sf.prune_warped_prob_thresh);
  W(sf->inter_sf.disable_interintra_wedge_var_thresh);
  W(sf->inter_sf.disable_interinter_wedge_var_thresh);
  W(sf->inter_sf.fast_interintra_wedge_search);
  W(sf->inter_sf.fast_wedge_sign_estimate);
  W(sf->inter_sf.disable_interinter_wedge_newmv_search);
  W(sf->inter_sf.use_dist_wtd_comp_flag);
  W(sf->inter_sf.mv_cost_upd_level);
  W(sf->inter_sf.coeff_cost_upd_level);
  W(sf->inter_sf.mode_cost_upd_level);
  W(sf->inter_sf.prune_inter_modes_based_on_tpl);
  W(sf->inter_sf.prune_nearmv_using_neighbors);
  W(sf->inter_sf.model_based_post_interp_filter_breakout);
  W(sf->inter_sf.reuse_compound_type_decision);
  W(sf->inter_sf.disable_masked_comp);
  W(sf->inter_sf.enable_fast_compound_mode_search);
  W(sf->inter_sf.reuse_mask_search_results);
  W(sf->inter_sf.enable_fast_wedge_mask_search);
  W(sf->inter_sf.inter_mode_txfm_breakout);
  W(sf->inter_sf.limit_inter_mode_cands);
  W(sf->inter_sf.limit_txfm_eval_per_mode);
  W(sf->inter_sf.extra_prune_warped);
  W(sf->inter_sf.skip_arf_compound);
  WF(sf->inter_sf.bias_warp_mode_rd_scale_pct);
  WF(sf->inter_sf.bias_obmc_mode_rd_scale_pct);
  WF(sf->inter_sf.bias_gm_mode_rd_scale_pct);
  W(sf->inter_sf.skip_cmp_using_top_cmp_avg_est_rd_lvl);
  W(sf->inter_sf.skip_interinter_wedge_search_based_on_mse);
  W(sf->inter_sf.enable_comp_wedge_search_using_model_rd);
  W(sf->inter_sf.skip_interintra_using_top_smooth_rd_lvl);
  W(sf->interp_sf.use_fast_interpolation_filter_search);
  W(sf->interp_sf.disable_dual_filter);
  W(sf->interp_sf.use_interp_filter);
  W(sf->interp_sf.skip_sharp_interp_filter_search);
  W(sf->interp_sf.cb_pred_filter_search);
  W(sf->interp_sf.adaptive_interp_filter_search);
  W(sf->interp_sf.skip_interp_filter_search);
  W(sf->interp_sf.use_more_sharp_interp);
  W(sf->interp_sf.skip_model_rd_uv);
  for (int i = 0; i < TX_SIZES; ++i) W(sf->intra_sf.intra_y_mode_mask[i]);
  for (int i = 0; i < TX_SIZES; ++i) W(sf->intra_sf.intra_uv_mode_mask[i]);
  W(sf->intra_sf.skip_intra_in_interframe);
  W(sf->intra_sf.intra_pruning_with_hog);
  W(sf->intra_sf.chroma_intra_pruning_with_hog);
  W(sf->intra_sf.disable_smooth_intra);
  W(sf->intra_sf.prune_smooth_intra_mode_for_chroma);
  W(sf->intra_sf.prune_filter_intra_level);
  W(sf->intra_sf.prune_palette_search_level);
  W(sf->intra_sf.prune_luma_palette_size_search_level);
  W(sf->intra_sf.prune_chroma_modes_using_luma_winner);
  W(sf->intra_sf.dv_cost_upd_level);
  W(sf->intra_sf.cfl_search_range);
  W(sf->intra_sf.top_intra_model_count_allowed);
  W(sf->intra_sf.adapt_top_model_rd_count_using_neighbors);
  W(sf->intra_sf.prune_luma_odd_delta_angles_in_intra);
  W(sf->intra_sf.early_term_chroma_palette_size_search);
  W(sf->intra_sf.skip_filter_intra_in_inter_frames);
  W(sf->tx_sf.inter_tx_size_search_init_depth_sqr);
  W(sf->tx_sf.inter_tx_size_search_init_depth_rect);
  W(sf->tx_sf.intra_tx_size_search_init_depth_sqr);
  W(sf->tx_sf.intra_tx_size_search_init_depth_rect);
  W(sf->tx_sf.tx_size_search_lgr_block);
  W(sf->tx_sf.tx_type_search.prune_2d_txfm_mode);
  W(sf->tx_sf.tx_type_search.fast_intra_tx_type_search);
  W(sf->tx_sf.tx_type_search.fast_inter_tx_type_prob_thresh);
  W(sf->tx_sf.tx_type_search.use_reduced_intra_txset);
  W(sf->tx_sf.tx_type_search.use_skip_flag_prediction);
  W(sf->tx_sf.tx_type_search.ml_tx_split_thresh);
  W(sf->tx_sf.tx_type_search.skip_tx_search);
  W(sf->tx_sf.tx_type_search.prune_tx_type_using_stats);
  W(sf->tx_sf.tx_type_search.prune_tx_type_est_rd);
  W(sf->tx_sf.tx_type_search.winner_mode_tx_type_pruning);
  W(sf->tx_sf.txb_split_cap);
  W(sf->tx_sf.adaptive_txb_search_level);
  W(sf->tx_sf.model_based_prune_tx_search_level);
  W(sf->tx_sf.refine_fast_tx_search_results);
  W(sf->tx_sf.prune_tx_size_level);
  W(sf->tx_sf.prune_intra_tx_depths_using_nn);
  W(sf->tx_sf.use_rd_based_breakout_for_intra_tx_search);
  W(sf->tx_sf.prune_inter_tx_split_rd_eval_lvl);
  W(sf->tx_sf.use_chroma_trellis_rd_mult);
  W(sf->rd_sf.simple_model_rd_from_var);
  W(sf->rd_sf.tx_domain_dist_level);
  W(sf->rd_sf.tx_domain_dist_thres_level);
  W(sf->rd_sf.optimize_coefficients);
  W(sf->rd_sf.use_mb_rd_hash);
  W(sf->rd_sf.perform_coeff_opt);
  W(sf->winner_mode_sf.enable_winner_mode_for_coeff_opt);
  W(sf->winner_mode_sf.enable_winner_mode_for_tx_size_srch);
  W(sf->winner_mode_sf.tx_size_search_level);
  W(sf->winner_mode_sf.enable_winner_mode_for_use_tx_domain_dist);
  W(sf->winner_mode_sf.multi_winner_mode_type);
  W(sf->winner_mode_sf.motion_mode_for_winner_cand);
  W(sf->winner_mode_sf.dc_blk_pred_level);
  W(sf->winner_mode_sf.winner_mode_ifs);
  W(sf->winner_mode_sf.prune_winner_mode_eval_level);
  W(sf->lpf_sf.lpf_pick);
  W(sf->lpf_sf.use_coarse_filter_level_search);
  W(sf->lpf_sf.adaptive_luma_loop_filter_skip);
  W(sf->lpf_sf.skip_loop_filter_using_filt_error);
  W(sf->lpf_sf.cdef_pick_method);
  W(sf->lpf_sf.zero_low_cdef_strengths);
  W(sf->lpf_sf.adaptive_cdef_mode);
  W(sf->lpf_sf.dual_sgr_penalty_level);
  W(sf->lpf_sf.switchable_lr_with_bias_level);
  W(sf->lpf_sf.enable_sgr_ep_pruning);
  W(sf->lpf_sf.disable_loop_restoration_chroma);
  W(sf->lpf_sf.disable_loop_restoration_luma);
  W(sf->lpf_sf.min_lr_unit_size);
  W(sf->lpf_sf.max_lr_unit_size);
  W(sf->lpf_sf.prune_wiener_based_on_src_var);
  W(sf->lpf_sf.prune_sgr_based_on_wiener);
  W(sf->lpf_sf.reduce_wiener_window_size);
  W(sf->lpf_sf.disable_wiener_filter);
  W(sf->lpf_sf.disable_sgr_filter);
  W(sf->lpf_sf.disable_wiener_coeff_refine_search);
  W(sf->lpf_sf.use_downsampled_wiener_stats);
  W(sf->rt_sf.check_intra_pred_nonrd);
  W(sf->rt_sf.skip_intra_pred);
  W(sf->rt_sf.estimate_motion_for_var_based_partition);
  W(sf->rt_sf.nonrd_check_partition_merge_mode);
  W(sf->rt_sf.nonrd_check_partition_split);
  W(sf->rt_sf.mode_search_skip_flags);
  W(sf->rt_sf.nonrd_prune_ref_frame_search);
  W(sf->rt_sf.use_nonrd_pick_mode);
  W(sf->rt_sf.discount_color_cost);
  W(sf->rt_sf.use_nonrd_altref_frame);
  W(sf->rt_sf.use_comp_ref_nonrd);
  for (int i = 0; i < 3; ++i) W(sf->rt_sf.ref_frame_comp_nonrd[i]);
  W(sf->rt_sf.use_real_time_ref_set);
  W(sf->rt_sf.short_circuit_low_temp_var);
  W(sf->rt_sf.reuse_inter_pred_nonrd);
  W(sf->rt_sf.num_inter_modes_for_tx_search);
  W(sf->rt_sf.use_nonrd_filter_search);
  W(sf->rt_sf.use_simple_rd_model);
  W(sf->rt_sf.hybrid_intra_pickmode);
  W(sf->rt_sf.prune_palette_search_nonrd);
  W(sf->rt_sf.source_metrics_sb_nonrd);
  W(sf->rt_sf.overshoot_detection_cbr);
  W(sf->rt_sf.check_scene_detection);
  W(sf->rt_sf.rc_adjust_keyframe);
  W(sf->rt_sf.rc_compute_spatial_var_sc_kf);
  W(sf->rt_sf.prefer_large_partition_blocks);
  W(sf->rt_sf.use_temporal_noise_estimate);
  W(sf->rt_sf.fullpel_search_step_param);
  for (int i = 0; i < BLOCK_SIZES; ++i) W(sf->rt_sf.intra_y_mode_bsize_mask_nrd[i]);
  W(sf->rt_sf.prune_hv_pred_modes_using_src_sad);
  W(sf->rt_sf.nonrd_aggressive_skip);
  W(sf->rt_sf.skip_cdef_sb);
  W(sf->rt_sf.selective_cdf_update);
  W(sf->rt_sf.rt_use_intrabc);
  W(sf->rt_sf.rt_prune_intrabc_nonrd);
  W(sf->rt_sf.rt_intrabc_miss_mode);
  W(sf->rt_sf.force_only_last_ref);
  W(sf->rt_sf.force_large_partition_blocks_intra);
  W(sf->rt_sf.use_fast_fixed_part);
  W(sf->rt_sf.increase_source_sad_thresh);
  W(sf->rt_sf.skip_tx_no_split_var_based_partition);
  W(sf->rt_sf.skip_newmv_mode_based_on_sse);
  W(sf->rt_sf.gf_length_lvl);
  W(sf->rt_sf.prune_inter_modes_with_golden_ref);
  W(sf->rt_sf.prune_inter_modes_wrt_gf_arf_based_on_sad);
  W(sf->rt_sf.prune_inter_modes_using_temp_var);
  W(sf->rt_sf.reduce_mv_pel_precision_highmotion);
  W(sf->rt_sf.reduce_mv_pel_precision_lowcomplex);
  W(sf->rt_sf.prune_intra_mode_based_on_mv_range);
  W(sf->rt_sf.var_part_split_threshold_shift);
  W(sf->rt_sf.var_part_based_on_qidx);
  W(sf->rt_sf.gf_refresh_based_on_qp);
  W(sf->rt_sf.use_rtc_tf);
  W(sf->rt_sf.use_idtx_nonrd);
  W(sf->rt_sf.prune_idtx_nonrd);
  W(sf->rt_sf.dct_only_palette_nonrd);
  W(sf->rt_sf.skip_lf_screen);
  W(sf->rt_sf.thresh_active_maps_skip_lf_cdef);
  W(sf->rt_sf.part_early_exit_zeromv);
  W(sf->rt_sf.sse_early_term_inter_search);
  W(sf->rt_sf.sad_based_adp_altref_lag);
  W(sf->rt_sf.partition_direct_merging);
  W(sf->rt_sf.tx_size_level_based_on_qstep);
  W(sf->rt_sf.vbp_prune_16x16_split_using_min_max_sub_blk_var);
  W(sf->rt_sf.screen_content_cdef_filter_qindex_thresh);
  W(sf->rt_sf.prune_compoundmode_with_singlecompound_var);
  W(sf->rt_sf.frame_level_mode_cost_update);
  W(sf->rt_sf.prune_h_pred_using_best_mode_so_far);
  W(sf->rt_sf.enable_intra_mode_pruning_using_neighbors);
  W(sf->rt_sf.prune_intra_mode_using_best_sad_so_far);
  W(sf->rt_sf.check_only_zero_zeromv_on_large_blocks);
  W(sf->rt_sf.disable_cdf_update_non_reference_frame);
  W(sf->rt_sf.prune_compoundmode_with_singlemode_var);
  W(sf->rt_sf.skip_compound_based_on_var);
  W(sf->rt_sf.set_zeromv_skip_based_on_source_sad);
  W(sf->rt_sf.use_adaptive_subpel_search);
  W(sf->rt_sf.enable_ref_short_signaling);
  W(sf->rt_sf.check_globalmv_on_single_ref);
  W(sf->rt_sf.increase_color_thresh_palette);
  W(sf->rt_sf.higher_thresh_scene_detection);
  W(sf->rt_sf.skip_newmv_flat_blocks_screen);
  W(sf->rt_sf.skip_encoding_non_reference_slide_change);
  W(sf->rt_sf.rc_faster_convergence_static);
  W(sf->rt_sf.skip_newmv_mode_sad_screen);
  const WinnerModeParams *const wmp = &cpi->winner_mode_params;
  for (int i = 0; i < MODE_EVAL_TYPES; ++i) {
    W(wmp->coeff_opt_thresholds[i][0]);
    W(wmp->coeff_opt_thresholds[i][1]);
    W(wmp->tx_size_search_methods[i]);
    W(wmp->use_transform_domain_distortion[i]);
    W(wmp->tx_domain_dist_threshold[i]);
    W(wmp->skip_txfm_level[i]);
    W(wmp->predict_dc_level[i]);
  }
  fractional_mv_step_fp *const f = cpi->mv_search_params.find_fractional_mv_step;
  W(f == av1_find_best_sub_pixel_tree                ? 0
    : f == av1_find_best_sub_pixel_tree_pruned      ? 1
    : f == av1_find_best_sub_pixel_tree_pruned_more ? 2
                                                    : -1);
  W(c.seq->enable_masked_compound);
  W(c.seq->enable_interintra_compound);
  W(c.seq->enable_dual_filter);
  W(c.seq->enable_restoration);
  W(c.seq->order_hint_info.enable_dist_wtd_comp);
  ctx_free(&c);
  return n;
}

/* Sets up default CDFs for base_qindex, runs the speed feature setters,
 * av1_set_rd_speed_thresholds and av1_initialize_rd_consts.
 * cdf_out receives the FRAME_CONTEXT CDFs (every aom_cdf_prob before `initialized`, in
 * declaration order); out receives RDMULT, errorperbit, thresh_mult[], threshes[][][],
 * ModeCosts, CoeffCosts (C layout). Returns the number of ints written to out. */
EXPORT int aomshim_e8_rd_consts(const int *p, uint16_t *cdf_out, int cdf_cap, int *cdf_count,
                                int *out, int cap) {
  Ctx c;
  ctx_setup(&c, p);
  AV1_COMP *const cpi = c.cpi;
  AV1_COMMON *const cm = &cpi->common;
  av1_init_cost_luts(); /* as av1_initialize_enc() does */
  av1_default_coef_probs(cm);
  av1_init_mode_probs(cm->fc);
  av1_init_mv_probs(cm);

  const int ncdf = (int)(offsetof(FRAME_CONTEXT, initialized) / sizeof(aom_cdf_prob));
  *cdf_count = ncdf;
  if (ncdf <= cdf_cap) memcpy(cdf_out, cm->fc, ncdf * sizeof(aom_cdf_prob));

  set_speed_features(cpi);
  av1_set_rd_speed_thresholds(cpi);
  av1_initialize_rd_consts(cpi);

  int n = 0;
  W(cpi->rd.RDMULT);
  W(cpi->td.mb.errorperbit);
  for (int i = 0; i < MAX_MODES; ++i) W(cpi->rd.thresh_mult[i]);
  for (int s = 0; s < MAX_SEGMENTS; ++s)
    for (int b = 0; b < BLOCK_SIZES_ALL; ++b)
      for (int m = 0; m < MAX_MODES; ++m) W(cpi->rd.threshes[s][b][m]);
  {
    const int *mc = (const int *)&cpi->td.mb.mode_costs;
    for (size_t i = 0; i < sizeof(ModeCosts) / sizeof(int); ++i) W(mc[i]);
  }
  {
    const int *cc = (const int *)&cpi->td.mb.coeff_costs;
    for (size_t i = 0; i < sizeof(CoeffCosts) / sizeof(int); ++i) W(cc[i]);
  }
  const MvCosts *const mv = c.mv_costs;
  if (mv) {
    for (int i = 0; i < MV_JOINTS; ++i) W(mv->nmv_joint_cost[i]);
    for (int k = 0; k < 2; ++k)
      for (int i = 0; i < MV_VALS; ++i) W(mv->nmv_cost_alloc[k][i]);
    for (int k = 0; k < 2; ++k)
      for (int i = 0; i < MV_VALS; ++i) W(mv->nmv_cost_hp_alloc[k][i]);
    for (int k = 0; k < 2; ++k) {
      W(mv->nmv_cost[k] ? mv->nmv_cost[k] - mv->nmv_cost_alloc[k] : -1);
      W(mv->nmv_cost_hp[k] ? mv->nmv_cost_hp[k] - mv->nmv_cost_hp_alloc[k] : -1);
    }
    W(mv->mv_cost_stack == NULL                    ? -1
      : (void *)mv->mv_cost_stack == (void *)mv->nmv_cost    ? 0
      : (void *)mv->mv_cost_stack == (void *)mv->nmv_cost_hp ? 1
                                                             : 2);
  }
  ctx_free(&c);
  return n;
}

/* av1_mv_pred on a 16x16 or 64x64 block. ref_center points at the co-located block in the
 * reference frame (the caller provides enough border for any clamped candidate).
 * mvs = { row0, col0, row1, col1 } are the first two ref_mv_stack entries of ref_frame.
 * out = { pred_mv_sad, pred_mv0_sad, pred_mv1_sad, max_mv_context }. */
EXPORT void aomshim_e8_mv_pred(const uint8_t *src, int src_stride, const uint8_t *ref_center,
                               int ref_stride, int bsize, int mi_row, int mi_col,
                               int mi_rows, int mi_cols, int ref_frame, const int *mvs,
                               int ref_mv_count, int gm_row, int gm_col, int *out) {
  AV1_COMP *cpi = (AV1_COMP *)calloc(1, sizeof(*cpi));
  AV1_PRIMARY *ppi = (AV1_PRIMARY *)calloc(1, sizeof(*ppi));
  cpi->ppi = ppi;
  ppi->fn_ptr[bsize].sdf = bsize == BLOCK_64X64 ? aom_sad64x64_c : aom_sad16x16_c;
  cpi->common.mi_params.mi_rows = mi_rows;
  cpi->common.mi_params.mi_cols = mi_cols;
  MACROBLOCK *x = &cpi->td.mb;
  x->plane[0].src.buf = (uint8_t *)src;
  x->plane[0].src.stride = src_stride;
  x->e_mbd.mi_row = mi_row;
  x->e_mbd.mi_col = mi_col;
  x->e_mbd.width = (uint8_t)mi_size_wide[bsize];
  x->e_mbd.height = (uint8_t)mi_size_high[bsize];
  x->mbmi_ext.ref_mv_stack[ref_frame][0].this_mv.as_mv.row = (int16_t)mvs[0];
  x->mbmi_ext.ref_mv_stack[ref_frame][0].this_mv.as_mv.col = (int16_t)mvs[1];
  x->mbmi_ext.ref_mv_stack[ref_frame][1].this_mv.as_mv.row = (int16_t)mvs[2];
  x->mbmi_ext.ref_mv_stack[ref_frame][1].this_mv.as_mv.col = (int16_t)mvs[3];
  x->mbmi_ext.ref_mv_count[ref_frame] = (uint8_t)ref_mv_count;
  x->mbmi_ext.global_mvs[ref_frame].as_mv.row = (int16_t)gm_row;
  x->mbmi_ext.global_mvs[ref_frame].as_mv.col = (int16_t)gm_col;
  x->pred_mv_sad[ref_frame] = -1;
  x->pred_mv0_sad[ref_frame] = -1;
  x->pred_mv1_sad[ref_frame] = -1;
  av1_mv_pred(cpi, x, (uint8_t *)ref_center, ref_stride, ref_frame, (BLOCK_SIZE)bsize);
  out[0] = x->pred_mv_sad[ref_frame];
  out[1] = x->pred_mv0_sad[ref_frame];
  out[2] = x->pred_mv1_sad[ref_frame];
  out[3] = (int)x->max_mv_context[ref_frame];
  free(ppi);
  free(cpi);
}
