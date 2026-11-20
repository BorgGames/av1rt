/* Shim for package f2 (nonrd_pickmode.c, nonrd_opt.c): wrappers around the public nonrd
 * building blocks (av1_block_yrd, av1_model_rd_for_sb_uv) for differential tests of the C#
 * port. The file-static helpers of nonrd_pickmode.c are only reachable through
 * av1_nonrd_pick_inter_mode_sb(), which needs a whole encoder; they are not exported here. */
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#include "config/aom_dsp_rtcd.h"
#include "config/av1_rtcd.h"
#include "av1/common/common_data.h"
#include "av1/encoder/block.h"
#include "av1/encoder/encoder.h"
#include "av1/encoder/nonrd_opt.h"

#define EXPORT __declspec(dllexport)

/* io[] layout for aomshim_f2_block_yrd (mirrored in the C# test). */
enum {
  YRD_IN_SSE,  /* this_rdc->sse on entry */
  YRD_OUT_RATE,
  YRD_OUT_DIST,
  YRD_OUT_SSE,
  YRD_OUT_SKIP_TXFM,
  YRD_OUT_SKIPPABLE,
  YRD_IO_COUNT
};

/* Runs av1_block_yrd() on a bw x bh block. src/dst are the luma source and prediction;
 * round_fp/quant_fp/dequant are the int16_t[8] quantizer rows (index 0 = DC, 1.. = AC).
 * blk_skip_out receives x->txfm_search_info.blk_skip (MAX_MIB_SIZE * MAX_MIB_SIZE bytes). */
EXPORT void aomshim_f2_block_yrd(int bsize, int tx_size, const uint8_t *src, int src_stride,
                                 uint8_t *dst, int dst_stride, int mb_to_right_edge,
                                 int mb_to_bottom_edge, const int16_t *round_fp,
                                 const int16_t *quant_fp, const int16_t *dequant,
                                 int64_t *io, uint8_t *blk_skip_out) {
  MACROBLOCK *x = (MACROBLOCK *)calloc(1, sizeof(*x));
  int16_t *src_diff = (int16_t *)calloc(MAX_SB_SQUARE, sizeof(*src_diff));
  tran_low_t *coeff = (tran_low_t *)calloc(MAX_SB_SQUARE, sizeof(*coeff));
  struct macroblock_plane *p = &x->plane[AOM_PLANE_Y];
  struct macroblockd_plane *pd = &x->e_mbd.plane[AOM_PLANE_Y];
  p->src.buf = (uint8_t *)src;
  p->src.stride = src_stride;
  p->src_diff = src_diff;
  p->coeff = coeff;
  p->round_fp_QTX = round_fp;
  p->quant_fp_QTX = quant_fp;
  p->dequant_QTX = dequant;
  pd->dst.buf = dst;
  pd->dst.stride = dst_stride;
  x->e_mbd.mb_to_right_edge = mb_to_right_edge;
  x->e_mbd.mb_to_bottom_edge = mb_to_bottom_edge;

  RD_STATS rdc;
  av1_invalid_rd_stats(&rdc);
  rdc.sse = io[YRD_IN_SSE];
  int skippable = -1;
  av1_block_yrd(x, &rdc, &skippable, (BLOCK_SIZE)bsize, (TX_SIZE)tx_size);
  io[YRD_OUT_RATE] = rdc.rate;
  io[YRD_OUT_DIST] = rdc.dist;
  io[YRD_OUT_SSE] = rdc.sse;
  io[YRD_OUT_SKIP_TXFM] = rdc.skip_txfm;
  io[YRD_OUT_SKIPPABLE] = skippable;
  memcpy(blk_skip_out, x->txfm_search_info.blk_skip,
         sizeof(x->txfm_search_info.blk_skip));

  free(coeff);
  free(src_diff);
  free(x);
}

static aom_variance_fn_t variance_fn(int bsize) {
  switch (bsize) {
    case BLOCK_4X4: return aom_variance4x4_c;
    case BLOCK_4X8: return aom_variance4x8_c;
    case BLOCK_8X4: return aom_variance8x4_c;
    case BLOCK_8X8: return aom_variance8x8_c;
    case BLOCK_8X16: return aom_variance8x16_c;
    case BLOCK_16X8: return aom_variance16x8_c;
    case BLOCK_16X16: return aom_variance16x16_c;
    case BLOCK_16X32: return aom_variance16x32_c;
    case BLOCK_32X16: return aom_variance32x16_c;
    case BLOCK_32X32: return aom_variance32x32_c;
    default: return NULL;
  }
}

/* io[] layout for aomshim_f2_model_rd_for_sb_uv (mirrored in the C# test). */
enum {
  UV_OUT_TOT_SSE,
  UV_OUT_RATE,
  UV_OUT_DIST,
  UV_OUT_SKIP_TXFM,
  UV_IO_COUNT
};

/* Runs av1_model_rd_for_sb_uv() on the U and V planes (start/stop given). src_u/src_v and
 * dst_u/dst_v share src_stride / dst_stride; dequant_uv holds { dc_u, ac_u, dc_v, ac_v }. */
EXPORT void aomshim_f2_model_rd_for_sb_uv(int plane_bsize, int start_plane, int stop_plane,
                                          const uint8_t *src_u, const uint8_t *src_v,
                                          int src_stride, uint8_t *dst_u, uint8_t *dst_v,
                                          int dst_stride, const int16_t *dequant_uv,
                                          int color_sens_u, int color_sens_v, int rdmult,
                                          int64_t *io) {
  AV1_COMP *cpi = (AV1_COMP *)calloc(1, sizeof(*cpi));
  AV1_PRIMARY *ppi = (AV1_PRIMARY *)calloc(1, sizeof(*ppi));
  MACROBLOCK *x = (MACROBLOCK *)calloc(1, sizeof(*x));
  cpi->ppi = ppi;
  ppi->fn_ptr[plane_bsize].vf = variance_fn(plane_bsize);
  int16_t dq[2][8];
  for (int i = 0; i < 8; ++i) {
    dq[0][i] = i == 0 ? dequant_uv[0] : dequant_uv[1];
    dq[1][i] = i == 0 ? dequant_uv[2] : dequant_uv[3];
  }
  const uint8_t *src[2] = { src_u, src_v };
  uint8_t *dst[2] = { dst_u, dst_v };
  for (int j = 0; j < 2; ++j) {
    x->plane[1 + j].src.buf = (uint8_t *)src[j];
    x->plane[1 + j].src.stride = src_stride;
    x->plane[1 + j].dequant_QTX = dq[j];
    x->e_mbd.plane[1 + j].dst.buf = dst[j];
    x->e_mbd.plane[1 + j].dst.stride = dst_stride;
  }
  x->color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_U)] = (uint8_t)color_sens_u;
  x->color_sensitivity[COLOR_SENS_IDX(AOM_PLANE_V)] = (uint8_t)color_sens_v;
  x->rdmult = rdmult;

  RD_STATS rdc;
  av1_invalid_rd_stats(&rdc);
  io[UV_OUT_TOT_SSE] = av1_model_rd_for_sb_uv(cpi, (BLOCK_SIZE)plane_bsize, x, &x->e_mbd,
                                              &rdc, start_plane, stop_plane);
  io[UV_OUT_RATE] = rdc.rate;
  io[UV_OUT_DIST] = rdc.dist;
  io[UV_OUT_SKIP_TXFM] = rdc.skip_txfm;

  free(x);
  free(ppi);
  free(cpi);
}
