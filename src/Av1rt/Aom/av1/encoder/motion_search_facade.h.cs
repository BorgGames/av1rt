// Port of libaom av1/encoder/motion_search_facade.h
global using static Av1rt.Aom.motion_search_facade;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class motion_search_facade
{
    internal const int NUM_JOINT_ME_REFINE_ITER = 2;
    internal const int REDUCED_JOINT_ME_REFINE_ITER = 1;

    // PORT-NOTE: C returns `const search_site_config *`, a pointer to the first element of a
    // search_site_config[NUM_DISTINCT_SEARCH_METHODS] array that callers index by search
    // method; the array itself is returned here.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static search_site_config[] av1_get_search_site_config(
        AV1_COMP cpi, MACROBLOCK x, SEARCH_METHODS search_method)
    {
        int ref_stride = x.e_mbd.plane[0].pre[0].stride;

        // AV1_COMP::mv_search_params.search_site_config is a compressor level cache
        // that's shared by multiple threads. In most cases where all frames have the
        // same resolution, the cache contains the search site config that we need.
        MotionVectorSearchParams mv_search_params = cpi.mv_search_params;
        if (ref_stride == mv_search_params.search_site_cfg[SS_CFG_SRC][0].stride)
        {
            return mv_search_params.search_site_cfg[SS_CFG_SRC];
        }
        else if (ref_stride ==
                 mv_search_params.search_site_cfg[SS_CFG_LOOKAHEAD][0].stride)
        {
            return mv_search_params.search_site_cfg[SS_CFG_LOOKAHEAD];
        }

        // If the cache does not contain the correct stride, then we will need to rely
        // on the thread level config MACROBLOCK::search_site_cfg_buf. If even the
        // thread level config doesn't match, then we need to update it.
        search_method = search_method_lookup[search_method];
        Debug.Assert(search_method_lookup[search_method] == search_method,
                     "The search_method_lookup table should be idempotent.");
        if (ref_stride != x.search_site_cfg_buf[search_method].stride)
        {
            av1_refresh_search_site_config(x.search_site_cfg_buf, search_method,
                                           ref_stride);
        }

        return x.search_site_cfg_buf;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SEARCH_METHODS av1_get_faster_search_method(
        SEARCH_METHODS search_method)
    {
        // Note on search method's accuracy:
        //  1. NSTEP
        //  2. DIAMOND
        //  3. BIGDIA
        //  4. HEX
        //  5. FAST_DIAMOND
        switch (search_method)
        {
            case NSTEP: return DIAMOND;
            case NSTEP_8PT: return DIAMOND;
            case DIAMOND: return BIGDIA;
            case CLAMPED_DIAMOND: return BIGDIA;
            case BIGDIA: return HEX;
            case HEX: return FAST_DIAMOND;
            case FAST_DIAMOND: return VFAST_DIAMOND;
            case FAST_BIGDIA: return FAST_BIGDIA;
            case VFAST_DIAMOND: return VFAST_DIAMOND;
            default: Debug.Assert(false, "Invalid search method!"); return DIAMOND;
        }
    }

    private static readonly int[] min_dim_th = { 128, 64, 32, 16 };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SEARCH_METHODS av1_get_default_mv_search_method(
        MACROBLOCK x, MV_SPEED_FEATURES mv_sf, BLOCK_SIZE bsize)
    {
        SEARCH_METHODS search_method = mv_sf.search_method;
        int sf_blk_search_method = mv_sf.use_bsize_dependent_search_method;
        int min_dim = Math.Min(block_size_wide[bsize], block_size_high[bsize]);
        int qband = x.qindex >> (QINDEX_BITS - 2);
        // C: function-local `const int min_dim_th[4]` (hoisted to a static table).
        bool use_faster_search_method = false;

        if (sf_blk_search_method >= 1 && sf_blk_search_method <= 3)
        {
            use_faster_search_method =
                (min_dim >= min_dim_th[sf_blk_search_method - 1]);
        }
        else if (sf_blk_search_method == 4)
        {
            use_faster_search_method =
                (min_dim >= min_dim_th[sf_blk_search_method - 1] &&
                 x.content_state_sb.source_sad_nonrd <= kMedSad && qband < 3);
        }

        if (use_faster_search_method)
        {
            search_method = av1_get_faster_search_method(search_method);
        }
        return search_method;
    }
}

// TODO(any): rename this struct to something else. There is already another
// struct called inter_modes_info, which makes this terribly confusing.
internal partial struct inter_mode_info
{
    internal long model_rd;
    internal int drl_cost;
    internal int_mv full_search_mv;
    internal int full_mv_rate;
    internal int full_mv_bestsme;
    internal uint pred_sse;
    internal int skip;
}
