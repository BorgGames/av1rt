// Stubs for av1/encoder/encodemv.c: delete this file when av1/encoder/encodemv.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_encodemv_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_encodemv_c
{
    // ---- av1/encoder/encodemv.c ---------------------------------------------------------
    internal static void av1_find_best_ref_mvs_from_stack(int allow_hp,
                                                          MB_MODE_INFO_EXT mbmi_ext,
                                                          MV_REFERENCE_FRAME ref_frame,
                                                          ref int_mv nearest_mv,
                                                          ref int_mv near_mv,
                                                          int is_integer) =>
        throw new NotImplementedException();
    // C: int *mvcost[2].
    internal static void av1_build_nmv_cost_table(Ptr<int> mvjoint, Ptr<int>[] mvcost,
                                                  nmv_context mvctx,
                                                  MvSubpelPrecision precision) =>
        throw new NotImplementedException();
    internal static int_mv av1_get_ref_mv_from_stack(int ref_idx,
                                                     Ptr<MV_REFERENCE_FRAME> ref_frame,
                                                     int ref_mv_idx,
                                                     MB_MODE_INFO_EXT mbmi_ext) =>
        throw new NotImplementedException();
}
