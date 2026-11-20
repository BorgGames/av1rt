// Stubs for av1/encoder/var_based_part.c: delete this file when av1/encoder/var_based_part.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_var_based_part_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_var_based_part_c
{
    // ---- av1/encoder/var_based_part.c (f3) ----------------------------------------------
    // C: const uint8_t *variance_low (callers pass &x->part_search_info.variance_low[0]).
    internal static int av1_get_force_skip_low_temp_var_small_sb(Ptr<byte> variance_low,
                                                                 int mi_row, int mi_col,
                                                                 BLOCK_SIZE bsize) =>
        throw new NotImplementedException();
    internal static int av1_get_force_skip_low_temp_var(Ptr<byte> variance_low, int mi_row,
                                                        int mi_col, BLOCK_SIZE bsize) =>
        throw new NotImplementedException();
}
