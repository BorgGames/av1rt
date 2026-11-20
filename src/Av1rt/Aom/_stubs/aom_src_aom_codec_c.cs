// Stubs for aom/src/aom_codec.c: delete this file when aom/src/aom_codec.c is ported.
global using static Av1rt.Aom.stubs_aom_src_aom_codec_c;

using System;

namespace Av1rt.Aom;

internal static class stubs_aom_src_aom_codec_c
{
    // aom/src/aom_codec.c. Called by the public API (src/Av1rt/Api/Av1Encoder.cs).
    internal static aom_codec_err_t aom_codec_destroy(aom_codec_ctx_t ctx) =>
        throw new NotImplementedException();

    // C: aom_codec_err_t aom_codec_control(aom_codec_ctx_t *ctx, int ctrl_id, ...). The variadic
    // arguments arrive as object[] and are wrapped in a va_list for the control function. Pointer
    // arguments (int *, aom_image_t *) are passed as Ptr<int> / the class instance.
    internal static aom_codec_err_t aom_codec_control(aom_codec_ctx_t ctx, int ctrl_id, params object[] args) =>
        throw new NotImplementedException();
}
