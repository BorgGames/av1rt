// Stubs for av1/common/reconinter.c: delete this file when av1/common/reconinter.c is ported.
global using static Av1rt.Aom.stubs_av1_common_reconinter_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_common_reconinter_c
{
    // av1/common/reconinter.c: const wedge_params_type av1_wedge_params_lookup[BLOCK_SIZES_ALL]
    internal static wedge_params_type[] av1_wedge_params_lookup =>
        throw new NotImplementedException();

    // av1/common/reconinter.c (used by e4: cdef.c, thread_common.c, pickcdef.c)
    internal static void av1_setup_dst_planes(macroblockd_plane[] planes, BLOCK_SIZE bsize,
                                              YV12_BUFFER_CONFIG src, int mi_row, int mi_col,
                                              int plane_start, int plane_end) =>
        throw new NotImplementedException();
}
