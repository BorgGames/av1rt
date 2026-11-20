/* Shim for work package e5 (dsp + buffers): exports wrappers around libaom's C kernels and
 * buffer helpers so tests/Av1rt.Tests/E5 can compare the C# port against them. */
#include <stdlib.h>
#include <string.h>

#include "config/aom_config.h"
#include "config/aom_dsp_rtcd.h"
#include "config/aom_scale_rtcd.h"

#include "aom_scale/yv12config.h"
#include "aom_util/aom_thread.h"
#include "av1/common/alloccommon.h"
#include "av1/common/av1_common_int.h"
#include "av1/common/thread_common.h"
#include "av1/common/tile_common.h"
#include "av1/common/timing.h"
#include "av1/encoder/extend.h"
#include "av1/encoder/lookahead.h"

#define EXPORT __declspec(dllexport)

/* ---- block-size dispatch ------------------------------------------------------------------- */

#define ALL_SIZES(X)                                                                   \
  X(128, 128) X(128, 64) X(64, 128) X(64, 64) X(64, 32) X(32, 64) X(32, 32) X(32, 16) \
  X(16, 32) X(16, 16) X(16, 8) X(8, 16) X(8, 8) X(8, 4) X(4, 8) X(4, 4)
#define AVG_SIZES(X)                                                                   \
  X(128, 128) X(128, 64) X(64, 128) X(64, 64) X(64, 32) X(32, 64) X(32, 32) X(32, 16) \
  X(16, 32) X(16, 16) X(16, 8) X(8, 16) X(8, 8)
#define SKIP_SIZES(X)                                                                  \
  X(128, 128) X(128, 64) X(64, 128) X(64, 64) X(64, 32) X(32, 64) X(32, 32) X(32, 16) \
  X(16, 32) X(16, 16) X(8, 16)
#define MSE_SIZES(X) X(16, 16) X(16, 8) X(8, 16) X(8, 8)

EXPORT unsigned int aomshim_e5_sad(int w, int h, const uint8_t *src, int src_stride,
                                   const uint8_t *ref, int ref_stride) {
#define C(m, n) \
  if (w == m && h == n) return aom_sad##m##x##n##_c(src, src_stride, ref, ref_stride);
  ALL_SIZES(C)
#undef C
  return 0xFFFFFFFFu;
}

EXPORT unsigned int aomshim_e5_sad_avg(int w, int h, const uint8_t *src, int src_stride,
                                       const uint8_t *ref, int ref_stride,
                                       const uint8_t *second_pred) {
#define C(m, n)                                                                  \
  if (w == m && h == n)                                                         \
    return aom_sad##m##x##n##_avg_c(src, src_stride, ref, ref_stride, second_pred);
  AVG_SIZES(C)
#undef C
  return 0xFFFFFFFFu;
}

EXPORT unsigned int aomshim_e5_sad_skip(int w, int h, const uint8_t *src, int src_stride,
                                        const uint8_t *ref, int ref_stride) {
#define C(m, n) \
  if (w == m && h == n) return aom_sad_skip_##m##x##n##_c(src, src_stride, ref, ref_stride);
  SKIP_SIZES(C)
#undef C
  return 0xFFFFFFFFu;
}

/* kind: 0 = x4d, 1 = x3d, 2 = skip x4d. The 4 reference pointers are ref + offsets[i]. */
EXPORT int aomshim_e5_sad_multi(int kind, int w, int h, const uint8_t *src, int src_stride,
                                const uint8_t *ref, const int *offsets, int ref_stride,
                                uint32_t *sad_array) {
  const uint8_t *const refs[4] = { ref + offsets[0], ref + offsets[1], ref + offsets[2],
                                   ref + offsets[3] };
  if (kind == 0) {
#define C(m, n)                                                              \
  if (w == m && h == n) {                                                    \
    aom_sad##m##x##n##x4d_c(src, src_stride, refs, ref_stride, sad_array); \
    return 1;                                                                \
  }
    ALL_SIZES(C)
#undef C
  } else if (kind == 1) {
#define C(m, n)                                                              \
  if (w == m && h == n) {                                                    \
    aom_sad##m##x##n##x3d_c(src, src_stride, refs, ref_stride, sad_array); \
    return 1;                                                                \
  }
    ALL_SIZES(C)
#undef C
  } else {
#define C(m, n)                                                                    \
  if (w == m && h == n) {                                                          \
    aom_sad_skip_##m##x##n##x4d_c(src, src_stride, refs, ref_stride, sad_array); \
    return 1;                                                                      \
  }
    SKIP_SIZES(C)
#undef C
  }
  return 0;
}

EXPORT uint32_t aomshim_e5_variance(int w, int h, const uint8_t *a, int a_stride,
                                    const uint8_t *b, int b_stride, uint32_t *sse) {
#define C(m, n) \
  if (w == m && h == n) return aom_variance##m##x##n##_c(a, a_stride, b, b_stride, sse);
  ALL_SIZES(C)
#undef C
  return 0xFFFFFFFFu;
}

EXPORT uint32_t aomshim_e5_sub_pixel_variance(int w, int h, const uint8_t *a, int a_stride,
                                              int xoffset, int yoffset, const uint8_t *b,
                                              int b_stride, uint32_t *sse) {
#define C(m, n)                                                                       \
  if (w == m && h == n)                                                              \
    return aom_sub_pixel_variance##m##x##n##_c(a, a_stride, xoffset, yoffset, b, b_stride, \
                                              sse);
  ALL_SIZES(C)
#undef C
  return 0xFFFFFFFFu;
}

EXPORT uint32_t aomshim_e5_sub_pixel_avg_variance(int w, int h, const uint8_t *a,
                                                  int a_stride, int xoffset, int yoffset,
                                                  const uint8_t *b, int b_stride,
                                                  uint32_t *sse, const uint8_t *second_pred) {
#define C(m, n)                                                                           \
  if (w == m && h == n)                                                                  \
    return aom_sub_pixel_avg_variance##m##x##n##_c(a, a_stride, xoffset, yoffset, b,      \
                                                  b_stride, sse, second_pred);
  ALL_SIZES(C)
#undef C
  return 0xFFFFFFFFu;
}

EXPORT uint32_t aomshim_e5_mse(int w, int h, const uint8_t *a, int a_stride, const uint8_t *b,
                               int b_stride, uint32_t *sse) {
#define C(m, n) \
  if (w == m && h == n) return aom_mse##m##x##n##_c(a, a_stride, b, b_stride, sse);
  MSE_SIZES(C)
#undef C
  return 0xFFFFFFFFu;
}

/* ---- other kernels ---------------------------------------------------------------------- */

EXPORT void aomshim_e5_comp_avg_pred(uint8_t *comp_pred, const uint8_t *pred, int width,
                                     int height, const uint8_t *ref, int ref_stride) {
  aom_comp_avg_pred_c(comp_pred, pred, width, height, ref, ref_stride);
}

EXPORT void aomshim_e5_get_var_sse_sum_8x8_quad(const uint8_t *a, int a_stride,
                                                const uint8_t *b, int b_stride,
                                                uint32_t *sse8x8, int *sum8x8,
                                                unsigned int *tot_sse, int *tot_sum,
                                                uint32_t *var8x8) {
  aom_get_var_sse_sum_8x8_quad_c(a, a_stride, b, b_stride, sse8x8, sum8x8, tot_sse, tot_sum,
                                 var8x8);
}

EXPORT void aomshim_e5_get_var_sse_sum_16x16_dual(const uint8_t *a, int a_stride,
                                                  const uint8_t *b, int b_stride,
                                                  uint32_t *sse16x16, unsigned int *tot_sse,
                                                  int *tot_sum, uint32_t *var16x16) {
  aom_get_var_sse_sum_16x16_dual_c(a, a_stride, b, b_stride, sse16x16, tot_sse, tot_sum,
                                   var16x16);
}

EXPORT unsigned int aomshim_e5_avg_4x4(const uint8_t *s, int p) { return aom_avg_4x4_c(s, p); }
EXPORT unsigned int aomshim_e5_avg_8x8(const uint8_t *s, int p) { return aom_avg_8x8_c(s, p); }
EXPORT void aomshim_e5_avg_8x8_quad(const uint8_t *s, int p, int x16_idx, int y16_idx,
                                    int *avg) {
  aom_avg_8x8_quad_c(s, p, x16_idx, y16_idx, avg);
}

/* kind: 0 = 8x8, 1 = 8x8 dual, 2 = 16x16 */
EXPORT void aomshim_e5_hadamard_lp(int kind, const int16_t *src_diff, int src_stride,
                                   int16_t *coeff) {
  if (kind == 0)
    aom_hadamard_lp_8x8_c(src_diff, src_stride, coeff);
  else if (kind == 1)
    aom_hadamard_lp_8x8_dual_c(src_diff, src_stride, coeff);
  else
    aom_hadamard_lp_16x16_c(src_diff, src_stride, coeff);
}

EXPORT int aomshim_e5_satd_lp(const int16_t *coeff, int length) {
  return aom_satd_lp_c(coeff, length);
}

EXPORT void aomshim_e5_int_pro_row(int16_t *hbuf, const uint8_t *ref, int ref_stride, int width,
                                   int height, int norm_factor) {
  aom_int_pro_row_c(hbuf, ref, ref_stride, width, height, norm_factor);
}

EXPORT void aomshim_e5_int_pro_col(int16_t *vbuf, const uint8_t *ref, int ref_stride, int width,
                                   int height, int norm_factor) {
  aom_int_pro_col_c(vbuf, ref, ref_stride, width, height, norm_factor);
}

EXPORT int aomshim_e5_vector_var(const int16_t *ref, const int16_t *src, int bwl) {
  return aom_vector_var_c(ref, src, bwl);
}

EXPORT int64_t aomshim_e5_sse(const uint8_t *a, int a_stride, const uint8_t *b, int b_stride,
                              int width, int height) {
  return aom_sse_c(a, a_stride, b, b_stride, width, height);
}

EXPORT void aomshim_e5_subtract_block(int rows, int cols, int16_t *diff, int diff_stride,
                                      const uint8_t *src, int src_stride, const uint8_t *pred,
                                      int pred_stride) {
  aom_subtract_block_c(rows, cols, diff, diff_stride, src, src_stride, pred, pred_stride);
}

/* ---- frame buffers ---------------------------------------------------------------------- */

/* layout[0..5] = frame_size, y/u/v buffer offsets into buffer_alloc, y_stride, uv_stride. */
static void get_layout(const YV12_BUFFER_CONFIG *ybf, int *layout) {
  layout[0] = (int)ybf->frame_size;
  layout[1] = (int)(ybf->y_buffer - ybf->buffer_alloc);
  layout[2] = (int)(ybf->u_buffer - ybf->buffer_alloc);
  layout[3] = (int)(ybf->v_buffer - ybf->buffer_alloc);
  layout[4] = ybf->y_stride;
  layout[5] = ybf->uv_stride;
}

/* Allocates a frame like the encoder does, loads buf (frame_size bytes) into it, runs op and
 * copies the whole allocation back. op 0: aom_extend_frame_borders_c(ybf, num_planes = a0);
 * op 1: aom_extend_frame_borders_plane_row_c(ybf, plane = a0, v_start = a1, v_end = a2).
 * Returns frame_size, or -1. */
EXPORT int aomshim_e5_extend(int width, int height, int ss_x, int ss_y, int border, int op,
                             int a0, int a1, int a2, uint8_t *buf, int buf_size, int *layout) {
  YV12_BUFFER_CONFIG ybf;
  memset(&ybf, 0, sizeof(ybf));
  if (aom_alloc_frame_buffer(&ybf, width, height, ss_x, ss_y, 0, border, 0, false, 0)) return -1;
  get_layout(&ybf, layout);
  if ((int)ybf.frame_size > buf_size) {
    aom_free_frame_buffer(&ybf);
    return -1;
  }
  memcpy(ybf.buffer_alloc, buf, ybf.frame_size);
  if (op == 0)
    aom_extend_frame_borders_c(&ybf, a0);
  else
    aom_extend_frame_borders_plane_row_c(&ybf, a0, a1, a2);
  memcpy(buf, ybf.buffer_alloc, ybf.frame_size);
  const int frame_size = (int)ybf.frame_size;
  aom_free_frame_buffer(&ybf);
  return frame_size;
}

/* Two frames: op 0/1/2: aom_yv12_copy_{y,u,v}_c(src, dst, use_crop);
 * op 3: av1_copy_and_extend_frame(src, dst). */
EXPORT int aomshim_e5_copy(int op, int sw, int sh, int sborder, int dw, int dh, int dborder,
                           int ss_x, int ss_y, int use_crop, uint8_t *sbuf, int sbuf_size,
                           uint8_t *dbuf, int dbuf_size) {
  YV12_BUFFER_CONFIG src, dst;
  memset(&src, 0, sizeof(src));
  memset(&dst, 0, sizeof(dst));
  if (aom_alloc_frame_buffer(&src, sw, sh, ss_x, ss_y, 0, sborder, 0, false, 0)) return -1;
  if (aom_alloc_frame_buffer(&dst, dw, dh, ss_x, ss_y, 0, dborder, 0, false, 0)) return -1;
  if ((int)src.frame_size > sbuf_size || (int)dst.frame_size > dbuf_size) return -1;
  memcpy(src.buffer_alloc, sbuf, src.frame_size);
  memcpy(dst.buffer_alloc, dbuf, dst.frame_size);
  switch (op) {
    case 0: aom_yv12_copy_y_c(&src, &dst, use_crop); break;
    case 1: aom_yv12_copy_u_c(&src, &dst, use_crop); break;
    case 2: aom_yv12_copy_v_c(&src, &dst, use_crop); break;
    default: av1_copy_and_extend_frame(&src, &dst); break;
  }
  memcpy(dbuf, dst.buffer_alloc, dst.frame_size);
  const int frame_size = (int)dst.frame_size;
  aom_free_frame_buffer(&src);
  aom_free_frame_buffer(&dst);
  return frame_size;
}

/* ---- tiles ---------------------------------------------------------------------------- */

/* Runs av1_get_tile_limits, then (uniform) clamps the requested log2 counts into the limits as
 * the encoder does, or (non-uniform) takes explicit start positions, then
 * av1_calculate_tile_cols / av1_calculate_tile_rows, and av1_tile_init on every tile.
 * params[0..15]: cols, rows, max_width_sb, max_height_sb, min_inner_width, log2_cols, log2_rows,
 * width, height, min_log2_cols, min_log2_rows, max_log2_cols, max_log2_rows, min_log2,
 * uniform_spacing, min_tile_width_satisfied.
 * starts: col_start_sb[0..cols], row_start_sb[0..rows] (in: explicit when not uniform; out).
 * tiles: per tile mi_row_start, mi_row_end, mi_col_start, mi_col_end, tile_row, tile_col,
 * sb_rows, sb_cols. */
EXPORT int aomshim_e5_tiles(int mi_rows, int mi_cols, int mib_size_log2, int uniform,
                            int log2_cols, int log2_rows, int ncols, int nrows,
                            int *col_starts, int *row_starts, int *params, int *tiles,
                            int tiles_cap) {
  AV1_COMMON *cm = (AV1_COMMON *)calloc(1, sizeof(*cm));
  SequenceHeader *seq = (SequenceHeader *)calloc(1, sizeof(*seq));
  if (!cm || !seq) return -1;
  cm->seq_params = seq;
  seq->mib_size_log2 = mib_size_log2;
  seq->mib_size = 1 << mib_size_log2;
  cm->mi_params.mi_rows = mi_rows;
  cm->mi_params.mi_cols = mi_cols;
  cm->width = mi_cols * 4;
  cm->superres_upscaled_width = cm->width;

  av1_get_tile_limits(cm);
  CommonTileParams *const t = &cm->tiles;
  t->uniform_spacing = uniform;
  if (uniform) {
    t->log2_cols = clamp(log2_cols, t->min_log2_cols, t->max_log2_cols);
  } else {
    t->cols = ncols;
    for (int i = 0; i <= ncols; i++) t->col_start_sb[i] = col_starts[i];
  }
  av1_calculate_tile_cols(seq, mi_rows, mi_cols, t);
  if (uniform) {
    t->log2_rows = clamp(log2_rows, t->min_log2_rows, t->max_log2_rows);
  } else {
    t->rows = nrows;
    for (int i = 0; i <= nrows; i++) t->row_start_sb[i] = row_starts[i];
  }
  av1_calculate_tile_rows(seq, mi_rows, t);

  params[0] = t->cols;
  params[1] = t->rows;
  params[2] = t->max_width_sb;
  params[3] = t->max_height_sb;
  params[4] = t->min_inner_width;
  params[5] = t->log2_cols;
  params[6] = t->log2_rows;
  params[7] = t->width;
  params[8] = t->height;
  params[9] = t->min_log2_cols;
  params[10] = t->min_log2_rows;
  params[11] = t->max_log2_cols;
  params[12] = t->max_log2_rows;
  params[13] = t->min_log2;
  params[14] = t->uniform_spacing;
  params[15] = av1_is_min_tile_width_satisfied(cm);
  for (int i = 0; i <= t->cols; i++) col_starts[i] = t->col_start_sb[i];
  for (int i = 0; i <= t->rows; i++) row_starts[i] = t->row_start_sb[i];

  int n = 0;
  for (int r = 0; r < t->rows; r++) {
    for (int c = 0; c < t->cols; c++) {
      if (n >= tiles_cap) break;
      TileInfo tile;
      av1_tile_init(&tile, cm, r, c);
      int *o = tiles + 8 * n++;
      o[0] = tile.mi_row_start;
      o[1] = tile.mi_row_end;
      o[2] = tile.mi_col_start;
      o[3] = tile.mi_col_end;
      o[4] = tile.tile_row;
      o[5] = tile.tile_col;
      o[6] = av1_get_sb_rows_in_tile(cm, &tile);
      o[7] = av1_get_sb_cols_in_tile(cm, &tile);
    }
  }
  free(seq);
  free(cm);
  return n;
}

/* ---- alloccommon / timing --------------------------------------------------------------- */

EXPORT int aomshim_e5_get_MBs(int width, int height) { return av1_get_MBs(width, height); }

EXPORT int64_t aomshim_e5_max_level_bitrate(int profile, int level, int tier) {
  return av1_max_level_bitrate((BITSTREAM_PROFILE)profile, level, tier);
}

/* Runs av1_alloc_cdef_buffers for each (mi_rows, mi_cols, num_workers) configuration in turn on
 * the same AV1_COMMON (so the reallocation logic runs) and records, per step, the allocated
 * sizes: srcbuf, colbuf[3], linebuf[3], allocated_mi_rows, allocated_num_workers, and whether
 * worker buffers exist. out: 10 ints per step. */
EXPORT int aomshim_e5_cdef_alloc(int steps, const int *cfg /* mi_rows, mi_cols, workers, ss_x,
                                                              enable_cdef per step */,
                                 int *out) {
  AV1_COMMON *cm = (AV1_COMMON *)calloc(1, sizeof(*cm));
  SequenceHeader *seq = (SequenceHeader *)calloc(1, sizeof(*seq));
  struct aom_internal_error_info *err =
      (struct aom_internal_error_info *)calloc(1, sizeof(*err));
  AV1CdefSync sync;
  memset(&sync, 0, sizeof(sync));
  AV1CdefWorkerData *workers = NULL;
  if (!cm || !seq || !err) return -1;
  cm->seq_params = seq;
  cm->error = err;
  for (int s = 0; s < steps; s++) {
    const int *c = cfg + 5 * s;
    cm->mi_params.mi_rows = c[0];
    cm->mi_params.mi_cols = c[1];
    seq->subsampling_x = c[3];
    seq->subsampling_y = c[3];
    seq->enable_cdef = (uint8_t)c[4];
    av1_alloc_cdef_buffers(cm, &workers, &sync, c[2], 1);
    int *o = out + 10 * s;
    o[0] = (int)cm->cdef_info.allocated_srcbuf_size;
    for (int p = 0; p < 3; p++) o[1 + p] = (int)cm->cdef_info.allocated_colbuf_size[p];
    for (int p = 0; p < 3; p++) o[4 + p] = (int)cm->cdef_info.allocated_linebuf_size[p];
    o[7] = cm->cdef_info.allocated_mi_rows;
    o[8] = cm->cdef_info.allocated_num_workers;
    o[9] = workers != NULL;
  }
  av1_free_cdef_buffers(cm, &workers, &sync);
  free(err);
  free(seq);
  free(cm);
  return steps;
}

/* ---- lookahead -------------------------------------------------------------------------- */

/* Runs a script of lookahead operations on frames of width x height (all frames filled with
 * the byte value op_arg + 1 for pushes). ops: pairs (op, arg): op 0 = push, 1 = pop(drain=arg),
 * 2 = peek(index=arg), 3 = depth. results: per op, the buffer index (entry - ctx->buf) or -1
 * (or the push return value / depth), then the entry's display_idx (or -1). */
EXPORT int aomshim_e5_lookahead(int width, int height, int depth, int nops, const int *ops,
                                int *results) {
  struct lookahead_ctx *ctx =
      av1_lookahead_init(width, height, 1, 1, 0, depth, AOM_BORDER_IN_PIXELS, 0, 0, false, false);
  if (!ctx) return -1;
  YV12_BUFFER_CONFIG src;
  memset(&src, 0, sizeof(src));
  if (aom_alloc_frame_buffer(&src, width, height, 1, 1, 0, 32, 0, false, 0)) return -1;
  for (int i = 0; i < nops; i++) {
    const int op = ops[2 * i], arg = ops[2 * i + 1];
    int r0 = -1, r1 = -1;
    struct lookahead_entry *e = NULL;
    if (op == 0) {
      memset(src.buffer_alloc, (arg + 1) & 0xFF, src.frame_size);
      r0 = av1_lookahead_push(ctx, &src, 1000 * i, 1000 * i + 999, 0, false, arg);
    } else if (op == 1) {
      e = av1_lookahead_pop(ctx, arg, ENCODE_STAGE);
    } else if (op == 2) {
      e = av1_lookahead_peek(ctx, arg, ENCODE_STAGE);
    } else {
      r0 = av1_lookahead_depth(ctx, ENCODE_STAGE);
      r1 = av1_lookahead_pop_sz(ctx, ENCODE_STAGE);
    }
    if (e != NULL) {
      r0 = (int)(e - ctx->buf);
      r1 = e->display_idx;
    }
    results[2 * i] = r0;
    results[2 * i + 1] = r1;
  }
  aom_free_frame_buffer(&src);
  av1_lookahead_destroy(ctx);
  return nops;
}
