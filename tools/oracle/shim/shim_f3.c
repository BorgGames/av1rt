/* Shim for package f3 (var_based_part.c, partition_search.c, context_tree.c): wrappers around
 * libaom's variance-based partitioning and partition-tree helpers, for differential tests of
 * the C# port. Every entry point builds its own zeroed encoder context (calloc), so the C#
 * harness can build the same one; see tests/Av1rt.Tests/F3/F3Context.cs. */
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#include "config/aom_dsp_rtcd.h"
#include "aom_dsp/variance.h"
#include "av1/common/av1_common_int.h"
#include "av1/common/common_data.h"
#include "av1/common/scale.h"
#include "av1/common/seg_common.h"
#include "av1/encoder/context_tree.h"
#include "av1/encoder/encoder.h"
#include "av1/encoder/var_based_part.h"

#define EXPORT __declspec(dllexport)

/* Parameter indices: keep in sync with F3Context.P. */
enum {
  P_WIDTH,
  P_HEIGHT,
  P_SB128,
  P_FRAME_TYPE,
  P_BASE_QINDEX,
  P_QINDEX,
  P_LOWSUMDIFF,
  P_SPEED,
  P_MODE,
  P_CONTENT,
  P_SPLIT_SHIFT,
  P_FORCE_LARGE_INTRA,
  P_PREFER_LARGE,
  P_VAR_PART_QIDX,
  P_DISABLE_8X8_QIDX,
  P_NOISE_ENABLED,
  P_NOISE_VALUE,
  P_NOISE_THRESH,
  P_FRAME_NUMBER,
  P_NON_REF_FRAME,
  P_FRAME_SOURCE_SAD,
  P_HIGH_MOTION_SCREEN,
  P_PARTITION_SEARCH_TYPE,
  P_TEMPORAL_LAYER_ID,
  P_NUM_TEMPORAL_LAYERS,
  P_AVG_SOURCE_SAD,
  P_USE_RTC_TF,
  P_EST_MOTION,
  P_NONRD_PRUNE_REF,
  P_SHORT_CIRCUIT,
  P_PART_EARLY_EXIT_ZEROMV,
  P_ZEROMV_SKIP_SRC_SAD,
  P_INCREASE_SRC_SAD_THRESH,
  P_FRAMES_SINCE_KEY,
  P_HIGH_SOURCE_SAD,
  P_PERCENT_MOTION,
  P_REF_FRAME_FLAGS,
  P_AQ_MODE,
  P_SEG_ENABLED,
  P_SEG_DELTA_Q1,
  P_SEG_DELTA_Q2,
  P_MI_ALLOC_BSIZE,
  P_USE_SRC_SAD_BLK,
  P_ROI_DELTA_QP,
  P_COUNT
};

/* Frame buffer layout shared with the C# harness: see F3Context.Layout. */
#define F3_BORDER 160
#define F3_UV_BORDER (F3_BORDER >> 1)
typedef struct {
  int aligned_w, aligned_h, y_stride, uv_stride, y_size, uv_size, frame_size;
  int y_origin, uv_origin;
} Layout;

static Layout layout(int width, int height) {
  Layout l;
  l.aligned_w = (width + 7) & ~7;
  l.aligned_h = (height + 7) & ~7;
  l.y_stride = (l.aligned_w + 2 * F3_BORDER + 31) & ~31;
  l.uv_stride = l.y_stride >> 1;
  l.y_size = l.y_stride * (l.aligned_h + 2 * F3_BORDER);
  l.uv_size = l.uv_stride * ((l.aligned_h >> 1) + 2 * F3_UV_BORDER);
  l.frame_size = l.y_size + 2 * l.uv_size;
  l.y_origin = F3_BORDER * l.y_stride + F3_BORDER;
  l.uv_origin = F3_UV_BORDER * l.uv_stride + F3_UV_BORDER;
  return l;
}

static void setup_buf(YV12_BUFFER_CONFIG *b, uint8_t *frame, const Layout *l, int width,
                      int height) {
  memset(b, 0, sizeof(*b));
  b->y_buffer = frame + l->y_origin;
  b->u_buffer = frame + l->y_size + l->uv_origin;
  b->v_buffer = frame + l->y_size + l->uv_size + l->uv_origin;
  b->y_stride = l->y_stride;
  b->uv_stride = l->uv_stride;
  b->y_width = l->aligned_w;
  b->y_height = l->aligned_h;
  b->uv_width = l->aligned_w >> 1;
  b->uv_height = l->aligned_h >> 1;
  b->y_crop_width = width;
  b->y_crop_height = height;
  b->uv_crop_width = (width + 1) >> 1;
  b->uv_crop_height = (height + 1) >> 1;
  b->border = F3_BORDER;
  b->subsampling_x = 1;
  b->subsampling_y = 1;
}

static void set_sad_fns(AV1_PRIMARY *ppi) {
  ppi->fn_ptr[BLOCK_4X4].sdf = aom_sad4x4_c;
  ppi->fn_ptr[BLOCK_4X8].sdf = aom_sad4x8_c;
  ppi->fn_ptr[BLOCK_8X4].sdf = aom_sad8x4_c;
  ppi->fn_ptr[BLOCK_8X8].sdf = aom_sad8x8_c;
  ppi->fn_ptr[BLOCK_8X16].sdf = aom_sad8x16_c;
  ppi->fn_ptr[BLOCK_16X8].sdf = aom_sad16x8_c;
  ppi->fn_ptr[BLOCK_16X16].sdf = aom_sad16x16_c;
  ppi->fn_ptr[BLOCK_16X32].sdf = aom_sad16x32_c;
  ppi->fn_ptr[BLOCK_32X16].sdf = aom_sad32x16_c;
  ppi->fn_ptr[BLOCK_32X32].sdf = aom_sad32x32_c;
  ppi->fn_ptr[BLOCK_32X64].sdf = aom_sad32x64_c;
  ppi->fn_ptr[BLOCK_64X32].sdf = aom_sad64x32_c;
  ppi->fn_ptr[BLOCK_64X64].sdf = aom_sad64x64_c;
  ppi->fn_ptr[BLOCK_64X128].sdf = aom_sad64x128_c;
  ppi->fn_ptr[BLOCK_128X64].sdf = aom_sad128x64_c;
  ppi->fn_ptr[BLOCK_128X128].sdf = aom_sad128x128_c;
}

/* Builds the frame-level context used by set_vbp_thresholds() and
 * av1_choose_var_based_partitioning(). */
static AV1_COMP *ctx_create(const int *p) {
  AV1_COMP *cpi = (AV1_COMP *)calloc(1, sizeof(*cpi));
  AV1_PRIMARY *ppi = (AV1_PRIMARY *)calloc(1, sizeof(*ppi));
  cpi->ppi = ppi;
  AV1_COMMON *cm = &cpi->common;
  cm->seq_params = &ppi->seq_params;
  SequenceHeader *seq = cm->seq_params;
  seq->bit_depth = AOM_BITS_8;
  seq->subsampling_x = 1;
  seq->subsampling_y = 1;
  seq->sb_size = p[P_SB128] ? BLOCK_128X128 : BLOCK_64X64;
  seq->mib_size = mi_size_wide[seq->sb_size];
  seq->mib_size_log2 = mi_size_wide_log2[seq->sb_size];

  cm->width = p[P_WIDTH];
  cm->height = p[P_HEIGHT];
  cm->current_frame.frame_type = (FRAME_TYPE)p[P_FRAME_TYPE];
  cm->current_frame.frame_number = (unsigned int)p[P_FRAME_NUMBER];
  cm->quant_params.base_qindex = p[P_BASE_QINDEX];

  cpi->sf.part_sf.partition_search_type =
      (PARTITION_SEARCH_TYPE)p[P_PARTITION_SEARCH_TYPE];
  cpi->sf.part_sf.disable_8x8_part_based_on_qidx = p[P_DISABLE_8X8_QIDX];
  REAL_TIME_SPEED_FEATURES *rt = &cpi->sf.rt_sf;
  rt->var_part_split_threshold_shift = p[P_SPLIT_SHIFT];
  rt->force_large_partition_blocks_intra = p[P_FORCE_LARGE_INTRA];
  rt->prefer_large_partition_blocks = p[P_PREFER_LARGE];
  rt->var_part_based_on_qidx = p[P_VAR_PART_QIDX];
  rt->use_rtc_tf = p[P_USE_RTC_TF];
  rt->estimate_motion_for_var_based_partition = p[P_EST_MOTION];
  rt->nonrd_prune_ref_frame_search = p[P_NONRD_PRUNE_REF];
  rt->short_circuit_low_temp_var = p[P_SHORT_CIRCUIT];
  rt->part_early_exit_zeromv = p[P_PART_EARLY_EXIT_ZEROMV];
  rt->set_zeromv_skip_based_on_source_sad = p[P_ZEROMV_SKIP_SRC_SAD];
  rt->increase_source_sad_thresh = p[P_INCREASE_SRC_SAD_THRESH];

  cpi->oxcf.mode = (MODE)p[P_MODE];
  cpi->oxcf.speed = p[P_SPEED];
  cpi->oxcf.tune_cfg.content = (aom_tune_content)p[P_CONTENT];
  cpi->oxcf.q_cfg.aq_mode = (AQ_MODE)p[P_AQ_MODE];

  cpi->noise_estimate.enabled = p[P_NOISE_ENABLED];
  cpi->noise_estimate.value = p[P_NOISE_VALUE];
  cpi->noise_estimate.thresh = p[P_NOISE_THRESH];

  ppi->rtc_ref.non_reference_frame = p[P_NON_REF_FRAME];
  cpi->rc.frame_source_sad = (uint64_t)p[P_FRAME_SOURCE_SAD];
  cpi->rc.avg_source_sad = (uint64_t)p[P_AVG_SOURCE_SAD];
  cpi->rc.high_motion_content_screen_rtc = p[P_HIGH_MOTION_SCREEN];
  cpi->rc.frames_since_key = p[P_FRAMES_SINCE_KEY];
  cpi->rc.high_source_sad = p[P_HIGH_SOURCE_SAD];
  cpi->rc.percent_blocks_with_motion = p[P_PERCENT_MOTION];

  cpi->svc.number_spatial_layers = 1;
  cpi->svc.number_temporal_layers = p[P_NUM_TEMPORAL_LAYERS];
  cpi->svc.temporal_layer_id = p[P_TEMPORAL_LAYER_ID];

  cpi->ref_frame_flags = p[P_REF_FRAME_FLAGS];
  cpi->roi.delta_qp_enabled = p[P_ROI_DELTA_QP];

  if (p[P_SEG_ENABLED]) {
    struct segmentation *seg = &cm->seg;
    seg->enabled = 1;
    av1_clearall_segfeatures(seg);
    av1_enable_segfeature(seg, 1, SEG_LVL_ALT_Q);
    av1_set_segdata(seg, 1, SEG_LVL_ALT_Q, p[P_SEG_DELTA_Q1]);
    av1_enable_segfeature(seg, 2, SEG_LVL_ALT_Q);
    av1_set_segdata(seg, 2, SEG_LVL_ALT_Q, p[P_SEG_DELTA_Q2]);
  }
  return cpi;
}

static void ctx_free(AV1_COMP *cpi) {
  free(cpi->ppi);
  free(cpi);
}

/* av1_set_variance_partition_thresholds(): out[0..4] = thresholds, out[5] = threshold_minmax. */
EXPORT void aomshim_f3_vbp_thresholds(const int *p, int64_t *out) {
  AV1_COMP *cpi = ctx_create(p);
  for (int i = 0; i < 5; ++i) cpi->vbp_info.thresholds[i] = -12345;
  cpi->vbp_info.threshold_minmax = -12345;
  av1_set_variance_partition_thresholds(cpi, p[P_QINDEX], p[P_LOWSUMDIFF]);
  for (int i = 0; i < 5; ++i) out[i] = cpi->vbp_info.thresholds[i];
  out[5] = cpi->vbp_info.threshold_minmax;
  ctx_free(cpi);
}

EXPORT int aomshim_f3_force_skip_low_temp_var(const uint8_t *variance_low, int mi_row,
                                              int mi_col, int bsize, int small_sb) {
  return small_sb ? av1_get_force_skip_low_temp_var_small_sb(variance_low, mi_row, mi_col,
                                                              (BLOCK_SIZE)bsize)
                  : av1_get_force_skip_low_temp_var(variance_low, mi_row, mi_col,
                                                    (BLOCK_SIZE)bsize);
}

/* av1_setup_sms_tree(): for every node, its block size and the indices of its 4 children
 * (-1 for none). Returns the number of nodes, or -1 if cap is too small. *root receives the
 * index of td->sms_root. */
EXPORT int aomshim_f3_sms_tree(int sb128, int key_freq_max, int *block_size, int *split,
                               int cap, int *root) {
  AV1_COMP *cpi = (AV1_COMP *)calloc(1, sizeof(*cpi));
  AV1_PRIMARY *ppi = (AV1_PRIMARY *)calloc(1, sizeof(*ppi));
  cpi->ppi = ppi;
  cpi->common.seq_params = &ppi->seq_params;
  cpi->common.seq_params->sb_size = sb128 ? BLOCK_128X128 : BLOCK_64X64;
  cpi->oxcf.kf_cfg.key_freq_max = key_freq_max;
  cpi->oxcf.pass = AOM_RC_ONE_PASS;
  ThreadData *td = &cpi->td;
  int n = -1;
  if (av1_setup_sms_tree(cpi, td) == 0) {
    if (td->sms_tree == NULL) {
      n = 0;
      *root = -1;
    } else {
      n = av1_get_pc_tree_nodes(sb128, 0);
      if (n <= cap) {
        for (int i = 0; i < n; ++i) {
          block_size[i] = td->sms_tree[i].block_size;
          for (int j = 0; j < 4; ++j) {
            const SIMPLE_MOTION_DATA_TREE *s = td->sms_tree[i].split[j];
            split[4 * i + j] = s ? (int)(s - td->sms_tree) : -1;
          }
        }
        *root = (int)(td->sms_root - td->sms_tree);
      } else {
        n = -1;
      }
    }
    av1_free_sms_tree(td);
  }
  free(ppi);
  free(cpi);
  return n;
}

/* av1_alloc_pmc() / av1_reset_pmc() / av1_alloc_pc_tree_node(). out:
 * [0] num_4x4_blk, [1] rd_mode_is_ready, [2] rd_stats.rate, [3] zero_rate, [4] skip_txfm,
 * [5] dist == INT64_MAX, [6] rdcost == INT64_MAX, [7] sse == INT64_MAX,
 * [8..10] coeff/qcoeff/dqcoeff of each plane are the shared buffers (bit mask per plane),
 * [11] color_index_map[0] != NULL, [12] color_index_map[1] != NULL,
 * [13] tx_type_map is zero after av1_reset_pmc, [14] rd_stats.rate after reset,
 * [15] pc_tree partitioning, [16] pc_tree block_size, [17] pc_tree index,
 * [18] number of planes. */
EXPORT void aomshim_f3_alloc_pmc(int bsize, int monochrome, int sb128,
                                 int allow_screen_content_tools, int *out) {
  AV1_COMP *cpi = (AV1_COMP *)calloc(1, sizeof(*cpi));
  AV1_PRIMARY *ppi = (AV1_PRIMARY *)calloc(1, sizeof(*ppi));
  cpi->ppi = ppi;
  AV1_COMMON *cm = &cpi->common;
  cm->seq_params = &ppi->seq_params;
  cm->seq_params->monochrome = (uint8_t)monochrome;
  cm->seq_params->subsampling_x = 1;
  cm->seq_params->subsampling_y = 1;
  cm->seq_params->sb_size = sb128 ? BLOCK_128X128 : BLOCK_64X64;
  cm->features.allow_screen_content_tools = (uint8_t)allow_screen_content_tools;
  struct aom_internal_error_info err;
  memset(&err, 0, sizeof(err));
  PC_TREE_SHARED_BUFFERS shared;
  memset(&shared, 0, sizeof(shared));
  av1_setup_shared_coeff_buffer(cm->seq_params, &shared, &err);

  PICK_MODE_CONTEXT *ctx = av1_alloc_pmc(cpi, (BLOCK_SIZE)bsize, &shared);
  const int num_planes = av1_num_planes(cm);
  out[0] = ctx->num_4x4_blk;
  out[1] = ctx->rd_mode_is_ready;
  out[2] = ctx->rd_stats.rate;
  out[3] = ctx->rd_stats.zero_rate;
  out[4] = ctx->rd_stats.skip_txfm;
  out[5] = ctx->rd_stats.dist == INT64_MAX;
  out[6] = ctx->rd_stats.rdcost == INT64_MAX;
  out[7] = ctx->rd_stats.sse == INT64_MAX;
  out[8] = out[9] = out[10] = 0;
  for (int i = 0; i < num_planes; ++i) {
    out[8] |= (ctx->coeff[i] == shared.coeff_buf[i]) << i;
    out[9] |= (ctx->qcoeff[i] == shared.qcoeff_buf[i]) << i;
    out[10] |= (ctx->dqcoeff[i] == shared.dqcoeff_buf[i]) << i;
  }
  out[11] = ctx->color_index_map[0] != NULL;
  out[12] = ctx->color_index_map[1] != NULL;
  memset(ctx->tx_type_map, 7, ctx->num_4x4_blk);
  ctx->rd_stats.rate = 5;
  av1_reset_pmc(ctx);
  int zero = 1;
  for (int i = 0; i < ctx->num_4x4_blk; ++i) zero &= ctx->tx_type_map[i] == 0;
  out[13] = zero;
  out[14] = ctx->rd_stats.rate;
  av1_free_pmc(ctx, num_planes);

  PC_TREE *tree = av1_alloc_pc_tree_node((BLOCK_SIZE)bsize);
  out[15] = tree->partitioning;
  out[16] = tree->block_size;
  out[17] = tree->index;
  out[18] = num_planes;
  av1_free_pc_tree_recursive(tree, num_planes, 0, 0, VAR_BASED_PARTITION);
  av1_free_shared_coeff_buffer(&shared);
  free(ppi);
  free(cpi);
}

/* ---- av1_choose_var_based_partitioning() over a whole frame ---------------------------- */

#define SB_INFO_COUNT 5 /* segment_id, source_sad_nonrd, source_sad_rd, lighting_change,
                           low_sumdiff */
#define SB_OUT_HEADER 14
#define SB_OUT_COUNT (SB_OUT_HEADER + 105)
#define VPARTVAR_INTS 4
#define VP64_VARS 2729 /* VPartVars in one VP64x64 (5 + 4 * (5 + 4 * (5 + 4 * (5 + 4 * 9)))) */

static int dump_var(const VPartVar *v, int *o) {
  o[0] = (int)v->sum_square_error;
  o[1] = v->sum_error;
  o[2] = v->log2_count;
  o[3] = v->variance;
  return VPARTVAR_INTS;
}

static int dump_pv(const VPVariance *pv, int *o) {
  int n = dump_var(&pv->none, o);
  n += dump_var(&pv->horz[0], o + n);
  n += dump_var(&pv->horz[1], o + n);
  n += dump_var(&pv->vert[0], o + n);
  n += dump_var(&pv->vert[1], o + n);
  return n;
}

static int dump_vt64(const VP64x64 *v64, int *o) {
  int n = dump_pv(&v64->part_variances, o);
  for (int a = 0; a < 4; ++a) {
    const VP32x32 *v32 = &v64->split[a];
    n += dump_pv(&v32->part_variances, o + n);
    for (int b = 0; b < 4; ++b) {
      const VP16x16 *v16 = &v32->split[b];
      n += dump_pv(&v16->part_variances, o + n);
      for (int c = 0; c < 4; ++c) {
        const VP8x8 *v8 = &v16->split[c];
        n += dump_pv(&v8->part_variances, o + n);
        for (int d = 0; d < 4; ++d) {
          const VP4x4 *v4 = &v8->split[d];
          n += dump_pv(&v4->part_variances, o + n);
          for (int e = 0; e < 4; ++e) n += dump_var(&v4->split[e], o + n);
        }
      }
    }
  }
  return n;
}

/* frames: 3 frames (source, LAST, GOLDEN) in the layout above.
 * thresh_exit_part: cpi->zeromv_skip_thresh_exit_part[BLOCK_SIZES_ALL].
 * src_sad_blk: cpi->src_sad_blk_64x64 (used if p[P_USE_SRC_SAD_BLK]).
 * sb_info: SB_INFO_COUNT ints per superblock (raster order).
 * Outputs: thresholds[6] (frame thresholds and threshold_minmax), sb_out (SB_OUT_COUNT per
 * SB), vt_out (num_64x64 * VP64_VARS * 4 ints per SB, the td->vt64x64 tree after the call)
 * and grid_out (mi_rows * mi_cols: bsize of mi_grid_base[] at each position, -1 for NULL). */
EXPORT int aomshim_f3_choose_vbp(const int *p, uint8_t *frames,
                                 const unsigned int *thresh_exit_part,
                                 const uint64_t *src_sad_blk, const int *sb_info,
                                 int64_t *thresholds, int *sb_out, int *vt_out, int *grid_out) {
  AV1_COMP *cpi = ctx_create(p);
  AV1_PRIMARY *ppi = cpi->ppi;
  AV1_COMMON *cm = &cpi->common;
  CommonModeInfoParams *mi_params = &cm->mi_params;
  const SequenceHeader *seq = cm->seq_params;
  const int width = p[P_WIDTH], height = p[P_HEIGHT];
  const Layout l = layout(width, height);

  set_sad_fns(ppi);
  for (int i = 0; i < BLOCK_SIZES_ALL; ++i)
    cpi->zeromv_skip_thresh_exit_part[i] = thresh_exit_part[i];
  cpi->src_sad_blk_64x64 = p[P_USE_SRC_SAD_BLK] ? (uint64_t *)src_sad_blk : NULL;

  /* mode info grid (av1_alloc_context_buffers / mi_alloc_setup equivalent) */
  mi_params->mi_cols = l.aligned_w >> MI_SIZE_LOG2;
  mi_params->mi_rows = l.aligned_h >> MI_SIZE_LOG2;
  mi_params->mi_stride = calc_mi_size(mi_params->mi_cols);
  mi_params->mi_alloc_bsize = (BLOCK_SIZE)p[P_MI_ALLOC_BSIZE];
  const int alloc_1d = mi_size_wide[mi_params->mi_alloc_bsize];
  mi_params->mi_alloc_stride = (mi_params->mi_stride + alloc_1d - 1) / alloc_1d;
  const int aligned_mi_rows = calc_mi_size(mi_params->mi_rows);
  mi_params->mi_alloc_size =
      mi_params->mi_alloc_stride * ((aligned_mi_rows + alloc_1d - 1) / alloc_1d);
  mi_params->mi_alloc =
      (MB_MODE_INFO *)calloc(mi_params->mi_alloc_size, sizeof(MB_MODE_INFO));
  mi_params->mi_grid_size = mi_params->mi_stride * aligned_mi_rows;
  mi_params->mi_grid_base =
      (MB_MODE_INFO **)calloc(mi_params->mi_grid_size, sizeof(MB_MODE_INFO *));

  /* reference frames: LAST -> map 0, GOLDEN -> map 1, the others -> map 0 */
  RefCntBuffer *bufs = (RefCntBuffer *)calloc(2, sizeof(RefCntBuffer));
  setup_buf(&bufs[0].buf, frames + l.frame_size, &l, width, height);
  setup_buf(&bufs[1].buf, frames + 2 * l.frame_size, &l, width, height);
  for (int i = 0; i < REF_FRAMES; ++i) cm->remapped_ref_idx[i] = 0;
  cm->remapped_ref_idx[GOLDEN_FRAME - LAST_FRAME] = 1;
  cm->ref_frame_map[0] = &bufs[0];
  cm->ref_frame_map[1] = &bufs[1];
  for (int i = 0; i < 2; ++i)
    av1_setup_scale_factors_for_frame(&cm->ref_scale_factors[i], width, height, width,
                                      height);
  YV12_BUFFER_CONFIG src;
  setup_buf(&src, frames, &l, width, height);

  av1_set_variance_partition_thresholds(cpi, p[P_QINDEX], p[P_LOWSUMDIFF]);
  for (int i = 0; i < 5; ++i) thresholds[i] = cpi->vbp_info.thresholds[i];
  thresholds[5] = cpi->vbp_info.threshold_minmax;

  ThreadData *td = &cpi->td;
  const int num_64x64 = seq->sb_size == BLOCK_64X64 ? 1 : 4;
  td->vt64x64 = (VP64x64 *)calloc(num_64x64, sizeof(VP64x64));
  td->num_64x64_blocks = num_64x64;
  MACROBLOCK *x = &td->mb;
  MACROBLOCKD *xd = &x->e_mbd;
  for (int i = 0; i < MAX_MB_PLANE; ++i) {
    xd->plane[i].subsampling_x = i ? 1 : 0;
    xd->plane[i].subsampling_y = i ? 1 : 0;
  }
  TileInfo tile;
  memset(&tile, 0, sizeof(tile));
  tile.mi_row_end = mi_params->mi_rows;
  tile.mi_col_end = mi_params->mi_cols;

  const int sb_mi = seq->mib_size;
  int sb = 0;
  for (int mi_row = 0; mi_row < mi_params->mi_rows; mi_row += sb_mi) {
    for (int mi_col = 0; mi_col < mi_params->mi_cols; mi_col += sb_mi, ++sb) {
      const int *info = sb_info + SB_INFO_COUNT * sb;
      /* av1_set_offsets() subset */
      const int grid_idx = get_mi_grid_idx(mi_params, mi_row, mi_col);
      xd->mi = mi_params->mi_grid_base + grid_idx;
      xd->mi[0] = &mi_params->mi_alloc[get_alloc_mi_idx(mi_params, mi_row, mi_col)];
      xd->mi[0]->segment_id = (uint8_t)info[0];
      xd->mi_row = mi_row;
      xd->mi_col = mi_col;
      xd->mb_to_bottom_edge = GET_MV_SUBPEL((mi_params->mi_rows - sb_mi - mi_row) * MI_SIZE);
      xd->mb_to_right_edge = GET_MV_SUBPEL((mi_params->mi_cols - sb_mi - mi_col) * MI_SIZE);
      xd->up_available = 0;
      xd->left_available = 0;
      x->plane[0].src.buf = src.y_buffer + mi_row * MI_SIZE * src.y_stride + mi_col * MI_SIZE;
      x->plane[0].src.stride = src.y_stride;
      x->plane[1].src.buf =
          src.u_buffer + (mi_row * MI_SIZE >> 1) * src.uv_stride + (mi_col * MI_SIZE >> 1);
      x->plane[1].src.stride = src.uv_stride;
      x->plane[2].src.buf =
          src.v_buffer + (mi_row * MI_SIZE >> 1) * src.uv_stride + (mi_col * MI_SIZE >> 1);
      x->plane[2].src.stride = src.uv_stride;
      x->content_state_sb.source_sad_nonrd = (SOURCE_SAD)info[1];
      x->content_state_sb.source_sad_rd = (SOURCE_SAD)info[2];
      x->content_state_sb.lighting_change = info[3];
      x->content_state_sb.low_sumdiff = info[4];
      x->delta_qindex = 0;
      x->nonrd_prune_ref_frame_search = 0;
      x->sb_me_partition = 0;
      x->force_zeromv_skip_for_sb = 0;
      memset(x->part_search_info.variance_low, 0,
             sizeof(x->part_search_info.variance_low));
      memset(x->color_sensitivity_sb, 0, sizeof(x->color_sensitivity_sb));
      memset(x->color_sensitivity_sb_g, 0, sizeof(x->color_sensitivity_sb_g));
      memset(x->color_sensitivity_sb_alt, 0, sizeof(x->color_sensitivity_sb_alt));

      const int ret = av1_choose_var_based_partitioning(cpi, &tile, td, x, mi_row, mi_col);

      int *o = sb_out + SB_OUT_COUNT * sb;
      o[0] = ret;
      o[1] = x->force_zeromv_skip_for_sb;
      o[2] = x->nonrd_prune_ref_frame_search;
      o[3] = x->sb_me_partition;
      o[4] = mi_params->mi_alloc[get_alloc_mi_idx(mi_params, mi_row, mi_col)].ref_frame[0];
      o[5] = (int)mi_params->mi_alloc[get_alloc_mi_idx(mi_params, mi_row, mi_col)].mv[0].as_int;
      o[6] = (int)x->source_variance;
      o[7] = x->color_sensitivity_sb[0];
      o[8] = x->color_sensitivity_sb[1];
      o[9] = x->color_sensitivity_sb_g[0];
      o[10] = x->color_sensitivity_sb_g[1];
      o[11] = x->color_sensitivity_sb_alt[0];
      o[12] = x->color_sensitivity_sb_alt[1];
      o[13] = mi_params->mi_alloc[get_alloc_mi_idx(mi_params, mi_row, mi_col)].bsize;
      for (int i = 0; i < 105; ++i)
        o[SB_OUT_HEADER + i] = x->part_search_info.variance_low[i];
      int *v = vt_out + sb * num_64x64 * VP64_VARS * VPARTVAR_INTS;
      for (int i = 0; i < num_64x64; ++i)
        v += dump_vt64(&td->vt64x64[i], v);
    }
  }

  for (int r = 0; r < mi_params->mi_rows; ++r) {
    for (int c = 0; c < mi_params->mi_cols; ++c) {
      const MB_MODE_INFO *mi = mi_params->mi_grid_base[r * mi_params->mi_stride + c];
      grid_out[r * mi_params->mi_cols + c] = mi ? (int)mi->bsize : -1;
    }
  }

  free(td->vt64x64);
  free(bufs);
  free(mi_params->mi_grid_base);
  free(mi_params->mi_alloc);
  ctx_free(cpi);
  return sb;
}
