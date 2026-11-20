// Stubs for aom/src/aom_encoder.c: delete this file when aom/src/aom_encoder.c is ported.
global using static Av1rt.Aom.stubs_aom_src_aom_encoder_c;

using System;

namespace Av1rt.Aom;

internal static class stubs_aom_src_aom_encoder_c
{
    // aom/src/aom_encoder.c. Called by the public API (src/Av1rt/Api/Av1Encoder.cs).
    // `aom_codec_enc_init(ctx, iface, cfg, flags)` is a C macro for
    // aom_codec_enc_init_ver(..., AOM_ENCODER_ABI_VERSION).
    internal static aom_codec_err_t aom_codec_enc_init_ver(aom_codec_ctx_t ctx, aom_codec_iface_t iface,
                                                           aom_codec_enc_cfg_t cfg, aom_codec_flags_t flags,
                                                           int ver) =>
        throw new NotImplementedException();

    internal static aom_codec_err_t aom_codec_enc_config_default(aom_codec_iface_t iface, aom_codec_enc_cfg_t cfg,
                                                                 uint usage) =>
        throw new NotImplementedException();

    // C: unsigned long duration (32 bits on the oracle build).
    internal static aom_codec_err_t aom_codec_encode(aom_codec_ctx_t ctx, aom_image_t img, aom_codec_pts_t pts,
                                                     uint duration, aom_enc_frame_flags_t flags) =>
        throw new NotImplementedException();

    // C: const aom_codec_cx_pkt_t *aom_codec_get_cx_data(aom_codec_ctx_t *ctx, aom_codec_iter_t *iter)
    internal static aom_codec_cx_pkt_t aom_codec_get_cx_data(aom_codec_ctx_t ctx, ref aom_codec_iter_t iter) =>
        throw new NotImplementedException();

    internal static aom_codec_err_t aom_codec_enc_config_set(aom_codec_ctx_t ctx, aom_codec_enc_cfg_t cfg) =>
        throw new NotImplementedException();
}
