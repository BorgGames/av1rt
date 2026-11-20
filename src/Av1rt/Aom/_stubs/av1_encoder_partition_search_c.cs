// Stubs for av1/encoder/partition_search.c: delete this file when av1/encoder/partition_search.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_partition_search_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_partition_search_c
{
    // av1/encoder/partition_search.c (f3)
    internal static void av1_set_offsets(AV1_COMP cpi, in TileInfo tile, MACROBLOCK x,
                                         int mi_row, int mi_col, BLOCK_SIZE bsize) =>
        throw new NotImplementedException();

    internal static void av1_nonrd_use_partition(AV1_COMP cpi, ThreadData td,
                                                 TileDataEnc tile_data, Ptr<MB_MODE_INFO> mib,
                                                 ref Ptr<TokenExtra> tp, int mi_row, int mi_col,
                                                 BLOCK_SIZE bsize, PC_TREE pc_tree) =>
        throw new NotImplementedException();
}
