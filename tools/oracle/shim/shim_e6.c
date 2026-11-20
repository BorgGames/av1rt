/* e6 (motion) shim: wrappers around libaom's mcomp.c / encodemv.c / mv_prec.c so the C# port
 * can be tested against them (docs/TESTING.md). Plain C types and pointers only. */
#include <stdlib.h>
#include <string.h>

#include "config/aom_dsp_rtcd.h"
#include "aom_dsp/bitwriter.h"
#include "av1/common/av1_common_int.h"
#include "av1/common/entropymv.h"
#include "av1/encoder/block.h"
#include "av1/encoder/encodemv.h"
#include "av1/encoder/encoder.h"
#include "av1/encoder/mcomp.h"

#define EXPORT __declspec(dllexport)

/* ---- MV entropy context ------------------------------------------------------------ */

EXPORT int e6_nmvc_size(void) { return (int)(sizeof(nmv_context) / sizeof(aom_cdf_prob)); }

/* Writes libaom's default nmv_context (av1_init_mv_probs) as a flat aom_cdf_prob array. */
EXPORT int e6_default_nmvc(uint16_t *out, int n) {
  if (n * sizeof(aom_cdf_prob) != sizeof(nmv_context)) return -1;
  AV1_COMMON *cm = (AV1_COMMON *)calloc(1, sizeof(*cm));
  FRAME_CONTEXT *fc = (FRAME_CONTEXT *)calloc(1, sizeof(*fc));
  cm->fc = fc;
  av1_init_mv_probs(cm);
  memcpy(out, &fc->nmvc, sizeof(nmv_context));
  free(fc);
  free(cm);
  return 0;
}

/* mvcost0/mvcost1 are full MV_VALS arrays (the C pointers point at their middle). */
EXPORT void e6_build_nmv_cost_table(const uint16_t *nmvc_flat, int precision, int *mvjoint,
                                    int *mvcost0, int *mvcost1) {
  nmv_context ctx;
  memcpy(&ctx, nmvc_flat, sizeof(ctx));
  int *mvcost[2] = { mvcost0 + MV_MAX, mvcost1 + MV_MAX };
  av1_build_nmv_cost_table(mvjoint, mvcost, &ctx, (MvSubpelPrecision)precision);
}

EXPORT int e6_mv_bit_cost(int mv_row, int mv_col, int ref_row, int ref_col,
                          const int *mvjcost, int *mvcost0, int *mvcost1, int weight) {
  const MV mv = { (int16_t)mv_row, (int16_t)mv_col };
  const MV ref = { (int16_t)ref_row, (int16_t)ref_col };
  int *mvcost[2] = { mvcost0 + MV_MAX, mvcost1 + MV_MAX };
  return av1_mv_bit_cost(&mv, &ref, mvjcost, mvcost, weight);
}

/* Encodes count MVs (mvs/refs are row,col pairs) with av1_encode_mv into out. The context is
 * updated in place (nmvc_flat). Returns the byte count, or -1. */
EXPORT int e6_encode_mvs(const int16_t *mvs, const int16_t *refs, int count, int usehp,
                         int allow_update_cdf, uint16_t *nmvc_flat, uint8_t *out, int cap,
                         int *max_mv_magnitude) {
  AV1_COMP *cpi = (AV1_COMP *)calloc(1, sizeof(*cpi));
  ThreadData *td = (ThreadData *)calloc(1, sizeof(*td));
  cpi->common.features.cur_frame_force_integer_mv = 0;
  cpi->sf.mv_sf.auto_mv_step_size = 1;
  td->max_mv_magnitude = 0;
  nmv_context ctx;
  memcpy(&ctx, nmvc_flat, sizeof(ctx));
  aom_writer w;
  memset(&w, 0, sizeof(w));
  aom_start_encode(&w, out, (size_t)cap);
  w.allow_update_cdf = (uint8_t)allow_update_cdf;
  for (int i = 0; i < count; ++i) {
    const MV mv = { mvs[2 * i], mvs[2 * i + 1] };
    const MV ref = { refs[2 * i], refs[2 * i + 1] };
    av1_encode_mv(cpi, &w, td, &mv, &ref, &ctx, usehp);
  }
  int ret = aom_stop_encode(&w) < 0 ? -1 : (int)w.pos;
  memcpy(nmvc_flat, &ctx, sizeof(ctx));
  *max_mv_magnitude = td->max_mv_magnitude;
  free(td);
  free(cpi);
  return ret;
}

/* ---- ref MV stack ------------------------------------------------------------------- */

/* stack_this[USABLE_REF_MV_STACK_SIZE] / global_mv are int_mv.as_int values for ref_frame. */
EXPORT void e6_find_best_ref_mvs(int allow_hp, int ref_frame, int is_integer,
                                 const uint32_t *stack_this, int count, uint32_t global_mv,
                                 uint32_t *out_nearest, uint32_t *out_near) {
  MB_MODE_INFO_EXT *ext = (MB_MODE_INFO_EXT *)calloc(1, sizeof(*ext));
  for (int i = 0; i < USABLE_REF_MV_STACK_SIZE; ++i)
    ext->ref_mv_stack[ref_frame][i].this_mv.as_int = stack_this[i];
  ext->ref_mv_count[ref_frame] = (uint8_t)count;
  ext->global_mvs[ref_frame].as_int = global_mv;
  int_mv nearest, near_mv;
  av1_find_best_ref_mvs_from_stack(allow_hp, ext, (MV_REFERENCE_FRAME)ref_frame, &nearest,
                                   &near_mv, is_integer);
  *out_nearest = nearest.as_int;
  *out_near = near_mv.as_int;
  free(ext);
}

/* stack_this / stack_comp: [MODE_CTX_REF_FRAMES][USABLE_REF_MV_STACK_SIZE] as_int values,
 * counts[MODE_CTX_REF_FRAMES], globals[REF_FRAMES]. */
EXPORT uint32_t e6_get_ref_mv_from_stack(int ref_idx, int rf0, int rf1, int ref_mv_idx,
                                         const uint32_t *stack_this, const uint32_t *stack_comp,
                                         const uint8_t *counts, const uint32_t *globals) {
  MB_MODE_INFO_EXT *ext = (MB_MODE_INFO_EXT *)calloc(1, sizeof(*ext));
  for (int t = 0; t < MODE_CTX_REF_FRAMES; ++t) {
    for (int i = 0; i < USABLE_REF_MV_STACK_SIZE; ++i) {
      ext->ref_mv_stack[t][i].this_mv.as_int = stack_this[t * USABLE_REF_MV_STACK_SIZE + i];
      ext->ref_mv_stack[t][i].comp_mv.as_int = stack_comp[t * USABLE_REF_MV_STACK_SIZE + i];
    }
    ext->ref_mv_count[t] = counts[t];
  }
  for (int t = 0; t < REF_FRAMES; ++t) ext->global_mvs[t].as_int = globals[t];
  const MV_REFERENCE_FRAME ref_frame[2] = { (MV_REFERENCE_FRAME)rf0, (MV_REFERENCE_FRAME)rf1 };
  const int_mv r = av1_get_ref_mv_from_stack(ref_idx, ref_frame, ref_mv_idx, ext);
  free(ext);
  return r.as_int;
}

/* ---- search site configs ------------------------------------------------------------ */

/* Serializes a search_site_config: num_search_steps, stride, searches_per_step[22],
 * radius[22], then site[22][17] as (row, col, offset) triples. */
#define E6_SS_STEPS (MAX_MVSEARCH_STEPS * 2)
#define E6_SS_SITES (16 + 1)
EXPORT int e6_search_site_ints(void) { return 2 + 2 * E6_SS_STEPS + 3 * E6_SS_STEPS * E6_SS_SITES; }

static void serialize_cfg(const search_site_config *cfg, int *out) {
  int k = 0;
  out[k++] = cfg->num_search_steps;
  out[k++] = cfg->stride;
  for (int i = 0; i < E6_SS_STEPS; ++i) out[k++] = cfg->searches_per_step[i];
  for (int i = 0; i < E6_SS_STEPS; ++i) out[k++] = cfg->radius[i];
  for (int i = 0; i < E6_SS_STEPS; ++i) {
    for (int j = 0; j < E6_SS_SITES; ++j) {
      out[k++] = cfg->site[i][j].mv.row;
      out[k++] = cfg->site[i][j].mv.col;
      out[k++] = cfg->site[i][j].offset;
    }
  }
}

/* method in [0, NUM_DISTINCT_SEARCH_METHODS): av1_init_motion_compensation[method];
 * method == -1: av1_init_motion_fpf. The config starts zeroed. */
EXPORT void e6_init_search_site(int method, int stride, int level, int *out) {
  search_site_config *cfg = (search_site_config *)calloc(1, sizeof(*cfg));
  if (method < 0)
    av1_init_motion_fpf(cfg, stride);
  else
    av1_init_motion_compensation[method](cfg, stride, level);
  serialize_cfg(cfg, out);
  free(cfg);
}

EXPORT int e6_init_search_range(int size) { return av1_init_search_range(size); }

/* ---- variance function table (libaom's own C kernels) --------------------------------- */

static aom_variance_fn_ptr_t g_fn[BLOCK_SIZES_ALL];
static int g_fn_init = 0;

#define E6_FN(BT, W, H)                                \
  g_fn[BT].sdf = aom_sad##W##x##H##_c;                 \
  g_fn[BT].sdx4df = aom_sad##W##x##H##x4d_c;           \
  g_fn[BT].sdx3df = aom_sad##W##x##H##x3d_c;           \
  g_fn[BT].vf = aom_variance##W##x##H##_c;             \
  g_fn[BT].svf = aom_sub_pixel_variance##W##x##H##_c;

#define E6_FN_SKIP(BT, W, H)                             g_fn[BT].sdsf = aom_sad_skip_##W##x##H##_c;            g_fn[BT].sdsx4df = aom_sad_skip_##W##x##H##x4d_c;

static void init_fn(void) {
  if (g_fn_init) return;
  E6_FN(BLOCK_8X8, 8, 8)
  E6_FN(BLOCK_8X16, 8, 16)
  E6_FN(BLOCK_16X8, 16, 8)
  E6_FN(BLOCK_16X16, 16, 16)
  E6_FN(BLOCK_16X32, 16, 32)
  E6_FN(BLOCK_32X16, 32, 16)
  E6_FN(BLOCK_32X32, 32, 32)
  E6_FN(BLOCK_64X64, 64, 64)
  E6_FN_SKIP(BLOCK_8X16, 8, 16)
  E6_FN_SKIP(BLOCK_16X16, 16, 16)
  E6_FN_SKIP(BLOCK_16X32, 16, 32)
  E6_FN_SKIP(BLOCK_32X16, 32, 16)
  E6_FN_SKIP(BLOCK_32X32, 32, 32)
  E6_FN_SKIP(BLOCK_64X64, 64, 64)
  g_fn_init = 1;
}

/* Kernel entry points for the C# side's vfp delegates (buffers are array + offset). */
EXPORT unsigned int e6_sdf(int bsize, const uint8_t *a, int a_off, int a_stride,
                           const uint8_t *b, int b_off, int b_stride) {
  init_fn();
  return g_fn[bsize].sdf(a + a_off, a_stride, b + b_off, b_stride);
}

EXPORT void e6_sdxnd(int bsize, int n, const uint8_t *a, int a_off, int a_stride,
                     const uint8_t *b, const int *b_offs, int b_stride, uint32_t *sads) {
  init_fn();
  const uint8_t *refs[4] = { b + b_offs[0], b + b_offs[1], b + b_offs[2], b + b_offs[3] };
  if (n == 4)
    g_fn[bsize].sdx4df(a + a_off, a_stride, refs, b_stride, sads);
  else
    g_fn[bsize].sdx3df(a + a_off, a_stride, refs, b_stride, sads);
}

EXPORT unsigned int e6_sdsf(int bsize, const uint8_t *a, int a_off, int a_stride,
                            const uint8_t *b, int b_off, int b_stride) {
  init_fn();
  return g_fn[bsize].sdsf(a + a_off, a_stride, b + b_off, b_stride);
}

EXPORT void e6_sdsx4d(int bsize, const uint8_t *a, int a_off, int a_stride,
                      const uint8_t *b, const int *b_offs, int b_stride, uint32_t *sads) {
  init_fn();
  const uint8_t *refs[4] = { b + b_offs[0], b + b_offs[1], b + b_offs[2], b + b_offs[3] };
  g_fn[bsize].sdsx4df(a + a_off, a_stride, refs, b_stride, sads);
}

EXPORT unsigned int e6_vf(int bsize, const uint8_t *a, int a_off, int a_stride,
                          const uint8_t *b, int b_off, int b_stride, unsigned int *sse) {
  init_fn();
  return g_fn[bsize].vf(a + a_off, a_stride, b + b_off, b_stride, sse);
}

EXPORT unsigned int e6_svf(int bsize, const uint8_t *a, int a_off, int a_stride, int xoff,
                           int yoff, const uint8_t *b, int b_off, int b_stride,
                           unsigned int *sse) {
  init_fn();
  return g_fn[bsize].svf(a + a_off, a_stride, xoff, yoff, b + b_off, b_stride, sse);
}

/* ---- full-pel search ------------------------------------------------------------------ */

static search_site_config g_ss[NUM_DISTINCT_SEARCH_METHODS];

static void setup_cost_params(MV_COST_PARAMS *p, const MV *ref_mv, const int *mvjcost,
                              int *mvcost0, int *mvcost1, int errorperbit, int sadperbit) {
  p->ref_mv = ref_mv;
  p->full_ref_mv = get_fullmv_from_mv(ref_mv);
  p->mv_cost_type = MV_COST_ENTROPY;
  p->error_per_bit = errorperbit;
  p->sad_per_bit = sadperbit;
  p->mvjcost = mvjcost;
  p->mvcost[0] = mvcost0 + MV_MAX;
  p->mvcost[1] = mvcost1 + MV_MAX;
}

/* in_ints: bsize, search_method, step_param, start_row, start_col, ref_mv_row, ref_mv_col,
 *          col_min, col_max, row_min, row_max (x->mv_limits), errorperbit, sadperbit,
 *          use_cost_list, prune_mesh_search, use_skip_sad (search with sdsf / sdsx4df, as
 *          av1_make_default_fullpel_ms_params does for use_downsampled_sad)
 * out: [0] var, [1] best row, [2] best col, [3] distortion, [4] sse, [5] err_cost,
 *      [6..10] cost_list, [11..14] ms_params.mv_limits after av1_set_mv_search_range */
EXPORT void e6_full_pixel_search(const int *in_ints, const uint8_t *src, int src_off,
                                 int src_stride, const uint8_t *ref, int ref_off,
                                 int ref_stride, const int *mvjcost, int *mvcost0,
                                 int *mvcost1, int *out) {
  init_fn();
  const int bsize = in_ints[0];
  const int search_method = in_ints[1];
  const int step_param = in_ints[2];
  const FULLPEL_MV start_mv = { (int16_t)in_ints[3], (int16_t)in_ints[4] };
  const MV ref_mv = { (int16_t)in_ints[5], (int16_t)in_ints[6] };
  for (int i = 0; i < NUM_DISTINCT_SEARCH_METHODS; ++i) {
    const int level = 0;
    av1_init_motion_compensation[i](&g_ss[i], ref_stride, level);
  }

  struct buf_2d src_buf = { 0 }, ref_buf = { 0 };
  src_buf.buf = (uint8_t *)src + src_off;
  src_buf.stride = src_stride;
  ref_buf.buf = (uint8_t *)ref + ref_off;
  ref_buf.stride = ref_stride;

  FULLPEL_MOTION_SEARCH_PARAMS ms;
  memset(&ms, 0, sizeof(ms));
  ms.bsize = (BLOCK_SIZE)bsize;
  ms.vfp = &g_fn[bsize];
  ms.ms_buffers.ref = &ref_buf;
  ms.ms_buffers.src = &src_buf;
  av1_set_ms_compound_refs(&ms.ms_buffers, NULL, NULL, 0, 0);
  av1_set_mv_search_method(&ms, g_ss, (SEARCH_METHODS)search_method);
  ms.mesh_search_mv_diff_threshold = 4;
  ms.prune_mesh_search = in_ints[14];
  ms.mv_limits.col_min = in_ints[7];
  ms.mv_limits.col_max = in_ints[8];
  ms.mv_limits.row_min = in_ints[9];
  ms.mv_limits.row_max = in_ints[10];
  av1_set_mv_search_range(&ms.mv_limits, &ref_mv);
  setup_cost_params(&ms.mv_cost_params, &ref_mv, mvjcost, mvcost0, mvcost1, in_ints[11],
                    in_ints[12]);
  ms.sdf = ms.vfp->sdf;
  ms.sdx4df = ms.vfp->sdx4df;
  ms.sdx3df = ms.vfp->sdx3df;
  if (in_ints[15]) {
    ms.sdf = ms.vfp->sdsf;
    ms.sdx4df = ms.vfp->sdsx4df;
    ms.sdx3df = ms.vfp->sdsx4df;
  }

  int cost_list[5] = { 0, 0, 0, 0, 0 };
  FULLPEL_MV best_mv;
  FULLPEL_MV_STATS stats;
  memset(&stats, 0, sizeof(stats));
  const int var = av1_full_pixel_search(start_mv, &ms, step_param,
                                        in_ints[13] ? cost_list : NULL, &best_mv, &stats,
                                        NULL);
  out[0] = var;
  out[1] = best_mv.row;
  out[2] = best_mv.col;
  out[3] = (int)stats.distortion;
  out[4] = (int)stats.sse;
  out[5] = stats.err_cost;
  for (int i = 0; i < 5; ++i) out[6 + i] = cost_list[i];
  out[11] = ms.mv_limits.col_min;
  out[12] = ms.mv_limits.col_max;
  out[13] = ms.mv_limits.row_min;
  out[14] = ms.mv_limits.row_max;
}

/* ---- sub-pel search ------------------------------------------------------------------- */

/* in_ints: bsize, start_row, start_col (1/8 pel), ref_mv_row, ref_mv_col,
 *          col_min, col_max, row_min, row_max (fullpel x->mv_limits), errorperbit, sadperbit,
 *          allow_hp, forced_stop, iters_per_step, has_cost_list, has_stats,
 *          stats.distortion, stats.sse, stats.err_cost, cost_list[5]
 * out: [0] besterr, [1] best row, [2] best col, [3] distortion, [4] sse1 */
EXPORT void e6_subpel_pruned_more(const int *in_ints, const uint8_t *src, int src_off,
                                  int src_stride, const uint8_t *ref, int ref_off,
                                  int ref_stride, const int *mvjcost, int *mvcost0,
                                  int *mvcost1, int *out) {
  init_fn();
  const int bsize = in_ints[0];
  const MV start_mv = { (int16_t)in_ints[1], (int16_t)in_ints[2] };
  const MV ref_mv = { (int16_t)in_ints[3], (int16_t)in_ints[4] };
  FullMvLimits full_limits;
  full_limits.col_min = in_ints[5];
  full_limits.col_max = in_ints[6];
  full_limits.row_min = in_ints[7];
  full_limits.row_max = in_ints[8];

  struct buf_2d src_buf = { 0 }, ref_buf = { 0 };
  src_buf.buf = (uint8_t *)src + src_off;
  src_buf.stride = src_stride;
  ref_buf.buf = (uint8_t *)ref + ref_off;
  ref_buf.stride = ref_stride;

  int cost_list[5];
  for (int i = 0; i < 5; ++i) cost_list[i] = in_ints[19 + i];

  SUBPEL_MOTION_SEARCH_PARAMS ms;
  memset(&ms, 0, sizeof(ms));
  ms.allow_hp = in_ints[11];
  ms.forced_stop = in_ints[12];
  ms.iters_per_step = in_ints[13];
  ms.cost_list = in_ints[14] ? cost_list : NULL;
  av1_set_subpel_mv_search_range(&ms.mv_limits, &full_limits, &ref_mv);
  setup_cost_params(&ms.mv_cost_params, &ref_mv, mvjcost, mvcost0, mvcost1, in_ints[9],
                    in_ints[10]);
  ms.var_params.vfp = &g_fn[bsize];
  ms.var_params.subpel_search_type = USE_2_TAPS;
  ms.var_params.w = block_size_wide[bsize];
  ms.var_params.h = block_size_high[bsize];
  ms.var_params.ms_buffers.ref = &ref_buf;
  ms.var_params.ms_buffers.src = &src_buf;
  av1_set_ms_compound_refs(&ms.var_params.ms_buffers, NULL, NULL, 0, 0);

  FULLPEL_MV_STATS stats;
  stats.distortion = (unsigned int)in_ints[16];
  stats.sse = (unsigned int)in_ints[17];
  stats.err_cost = in_ints[18];

  MACROBLOCKD *xd = (MACROBLOCKD *)calloc(1, sizeof(*xd));
  AV1_COMMON *cm = (AV1_COMMON *)calloc(1, sizeof(*cm));
  MB_MODE_INFO mbmi;
  memset(&mbmi, 0, sizeof(mbmi));
  MB_MODE_INFO *mbmi_ptr = &mbmi;
  xd->mi = &mbmi_ptr;
  struct scale_factors sf = { REF_NO_SCALE, REF_NO_SCALE, 16, 16 };
  xd->block_ref_scale_factors[0] = &sf;

  MV bestmv;
  int distortion = 0;
  unsigned int sse1 = 0;
  const int besterr = av1_find_best_sub_pixel_tree_pruned_more(
      xd, cm, &ms, start_mv, in_ints[15] ? &stats : NULL, &bestmv, &distortion, &sse1,
      NULL);
  out[0] = besterr;
  out[1] = bestmv.row;
  out[2] = bestmv.col;
  out[3] = distortion;
  out[4] = (int)sse1;
  free(cm);
  free(xd);
}
