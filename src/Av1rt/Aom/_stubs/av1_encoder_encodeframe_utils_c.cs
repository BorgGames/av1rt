// Stubs for av1/encoder/encodeframe_utils.c: delete this file when av1/encoder/encodeframe_utils.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_encodeframe_utils_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_encodeframe_utils_c
{
    // ---- av1/encoder/encodeframe_utils.c ------------------------------------------------
    internal static void av1_dealloc_src_diff_buf(MACROBLOCK mb, int num_planes) =>
        throw new NotImplementedException();
    internal static void av1_alloc_src_diff_buf(AV1_COMMON cm, MACROBLOCK mb) =>
        throw new NotImplementedException();

    // f3: called by partition_search.c
    internal static void av1_update_state(AV1_COMP cpi, ThreadData td, PICK_MODE_CONTEXT ctx,
                                          int mi_row, int mi_col, BLOCK_SIZE bsize,
                                          RUN_TYPE dry_run) =>
        throw new NotImplementedException();
}
