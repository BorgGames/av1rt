/* Shim for package g1 (encoder.c, encode_strategy.c, av1_cx_iface.c, aom_encoder.c,
 * aom_codec.c): differential tests of the encoder configuration layer.
 *
 * validate_config, set_encoder_config and default_extra_cfg are file-static in
 * av1/av1_cx_iface.c, so the shim compiles that file itself. Every external symbol of
 * av1_cx_iface.obj is then defined here, and the linker never pulls that object from aom.lib.
 * The C# tests reach the same private functions through reflection. */
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#include "av1/av1_cx_iface.c"

#include "shim_g1_fields.h"

#define EXPORT __declspec(dllexport)

/* kv: n (id, value) pairs. id < 1000 sets an aom_codec_enc_cfg_t field (CFG_SET order),
 * id >= 1000 an av1_extracfg field (EXTRA_SET order). Starts from the realtime defaults. */
static void g1_make_config(aom_codec_enc_cfg_t *cfg, struct av1_extracfg *extra,
                           const int64_t *kv, int n) {
  memset(cfg, 0, sizeof(*cfg));
  if (aom_codec_enc_config_default(aom_codec_av1_cx(), cfg, AOM_USAGE_REALTIME)) abort();
  *extra = default_extra_cfg;
  for (int i = 0; i < n; i++) {
    const int id = (int)kv[2 * i];
    const int64_t v = kv[2 * i + 1];
    if (id < 1000)
      g1_set_cfg(cfg, id, v);
    else
      g1_set_extra(extra, id - 1000, v);
  }
}

static void g1_copy_detail(const char *detail, char *out, int cap) {
  if (cap <= 0) return;
  if (detail == NULL) {
    out[0] = 0;
    return;
  }
  strncpy(out, detail, cap - 1);
  out[cap - 1] = 0;
}

/* aom_codec_enc_config_default: returns the number of dumped fields, or -error. */
EXPORT int aomshim_g1_config_default(unsigned int usage, int64_t *out) {
  aom_codec_enc_cfg_t cfg;
  memset(&cfg, 0, sizeof(cfg));
  const aom_codec_err_t res = aom_codec_enc_config_default(aom_codec_av1_cx(), &cfg, usage);
  if (res != AOM_CODEC_OK) return -(int)res;
  return g1_dump_cfg(&cfg, out);
}

/* validate_config: returns the aom_codec_err_t, err_detail in detail ("" for NULL, and
 * *has_detail = 0). */
EXPORT int aomshim_g1_validate_config(const int64_t *kv, int n, char *detail, int cap,
                                      int *has_detail) {
  aom_codec_enc_cfg_t cfg;
  struct av1_extracfg extra;
  g1_make_config(&cfg, &extra, kv, n);
  aom_codec_alg_priv_t *ctx = (aom_codec_alg_priv_t *)calloc(1, sizeof(*ctx));
  const aom_codec_err_t res = validate_config(ctx, &cfg, &extra);
  *has_detail = ctx->base.err_detail != NULL;
  g1_copy_detail(ctx->base.err_detail, detail, cap);
  free(ctx);
  return (int)res;
}

/* set_encoder_config into a zeroed AV1EncoderConfig: returns the number of dumped fields;
 * tiles[0..1] receive extra_cfg.tile_columns / tile_rows afterwards (set_auto_tiles). */
EXPORT int aomshim_g1_set_encoder_config(const int64_t *kv, int n, int64_t *out,
                                         int64_t *tiles) {
  aom_codec_enc_cfg_t cfg;
  struct av1_extracfg extra;
  g1_make_config(&cfg, &extra, kv, n);
  AV1EncoderConfig *oxcf = (AV1EncoderConfig *)calloc(1, sizeof(*oxcf));
  set_encoder_config(oxcf, &cfg, &extra);
  const int count = g1_dump_oxcf(oxcf, out);
  tiles[0] = extra.tile_columns;
  tiles[1] = extra.tile_rows;
  free(oxcf);
  return count;
}

/* av1_get_encoder_config (default extra config): returns the number of dumped fields. */
EXPORT int aomshim_g1_get_encoder_config(const int64_t *kv, int n, int64_t *out) {
  aom_codec_enc_cfg_t cfg;
  struct av1_extracfg extra;
  g1_make_config(&cfg, &extra, kv, n);
  AV1EncoderConfig *oxcf = (AV1EncoderConfig *)calloc(1, sizeof(*oxcf));
  *oxcf = av1_get_encoder_config(&cfg);
  /* av1_get_encoder_config leaves the fields set_encoder_config does not write
   * uninitialized; the dump only reads written fields. */
  const int count = g1_dump_oxcf(oxcf, out);
  free(oxcf);
  return count;
}

/* set_encoder_config + av1_create_primary_compressor (init_config_sequence ->
 * av1_change_config_seq -> init_seq_coding_tools): dumps ppi->seq_params. Returns the number
 * of dumped fields, or -1 if the primary compressor could not be created. */
EXPORT int aomshim_g1_seq_params(const int64_t *kv, int n, int64_t *out) {
  static struct aom_codec_pkt_list_decl_g1 {
    struct aom_codec_pkt_list head;
    struct aom_codec_cx_pkt pkts[256];
  } pkt_list;
  aom_codec_enc_cfg_t cfg;
  struct av1_extracfg extra;
  g1_make_config(&cfg, &extra, kv, n);
  AV1EncoderConfig *oxcf = (AV1EncoderConfig *)calloc(1, sizeof(*oxcf));
  set_encoder_config(oxcf, &cfg, &extra);
  av1_initialize_enc(cfg.g_usage, cfg.rc_end_usage);
  AV1_PRIMARY *ppi = av1_create_primary_compressor(&pkt_list.head, 0, oxcf);
  int count = -1;
  if (ppi != NULL) {
    count = g1_dump_seq(&ppi->seq_params, out);
    av1_remove_primary_compressor(ppi);
  }
  free(oxcf);
  return count;
}

/* aom_codec_enc_init with the given configuration (public path of validate_config). Returns
 * the init result; detail receives ctx.err_detail. The encoder is destroyed again. */
EXPORT int aomshim_g1_enc_init(const int64_t *kv, int n, char *detail, int cap,
                               int *has_detail) {
  aom_codec_enc_cfg_t cfg;
  struct av1_extracfg extra;
  g1_make_config(&cfg, &extra, kv, n);
  aom_codec_ctx_t codec;
  memset(&codec, 0, sizeof(codec));
  const aom_codec_err_t res = aom_codec_enc_init(&codec, aom_codec_av1_cx(), &cfg, 0);
  *has_detail = codec.err_detail != NULL;
  g1_copy_detail(codec.err_detail, detail, cap);
  if (res == AOM_CODEC_OK) aom_codec_destroy(&codec);
  return (int)res;
}

/* av1_convert_sect5obus_to_annexb (encoder.c). */
EXPORT int aomshim_g1_convert_sect5obus_to_annexb(uint8_t *buffer, int buffer_size,
                                                  int *frame_size) {
  size_t size = (size_t)*frame_size;
  const int res = av1_convert_sect5obus_to_annexb(buffer, (size_t)buffer_size, &size);
  *frame_size = (int)size;
  return res;
}

EXPORT int aomshim_g1_counts(int *out) {
  out[0] = G1_CFG_DUMP_COUNT;
  out[1] = G1_OXCF_DUMP_COUNT;
  out[2] = G1_SEQ_DUMP_COUNT;
  out[3] = G1_CFG_SET_COUNT;
  out[4] = G1_EXTRA_SET_COUNT;
  return 5;
}
