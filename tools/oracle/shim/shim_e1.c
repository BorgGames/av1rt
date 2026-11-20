/* Shim of work package e1 (entropy): exports the default CDF / scan / txb / quantizer tables
   and the functions of entropy.c, entropymode.c, entropymv.c, quant_common.c, seg_common.c,
   cost.c and aom_integer.c, plus the static inline context helpers of txb_common.h (compiled
   into this DLL), for the differential tests in tests/Av1rt.Tests/E1. */
#include <stdlib.h>
#include <string.h>

#include "config/aom_config.h"
#include "config/av1_rtcd.h"

#include "aom/aom_integer.h"
#include "av1/common/av1_common_int.h"
#include "av1/common/blockd.h"
#include "av1/common/entropy.h"
#include "av1/common/entropymode.h"
#include "av1/common/entropymv.h"
#include "av1/common/quant_common.h"
#include "av1/common/scan.h"
#include "av1/common/seg_common.h"
#include "av1/common/txb_common.h"
#include "av1/encoder/cost.h"

#define EXPORT __declspec(dllexport)

extern const int16_t av1_eob_offset_bits[12];

/* ---- FRAME_CONTEXT initialisation (entropy.c, entropymode.c, entropymv.c) ---------------- */

EXPORT int aomshim_e1_frame_context_size(void) { return (int)sizeof(FRAME_CONTEXT); }

EXPORT void aomshim_e1_init_mode_probs(FRAME_CONTEXT *fc) { av1_init_mode_probs(fc); }

EXPORT void aomshim_e1_default_coef_probs(int base_qindex, FRAME_CONTEXT *fc) {
  AV1_COMMON *cm = calloc(1, sizeof(*cm));
  cm->fc = fc;
  cm->quant_params.base_qindex = base_qindex;
  av1_default_coef_probs(cm);
  free(cm);
}

EXPORT void aomshim_e1_init_mv_probs(FRAME_CONTEXT *fc) {
  AV1_COMMON *cm = calloc(1, sizeof(*cm));
  cm->fc = fc;
  av1_init_mv_probs(cm);
  free(cm);
}

EXPORT void aomshim_e1_reset_cdf_symbol_counters(FRAME_CONTEXT *fc) {
  av1_reset_cdf_symbol_counters(fc);
}

/* Runs av1_setup_past_independence on a minimal AV1_COMMON. seg_feature_data[8*8] /
   seg_feature_mask[8] are the initial cm->seg features (to see them cleared); seg_map
   (mi_rows * mi_cols bytes, may be NULL) is the current frame's map. Outputs: fc, dfc
   (cm->default_frame_context), deltas[0..7] cur_frame->ref_deltas, [8..9] mode_deltas,
   [10..17] lf.ref_deltas, [18..19] lf.mode_deltas, lf_flags[0..1] mode_ref_delta_enabled /
   _update, initialized = fc->initialized. */
EXPORT void aomshim_e1_setup_past_independence(int base_qindex, int mi_rows, int mi_cols,
                                               uint8_t *seg_map, int16_t *seg_feature_data,
                                               uint32_t *seg_feature_mask, FRAME_CONTEXT *fc,
                                               FRAME_CONTEXT *dfc, int8_t *deltas,
                                               int *lf_flags) {
  AV1_COMMON *cm = calloc(1, sizeof(*cm));
  RefCntBuffer *cur = calloc(1, sizeof(*cur));
  cur->seg_map = seg_map;
  cur->mi_rows = mi_rows;
  cur->mi_cols = mi_cols;
  cm->cur_frame = cur;
  cm->fc = fc;
  cm->default_frame_context = dfc;
  cm->quant_params.base_qindex = base_qindex;
  memcpy(cm->seg.feature_data, seg_feature_data, sizeof(cm->seg.feature_data));
  memcpy(cm->seg.feature_mask, seg_feature_mask, sizeof(cm->seg.feature_mask));
  av1_setup_past_independence(cm);
  memcpy(seg_feature_data, cm->seg.feature_data, sizeof(cm->seg.feature_data));
  memcpy(seg_feature_mask, cm->seg.feature_mask, sizeof(cm->seg.feature_mask));
  memcpy(deltas, cur->ref_deltas, 8);
  memcpy(deltas + 8, cur->mode_deltas, 2);
  memcpy(deltas + 10, cm->lf.ref_deltas, 8);
  memcpy(deltas + 18, cm->lf.mode_deltas, 2);
  lf_flags[0] = cm->lf.mode_ref_delta_enabled;
  lf_flags[1] = cm->lf.mode_ref_delta_update;
  free(cur);
  free(cm);
}

/* ---- tables ------------------------------------------------------------------------------ */

EXPORT void aomshim_e1_scan_order(int tx_size, int tx_type, int16_t *scan, int16_t *iscan,
                                  int n) {
  const SCAN_ORDER *so = &av1_scan_orders[tx_size][tx_type];
  memcpy(scan, so->scan, n * sizeof(*scan));
  memcpy(iscan, so->iscan, n * sizeof(*iscan));
}

EXPORT void aomshim_e1_txb_padded_idx(int i, int16_t *out, int n) {
  memcpy(out, av1_txb_padded_idx[i], n * sizeof(*out));
}

EXPORT void aomshim_e1_nz_map_ctx_offset(int tx_size, int8_t *out, int n) {
  memcpy(out, av1_nz_map_ctx_offset[tx_size], n);
}

EXPORT void aomshim_e1_eob_tables(int16_t *group_start, int16_t *offset_bits) {
  memcpy(group_start, av1_eob_group_start, 12 * sizeof(int16_t));
  memcpy(offset_bits, av1_eob_offset_bits, 12 * sizeof(int16_t));
}

EXPORT void aomshim_e1_prob_cost(uint16_t *out) {
  memcpy(out, av1_prob_cost, sizeof(av1_prob_cost));
}

EXPORT void aomshim_e1_cost_lut_low(uint16_t *out) {
  av1_init_cost_luts();
  memcpy(out, av1_cost_lut_low, sizeof(av1_cost_lut_low));
}

/* ---- quant_common.c ---------------------------------------------------------------------- */

EXPORT int aomshim_e1_dc_quant_QTX(int qindex, int delta, int bit_depth) {
  return av1_dc_quant_QTX(qindex, delta, (aom_bit_depth_t)bit_depth);
}

EXPORT int aomshim_e1_ac_quant_QTX(int qindex, int delta, int bit_depth) {
  return av1_ac_quant_QTX(qindex, delta, (aom_bit_depth_t)bit_depth);
}

static void make_seg(struct segmentation *seg, int enabled, const uint32_t *feature_mask,
                     const int16_t *feature_data) {
  memset(seg, 0, sizeof(*seg));
  seg->enabled = (uint8_t)enabled;
  memcpy(seg->feature_mask, feature_mask, sizeof(seg->feature_mask));
  memcpy(seg->feature_data, feature_data, sizeof(seg->feature_data));
}

EXPORT int aomshim_e1_get_qindex(int enabled, const uint32_t *feature_mask,
                                 const int16_t *feature_data, int segment_id,
                                 int base_qindex) {
  struct segmentation seg;
  make_seg(&seg, enabled, feature_mask, feature_data);
  return av1_get_qindex(&seg, segment_id, base_qindex);
}

EXPORT int aomshim_e1_use_qmatrix(int using_qmatrix, const int *lossless, int segment_id) {
  CommonQuantParams qp;
  memset(&qp, 0, sizeof(qp));
  qp.using_qmatrix = using_qmatrix != 0;
  MACROBLOCKD *xd = calloc(1, sizeof(*xd));
  memcpy(xd->lossless, lossless, sizeof(xd->lossless));
  int r = av1_use_qmatrix(&qp, xd, segment_id);
  free(xd);
  return r;
}

/* ---- seg_common.c ------------------------------------------------------------------------ */

EXPORT void aomshim_e1_calculate_segdata(const uint32_t *feature_mask, int *segid_preskip,
                                         int *last_active_segid) {
  struct segmentation seg;
  memset(&seg, 0, sizeof(seg));
  memcpy(seg.feature_mask, feature_mask, sizeof(seg.feature_mask));
  seg.segid_preskip = 77;
  seg.last_active_segid = 77;
  av1_calculate_segdata(&seg);
  *segid_preskip = seg.segid_preskip;
  *last_active_segid = seg.last_active_segid;
}

EXPORT int aomshim_e1_seg_feature_data_max(int feature_id) {
  return av1_seg_feature_data_max((SEG_LVL_FEATURES)feature_id);
}

EXPORT int aomshim_e1_is_segfeature_signed(int feature_id) {
  return av1_is_segfeature_signed((SEG_LVL_FEATURES)feature_id);
}

/* ---- cost.c ------------------------------------------------------------------------------ */

EXPORT void aomshim_e1_cost_tokens_from_cdf(int *costs, const uint16_t *cdf,
                                            const int *inv_map) {
  av1_cost_tokens_from_cdf(costs, cdf, inv_map);
}

EXPORT void aomshim_e1_cost_tokens_from_cdf_2_batch(int *costs, int costs_stride,
                                                    const uint16_t *cdf, int cdf_stride,
                                                    int n) {
  av1_cost_tokens_from_cdf_2_batch_c(costs, costs_stride, cdf, cdf_stride, n);
}

EXPORT void aomshim_e1_cost_tokens_from_cdf_4_batch(int *costs, int costs_stride,
                                                    const uint16_t *cdf, int cdf_stride,
                                                    int n) {
  av1_cost_tokens_from_cdf_4_batch_c(costs, costs_stride, cdf, cdf_stride, n);
}

/* ---- aom_integer.c ----------------------------------------------------------------------- */

EXPORT int aomshim_e1_uleb_size_in_bytes(uint64_t value) {
  return (int)aom_uleb_size_in_bytes(value);
}

EXPORT int aomshim_e1_uleb_encode(uint64_t value, int available, uint8_t *coded_value,
                                  int *coded_size) {
  size_t size = (size_t)*coded_size;
  int r = aom_uleb_encode(value, (size_t)available, coded_value, &size);
  *coded_size = (int)size;
  return r;
}

EXPORT int aomshim_e1_uleb_decode(const uint8_t *buffer, int available, uint64_t *value,
                                  int *length) {
  size_t len = (size_t)*length;
  int r = aom_uleb_decode(buffer, (size_t)available, value, &len);
  *length = (int)len;
  return r;
}

/* ---- txb_common.h context helpers (use the txb_common.c / scan.c tables) ----------------- */

EXPORT int aomshim_e1_get_padded_idx(int idx, int bhl) { return get_padded_idx(idx, bhl); }

EXPORT int aomshim_e1_get_nz_map_ctx_from_stats(int stats, int coeff_idx, int bhl,
                                                int tx_size, int tx_class) {
  return get_nz_map_ctx_from_stats(stats, coeff_idx, bhl, (TX_SIZE)tx_size,
                                   (TX_CLASS)tx_class);
}

EXPORT int aomshim_e1_get_nz_mag(const uint8_t *levels_buf, int offset, int bhl,
                                 int tx_class) {
  return get_nz_mag(levels_buf + offset, bhl, (TX_CLASS)tx_class);
}

EXPORT int aomshim_e1_get_br_ctx(const uint8_t *levels_buf, int offset, int c, int bhl,
                                 int tx_class) {
  return get_br_ctx(levels_buf + offset, c, bhl, (TX_CLASS)tx_class);
}

EXPORT void aomshim_e1_get_txb_ctx(int plane_bsize, int tx_size, int plane,
                                   const int8_t *a, const int8_t *l, int *txb_skip_ctx,
                                   int *dc_sign_ctx) {
  TXB_CTX ctx;
  get_txb_ctx((BLOCK_SIZE)plane_bsize, (TX_SIZE)tx_size, plane, (const ENTROPY_CONTEXT *)a,
              (const ENTROPY_CONTEXT *)l, &ctx);
  *txb_skip_ctx = ctx.txb_skip_ctx;
  *dc_sign_ctx = ctx.dc_sign_ctx;
}

EXPORT int aomshim_e1_get_txb_entropy_context(const tran_low_t *qcoeff, int tx_size,
                                              int tx_type, int eob) {
  return av1_get_txb_entropy_context(qcoeff, get_scan((TX_SIZE)tx_size, (TX_TYPE)tx_type),
                                     eob);
}
