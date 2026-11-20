// Stubs for av1/common/cdef.c: delete this file when av1/common/cdef.c is ported.
global using static Av1rt.Aom.stubs_av1_common_cdef_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_common_cdef_c
{
    // av1/common/cdef.c (e4), called from encoder.c (g1)
    internal static void av1_cdef_frame(YV12_BUFFER_CONFIG frame, AV1_COMMON cm,
                                        MACROBLOCKD xd, cdef_init_fb_row_t cdef_init_fb_row_fn) =>
        throw new NotImplementedException();

    internal static void av1_cdef_init_fb_row(AV1_COMMON cm, MACROBLOCKD xd,
                                              CdefBlockInfo fb_info, Ptr<ushort>[] linebuf,
                                              Ptr<ushort> src, AV1CdefSync cdef_sync, int fbr) =>
        throw new NotImplementedException();
}
