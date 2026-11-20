/* Shim for porter e3 (prediction): wrappers around libaom's intra predictors,
 * convolution kernels, scale factors, inter prediction, pred_common contexts,
 * MV reference search and entropy-context helpers. See docs/TESTING.md. */
#include <stdlib.h>
#include <string.h>

#include "config/aom_dsp_rtcd.h"
#include "config/av1_rtcd.h"

#include "av1/common/av1_common_int.h"
#include "av1/common/blockd.h"
#include "av1/common/convolve.h"
#include "av1/common/filter.h"
#include "av1/common/mvref_common.h"
#include "av1/common/pred_common.h"
#include "av1/common/reconinter.h"
#include "av1/common/reconintra.h"
#include "av1/common/scale.h"
#include "av1/encoder/reconinter_enc.h"

#define EXPORT __declspec(dllexport)

/* ---------------------------------------------------------------- intra -- */

typedef void (*ipred_fn)(uint8_t *, ptrdiff_t, const uint8_t *, const uint8_t *);

/* TX_SIZE order. */
#define ALL_SIZES(f)                                                          \
  {                                                                           \
    aom_##f##_predictor_4x4_c, aom_##f##_predictor_8x8_c,                     \
        aom_##f##_predictor_16x16_c, aom_##f##_predictor_32x32_c,             \
        aom_##f##_predictor_64x64_c, aom_##f##_predictor_4x8_c,               \
        aom_##f##_predictor_8x4_c, aom_##f##_predictor_8x16_c,                \
        aom_##f##_predictor_16x8_c, aom_##f##_predictor_16x32_c,              \
        aom_##f##_predictor_32x16_c, aom_##f##_predictor_32x64_c,             \
        aom_##f##_predictor_64x32_c, aom_##f##_predictor_4x16_c,              \
        aom_##f##_predictor_16x4_c, aom_##f##_predictor_8x32_c,               \
        aom_##f##_predictor_32x8_c, aom_##f##_predictor_16x64_c,              \
        aom_##f##_predictor_64x16_c                                           \
  }

/* Families: 0 dc, 1 dc_top, 2 dc_left, 3 dc_128, 4 v, 5 h, 6 smooth. */
static const ipred_fn ipred[7][TX_SIZES_ALL] = {
  ALL_SIZES(dc),    ALL_SIZES(dc_top), ALL_SIZES(dc_left), ALL_SIZES(dc_128),
  ALL_SIZES(v),     ALL_SIZES(h),      ALL_SIZES(smooth),
};

EXPORT void aomshim_e3_intra_kernel(int family, int tx_size, uint8_t *dst,
                                    int stride, const uint8_t *above,
                                    const uint8_t *left) {
  ipred[family][tx_size](dst, stride, above, left);
}

/* av1_predict_intra_block on buf (prediction in place, like the facade). */
EXPORT void aomshim_e3_predict_intra_block(
    int plane, int ss_x, int ss_y, int up_available, int left_available,
    int chroma_up_available, int chroma_left_available, int mb_to_right_edge,
    int mb_to_bottom_edge, int enable_intra_edge_filter, int wpx, int hpx,
    int tx_size, int mode, uint8_t *buf, int stride, int col_off,
    int row_off) {
  av1_init_intra_predictors();
  MACROBLOCKD *xd = (MACROBLOCKD *)calloc(1, sizeof(*xd));
  MB_MODE_INFO mbmi;
  memset(&mbmi, 0, sizeof(mbmi));
  mbmi.bsize = BLOCK_64X64;
  MB_MODE_INFO *mip = &mbmi;
  xd->mi = &mip;
  xd->plane[plane].subsampling_x = ss_x;
  xd->plane[plane].subsampling_y = ss_y;
  xd->up_available = up_available;
  xd->left_available = left_available;
  xd->chroma_up_available = chroma_up_available;
  xd->chroma_left_available = chroma_left_available;
  xd->mb_to_right_edge = mb_to_right_edge;
  xd->mb_to_bottom_edge = mb_to_bottom_edge;
  av1_predict_intra_block(xd, BLOCK_64X64, enable_intra_edge_filter, wpx, hpx,
                          (TX_SIZE)tx_size, (PREDICTION_MODE)mode, 0, 0,
                          FILTER_INTRA_MODES, buf, stride, buf, stride, col_off,
                          row_off, plane);
  free(xd);
}

/* ------------------------------------------------------------- convolve -- */

/* kind: 0 = av1_convolve_2d_sr_c, 1 = av1_convolve_x_sr_c,
 *       2 = av1_convolve_y_sr_c, 3 = av1_convolve_2d_facade,
 *       4 = aom_convolve_copy_c. */
EXPORT void aomshim_e3_convolve(int kind, const uint8_t *src, int src_stride,
                                uint8_t *dst, int dst_stride, int w, int h,
                                int filter_x, int filter_y, int subpel_x_qn,
                                int subpel_y_qn) {
  const InterpFilterParams *fx =
      av1_get_interp_filter_params_with_block_size((InterpFilter)filter_x, w);
  const InterpFilterParams *fy =
      av1_get_interp_filter_params_with_block_size((InterpFilter)filter_y, h);
  ConvolveParams cp = get_conv_params_no_round(0, 0, NULL, 0, 0, 8);
  switch (kind) {
    case 0:
      av1_convolve_2d_sr_c(src, src_stride, dst, dst_stride, w, h, fx, fy,
                           subpel_x_qn, subpel_y_qn, &cp);
      break;
    case 1:
      av1_convolve_x_sr_c(src, src_stride, dst, dst_stride, w, h, fx,
                          subpel_x_qn, &cp);
      break;
    case 2:
      av1_convolve_y_sr_c(src, src_stride, dst, dst_stride, w, h, fy,
                          subpel_y_qn);
      break;
    case 3: {
      const InterpFilterParams *filters[2] = { fx, fy };
      av1_convolve_2d_facade(src, src_stride, dst, dst_stride, w, h, filters,
                             subpel_x_qn, SUBPEL_SHIFTS, subpel_y_qn,
                             SUBPEL_SHIFTS, 0, &cp);
      break;
    }
    default:
      aom_convolve_copy_c(src, src_stride, dst, dst_stride, w, h);
      break;
  }
}

/* ---------------------------------------------------------------- scale -- */

EXPORT void aomshim_e3_setup_scale_factors(int other_w, int other_h, int this_w,
                                           int this_h, int *out4) {
  struct scale_factors sf;
  memset(&sf, 0, sizeof(sf));
  av1_setup_scale_factors_for_frame(&sf, other_w, other_h, this_w, this_h);
  out4[0] = sf.x_scale_fp;
  out4[1] = sf.y_scale_fp;
  out4[2] = sf.x_step_q4;
  out4[3] = sf.y_step_q4;
}

/* ------------------------------------------------------------ mode info -- */

/* Cell layout (8 ints): bsize, mode, ref_frame[0], ref_frame[1], mv[0].as_int,
 * mv[1].as_int, use_intrabc, interp_filters.as_int. */
#define CELL_INTS 8

static void fill_mbmi(MB_MODE_INFO *m, const int *c) {
  memset(m, 0, sizeof(*m));
  m->bsize = (BLOCK_SIZE)c[0];
  m->mode = (PREDICTION_MODE)c[1];
  m->ref_frame[0] = (MV_REFERENCE_FRAME)c[2];
  m->ref_frame[1] = (MV_REFERENCE_FRAME)c[3];
  m->mv[0].as_int = (uint32_t)c[4];
  m->mv[1].as_int = (uint32_t)c[5];
  m->use_intrabc = c[6] & 1;
  m->interp_filters.as_int = (uint32_t)c[7];
  m->motion_mode = SIMPLE_TRANSLATION;
  m->interinter_comp.type = COMPOUND_AVERAGE;
}

/* ---------------------------------------------------------- pred_common -- */

/* which: 1, 3, 4, 5 = av1_get_pred_context_single_ref_p{1,3,4,5}. */
EXPORT int aomshim_e3_single_ref_ctx(const uint8_t *counts, int which) {
  MACROBLOCKD *xd = (MACROBLOCKD *)calloc(1, sizeof(*xd));
  memcpy(xd->neighbors_ref_counts, counts, REF_FRAMES);
  int r = -1;
  switch (which) {
    case 1: r = av1_get_pred_context_single_ref_p1(xd); break;
    case 3: r = av1_get_pred_context_single_ref_p3(xd); break;
    case 4: r = av1_get_pred_context_single_ref_p4(xd); break;
    case 5: r = av1_get_pred_context_single_ref_p5(xd); break;
  }
  free(xd);
  return r;
}

/* 2x2 mi grid (stride 2): cell 1 = above, cell 2 = left, cell 3 = current.
 * which: 0 = av1_get_intra_inter_context,
 *        1 = av1_get_comp_reference_type_context,
 *        2/3 = av1_get_pred_context_switchable_interp(dir 0/1). */
EXPORT int aomshim_e3_neighbour_ctx(const int *cells, int up_available,
                                    int left_available, int which) {
  MB_MODE_INFO m[4];
  MB_MODE_INFO *grid[4];
  for (int i = 0; i < 4; ++i) {
    fill_mbmi(&m[i], cells + i * CELL_INTS);
    grid[i] = &m[i];
  }
  MACROBLOCKD *xd = (MACROBLOCKD *)calloc(1, sizeof(*xd));
  xd->mi = grid + 3;
  xd->mi_stride = 2;
  xd->up_available = up_available;
  xd->left_available = left_available;
  xd->above_mbmi = up_available ? grid[1] : NULL;
  xd->left_mbmi = left_available ? grid[2] : NULL;
  int r = -1;
  switch (which) {
    case 0: r = av1_get_intra_inter_context(xd); break;
    case 1: r = av1_get_comp_reference_type_context(xd); break;
    case 2: r = av1_get_pred_context_switchable_interp(xd, 0); break;
    case 3: r = av1_get_pred_context_switchable_interp(xd, 1); break;
  }
  free(xd);
  return r;
}

/* ---------------------------------------------------------------- mvref -- */

EXPORT void aomshim_e3_find_mv_refs(
    int mi_rows, int mi_cols, int grid_rows, int mi_stride, const int *cells,
    int mi_row, int mi_col, int partition, int is_last_vertical_rect,
    int is_first_horizontal_rect, int sb_size, int allow_hp,
    int force_integer_mv, const int *sign_bias, int ref_frame,
    int use_mv_ref_list, uint8_t *ref_mv_count /* [MODE_CTX_REF_FRAMES] */,
    int *stack /* [MODE_CTX_REF_FRAMES][MAX_REF_MV_STACK_SIZE][2] */,
    uint16_t *weight /* [MODE_CTX_REF_FRAMES][MAX_REF_MV_STACK_SIZE] */,
    int *mv_ref_list /* [MODE_CTX_REF_FRAMES][MAX_MV_REF_CANDIDATES] */,
    int *global_mvs /* [REF_FRAMES] */,
    int16_t *mode_context /* [MODE_CTX_REF_FRAMES] */) {
  const int n = grid_rows * mi_stride;
  MB_MODE_INFO *m = (MB_MODE_INFO *)calloc(n, sizeof(*m));
  MB_MODE_INFO **grid = (MB_MODE_INFO **)calloc(n, sizeof(*grid));
  for (int i = 0; i < n; ++i) {
    fill_mbmi(&m[i], cells + i * CELL_INTS);
    grid[i] = &m[i];
  }

  SequenceHeader *seq = (SequenceHeader *)calloc(1, sizeof(*seq));
  seq->sb_size = (BLOCK_SIZE)sb_size;
  AV1_COMMON *cm = (AV1_COMMON *)calloc(1, sizeof(*cm));
  cm->seq_params = seq;
  cm->mi_params.mi_rows = mi_rows;
  cm->mi_params.mi_cols = mi_cols;
  cm->features.allow_high_precision_mv = allow_hp;
  cm->features.cur_frame_force_integer_mv = force_integer_mv;
  cm->features.allow_ref_frame_mvs = 0;
  for (int i = 0; i < REF_FRAMES; ++i) {
    cm->global_motion[i] = default_warp_params;
    cm->ref_frame_sign_bias[i] = sign_bias[i];
  }

  MACROBLOCKD *xd = (MACROBLOCKD *)calloc(1, sizeof(*xd));
  xd->mi = grid + mi_row * mi_stride + mi_col;
  xd->mi_stride = mi_stride;
  xd->mi_row = mi_row;
  xd->mi_col = mi_col;
  MB_MODE_INFO *mbmi = xd->mi[0];
  mbmi->partition = (PARTITION_TYPE)partition;
  const BLOCK_SIZE bsize = mbmi->bsize;
  const int bw = mi_size_wide[bsize], bh = mi_size_high[bsize];
  xd->width = bw;
  xd->height = bh;
  xd->tile.mi_row_start = 0;
  xd->tile.mi_row_end = mi_rows;
  xd->tile.mi_col_start = 0;
  xd->tile.mi_col_end = mi_cols;
  xd->up_available = mi_row > 0;
  xd->left_available = mi_col > 0;
  xd->mb_to_top_edge = -GET_MV_SUBPEL(mi_row * MI_SIZE);
  xd->mb_to_bottom_edge = GET_MV_SUBPEL((mi_rows - bh - mi_row) * MI_SIZE);
  xd->mb_to_left_edge = -GET_MV_SUBPEL(mi_col * MI_SIZE);
  xd->mb_to_right_edge = GET_MV_SUBPEL((mi_cols - bw - mi_col) * MI_SIZE);
  xd->is_last_vertical_rect = is_last_vertical_rect;
  xd->is_first_horizontal_rect = is_first_horizontal_rect;

  CANDIDATE_MV st[MODE_CTX_REF_FRAMES][MAX_REF_MV_STACK_SIZE];
  uint16_t wt[MODE_CTX_REF_FRAMES][MAX_REF_MV_STACK_SIZE];
  int_mv list[MODE_CTX_REF_FRAMES][MAX_MV_REF_CANDIDATES];
  int_mv gm[REF_FRAMES];
  for (int r = 0; r < MODE_CTX_REF_FRAMES; ++r) {
    for (int k = 0; k < MAX_REF_MV_STACK_SIZE; ++k) {
      st[r][k].this_mv.as_int = (uint32_t)stack[(r * MAX_REF_MV_STACK_SIZE + k) * 2];
      st[r][k].comp_mv.as_int =
          (uint32_t)stack[(r * MAX_REF_MV_STACK_SIZE + k) * 2 + 1];
      wt[r][k] = weight[r * MAX_REF_MV_STACK_SIZE + k];
    }
    for (int k = 0; k < MAX_MV_REF_CANDIDATES; ++k)
      list[r][k].as_int = (uint32_t)mv_ref_list[r * MAX_MV_REF_CANDIDATES + k];
  }
  for (int r = 0; r < REF_FRAMES; ++r) gm[r].as_int = (uint32_t)global_mvs[r];

  av1_find_mv_refs(cm, xd, mbmi, (MV_REFERENCE_FRAME)ref_frame, ref_mv_count,
                   st, wt, use_mv_ref_list ? list : NULL, gm, mode_context);

  for (int r = 0; r < MODE_CTX_REF_FRAMES; ++r) {
    for (int k = 0; k < MAX_REF_MV_STACK_SIZE; ++k) {
      stack[(r * MAX_REF_MV_STACK_SIZE + k) * 2] = (int)st[r][k].this_mv.as_int;
      stack[(r * MAX_REF_MV_STACK_SIZE + k) * 2 + 1] =
          (int)st[r][k].comp_mv.as_int;
      weight[r * MAX_REF_MV_STACK_SIZE + k] = wt[r][k];
    }
    for (int k = 0; k < MAX_MV_REF_CANDIDATES; ++k)
      mv_ref_list[r * MAX_MV_REF_CANDIDATES + k] = (int)list[r][k].as_int;
  }
  for (int r = 0; r < REF_FRAMES; ++r) global_mvs[r] = (int)gm[r].as_int;

  free(xd);
  free(cm);
  free(seq);
  free(grid);
  free(m);
}

/* -------------------------------------------------------- inter predict -- */

/* Frame geometry (9 ints): y offset, u offset, v offset, y crop width,
 * y crop height, y stride, uv crop width, uv crop height, uv stride. */
static void make_yv12(YV12_BUFFER_CONFIG *b, uint8_t *base, const int *g) {
  memset(b, 0, sizeof(*b));
  b->y_buffer = base + g[0];
  b->u_buffer = base + g[1];
  b->v_buffer = base + g[2];
  b->y_crop_width = g[3];
  b->y_crop_height = g[4];
  b->y_stride = g[5];
  b->uv_crop_width = g[6];
  b->uv_crop_height = g[7];
  b->uv_stride = g[8];
}

/* 2x2 mi grid (stride 2) around the current block (cell 3), so the sub-8x8
 * chroma path can read the above / left / above-left blocks.
 * y_nonrd: 0 = av1_enc_build_inter_predictor(plane_from..plane_to),
 *          1 = av1_enc_build_inter_predictor_y_nonrd. */
EXPORT void aomshim_e3_enc_build_inter(uint8_t *refbase, const int *rg,
                                       uint8_t *dstbase, const int *dg,
                                       int ss_x, int ss_y, int mi_row,
                                       int mi_col, const int *cells,
                                       int plane_from, int plane_to,
                                       int is_chroma_ref, int y_nonrd) {
  YV12_BUFFER_CONFIG refb, dstb;
  make_yv12(&refb, refbase, rg);
  make_yv12(&dstb, dstbase, dg);
  struct scale_factors sf;
  av1_setup_scale_factors_for_frame(&sf, rg[3], rg[4], rg[3], rg[4]);

  MB_MODE_INFO m[4];
  MB_MODE_INFO *grid[4];
  for (int i = 0; i < 4; ++i) {
    fill_mbmi(&m[i], cells + i * CELL_INTS);
    grid[i] = &m[i];
  }
  const BLOCK_SIZE bsize = m[3].bsize;

  WarpedMotionParams gm[REF_FRAMES];
  for (int i = 0; i < REF_FRAMES; ++i) gm[i] = default_warp_params;

  MACROBLOCKD *xd = (MACROBLOCKD *)calloc(1, sizeof(*xd));
  CONV_BUF_TYPE *tmp_conv_dst =
      (CONV_BUF_TYPE *)calloc(MAX_SB_SIZE * MAX_SB_SIZE, sizeof(CONV_BUF_TYPE));
  xd->mi = grid + 3;
  xd->mi_stride = 2;
  xd->mi_row = mi_row;
  xd->mi_col = mi_col;
  xd->is_chroma_ref = is_chroma_ref;
  xd->bd = 8;
  xd->global_motion = gm;
  xd->block_ref_scale_factors[0] = &sf;
  xd->tmp_conv_dst = tmp_conv_dst;
  for (int p = 0; p < MAX_MB_PLANE; ++p) {
    struct macroblockd_plane *pd = &xd->plane[p];
    pd->subsampling_x = p ? ss_x : 0;
    pd->subsampling_y = p ? ss_y : 0;
    int w = (mi_size_wide[bsize] * MI_SIZE) >> pd->subsampling_x;
    int h = (mi_size_high[bsize] * MI_SIZE) >> pd->subsampling_y;
    pd->width = AOMMAX(w, 4);
    pd->height = AOMMAX(h, 4);
  }
  av1_setup_dst_planes(xd->plane, bsize, &dstb, mi_row, mi_col, 0,
                       MAX_MB_PLANE);
  av1_setup_pre_planes(xd, 0, &refb, mi_row, mi_col, &sf, MAX_MB_PLANE);

  AV1_COMMON *cm = (AV1_COMMON *)calloc(1, sizeof(*cm));
  RefCntBuffer *rcb = (RefCntBuffer *)calloc(1, sizeof(*rcb));
  rcb->buf = refb;
  for (int i = 0; i < REF_FRAMES; ++i) {
    cm->remapped_ref_idx[i] = i;
    cm->ref_frame_map[i] = rcb;
    cm->ref_scale_factors[i] = sf;
  }

  if (y_nonrd) {
    InterPredParams ipp;
    av1_init_inter_params(&ipp, xd->plane[0].width, xd->plane[0].height,
                          mi_row * MI_SIZE, mi_col * MI_SIZE, 0, 0, 8, 0, 0,
                          &sf, &xd->plane[0].pre[0], m[3].interp_filters);
    ipp.conv_params = get_conv_params_no_round(0, 0, NULL, 0, 0, 8);
    SubpelParams sp;
    init_subpel_params(&m[3].mv[0].as_mv, &ipp, &sp,
                       xd->plane[0].pre[0].width, xd->plane[0].pre[0].height);
    av1_enc_build_inter_predictor_y_nonrd(xd, &ipp, &sp);
  } else {
    av1_enc_build_inter_predictor(cm, xd, mi_row, mi_col, NULL, bsize,
                                  plane_from, plane_to);
  }

  free(rcb);
  free(cm);
  free(tmp_conv_dst);
  free(xd);
}

/* --------------------------------------------------------------- blockd -- */

/* above/left: int8 contexts (ENTROPY_CONTEXT is char), modified in place. */
EXPORT void aomshim_e3_set_entropy_contexts(
    int plane, int ss_x, int ss_y, int mb_to_right_edge, int mb_to_bottom_edge,
    int plane_bsize, int tx_size, int has_eob, int aoff, int loff,
    signed char *above, signed char *left) {
  MACROBLOCKD *xd = (MACROBLOCKD *)calloc(1, sizeof(*xd));
  struct macroblockd_plane *pd = &xd->plane[plane];
  pd->subsampling_x = ss_x;
  pd->subsampling_y = ss_y;
  pd->above_entropy_context = (ENTROPY_CONTEXT *)above;
  pd->left_entropy_context = (ENTROPY_CONTEXT *)left;
  xd->mb_to_right_edge = mb_to_right_edge;
  xd->mb_to_bottom_edge = mb_to_bottom_edge;
  av1_set_entropy_contexts(xd, pd, plane, (BLOCK_SIZE)plane_bsize,
                           (TX_SIZE)tx_size, has_eob, aoff, loff);
  free(xd);
}
