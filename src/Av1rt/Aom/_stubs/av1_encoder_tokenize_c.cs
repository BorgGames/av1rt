// Stubs for av1/encoder/tokenize.c: delete this file when av1/encoder/tokenize.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_tokenize_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_tokenize_c
{
    // ---- av1/encoder/tokenize.c ---------------------------------------------------------
    // f3: called by partition_search.c. `int *rate` may be NULL (encode_b_nonrd passes NULL)
    // and is only written through `if (rate)`, hence Ptr<int>.
    internal static void av1_tokenize_sb_vartx(AV1_COMP cpi, ThreadData td, RUN_TYPE dry_run,
                                               BLOCK_SIZE bsize, Ptr<int> rate,
                                               byte allow_update_cdf) =>
        throw new NotImplementedException();
}
