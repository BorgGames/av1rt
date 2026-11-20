// Stubs for av1/encoder/encodeframe.c: delete this file when av1/encoder/encodeframe.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_encodeframe_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_encodeframe_c
{
    // ---- av1/encoder/encodeframe.c ------------------------------------------------------
    internal static void av1_setup_src_planes(MACROBLOCK x, YV12_BUFFER_CONFIG src,
                                              int mi_row, int mi_col, int num_planes,
                                              BLOCK_SIZE bsize) =>
        throw new NotImplementedException();

    // av1/encoder/encodeframe.c (f4), called from encoder.c (g1)
    internal static void av1_encode_frame(AV1_COMP cpi) =>
        throw new NotImplementedException();
}
