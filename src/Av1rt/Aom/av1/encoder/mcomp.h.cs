// Port of libaom av1/encoder/mcomp.h
global using MV_COST_TYPE = int;
global using SUBPEL_FORCE_STOP = int;
global using static Av1rt.Aom.mcomp;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class mcomp
{
    // =============================================================================
    //  Cost functions
    // =============================================================================
    // enum MV_COST_TYPE
    internal const int MV_COST_ENTROPY = 0; // Use the entropy rate of the mv as the cost
    internal const int MV_COST_L1_LOWRES = 1; // Use the l1 norm of the mv as the cost (<480p)
    internal const int MV_COST_L1_MIDRES = 2; // Use the l1 norm of the mv as the cost (>=480p)
    internal const int MV_COST_L1_HDRES = 3; // Use the l1 norm of the mv as the cost (>=720p)
    internal const int MV_COST_NONE = 4; // Use 0 as as cost irrespective of the current mv

    // =============================================================================
    //  Motion Search
    // =============================================================================
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_set_ms_compound_refs(ref MSBuffers ms_buffers,
                                                  Ptr<byte> second_pred,
                                                  Ptr<byte> mask,
                                                  int mask_stride, int invert_mask)
    {
        ms_buffers.second_pred = second_pred;
        ms_buffers.mask = mask;
        ms_buffers.mask_stride = mask_stride;
        ms_buffers.inv_mask = invert_mask;
    }

    // Array to inform which all search methods are having
    // same candidates and different in number of search steps.
    internal static readonly SEARCH_METHODS[] search_method_lookup = { DIAMOND, NSTEP, NSTEP_8PT, CLAMPED_DIAMOND, HEX, BIGDIA, BIGDIA, BIGDIA, BIGDIA };

    // Reinitialize the search site config.
    // Only reached when the reference stride differs from the cached configs
    // (motion_search_facade.h av1_get_search_site_config), which is data-dependent.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_refresh_search_site_config(
        search_site_config[] ss_cfg_buf, SEARCH_METHODS search_method,
        int ref_stride)
    {
        int level =
            B(search_method == NSTEP_8PT || search_method == CLAMPED_DIAMOND);
        search_method = search_method_lookup[search_method];
        av1_init_motion_compensation[search_method](ss_cfg_buf[search_method],
                                                    ref_stride, level);
    }

    // Mv beyond the range do not produce new/different prediction block.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_set_mv_search_method(
        ref FULLPEL_MOTION_SEARCH_PARAMS ms_params,
        search_site_config[] search_sites /* [NUM_DISTINCT_SEARCH_METHODS] */,
        SEARCH_METHODS search_method)
    {
        ms_params.search_method = search_method;
        ms_params.search_sites =
            search_sites[search_method_lookup[ms_params.search_method]];
    }

    // Set up limit values for MV components.
    // Mv beyond the range do not produce new/different prediction block.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_set_mv_row_limits(
        CommonModeInfoParams mi_params, ref FullMvLimits mv_limits,
        int mi_row, int mi_height, int border)
    {
        int min1 = -(mi_row * MI_SIZE + border - 2 * AOM_INTERP_EXTEND);
        int min2 = -(((mi_row + mi_height) * MI_SIZE) + 2 * AOM_INTERP_EXTEND);
        mv_limits.row_min = AOMMAX(min1, min2);
        int max1 = (mi_params.mi_rows - mi_row - mi_height) * MI_SIZE +
                   border - 2 * AOM_INTERP_EXTEND;
        int max2 =
            (mi_params.mi_rows - mi_row) * MI_SIZE + 2 * AOM_INTERP_EXTEND;
        mv_limits.row_max = AOMMIN(max1, max2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_set_mv_col_limits(
        CommonModeInfoParams mi_params, ref FullMvLimits mv_limits,
        int mi_col, int mi_width, int border)
    {
        int min1 = -(mi_col * MI_SIZE + border - 2 * AOM_INTERP_EXTEND);
        int min2 = -(((mi_col + mi_width) * MI_SIZE) + 2 * AOM_INTERP_EXTEND);
        mv_limits.col_min = AOMMAX(min1, min2);
        int max1 = (mi_params.mi_cols - mi_col - mi_width) * MI_SIZE + border -
                   2 * AOM_INTERP_EXTEND;
        int max2 =
            (mi_params.mi_cols - mi_col) * MI_SIZE + 2 * AOM_INTERP_EXTEND;
        mv_limits.col_max = AOMMIN(max1, max2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_set_mv_limits(
        CommonModeInfoParams mi_params, ref FullMvLimits mv_limits,
        int mi_row, int mi_col, int mi_height, int mi_width, int border)
    {
        av1_set_mv_row_limits(mi_params, ref mv_limits, mi_row, mi_height, border);
        av1_set_mv_col_limits(mi_params, ref mv_limits, mi_col, mi_width, border);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_is_fullmv_in_range(in FullMvLimits mv_limits,
                                               FULLPEL_MV mv)
    {
        return B((mv.col >= mv_limits.col_min) && (mv.col <= mv_limits.col_max) &&
                 (mv.row >= mv_limits.row_min) && (mv.row <= mv_limits.row_max));
    }

    // =============================================================================
    //  Subpixel Motion Search
    // =============================================================================
    // enum SUBPEL_FORCE_STOP
    internal const int EIGHTH_PEL = 0;
    internal const int QUARTER_PEL = 1;
    internal const int HALF_PEL = 2;
    internal const int FULL_PEL = 3;

    // [subset] av1_set_fractional_mv: never used in the subset.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_set_subpel_mv_search_range(ref SubpelMvLimits subpel_limits,
                                                        in FullMvLimits mv_limits,
                                                        in MV ref_mv)
    {
        int max_mv = GET_MV_SUBPEL(MAX_FULL_PEL_VAL);
        int minc = AOMMAX(GET_MV_SUBPEL(mv_limits.col_min), ref_mv.col - max_mv);
        int maxc = AOMMIN(GET_MV_SUBPEL(mv_limits.col_max), ref_mv.col + max_mv);
        int minr = AOMMAX(GET_MV_SUBPEL(mv_limits.row_min), ref_mv.row - max_mv);
        int maxr = AOMMIN(GET_MV_SUBPEL(mv_limits.row_max), ref_mv.row + max_mv);

        maxc = AOMMAX(minc, maxc);
        maxr = AOMMAX(minr, maxr);

        subpel_limits.col_min = AOMMAX(MV_LOW + 1, minc);
        subpel_limits.col_max = AOMMIN(MV_UPP - 1, maxc);
        subpel_limits.row_min = AOMMAX(MV_LOW + 1, minr);
        subpel_limits.row_max = AOMMIN(MV_UPP - 1, maxr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_is_subpelmv_in_range(in SubpelMvLimits mv_limits,
                                                 MV mv)
    {
        return B((mv.col >= mv_limits.col_min) && (mv.col <= mv_limits.col_max) &&
                 (mv.row >= mv_limits.row_min) && (mv.row <= mv_limits.row_max));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_offset_from_fullmv(in FULLPEL_MV mv, int stride)
    {
        return mv.row * stride + mv.col;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<byte> get_buf_from_fullmv(in buf_2d buf,
                                                  in FULLPEL_MV mv)
    {
        return buf.buf + get_offset_from_fullmv(mv, buf.stride);
    }
}

// =============================================================================
//  Cost functions
// =============================================================================

// Struct: part of FULLPEL_/SUBPEL_MOTION_SEARCH_PARAMS, which are stack locals copied by value.
internal partial struct MV_COST_PARAMS
{
    // The reference mv used to compute the mv cost
    // PORT-NOTE: C stores `const MV *ref_mv`. Every in-scope producer points it at a caller
    // local / field that is not modified while the params are in use, so the MV is stored by
    // value.
    internal MV ref_mv;
    internal FULLPEL_MV full_ref_mv;
    internal MV_COST_TYPE mv_cost_type;
    internal Ptr<int> mvjcost;
    internal Arr2<Ptr<int>> mvcost; // const int *mvcost[2]
    internal int error_per_bit;
    // A multiplier used to convert rate to sad cost
    internal int sad_per_bit;
}

// =============================================================================
//  Motion Search
// =============================================================================
internal partial struct MSBuffers
{
    // The reference buffer
    // PORT-NOTE: C stores `const struct buf_2d *ref` (= &x->e_mbd.plane[0].pre[0]) and
    // `const struct buf_2d *src` (= &x->plane[0].src). The pointees are only read and are not
    // changed while a search runs, so the buf_2d is stored by value; producers assign
    // `ms_buffers.ref = xd.plane[0].pre[0];`.
    internal buf_2d @ref;

    // The source and predictors/mask used by translational search
    internal buf_2d src;
    internal Ptr<byte> second_pred;
    internal Ptr<byte> mask;
    internal int mask_stride;
    internal int inv_mask;

    // [subset] wsrc / obmc_mask (the weighted source and mask used by OBMC): OBMC is off.
}

// =============================================================================
//  Fullpixel Motion Search
// =============================================================================
// This struct holds fullpixel motion search parameters that should be constant
// during the search
// Struct (not class) although it is ~350 bytes: it is a stack local that C copies by value
// (FULLPEL_MOTION_SEARCH_PARAMS new_ms_params = *ms_params;), created per motion search.
// Pass as `in` (const *) or `ref`.
internal partial struct FULLPEL_MOTION_SEARCH_PARAMS
{
    internal BLOCK_SIZE bsize;
    // A function pointer to the simd function for fast computation
    // (C: const aom_variance_fn_ptr_t *, an element of cpi->ppi->fn_ptr[]).
    internal aom_variance_fn_ptr_t vfp;

    internal MSBuffers ms_buffers;

    // WARNING: search_method should be regarded as a private variable and should
    // not be modified directly so it is in sync with search_sites. To modify it,
    // use av1_set_mv_search_method.
    internal SEARCH_METHODS search_method;
    internal search_site_config search_sites;
    internal FullMvLimits mv_limits;

    internal int run_mesh_search;    // Sets mesh search unless it got pruned by
                                     // prune_mesh_search.
    internal int prune_mesh_search;  // Disables mesh search if the best_mv after a normal
                                     // search if close to the start_mv.
    internal int mesh_search_mv_diff_threshold;  // mv diff threshold to enable
                                                 // prune_mesh_search
    internal int force_mesh_thresh;  // Forces mesh search if the residue variance is
                                     // higher than the threshold.
    internal Arr2<Ptr<MESH_PATTERN>> mesh_patterns; // const struct MESH_PATTERN *mesh_patterns[2]

    // Use maximum search interval of 4 if true. This helps motion search to find
    // the best motion vector for screen content types.
    internal int fine_search_interval;

    internal int is_intra_mode;

    internal int fast_obmc_search;

    // For calculating mv cost
    internal MV_COST_PARAMS mv_cost_params;

    // Stores the function used to compute the sad. This can be different from the
    // sdf in vfp (e.g. downsampled sad and not sad) to allow speed up.
    internal aom_sad_fn_t sdf;
    internal aom_sad_multi_d_fn_t sdx4df;
    internal aom_sad_multi_d_fn_t sdx3df;
}

internal partial struct FULLPEL_MV_STATS
{
    internal int err_cost;
    internal uint distortion;
    internal uint sse;
}

/*! Function pointer to search site config initialization of different search
 * method functions. */
internal delegate void av1_init_search_site_config(search_site_config cfg, int stride,
                                                   int level);

// =============================================================================
//  Subpixel Motion Search
// =============================================================================
internal partial struct SUBPEL_SEARCH_VAR_PARAMS
{
    internal aom_variance_fn_ptr_t vfp;
    internal SUBPEL_SEARCH_TYPE subpel_search_type;
    // Source and reference buffers
    internal MSBuffers ms_buffers;
    internal int w, h;
}

// This struct holds subpixel motion search parameters that should be constant
// during the search
// Struct: stack local, created per search (see FULLPEL_MOTION_SEARCH_PARAMS).
internal partial struct SUBPEL_MOTION_SEARCH_PARAMS
{
    // High level motion search settings
    internal int allow_hp;
    internal Ptr<int> cost_list;
    internal SUBPEL_FORCE_STOP forced_stop;
    internal int iters_per_step;
    internal SubpelMvLimits mv_limits;

    // For calculating mv cost
    internal MV_COST_PARAMS mv_cost_params;

    // Distortion calculation params
    internal SUBPEL_SEARCH_VAR_PARAMS var_params;
}

// C: typedef int(fractional_mv_step_fp)(MACROBLOCKD *xd, const AV1_COMMON *const cm,
//        const SUBPEL_MOTION_SEARCH_PARAMS *ms_params, MV start_mv,
//        const FULLPEL_MV_STATS *start_mv_stats, MV *bestmv, int *distortion,
//        unsigned int *sse1, int_mv *last_mv_search_list);
// PORT-NOTE: start_mv_stats may be NULL in C, so it is passed as a nullable value
// (null == NULL).
internal delegate int fractional_mv_step_fp(MACROBLOCKD xd, AV1_COMMON cm,
                                            in SUBPEL_MOTION_SEARCH_PARAMS ms_params,
                                            MV start_mv,
                                            FULLPEL_MV_STATS? start_mv_stats,
                                            ref MV bestmv, ref int distortion,
                                            ref uint sse1,
                                            Ptr<int_mv> last_mv_search_list);
