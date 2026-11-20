// Stubs for av1/encoder/nonrd_pickmode.c: delete this file when av1/encoder/nonrd_pickmode.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_nonrd_pickmode_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_nonrd_pickmode_c
{
    // ---- av1/encoder/nonrd_pickmode.c ---------------------------------------------------
    // f3: called by partition_search.c
    internal static void av1_nonrd_pick_intra_mode(AV1_COMP cpi, MACROBLOCK x, ref RD_STATS rd_cost,
                                                   BLOCK_SIZE bsize, PICK_MODE_CONTEXT ctx) =>
        throw new NotImplementedException();

    internal static void av1_nonrd_pick_inter_mode_sb(AV1_COMP cpi, TileDataEnc tile_data,
                                                      MACROBLOCK x, ref RD_STATS rd_cost,
                                                      BLOCK_SIZE bsize, PICK_MODE_CONTEXT ctx) =>
        throw new NotImplementedException();
}
