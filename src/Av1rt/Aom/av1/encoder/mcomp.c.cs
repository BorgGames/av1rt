// Port of libaom av1/encoder/mcomp.c
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class mcomp
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void init_mv_cost_params(ref MV_COST_PARAMS mv_cost_params,
                                            MvCosts mv_costs,
                                            in MV ref_mv, int errorperbit,
                                            int sadperbit)
    {
        // PORT-NOTE: C stores the pointer ref_mv; the MV is copied (see MV_COST_PARAMS).
        mv_cost_params.ref_mv = ref_mv;
        mv_cost_params.full_ref_mv = get_fullmv_from_mv(ref_mv);
        mv_cost_params.mv_cost_type = MV_COST_ENTROPY;
        mv_cost_params.error_per_bit = errorperbit;
        mv_cost_params.sad_per_bit = sadperbit;
        // For allintra encoding mode, 'mv_costs' is not allocated. Hence, the
        // population of mvjcost and mvcost are avoided. In case of IntraBC, these
        // values are populated from 'dv_costs' in av1_set_ms_to_intra_mode().
        if (mv_costs != null)
        {
            mv_cost_params.mvjcost = mv_costs.nmv_joint_cost;
            mv_cost_params.mvcost[0] = mv_costs.mv_cost_stack[0];
            mv_cost_params.mvcost[1] = mv_costs.mv_cost_stack[1];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void init_ms_buffers(ref MSBuffers ms_buffers, MACROBLOCK x)
    {
        // PORT-NOTE: C stores &x->e_mbd.plane[0].pre[0] and &x->plane[0].src; the buf_2d
        // values are copied (see MSBuffers). No code between here and the searches that use
        // these params changes the pointees, and the search code only reads them.
        ms_buffers.@ref = x.e_mbd.plane[0].pre[0];
        ms_buffers.src = x.plane[0].src;

        av1_set_ms_compound_refs(ref ms_buffers, Ptr<byte>.Null, Ptr<byte>.Null, 0, 0);

        // [subset] ms_buffers->wsrc / obmc_mask (= x->obmc_buffer.wsrc / mask): OBMC is off
        // and the fields are not ported.
    }

    // [subset] av1_init_obmc_buffer: OBMC is off (never executed).

    internal static void av1_make_default_fullpel_ms_params(
        ref FULLPEL_MOTION_SEARCH_PARAMS ms_params, AV1_COMP cpi,
        MACROBLOCK x, BLOCK_SIZE bsize, in MV ref_mv, FULLPEL_MV start_mv,
        search_site_config[] search_sites /* [NUM_DISTINCT_SEARCH_METHODS] */,
        SEARCH_METHODS search_method, int fine_search_interval)
    {
        MV_SPEED_FEATURES mv_sf = cpi.sf.mv_sf;
        int is_key_frame =
            B(cpi.ppi.gf_group.update_type[cpi.gf_frame_index] == KF_UPDATE);

        // High level params
        ms_params.bsize = bsize;
        ms_params.vfp = cpi.ppi.fn_ptr[bsize];

        init_ms_buffers(ref ms_params.ms_buffers, x);

        av1_set_mv_search_method(ref ms_params, search_sites, search_method);

        ms_params.mesh_patterns[0] = mv_sf.mesh_patterns;
        ms_params.mesh_patterns[1] = mv_sf.intrabc_mesh_patterns;
        ms_params.force_mesh_thresh = mv_sf.exhaustive_searches_thresh;
        ms_params.prune_mesh_search =
            (cpi.sf.mv_sf.prune_mesh_search == PRUNE_MESH_SEARCH_LVL_2) ? 1 : 0;
        ms_params.mesh_search_mv_diff_threshold = 4;
        ms_params.run_mesh_search = 0;
        ms_params.fine_search_interval = fine_search_interval;

        ms_params.is_intra_mode = 0;

        ms_params.fast_obmc_search = mv_sf.obmc_full_pixel_search_level;

        ms_params.mv_limits = x.mv_limits;
        av1_set_mv_search_range(ref ms_params.mv_limits, ref_mv);

        if (cpi.oxcf.algo_cfg.sharpness == 3)
        {
            int top_margin = x.e_mbd.mi_row * MI_SIZE + 8;
            int left_margin = x.e_mbd.mi_col * MI_SIZE + 8;
            int bottom_margin =
                cpi.common.height - mi_size_high[bsize] * MI_SIZE - top_margin + 16;
            int right_margin =
                cpi.common.width - mi_size_wide[bsize] * MI_SIZE - left_margin + 16;

            bottom_margin = AOMMAX(bottom_margin, -top_margin);
            right_margin = AOMMAX(right_margin, -left_margin);

            ref FullMvLimits mv_limits = ref ms_params.mv_limits;
            mv_limits.row_min = AOMMAX(mv_limits.row_min, -top_margin);
            mv_limits.row_max = AOMMIN(mv_limits.row_max, bottom_margin);
            mv_limits.col_min = AOMMAX(mv_limits.col_min, -left_margin);
            mv_limits.col_max = AOMMIN(mv_limits.col_max, right_margin);
        }

        // Mvcost params
        init_mv_cost_params(ref ms_params.mv_cost_params, x.mv_costs, ref_mv,
                            x.errorperbit, x.sadperbit);

        ms_params.sdf = ms_params.vfp.sdf;
        ms_params.sdx4df = ms_params.vfp.sdx4df;
        ms_params.sdx3df = ms_params.vfp.sdx3df;

        if (mv_sf.use_downsampled_sad == 2 && block_size_high[bsize] >= 16)
        {
            Debug.Assert(ms_params.vfp.sdsf != null);
            ms_params.sdf = ms_params.vfp.sdsf;
            Debug.Assert(ms_params.vfp.sdsx4df != null);
            ms_params.sdx4df = ms_params.vfp.sdsx4df;
            // Skip version of sadx3 is not available yet
            ms_params.sdx3df = ms_params.vfp.sdsx4df;
        }
        else if (mv_sf.use_downsampled_sad == 1 && block_size_high[bsize] >= 16 &&
                 is_key_frame == 0)
        {
            FULLPEL_MV start_mv_clamped = start_mv;
            // adjust start_mv to make sure it is within MV range
            clamp_fullmv(ref start_mv_clamped, ms_params.mv_limits);

            ref readonly buf_2d @ref = ref ms_params.ms_buffers.@ref;
            int ref_stride = @ref.stride;
            Ptr<byte> best_address = get_buf_from_fullmv(@ref, start_mv_clamped);
            ref readonly buf_2d src = ref ms_params.ms_buffers.src;
            Ptr<byte> src_buf = src.buf;
            int src_stride = src.stride;

            uint start_mv_sad_even_rows, start_mv_sad_odd_rows;
            Debug.Assert(ms_params.vfp.sdsf != null);
            start_mv_sad_even_rows =
                ms_params.vfp.sdsf(src_buf, src_stride, best_address, ref_stride);
            start_mv_sad_odd_rows =
                ms_params.vfp.sdsf(src_buf + src_stride, src_stride,
                                   best_address + ref_stride, ref_stride);

            // If the absolute SAD difference computed between the pred-to-src of even
            // and odd rows is small, skip every other row in sad computation.
            int odd_to_even_diff_sad =
                Math.Abs((int)start_mv_sad_even_rows - (int)start_mv_sad_odd_rows);
            const int mult_thresh = 4;
            if (odd_to_even_diff_sad * mult_thresh < (int)start_mv_sad_even_rows)
            {
                ms_params.sdf = ms_params.vfp.sdsf;
                Debug.Assert(ms_params.vfp.sdsx4df != null);
                ms_params.sdx4df = ms_params.vfp.sdsx4df;
                ms_params.sdx3df = ms_params.vfp.sdsx4df;
            }
        }
    }

    // [subset] av1_set_ms_to_intra_mode: intra block copy is off (never executed).

    internal static void av1_make_default_subpel_ms_params(ref SUBPEL_MOTION_SEARCH_PARAMS ms_params,
                                                           AV1_COMP cpi,
                                                           MACROBLOCK x, BLOCK_SIZE bsize,
                                                           in MV ref_mv, Ptr<int> cost_list)
    {
        AV1_COMMON cm = cpi.common;
        // High level params
        ms_params.allow_hp = B(cm.features.allow_high_precision_mv);
        ms_params.forced_stop = cpi.sf.mv_sf.subpel_force_stop;
        ms_params.iters_per_step = cpi.sf.mv_sf.subpel_iters_per_step;
        ms_params.cost_list = cond_cost_list_const(cpi, cost_list);

        av1_set_subpel_mv_search_range(ref ms_params.mv_limits, x.mv_limits, ref_mv);

        if (cpi.oxcf.algo_cfg.sharpness == 3)
        {
            int top_margin = GET_MV_SUBPEL(x.e_mbd.mi_row * MI_SIZE + 8);
            int left_margin = GET_MV_SUBPEL(x.e_mbd.mi_col * MI_SIZE + 8);
            int bottom_margin =
                GET_MV_SUBPEL(cpi.common.height - mi_size_high[bsize] * MI_SIZE -
                              x.e_mbd.mi_row * MI_SIZE + 8);
            int right_margin =
                GET_MV_SUBPEL(cpi.common.width - mi_size_wide[bsize] * MI_SIZE -
                              x.e_mbd.mi_col * MI_SIZE + 8);

            bottom_margin = AOMMAX(bottom_margin, -top_margin);
            right_margin = AOMMAX(right_margin, -left_margin);

            ref SubpelMvLimits mv_limits = ref ms_params.mv_limits;
            mv_limits.row_min = AOMMAX(mv_limits.row_min, -top_margin);
            mv_limits.row_max = AOMMIN(mv_limits.row_max, bottom_margin);
            mv_limits.col_min = AOMMAX(mv_limits.col_min, -left_margin);
            mv_limits.col_max = AOMMIN(mv_limits.col_max, right_margin);
        }

        // Mvcost params
        init_mv_cost_params(ref ms_params.mv_cost_params, x.mv_costs, ref_mv,
                            x.errorperbit, x.sadperbit);

        // Subpel variance params
        ms_params.var_params.vfp = cpi.ppi.fn_ptr[bsize];
        ms_params.var_params.subpel_search_type =
            cpi.sf.mv_sf.use_accurate_subpel_search;
        ms_params.var_params.w = block_size_wide[bsize];
        ms_params.var_params.h = block_size_high[bsize];

        // Ref and src buffers
        init_ms_buffers(ref ms_params.var_params.ms_buffers, x);
    }

    internal static void av1_set_mv_search_range(ref FullMvLimits mv_limits, in MV mv)
    {
        // Calculate the outermost full-pixel MVs which are inside the limits set by
        // av1_set_subpel_mv_search_range().
        //
        // The subpel limits are simply mv->col +/- 8*MAX_FULL_PEL_VAL, and similar
        // for mv->row. We can then divide by 8 to find the fullpel MV limits. But
        // we have to be careful about the rounding. We want these bounds to be
        // at least as tight as the subpel limits, which means that we must round
        // the minimum values up and the maximum values down when dividing.
        int col_min = ((mv.col + 7) >> 3) - MAX_FULL_PEL_VAL;
        int row_min = ((mv.row + 7) >> 3) - MAX_FULL_PEL_VAL;
        int col_max = (mv.col >> 3) + MAX_FULL_PEL_VAL;
        int row_max = (mv.row >> 3) + MAX_FULL_PEL_VAL;

        col_min = AOMMAX(col_min, (MV_LOW >> 3) + 1);
        row_min = AOMMAX(row_min, (MV_LOW >> 3) + 1);
        col_max = AOMMIN(col_max, (MV_UPP >> 3) - 1);
        row_max = AOMMIN(row_max, (MV_UPP >> 3) - 1);

        // Get intersection of UMV window and valid MV window to reduce # of checks
        // in diamond search.
        mv_limits.col_min = AOMMAX(mv_limits.col_min, col_min);
        mv_limits.col_max = AOMMIN(mv_limits.col_max, col_max);
        mv_limits.row_min = AOMMAX(mv_limits.row_min, row_min);
        mv_limits.row_max = AOMMIN(mv_limits.row_max, row_max);

        mv_limits.col_max = AOMMAX(mv_limits.col_min, mv_limits.col_max);
        mv_limits.row_max = AOMMAX(mv_limits.row_min, mv_limits.row_max);
    }

    internal static int av1_init_search_range(int size)
    {
        int sr = 0;
        // Minimum search size no matter what the passed in value.
        size = AOMMAX(16, size);

        while ((size << sr) < MAX_FULL_PEL_VAL) sr++;

        sr = AOMMIN(sr, MAX_MVSEARCH_STEPS - 2);
        return sr;
    }

    // ============================================================================
    //  Cost of motion vectors
    // ============================================================================
    // TODO(any): Adaptively adjust the regularization strength based on image size
    // and motion activity instead of using hard-coded values. It seems like we
    // roughly half the lambda for each increase in resolution
    // These are multiplier used to perform regularization in motion compensation
    // when x->mv_cost_type is set to MV_COST_L1.
    // LOWRES
    private const int SSE_LAMBDA_LOWRES = 2;   // Used by mv_cost_err_fn
    private const int SAD_LAMBDA_LOWRES = 32;  // Used by mvsad_err_cost during full pixel search
    // MIDRES
    private const int SSE_LAMBDA_MIDRES = 0;   // Used by mv_cost_err_fn
    private const int SAD_LAMBDA_MIDRES = 15;  // Used by mvsad_err_cost during full pixel search
    // HDRES
    private const int SSE_LAMBDA_HDRES = 1;  // Used by mv_cost_err_fn
    private const int SAD_LAMBDA_HDRES = 8;  // Used by mvsad_err_cost during full pixel search

    // Returns the rate of encoding the current motion vector based on the
    // joint_cost and comp_cost. joint_costs covers the cost of transmitting
    // JOINT_MV, and comp_cost covers the cost of transmitting the actual motion
    // vector.
    // C: `const int *const comp_cost[2]`; callers pass either an `int *[2]` array
    // (Ptr<int>[]) or MV_COST_PARAMS::mvcost (Arr2<Ptr<int>>), hence the span.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int mv_cost(in MV mv, Ptr<int> joint_cost,
                               ReadOnlySpan<Ptr<int>> comp_cost)
    {
        return joint_cost[av1_get_mv_joint(mv)] + comp_cost[0][mv.row] +
               comp_cost[1][mv.col];
    }

    // Returns the cost of encoding the motion vector diff := *mv - *ref. The cost
    // is defined as the rate required to encode diff * weight, rounded to the
    // nearest 2 ** 7.
    // This is NOT used during motion compensation.
    internal static int av1_mv_bit_cost(in MV mv, in MV ref_mv, Ptr<int> mvjcost,
                                        Ptr<int>[] mvcost, int weight)
    {
        MV diff = new((short)(mv.row - ref_mv.row), (short)(mv.col - ref_mv.col));
        return ROUND_POWER_OF_TWO(
            mv_cost(diff, mvjcost, mvcost) * weight, 7);
    }

    // Returns the cost of using the current mv during the motion search. This is
    // used when var is used as the error metric.
    private const int PIXEL_TRANSFORM_ERROR_SCALE = 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int mv_err_cost(in MV mv, in MV ref_mv,
                                   Ptr<int> mvjcost, ReadOnlySpan<Ptr<int>> mvcost,
                                   int error_per_bit, MV_COST_TYPE mv_cost_type)
    {
        MV diff = new((short)(mv.row - ref_mv.row), (short)(mv.col - ref_mv.col));
        MV abs_diff = new((short)Math.Abs((int)diff.row), (short)Math.Abs((int)diff.col));

        switch (mv_cost_type)
        {
            case MV_COST_ENTROPY:
                // C: `if (mvcost)`; mvcost is MV_COST_PARAMS::mvcost, an array, so the test is
                // always true.
                return (int)ROUND_POWER_OF_TWO_64(
                    (long)mv_cost(diff, mvjcost, mvcost) * error_per_bit,
                    RDDIV_BITS + AV1_PROB_COST_SHIFT - RD_EPB_SHIFT +
                        PIXEL_TRANSFORM_ERROR_SCALE);
            case MV_COST_L1_LOWRES:
                return (SSE_LAMBDA_LOWRES * (abs_diff.row + abs_diff.col)) >> 3;
            case MV_COST_L1_MIDRES:
                return (SSE_LAMBDA_MIDRES * (abs_diff.row + abs_diff.col)) >> 3;
            case MV_COST_L1_HDRES:
                return (SSE_LAMBDA_HDRES * (abs_diff.row + abs_diff.col)) >> 3;
            case MV_COST_NONE: return 0;
            default: Debug.Assert(false, "Invalid rd_cost_type"); return 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int mv_err_cost_(in MV mv,
                                    in MV_COST_PARAMS mv_cost_params)
    {
        if (mv_cost_params.mv_cost_type == MV_COST_NONE)
        {
            return 0;
        }
        return mv_err_cost(mv, mv_cost_params.ref_mv, mv_cost_params.mvjcost,
                           mv_cost_params.mvcost, mv_cost_params.error_per_bit,
                           mv_cost_params.mv_cost_type);
    }

    // Returns the cost of using the current mv during the motion search. This is
    // only used during full pixel motion search when sad is used as the error
    // metric
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int mvsad_err_cost(in FULLPEL_MV mv, in FULLPEL_MV ref_mv,
                                      Ptr<int> mvjcost, ReadOnlySpan<Ptr<int>> mvcost,
                                      int sad_per_bit, MV_COST_TYPE mv_cost_type)
    {
        switch (mv_cost_type)
        {
            case MV_COST_ENTROPY:
                {
                    MV diff = new((short)GET_MV_SUBPEL(mv.row - ref_mv.row),
                                  (short)GET_MV_SUBPEL(mv.col - ref_mv.col));
                    return (int)ROUND_POWER_OF_TWO(
                        (uint)mv_cost(diff, mvjcost, mvcost) * (uint)sad_per_bit,
                        AV1_PROB_COST_SHIFT);
                }
            case MV_COST_L1_LOWRES:
                return SAD_LAMBDA_LOWRES *
                       (Math.Abs(mv.row - ref_mv.row) + Math.Abs(mv.col - ref_mv.col));
            case MV_COST_L1_MIDRES:
                return SAD_LAMBDA_MIDRES *
                       (Math.Abs(mv.row - ref_mv.row) + Math.Abs(mv.col - ref_mv.col));
            case MV_COST_L1_HDRES:
                return SAD_LAMBDA_HDRES *
                       (Math.Abs(mv.row - ref_mv.row) + Math.Abs(mv.col - ref_mv.col));
            case MV_COST_NONE: return 0;
            default: Debug.Assert(false, "Invalid rd_cost_type"); return 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int mvsad_err_cost_(in FULLPEL_MV mv,
                                       in MV_COST_PARAMS mv_cost_params)
    {
        return mvsad_err_cost(mv, mv_cost_params.full_ref_mv,
                              mv_cost_params.mvjcost, mv_cost_params.mvcost,
                              mv_cost_params.sad_per_bit,
                              mv_cost_params.mv_cost_type);
    }

    // =============================================================================
    //  Fullpixel Motion Search: Translational
    // =============================================================================
    private const int MAX_PATTERN_SCALES = 11;
    private const int MAX_PATTERN_CANDIDATES = 8;  // max number of candidates per scale
    private const int PATTERN_CANDIDATES_REF = 3;  // number of refinement candidates

    // Search site initialization for DIAMOND / CLAMPED_DIAMOND search methods.
    // level = 0: DIAMOND, level = 1: CLAMPED_DIAMOND.
    private static void init_dsmotion_compensation(search_site_config cfg, int stride,
                                                   int level)
    {
        int num_search_steps = 0;
        int stage_index = MAX_MVSEARCH_STEPS - 1;

        cfg.site[stage_index][0].mv.col = cfg.site[stage_index][0].mv.row = 0;
        cfg.site[stage_index][0].offset = 0;
        cfg.stride = stride;

        // Choose the initial step size depending on level.
        int first_step = (level > 0) ? (MAX_FIRST_STEP / 4) : MAX_FIRST_STEP;

        for (int radius = first_step; radius > 0;)
        {
            int num_search_pts = 8;

            // C: `const FULLPEL_MV search_site_mvs[13]`; the trailing elements are zero.
            FULLPEL_MV[] search_site_mvs = {
                new(0, 0),                               new((short)-radius, 0),                new((short)radius, 0),
                new(0, (short)-radius),                  new(0, (short)radius),                 new((short)-radius, (short)-radius),
                new((short)radius, (short)radius),       new((short)-radius, (short)radius),    new((short)radius, (short)-radius),
                default, default, default, default,
            };

            int i;
            for (i = 0; i <= num_search_pts; ++i)
            {
                ref search_site site = ref cfg.site[stage_index][i];
                site.mv = search_site_mvs[i];
                site.offset = get_offset_from_fullmv(site.mv, stride);
            }
            cfg.searches_per_step[stage_index] = num_search_pts;
            cfg.radius[stage_index] = radius;
            // Update the search radius based on level.
            if (level == 0 || ((stage_index < 9) && level != 0)) radius /= 2;
            --stage_index;
            ++num_search_steps;
        }
        cfg.num_search_steps = num_search_steps;
    }

    internal static void av1_init_motion_fpf(search_site_config cfg, int stride)
    {
        int num_search_steps = 0;
        int stage_index = MAX_MVSEARCH_STEPS - 1;

        cfg.site[stage_index][0].mv.col = cfg.site[stage_index][0].mv.row = 0;
        cfg.site[stage_index][0].offset = 0;
        cfg.stride = stride;

        for (int radius = MAX_FIRST_STEP; radius > 0; radius /= 2)
        {
            // Generate offsets for 8 search sites per step.
            int tan_radius = AOMMAX((int)(0.41 * radius), 1);
            int num_search_pts = 12;
            if (radius == 1) num_search_pts = 8;

            FULLPEL_MV[] search_site_mvs = {
                new(0, 0),
                new((short)-radius, 0),
                new((short)radius, 0),
                new(0, (short)-radius),
                new(0, (short)radius),
                new((short)-radius, (short)-tan_radius),
                new((short)radius, (short)tan_radius),
                new((short)-tan_radius, (short)radius),
                new((short)tan_radius, (short)-radius),
                new((short)-radius, (short)tan_radius),
                new((short)radius, (short)-tan_radius),
                new((short)tan_radius, (short)radius),
                new((short)-tan_radius, (short)-radius),
            };

            int i;
            for (i = 0; i <= num_search_pts; ++i)
            {
                ref search_site site = ref cfg.site[stage_index][i];
                site.mv = search_site_mvs[i];
                site.offset = get_offset_from_fullmv(site.mv, stride);
            }
            cfg.searches_per_step[stage_index] = num_search_pts;
            cfg.radius[stage_index] = radius;
            --stage_index;
            ++num_search_steps;
        }
        cfg.num_search_steps = num_search_steps;
    }

    // Search site initialization for NSTEP / NSTEP_8PT search methods.
    // level = 0: NSTEP, level = 1: NSTEP_8PT.
    private static void init_motion_compensation_nstep(search_site_config cfg, int stride,
                                                       int level)
    {
        int num_search_steps = 0;
        int stage_index = 0;
        cfg.stride = stride;
        int radius = 1;
        int num_stages = (level > 0) ? 16 : 15;
        for (stage_index = 0; stage_index < num_stages; ++stage_index)
        {
            int tan_radius = AOMMAX((int)(0.41 * radius), 1);
            int num_search_pts = 12;
            if ((radius <= 5) || (level > 0))
            {
                tan_radius = radius;
                num_search_pts = 8;
            }
            FULLPEL_MV[] search_site_mvs = {
                new(0, 0),
                new((short)-radius, 0),
                new((short)radius, 0),
                new(0, (short)-radius),
                new(0, (short)radius),
                new((short)-radius, (short)-tan_radius),
                new((short)radius, (short)tan_radius),
                new((short)-tan_radius, (short)radius),
                new((short)tan_radius, (short)-radius),
                new((short)-radius, (short)tan_radius),
                new((short)radius, (short)-tan_radius),
                new((short)tan_radius, (short)radius),
                new((short)-tan_radius, (short)-radius),
            };

            for (int i = 0; i <= num_search_pts; ++i)
            {
                ref search_site site = ref cfg.site[stage_index][i];
                site.mv = search_site_mvs[i];
                site.offset = get_offset_from_fullmv(site.mv, stride);
            }
            cfg.searches_per_step[stage_index] = num_search_pts;
            cfg.radius[stage_index] = radius;
            ++num_search_steps;
            if (stage_index < 12)
                radius = (int)AOMMAX((radius * 1.5 + 0.5), (double)(radius + 1));
        }
        cfg.num_search_steps = num_search_steps;
    }

    // Search site initialization for BIGDIA / FAST_BIGDIA / FAST_DIAMOND
    // search methods.
    // First scale has 4-closest points, the rest have 8 points in diamond
    // shape at increasing scales
    private static readonly int[] bigdia_num_candidates = {
        4, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
    };

    // BIGDIA search method candidates.
    // Note that the largest candidate step at each scale is 2^scale
    // (generated from mcomp.c by script)
    private static readonly FULLPEL_MV[][] site_candidates = {
        new FULLPEL_MV[] { new(0, -1), new(1, 0), new(0, 1), new(-1, 0), new(0, 0), new(0, 0), new(0, 0), new(0, 0) },
        new FULLPEL_MV[] { new(-1, -1), new(0, -2), new(1, -1), new(2, 0), new(1, 1), new(0, 2), new(-1, 1), new(-2, 0) },
        new FULLPEL_MV[] { new(-2, -2), new(0, -4), new(2, -2), new(4, 0), new(2, 2), new(0, 4), new(-2, 2), new(-4, 0) },
        new FULLPEL_MV[] { new(-4, -4), new(0, -8), new(4, -4), new(8, 0), new(4, 4), new(0, 8), new(-4, 4), new(-8, 0) },
        new FULLPEL_MV[] { new(-8, -8), new(0, -16), new(8, -8), new(16, 0), new(8, 8), new(0, 16), new(-8, 8), new(-16, 0) },
        new FULLPEL_MV[] { new(-16, -16), new(0, -32), new(16, -16), new(32, 0), new(16, 16), new(0, 32), new(-16, 16), new(-32, 0) },
        new FULLPEL_MV[] { new(-32, -32), new(0, -64), new(32, -32), new(64, 0), new(32, 32), new(0, 64), new(-32, 32), new(-64, 0) },
        new FULLPEL_MV[] { new(-64, -64), new(0, -128), new(64, -64), new(128, 0), new(64, 64), new(0, 128), new(-64, 64), new(-128, 0) },
        new FULLPEL_MV[] { new(-128, -128), new(0, -256), new(128, -128), new(256, 0), new(128, 128), new(0, 256), new(-128, 128), new(-256, 0) },
        new FULLPEL_MV[] { new(-256, -256), new(0, -512), new(256, -256), new(512, 0), new(256, 256), new(0, 512), new(-256, 256), new(-512, 0) },
        new FULLPEL_MV[] { new(-512, -512), new(0, -1024), new(512, -512), new(1024, 0), new(512, 512), new(0, 1024), new(-512, 512), new(-1024, 0) },
    };

    private static void init_motion_compensation_bigdia(search_site_config cfg, int stride,
                                                        int level)
    {
        _ = level;
        cfg.stride = stride;
        // C: function-local statics bigdia_num_candidates / site_candidates (hoisted).
        int radius = 1;
        for (int i = 0; i < MAX_PATTERN_SCALES; ++i)
        {
            cfg.searches_per_step[i] = bigdia_num_candidates[i];
            cfg.radius[i] = radius;
            for (int j = 0; j < MAX_PATTERN_CANDIDATES; ++j)
            {
                ref search_site site = ref cfg.site[i][j];
                site.mv = site_candidates[i][j];
                site.offset = get_offset_from_fullmv(site.mv, stride);
            }
            radius *= 2;
        }
        cfg.num_search_steps = MAX_PATTERN_SCALES;
    }

    // Search site initialization for HEX search method.
    // First scale has 8-closest points, the rest have 6 points in hex shape
    // at increasing scales.
    private static readonly int[] hex_num_candidates = { 8, 6, 6, 6, 6, 6,
                                                         6, 6, 6, 6, 6 };
    // Note that the largest candidate step at each scale is 2^scale.
    // (generated from mcomp.c by script; C pads the 6-entry rows to 8 with zeros, which are
    // never read)
    private static readonly FULLPEL_MV[][] hex_candidates = {
        new FULLPEL_MV[] { new(-1, -1), new(0, -1), new(1, -1), new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0) },
        new FULLPEL_MV[] { new(-1, -2), new(1, -2), new(2, 0), new(1, 2), new(-1, 2), new(-2, 0) },
        new FULLPEL_MV[] { new(-2, -4), new(2, -4), new(4, 0), new(2, 4), new(-2, 4), new(-4, 0) },
        new FULLPEL_MV[] { new(-4, -8), new(4, -8), new(8, 0), new(4, 8), new(-4, 8), new(-8, 0) },
        new FULLPEL_MV[] { new(-8, -16), new(8, -16), new(16, 0), new(8, 16), new(-8, 16), new(-16, 0) },
        new FULLPEL_MV[] { new(-16, -32), new(16, -32), new(32, 0), new(16, 32), new(-16, 32), new(-32, 0) },
        new FULLPEL_MV[] { new(-32, -64), new(32, -64), new(64, 0), new(32, 64), new(-32, 64), new(-64, 0) },
        new FULLPEL_MV[] { new(-64, -128), new(64, -128), new(128, 0), new(64, 128), new(-64, 128), new(-128, 0) },
        new FULLPEL_MV[] { new(-128, -256), new(128, -256), new(256, 0), new(128, 256), new(-128, 256), new(-256, 0) },
        new FULLPEL_MV[] { new(-256, -512), new(256, -512), new(512, 0), new(256, 512), new(-256, 512), new(-512, 0) },
        new FULLPEL_MV[] { new(-512, -1024), new(512, -1024), new(1024, 0), new(512, 1024), new(-512, 1024), new(-1024, 0) },
    };

    private static void init_motion_compensation_hex(search_site_config cfg, int stride,
                                                     int level)
    {
        _ = level;
        cfg.stride = stride;
        // C: function-local statics hex_num_candidates / hex_candidates (hoisted).
        int radius = 1;
        for (int i = 0; i < MAX_PATTERN_SCALES; ++i)
        {
            cfg.searches_per_step[i] = hex_num_candidates[i];
            cfg.radius[i] = radius;
            for (int j = 0; j < hex_num_candidates[i]; ++j)
            {
                ref search_site site = ref cfg.site[i][j];
                site.mv = hex_candidates[i][j];
                site.offset = get_offset_from_fullmv(site.mv, stride);
            }
            radius *= 2;
        }
        cfg.num_search_steps = MAX_PATTERN_SCALES;
    }

    internal static readonly av1_init_search_site_config[]
        av1_init_motion_compensation = {
            init_dsmotion_compensation,     init_motion_compensation_nstep,
            init_motion_compensation_nstep, init_dsmotion_compensation,
            init_motion_compensation_hex,   init_motion_compensation_bigdia
        };

    // Checks whether the mv is within range of the mv_limits
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int check_bounds(in FullMvLimits mv_limits, int row, int col,
                                    int range)
    {
        return B((row - range) >= mv_limits.row_min) &
               B((row + range) <= mv_limits.row_max) &
               B((col - range) >= mv_limits.col_min) &
               B((col + range) <= mv_limits.col_max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int get_mvpred_var_cost(
        in FULLPEL_MOTION_SEARCH_PARAMS ms_params, in FULLPEL_MV this_mv,
        ref FULLPEL_MV_STATS mv_stats)
    {
        aom_variance_fn_ptr_t vfp = ms_params.vfp;
        MV sub_this_mv = get_mv_from_fullmv(this_mv);
        ref readonly buf_2d src = ref ms_params.ms_buffers.src;
        ref readonly buf_2d @ref = ref ms_params.ms_buffers.@ref;
        Ptr<byte> src_buf = src.buf;
        int src_stride = src.stride;
        int ref_stride = @ref.stride;

        int bestsme;

        bestsme = (int)vfp.vf(src_buf, src_stride, get_buf_from_fullmv(@ref, this_mv),
                              ref_stride, ref mv_stats.sse);
        mv_stats.distortion = (uint)bestsme;

        mv_stats.err_cost = mv_err_cost_(sub_this_mv, ms_params.mv_cost_params);
        bestsme += mv_stats.err_cost;

        return bestsme;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int get_mvpred_sad(in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
                                      in buf_2d src,
                                      Ptr<byte> ref_address,
                                      int ref_stride)
    {
        Ptr<byte> src_buf = src.buf;
        int src_stride = src.stride;

        return (int)ms_params.sdf(src_buf, src_stride, ref_address, ref_stride);
    }

    // [subset] get_mvpred_compound_var_cost / get_mvpred_compound_sad: only called from the
    // diamond / refining / exhaustive searches (NSTEP, DIAMOND), which the subset never uses.

    // Calculates and returns a sad+mvcost list around an integer best pel during
    // fullpixel motion search. The resulting list can be used to speed up subpel
    // motion search later.
    private const int USE_SAD_COSTLIST = 1;

    // [subset] calc_int_cost_list (var-based cost list): USE_SAD_COSTLIST is 1, so it is
    // compiled but never called.

    private static readonly FULLPEL_MV[] neighbors = {
        new(0, -1), new(1, 0), new(0, 1), new(-1, 0)
    };

    // calc_int_sad_list uses sad to populate the costlist, which is less accurate
    // than var but faster.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void calc_int_sad_list(
        FULLPEL_MV best_mv, in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
        Ptr<int> cost_list, int costlist_has_sad)
    {
        // C: function-local static `neighbors` (hoisted).
        ref readonly buf_2d src = ref ms_params.ms_buffers.src;
        ref readonly buf_2d @ref = ref ms_params.ms_buffers.@ref;
        int ref_stride = @ref.stride;
        int br = best_mv.row;
        int bc = best_mv.col;

        Debug.Assert(av1_is_fullmv_in_range(ms_params.mv_limits, best_mv) != 0);

        // Refresh the costlist it does not contain valid sad
        if (costlist_has_sad == 0)
        {
            cost_list[0] = get_mvpred_sad(
                ms_params, src, get_buf_from_fullmv(@ref, best_mv), ref_stride);

            if (check_bounds(ms_params.mv_limits, br, bc, 1) != 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    FULLPEL_MV this_mv = new((short)(br + neighbors[i].row),
                                             (short)(bc + neighbors[i].col));
                    cost_list[i + 1] = get_mvpred_sad(
                        ms_params, src, get_buf_from_fullmv(@ref, this_mv), ref_stride);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    FULLPEL_MV this_mv = new((short)(br + neighbors[i].row),
                                             (short)(bc + neighbors[i].col));
                    if (av1_is_fullmv_in_range(ms_params.mv_limits, this_mv) == 0)
                    {
                        cost_list[i + 1] = int.MaxValue;
                    }
                    else
                    {
                        cost_list[i + 1] = get_mvpred_sad(
                            ms_params, src, get_buf_from_fullmv(@ref, this_mv), ref_stride);
                    }
                }
            }
        }

        ref readonly MV_COST_PARAMS mv_cost_params = ref ms_params.mv_cost_params;
        cost_list[0] += mvsad_err_cost_(best_mv, mv_cost_params);

        for (int idx = 0; idx < 4; idx++)
        {
            if (cost_list[idx + 1] != int.MaxValue)
            {
                FULLPEL_MV this_mv = new((short)(br + neighbors[idx].row),
                                         (short)(bc + neighbors[idx].col));
                cost_list[idx + 1] += mvsad_err_cost_(this_mv, mv_cost_params);
            }
        }
    }

    // Computes motion vector cost and adds to the sad cost.
    // Then updates the best sad and motion vectors.
    // Inputs:
    //   this_sad: the sad to be evaluated.
    //   mv: the current motion vector.
    //   mv_cost_params: a structure containing information to compute mv cost.
    //   best_sad: the current best sad.
    //   raw_best_sad (optional): the current best sad without calculating mv cost.
    //   best_mv: the current best motion vector.
    //   second_best_mv (optional): the second best motion vector up to now.
    // Modifies:
    //   best_sad, raw_best_sad, best_mv, second_best_mv
    //   If the current sad is lower than the current best sad.
    // Returns:
    //   Whether the input sad (mv) is better than the current best.
    // PORT-NOTE: every ported caller passes a valid raw_best_sad and second_best_mv == NULL
    // (only the dropped diamond search passes a second_best_mv), so raw_best_sad is a ref and
    // the second_best_mv parameter is omitted.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int update_mvs_and_sad(uint this_sad,
                                          in FULLPEL_MV mv,
                                          in MV_COST_PARAMS mv_cost_params,
                                          ref uint best_sad,
                                          ref uint raw_best_sad,
                                          ref FULLPEL_MV best_mv)
    {
        if (this_sad >= best_sad) return 0;

        // Add the motion vector cost.
        uint sad = this_sad + (uint)mvsad_err_cost_(mv, mv_cost_params);
        if (sad < best_sad)
        {
            raw_best_sad = this_sad;
            best_sad = sad;
            best_mv = mv;
            return 1;
        }
        return 0;
    }

    // Scratch buffers of the leaf functions calc_sad4_update_bestmv /
    // calc_sad3_update_bestmv (C: stack arrays block_offset[4], sads_buf[4] / sads[4]).
    [ThreadStatic] private static Ptr<byte>[] s_block_offset;
    [ThreadStatic] private static uint[] s_sads;

    // Calculate sad4 and update the bestmv information
    // in FAST_DIAMOND search method.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void calc_sad4_update_bestmv(
        in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
        in MV_COST_PARAMS mv_cost_params, ref FULLPEL_MV best_mv,
        FULLPEL_MV center_mv, Ptr<byte> center_address,
        ref uint bestsad, ref uint raw_bestsad, int search_step,
        ref int best_site, int cand_start, Ptr<int> cost_list)
    {
        ref readonly buf_2d src = ref ms_params.ms_buffers.src;
        ref readonly buf_2d @ref = ref ms_params.ms_buffers.@ref;
        search_site[] site = ms_params.search_sites.site[search_step];

        Ptr<byte>[] block_offset = s_block_offset ??= new Ptr<byte>[4];
        // PORT-NOTE: with a cost list, C points `sads` at cost_list + 1 (an int array read
        // as unsigned) so sdx4df writes the SADs straight into cost_list[1..4]. Here sdx4df
        // writes a uint scratch buffer, which is then copied into cost_list[1..4]; nothing
        // reads cost_list in between, so the result is identical.
        uint[] sads = s_sads ??= new uint[4];
        Ptr<byte> src_buf = src.buf;
        int src_stride = src.stride;
        // Loop over number of candidates.
        for (int j = 0; j < 4; j++)
            block_offset[j] = center_address + site[cand_start + j].offset;

        // 4-point sad calculation.
        ms_params.sdx4df(src_buf, src_stride, block_offset, @ref.stride, sads);
        if (!cost_list.IsNull)
        {
            for (int j = 0; j < 4; j++) cost_list[1 + j] = (int)sads[j];
        }

        for (int j = 0; j < 4; j++)
        {
            FULLPEL_MV this_mv = new((short)(center_mv.row + site[cand_start + j].mv.row),
                                     (short)(center_mv.col + site[cand_start + j].mv.col));
            int found_better_mv = update_mvs_and_sad(
                sads[j], this_mv, mv_cost_params, ref bestsad, ref raw_bestsad, ref best_mv
                /*second_best_mv=NULL*/);
            if (found_better_mv != 0) best_site = cand_start + j;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void calc_sad3_update_bestmv(
        in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
        in MV_COST_PARAMS mv_cost_params, ref FULLPEL_MV best_mv,
        FULLPEL_MV center_mv, Ptr<byte> center_address, ref uint bestsad,
        ref uint raw_bestsad, int search_step, ref int best_site,
        ReadOnlySpan<int> chkpts_indices, Ptr<int> cost_list)
    {
        ref readonly buf_2d src = ref ms_params.ms_buffers.src;
        ref readonly buf_2d @ref = ref ms_params.ms_buffers.@ref;
        search_site[] site = ms_params.search_sites.site[search_step];
        Ptr<byte>[] block_offset = s_block_offset ??= new Ptr<byte>[4];
        block_offset[0] = center_address + site[chkpts_indices[0]].offset;
        block_offset[1] = center_address + site[chkpts_indices[1]].offset;
        block_offset[2] = center_address + site[chkpts_indices[2]].offset;
        block_offset[3] = center_address;
        uint[] sads = s_sads ??= new uint[4];
        ms_params.sdx3df(src.buf, src.stride, block_offset, @ref.stride, sads);
        for (int j = 0; j < 3; j++)
        {
            int index = chkpts_indices[j];
            FULLPEL_MV this_mv = new((short)(center_mv.row + site[index].mv.row),
                                     (short)(center_mv.col + site[index].mv.col));
            int found_better_mv = update_mvs_and_sad(
                sads[j], this_mv, mv_cost_params, ref bestsad, ref raw_bestsad, ref best_mv
                /*second_best_mv=NULL*/);
            if (found_better_mv != 0) best_site = j;
        }
        if (!cost_list.IsNull)
        {
            for (int j = 0; j < 3; j++)
            {
                int index = chkpts_indices[j];
                cost_list[index + 1] = (int)sads[j];
            }
        }
    }

    // Calculate sad and update the bestmv information
    // in FAST_DIAMOND search method.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void calc_sad_update_bestmv(
        in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
        in MV_COST_PARAMS mv_cost_params, ref FULLPEL_MV best_mv,
        FULLPEL_MV center_mv, Ptr<byte> center_address,
        ref uint bestsad, ref uint raw_bestsad, int search_step,
        ref int best_site, int num_candidates, int cand_start, Ptr<int> cost_list)
    {
        ref readonly buf_2d src = ref ms_params.ms_buffers.src;
        ref readonly buf_2d @ref = ref ms_params.ms_buffers.@ref;
        search_site[] site = ms_params.search_sites.site[search_step];
        // Loop over number of candidates.
        for (int i = cand_start; i < num_candidates; i++)
        {
            FULLPEL_MV this_mv = new((short)(center_mv.row + site[i].mv.row),
                                     (short)(center_mv.col + site[i].mv.col));
            if (av1_is_fullmv_in_range(ms_params.mv_limits, this_mv) == 0) continue;
            int thissad = get_mvpred_sad(ms_params, src,
                                         center_address + site[i].offset, @ref.stride);
            if (!cost_list.IsNull)
            {
                cost_list[i + 1] = thissad;
            }
            int found_better_mv = update_mvs_and_sad(
                (uint)thissad, this_mv, mv_cost_params, ref bestsad, ref raw_bestsad, ref best_mv
                /*second_best_mv=NULL*/);
            if (found_better_mv != 0) best_site = i;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void calc_sad_update_bestmv_with_indices(
        in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
        in MV_COST_PARAMS mv_cost_params, ref FULLPEL_MV best_mv,
        FULLPEL_MV center_mv, Ptr<byte> center_address,
        ref uint bestsad, ref uint raw_bestsad, int search_step,
        ref int best_site, int num_candidates, ReadOnlySpan<int> chkpts_indices,
        Ptr<int> cost_list)
    {
        ref readonly buf_2d src = ref ms_params.ms_buffers.src;
        ref readonly buf_2d @ref = ref ms_params.ms_buffers.@ref;
        search_site[] site = ms_params.search_sites.site[search_step];
        // Loop over number of candidates.
        for (int i = 0; i < num_candidates; i++)
        {
            int index = chkpts_indices[i];
            FULLPEL_MV this_mv = new((short)(center_mv.row + site[index].mv.row),
                                     (short)(center_mv.col + site[index].mv.col));
            if (av1_is_fullmv_in_range(ms_params.mv_limits, this_mv) == 0)
            {
                if (!cost_list.IsNull)
                {
                    cost_list[index + 1] = int.MaxValue;
                }
                continue;
            }
            int thissad = get_mvpred_sad(
                ms_params, src, center_address + site[index].offset, @ref.stride);
            if (!cost_list.IsNull)
            {
                cost_list[index + 1] = thissad;
            }
            int found_better_mv = update_mvs_and_sad(
                (uint)thissad, this_mv, mv_cost_params, ref bestsad, ref raw_bestsad, ref best_mv
                /*second_best_mv=NULL*/);
            if (found_better_mv != 0) best_site = i;
        }
    }

    private static readonly int[] search_steps = {
        10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0,
    };

    // Generic pattern search function that searches over multiple scales.
    // Each scale can have a different number of candidates and shape of
    // candidates as indicated in the num_candidates and candidates arrays
    // passed into this function
    private static int pattern_search(FULLPEL_MV start_mv,
                                      in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
                                      int search_step, int do_init_search,
                                      Ptr<int> cost_list, ref FULLPEL_MV best_mv,
                                      ref FULLPEL_MV_STATS best_mv_stats)
    {
        // C: function-local static `search_steps` (hoisted).
        int i, s, t;

        ref readonly buf_2d src = ref ms_params.ms_buffers.src;
        ref readonly buf_2d @ref = ref ms_params.ms_buffers.@ref;
        search_site_config search_sites = ms_params.search_sites;
        int[] num_candidates = search_sites.searches_per_step;
        int ref_stride = @ref.stride;
        int last_is_4 = B(num_candidates[0] == 4);
        int br, bc;
        uint bestsad = uint.MaxValue, raw_bestsad = uint.MaxValue;
        int k = -1;
        ref readonly MV_COST_PARAMS mv_cost_params = ref ms_params.mv_cost_params;
        search_step = AOMMIN(search_step, MAX_MVSEARCH_STEPS - 1);
        Debug.Assert(search_step >= 0);
        int best_init_s = search_steps[search_step];
        // adjust ref_mv to make sure it is within MV range
        clamp_fullmv(ref start_mv, ms_params.mv_limits);
        br = start_mv.row;
        bc = start_mv.col;
        if (!cost_list.IsNull)
        {
            cost_list[0] = cost_list[1] = cost_list[2] = cost_list[3] = cost_list[4] =
                int.MaxValue;
        }
        int costlist_has_sad = 0;

        // C: `int next_chkpts_indices[PATTERN_CANDIDATES_REF]`, block-local in the loops below.
        Span<int> next_chkpts_indices = stackalloc int[PATTERN_CANDIDATES_REF];

        // Work out the start point for the search
        raw_bestsad = (uint)get_mvpred_sad(ms_params, src,
                                           get_buf_from_fullmv(@ref, start_mv), ref_stride);
        bestsad = raw_bestsad + (uint)mvsad_err_cost_(start_mv, mv_cost_params);

        // Search all possible scales up to the search param around the center point
        // pick the scale of the point that is best as the starting scale of
        // further steps around it.
        Ptr<byte> center_address = get_buf_from_fullmv(@ref, start_mv);
        if (do_init_search != 0)
        {
            s = best_init_s;
            best_init_s = -1;
            for (t = 0; t <= s; ++t)
            {
                int best_site = -1;
                FULLPEL_MV center_mv = new((short)br, (short)bc);
                if (check_bounds(ms_params.mv_limits, br, bc, 1 << t) != 0)
                {
                    // Call 4-point sad for multiples of 4 candidates.
                    int no_of_4_cand_loops = num_candidates[t] >> 2;
                    for (i = 0; i < no_of_4_cand_loops; i++)
                    {
                        calc_sad4_update_bestmv(ms_params, mv_cost_params, ref best_mv, center_mv,
                                                center_address, ref bestsad, ref raw_bestsad, t,
                                                ref best_site, i * 4, /*cost_list=*/Ptr<int>.Null);
                    }
                    // Rest of the candidates
                    int remaining_cand = num_candidates[t] % 4;
                    calc_sad_update_bestmv(ms_params, mv_cost_params, ref best_mv, center_mv,
                                           center_address, ref bestsad, ref raw_bestsad, t,
                                           ref best_site, remaining_cand,
                                           no_of_4_cand_loops * 4, Ptr<int>.Null);
                }
                else
                {
                    calc_sad_update_bestmv(ms_params, mv_cost_params, ref best_mv, center_mv,
                                           center_address, ref bestsad, ref raw_bestsad, t,
                                           ref best_site, num_candidates[t], 0, Ptr<int>.Null);
                }
                if (best_site == -1)
                {
                    continue;
                }
                else
                {
                    best_init_s = t;
                    k = best_site;
                }
            }
            if (best_init_s != -1)
            {
                br += search_sites.site[best_init_s][k].mv.row;
                bc += search_sites.site[best_init_s][k].mv.col;
                center_address += search_sites.site[best_init_s][k].offset;
            }
        }

        // If the center point is still the best, just skip this and move to
        // the refinement step.
        if (best_init_s != -1)
        {
            int last_s = B(last_is_4 != 0 && !cost_list.IsNull);
            int best_site = -1;
            s = best_init_s;

            for (; s >= last_s; s--)
            {
                // No need to search all points the 1st time if initial search was used
                if (do_init_search == 0 || s != best_init_s)
                {
                    FULLPEL_MV center_mv = new((short)br, (short)bc);
                    if (check_bounds(ms_params.mv_limits, br, bc, 1 << s) != 0)
                    {
                        // Call 4-point sad for multiples of 4 candidates.
                        int no_of_4_cand_loops = num_candidates[s] >> 2;
                        for (i = 0; i < no_of_4_cand_loops; i++)
                        {
                            calc_sad4_update_bestmv(ms_params, mv_cost_params, ref best_mv,
                                                    center_mv, center_address, ref bestsad,
                                                    ref raw_bestsad, s, ref best_site, i * 4,
                                                    /*cost_list=*/Ptr<int>.Null);
                        }
                        // Rest of the candidates
                        int remaining_cand = num_candidates[s] % 4;
                        calc_sad_update_bestmv(ms_params, mv_cost_params, ref best_mv, center_mv,
                                               center_address, ref bestsad, ref raw_bestsad, s,
                                               ref best_site, remaining_cand,
                                               no_of_4_cand_loops * 4, Ptr<int>.Null);
                    }
                    else
                    {
                        calc_sad_update_bestmv(ms_params, mv_cost_params, ref best_mv, center_mv,
                                               center_address, ref bestsad, ref raw_bestsad, s,
                                               ref best_site, num_candidates[s], 0, Ptr<int>.Null);
                    }

                    if (best_site == -1)
                    {
                        continue;
                    }
                    else
                    {
                        br += search_sites.site[s][best_site].mv.row;
                        bc += search_sites.site[s][best_site].mv.col;
                        center_address += search_sites.site[s][best_site].offset;
                        k = best_site;
                    }
                }

                do
                {
                    best_site = -1;
                    next_chkpts_indices[0] = (k == 0) ? num_candidates[s] - 1 : k - 1;
                    next_chkpts_indices[1] = k;
                    next_chkpts_indices[2] = (k == num_candidates[s] - 1) ? 0 : k + 1;

                    FULLPEL_MV center_mv = new((short)br, (short)bc);
                    if (check_bounds(ms_params.mv_limits, br, bc, 1 << s) != 0)
                    {
                        calc_sad3_update_bestmv(ms_params, mv_cost_params, ref best_mv, center_mv,
                                                center_address, ref bestsad, ref raw_bestsad, s,
                                                ref best_site, next_chkpts_indices, Ptr<int>.Null);
                    }
                    else
                    {
                        calc_sad_update_bestmv_with_indices(
                            ms_params, mv_cost_params, ref best_mv, center_mv, center_address,
                            ref bestsad, ref raw_bestsad, s, ref best_site, PATTERN_CANDIDATES_REF,
                            next_chkpts_indices, Ptr<int>.Null);
                    }

                    if (best_site != -1)
                    {
                        k = next_chkpts_indices[best_site];
                        br += search_sites.site[s][k].mv.row;
                        bc += search_sites.site[s][k].mv.col;
                        center_address += search_sites.site[s][k].offset;
                    }
                } while (best_site != -1);
            }
            // Note: If we enter the if below, then cost_list must be non-NULL.
            if (s == 0)
            {
                cost_list[0] = (int)raw_bestsad;
                costlist_has_sad = 1;
                Debug.Assert(num_candidates[s] == 4);
                if (do_init_search == 0 || s != best_init_s)
                {
                    FULLPEL_MV center_mv = new((short)br, (short)bc);
                    if (check_bounds(ms_params.mv_limits, br, bc, 1 << s) != 0)
                    {
                        calc_sad4_update_bestmv(ms_params, mv_cost_params, ref best_mv, center_mv,
                                                center_address, ref bestsad, ref raw_bestsad, s,
                                                ref best_site, 0, cost_list);
                    }
                    else
                    {
                        calc_sad_update_bestmv(ms_params, mv_cost_params, ref best_mv, center_mv,
                                               center_address, ref bestsad, ref raw_bestsad, s,
                                               ref best_site, /*num_candidates=*/4,
                                               /*cand_start=*/0, cost_list);
                    }

                    if (best_site != -1)
                    {
                        br += search_sites.site[s][best_site].mv.row;
                        bc += search_sites.site[s][best_site].mv.col;
                        center_address += search_sites.site[s][best_site].offset;
                        k = best_site;
                    }
                }
                while (best_site != -1)
                {
                    best_site = -1;
                    next_chkpts_indices[0] = (k == 0) ? num_candidates[s] - 1 : k - 1;
                    next_chkpts_indices[1] = k;
                    next_chkpts_indices[2] = (k == num_candidates[s] - 1) ? 0 : k + 1;
                    cost_list[1] = cost_list[2] = cost_list[3] = cost_list[4] = int.MaxValue;
                    cost_list[((k + 2) % 4) + 1] = cost_list[0];
                    cost_list[0] = (int)raw_bestsad;

                    FULLPEL_MV center_mv = new((short)br, (short)bc);
                    if (check_bounds(ms_params.mv_limits, br, bc, 1 << s) != 0)
                    {
                        Debug.Assert(PATTERN_CANDIDATES_REF == 3);
                        calc_sad3_update_bestmv(ms_params, mv_cost_params, ref best_mv, center_mv,
                                                center_address, ref bestsad, ref raw_bestsad, s,
                                                ref best_site, next_chkpts_indices, cost_list);
                    }
                    else
                    {
                        calc_sad_update_bestmv_with_indices(
                            ms_params, mv_cost_params, ref best_mv, center_mv, center_address,
                            ref bestsad, ref raw_bestsad, s, ref best_site, PATTERN_CANDIDATES_REF,
                            next_chkpts_indices, cost_list);
                    }

                    if (best_site != -1)
                    {
                        k = next_chkpts_indices[best_site];
                        br += search_sites.site[s][k].mv.row;
                        bc += search_sites.site[s][k].mv.col;
                        center_address += search_sites.site[s][k].offset;
                    }
                }
            }
        }
        best_mv.row = (short)br;
        best_mv.col = (short)bc;

        Debug.Assert(center_address == get_buf_from_fullmv(@ref, best_mv),
                     "center address is out of sync with best_mv!\n");

        // Returns the one-away integer pel cost/sad around the best as follows:
        // cost_list[0]: cost/sad at the best integer pel
        // cost_list[1]: cost/sad at delta {0, -1} (left)   from the best integer pel
        // cost_list[2]: cost/sad at delta { 1, 0} (bottom) from the best integer pel
        // cost_list[3]: cost/sad at delta { 0, 1} (right)  from the best integer pel
        // cost_list[4]: cost/sad at delta {-1, 0} (top)    from the best integer pel
        if (!cost_list.IsNull)
        {
            // USE_SAD_COSTLIST is 1: the calc_int_cost_list alternative is not ported.
            calc_int_sad_list(best_mv, ms_params, cost_list, costlist_has_sad);
        }

        int var_cost = get_mvpred_var_cost(ms_params, best_mv, ref best_mv_stats);
        return var_cost;
    }

    // For the following foo_search, the input arguments are:
    // start_mv: where we are starting our motion search
    // ms_params: a collection of motion search parameters
    // search_step: how many steps to skip in our motion search. For example,
    //   a value 3 suggests that 3 search steps have already taken place prior to
    //   this function call, so we jump directly to step 4 of the search process
    // do_init_search: if on, do an initial search of all possible scales around the
    //   start_mv, and then pick the best scale.
    // cond_list: used to hold the cost around the best full mv so we can use it to
    //   speed up subpel search later.
    // best_mv: the best mv found in the motion search
    private static int hex_search(FULLPEL_MV start_mv,
                                  in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
                                  int search_step, int do_init_search,
                                  Ptr<int> cost_list, ref FULLPEL_MV best_mv,
                                  ref FULLPEL_MV_STATS best_mv_stats)
    {
        return pattern_search(start_mv, ms_params, search_step, do_init_search,
                              cost_list, ref best_mv, ref best_mv_stats);
    }

    private static int bigdia_search(FULLPEL_MV start_mv,
                                     in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
                                     int search_step, int do_init_search,
                                     Ptr<int> cost_list, ref FULLPEL_MV best_mv,
                                     ref FULLPEL_MV_STATS best_mv_stats)
    {
        return pattern_search(start_mv, ms_params, search_step, do_init_search,
                              cost_list, ref best_mv, ref best_mv_stats);
    }

    private static int vfast_dia_search(FULLPEL_MV start_mv,
                                        in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
                                        int search_step, int do_init_search,
                                        Ptr<int> cost_list, ref FULLPEL_MV best_mv,
                                        ref FULLPEL_MV_STATS best_mv_stats)
    {
        return bigdia_search(start_mv, ms_params,
                             AOMMAX(MAX_MVSEARCH_STEPS - 1, search_step),
                             do_init_search, cost_list, ref best_mv, ref best_mv_stats);
    }

    private static int fast_dia_search(FULLPEL_MV start_mv,
                                       in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
                                       int search_step, int do_init_search,
                                       Ptr<int> cost_list, ref FULLPEL_MV best_mv,
                                       ref FULLPEL_MV_STATS best_mv_stats)
    {
        return bigdia_search(start_mv, ms_params,
                             AOMMAX(MAX_MVSEARCH_STEPS - 2, search_step),
                             do_init_search, cost_list, ref best_mv, ref best_mv_stats);
    }

    private static int fast_bigdia_search(FULLPEL_MV start_mv,
                                          in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
                                          int search_step, int do_init_search,
                                          Ptr<int> cost_list, ref FULLPEL_MV best_mv,
                                          ref FULLPEL_MV_STATS best_mv_stats)
    {
        return bigdia_search(start_mv, ms_params,
                             AOMMAX(MAX_MVSEARCH_STEPS - 3, search_step),
                             do_init_search, cost_list, ref best_mv, ref best_mv_stats);
    }

    // [subset] update_best_site, diamond_search_sad, get_start_mvpred_sad_cost,
    // full_pixel_diamond, exhaustive_mesh_search, full_pixel_exhaustive and
    // av1_refining_search_8p_c: only reached through the NSTEP / NSTEP_8PT / DIAMOND /
    // CLAMPED_DIAMOND search methods (and the mesh search they enable). The realtime
    // non-screen speed features always use FAST_DIAMOND, so none of them ever runs.

    // PORT-NOTE: second_best_mv may be NULL in C, so it is a Ptr<FULLPEL_MV> (Ptr.Null ==
    // NULL); every caller in the subset passes NULL. best_mv_stats is never NULL in the
    // subset's callers.
    internal static int av1_full_pixel_search(FULLPEL_MV start_mv,
                                              in FULLPEL_MOTION_SEARCH_PARAMS ms_params,
                                              int step_param, Ptr<int> cost_list,
                                              ref FULLPEL_MV best_mv,
                                              ref FULLPEL_MV_STATS best_mv_stats,
                                              Ptr<FULLPEL_MV> second_best_mv)
    {
        BLOCK_SIZE bsize = ms_params.bsize;
        SEARCH_METHODS search_method = ms_params.search_method;

        int is_intra_mode = ms_params.is_intra_mode;
        int run_mesh_search = ms_params.run_mesh_search;

        int var = 0;
        // MARK_MV_INVALID(best_mv): as_int = INVALID_MV sets row and col to -32768.
        best_mv.row = INVALID_MV_ROW_COL;
        best_mv.col = INVALID_MV_ROW_COL;
        if (!second_best_mv.IsNull)
        {
            second_best_mv.Value.row = INVALID_MV_ROW_COL;
            second_best_mv.Value.col = INVALID_MV_ROW_COL;
        }

        if (!cost_list.IsNull)
        {
            cost_list[0] = int.MaxValue;
            cost_list[1] = int.MaxValue;
            cost_list[2] = int.MaxValue;
            cost_list[3] = int.MaxValue;
            cost_list[4] = int.MaxValue;
        }

        Debug.Assert(ms_params.ms_buffers.@ref.stride == ms_params.search_sites.stride);

        switch (search_method)
        {
            case FAST_BIGDIA:
                var = fast_bigdia_search(start_mv, ms_params, step_param, 0, cost_list,
                                         ref best_mv, ref best_mv_stats);
                break;
            case VFAST_DIAMOND:
                var = vfast_dia_search(start_mv, ms_params, step_param, 0, cost_list,
                                       ref best_mv, ref best_mv_stats);
                break;
            case FAST_DIAMOND:
                var = fast_dia_search(start_mv, ms_params, step_param, 0, cost_list,
                                      ref best_mv, ref best_mv_stats);
                break;
            case HEX:
                var = hex_search(start_mv, ms_params, step_param, 1, cost_list, ref best_mv,
                                 ref best_mv_stats);
                break;
            case BIGDIA:
                var = bigdia_search(start_mv, ms_params, step_param, 1, cost_list,
                                    ref best_mv, ref best_mv_stats);
                break;
            case NSTEP:
            case NSTEP_8PT:
            case DIAMOND:
            case CLAMPED_DIAMOND:
                // [subset] full_pixel_diamond: these search methods are only selected for
                // screen content / non-realtime speed features.
                throw new NotSupportedException(
                    "av1_full_pixel_search: NSTEP/DIAMOND search methods are not in the subset");
            default: Debug.Assert(false, "Invalid search method."); break;
        }

        // Should we allow a follow on exhaustive search?
        if (run_mesh_search == 0 &&
            ((search_method == NSTEP) || (search_method == NSTEP_8PT)) &&
            ms_params.ms_buffers.second_pred.IsNull)
        {
            int exhaustive_thr = ms_params.force_mesh_thresh;
            exhaustive_thr >>=
                10 - (mi_size_wide_log2[bsize] + mi_size_high_log2[bsize]);
            // Threshold variance for an exhaustive full search.
            if (var > exhaustive_thr) run_mesh_search = 1;
        }

        // TODO(yunqing): the following is used to reduce mesh search in temporal
        // filtering. Can extend it to intrabc.
        if (is_intra_mode == 0 && ms_params.prune_mesh_search != 0)
        {
            int full_pel_mv_diff = AOMMAX(Math.Abs(start_mv.row - best_mv.row),
                                          Math.Abs(start_mv.col - best_mv.col));
            if (full_pel_mv_diff <= ms_params.mesh_search_mv_diff_threshold)
            {
                run_mesh_search = 0;
            }
        }

        if (ms_params.sdf != ms_params.vfp.sdf)
        {
            // If we are skipping rows when we perform the motion search, we need to
            // check the quality of skipping. If it's bad, then we run mesh search with
            // skip row features off.
            // TODO(chiyotsai@google.com): Handle the case where we have a vertical
            // offset of 1 before we hit this statement to avoid having to redo
            // motion search.
            ref readonly buf_2d src = ref ms_params.ms_buffers.src;
            ref readonly buf_2d @ref = ref ms_params.ms_buffers.@ref;
            int src_stride = src.stride;
            int ref_stride = @ref.stride;

            Ptr<byte> src_address = src.buf;
            Ptr<byte> best_address = get_buf_from_fullmv(@ref, best_mv);
            int sad =
                (int)ms_params.vfp.sdf(src_address, src_stride, best_address, ref_stride);
            int skip_sad =
                (int)ms_params.vfp.sdsf(src_address, src_stride, best_address, ref_stride);
            // We will keep the result of skipping rows if it's good enough. Here, good
            // enough means the error is less than 1 per pixel.
            int kSADThresh =
                1 << (mi_size_wide_log2[bsize] + mi_size_high_log2[bsize]);
            if (sad > kSADThresh && Math.Abs(skip_sad - sad) * 10 >= AOMMAX(sad, 1) * 9)
            {
                // There is a large discrepancy between skipping and not skipping, so we
                // need to redo the motion search.
                FULLPEL_MOTION_SEARCH_PARAMS new_ms_params = ms_params;
                new_ms_params.sdf = new_ms_params.vfp.sdf;
                new_ms_params.sdx4df = new_ms_params.vfp.sdx4df;
                new_ms_params.sdx3df = new_ms_params.vfp.sdx3df;

                return av1_full_pixel_search(start_mv, new_ms_params, step_param,
                                             cost_list, ref best_mv, ref best_mv_stats,
                                             second_best_mv);
            }
        }

        if (run_mesh_search != 0)
        {
            // [subset] full_pixel_exhaustive (mesh search): run_mesh_search is only set
            // for the NSTEP search methods, which the subset never uses.
            throw new NotSupportedException(
                "av1_full_pixel_search: mesh search is not in the subset");
        }

        return var;
    }

    // [subset] av1_intrabc_hash_search: intra block copy is off (never executed).

    internal static int av1_vector_match(Ptr<short> @ref, Ptr<short> src, int bwl,
                                         int search_size_top, int search_size_bottom,
                                         int full_search, ref int sad)
    {
        int best_sad = int.MaxValue;
        int this_sad;
        int d;
        int center, offset = 0;
        int bw = search_size_top + search_size_bottom;

        if (full_search != 0)
        {
            for (d = 0; d <= bw; d++)
            {
                this_sad = aom_vector_var(@ref + d, src, bwl);
                if (this_sad < best_sad)
                {
                    best_sad = this_sad;
                    offset = d;
                }
            }
            center = offset;
            sad = best_sad;
            return (center - search_size_top);
        }

        for (d = 0; d <= bw; d += 16)
        {
            this_sad = aom_vector_var(@ref + d, src, bwl);
            if (this_sad < best_sad)
            {
                best_sad = this_sad;
                offset = d;
            }
        }
        center = offset;

        for (d = -8; d <= 8; d += 16)
        {
            int this_pos = offset + d;
            // check limit
            if (this_pos < 0 || this_pos > bw) continue;
            this_sad = aom_vector_var(@ref + this_pos, src, bwl);
            if (this_sad < best_sad)
            {
                best_sad = this_sad;
                center = this_pos;
            }
        }
        offset = center;

        for (d = -4; d <= 4; d += 8)
        {
            int this_pos = offset + d;
            // check limit
            if (this_pos < 0 || this_pos > bw) continue;
            this_sad = aom_vector_var(@ref + this_pos, src, bwl);
            if (this_sad < best_sad)
            {
                best_sad = this_sad;
                center = this_pos;
            }
        }
        offset = center;

        for (d = -2; d <= 2; d += 4)
        {
            int this_pos = offset + d;
            // check limit
            if (this_pos < 0 || this_pos > bw) continue;
            this_sad = aom_vector_var(@ref + this_pos, src, bwl);
            if (this_sad < best_sad)
            {
                best_sad = this_sad;
                center = this_pos;
            }
        }
        offset = center;

        for (d = -1; d <= 1; d += 2)
        {
            int this_pos = offset + d;
            // check limit
            if (this_pos < 0 || this_pos > bw) continue;
            this_sad = aom_vector_var(@ref + this_pos, src, bwl);
            if (this_sad < best_sad)
            {
                best_sad = this_sad;
                center = this_pos;
            }
        }
        sad = best_sad;
        return (center - search_size_top);
    }

    private static readonly MV[] search_pos = {
        new(-1, 0),
        new(0, -1),
        new(0, 1),
        new(1, 0),
    };

    // A special fast version of motion search used in rt mode.
    // The search window along columns and row is given by:
    //  +/- me_search_size_col/row.
    internal static uint av1_int_pro_motion_estimation(
        AV1_COMP cpi, MACROBLOCK x, BLOCK_SIZE bsize, int mi_row,
        int mi_col, in MV ref_mv, ref uint y_sad_zero,
        int me_search_size_col, int me_search_size_row, int is_var_part,
        int use_larger_search)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mi = xd.mi[0];
        // [subset] backup_yv12[MAX_MB_PLANE]: only used with a scaled reference (below).
        int idx;
        int bw = block_size_wide[bsize];
        int bh = block_size_high[bsize];
        int is_screen = B(cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN);
        int full_search = is_screen;
        bool scroll_superblock =
            use_larger_search != 0 && bsize == cm.seq_params.sb_size;
        // Keep border a multiple of 16.
        int border = (cpi.oxcf.border_in_pixels >> 4) << 4;
        int search_size_width_left = me_search_size_col;
        int search_size_width_right = me_search_size_col;
        int search_size_height_top = me_search_size_row;
        int search_size_height_bottom = me_search_size_row;
        // Allow for larger search size for column/horizontal screen motion.
        if (scroll_superblock && is_var_part != 0)
        {
            if (((mi_col << 2) - search_size_width_left) < -border)
                search_size_width_left = (mi_col << 2) + border;
            if (((mi_col << 2) + search_size_width_right + bw) > cm.width + border)
                search_size_width_right = cm.width + border - (mi_col << 2) - bw;
        }
        else
        {
            if (((mi_col << 2) - search_size_width_left < -border) ||
                ((mi_col << 2) + search_size_width_right + bw > cm.width + border))
            {
                search_size_width_left = AOMMIN(border, (mi_col << 2) + border);
                search_size_width_right =
                    AOMMIN(border, cm.width + border - (mi_col << 2) - bw);
            }
        }
        // Allow for larger search size for row/vertical screen motion.
        if (scroll_superblock && is_var_part != 0)
        {
            if (((mi_row << 2) - search_size_height_top) < -border)
                search_size_height_top = (mi_row << 2) + border;
            if (((mi_row << 2) + search_size_height_bottom + bh) > cm.height + border)
                search_size_height_bottom = cm.height + border - (mi_row << 2) - bh;
        }
        else
        {
            if (((mi_row << 2) - search_size_height_top < -border) ||
                ((mi_row << 2) + search_size_height_bottom + bh >
                 cm.height + border))
            {
                search_size_height_top = AOMMIN(border, (mi_row << 2) + border);
                search_size_height_bottom =
                    AOMMIN(border, cm.height + border - (mi_row << 2) - bh);
            }
        }
        // Make search_size_width/height_left/right/top/bottom multiple of 16.
        search_size_width_left &= ~15;
        search_size_width_right &= ~15;
        search_size_height_top &= ~15;
        search_size_height_bottom &= ~15;
        int src_stride = x.plane[0].src.stride;
        Ptr<byte> ref_buf, src_buf;
        ref int_mv best_int_mv = ref xd.mi[0].mv[0];
        uint best_sad, tmp_sad;
        int best_sad_col = 0, best_sad_row = 0;
        int row_norm_factor = mi_size_high_log2[bsize] + 1;
        int col_norm_factor = 3 + (bw >> 5);
        YV12_BUFFER_CONFIG scaled_ref_frame =
            av1_get_scaled_ref_frame(cpi, mi.ref_frame[0]);
        // C: function-local static search_pos (hoisted).

        if (scaled_ref_frame != null)
        {
            // [subset] Swapping in a scaled reference (backup_yv12 / av1_setup_pre_planes):
            // reference scaling needs resize / superres / spatial layers, which are off.
            throw new NotSupportedException(
                "av1_int_pro_motion_estimation: scaled references are not in the subset");
        }
        int ref_stride = xd.plane[0].pre[0].stride;

        if (xd.bd != 8)
        {
            // [subset] High bitdepth is compiled out (bd is always 8).
            throw new NotSupportedException(
                "av1_int_pro_motion_estimation: high bitdepth is not in the subset");
        }

        int width_ref_buf =
            search_size_width_left + search_size_width_right + bw;
        int height_ref_buf =
            search_size_height_top + search_size_height_bottom + bh;
        // [av1rt] C mallocs these per call; here they are per-thread buffers reused across
        // calls (only the first width_ref_buf / height_ref_buf / bw / bh entries are written
        // and read).
        NONRD_SCRATCH int_pro = x.nonrd_scratch;
        short[] hbuf = NONRD_SCRATCH.int_pro_buf(ref int_pro.int_pro_hbuf, width_ref_buf);
        short[] vbuf = NONRD_SCRATCH.int_pro_buf(ref int_pro.int_pro_vbuf, height_ref_buf);
        short[] src_hbuf = NONRD_SCRATCH.int_pro_buf(ref int_pro.int_pro_src_hbuf, bw);
        short[] src_vbuf = NONRD_SCRATCH.int_pro_buf(ref int_pro.int_pro_src_vbuf, bh);

        // Set up prediction 1-D reference set for rows.
        ref_buf = xd.plane[0].pre[0].buf - search_size_width_left;
        aom_int_pro_row(hbuf, ref_buf, ref_stride, width_ref_buf, bh,
                        row_norm_factor);

        // Set up prediction 1-D reference set for cols
        ref_buf = xd.plane[0].pre[0].buf - search_size_height_top * ref_stride;
        aom_int_pro_col(vbuf, ref_buf, ref_stride, bw, height_ref_buf,
                        col_norm_factor);

        // Set up src 1-D reference set
        src_buf = x.plane[0].src.buf;
        aom_int_pro_row(src_hbuf, src_buf, src_stride, bw, bh, row_norm_factor);
        aom_int_pro_col(src_vbuf, src_buf, src_stride, bw, bh, col_norm_factor);

        // Find the best match per 1-D search
        best_int_mv.as_fullmv.col = (short)av1_vector_match(
            hbuf, src_hbuf, mi_size_wide_log2[bsize], search_size_width_left,
            search_size_width_right, full_search, ref best_sad_col);
        best_int_mv.as_fullmv.row = (short)av1_vector_match(
            vbuf, src_vbuf, mi_size_high_log2[bsize], search_size_height_top,
            search_size_height_bottom, full_search, ref best_sad_row);

        // For screen: select between horiz or vert motion.
        if (is_screen != 0)
        {
            if (best_sad_col < best_sad_row)
                best_int_mv.as_fullmv.row = 0;
            else
                best_int_mv.as_fullmv.col = 0;
        }

        FULLPEL_MV this_mv = best_int_mv.as_fullmv;
        src_buf = x.plane[0].src.buf;
        ref_buf = get_buf_from_fullmv(xd.plane[0].pre[0], this_mv);
        best_sad =
            cpi.ppi.fn_ptr[bsize].sdf(src_buf, src_stride, ref_buf, ref_stride);

        if (is_screen == 0 && scroll_superblock)
        {
            if (best_sad_col < best_sad_row && best_sad_col < (int)best_sad)
            {
                best_int_mv.as_fullmv.row = 0;
                best_sad = (uint)best_sad_col;
            }
            else if (best_sad_row < best_sad_col && best_sad_row < (int)best_sad)
            {
                best_int_mv.as_fullmv.col = 0;
                best_sad = (uint)best_sad_row;
            }
        }

        // Evaluate zero MV if found MV is non-zero.
        if (best_int_mv.as_int != 0)
        {
            tmp_sad = cpi.ppi.fn_ptr[bsize].sdf(x.plane[0].src.buf, src_stride,
                                                xd.plane[0].pre[0].buf, ref_stride);
            y_sad_zero = tmp_sad;
            if (tmp_sad < best_sad)
            {
                best_int_mv.as_fullmv = kZeroFullMv;
                this_mv = best_int_mv.as_fullmv;
                ref_buf = xd.plane[0].pre[0].buf;
                best_sad = tmp_sad;
            }
        }
        else
        {
            y_sad_zero = best_sad;
        }

        if (!scroll_superblock)
        {
            // TODO(perf): per-call allocation (C: stack arrays pos[4], this_sad[4]).
            Ptr<byte>[] pos = {
                ref_buf - ref_stride,
                ref_buf - 1,
                ref_buf + 1,
                ref_buf + ref_stride,
            };
            uint[] this_sad = new uint[4];

            cpi.ppi.fn_ptr[bsize].sdx4df(src_buf, src_stride, pos, ref_stride,
                                         this_sad);

            for (idx = 0; idx < 4; ++idx)
            {
                if (this_sad[idx] < best_sad)
                {
                    best_sad = this_sad[idx];
                    best_int_mv.as_fullmv.row = (short)(search_pos[idx].row + this_mv.row);
                    best_int_mv.as_fullmv.col = (short)(search_pos[idx].col + this_mv.col);
                }
            }

            if (this_sad[0] < this_sad[3])
                this_mv.row -= 1;
            else
                this_mv.row += 1;

            if (this_sad[1] < this_sad[2])
                this_mv.col -= 1;
            else
                this_mv.col += 1;

            ref_buf = get_buf_from_fullmv(xd.plane[0].pre[0], this_mv);

            tmp_sad =
                cpi.ppi.fn_ptr[bsize].sdf(src_buf, src_stride, ref_buf, ref_stride);
            if (best_sad > tmp_sad)
            {
                best_int_mv.as_fullmv = this_mv;
                best_sad = tmp_sad;
            }
        }

        FullMvLimits mv_limits = x.mv_limits;
        av1_set_mv_search_range(ref mv_limits, ref_mv);
        clamp_fullmv(ref best_int_mv.as_fullmv, mv_limits);

        convert_fullmv_to_mv(ref best_int_mv);

        // [subset] Restoring xd->plane[i].pre[0] from backup_yv12: no scaled references.

        return best_sad;
    }

    // [subset] OBMC full-pixel search (get_obmc_mvpred_var, obmc_refining_search_sad,
    // obmc_diamond_search_sad, obmc_full_pixel_diamond, av1_obmc_full_pixel_search): OBMC is
    // off (never executed).

    // =============================================================================
    //  Subpixel Motion Search: Translational
    // =============================================================================
    private const int INIT_SUBPEL_STEP_SIZE = 4;
    /*
     * To avoid the penalty for crossing cache-line read, preload the reference
     * area in a small buffer, which is aligned to make sure there won't be crossing
     * cache-line read while reading from this buffer. This reduced the cpu
     * cycles spent on reading ref data in sub-pixel filter functions.
     * TODO: Currently, since sub-pixel search range here is -3 ~ 3, copy 22 rows x
     * 32 cols area that is enough for 16x16 macroblock. Later, for SPLITMV, we
     * could reduce the area.
     */

    // Returns the subpel offset used by various subpel variance functions [m]sv[a]f
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int get_subpel_part(int x) { return x & 7; }

    // Gets the address of the ref buffer at subpel location (r, c), rounded to the
    // nearest fullpel precision toward - \infty
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Ptr<byte> get_buf_from_mv(in buf_2d buf, MV mv)
    {
        int offset = (mv.row >> 3) * buf.stride + (mv.col >> 3);
        return buf.buf + offset;
    }

    // Estimates the variance of prediction residue using bilinear filter for fast
    // search.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int estimated_pref_error(
        in MV this_mv, in SUBPEL_SEARCH_VAR_PARAMS var_params,
        ref uint sse)
    {
        aom_variance_fn_ptr_t vfp = var_params.vfp;

        ref readonly MSBuffers ms_buffers = ref var_params.ms_buffers;
        Ptr<byte> src = ms_buffers.src.buf;
        Ptr<byte> @ref = get_buf_from_mv(ms_buffers.@ref, this_mv);
        int src_stride = ms_buffers.src.stride;
        int ref_stride = ms_buffers.@ref.stride;
        Ptr<byte> second_pred = ms_buffers.second_pred;
        Ptr<byte> mask = ms_buffers.mask;
        int mask_stride = ms_buffers.mask_stride;
        int invert_mask = ms_buffers.inv_mask;

        int subpel_x_q3 = get_subpel_part(this_mv.col);
        int subpel_y_q3 = get_subpel_part(this_mv.row);

        if (second_pred.IsNull)
        {
            return (int)vfp.svf(@ref, ref_stride, subpel_x_q3, subpel_y_q3, src, src_stride,
                                ref sse);
        }
        else if (!mask.IsNull)
        {
            return (int)vfp.msvf(@ref, ref_stride, subpel_x_q3, subpel_y_q3, src, src_stride,
                                 second_pred, mask, mask_stride, invert_mask, ref sse);
        }
        else
        {
            return (int)vfp.svaf(@ref, ref_stride, subpel_x_q3, subpel_y_q3, src, src_stride,
                                 ref sse, second_pred);
        }
    }

    // [subset] upsampled_pref_error (aom_upsampled_pred / aom_comp_*_upsampled_pred): only
    // reached with a scaled reference (is_scaled) or from the unported
    // av1_find_best_sub_pixel_tree / _pruned (check_better). Reference scaling needs
    // resize / superres / spatial layers, which are off.

    // Estimates whether this_mv is better than best_mv. This function incorporates
    // both prediction error and residue into account. It is suffixed "fast" because
    // it uses bilinear filter to estimate the prediction.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint check_better_fast(
        MACROBLOCKD xd, AV1_COMMON cm, in MV this_mv, ref MV best_mv,
        in SubpelMvLimits mv_limits, in SUBPEL_SEARCH_VAR_PARAMS var_params,
        in MV_COST_PARAMS mv_cost_params, ref uint besterr,
        ref uint sse1, ref int distortion, ref int has_better_mv, int is_scaled)
    {
        uint cost;
        if (av1_is_subpelmv_in_range(mv_limits, this_mv) != 0)
        {
            uint sse = 0;
            int thismse;
            if (is_scaled != 0)
            {
                // [subset] upsampled_pref_error: no scaled references.
                throw new NotSupportedException(
                    "check_better_fast: scaled references are not in the subset");
            }
            else
            {
                thismse = estimated_pref_error(this_mv, var_params, ref sse);
            }
            cost = (uint)mv_err_cost_(this_mv, mv_cost_params);
            cost += (uint)thismse;

            if (cost < besterr)
            {
                besterr = cost;
                best_mv = this_mv;
                distortion = thismse;
                sse1 = sse;
                has_better_mv |= 1;
            }
        }
        else
        {
            cost = int.MaxValue;
        }
        return cost;
    }

    // [subset] check_better: only used by the unported av1_find_best_sub_pixel_tree /
    // _pruned (first_level_check, second_level_check_v2).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MV get_best_diag_step(int step_size, uint left_cost,
                                         uint right_cost,
                                         uint up_cost,
                                         uint down_cost)
    {
        MV diag_step = new((short)(up_cost <= down_cost ? -step_size : step_size),
                           (short)(left_cost <= right_cost ? -step_size : step_size));

        return diag_step;
    }

    // Searches the four cardinal direction for a better mv, then follows up with a
    // search in the best quadrant. This uses bilinear filter to speed up the
    // calculation.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MV first_level_check_fast(
        MACROBLOCKD xd, AV1_COMMON cm, MV this_mv, ref MV best_mv,
        int hstep, in SubpelMvLimits mv_limits,
        in SUBPEL_SEARCH_VAR_PARAMS var_params,
        in MV_COST_PARAMS mv_cost_params, ref uint besterr,
        ref uint sse1, ref int distortion, int is_scaled)
    {
        // Check the four cardinal directions
        MV left_mv = new(this_mv.row, (short)(this_mv.col - hstep));
        int dummy = 0;
        uint left = check_better_fast(
            xd, cm, left_mv, ref best_mv, mv_limits, var_params, mv_cost_params, ref besterr,
            ref sse1, ref distortion, ref dummy, is_scaled);

        MV right_mv = new(this_mv.row, (short)(this_mv.col + hstep));
        uint right = check_better_fast(
            xd, cm, right_mv, ref best_mv, mv_limits, var_params, mv_cost_params,
            ref besterr, ref sse1, ref distortion, ref dummy, is_scaled);

        MV top_mv = new((short)(this_mv.row - hstep), this_mv.col);
        uint up = check_better_fast(
            xd, cm, top_mv, ref best_mv, mv_limits, var_params, mv_cost_params, ref besterr,
            ref sse1, ref distortion, ref dummy, is_scaled);

        MV bottom_mv = new((short)(this_mv.row + hstep), this_mv.col);
        uint down = check_better_fast(
            xd, cm, bottom_mv, ref best_mv, mv_limits, var_params, mv_cost_params,
            ref besterr, ref sse1, ref distortion, ref dummy, is_scaled);

        MV diag_step = get_best_diag_step(hstep, left, right, up, down);
        MV diag_mv = new((short)(this_mv.row + diag_step.row),
                         (short)(this_mv.col + diag_step.col));

        // Check the diagonal direction with the best mv
        check_better_fast(xd, cm, diag_mv, ref best_mv, mv_limits, var_params,
                          mv_cost_params, ref besterr, ref sse1, ref distortion, ref dummy,
                          is_scaled);

        return diag_step;
    }

    // Performs a following up search after first_level_check_fast is called. This
    // performs two extra chess pattern searches in the best quadrant.
    // (Never executed in the subset: subpel_iters_per_step is 1. Ported because it is small
    // and reached from two_level_checks_fast.)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void second_level_check_fast(
        MACROBLOCKD xd, AV1_COMMON cm, MV this_mv, MV diag_step,
        ref MV best_mv, int hstep, in SubpelMvLimits mv_limits,
        in SUBPEL_SEARCH_VAR_PARAMS var_params,
        in MV_COST_PARAMS mv_cost_params, ref uint besterr,
        ref uint sse1, ref int distortion, int is_scaled)
    {
        Debug.Assert(diag_step.row == hstep || diag_step.row == -hstep);
        Debug.Assert(diag_step.col == hstep || diag_step.col == -hstep);
        int tr = this_mv.row;
        int tc = this_mv.col;
        int br = best_mv.row;
        int bc = best_mv.col;
        int dummy = 0;
        if (tr != br && tc != bc)
        {
            Debug.Assert(diag_step.col == bc - tc);
            Debug.Assert(diag_step.row == br - tr);
            MV chess_mv_1 = new((short)br, (short)(bc + diag_step.col));
            MV chess_mv_2 = new((short)(br + diag_step.row), (short)bc);
            check_better_fast(xd, cm, chess_mv_1, ref best_mv, mv_limits, var_params,
                              mv_cost_params, ref besterr, ref sse1, ref distortion, ref dummy,
                              is_scaled);

            check_better_fast(xd, cm, chess_mv_2, ref best_mv, mv_limits, var_params,
                              mv_cost_params, ref besterr, ref sse1, ref distortion, ref dummy,
                              is_scaled);
        }
        else if (tr == br && tc != bc)
        {
            Debug.Assert(diag_step.col == bc - tc);
            // Continue searching in the best direction
            MV bottom_long_mv = new((short)(br + hstep), (short)(bc + diag_step.col));
            MV top_long_mv = new((short)(br - hstep), (short)(bc + diag_step.col));
            check_better_fast(xd, cm, bottom_long_mv, ref best_mv, mv_limits, var_params,
                              mv_cost_params, ref besterr, ref sse1, ref distortion, ref dummy,
                              is_scaled);
            check_better_fast(xd, cm, top_long_mv, ref best_mv, mv_limits, var_params,
                              mv_cost_params, ref besterr, ref sse1, ref distortion, ref dummy,
                              is_scaled);

            // Search in the direction opposite of the best quadrant
            MV rev_mv = new((short)(br - diag_step.row), (short)bc);
            check_better_fast(xd, cm, rev_mv, ref best_mv, mv_limits, var_params,
                              mv_cost_params, ref besterr, ref sse1, ref distortion, ref dummy,
                              is_scaled);
        }
        else if (tr != br && tc == bc)
        {
            Debug.Assert(diag_step.row == br - tr);
            // Continue searching in the best direction
            MV right_long_mv = new((short)(br + diag_step.row), (short)(bc + hstep));
            MV left_long_mv = new((short)(br + diag_step.row), (short)(bc - hstep));
            check_better_fast(xd, cm, right_long_mv, ref best_mv, mv_limits, var_params,
                              mv_cost_params, ref besterr, ref sse1, ref distortion, ref dummy,
                              is_scaled);
            check_better_fast(xd, cm, left_long_mv, ref best_mv, mv_limits, var_params,
                              mv_cost_params, ref besterr, ref sse1, ref distortion, ref dummy,
                              is_scaled);

            // Search in the direction opposite of the best quadrant
            MV rev_mv = new((short)br, (short)(bc - diag_step.col));
            check_better_fast(xd, cm, rev_mv, ref best_mv, mv_limits, var_params,
                              mv_cost_params, ref besterr, ref sse1, ref distortion, ref dummy,
                              is_scaled);
        }
    }

    // Combines first level check and second level check when applicable. This first
    // searches the four cardinal directions, and perform several
    // diagonal/chess-pattern searches in the best quadrant.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void two_level_checks_fast(
        MACROBLOCKD xd, AV1_COMMON cm, MV this_mv, ref MV best_mv,
        int hstep, in SubpelMvLimits mv_limits,
        in SUBPEL_SEARCH_VAR_PARAMS var_params,
        in MV_COST_PARAMS mv_cost_params, ref uint besterr,
        ref uint sse1, ref int distortion, int iters, int is_scaled)
    {
        MV diag_step = first_level_check_fast(
            xd, cm, this_mv, ref best_mv, hstep, mv_limits, var_params, mv_cost_params,
            ref besterr, ref sse1, ref distortion, is_scaled);
        if (iters > 1)
        {
            second_level_check_fast(xd, cm, this_mv, diag_step, ref best_mv, hstep,
                                    mv_limits, var_params, mv_cost_params, ref besterr,
                                    ref sse1, ref distortion, is_scaled);
        }
    }

    // [subset] first_level_check / second_level_check_v2: only used by the unported
    // av1_find_best_sub_pixel_tree / _pruned.

    // Gets the error at the beginning when the mv has fullpel precision
    private static uint setup_center_error(
        MACROBLOCKD xd, in MV bestmv,
        in SUBPEL_SEARCH_VAR_PARAMS var_params,
        in MV_COST_PARAMS mv_cost_params, ref uint sse1, ref int distortion)
    {
        aom_variance_fn_ptr_t vfp = var_params.vfp;
        int w = var_params.w;
        int h = var_params.h;

        ref readonly MSBuffers ms_buffers = ref var_params.ms_buffers;
        Ptr<byte> src = ms_buffers.src.buf;
        Ptr<byte> y = get_buf_from_mv(ms_buffers.@ref, bestmv);
        int src_stride = ms_buffers.src.stride;
        int y_stride = ms_buffers.@ref.stride;
        Ptr<byte> second_pred = ms_buffers.second_pred;

        uint besterr;

        if (!second_pred.IsNull)
        {
            // [subset] Compound (second_pred) motion search via aom_comp_avg_pred /
            // aom_comp_mask_pred: the nonrd path never searches compound NEWMV.
            _ = w;
            _ = h;
            throw new NotSupportedException(
                "setup_center_error: compound motion search is not in the subset");
        }
        else
        {
            besterr = vfp.vf(y, y_stride, src, src_stride, ref sse1);
        }
        distortion = (int)besterr;
        besterr += (uint)mv_err_cost_(bestmv, mv_cost_params);
        return besterr;
    }

    // [subset] upsampled_setup_center_error: scaled references only (see
    // upsampled_pref_error).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int divide_and_round(int n, int d)
    {
        return ((n < 0) ^ (d < 0)) ? ((n - d / 2) / d) : ((n + d / 2) / d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int is_cost_list_wellbehaved(Ptr<int> cost_list)
    {
        return B(cost_list[0] < cost_list[1] && cost_list[0] < cost_list[2] &&
                 cost_list[0] < cost_list[3] && cost_list[0] < cost_list[4]);
    }

    // Returns surface minima estimate at given precision in 1/2^n bits.
    // Assume a model for the cost surface: S = A(x - x0)^2 + B(y - y0)^2 + C
    // For a given set of costs S0, S1, S2, S3, S4 at points
    // (y, x) = (0, 0), (0, -1), (1, 0), (0, 1) and (-1, 0) respectively,
    // the solution for the location of the minima (x0, y0) is given by:
    // x0 = 1/2 (S1 - S3)/(S1 + S3 - 2*S0),
    // y0 = 1/2 (S4 - S2)/(S4 + S2 - 2*S0).
    // The code below is an integerized version of that.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void get_cost_surf_min(Ptr<int> cost_list, ref int ir, ref int ic,
                                          int bits)
    {
        ic = divide_and_round((cost_list[1] - cost_list[3]) * (1 << (bits - 1)),
                              (cost_list[1] - 2 * cost_list[0] + cost_list[3]));
        ir = divide_and_round((cost_list[4] - cost_list[2]) * (1 << (bits - 1)),
                              (cost_list[4] - 2 * cost_list[0] + cost_list[2]));
    }

    // Checks the list of mvs searched in the last iteration and see if we are
    // repeating it. If so, return 1. Otherwise we update the last_mv_search_list
    // with current_mv and return 0.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int check_repeated_mv_and_update(Ptr<int_mv> last_mv_search_list,
                                                    MV current_mv, int iter)
    {
        if (!last_mv_search_list.IsNull)
        {
            if (CHECK_MV_EQUAL(last_mv_search_list[iter].as_mv, current_mv))
            {
                return 1;
            }

            last_mv_search_list[iter].as_mv = current_mv;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int setup_center_error_facade(
        MACROBLOCKD xd, AV1_COMMON cm, in MV bestmv,
        in SUBPEL_SEARCH_VAR_PARAMS var_params,
        in MV_COST_PARAMS mv_cost_params, ref uint sse1, ref int distortion,
        int is_scaled)
    {
        if (is_scaled != 0)
        {
            // [subset] upsampled_setup_center_error: no scaled references.
            throw new NotSupportedException(
                "setup_center_error_facade: scaled references are not in the subset");
        }
        else
        {
            return (int)setup_center_error(xd, bestmv, var_params, mv_cost_params, ref sse1,
                                           ref distortion);
        }
    }

    internal static int av1_find_best_sub_pixel_tree_pruned_more(
        MACROBLOCKD xd, AV1_COMMON cm,
        in SUBPEL_MOTION_SEARCH_PARAMS ms_params, MV start_mv,
        FULLPEL_MV_STATS? start_mv_stats, ref MV bestmv, ref int distortion,
        ref uint sse1, Ptr<int_mv> last_mv_search_list)
    {
        int allow_hp = ms_params.allow_hp;
        int forced_stop = ms_params.forced_stop;
        int iters_per_step = ms_params.iters_per_step;
        Ptr<int> cost_list = ms_params.cost_list;
        ref readonly SubpelMvLimits mv_limits = ref ms_params.mv_limits;
        ref readonly MV_COST_PARAMS mv_cost_params = ref ms_params.mv_cost_params;
        ref readonly SUBPEL_SEARCH_VAR_PARAMS var_params = ref ms_params.var_params;

        // The iteration we are current searching for. Iter 0 corresponds to fullpel
        // mv, iter 1 to half pel, and so on
        int iter = 0;
        int hstep = INIT_SUBPEL_STEP_SIZE;  // Step size, initialized to 4/8=1/2 pel
        uint besterr = int.MaxValue;
        bestmv = start_mv;

        scale_factors sf = is_intrabc_block(xd.mi[0]) != 0
                               ? cm.sf_identity
                               : xd.block_ref_scale_factors[0];
        int is_scaled = av1_is_scaled(sf);

        if (start_mv_stats != null && is_scaled == 0)
        {
            FULLPEL_MV_STATS stats = start_mv_stats.GetValueOrDefault();
            besterr = stats.distortion + (uint)stats.err_cost;
            distortion = (int)stats.distortion;
            sse1 = stats.sse;
        }
        else
        {
            besterr =
                (uint)setup_center_error_facade(xd, cm, bestmv, var_params, mv_cost_params,
                                                ref sse1, ref distortion, is_scaled);
        }

        // If forced_stop is FULL_PEL, return.
        if (forced_stop == FULL_PEL) return (int)besterr;

        if (check_repeated_mv_and_update(last_mv_search_list, bestmv, iter) != 0)
        {
            return int.MaxValue;
        }
        iter++;

        if (!cost_list.IsNull && cost_list[0] != int.MaxValue && cost_list[1] != int.MaxValue &&
            cost_list[2] != int.MaxValue && cost_list[3] != int.MaxValue &&
            cost_list[4] != int.MaxValue && is_cost_list_wellbehaved(cost_list) != 0)
        {
            int ir = 0, ic = 0;
            get_cost_surf_min(cost_list, ref ir, ref ic, 1);
            if (ir != 0 || ic != 0)
            {
                MV this_mv = new((short)(start_mv.row + ir * hstep),
                                 (short)(start_mv.col + ic * hstep));
                int dummy = 0;
                check_better_fast(xd, cm, this_mv, ref bestmv, mv_limits, var_params,
                                  mv_cost_params, ref besterr, ref sse1, ref distortion, ref dummy,
                                  is_scaled);
            }
        }
        else
        {
            two_level_checks_fast(xd, cm, start_mv, ref bestmv, hstep, mv_limits,
                                  var_params, mv_cost_params, ref besterr, ref sse1,
                                  ref distortion, iters_per_step, is_scaled);
        }

        // Each subsequent iteration checks at least one point in common with
        // the last iteration could be 2 ( if diag selected) 1/4 pel
        if (forced_stop < HALF_PEL)
        {
            if (check_repeated_mv_and_update(last_mv_search_list, bestmv, iter) != 0)
            {
                return int.MaxValue;
            }
            iter++;

            hstep >>= 1;
            start_mv = bestmv;
            two_level_checks_fast(xd, cm, start_mv, ref bestmv, hstep, mv_limits,
                                  var_params, mv_cost_params, ref besterr, ref sse1,
                                  ref distortion, iters_per_step, is_scaled);
        }

        if (allow_hp != 0 && forced_stop == EIGHTH_PEL)
        {
            if (check_repeated_mv_and_update(last_mv_search_list, bestmv, iter) != 0)
            {
                return int.MaxValue;
            }
            iter++;

            hstep >>= 1;
            start_mv = bestmv;
            two_level_checks_fast(xd, cm, start_mv, ref bestmv, hstep, mv_limits,
                                  var_params, mv_cost_params, ref besterr, ref sse1,
                                  ref distortion, iters_per_step, is_scaled);
        }

        return (int)besterr;
    }

    // [subset] av1_find_best_sub_pixel_tree_pruned / av1_find_best_sub_pixel_tree are never
    // executed: the realtime speed features select SUBPEL_TREE_PRUNED_MORE at speed 11
    // (speed_features.c set_subpel_search_method). They exist so the speed-feature table
    // fractional_mv_search[] can reference them; calling one throws.
    internal static int av1_find_best_sub_pixel_tree_pruned(
        MACROBLOCKD xd, AV1_COMMON cm,
        in SUBPEL_MOTION_SEARCH_PARAMS ms_params, MV start_mv,
        FULLPEL_MV_STATS? start_mv_stats, ref MV bestmv, ref int distortion,
        ref uint sse1, Ptr<int_mv> last_mv_search_list) =>
        throw new NotSupportedException(
            "av1_find_best_sub_pixel_tree_pruned is not in the subset");

    internal static int av1_find_best_sub_pixel_tree(
        MACROBLOCKD xd, AV1_COMMON cm,
        in SUBPEL_MOTION_SEARCH_PARAMS ms_params, MV start_mv,
        FULLPEL_MV_STATS? start_mv_stats, ref MV bestmv, ref int distortion,
        ref uint sse1, Ptr<int_mv> last_mv_search_list) =>
        throw new NotSupportedException(
            "av1_find_best_sub_pixel_tree is not in the subset");

    // Note(yunqingwang): The following 2 functions are only used in the motion
    // vector unit test, which return extreme motion vectors allowed by the MV
    // limits.
    // (Never executed; ported because they are tiny and referenced by speed_features.c.)
    // Returns the maximum MV.
    internal static int av1_return_max_sub_pixel_mv(MACROBLOCKD xd, AV1_COMMON cm,
                                                    in SUBPEL_MOTION_SEARCH_PARAMS ms_params,
                                                    MV start_mv,
                                                    FULLPEL_MV_STATS? start_mv_stats,
                                                    ref MV bestmv, ref int distortion, ref uint sse1,
                                                    Ptr<int_mv> last_mv_search_list)
    {
        int allow_hp = ms_params.allow_hp;
        ref readonly SubpelMvLimits mv_limits = ref ms_params.mv_limits;

        bestmv.row = (short)mv_limits.row_max;
        bestmv.col = (short)mv_limits.col_max;

        uint besterr = 0;

        // In the sub-pel motion search, if hp is not used, then the last bit of mv
        // has to be 0.
        lower_mv_precision(ref bestmv, allow_hp, 0);
        distortion = (int)besterr;
        sse1 = besterr;
        return (int)besterr;
    }

    // Returns the minimum MV.
    internal static int av1_return_min_sub_pixel_mv(MACROBLOCKD xd, AV1_COMMON cm,
                                                    in SUBPEL_MOTION_SEARCH_PARAMS ms_params,
                                                    MV start_mv,
                                                    FULLPEL_MV_STATS? start_mv_stats,
                                                    ref MV bestmv, ref int distortion, ref uint sse1,
                                                    Ptr<int_mv> last_mv_search_list)
    {
        int allow_hp = ms_params.allow_hp;
        ref readonly SubpelMvLimits mv_limits = ref ms_params.mv_limits;

        bestmv.row = (short)mv_limits.row_min;
        bestmv.col = (short)mv_limits.col_min;

        uint besterr = 0;
        // In the sub-pel motion search, if hp is not used, then the last bit of mv
        // has to be 0.
        lower_mv_precision(ref bestmv, allow_hp, 0);
        distortion = (int)besterr;
        sse1 = besterr;
        return (int)besterr;
    }

    // [subset] The OBMC sub-pixel search (estimate_obmc_pref_error ...
    // av1_find_best_obmc_sub_pixel_tree_up) and av1_get_mvpred_sse are never executed
    // (OBMC is off; av1_get_mvpred_sse has no realtime caller).
}
