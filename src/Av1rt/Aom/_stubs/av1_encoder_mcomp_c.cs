// Stubs for av1/encoder/mcomp.c: delete this file when av1/encoder/mcomp.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_mcomp_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_mcomp_c
{
    // ---- av1/encoder/mcomp.c ------------------------------------------------------------
    // extern const av1_init_search_site_config
    //     av1_init_motion_compensation[NUM_DISTINCT_SEARCH_METHODS];
    internal static av1_init_search_site_config[] av1_init_motion_compensation =>
        throw new NotImplementedException();
    // extern fractional_mv_step_fp av1_find_best_sub_pixel_tree{,_pruned,_pruned_more};
    internal static int av1_find_best_sub_pixel_tree(
        MACROBLOCKD xd, AV1_COMMON cm, in SUBPEL_MOTION_SEARCH_PARAMS ms_params, MV start_mv,
        FULLPEL_MV_STATS? start_mv_stats, ref MV bestmv, ref int distortion, ref uint sse1,
        Ptr<int_mv> last_mv_search_list) =>
        throw new NotImplementedException();
    internal static int av1_find_best_sub_pixel_tree_pruned(
        MACROBLOCKD xd, AV1_COMMON cm, in SUBPEL_MOTION_SEARCH_PARAMS ms_params, MV start_mv,
        FULLPEL_MV_STATS? start_mv_stats, ref MV bestmv, ref int distortion, ref uint sse1,
        Ptr<int_mv> last_mv_search_list) =>
        throw new NotImplementedException();
    internal static int av1_find_best_sub_pixel_tree_pruned_more(
        MACROBLOCKD xd, AV1_COMMON cm, in SUBPEL_MOTION_SEARCH_PARAMS ms_params, MV start_mv,
        FULLPEL_MV_STATS? start_mv_stats, ref MV bestmv, ref int distortion, ref uint sse1,
        Ptr<int_mv> last_mv_search_list) =>
        throw new NotImplementedException();
}
