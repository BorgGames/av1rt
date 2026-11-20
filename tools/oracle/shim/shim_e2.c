/* e2 (transform) shim: forward/inverse transforms and quantizers of the pure-C libaom build,
 * for the differential tests in tests/Av1rt.Tests/Transform. */
#include <string.h>

#include "config/aom_dsp_rtcd.h"
#include "config/av1_rtcd.h"

#include "aom_dsp/txfm_common.h"
#include "av1/common/blockd.h"
#include "av1/common/idct.h"
#include "av1/common/scan.h"
#include "av1/encoder/av1_quantize.h"
#include "av1/encoder/block.h"
#include "av1/encoder/hybrid_fwd_txfm.h"

#define EXPORT __declspec(dllexport)

static void init_txfm_param(TxfmParam *p, int tx_size, int tx_type, int eob) {
  memset(p, 0, sizeof(*p));
  p->tx_type = (TX_TYPE)tx_type;
  p->tx_size = (TX_SIZE)tx_size;
  p->lossless = 0;
  p->bd = 8;
  p->is_hbd = 0;
  p->tx_set_type = EXT_TX_SET_ALL16;
  p->eob = eob;
}

/* av1_lowbd_fwd_txfm (rtcd -> av1_lowbd_fwd_txfm_c -> av1_highbd_fwd_txfm). */
EXPORT void e2_fwd_txfm(const int16_t *src_diff, int32_t *coeff, int diff_stride,
                        int tx_size, int tx_type) {
  TxfmParam p;
  init_txfm_param(&p, tx_size, tx_type, 0);
  av1_lowbd_fwd_txfm(src_diff, coeff, diff_stride, &p);
}

/* av1_inv_txfm_add (rtcd -> av1_inv_txfm_add_c). */
EXPORT void e2_inv_txfm_add(const int32_t *dqcoeff, uint8_t *dst, int stride, int tx_size,
                            int tx_type, int eob) {
  TxfmParam p;
  init_txfm_param(&p, tx_size, tx_type, eob);
  av1_inv_txfm_add(dqcoeff, dst, stride, &p);
}

/* Scan order of (tx_size, tx_type); copies av1_get_max_eob(tx_size) entries. */
EXPORT int e2_get_scan(int tx_size, int tx_type, int16_t *scan, int16_t *iscan) {
  const SCAN_ORDER *sc = get_scan((TX_SIZE)tx_size, (TX_TYPE)tx_type);
  const int n = av1_get_max_eob((TX_SIZE)tx_size);
  memcpy(scan, sc->scan, n * sizeof(*scan));
  memcpy(iscan, sc->iscan, n * sizeof(*iscan));
  return n;
}

/* av1_build_quantizer into flat copies of QUANTS / Dequants (field order as in the C struct). */
static QUANTS g_quants;
static Dequants g_dequants;

EXPORT void e2_build_quantizer(int bit_depth, int y_dc_delta_q, int u_dc_delta_q,
                               int u_ac_delta_q, int v_dc_delta_q, int v_ac_delta_q,
                               int sharpness, int16_t *quants_out, int16_t *dequants_out) {
  av1_build_quantizer((aom_bit_depth_t)bit_depth, y_dc_delta_q, u_dc_delta_q, u_ac_delta_q,
                      v_dc_delta_q, v_ac_delta_q, &g_quants, &g_dequants, sharpness);
  memcpy(quants_out, &g_quants, sizeof(g_quants));
  memcpy(dequants_out, &g_dequants, sizeof(g_dequants));
}

EXPORT int e2_quants_size(void) { return (int)(sizeof(QUANTS) / sizeof(int16_t)); }
EXPORT int e2_dequants_size(void) { return (int)(sizeof(Dequants) / sizeof(int16_t)); }

/* av1_quantize_b_facade with a MACROBLOCK_PLANE built from the given 8-entry rows, the
 * libaom scan order of (tx_size, tx_type) and no quantization matrix. */
EXPORT void e2_quantize_b_facade(const int32_t *coeff, int n_coeffs, const int16_t *zbin,
                                 const int16_t *round, const int16_t *quant,
                                 const int16_t *quant_shift, const int16_t *dequant,
                                 int32_t *qcoeff, int32_t *dqcoeff, uint16_t *eob,
                                 int tx_size, int tx_type) {
  MACROBLOCK_PLANE p;
  memset(&p, 0, sizeof(p));
  p.zbin_QTX = zbin;
  p.round_QTX = round;
  p.quant_QTX = quant;
  p.quant_shift_QTX = quant_shift;
  p.dequant_QTX = dequant;
  QUANT_PARAM qparam;
  memset(&qparam, 0, sizeof(qparam));
  qparam.log_scale = av1_get_tx_scale((TX_SIZE)tx_size);
  qparam.tx_size = (TX_SIZE)tx_size;
  qparam.qmatrix = NULL;
  qparam.iqmatrix = NULL;
  qparam.use_quant_b_adapt = 0;
  qparam.use_optimize_b = 0;
  qparam.xform_quant_idx = 1; /* AV1_XFORM_QUANT_B */
  const SCAN_ORDER *sc = get_scan((TX_SIZE)tx_size, (TX_TYPE)tx_type);
  av1_quantize_b_facade(coeff, n_coeffs, &p, qcoeff, dqcoeff, eob, sc, &qparam);
}

/* av1_quantize_lp (rtcd -> av1_quantize_lp_c). */
EXPORT void e2_quantize_lp(const int16_t *coeff, int n_coeffs, const int16_t *round,
                           const int16_t *quant, int16_t *qcoeff, int16_t *dqcoeff,
                           const int16_t *dequant, uint16_t *eob, const int16_t *scan,
                           const int16_t *iscan) {
  av1_quantize_lp(coeff, n_coeffs, round, quant, qcoeff, dqcoeff, dequant, eob, scan, iscan);
}

EXPORT int e2_quantizer_to_qindex(int quantizer) { return av1_quantizer_to_qindex(quantizer); }
EXPORT int e2_qindex_to_quantizer(int qindex) { return av1_qindex_to_quantizer(qindex); }
