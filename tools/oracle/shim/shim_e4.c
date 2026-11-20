/* Shim of work package e4 (filters): exports libaom's deblocking kernels (aom_dsp/loopfilter.c),
   loop-filter threshold/level tables (av1/common/av1_loopfilter.c), CDEF kernels and block
   filter (av1/common/cdef_block.c, cdef.c), the Q-based level pickers (av1/encoder/picklpf.c,
   pickcdef.c) and a frame-level deblocking run, for the differential tests in
   tests/Av1rt.Tests/E4. */
#include <stdlib.h>
#include <string.h>

#include "config/aom_config.h"
#include "config/aom_dsp_rtcd.h"
#include "config/av1_rtcd.h"

#include "aom/aom_integer.h"
#include "aom_mem/aom_mem.h"
#include "av1/common/av1_common_int.h"
#include "av1/common/av1_loopfilter.h"
#include "av1/common/cdef.h"
#include "av1/common/cdef_block.h"
#include "av1/common/thread_common.h"
#include "av1/encoder/encoder.h"
#include "av1/encoder/pickcdef.h"
#include "av1/encoder/picklpf.h"

#define EXPORT __declspec(dllexport)

/* ---- aom_dsp/loopfilter.c ------------------------------------------------------------------ */

typedef void (*lpf_fn)(uint8_t *s, int pitch, const uint8_t *blimit, const uint8_t *limit,
                       const uint8_t *thresh);
typedef void (*lpf_dual_fn)(uint8_t *s, int pitch, const uint8_t *blimit0,
                            const uint8_t *limit0, const uint8_t *thresh0,
                            const uint8_t *blimit1, const uint8_t *limit1,
                            const uint8_t *thresh1);

/* kind = dir * 12 + size * 3 + variant; dir 0 horizontal / 1 vertical, size 0..3 = 4/6/8/14,
   variant 0 single / 1 dual / 2 quad. */
static const lpf_fn lpf_single[2][4] = {
  { aom_lpf_horizontal_4_c, aom_lpf_horizontal_6_c, aom_lpf_horizontal_8_c,
    aom_lpf_horizontal_14_c },
  { aom_lpf_vertical_4_c, aom_lpf_vertical_6_c, aom_lpf_vertical_8_c, aom_lpf_vertical_14_c },
};
static const lpf_dual_fn lpf_dual[2][4] = {
  { aom_lpf_horizontal_4_dual_c, aom_lpf_horizontal_6_dual_c, aom_lpf_horizontal_8_dual_c,
    aom_lpf_horizontal_14_dual_c },
  { aom_lpf_vertical_4_dual_c, aom_lpf_vertical_6_dual_c, aom_lpf_vertical_8_dual_c,
    aom_lpf_vertical_14_dual_c },
};
static const lpf_fn lpf_quad[2][4] = {
  { aom_lpf_horizontal_4_quad_c, aom_lpf_horizontal_6_quad_c, aom_lpf_horizontal_8_quad_c,
    aom_lpf_horizontal_14_quad_c },
  { aom_lpf_vertical_4_quad_c, aom_lpf_vertical_6_quad_c, aom_lpf_vertical_8_quad_c,
    aom_lpf_vertical_14_quad_c },
};

EXPORT void aomshim_e4_lpf(int kind, uint8_t *s, int pitch, const uint8_t *b0, const uint8_t *l0,
                           const uint8_t *t0, const uint8_t *b1, const uint8_t *l1,
                           const uint8_t *t1) {
  const int dir = kind / 12, size = (kind % 12) / 3, variant = kind % 3;
  if (variant == 0)
    lpf_single[dir][size](s, pitch, b0, l0, t0);
  else if (variant == 1)
    lpf_dual[dir][size](s, pitch, b0, l0, t0, b1, l1, t1);
  else
    lpf_quad[dir][size](s, pitch, b0, l0, t0);
}

/* ---- av1/common/av1_loopfilter.c ----------------------------------------------------------- */

static void copy_lfthr(const AV1_COMMON *cm, uint8_t *out) {
  for (int l = 0; l <= MAX_LOOP_FILTER; l++) {
    memcpy(out + l * 48, cm->lf_info.lfthr[l].mblim, SIMD_WIDTH);
    memcpy(out + l * 48 + 16, cm->lf_info.lfthr[l].lim, SIMD_WIDTH);
    memcpy(out + l * 48 + 32, cm->lf_info.lfthr[l].hev_thr, SIMD_WIDTH);
  }
}

/* av1_loop_filter_init: out = 64 levels x {mblim[16], lim[16], hev_thr[16]}. */
EXPORT void aomshim_e4_lf_init(int sharpness, uint8_t *out) {
  AV1_COMMON *cm = calloc(1, sizeof(*cm));
  cm->lf.sharpness_level = sharpness;
  av1_loop_filter_init(cm);
  copy_lfthr(cm, out);
  free(cm);
}

/* av1_loop_filter_frame_init. levels = {filter_level[0], filter_level[1], u, v, sharpness};
   ref_deltas[8], mode_deltas[2]; seg_data[8 * 8], seg_mask[8].
   Outputs lvl[3][8][2][8][2] and the thresholds (as aomshim_e4_lf_init). */
EXPORT void aomshim_e4_lf_frame_init(const int *levels, int mode_ref_delta_enabled,
                                     const int8_t *ref_deltas, const int8_t *mode_deltas,
                                     int seg_enabled, const int16_t *seg_data,
                                     const uint32_t *seg_mask, int plane_start, int plane_end,
                                     uint8_t *lvl, uint8_t *thr) {
  AV1_COMMON *cm = calloc(1, sizeof(*cm));
  cm->lf.filter_level[0] = levels[0];
  cm->lf.filter_level[1] = levels[1];
  cm->lf.filter_level_u = levels[2];
  cm->lf.filter_level_v = levels[3];
  cm->lf.sharpness_level = levels[4];
  cm->lf.mode_ref_delta_enabled = (uint8_t)mode_ref_delta_enabled;
  memcpy(cm->lf.ref_deltas, ref_deltas, REF_FRAMES);
  memcpy(cm->lf.mode_deltas, mode_deltas, MAX_MODE_LF_DELTAS);
  cm->seg.enabled = (uint8_t)seg_enabled;
  for (int s = 0; s < MAX_SEGMENTS; s++) {
    cm->seg.feature_mask[s] = seg_mask[s];
    for (int f = 0; f < SEG_LVL_MAX; f++) cm->seg.feature_data[s][f] = seg_data[s * 8 + f];
  }
  av1_loop_filter_frame_init(cm, plane_start, plane_end);
  memcpy(lvl, cm->lf_info.lvl, sizeof(cm->lf_info.lvl));
  copy_lfthr(cm, thr);
  free(cm);
}

/* Frame-level deblocking of an 8-bit 4:2:0 frame through av1_loop_filter_frame_mt (one worker,
   i.e. loop_filter_rows) with lpf_opt_level 2, as the realtime encoder runs it.
   Mode info: per-mi index into the block arrays (mi_rows * mi_cols, mi_stride == mi_cols).
   Block arrays: bsize, tx_size, skip_txfm, ref_frame0, mode, segment_id; inter_tx_size is set to
   tx_size for every entry. levels = {filter_level[0], [1], u, v, sharpness, mode_ref_delta_enabled}.
   Planes are filtered in place; y has stride ys, u/v stride uvs. */
EXPORT void aomshim_e4_lf_frame(int mi_rows, int mi_cols, int sb128, const int *mi_block,
                                int nblocks, const uint8_t *bsize, const uint8_t *tx_size,
                                const uint8_t *skip_txfm, const int8_t *ref_frame0,
                                const uint8_t *mode, const uint8_t *segment_id,
                                const int *levels, const int8_t *ref_deltas,
                                const int8_t *mode_deltas, uint8_t *y, int ys, uint8_t *u,
                                uint8_t *v, int uvs, int plane_start, int plane_end) {
  AV1_COMMON *cm = calloc(1, sizeof(*cm));
  SequenceHeader *seq = calloc(1, sizeof(*seq));
  MACROBLOCKD *xd = calloc(1, sizeof(*xd));
  MB_MODE_INFO *blocks = calloc(nblocks, sizeof(*blocks));
  MB_MODE_INFO **grid = calloc((size_t)mi_rows * mi_cols, sizeof(*grid));
  YV12_BUFFER_CONFIG *frame = calloc(1, sizeof(*frame));

  seq->sb_size = sb128 ? BLOCK_128X128 : BLOCK_64X64;
  seq->mib_size = mi_size_wide[seq->sb_size];
  seq->mib_size_log2 = mi_size_wide_log2[seq->sb_size];
  seq->bit_depth = AOM_BITS_8;
  seq->use_highbitdepth = 0;
  seq->monochrome = 0;
  seq->subsampling_x = seq->subsampling_y = 1;
  cm->seq_params = seq;

  for (int i = 0; i < nblocks; i++) {
    MB_MODE_INFO *m = &blocks[i];
    m->bsize = bsize[i];
    m->tx_size = tx_size[i];
    m->skip_txfm = skip_txfm[i];
    m->ref_frame[0] = ref_frame0[i];
    m->ref_frame[1] = NONE_FRAME;
    m->mode = mode[i];
    m->segment_id = segment_id[i];
    for (int k = 0; k < INTER_TX_SIZE_BUF_LEN; k++) m->inter_tx_size[k] = tx_size[i];
  }
  for (int i = 0; i < mi_rows * mi_cols; i++) grid[i] = &blocks[mi_block[i]];
  cm->mi_params.mi_rows = mi_rows;
  cm->mi_params.mi_cols = mi_cols;
  cm->mi_params.mi_stride = mi_cols;
  cm->mi_params.mi_grid_base = grid;

  cm->lf.filter_level[0] = levels[0];
  cm->lf.filter_level[1] = levels[1];
  cm->lf.filter_level_u = levels[2];
  cm->lf.filter_level_v = levels[3];
  cm->lf.sharpness_level = levels[4];
  cm->lf.mode_ref_delta_enabled = (uint8_t)levels[5];
  memcpy(cm->lf.ref_deltas, ref_deltas, REF_FRAMES);
  memcpy(cm->lf.mode_deltas, mode_deltas, MAX_MODE_LF_DELTAS);

  for (int p = 0; p < MAX_MB_PLANE; p++) {
    xd->plane[p].subsampling_x = p ? 1 : 0;
    xd->plane[p].subsampling_y = p ? 1 : 0;
  }

  frame->y_buffer = y;
  frame->u_buffer = u;
  frame->v_buffer = v;
  frame->y_stride = ys;
  frame->uv_stride = uvs;
  frame->y_crop_width = frame->y_width = mi_cols * 4;
  frame->y_crop_height = frame->y_height = mi_rows * 4;
  frame->uv_crop_width = frame->uv_width = mi_cols * 2;
  frame->uv_crop_height = frame->uv_height = mi_rows * 2;

  av1_loop_filter_init(cm);
  av1_loop_filter_frame_mt(frame, cm, xd, plane_start, plane_end, 0, NULL, 1, NULL, 2);

  free(frame);
  free(grid);
  free(blocks);
  free(xd);
  free(seq);
  free(cm);
}

/* ---- av1/common/cdef_block.c, cdef.c ------------------------------------------------------- */

EXPORT int aomshim_e4_cdef_find_dir(const uint16_t *img, int stride, int32_t *var,
                                    int coeff_shift) {
  return cdef_find_dir_c(img, stride, var, coeff_shift);
}

EXPORT void aomshim_e4_cdef_find_dir_dual(const uint16_t *img1, const uint16_t *img2,
                                          int stride, int32_t *var1, int32_t *var2,
                                          int coeff_shift, int *out1, int *out2) {
  cdef_find_dir_dual_c(img1, img2, stride, var1, var2, coeff_shift, out1, out2);
}

EXPORT void aomshim_e4_cdef_filter_8(int variant, uint8_t *dst, int dstride, const uint16_t *in,
                                     int pri_strength, int sec_strength, int dir,
                                     int pri_damping, int sec_damping, int coeff_shift,
                                     int block_width, int block_height) {
  static const cdef_filter_block_func fns[4] = { cdef_filter_8_0_c, cdef_filter_8_1_c,
                                                 cdef_filter_8_2_c, cdef_filter_8_3_c };
  fns[variant](dst, dstride, in, pri_strength, sec_strength, dir, pri_damping, sec_damping,
               coeff_shift, block_width, block_height);
}

EXPORT void aomshim_e4_cdef_copy_rect8(uint16_t *dst, int dstride, const uint8_t *src,
                                       int sstride, int width, int height) {
  cdef_copy_rect8_8bit_to_16bit_c(dst, dstride, src, sstride, width, height);
}

/* av1_cdef_filter_fb into an 8-bit destination. dir/var are int[16][16]; dirinit may be NULL;
   dlist holds cdef_count (by, bx) byte pairs. */
EXPORT void aomshim_e4_cdef_filter_fb(uint8_t *dst8, int dstride, const uint16_t *in, int xdec,
                                      int ydec, int *dir, int *dirinit, int *var, int pli,
                                      const uint8_t *dlist, int cdef_count, int level,
                                      int sec_strength, int damping, int coeff_shift) {
  cdef_list list[MI_SIZE_128X128 * MI_SIZE_128X128];
  for (int i = 0; i < cdef_count; i++) {
    list[i].by = dlist[2 * i];
    list[i].bx = dlist[2 * i + 1];
  }
  av1_cdef_filter_fb(dst8, NULL, dstride, in, xdec, ydec, (int(*)[CDEF_NBLOCKS])dir, dirinit,
                     (int(*)[CDEF_NBLOCKS])var, pli, list, cdef_count, level, sec_strength,
                     damping, coeff_shift);
}

/* av1_cdef_compute_sb_list over a skip_txfm map (one MB_MODE_INFO per mi). */
EXPORT int aomshim_e4_cdef_compute_sb_list(int mi_rows, int mi_cols, int mi_stride,
                                           const uint8_t *skip, int mi_row, int mi_col, int bs,
                                           uint8_t *dlist_out) {
  MB_MODE_INFO *mbmi = calloc((size_t)mi_rows * mi_stride, sizeof(*mbmi));
  MB_MODE_INFO **grid = calloc((size_t)mi_rows * mi_stride, sizeof(*grid));
  for (int i = 0; i < mi_rows * mi_stride; i++) {
    mbmi[i].skip_txfm = skip[i];
    grid[i] = &mbmi[i];
  }
  CommonModeInfoParams mp;
  memset(&mp, 0, sizeof(mp));
  mp.mi_rows = mi_rows;
  mp.mi_cols = mi_cols;
  mp.mi_stride = mi_stride;
  mp.mi_grid_base = grid;
  cdef_list dlist[MI_SIZE_128X128 * MI_SIZE_128X128];
  const int n = av1_cdef_compute_sb_list(&mp, mi_row, mi_col, dlist, (BLOCK_SIZE)bs);
  for (int i = 0; i < n; i++) {
    dlist_out[2 * i] = dlist[i].by;
    dlist_out[2 * i + 1] = dlist[i].bx;
  }
  free(grid);
  free(mbmi);
  return n;
}

/* Frame-level CDEF of an 8-bit 4:2:0 frame (64x64 superblocks) through av1_cdef_frame with
   av1_cdef_init_fb_row (the single-threaded path). One MB_MODE_INFO per mi carries skip_txfm and
   cdef_strength (read at each 64x64 top-left). info = {cdef_damping, nb_cdef_strengths,
   cdef_bits}; strengths / uv_strengths: 8 each. Planes are filtered in place; the strides must
   cover the 16-aligned width (the line buffers copy that many pixels). */
EXPORT void aomshim_e4_cdef_frame(int mi_rows, int mi_cols, const uint8_t *skip,
                                  const int8_t *strength, const int *info, const int *strengths,
                                  const int *uv_strengths, uint8_t *y, int ys, uint8_t *u,
                                  uint8_t *v, int uvs) {
  AV1_COMMON *cm = calloc(1, sizeof(*cm));
  SequenceHeader *seq = calloc(1, sizeof(*seq));
  MACROBLOCKD *xd = calloc(1, sizeof(*xd));
  MB_MODE_INFO *mbmi = calloc((size_t)mi_rows * mi_cols, sizeof(*mbmi));
  MB_MODE_INFO **grid = calloc((size_t)mi_rows * mi_cols, sizeof(*grid));
  YV12_BUFFER_CONFIG *frame = calloc(1, sizeof(*frame));

  seq->sb_size = BLOCK_64X64;
  seq->mib_size = 16;
  seq->mib_size_log2 = 4;
  seq->bit_depth = AOM_BITS_8;
  seq->use_highbitdepth = 0;
  seq->monochrome = 0;
  seq->enable_cdef = 1;
  seq->subsampling_x = seq->subsampling_y = 1;
  cm->seq_params = seq;

  for (int i = 0; i < mi_rows * mi_cols; i++) {
    mbmi[i].bsize = BLOCK_64X64;
    mbmi[i].skip_txfm = skip[i];
    mbmi[i].cdef_strength = strength[i];
    grid[i] = &mbmi[i];
  }
  cm->mi_params.mi_rows = mi_rows;
  cm->mi_params.mi_cols = mi_cols;
  cm->mi_params.mi_stride = mi_cols;
  cm->mi_params.mi_grid_base = grid;

  CdefInfo *ci = &cm->cdef_info;
  ci->cdef_damping = info[0];
  ci->nb_cdef_strengths = info[1];
  ci->cdef_bits = info[2];
  for (int i = 0; i < CDEF_MAX_STRENGTHS; i++) {
    ci->cdef_strengths[i] = strengths[i];
    ci->cdef_uv_strengths[i] = uv_strengths[i];
  }
  /* Buffer sizes of av1_alloc_cdef_buffers with one worker. */
  const int luma_stride = ALIGN_POWER_OF_TWO(mi_cols << MI_SIZE_LOG2, 4);
  ci->srcbuf = calloc(CDEF_INBUF_SIZE, sizeof(uint16_t));
  for (int p = 0; p < MAX_MB_PLANE; p++) {
    const int shift = p ? 1 : 0;
    ci->linebuf[p] = calloc((size_t)3 * (CDEF_VBORDER << 1) * (luma_stride >> shift),
                            sizeof(uint16_t));
    ci->colbuf[p] = calloc((size_t)(CDEF_BLOCKSIZE << (MI_SIZE_LOG2 - shift)) * 2 *
                               CDEF_VBORDER * CDEF_HBORDER,
                           sizeof(uint16_t));
    xd->plane[p].subsampling_x = shift;
    xd->plane[p].subsampling_y = shift;
  }

  frame->y_buffer = y;
  frame->u_buffer = u;
  frame->v_buffer = v;
  frame->y_stride = ys;
  frame->uv_stride = uvs;
  frame->y_crop_width = frame->y_width = mi_cols * 4;
  frame->y_crop_height = frame->y_height = mi_rows * 4;
  frame->uv_crop_width = frame->uv_width = mi_cols * 2;
  frame->uv_crop_height = frame->uv_height = mi_rows * 2;

  av1_cdef_frame(frame, cm, xd, av1_cdef_init_fb_row);

  for (int p = 0; p < MAX_MB_PLANE; p++) {
    free(ci->linebuf[p]);
    free(ci->colbuf[p]);
  }
  free(ci->srcbuf);
  free(frame);
  free(grid);
  free(mbmi);
  free(xd);
  free(seq);
  free(cm);
}

/* ---- av1/encoder/pickcdef.c, picklpf.c ----------------------------------------------------- */

/* av1_cdef_search (single-threaded MSE pass) on a minimal AV1_COMP: an 8-bit 4:2:0 frame with
   one MB_MODE_INFO per mi (skip_txfm from `skip`; bsize BLOCK_128X128 everywhere when sb128,
   else BLOCK_64X64), source planes src_* and reconstruction rec_* (same strides).
   out = {cdef_bits, nb_cdef_strengths, cdef_damping, cdef_strengths[16], cdef_uv_strengths[16]};
   strength_out = cdef_strength of every mi. */
EXPORT void aomshim_e4_cdef_search(int mi_rows, int mi_cols, int sb128, const uint8_t *skip,
                                   int qindex, int pick_method, int rdmult, uint8_t *src_y,
                                   uint8_t *src_u, uint8_t *src_v, uint8_t *rec_y,
                                   uint8_t *rec_u, uint8_t *rec_v, int ys, int uvs, int *out,
                                   int8_t *strength_out) {
  AV1_COMP *cpi = calloc(1, sizeof(*cpi));
  AV1_PRIMARY *ppi = calloc(1, sizeof(*ppi));
  SequenceHeader *seq = calloc(1, sizeof(*seq));
  struct aom_internal_error_info *error = calloc(1, sizeof(*error));
  RefCntBuffer *cur = calloc(1, sizeof(*cur));
  YV12_BUFFER_CONFIG *source = calloc(1, sizeof(*source));
  MB_MODE_INFO *mbmi = calloc((size_t)mi_rows * mi_cols, sizeof(*mbmi));
  MB_MODE_INFO **grid = calloc((size_t)mi_rows * mi_cols, sizeof(*grid));
  AV1_COMMON *cm = &cpi->common;

  seq->sb_size = sb128 ? BLOCK_128X128 : BLOCK_64X64;
  seq->mib_size = mi_size_wide[seq->sb_size];
  seq->mib_size_log2 = mi_size_wide_log2[seq->sb_size];
  seq->bit_depth = AOM_BITS_8;
  seq->use_highbitdepth = 0;
  seq->monochrome = 0;
  seq->enable_cdef = 1;
  seq->subsampling_x = seq->subsampling_y = 1;
  cpi->ppi = ppi;
  cm->seq_params = seq;
  cm->error = error;
  cm->quant_params.base_qindex = qindex;

  for (int i = 0; i < mi_rows * mi_cols; i++) {
    mbmi[i].bsize = sb128 ? BLOCK_128X128 : BLOCK_64X64;
    mbmi[i].skip_txfm = skip[i];
    grid[i] = &mbmi[i];
  }
  cm->mi_params.mi_rows = mi_rows;
  cm->mi_params.mi_cols = mi_cols;
  cm->mi_params.mi_stride = mi_cols;
  cm->mi_params.mi_grid_base = grid;

  YV12_BUFFER_CONFIG *bufs[2] = { &cur->buf, source };
  uint8_t *planes[2][3] = { { rec_y, rec_u, rec_v }, { src_y, src_u, src_v } };
  for (int b = 0; b < 2; b++) {
    YV12_BUFFER_CONFIG *f = bufs[b];
    f->y_buffer = planes[b][0];
    f->u_buffer = planes[b][1];
    f->v_buffer = planes[b][2];
    f->y_stride = ys;
    f->uv_stride = uvs;
    f->y_crop_width = f->y_width = mi_cols * 4;
    f->y_crop_height = f->y_height = mi_rows * 4;
    f->uv_crop_width = f->uv_width = mi_cols * 2;
    f->uv_crop_height = f->uv_height = mi_rows * 2;
  }
  cm->cur_frame = cur;
  cpi->source = source;
  for (int p = 0; p < MAX_MB_PLANE; p++) {
    cpi->td.mb.e_mbd.plane[p].subsampling_x = p ? 1 : 0;
    cpi->td.mb.e_mbd.plane[p].subsampling_y = p ? 1 : 0;
  }
  cpi->td.mb.rdmult = rdmult;
  cpi->oxcf.tool_cfg.cdef_control = CDEF_ALL;
  cpi->sf.lpf_sf.cdef_pick_method = (CDEF_PICK_METHOD)pick_method;
  cpi->mt_info.num_mod_workers[MOD_CDEF_SEARCH] = 1;

  av1_cdef_search(cpi);

  const CdefInfo *ci = &cm->cdef_info;
  out[0] = ci->cdef_bits;
  out[1] = ci->nb_cdef_strengths;
  out[2] = ci->cdef_damping;
  for (int i = 0; i < CDEF_MAX_STRENGTHS; i++) {
    out[3 + i] = ci->cdef_strengths[i];
    out[3 + CDEF_MAX_STRENGTHS + i] = ci->cdef_uv_strengths[i];
  }
  for (int i = 0; i < mi_rows * mi_cols; i++) strength_out[i] = mbmi[i].cdef_strength;

  aom_free(cpi->cdef_search_ctx); /* allocated by av1_cdef_search with aom_calloc */
  free(grid);
  free(mbmi);
  free(source);
  free(cur);
  free(error);
  free(seq);
  free(ppi);
  free(cpi);
}

/* av1_pick_cdef_from_qp (8-bit, no mode-info grid). out = {cdef_bits, nb_cdef_strengths,
   cdef_damping, cdef_strengths[0], [1], cdef_uv_strengths[0], [1]}. */
EXPORT void aomshim_e4_pick_cdef_from_qp(int qindex, int frame_type, int skip_cdef,
                                         int avoid_uv_cdef, int *out) {
  AV1_COMMON *cm = calloc(1, sizeof(*cm));
  SequenceHeader *seq = calloc(1, sizeof(*seq));
  seq->bit_depth = AOM_BITS_8;
  cm->seq_params = seq;
  cm->quant_params.base_qindex = qindex;
  cm->current_frame.frame_type = (FRAME_TYPE)frame_type;
  av1_pick_cdef_from_qp(cm, skip_cdef, 0, avoid_uv_cdef != 0);
  out[0] = cm->cdef_info.cdef_bits;
  out[1] = cm->cdef_info.nb_cdef_strengths;
  out[2] = cm->cdef_info.cdef_damping;
  out[3] = cm->cdef_info.cdef_strengths[0];
  out[4] = cm->cdef_info.cdef_strengths[1];
  out[5] = cm->cdef_info.cdef_uv_strengths[0];
  out[6] = cm->cdef_info.cdef_uv_strengths[1];
  free(seq);
  free(cm);
}

/* av1_pick_filter_level with LPF_PICK_FROM_Q-or-MINIMAL methods on a minimal AV1_COMP.
   p = {qindex, frame_type, mode, tuning, sharpness, enable_adaptive_sharpness,
        loopfilter_control, use_nonrd_pick_mode, use_fast_fixed_part, number_temporal_layers,
        temporal_layer_id, width, height, frame_source_sad, method, pass,
        section_intra_rating, non_reference_frame}.
   out = {filter_level[0], filter_level[1], filter_level_u, filter_level_v, sharpness_level}. */
EXPORT void aomshim_e4_pick_filter_level(const int *p, int *out) {
  AV1_COMP *cpi = calloc(1, sizeof(*cpi));
  AV1_PRIMARY *ppi = calloc(1, sizeof(*ppi));
  SequenceHeader *seq = calloc(1, sizeof(*seq));
  seq->bit_depth = AOM_BITS_8;
  cpi->ppi = ppi;
  cpi->common.seq_params = seq;
  cpi->common.quant_params.base_qindex = p[0];
  cpi->common.current_frame.frame_type = (FRAME_TYPE)p[1];
  cpi->oxcf.mode = (MODE)p[2];
  cpi->oxcf.tune_cfg.tuning = (aom_tune_metric)p[3];
  cpi->oxcf.algo_cfg.sharpness = p[4];
  cpi->oxcf.algo_cfg.enable_adaptive_sharpness = p[5] != 0;
  cpi->oxcf.algo_cfg.loopfilter_control = (LOOPFILTER_CONTROL)p[6];
  cpi->sf.rt_sf.use_nonrd_pick_mode = p[7];
  cpi->sf.rt_sf.use_fast_fixed_part = p[8];
  cpi->svc.number_temporal_layers = p[9];
  cpi->svc.temporal_layer_id = p[10];
  cpi->common.width = p[11];
  cpi->common.height = p[12];
  cpi->rc.frame_source_sad = (uint64_t)p[13];
  cpi->oxcf.pass = (enum aom_enc_pass)p[15];
  ppi->twopass.section_intra_rating = p[16];
  ppi->rtc_ref.non_reference_frame = p[17];
  /* Sentinels so that untouched outputs are visible. */
  cpi->common.lf.filter_level[0] = cpi->common.lf.filter_level[1] = -1;
  cpi->common.lf.filter_level_u = cpi->common.lf.filter_level_v = -1;
  av1_pick_filter_level(NULL, cpi, (LPF_PICK_METHOD)p[14]);
  out[0] = cpi->common.lf.filter_level[0];
  out[1] = cpi->common.lf.filter_level[1];
  out[2] = cpi->common.lf.filter_level_u;
  out[3] = cpi->common.lf.filter_level_v;
  out[4] = cpi->common.lf.sharpness_level;
  free(seq);
  free(ppi);
  free(cpi);
}

EXPORT int aomshim_e4_ac_quant_QTX(int qindex, int delta, int bit_depth) {
  return av1_ac_quant_QTX(qindex, delta, (aom_bit_depth_t)bit_depth);
}
