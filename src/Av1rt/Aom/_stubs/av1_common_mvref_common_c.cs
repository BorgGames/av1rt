// Stubs for av1/common/mvref_common.c: delete this file when av1/common/mvref_common.c is ported.
global using static Av1rt.Aom.stubs_av1_common_mvref_common_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_common_mvref_common_c
{
    // ---- av1/common/mvref_common.c ------------------------------------------------------
    // C: uint8_t ref_mv_count[MODE_CTX_REF_FRAMES],
    //    CANDIDATE_MV ref_mv_stack[][MAX_REF_MV_STACK_SIZE],
    //    uint16_t ref_mv_weight[][MAX_REF_MV_STACK_SIZE],
    //    int_mv mv_ref_list[][MAX_MV_REF_CANDIDATES], int_mv *global_mvs,
    //    int16_t *mode_context
    internal static void av1_find_mv_refs(AV1_COMMON cm, MACROBLOCKD xd, MB_MODE_INFO mi,
                                          MV_REFERENCE_FRAME ref_frame,
                                          Ptr<byte> ref_mv_count,
                                          CANDIDATE_MV[][] ref_mv_stack,
                                          ushort[][] ref_mv_weight,
                                          int_mv[][] mv_ref_list, Ptr<int_mv> global_mvs,
                                          Ptr<short> mode_context) =>
        throw new NotImplementedException();
}
