// Stubs for av1/encoder/encodetxb.c: delete this file when av1/encoder/encodetxb.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_encodetxb_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_encodetxb_c
{
    // av1/encoder/encodetxb.c (f1)
    internal static void av1_alloc_txb_buf(AV1_COMP cpi) =>
        throw new NotImplementedException();

    internal static void av1_free_txb_buf(AV1_COMP cpi) =>
        throw new NotImplementedException();

    // f3: called by partition_search.c
    internal static void av1_update_intra_mb_txb_context(AV1_COMP cpi, ThreadData td,
                                                         RUN_TYPE dry_run, BLOCK_SIZE bsize,
                                                         byte allow_update_cdf) =>
        throw new NotImplementedException();

    internal static CB_COEFF_BUFFER av1_get_cb_coeff_buffer(AV1_COMP cpi, int mi_row, int mi_col) =>
        throw new NotImplementedException();
}
