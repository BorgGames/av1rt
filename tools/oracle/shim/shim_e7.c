/* e7 shim: libaom rate control (ratectrl.c) and cyclic refresh (aq_cyclicrefresh.c).
 *
 * Stateful functions are driven through a flat E7State (see gen_e7_fields.py): the shim
 * builds a zeroed AV1_COMP/AV1_PRIMARY, loads the state into it, calls the libaom function and
 * stores the state back, so the C# harness can do the same and compare every field.
 * Not thread safe (one static encoder context): the C# tests serialize their calls. */
#include <stdlib.h>
#include <string.h>

#include "config/aom_dsp_rtcd.h"
#include "aom_mem/aom_mem.h"
#include "av1/encoder/aq_cyclicrefresh.h"
#include "av1/encoder/encoder.h"
#include "av1/encoder/ratectrl.h"
#include "shim_e7_fields.h"

#define EXPORT __declspec(dllexport)

#define DECL_I(name, path) int64_t name;
#define DECL_D(name, path) double name;
#define DECL_X(name) int64_t name;
typedef struct E7State {
  E7_ALL_FIELDS(DECL_I, DECL_D, DECL_X)
} E7State;

#define LOAD_I(name, path) path = s->name;
#define LOAD_D(name, path) path = s->name;
#define STORE_I(name, path) s->name = (int64_t)(path);
#define STORE_D(name, path) s->name = (path);
#define NOP_X(name)

static AV1_COMP *g_cpi;
static AV1_PRIMARY *g_ppi;
static CYCLIC_REFRESH *g_cr;
static int8_t *g_cr_map;
static RefCntBuffer g_prev_frame;
static RefCntBuffer g_cur_frame;
static MACROBLOCK g_x;
static MB_MODE_INFO g_mbmi;
static MB_MODE_INFO *g_mi_ptr;

static AV1_COMP *setup(const E7State *s) {
  if (g_cpi == NULL) {
    g_cpi = (AV1_COMP *)aom_calloc(1, sizeof(*g_cpi));
    g_ppi = (AV1_PRIMARY *)aom_calloc(1, sizeof(*g_ppi));
    g_cr = av1_cyclic_refresh_alloc(1, 1);
    g_cr_map = g_cr->map;
  } else {
    aom_free(g_cpi->src_sad_blk_64x64);
    memset(g_cpi, 0, sizeof(*g_cpi));
    memset(g_ppi, 0, sizeof(*g_ppi));
    memset(g_cr, 0, sizeof(*g_cr));
    g_cr->map = g_cr_map;
  }
  av1_rc_init_minq_luts();  /* av1_create_compressor() does this once */
  AV1_COMP *const cpi = g_cpi;
  cpi->ppi = g_ppi;
  cpi->common.seq_params = &g_ppi->seq_params;
  cpi->common.seq_params->bit_depth = AOM_BITS_8;
  cpi->oxcf.tool_cfg.bit_depth = AOM_BITS_8;
  cpi->cyclic_refresh = g_cr;
  E7_ALL_FIELDS(LOAD_I, LOAD_D, NOP_X)
  memset(&g_prev_frame, 0, sizeof(g_prev_frame));
  g_prev_frame.width = (int)s->x_prev_frame_width;
  g_prev_frame.height = (int)s->x_prev_frame_height;
  cpi->common.prev_frame = s->x_has_prev_frame ? &g_prev_frame : NULL;
  cpi->ppi->fn_ptr[BLOCK_64X64].sdf = aom_sad64x64_c;
  /* What save_all_coding_context() leaves for restore_all_coding_context(). */
  cpi->coding_context.rc = cpi->rc;
  cpi->coding_context.frame_number = cpi->common.current_frame.frame_number;
  return cpi;
}

static void store(E7State *s) {
  AV1_COMP *const cpi = g_cpi;
  E7_ALL_FIELDS(STORE_I, STORE_D, NOP_X)
}

EXPORT int e7_state_size(void) { return (int)sizeof(E7State); }

/* ---- pure functions (8-bit) ------------------------------------------------------------ */

EXPORT double e7_convert_qindex_to_q(int qindex) {
  return av1_convert_qindex_to_q(qindex, AOM_BITS_8);
}

EXPORT int e7_convert_q_to_qindex(double q) {
  return av1_convert_q_to_qindex(q, AOM_BITS_8);
}

EXPORT int e7_find_qindex(double desired_q, int best_qindex, int worst_qindex) {
  return av1_find_qindex(desired_q, AOM_BITS_8, best_qindex, worst_qindex);
}

EXPORT int e7_default_min_gf_interval(int width, int height, double framerate) {
  return av1_rc_get_default_min_gf_interval(width, height, framerate);
}

/* ---- functions reading an AV1_COMP --------------------------------------------------- */

EXPORT int e7_rc_bits_per_mb(E7State *s, int frame_type, int qindex,
                             double correction_factor) {
  AV1_COMP *cpi = setup(s);
  int r = av1_rc_bits_per_mb(cpi, (FRAME_TYPE)frame_type, qindex,
                             correction_factor, 0);
  store(s);
  return r;
}

EXPORT int e7_estimate_bits_at_q(E7State *s, int q, double correction_factor) {
  AV1_COMP *cpi = setup(s);
  int r = av1_estimate_bits_at_q(cpi, q, correction_factor);
  store(s);
  return r;
}

EXPORT int e7_compute_qdelta(E7State *s, double qstart, double qtarget) {
  AV1_COMP *cpi = setup(s);
  int r = av1_compute_qdelta(&cpi->rc, qstart, qtarget, AOM_BITS_8);
  store(s);
  return r;
}

EXPORT int e7_compute_qdelta_by_rate(E7State *s, int frame_type, int qindex,
                                     double rate_target_ratio) {
  AV1_COMP *cpi = setup(s);
  int r = av1_compute_qdelta_by_rate(cpi, (FRAME_TYPE)frame_type, qindex,
                                     rate_target_ratio);
  store(s);
  return r;
}

EXPORT int e7_cr_rc_bits_per_mb(E7State *s, int i, double correction_factor) {
  AV1_COMP *cpi = setup(s);
  int r = av1_cyclic_refresh_rc_bits_per_mb(cpi, i, correction_factor);
  store(s);
  return r;
}

EXPORT int e7_cr_estimate_bits_at_q(E7State *s, double correction_factor) {
  AV1_COMP *cpi = setup(s);
  int r = av1_cyclic_refresh_estimate_bits_at_q(cpi, correction_factor);
  store(s);
  return r;
}

/* Batch forms (one context for every qindex), out[QINDEX_RANGE]. */
EXPORT void e7_rc_bits_per_mb_all(E7State *s, int frame_type, double correction_factor,
                                  int *out) {
  AV1_COMP *cpi = setup(s);
  for (int q = 0; q < QINDEX_RANGE; ++q)
    out[q] = av1_rc_bits_per_mb(cpi, (FRAME_TYPE)frame_type, q, correction_factor, 0);
}

EXPORT void e7_estimate_bits_at_q_all(E7State *s, double correction_factor, int *out) {
  AV1_COMP *cpi = setup(s);
  for (int q = 0; q < QINDEX_RANGE; ++q)
    out[q] = av1_estimate_bits_at_q(cpi, q, correction_factor);
}

EXPORT void e7_compute_qdelta_by_rate_all(E7State *s, int frame_type,
                                          double rate_target_ratio, int *out) {
  AV1_COMP *cpi = setup(s);
  for (int q = 0; q < QINDEX_RANGE; ++q)
    out[q] = av1_compute_qdelta_by_rate(cpi, (FRAME_TYPE)frame_type, q,
                                        rate_target_ratio);
}

EXPORT void e7_cr_rc_bits_per_mb_all(E7State *s, double correction_factor, int *out) {
  AV1_COMP *cpi = setup(s);
  for (int q = 0; q < QINDEX_RANGE; ++q)
    out[q] = av1_cyclic_refresh_rc_bits_per_mb(cpi, q, correction_factor);
}

/* ---- stateful functions -------------------------------------------------------------- */

EXPORT void e7_rc_init(E7State *s) {
  AV1_COMP *cpi = setup(s);
  av1_primary_rc_init(&cpi->oxcf, &cpi->ppi->p_rc);
  av1_rc_init(&cpi->oxcf, &cpi->rc);
  store(s);
}

EXPORT void e7_rc_update_framerate(E7State *s) {
  AV1_COMP *cpi = setup(s);
  av1_rc_update_framerate(cpi, cpi->common.width, cpi->common.height);
  store(s);
}

EXPORT int e7_calc_pframe_target(E7State *s, int frame_update_type) {
  AV1_COMP *cpi = setup(s);
  int r = av1_calc_pframe_target_size_one_pass_cbr(
      cpi, (FRAME_UPDATE_TYPE)frame_update_type);
  store(s);
  return r;
}

EXPORT int e7_calc_iframe_target(E7State *s) {
  AV1_COMP *cpi = setup(s);
  int r = av1_calc_iframe_target_size_one_pass_cbr(cpi);
  store(s);
  return r;
}

EXPORT int e7_rc_drop_frame(E7State *s) {
  AV1_COMP *cpi = setup(s);
  int r = av1_rc_drop_frame(cpi);
  store(s);
  return r;
}

EXPORT void e7_rc_set_frame_target(E7State *s, int target) {
  AV1_COMP *cpi = setup(s);
  av1_rc_set_frame_target(cpi, target, cpi->common.width, cpi->common.height);
  store(s);
}

EXPORT int e7_pick_q_and_bounds(E7State *s, int *bottom_index, int *top_index) {
  AV1_COMP *cpi = setup(s);
  int r = av1_rc_pick_q_and_bounds(cpi, cpi->common.width, cpi->common.height,
                                   cpi->gf_frame_index, bottom_index, top_index);
  store(s);
  return r;
}

EXPORT void e7_update_rate_correction_factors(E7State *s, int is_encode_stage) {
  AV1_COMP *cpi = setup(s);
  av1_rc_update_rate_correction_factors(cpi, is_encode_stage, cpi->common.width,
                                        cpi->common.height);
  store(s);
}

EXPORT void e7_postencode_update(E7State *s, uint64_t bytes_used) {
  AV1_COMP *cpi = setup(s);
  av1_rc_postencode_update(cpi, bytes_used);
  store(s);
}

EXPORT void e7_postencode_update_drop_frame(E7State *s) {
  setup(s);
  av1_rc_postencode_update_drop_frame(g_cpi);
  store(s);
}

EXPORT int e7_encodedframe_overshoot_cbr(E7State *s, int *q) {
  AV1_COMP *cpi = setup(s);
  int r = av1_encodedframe_overshoot_cbr(cpi, q);
  store(s);
  return r;
}

EXPORT int e7_postencode_drop_cbr(E7State *s, int *size) {
  AV1_COMP *cpi = setup(s);
  size_t sz = (size_t)*size;
  int r = av1_postencode_drop_cbr(cpi, &sz);
  *size = (int)sz;
  store(s);
  return r;
}

EXPORT void e7_cr_update_parameters(E7State *s) {
  AV1_COMP *cpi = setup(s);
  av1_cyclic_refresh_update_parameters(cpi);
  store(s);
}

EXPORT void e7_adjust_gf_refresh_qp(E7State *s) {
  AV1_COMP *cpi = setup(s);
  av1_adjust_gf_refresh_qp_one_pass_rt(cpi);
  store(s);
}

EXPORT void e7_set_rtc_reference_structure(E7State *s, int gf_update) {
  AV1_COMP *cpi = setup(s);
  av1_set_rtc_reference_structure_one_layer(cpi, gf_update);
  store(s);
}

static YV12_BUFFER_CONFIG g_src, g_last;

static void set_plane(YV12_BUFFER_CONFIG *b, const uint8_t *y, int stride, int w,
                      int h) {
  memset(b, 0, sizeof(*b));
  b->y_buffer = (uint8_t *)y;
  b->y_stride = stride;
  b->y_width = w;
  b->y_height = h;
}

/* Copies cpi->src_sad_blk_64x64 to blk_out; returns its size (-1 when NULL). */
static int copy_blk_sad(uint64_t *blk_out, int blk_cap) {
  if (g_cpi->src_sad_blk_64x64 == NULL) return -1;
  int n = g_cpi->src_sad_blk_alloc_size;
  for (int i = 0; i < n && i < blk_cap; ++i) blk_out[i] = g_cpi->src_sad_blk_64x64[i];
  return n;
}

/* last_w/last_h let the caller test the size-mismatch early return. */
EXPORT int e7_scene_detection(E7State *s, const uint8_t *src, const uint8_t *last,
                              int stride, int w, int h, int last_w, int last_h,
                              uint64_t *blk_out, int blk_cap) {
  AV1_COMP *cpi = setup(s);
  set_plane(&g_src, src, stride, w, h);
  set_plane(&g_last, last, stride, last_w, last_h);
  EncodeFrameInput fi = { &g_src, &g_last, 0 };
  av1_rc_scene_detection_onepass_rt(cpi, &fi);
  store(s);
  return copy_blk_sad(blk_out, blk_cap);
}

/* src == NULL: frame_input without sources. Returns the frame type. */
EXPORT int e7_get_one_pass_rt_params(E7State *s, unsigned int frame_flags,
                                     const uint8_t *src, const uint8_t *last,
                                     int stride, int w, int h, uint64_t *blk_out,
                                     int blk_cap, int *blk_size) {
  AV1_COMP *cpi = setup(s);
  EncodeFrameInput fi = { NULL, NULL, 0 };
  if (src != NULL) {
    set_plane(&g_src, src, stride, w, h);
    set_plane(&g_last, last, stride, w, h);
    fi.source = &g_src;
    fi.last_source = &g_last;
  }
  FRAME_TYPE frame_type = INTER_FRAME;
  av1_get_one_pass_rt_params(cpi, &frame_type, &fi, frame_flags);
  store(s);
  *blk_size = copy_blk_sad(blk_out, blk_cap);
  return frame_type;
}

/* ---- cyclic refresh block level ----------------------------------------------------- */

static void setup_block(int mv_row, int mv_col, int ref0, int ref1, int segment_id,
                        const int *x_counts) {
  memset(&g_x, 0, sizeof(g_x));
  memset(&g_mbmi, 0, sizeof(g_mbmi));
  g_mbmi.mv[0].as_mv.row = (int16_t)mv_row;
  g_mbmi.mv[0].as_mv.col = (int16_t)mv_col;
  g_mbmi.ref_frame[0] = (MV_REFERENCE_FRAME)ref0;
  g_mbmi.ref_frame[1] = (MV_REFERENCE_FRAME)ref1;
  g_mbmi.segment_id = (uint8_t)segment_id;
  g_mi_ptr = &g_mbmi;
  g_x.e_mbd.mi = &g_mi_ptr;
  g_x.actual_num_seg1_blocks = x_counts[0];
  g_x.actual_num_seg2_blocks = x_counts[1];
}

/* Maps are mi_rows * mi_cols; x_counts = { actual_num_seg1_blocks, ..seg2.. } in/out.
 * Returns the block's new segment_id. */
EXPORT int e7_cr_update_segment(E7State *s, int mi_row, int mi_col, int bsize,
                                int64_t rate, int64_t dist, int skip, int dry_run,
                                int mv_row, int mv_col, int ref0, int ref1,
                                int segment_id, uint8_t *enc_map, uint8_t *seg_map,
                                int8_t *cr_map, int *x_counts) {
  AV1_COMP *cpi = setup(s);
  setup_block(mv_row, mv_col, ref0, ref1, segment_id, x_counts);
  memset(&g_cur_frame, 0, sizeof(g_cur_frame));
  g_cur_frame.seg_map = seg_map;
  cpi->common.cur_frame = &g_cur_frame;
  cpi->enc_seg.map = enc_map;
  g_cr->map = cr_map;
  av1_cyclic_refresh_update_segment(cpi, &g_x, mi_row, mi_col, (BLOCK_SIZE)bsize,
                                    rate, dist, skip, (RUN_TYPE)dry_run);
  g_cr->map = g_cr_map;
  x_counts[0] = g_x.actual_num_seg1_blocks;
  x_counts[1] = g_x.actual_num_seg2_blocks;
  store(s);
  return g_mbmi.segment_id;
}

EXPORT int e7_cr_reset_segment_skip(E7State *s, int mi_row, int mi_col, int bsize,
                                    int dry_run, int segment_id, int *x_counts) {
  AV1_COMP *cpi = setup(s);
  setup_block(0, 0, 0, 0, segment_id, x_counts);
  av1_cyclic_reset_segment_skip(cpi, &g_x, mi_row, mi_col, (BLOCK_SIZE)bsize,
                                (RUN_TYPE)dry_run);
  x_counts[0] = g_x.actual_num_seg1_blocks;
  x_counts[1] = g_x.actual_num_seg2_blocks;
  store(s);
  return g_mbmi.segment_id;
}
