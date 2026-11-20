/* Shim of work package f1 (bitstream): exports wrappers around av1/encoder/bitstream.c,
   encodetxb.c and the C versions of the txb level / context kernels, for the differential
   tests in tests/Av1rt.Tests/F1. Sequence and frame parameters come in as flat int arrays
   whose layout (SEQ_* / FH_* below) the C# side mirrors in F1Params.cs. */
#include <stdlib.h>
#include <string.h>

#include "config/aom_config.h"
#include "config/av1_rtcd.h"

#include "aom_dsp/bitwriter.h"
#include "aom_dsp/bitwriter_buffer.h"
#include "av1/common/av1_common_int.h"
#include "av1/common/blockd.h"
#include "av1/common/scan.h"
#include "av1/common/txb_common.h"
#include "av1/encoder/bitstream.h"
#include "av1/encoder/block.h"
#include "av1/encoder/encoder.h"
#include "av1/encoder/encodetxb.h"

#define EXPORT __declspec(dllexport)

/* ---- sequence parameters (shared by the sequence header and frame header tests) -------- */

enum {
  SEQ_PROFILE,
  SEQ_STILL_PICTURE,
  SEQ_REDUCED_STILL_PICTURE_HDR,
  SEQ_OPERATING_POINTS_CNT_MINUS_1,
  SEQ_HAS_NONZERO_OPERATING_POINT_IDC,
  SEQ_DISPLAY_MODEL_INFO_PRESENT_FLAG,
  SEQ_DISPLAY_MODEL_PARAM_PRESENT_FLAG,
  SEQ_INITIAL_DISPLAY_DELAY,
  SEQ_NUM_BITS_WIDTH,
  SEQ_NUM_BITS_HEIGHT,
  SEQ_MAX_FRAME_WIDTH,
  SEQ_MAX_FRAME_HEIGHT,
  SEQ_FRAME_ID_NUMBERS_PRESENT_FLAG,
  SEQ_DELTA_FRAME_ID_LENGTH,
  SEQ_FRAME_ID_LENGTH,
  SEQ_SB_SIZE,
  SEQ_ENABLE_FILTER_INTRA,
  SEQ_ENABLE_INTRA_EDGE_FILTER,
  SEQ_ENABLE_INTERINTRA_COMPOUND,
  SEQ_ENABLE_MASKED_COMPOUND,
  SEQ_ENABLE_WARPED_MOTION,
  SEQ_ENABLE_DUAL_FILTER,
  SEQ_ENABLE_ORDER_HINT,
  SEQ_ENABLE_DIST_WTD_COMP,
  SEQ_ENABLE_REF_FRAME_MVS,
  SEQ_ORDER_HINT_BITS_MINUS_1,
  SEQ_FORCE_SCREEN_CONTENT_TOOLS,
  SEQ_FORCE_INTEGER_MV,
  SEQ_ENABLE_SUPERRES,
  SEQ_ENABLE_CDEF,
  SEQ_ENABLE_RESTORATION,
  SEQ_BIT_DEPTH,
  SEQ_MONOCHROME,
  SEQ_COLOR_PRIMARIES,
  SEQ_TRANSFER_CHARACTERISTICS,
  SEQ_MATRIX_COEFFICIENTS,
  SEQ_COLOR_RANGE,
  SEQ_SUBSAMPLING_X,
  SEQ_SUBSAMPLING_Y,
  SEQ_CHROMA_SAMPLE_POSITION,
  SEQ_SEPARATE_UV_DELTA_Q,
  SEQ_FILM_GRAIN_PARAMS_PRESENT,
  SEQ_OP_BASE, /* 4 x { operating_point_idc, seq_level_idx, tier } */
  SEQ_COUNT = SEQ_OP_BASE + 12
};

static void fill_seq(SequenceHeader *s, const int *p) {
  memset(s, 0, sizeof(*s));
  s->profile = (BITSTREAM_PROFILE)p[SEQ_PROFILE];
  s->still_picture = (uint8_t)p[SEQ_STILL_PICTURE];
  s->reduced_still_picture_hdr = (uint8_t)p[SEQ_REDUCED_STILL_PICTURE_HDR];
  s->operating_points_cnt_minus_1 = p[SEQ_OPERATING_POINTS_CNT_MINUS_1];
  s->has_nonzero_operating_point_idc = p[SEQ_HAS_NONZERO_OPERATING_POINT_IDC] != 0;
  s->display_model_info_present_flag = (uint8_t)p[SEQ_DISPLAY_MODEL_INFO_PRESENT_FLAG];
  for (int i = 0; i < 4; ++i) {
    s->operating_point_idc[i] = p[SEQ_OP_BASE + 3 * i];
    s->seq_level_idx[i] = (AV1_LEVEL)p[SEQ_OP_BASE + 3 * i + 1];
    s->tier[i] = (uint8_t)p[SEQ_OP_BASE + 3 * i + 2];
    s->op_params[i].display_model_param_present_flag = p[SEQ_DISPLAY_MODEL_PARAM_PRESENT_FLAG];
    s->op_params[i].initial_display_delay = p[SEQ_INITIAL_DISPLAY_DELAY];
  }
  s->num_bits_width = p[SEQ_NUM_BITS_WIDTH];
  s->num_bits_height = p[SEQ_NUM_BITS_HEIGHT];
  s->max_frame_width = p[SEQ_MAX_FRAME_WIDTH];
  s->max_frame_height = p[SEQ_MAX_FRAME_HEIGHT];
  s->frame_id_numbers_present_flag = (uint8_t)p[SEQ_FRAME_ID_NUMBERS_PRESENT_FLAG];
  s->delta_frame_id_length = p[SEQ_DELTA_FRAME_ID_LENGTH];
  s->frame_id_length = p[SEQ_FRAME_ID_LENGTH];
  s->sb_size = (BLOCK_SIZE)p[SEQ_SB_SIZE];
  s->mib_size = mi_size_wide[s->sb_size];
  s->mib_size_log2 = mi_size_wide_log2[s->sb_size];
  s->enable_filter_intra = (uint8_t)p[SEQ_ENABLE_FILTER_INTRA];
  s->enable_intra_edge_filter = (uint8_t)p[SEQ_ENABLE_INTRA_EDGE_FILTER];
  s->enable_interintra_compound = (uint8_t)p[SEQ_ENABLE_INTERINTRA_COMPOUND];
  s->enable_masked_compound = (uint8_t)p[SEQ_ENABLE_MASKED_COMPOUND];
  s->enable_warped_motion = (uint8_t)p[SEQ_ENABLE_WARPED_MOTION];
  s->enable_dual_filter = (uint8_t)p[SEQ_ENABLE_DUAL_FILTER];
  s->order_hint_info.enable_order_hint = p[SEQ_ENABLE_ORDER_HINT];
  s->order_hint_info.enable_dist_wtd_comp = p[SEQ_ENABLE_DIST_WTD_COMP];
  s->order_hint_info.enable_ref_frame_mvs = p[SEQ_ENABLE_REF_FRAME_MVS];
  s->order_hint_info.order_hint_bits_minus_1 = p[SEQ_ORDER_HINT_BITS_MINUS_1];
  s->force_screen_content_tools = (uint8_t)p[SEQ_FORCE_SCREEN_CONTENT_TOOLS];
  s->force_integer_mv = (uint8_t)p[SEQ_FORCE_INTEGER_MV];
  s->enable_superres = (uint8_t)p[SEQ_ENABLE_SUPERRES];
  s->enable_cdef = (uint8_t)p[SEQ_ENABLE_CDEF];
  s->enable_restoration = (uint8_t)p[SEQ_ENABLE_RESTORATION];
  s->bit_depth = (aom_bit_depth_t)p[SEQ_BIT_DEPTH];
  s->use_highbitdepth = p[SEQ_BIT_DEPTH] > 8;
  s->monochrome = (uint8_t)p[SEQ_MONOCHROME];
  s->color_primaries = (aom_color_primaries_t)p[SEQ_COLOR_PRIMARIES];
  s->transfer_characteristics = (aom_transfer_characteristics_t)p[SEQ_TRANSFER_CHARACTERISTICS];
  s->matrix_coefficients = (aom_matrix_coefficients_t)p[SEQ_MATRIX_COEFFICIENTS];
  s->color_range = p[SEQ_COLOR_RANGE];
  s->subsampling_x = p[SEQ_SUBSAMPLING_X];
  s->subsampling_y = p[SEQ_SUBSAMPLING_Y];
  s->chroma_sample_position = (aom_chroma_sample_position_t)p[SEQ_CHROMA_SAMPLE_POSITION];
  s->separate_uv_delta_q = (uint8_t)p[SEQ_SEPARATE_UV_DELTA_Q];
  s->film_grain_params_present = (uint8_t)p[SEQ_FILM_GRAIN_PARAMS_PRESENT];
}

EXPORT int f1_seq_count(void) { return SEQ_COUNT; }

/* ---- OBU header / size ---------------------------------------------------------------- */

/* out[0] = frame_header_count after the call (starting from 0). Returns the header size. */
EXPORT uint32_t f1_write_obu_header(int keep_level_stats, int obu_type,
                                    int has_nonzero_operating_point_idc,
                                    int is_layer_specific_obu, int obu_extension,
                                    uint8_t *dst, int *out) {
  AV1LevelParams *lp = (AV1LevelParams *)calloc(1, sizeof(*lp));
  lp->keep_level_stats = (uint32_t)keep_level_stats;
  int frame_header_count = 0;
  uint32_t size = av1_write_obu_header(lp, &frame_header_count, (OBU_TYPE)obu_type,
                                       has_nonzero_operating_point_idc != 0,
                                       is_layer_specific_obu != 0, obu_extension, dst);
  out[0] = frame_header_count;
  free(lp);
  return size;
}

EXPORT int f1_write_uleb_obu_size(uint64_t obu_payload_size, uint8_t *dest, int dest_size) {
  return av1_write_uleb_obu_size((size_t)obu_payload_size, dest, (size_t)dest_size);
}

EXPORT uint32_t f1_write_sequence_header_obu(const int *seq, uint8_t *dst, int dst_size) {
  SequenceHeader *s = (SequenceHeader *)calloc(1, sizeof(*s));
  fill_seq(s, seq);
  uint32_t size = av1_write_sequence_header_obu(s, dst, (size_t)dst_size);
  free(s);
  return size;
}

EXPORT int f1_neg_interleave(int x, int ref, int max) { return av1_neg_interleave(x, ref, max); }

/* ---- frame header: av1_write_obu_tg_tile_headers ----------------------------------------- */

enum {
  FH_FRAME_TYPE,
  FH_SHOW_FRAME,
  FH_SHOWABLE_FRAME,
  FH_SHOW_EXISTING_FRAME,
  FH_EXISTING_FB_IDX_TO_SHOW,
  FH_ERROR_RESILIENT_MODE,
  FH_DISABLE_CDF_UPDATE,
  FH_ALLOW_SCREEN_CONTENT_TOOLS,
  FH_CUR_FRAME_FORCE_INTEGER_MV,
  FH_CURRENT_FRAME_ID,
  FH_UPSCALED_WIDTH,
  FH_UPSCALED_HEIGHT,
  FH_RENDER_WIDTH,
  FH_RENDER_HEIGHT,
  FH_ORDER_HINT,
  FH_PRIMARY_REF_FRAME,
  FH_REFRESH_FRAME_FLAGS,
  FH_ALLOW_INTRABC,
  FH_ALLOW_HIGH_PRECISION_MV,
  FH_INTERP_FILTER,
  FH_SWITCHABLE_MOTION_MODE,
  FH_ALLOW_REF_FRAME_MVS,
  FH_REFRESH_FRAME_CONTEXT,
  FH_BASE_QINDEX,
  FH_Y_DC_DELTA_Q,
  FH_U_DC_DELTA_Q,
  FH_U_AC_DELTA_Q,
  FH_V_DC_DELTA_Q,
  FH_V_AC_DELTA_Q,
  FH_USING_QMATRIX,
  FH_QM_Y,
  FH_QM_U,
  FH_QM_V,
  FH_SEG_ENABLED,
  FH_SEG_UPDATE_MAP,
  FH_SEG_TEMPORAL_UPDATE,
  FH_SEG_UPDATE_DATA,
  FH_FILTER_LEVEL_0,
  FH_FILTER_LEVEL_1,
  FH_FILTER_LEVEL_U,
  FH_FILTER_LEVEL_V,
  FH_SHARPNESS,
  FH_MODE_REF_DELTA_ENABLED,
  FH_MODE_REF_DELTA_UPDATE,
  FH_CDEF_DAMPING,
  FH_CDEF_BITS,
  FH_NB_CDEF_STRENGTHS,
  FH_CODED_LOSSLESS,
  FH_ALL_LOSSLESS,
  FH_TX_MODE,
  FH_REFERENCE_MODE,
  FH_SKIP_MODE_ALLOWED,
  FH_SKIP_MODE_FLAG,
  FH_ALLOW_WARPED_MOTION,
  FH_REDUCED_TX_SET_USED,
  FH_TILE_UNIFORM,
  FH_TILE_LOG2_COLS,
  FH_TILE_LOG2_ROWS,
  FH_TILE_MIN_LOG2_COLS,
  FH_TILE_MAX_LOG2_COLS,
  FH_TILE_MIN_LOG2_ROWS,
  FH_TILE_MAX_LOG2_ROWS,
  FH_TILE_COLS,
  FH_TILE_ROWS,
  FH_TILE_MAX_WIDTH_SB,
  FH_TILE_MAX_HEIGHT_SB,
  FH_MI_COLS,
  FH_MI_ROWS,
  FH_NUM_TG,
  FH_TILE_IDX,
  FH_OBU_EXTN_HEADER,
  FH_KEEP_LEVEL_STATS,
  FH_ENABLE_REF_SHORT_SIGNALING,
  FH_SUPERRES_SCALE_DENOMINATOR,
  FH_HAS_PREV_FRAME,
  FH_REMAPPED_REF_IDX = FH_HAS_PREV_FRAME + 1, /* [INTER_REFS_PER_FRAME] */
  FH_REF_FRAME_ID = FH_REMAPPED_REF_IDX + INTER_REFS_PER_FRAME, /* [REF_FRAMES] */
  FH_LF_REF_DELTAS = FH_REF_FRAME_ID + REF_FRAMES,              /* [REF_FRAMES] */
  FH_LF_MODE_DELTAS = FH_LF_REF_DELTAS + REF_FRAMES,            /* [MAX_MODE_LF_DELTAS] */
  FH_CDEF_STRENGTHS = FH_LF_MODE_DELTAS + MAX_MODE_LF_DELTAS,   /* [CDEF_MAX_STRENGTHS] */
  FH_CDEF_UV_STRENGTHS = FH_CDEF_STRENGTHS + CDEF_MAX_STRENGTHS,
  FH_SEG_FEATURE_MASK = FH_CDEF_UV_STRENGTHS + CDEF_MAX_STRENGTHS, /* [MAX_SEGMENTS] */
  FH_SEG_FEATURE_DATA = FH_SEG_FEATURE_MASK + MAX_SEGMENTS, /* [MAX_SEGMENTS][SEG_LVL_MAX] */
  /* REF_FRAMES x { y_crop_width, y_crop_height, render_width, render_height, order_hint,
     ref_deltas[REF_FRAMES], mode_deltas[MAX_MODE_LF_DELTAS] } */
  FH_REF_BUFS = FH_SEG_FEATURE_DATA + MAX_SEGMENTS * SEG_LVL_MAX,
  FH_REF_BUF_STRIDE = 5 + REF_FRAMES + MAX_MODE_LF_DELTAS,
  FH_COL_START_SB = FH_REF_BUFS + REF_FRAMES * FH_REF_BUF_STRIDE, /* [17] */
  FH_ROW_START_SB = FH_COL_START_SB + 17,                          /* [17] */
  FH_COUNT = FH_ROW_START_SB + 17
};

EXPORT int f1_fh_count(void) { return FH_COUNT; }

/* Runs av1_write_obu_tg_tile_headers on a calloc'd AV1_COMP built from seq/fh into dst.
   out[0] curr_tg_hdr_size, out[1] obu_header_size, out[2] total_size, out[3] saved_wb byte
   offset from dst (-1 when the saved bit buffer was not set), out[4] saved_wb bit_offset,
   out[5] frame_header_count, out[6] frame_refs_short_signaling. */
EXPORT void f1_write_obu_tg_tile_headers(const int *seq, const int *fh, uint8_t *dst,
                                         int *out) {
  AV1_PRIMARY *ppi = (AV1_PRIMARY *)calloc(1, sizeof(*ppi));
  AV1_COMP *cpi = (AV1_COMP *)calloc(1, sizeof(*cpi));
  SequenceHeader *s = (SequenceHeader *)calloc(1, sizeof(*s));
  RefCntBuffer *bufs = (RefCntBuffer *)calloc(REF_FRAMES + 1, sizeof(*bufs));
  struct aom_internal_error_info *err =
      (struct aom_internal_error_info *)calloc(1, sizeof(*err));
  fill_seq(s, seq);
  cpi->ppi = ppi;
  AV1_COMMON *const cm = &cpi->common;
  cm->seq_params = s;
  cm->error = err;
  ppi->level_params.keep_level_stats = (uint32_t)fh[FH_KEEP_LEVEL_STATS];
  cpi->sf.rt_sf.enable_ref_short_signaling = fh[FH_ENABLE_REF_SHORT_SIGNALING] != 0;
  cpi->num_tg = fh[FH_NUM_TG];
  cpi->existing_fb_idx_to_show = fh[FH_EXISTING_FB_IDX_TO_SHOW];

  cm->current_frame.frame_type = (FRAME_TYPE)fh[FH_FRAME_TYPE];
  cm->show_frame = fh[FH_SHOW_FRAME];
  cm->showable_frame = fh[FH_SHOWABLE_FRAME];
  cm->show_existing_frame = fh[FH_SHOW_EXISTING_FRAME];
  FeatureFlags *f = &cm->features;
  f->error_resilient_mode = fh[FH_ERROR_RESILIENT_MODE] != 0;
  f->disable_cdf_update = fh[FH_DISABLE_CDF_UPDATE] != 0;
  f->allow_screen_content_tools = fh[FH_ALLOW_SCREEN_CONTENT_TOOLS] != 0;
  f->cur_frame_force_integer_mv = fh[FH_CUR_FRAME_FORCE_INTEGER_MV] != 0;
  f->primary_ref_frame = fh[FH_PRIMARY_REF_FRAME];
  f->allow_intrabc = fh[FH_ALLOW_INTRABC] != 0;
  f->allow_high_precision_mv = fh[FH_ALLOW_HIGH_PRECISION_MV] != 0;
  f->interp_filter = (InterpFilter)fh[FH_INTERP_FILTER];
  f->switchable_motion_mode = fh[FH_SWITCHABLE_MOTION_MODE] != 0;
  f->allow_ref_frame_mvs = fh[FH_ALLOW_REF_FRAME_MVS] != 0;
  f->refresh_frame_context = (REFRESH_FRAME_CONTEXT_MODE)fh[FH_REFRESH_FRAME_CONTEXT];
  f->coded_lossless = fh[FH_CODED_LOSSLESS] != 0;
  f->all_lossless = fh[FH_ALL_LOSSLESS] != 0;
  f->tx_mode = (TX_MODE)fh[FH_TX_MODE];
  f->allow_warped_motion = fh[FH_ALLOW_WARPED_MOTION] != 0;
  f->reduced_tx_set_used = fh[FH_REDUCED_TX_SET_USED] != 0;
  cm->current_frame_id = fh[FH_CURRENT_FRAME_ID];
  cm->width = cm->superres_upscaled_width = fh[FH_UPSCALED_WIDTH];
  cm->height = cm->superres_upscaled_height = fh[FH_UPSCALED_HEIGHT];
  cm->render_width = fh[FH_RENDER_WIDTH];
  cm->render_height = fh[FH_RENDER_HEIGHT];
  cm->superres_scale_denominator = (uint8_t)fh[FH_SUPERRES_SCALE_DENOMINATOR];
  cm->current_frame.order_hint = (unsigned int)fh[FH_ORDER_HINT];
  cm->current_frame.refresh_frame_flags = fh[FH_REFRESH_FRAME_FLAGS];
  cm->current_frame.reference_mode = (REFERENCE_MODE)fh[FH_REFERENCE_MODE];
  cm->current_frame.skip_mode_info.skip_mode_allowed = fh[FH_SKIP_MODE_ALLOWED];
  cm->current_frame.skip_mode_info.skip_mode_flag = fh[FH_SKIP_MODE_FLAG];
  cm->mi_params.mi_cols = fh[FH_MI_COLS];
  cm->mi_params.mi_rows = fh[FH_MI_ROWS];

  CommonQuantParams *q = &cm->quant_params;
  q->base_qindex = fh[FH_BASE_QINDEX];
  q->y_dc_delta_q = fh[FH_Y_DC_DELTA_Q];
  q->u_dc_delta_q = fh[FH_U_DC_DELTA_Q];
  q->u_ac_delta_q = fh[FH_U_AC_DELTA_Q];
  q->v_dc_delta_q = fh[FH_V_DC_DELTA_Q];
  q->v_ac_delta_q = fh[FH_V_AC_DELTA_Q];
  q->using_qmatrix = fh[FH_USING_QMATRIX] != 0;
  q->qmatrix_level_y = fh[FH_QM_Y];
  q->qmatrix_level_u = fh[FH_QM_U];
  q->qmatrix_level_v = fh[FH_QM_V];

  struct segmentation *seg = &cm->seg;
  seg->enabled = (uint8_t)fh[FH_SEG_ENABLED];
  seg->update_map = (uint8_t)fh[FH_SEG_UPDATE_MAP];
  seg->temporal_update = (uint8_t)fh[FH_SEG_TEMPORAL_UPDATE];
  seg->update_data = (uint8_t)fh[FH_SEG_UPDATE_DATA];
  for (int i = 0; i < MAX_SEGMENTS; ++i) {
    seg->feature_mask[i] = (unsigned int)fh[FH_SEG_FEATURE_MASK + i];
    for (int j = 0; j < SEG_LVL_MAX; ++j)
      seg->feature_data[i][j] = (int16_t)fh[FH_SEG_FEATURE_DATA + i * SEG_LVL_MAX + j];
  }

  struct loopfilter *lf = &cm->lf;
  lf->filter_level[0] = fh[FH_FILTER_LEVEL_0];
  lf->filter_level[1] = fh[FH_FILTER_LEVEL_1];
  lf->filter_level_u = fh[FH_FILTER_LEVEL_U];
  lf->filter_level_v = fh[FH_FILTER_LEVEL_V];
  lf->sharpness_level = fh[FH_SHARPNESS];
  lf->mode_ref_delta_enabled = (uint8_t)fh[FH_MODE_REF_DELTA_ENABLED];
  lf->mode_ref_delta_update = (uint8_t)fh[FH_MODE_REF_DELTA_UPDATE];
  for (int i = 0; i < REF_FRAMES; ++i) lf->ref_deltas[i] = (int8_t)fh[FH_LF_REF_DELTAS + i];
  for (int i = 0; i < MAX_MODE_LF_DELTAS; ++i)
    lf->mode_deltas[i] = (int8_t)fh[FH_LF_MODE_DELTAS + i];

  CdefInfo *cdef = &cm->cdef_info;
  cdef->cdef_damping = fh[FH_CDEF_DAMPING];
  cdef->cdef_bits = fh[FH_CDEF_BITS];
  cdef->nb_cdef_strengths = fh[FH_NB_CDEF_STRENGTHS];
  for (int i = 0; i < CDEF_MAX_STRENGTHS; ++i) {
    cdef->cdef_strengths[i] = fh[FH_CDEF_STRENGTHS + i];
    cdef->cdef_uv_strengths[i] = fh[FH_CDEF_UV_STRENGTHS + i];
  }

  CommonTileParams *t = &cm->tiles;
  t->uniform_spacing = fh[FH_TILE_UNIFORM];
  t->log2_cols = fh[FH_TILE_LOG2_COLS];
  t->log2_rows = fh[FH_TILE_LOG2_ROWS];
  t->min_log2_cols = fh[FH_TILE_MIN_LOG2_COLS];
  t->max_log2_cols = fh[FH_TILE_MAX_LOG2_COLS];
  t->min_log2_rows = fh[FH_TILE_MIN_LOG2_ROWS];
  t->max_log2_rows = fh[FH_TILE_MAX_LOG2_ROWS];
  t->cols = fh[FH_TILE_COLS];
  t->rows = fh[FH_TILE_ROWS];
  t->max_width_sb = fh[FH_TILE_MAX_WIDTH_SB];
  t->max_height_sb = fh[FH_TILE_MAX_HEIGHT_SB];
  for (int i = 0; i < 17; ++i) {
    t->col_start_sb[i] = fh[FH_COL_START_SB + i];
    t->row_start_sb[i] = fh[FH_ROW_START_SB + i];
  }

  for (int i = 0; i < INTER_REFS_PER_FRAME; ++i)
    cm->remapped_ref_idx[i] = fh[FH_REMAPPED_REF_IDX + i];
  for (int i = 0; i < REF_FRAMES; ++i) {
    cm->ref_frame_id[i] = fh[FH_REF_FRAME_ID + i];
    const int *b = fh + FH_REF_BUFS + i * FH_REF_BUF_STRIDE;
    RefCntBuffer *buf = &bufs[i];
    buf->buf.y_crop_width = b[0];
    buf->buf.y_crop_height = b[1];
    buf->buf.render_width = b[2];
    buf->buf.render_height = b[3];
    buf->order_hint = (unsigned int)b[4];
    for (int j = 0; j < REF_FRAMES; ++j) buf->ref_deltas[j] = (int8_t)b[5 + j];
    for (int j = 0; j < MAX_MODE_LF_DELTAS; ++j)
      buf->mode_deltas[j] = (int8_t)b[5 + REF_FRAMES + j];
    cm->ref_frame_map[i] = buf;
  }
  for (int i = 0; i < REF_FRAMES; ++i) cm->global_motion[i] = default_warp_params;
  if (fh[FH_HAS_PREV_FRAME]) {
    cm->prev_frame = &bufs[REF_FRAMES];
    for (int i = 0; i < REF_FRAMES; ++i) cm->prev_frame->global_motion[i] = default_warp_params;
  }

  struct aom_write_bit_buffer saved_wb = { NULL, 0 };
  uint32_t total_size = 0;
  PackBSParams pb;
  memset(&pb, 0, sizeof(pb));
  pb.saved_wb = &saved_wb;
  pb.total_size = &total_size;
  pb.dst = dst;
  pb.tile_data_curr = dst;
  pb.obu_extn_header = (uint8_t)fh[FH_OBU_EXTN_HEADER];
  av1_write_obu_tg_tile_headers(cpi, &cpi->td.mb.e_mbd, &pb, fh[FH_TILE_IDX]);

  out[0] = pb.curr_tg_hdr_size;
  out[1] = (int)pb.obu_header_size;
  out[2] = (int)total_size;
  out[3] = saved_wb.bit_buffer ? (int)(saved_wb.bit_buffer - dst) : -1;
  out[4] = (int)saved_wb.bit_offset;
  out[5] = cpi->frame_header_count;
  out[6] = cm->current_frame.frame_refs_short_signaling;

  free(err);
  free(bufs);
  free(s);
  free(cpi);
  free(ppi);
}

/* ---- coefficients: av1_write_coeffs_txb ------------------------------------------------- */

enum {
  CB_TX_SIZE,
  CB_TX_TYPE,
  CB_PLANE,
  CB_IS_INTER,
  CB_MODE,
  CB_UV_MODE,
  CB_EOB,
  CB_ENTROPY_CTX,
  CB_COUNT
};

/* Writes count transform blocks into one aom_writer with the FRAME_CONTEXT image fc (updated
   in place). params is count x CB_COUNT, coeffs is count x 1024 tran_low_t (the block in
   av1_txb_init_levels layout). Returns the byte count, or -1. */
EXPORT int f1_write_coeffs_txb(uint8_t *fc, int base_qindex, int reduced_tx_set,
                               int allow_update_cdf, int count, const int *params,
                               const int32_t *coeffs, uint8_t *out, int cap) {
  AV1_COMMON *cm = (AV1_COMMON *)calloc(1, sizeof(*cm));
  MACROBLOCK *x = (MACROBLOCK *)calloc(1, sizeof(*x));
  MB_MODE_INFO *mbmi = (MB_MODE_INFO *)calloc(1, sizeof(*mbmi));
  MB_MODE_INFO_EXT_FRAME *ext = (MB_MODE_INFO_EXT_FRAME *)calloc(1, sizeof(*ext));
  CB_COEFF_BUFFER cb;
  uint8_t tx_type_map[1];
  uint16_t eobs[1];
  uint8_t ectx[1];
  int32_t *tcoeff = (int32_t *)calloc(1024, sizeof(*tcoeff));
  cm->features.reduced_tx_set_used = reduced_tx_set != 0;
  cm->seg.enabled = 0;
  cm->quant_params.base_qindex = base_qindex;
  MACROBLOCKD *xd = &x->e_mbd;
  MB_MODE_INFO *mi_ptr = mbmi;
  xd->mi = &mi_ptr;
  xd->tile_ctx = (FRAME_CONTEXT *)fc;
  xd->tx_type_map = tx_type_map;
  xd->tx_type_map_stride = 1;
  for (int p = 1; p < MAX_MB_PLANE; ++p) {
    xd->plane[p].subsampling_x = 1;
    xd->plane[p].subsampling_y = 1;
  }
  x->mbmi_ext_frame = ext;
  x->cb_coef_buff = &cb;
  for (int p = 0; p < MAX_MB_PLANE; ++p) {
    cb.tcoeff[p] = tcoeff;
    cb.eobs[p] = eobs;
    cb.entropy_ctx[p] = ectx;
  }

  aom_writer w;
  memset(&w, 0, sizeof(w));
  aom_start_encode(&w, out, (size_t)cap);
  w.allow_update_cdf = (uint8_t)allow_update_cdf;
  for (int i = 0; i < count; ++i) {
    const int *p = params + i * CB_COUNT;
    mbmi->ref_frame[0] = p[CB_IS_INTER] ? LAST_FRAME : INTRA_FRAME;
    mbmi->ref_frame[1] = NONE_FRAME;
    mbmi->mode = (PREDICTION_MODE)p[CB_MODE];
    mbmi->uv_mode = (UV_PREDICTION_MODE)p[CB_UV_MODE];
    tx_type_map[0] = (uint8_t)p[CB_TX_TYPE];
    eobs[0] = (uint16_t)p[CB_EOB];
    ectx[0] = (uint8_t)p[CB_ENTROPY_CTX];
    memcpy(tcoeff, coeffs + i * 1024, 1024 * sizeof(*tcoeff));
    av1_write_coeffs_txb(cm, x, &w, 0, 0, p[CB_PLANE], 0, (TX_SIZE)p[CB_TX_SIZE]);
  }
  int ret = aom_stop_encode(&w) < 0 ? -1 : (int)w.pos;

  free(tcoeff);
  free(ext);
  free(mbmi);
  free(x);
  free(cm);
  return ret;
}

/* ---- txb levels / contexts --------------------------------------------------------------- */

EXPORT void f1_txb_init_levels(const int32_t *coeff, int width, int height,
                               uint8_t *levels_buf) {
  av1_txb_init_levels_c(coeff, width, height, set_levels(levels_buf, height));
}

EXPORT void f1_get_nz_map_contexts(const uint8_t *levels_buf, int tx_size, int tx_type, int eob,
                                   int8_t *coeff_contexts) {
  const SCAN_ORDER *so = get_scan((TX_SIZE)tx_size, (TX_TYPE)tx_type);
  const int height = get_txb_high((TX_SIZE)tx_size);
  av1_get_nz_map_contexts_c(set_levels((uint8_t *)levels_buf, height), so->scan,
                            (uint16_t)eob, (TX_SIZE)tx_size, tx_type_to_class[tx_type],
                            coeff_contexts);
}

EXPORT int f1_get_eob_pos_token(int eob, int *extra) {
  return av1_get_eob_pos_token(eob, extra);
}
