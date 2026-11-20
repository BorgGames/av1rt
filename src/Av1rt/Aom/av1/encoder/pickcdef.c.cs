// Port of libaom av1/encoder/pickcdef.c

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class pickcdef
{
    // Get primary and secondary filter strength for the given strength index and
    // search method
    private static void get_cdef_filter_strengths(CDEF_PICK_METHOD pick_method,
                                                  ref int pri_strength,
                                                  ref int sec_strength,
                                                  int strength_idx)
    {
        int tot_sec_filter =
            (pick_method == CDEF_FAST_SEARCH_LVL5)
                ? REDUCED_SEC_STRENGTHS_LVL5
                : ((pick_method >= CDEF_FAST_SEARCH_LVL3) ? REDUCED_SEC_STRENGTHS_LVL3
                                                          : CDEF_SEC_STRENGTHS);
        int pri_idx = strength_idx / tot_sec_filter;
        int sec_idx = strength_idx % tot_sec_filter;
        pri_strength = pri_idx;
        sec_strength = sec_idx;
        if (pick_method == CDEF_FULL_SEARCH) return;

        switch (pick_method)
        {
            // [subset] CDEF_FAST_SEARCH_LVL1 .. LVL4: speed 11 picks CDEF_FAST_SEARCH_LVL5 or
            // CDEF_PICK_FROM_Q (by resolution); never executed.
            case CDEF_FAST_SEARCH_LVL5:
                Debug.Assert(pri_idx < REDUCED_PRI_STRENGTHS_LVL4);
                Debug.Assert(sec_idx < REDUCED_SEC_STRENGTHS_LVL5);
                pri_strength = priconv_lvl5[pri_idx];
                sec_strength = secconv_lvl5[sec_idx];
                break;
            default:
                throw new NotSupportedException("get_cdef_filter_strengths: CDEF search method " + pick_method);
        }
    }

    // [port] STORE_CDEF_FILTER_STRENGTH(cdef_strength, pick_method, strength_idx) is expanded
    // at its only call site (av1_cdef_search).

    // [subset] search_one / joint_strength_search: single-plane (monochrome) search, not in the
    // subset (4:2:0 only); never executed.

    // [port] Function-local `uint64_t tot_mse[TOTAL_STRENGTHS][TOTAL_STRENGTHS]` (flattened).
    [ThreadStatic] private static ulong[] s_tot_mse;

    /* Search for the best luma+chroma strength to add as an option, knowing we
       already selected nb_strengths options. */
    // [port] `uint64_t (**mse)[TOTAL_STRENGTHS]` is the jagged mse[pli][sb][strength].
    private static ulong search_one_dual(Ptr<int> lev0, Ptr<int> lev1, int nb_strengths,
                                         ulong[][][] mse, int sb_count,
                                         CDEF_PICK_METHOD pick_method)
    {
        ulong[] tot_mse = s_tot_mse ??= new ulong[TOTAL_STRENGTHS * TOTAL_STRENGTHS];
        int i, j;
        ulong best_tot_mse = (ulong)1 << 63;
        int best_id0 = 0;
        int best_id1 = 0;
        int total_strengths = nb_cdef_strengths[pick_method];
        tot_mse.AsSpan().Clear();
        for (i = 0; i < sb_count; i++)
        {
            int gi;
            ulong best_mse = (ulong)1 << 63;
            /* Find best mse among already selected options. */
            for (gi = 0; gi < nb_strengths; gi++)
            {
                ulong curr = mse[0][i][lev0[gi]];
                curr += mse[1][i][lev1[gi]];
                if (curr < best_mse)
                {
                    best_mse = curr;
                }
            }
            /* Find best mse when adding each possible new option. */
            for (j = 0; j < total_strengths; j++)
            {
                int k;
                for (k = 0; k < total_strengths; k++)
                {
                    ulong best = best_mse;
                    ulong curr = mse[0][i][j];
                    curr += mse[1][i][k];
                    if (curr < best) best = curr;
                    tot_mse[j * TOTAL_STRENGTHS + k] += best;
                }
            }
        }
        for (j = 0; j < total_strengths; j++)
        {
            int k;
            for (k = 0; k < total_strengths; k++)
            {
                if (tot_mse[j * TOTAL_STRENGTHS + k] < best_tot_mse)
                {
                    best_tot_mse = tot_mse[j * TOTAL_STRENGTHS + k];
                    best_id0 = j;
                    best_id1 = k;
                }
            }
        }
        lev0[nb_strengths] = best_id0;
        lev1[nb_strengths] = best_id1;
        return best_tot_mse;
    }

    /* Search for the set of luma+chroma strengths that minimizes mse. */
    private static ulong joint_strength_search_dual(Ptr<int> best_lev0, Ptr<int> best_lev1,
                                                    int nb_strengths,
                                                    ulong[][][] mse,
                                                    int sb_count,
                                                    CDEF_PICK_METHOD pick_method)
    {
        ulong best_tot_mse;
        int i;
        best_tot_mse = (ulong)1 << 63;
        /* Greedy search: add one strength options at a time. */
        for (i = 0; i < nb_strengths; i++)
        {
            best_tot_mse =
                search_one_dual(best_lev0, best_lev1, i, mse, sb_count, pick_method);
        }
        /* Trying to refine the greedy search by reconsidering each
           already-selected option. */
        for (i = 0; i < 4 * nb_strengths; i++)
        {
            int j;
            for (j = 0; j < nb_strengths - 1; j++)
            {
                best_lev0[j] = best_lev0[j + 1];
                best_lev1[j] = best_lev1[j + 1];
            }
            best_tot_mse = search_one_dual(best_lev0, best_lev1, nb_strengths - 1, mse,
                                           sb_count, pick_method);
        }
        return best_tot_mse;
    }

    // [subset] init_src_params, is_dual_or_quad_applicable, compute_cdef_dist (and the
    // CONFIG_AV1_HIGHBITDEPTH compute_cdef_dist_highbd): only used through
    // compute_cdef_dist_fn by the high-bitdepth branch of get_filt_error; never executed.

    // Fill the boundary regions of the block with CDEF_VERY_LARGE, only if the
    // region is outside frame boundary
    private static void fill_borders_for_fbs_on_frame_boundary(
        Ptr<ushort> inbuf, int hfilt_size, int vfilt_size,
        bool is_fb_on_frm_left_boundary, bool is_fb_on_frm_right_boundary,
        bool is_fb_on_frm_top_boundary, bool is_fb_on_frm_bottom_boundary)
    {
        if (!is_fb_on_frm_left_boundary && !is_fb_on_frm_right_boundary &&
            !is_fb_on_frm_top_boundary && !is_fb_on_frm_bottom_boundary)
            return;
        if (is_fb_on_frm_bottom_boundary)
        {
            // Fill bottom region of the block
            int buf_offset =
                (vfilt_size + CDEF_VBORDER) * CDEF_BSTRIDE + CDEF_HBORDER;
            fill_rect(inbuf + buf_offset, CDEF_BSTRIDE, CDEF_VBORDER, hfilt_size,
                      CDEF_VERY_LARGE);
        }
        if (is_fb_on_frm_bottom_boundary || is_fb_on_frm_left_boundary)
        {
            int buf_offset = (vfilt_size + CDEF_VBORDER) * CDEF_BSTRIDE;
            // Fill bottom-left region of the block
            fill_rect(inbuf + buf_offset, CDEF_BSTRIDE, CDEF_VBORDER, CDEF_HBORDER,
                      CDEF_VERY_LARGE);
        }
        if (is_fb_on_frm_bottom_boundary || is_fb_on_frm_right_boundary)
        {
            int buf_offset =
                (vfilt_size + CDEF_VBORDER) * CDEF_BSTRIDE + hfilt_size + CDEF_HBORDER;
            // Fill bottom-right region of the block
            fill_rect(inbuf + buf_offset, CDEF_BSTRIDE, CDEF_VBORDER, CDEF_HBORDER,
                      CDEF_VERY_LARGE);
        }
        if (is_fb_on_frm_top_boundary)
        {
            // Fill top region of the block
            fill_rect(inbuf + CDEF_HBORDER, CDEF_BSTRIDE, CDEF_VBORDER, hfilt_size,
                      CDEF_VERY_LARGE);
        }
        if (is_fb_on_frm_top_boundary || is_fb_on_frm_left_boundary)
        {
            // Fill top-left region of the block
            fill_rect(inbuf, CDEF_BSTRIDE, CDEF_VBORDER, CDEF_HBORDER, CDEF_VERY_LARGE);
        }
        if (is_fb_on_frm_top_boundary || is_fb_on_frm_right_boundary)
        {
            int buf_offset = hfilt_size + CDEF_HBORDER;
            // Fill top-right region of the block
            fill_rect(inbuf + buf_offset, CDEF_BSTRIDE, CDEF_VBORDER, CDEF_HBORDER,
                      CDEF_VERY_LARGE);
        }
        if (is_fb_on_frm_left_boundary)
        {
            int buf_offset = CDEF_VBORDER * CDEF_BSTRIDE;
            // Fill left region of the block
            fill_rect(inbuf + buf_offset, CDEF_BSTRIDE, vfilt_size, CDEF_HBORDER,
                      CDEF_VERY_LARGE);
        }
        if (is_fb_on_frm_right_boundary)
        {
            int buf_offset = CDEF_VBORDER * CDEF_BSTRIDE;
            // Fill right region of the block
            fill_rect(inbuf + (buf_offset + hfilt_size + CDEF_HBORDER), CDEF_BSTRIDE,
                      vfilt_size, CDEF_HBORDER, CDEF_VERY_LARGE);
        }
    }

    // Calculate the number of 8x8/4x4 filter units for which SSE can be calculated
    // after CDEF filtering in single function call
    private static int get_error_calc_width_in_filt_units(
        Ptr<cdef_list> dlist, int cdef_count, int bi, int subsampling_x,
        int subsampling_y)
    {
        // TODO(Ranjit): Extend the optimization for 422
        if (subsampling_x != subsampling_y) return 1;

        // Combining more blocks seems to increase encode time due to increase in
        // control code
        if (bi + 3 < cdef_count && dlist[bi].by == dlist[bi + 3].by &&
            dlist[bi].bx + 3 == dlist[bi + 3].bx)
        {
            /* Calculate error for four 8x8/4x4 blocks using 32x8/16x4 block specific
             * logic if y co-ordinates match and x co-ordinates are
             * separated by 3 for first and fourth 8x8/4x4 blocks in dlist[]. */
            return 4;
        }
        if (bi + 1 < cdef_count && dlist[bi].by == dlist[bi + 1].by &&
            dlist[bi].bx + 1 == dlist[bi + 1].bx)
        {
            /* Calculate error for two 8x8/4x4 blocks using 16x8/8x4 block specific
             * logic if their y co-ordinates match and x co-ordinates are
             * separated by 1 for first and second 8x8/4x4 blocks in dlist[]. */
            return 2;
        }
        return 1;
    }

    // [port] DECLARE_ALIGNED(32, uint8_t, tmp_dst8[1 << (MAX_SB_SIZE_LOG2 * 2)]) of
    // get_filt_error (a leaf function).
    [ThreadStatic] private static byte[] s_tmp_dst8;

    // Returns the block error after CDEF filtering for a given strength
    private static ulong get_filt_error(
        CdefSearchCtx cdef_search_ctx, macroblockd_plane pd,
        Ptr<cdef_list> dlist, int[][] dir, Ptr<int> dirinit,
        int[][] var, Ptr<ushort> @in, Ptr<byte> ref_buffer,
        int ref_stride, int row, int col, int pri_strength, int sec_strength,
        int cdef_count, int pli, int coeff_shift, BLOCK_SIZE bs)
    {
        ulong curr_sse = 0;
        BLOCK_SIZE plane_bsize =
            get_plane_block_size(bs, pd.subsampling_x, pd.subsampling_y);
        int bw_log2 = 3 - pd.subsampling_x;
        int bh_log2 = 3 - pd.subsampling_y;

        // TODO(Ranjit): Extend this optimization for HBD
        if (!cdef_search_ctx.use_highbitdepth)
        {
            // If all 8x8/4x4 blocks in CDEF block need to be filtered, calculate the
            // error at CDEF block level
            int tot_blk_count =
                (block_size_wide[plane_bsize] * block_size_high[plane_bsize]) >>
                (bw_log2 + bh_log2);
            if (cdef_count == tot_blk_count)
            {
                int buf_offset = row * ref_stride + col;
                int dst_offset = row * pd.dst.stride + col;
                if (pri_strength == 0 && sec_strength == 0)
                {
                    // When CDEF strength is zero, filtering is not applied. Hence
                    // error is calculated between source and unfiltered pixels
                    curr_sse =
                        (ulong)aom_sse(ref_buffer + buf_offset, ref_stride,
                                       pd.dst.buf + dst_offset, pd.dst.stride,
                                       block_size_wide[plane_bsize], block_size_high[plane_bsize]);
                }
                else
                {
                    byte[] tmp_dst8 = s_tmp_dst8 ??= new byte[1 << (MAX_SB_SIZE_LOG2 * 2)];

                    av1_cdef_filter_fb(tmp_dst8, Ptr<ushort>.Null, (1 << MAX_SB_SIZE_LOG2), @in,
                                       cdef_search_ctx.xdec[pli],
                                       cdef_search_ctx.ydec[pli], dir, dirinit, var, pli,
                                       dlist, cdef_count, pri_strength,
                                       sec_strength + B(sec_strength == 3),
                                       cdef_search_ctx.damping, coeff_shift);
                    curr_sse =
                        (ulong)aom_sse(ref_buffer + buf_offset, ref_stride, tmp_dst8,
                                       (1 << MAX_SB_SIZE_LOG2), block_size_wide[plane_bsize],
                                       block_size_high[plane_bsize]);
                }
            }
            else
            {
                // If few 8x8/4x4 blocks in CDEF block need to be filtered, filtering
                // functions produce 8-bit output and the error is calculated in 8-bit
                // domain
                if (pri_strength == 0 && sec_strength == 0)
                {
                    int num_error_calc_filt_units = 1;
                    for (int bi = 0; bi < cdef_count; bi = bi + num_error_calc_filt_units)
                    {
                        byte by = dlist[bi].by;
                        byte bx = dlist[bi].bx;
                        int by_pos = by << bh_log2;
                        int bx_pos = bx << bw_log2;
                        int buf_offset =
                            (row + by_pos) * ref_stride + (col + bx_pos);
                        int dst_offset =
                            (row + by_pos) * pd.dst.stride + (col + bx_pos);
                        num_error_calc_filt_units = get_error_calc_width_in_filt_units(
                            dlist, cdef_count, bi, pd.subsampling_x, pd.subsampling_y);
                        curr_sse +=
                            (ulong)aom_sse(ref_buffer + buf_offset, ref_stride,
                                           pd.dst.buf + dst_offset, pd.dst.stride,
                                           num_error_calc_filt_units * (1 << bw_log2), 1 << bh_log2);
                    }
                }
                else
                {
                    byte[] tmp_dst8 = s_tmp_dst8 ??= new byte[1 << (MAX_SB_SIZE_LOG2 * 2)];
                    av1_cdef_filter_fb(tmp_dst8, Ptr<ushort>.Null, (1 << MAX_SB_SIZE_LOG2), @in,
                                       cdef_search_ctx.xdec[pli],
                                       cdef_search_ctx.ydec[pli], dir, dirinit, var, pli,
                                       dlist, cdef_count, pri_strength,
                                       sec_strength + B(sec_strength == 3),
                                       cdef_search_ctx.damping, coeff_shift);
                    int num_error_calc_filt_units = 1;
                    for (int bi = 0; bi < cdef_count; bi = bi + num_error_calc_filt_units)
                    {
                        byte by = dlist[bi].by;
                        byte bx = dlist[bi].bx;
                        int by_pos = by << bh_log2;
                        int bx_pos = bx << bw_log2;
                        int buf_offset =
                            (row + by_pos) * ref_stride + (col + bx_pos);
                        int tmp_buf_offset =
                            by_pos * (1 << MAX_SB_SIZE_LOG2) + bx_pos;
                        num_error_calc_filt_units = get_error_calc_width_in_filt_units(
                            dlist, cdef_count, bi, pd.subsampling_x, pd.subsampling_y);
                        curr_sse += (ulong)aom_sse(
                            ref_buffer + buf_offset, ref_stride, new Ptr<byte>(tmp_dst8, tmp_buf_offset),
                            (1 << MAX_SB_SIZE_LOG2),
                            num_error_calc_filt_units * (1 << bw_log2), (1 << bh_log2));
                    }
                }
            }
        }
        else
        {
            // [subset] 16-bit CDEF output + compute_cdef_dist_fn: high bitdepth only.
            throw new NotSupportedException("get_filt_error: high bitdepth");
        }
        return curr_sse;
    }

    // [port] Function-local buffers of av1_cdef_mse_calc_block (not re-entered on a thread;
    // the MT search calls it from each worker thread).
    private sealed class mse_calc_block_scratch
    {
        public readonly ushort[] inbuf = new ushort[CDEF_INBUF_SIZE];
        public readonly cdef_list[] dlist = new cdef_list[MI_SIZE_128X128 * MI_SIZE_128X128];
        public readonly int[][] dir = new_jagged<int>(CDEF_NBLOCKS, CDEF_NBLOCKS);
        public readonly int[][] var = new_jagged<int>(CDEF_NBLOCKS, CDEF_NBLOCKS);
        public readonly int[] dirinit = new int[1];
    }

    [ThreadStatic] private static mse_calc_block_scratch s_mse_calc_block_scratch;

    // Calculates MSE at block level.
    // Inputs:
    //   cdef_search_ctx: Pointer to the structure containing parameters related to
    //   CDEF search context.
    //   fbr: Row index in units of 64x64 block
    //   fbc: Column index in units of 64x64 block
    //   adaptive_cdef_mode: Speed feature to control CDEF adaptively.
    // Returns:
    //   Nothing will be returned. Contents of cdef_search_ctx will be modified.
    internal static void av1_cdef_mse_calc_block(CdefSearchCtx cdef_search_ctx,
                                                 aom_internal_error_info error_info,
                                                 int fbr, int fbc, int sb_count,
                                                 int adaptive_cdef_mode)
    {
        // TODO(aomedia:3276): Pass error_info to the low-level functions as required
        // in future to handle error propagation.
        CommonModeInfoParams mi_params = cdef_search_ctx.mi_params;
        YV12_BUFFER_CONFIG @ref = cdef_search_ctx.@ref;
        int coeff_shift = cdef_search_ctx.coeff_shift;
        int[] mi_wide_l2 = cdef_search_ctx.mi_wide_l2;
        int[] mi_high_l2 = cdef_search_ctx.mi_high_l2;

        // Declare and initialize the temporary buffers.
        mse_calc_block_scratch scratch = s_mse_calc_block_scratch ??= new mse_calc_block_scratch();
        ushort[] inbuf = scratch.inbuf;
        cdef_list[] dlist = scratch.dlist;
        int[][] dir = scratch.dir;
        int[][] var = scratch.var;
        av1_zero(dir);
        av1_zero(var);
        Ptr<ushort> @in = new Ptr<ushort>(inbuf, CDEF_VBORDER * CDEF_BSTRIDE + CDEF_HBORDER);
        int nhb = AOMMIN(MI_SIZE_64X64, mi_params.mi_cols - MI_SIZE_64X64 * fbc);
        int nvb = AOMMIN(MI_SIZE_64X64, mi_params.mi_rows - MI_SIZE_64X64 * fbr);
        int hb_step = 1, vb_step = 1;
        BLOCK_SIZE bs;

        MB_MODE_INFO mbmi =
            mi_params.mi_grid_base[MI_SIZE_64X64 * fbr * mi_params.mi_stride +
                                   MI_SIZE_64X64 * fbc];

        Span<int> ref_stride = stackalloc int[MAX_MB_PLANE];
        ref_stride[0] = @ref.y_stride;
        ref_stride[1] = @ref.uv_stride;
        ref_stride[2] = @ref.uv_stride;

        if (mbmi.bsize == BLOCK_128X128 || mbmi.bsize == BLOCK_128X64 ||
            mbmi.bsize == BLOCK_64X128)
        {
            bs = mbmi.bsize;
            if (bs == BLOCK_128X128 || bs == BLOCK_128X64)
            {
                nhb = AOMMIN(MI_SIZE_128X128, mi_params.mi_cols - MI_SIZE_64X64 * fbc);
                hb_step = 2;
            }
            if (bs == BLOCK_128X128 || bs == BLOCK_64X128)
            {
                nvb = AOMMIN(MI_SIZE_128X128, mi_params.mi_rows - MI_SIZE_64X64 * fbr);
                vb_step = 2;
            }
        }
        else
        {
            bs = BLOCK_64X64;
        }
        // Get number of 8x8 blocks which are not skip. Cdef processing happens for
        // 8x8 blocks which are not skip.
        int cdef_count = av1_cdef_compute_sb_list(
            mi_params, fbr * MI_SIZE_64X64, fbc * MI_SIZE_64X64, dlist, bs);
        bool is_fb_on_frm_left_boundary = (fbc == 0);
        bool is_fb_on_frm_right_boundary =
            (fbc + hb_step == cdef_search_ctx.nhfb);
        bool is_fb_on_frm_top_boundary = (fbr == 0);
        bool is_fb_on_frm_bottom_boundary =
            (fbr + vb_step == cdef_search_ctx.nvfb);
        int yoff = CDEF_VBORDER * B(!is_fb_on_frm_top_boundary);
        int xoff = CDEF_HBORDER * B(!is_fb_on_frm_left_boundary);
        Ptr<int> dirinit = scratch.dirinit;
        dirinit.Value = 0;
        for (int pli = 0; pli < cdef_search_ctx.num_planes; pli++)
        {
            // To disable CDEF filter of chroma, set MSE of chroma strength index 0 to
            // zero and all non-zero strength indices to 1, such that the joint
            // luma-chroma strength search always chooses filter strength 0 for chroma
            // due to least cost.
            if (adaptive_cdef_mode > 0 && pli > 0)
            {
                // [subset] adaptive_cdef_mode is 0 at speed 11 (RT); never executed.
                throw new NotSupportedException("av1_cdef_mse_calc_block: adaptive_cdef_mode");
            }

            /* We avoid filtering the pixels for which some of the pixels to
            average are outside the frame. We could change the filter instead,
            but it would add special cases for any future vectorization. */
            int hfilt_size = (nhb << mi_wide_l2[pli]);
            int vfilt_size = (nvb << mi_high_l2[pli]);
            int ysize =
                vfilt_size + CDEF_VBORDER * B(!is_fb_on_frm_bottom_boundary) + yoff;
            int xsize =
                hfilt_size + CDEF_HBORDER * B(!is_fb_on_frm_right_boundary) + xoff;
            int row = fbr * MI_SIZE_64X64 << mi_high_l2[pli];
            int col = fbc * MI_SIZE_64X64 << mi_wide_l2[pli];
            // [port] `struct macroblockd_plane pd = cdef_search_ctx->plane[pli]` is a read-only
            // copy; the class instance is used directly.
            macroblockd_plane pd = cdef_search_ctx.plane[pli];
            cdef_search_ctx.copy_fn(@in + (-yoff * CDEF_BSTRIDE - xoff), CDEF_BSTRIDE,
                                    pd.dst.buf, row - yoff, col - xoff, pd.dst.stride,
                                    ysize, xsize);
            fill_borders_for_fbs_on_frame_boundary(
                inbuf, hfilt_size, vfilt_size, is_fb_on_frm_left_boundary,
                is_fb_on_frm_right_boundary, is_fb_on_frm_top_boundary,
                is_fb_on_frm_bottom_boundary);
            Ptr<byte> ref_buffer = pli == 0 ? @ref.y_buffer : pli == 1 ? @ref.u_buffer : @ref.v_buffer;
            for (int gi = 0; gi < cdef_search_ctx.total_strengths; gi++)
            {
                int pri_strength = 0, sec_strength = 0;
                get_cdef_filter_strengths(cdef_search_ctx.pick_method, ref pri_strength,
                                          ref sec_strength, gi);
                ulong curr_mse = get_filt_error(
                    cdef_search_ctx, pd, dlist, dir, dirinit, var, @in, ref_buffer,
                    ref_stride[pli], row, col, pri_strength, sec_strength, cdef_count,
                    pli, coeff_shift, bs);
                if (pli < 2)
                    cdef_search_ctx.mse[pli][sb_count][gi] = curr_mse;
                else
                    cdef_search_ctx.mse[1][sb_count][gi] += curr_mse;
            }
        }
        cdef_search_ctx.sb_index[sb_count] =
            MI_SIZE_64X64 * fbr * mi_params.mi_stride + MI_SIZE_64X64 * fbc;
    }

    // MSE calculation at frame level.
    // Inputs:
    //   cdef_search_ctx: Pointer to the structure containing parameters related to
    //   CDEF search context.
    //   adaptive_cdef_mode: Speed feature to control CDEF adaptively.
    // Returns:
    //   Nothing will be returned. Contents of cdef_search_ctx will be modified.
    private static void cdef_mse_calc_frame(CdefSearchCtx cdef_search_ctx,
                                            aom_internal_error_info error_info,
                                            int adaptive_cdef_mode)
    {
        // Loop over each sb.
        for (int fbr = 0; fbr < cdef_search_ctx.nvfb; ++fbr)
        {
            for (int fbc = 0; fbc < cdef_search_ctx.nhfb; ++fbc)
            {
                // Checks if cdef processing can be skipped for particular sb.
                if (cdef_sb_skip(cdef_search_ctx.mi_params, fbr, fbc) != 0) continue;
                // Calculate mse for each sb and store the relevant sb index.
                av1_cdef_mse_calc_block(cdef_search_ctx, error_info, fbr, fbc,
                                        cdef_search_ctx.sb_count, adaptive_cdef_mode);
                cdef_search_ctx.sb_count++;
            }
        }
    }

    // Allocates memory for members of CdefSearchCtx.
    // Inputs:
    //   cdef_search_ctx: Pointer to the structure containing parameters
    //   related to CDEF search context.
    // Returns:
    //   Nothing will be returned. Contents of cdef_search_ctx will be modified.
    private static void cdef_alloc_data(AV1_COMMON cm, CdefSearchCtx cdef_search_ctx)
    {
        int nvfb = cdef_search_ctx.nvfb;
        int nhfb = cdef_search_ctx.nhfb;
        // TODO(perf): per-frame allocation (the C code mallocs per frame too).
        cdef_search_ctx.sb_index = new int[nvfb * nhfb];
        cdef_search_ctx.sb_count = 0;
        cdef_search_ctx.mse[0] = new_jagged<ulong>(nvfb * nhfb, TOTAL_STRENGTHS);
        cdef_search_ctx.mse[1] = new_jagged<ulong>(nvfb * nhfb, TOTAL_STRENGTHS);
    }

    // Deallocates the memory allocated for members of CdefSearchCtx.
    // Inputs:
    //   cdef_search_ctx: Pointer to the structure containing parameters
    //   related to CDEF search context.
    // Returns:
    //   Nothing will be returned.
    internal static void av1_cdef_dealloc_data(CdefSearchCtx cdef_search_ctx)
    {
        if (cdef_search_ctx != null)
        {
            cdef_search_ctx.mse[0] = null;
            cdef_search_ctx.mse[1] = null;
            cdef_search_ctx.sb_index = Ptr<int>.Null;
        }
    }

    private static readonly copy_fn_t s_copy_sb8_16_lowbd = av1_cdef_copy_sb8_16_lowbd;

    // Initialize the parameters related to CDEF search context.
    // Inputs:
    //   frame: Pointer to compressed frame buffer
    //   ref: Pointer to the frame buffer holding the source frame
    //   cm: Pointer to top level common structure
    //   xd: Pointer to common current coding block structure
    //   cdef_search_ctx: Pointer to the structure containing parameters related to
    //   CDEF search context.
    //   pick_method: Search method used to select CDEF parameters
    // Returns:
    //   Nothing will be returned. Contents of cdef_search_ctx will be modified.
    private static void cdef_params_init(YV12_BUFFER_CONFIG frame,
                                         YV12_BUFFER_CONFIG @ref,
                                         AV1_COMMON cm, MACROBLOCKD xd,
                                         CdefSearchCtx cdef_search_ctx,
                                         CDEF_PICK_METHOD pick_method)
    {
        CommonModeInfoParams mi_params = cm.mi_params;
        int num_planes = av1_num_planes(cm);
        cdef_search_ctx.mi_params = cm.mi_params;
        cdef_search_ctx.@ref = @ref;
        cdef_search_ctx.nvfb =
            (mi_params.mi_rows + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;
        cdef_search_ctx.nhfb =
            (mi_params.mi_cols + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;
        cdef_search_ctx.coeff_shift = AOMMAX(cm.seq_params.bit_depth - 8, 0);
        cdef_search_ctx.damping = 3 + (cm.quant_params.base_qindex >> 6);
        cdef_search_ctx.total_strengths = nb_cdef_strengths[pick_method];
        cdef_search_ctx.num_planes = num_planes;
        cdef_search_ctx.pick_method = pick_method;
        cdef_search_ctx.sb_count = 0;
        cdef_search_ctx.use_highbitdepth = cm.seq_params.use_highbitdepth != 0;
        av1_setup_dst_planes(xd.plane, cm.seq_params.sb_size, frame, 0, 0, 0,
                             num_planes);
        // Initialize plane wise information.
        for (int pli = 0; pli < num_planes; pli++)
        {
            cdef_search_ctx.xdec[pli] = xd.plane[pli].subsampling_x;
            cdef_search_ctx.ydec[pli] = xd.plane[pli].subsampling_y;
            cdef_search_ctx.bsize[pli] =
                cdef_search_ctx.ydec[pli] != 0
                    ? (cdef_search_ctx.xdec[pli] != 0 ? BLOCK_4X4 : BLOCK_8X4)
                    : (cdef_search_ctx.xdec[pli] != 0 ? BLOCK_4X8 : BLOCK_8X8);
            cdef_search_ctx.mi_wide_l2[pli] =
                MI_SIZE_LOG2 - xd.plane[pli].subsampling_x;
            cdef_search_ctx.mi_high_l2[pli] =
                MI_SIZE_LOG2 - xd.plane[pli].subsampling_y;
            cdef_search_ctx.plane[pli].CopyFrom(xd.plane[pli]);
        }
        // Function pointer initialization.
        cdef_search_ctx.copy_fn = s_copy_sb8_16_lowbd;
        // [subset] compute_cdef_dist is only called by the high-bitdepth branch of
        // get_filt_error; not ported, so the pointer stays null.
        cdef_search_ctx.compute_cdef_dist_fn = null;
    }

    internal static void av1_pick_cdef_from_qp(AV1_COMMON cm, int skip_cdef,
                                               int is_screen_content, bool avoid_uv_cdef)
    {
        int bd = cm.seq_params.bit_depth;
        int q =
            av1_ac_quant_QTX(cm.quant_params.base_qindex, 0, bd) >> (bd - 8);
        CdefInfo cdef_info = cm.cdef_info;
        // Check the speed feature to avoid extra signaling.
        if (skip_cdef != 0)
        {
            cdef_info.cdef_bits = 1;
            cdef_info.nb_cdef_strengths = 2;
        }
        else
        {
            cdef_info.cdef_bits = 0;
            cdef_info.nb_cdef_strengths = 1;
        }
        cdef_info.cdef_damping = 3 + (cm.quant_params.base_qindex >> 6);

        int predicted_y_f1 = 0;
        int predicted_y_f2 = 0;
        int predicted_uv_f1 = 0;
        int predicted_uv_f2 = 0;
        if (is_screen_content != 0)
        {
            // [subset] Screen-content model: tune_content is default in the subset; never
            // executed.
            throw new NotSupportedException("av1_pick_cdef_from_qp: screen content");
        }
        else
        {
            if (frame_is_intra_only(cm) == 0)
            {
                predicted_y_f1 = clamp((int)roundf(q * q * -0.0000023593946f +
                                                   q * 0.0068615186f + 0.02709886f),
                                       0, 15);
                predicted_y_f2 = clamp((int)roundf(q * q * -0.00000057629734f +
                                                   q * 0.0013993345f + 0.03831067f),
                                       0, 3);
                predicted_uv_f1 = clamp((int)roundf(q * q * -0.0000007095069f +
                                                    q * 0.0034628846f + 0.00887099f),
                                        0, 15);
                predicted_uv_f2 = clamp((int)roundf(q * q * 0.00000023874085f +
                                                    q * 0.00028223585f + 0.05576307f),
                                        0, 3);
            }
            else
            {
                predicted_y_f1 = clamp(
                    (int)roundf(q * q * 0.0000033731974f + q * 0.008070594f + 0.0187634f),
                    0, 15);
                predicted_y_f2 = clamp((int)roundf(q * q * 0.0000029167343f +
                                                   q * 0.0027798624f + 0.0079405f),
                                       0, 3);
                predicted_uv_f1 = clamp((int)roundf(q * q * -0.0000130790995f +
                                                    q * 0.012892405f - 0.00748388f),
                                        0, 15);
                predicted_uv_f2 = clamp((int)roundf(q * q * 0.0000032651783f +
                                                    q * 0.00035520183f + 0.00228092f),
                                        0, 3);
            }
        }
        cdef_info.cdef_strengths[0] =
            predicted_y_f1 * CDEF_SEC_STRENGTHS + predicted_y_f2;
        cdef_info.cdef_uv_strengths[0] =
            avoid_uv_cdef ? 0
                          : predicted_uv_f1 * CDEF_SEC_STRENGTHS + predicted_uv_f2;

        // mbmi->cdef_strength is already set in the encoding stage. We don't need to
        // set it again here, unless the first strength is 0 too.
        if (skip_cdef != 0)
        {
            cdef_info.cdef_strengths[1] = 0;
            cdef_info.cdef_uv_strengths[1] = 0;
            if (cdef_info.cdef_strengths[0] != 0 || cdef_info.cdef_uv_strengths[0] != 0) return;
            // Both strengths are 0, so signal only one. This saves the per block
            // strength index and lets the decoder skip CDEF for the frame.
            cdef_info.cdef_bits = 0;
            cdef_info.nb_cdef_strengths = 1;
        }

        CommonModeInfoParams mi_params = cm.mi_params;
        int nvfb = (mi_params.mi_rows + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;
        int nhfb = (mi_params.mi_cols + MI_SIZE_64X64 - 1) / MI_SIZE_64X64;
        MB_MODE_INFO[] grid = mi_params.mi_grid_base;
        // mbmi is NULL when real-time rate control library is used.
        if (grid == null) return;
        int mbmi = 0;
        for (int r = 0; r < nvfb; ++r)
        {
            for (int c = 0; c < nhfb; ++c)
            {
                MB_MODE_INFO current_mbmi = grid[mbmi + MI_SIZE_64X64 * c];
                current_mbmi.cdef_strength = 0;
            }
            mbmi += MI_SIZE_64X64 * mi_params.mi_stride;
        }
    }

    // C roundf: round half away from zero, in float.
    private static float roundf(float x) => MathF.Round(x, MidpointRounding.AwayFromZero);

    internal static void av1_cdef_search(AV1_COMP cpi)
    {
        AV1_COMMON cm = cpi.common;
        CDEF_CONTROL cdef_control = cpi.oxcf.tool_cfg.cdef_control;

        Debug.Assert(cdef_control != CDEF_NONE);
        // [subset] CDEF_REFERENCE (skip non-reference frames) and CDEF_ADAPTIVE (apply_adaptive_cdef:
        // early exit around qindex 32, strength reduction/zeroing, avoid_uv_cdef): the subset
        // runs with CDEF on for all frames (CDEF_ALL); never executed.
        if (cdef_control != CDEF_ALL)
            throw new NotSupportedException("av1_cdef_search: cdef_control " + cdef_control);
        const bool apply_adaptive_cdef = false;

        // Indicate if external RC is used for testing
        int rtc_ext_rc = cpi.rc.rtc_external_ratectrl;
        if (rtc_ext_rc != 0)
        {
            // [subset] External rate control is not in the subset; never executed.
            throw new NotSupportedException("av1_cdef_search: rtc_external_ratectrl");
        }
        CDEF_PICK_METHOD pick_method = cpi.sf.lpf_sf.cdef_pick_method;
        if (pick_method == CDEF_PICK_FROM_Q)
        {
            int use_screen_content_model = B(
                cm.quant_params.base_qindex >
                    AOMMAX(cpi.sf.rt_sf.screen_content_cdef_filter_qindex_thresh,
                           cpi.rc.best_quality + 5) &&
                cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN);

            // For adaptive CDEF, do not apply CDEF to chroma channels.
            // This is done to reduce decode time, as CDEF is a relatively-expensive
            // filter to compute.
            bool avoid_uv_cdef = apply_adaptive_cdef;

            av1_pick_cdef_from_qp(cm, B(cpi.sf.rt_sf.skip_cdef_sb != 0),
                                  use_screen_content_model, avoid_uv_cdef);
            return;
        }
        CommonModeInfoParams mi_params = cm.mi_params;
        int damping = 3 + (cm.quant_params.base_qindex >> 6);
        int fast = B(pick_method >= CDEF_FAST_SEARCH_LVL1 &&
                     pick_method <= CDEF_FAST_SEARCH_LVL5);
        int adaptive_cdef_mode = cpi.sf.lpf_sf.adaptive_cdef_mode;
        int num_planes = av1_num_planes(cm);
        MACROBLOCKD xd = cpi.td.mb.e_mbd;

        if (cpi.cdef_search_ctx == null)
            cpi.cdef_search_ctx = new CdefSearchCtx();
        CdefSearchCtx cdef_search_ctx = cpi.cdef_search_ctx;

        // Initialize parameters related to CDEF search context.
        cdef_params_init(cm.cur_frame.buf, cpi.source, cm, xd, cdef_search_ctx,
                         pick_method);
        // Allocate CDEF search context buffers.
        cdef_alloc_data(cm, cdef_search_ctx);
        // Frame level mse calculation.
        if (cpi.mt_info.num_mod_workers[MOD_CDEF_SEARCH] > 1)
        {
            av1_cdef_mse_calc_frame_mt(cpi);
        }
        else
        {
            cdef_mse_calc_frame(cdef_search_ctx, cm.error, adaptive_cdef_mode);
        }

        /* Search for different number of signaling bits. */
        int nb_strength_bits = 0;
        ulong best_rd = ulong.MaxValue;
        CdefInfo cdef_info = cm.cdef_info;
        int sb_count = cdef_search_ctx.sb_count;
        ulong[][][] mse = cdef_search_ctx.mse;
        /* Calculate the maximum number of bits required to signal CDEF strengths at
         * block level */
        int total_strengths = nb_cdef_strengths[pick_method];
        int joint_strengths =
            num_planes > 1 ? total_strengths * total_strengths : total_strengths;
        int max_signaling_bits =
            joint_strengths == 1 ? 0 : get_msb(joint_strengths - 1) + 1;
        int rdmult = cpi.td.mb.rdmult;

        // [subset] should_reduce_cdef_strengths / should_zero_cdef_strengths are false
        // (apply_adaptive_cdef is false), so min_signaling_bits is 0 and the strength
        // reduction below is not ported.
        const int min_signaling_bits = 0;

        for (int i = min_signaling_bits; i <= 3; i++)
        {
            if (i > max_signaling_bits) break;
            int[] best_lev0 = new int[CDEF_MAX_STRENGTHS];
            int[] best_lev1 = new int[CDEF_MAX_STRENGTHS];
            int nb_strengths = 1 << i;
            ulong tot_mse;
            if (num_planes > 1)
            {
                tot_mse = joint_strength_search_dual(best_lev0, best_lev1, nb_strengths,
                                                     mse, sb_count, pick_method);
            }
            else
            {
                // [subset] joint_strength_search (monochrome): not in the subset.
                throw new NotSupportedException("av1_cdef_search: monochrome");
            }

            int total_bits = sb_count * i + nb_strengths * CDEF_STRENGTH_BITS *
                                                (num_planes > 1 ? 2 : 1);
            int rate_cost = av1_cost_literal(total_bits);
            ulong dist = tot_mse * 16;
            ulong rd = (ulong)RDCOST(rdmult, rate_cost, (long)dist);
            if (rd < best_rd)
            {
                best_rd = rd;
                nb_strength_bits = i;
                memcpy<int>(cdef_info.cdef_strengths, best_lev0, nb_strengths);
                if (num_planes > 1)
                {
                    memcpy<int>(cdef_info.cdef_uv_strengths, best_lev1, nb_strengths);
                }
            }
        }

        cdef_info.cdef_bits = nb_strength_bits;
        cdef_info.nb_cdef_strengths = 1 << nb_strength_bits;
        ulong tot_best_mse_y = 0, tot_zero_mse_y = 0;
        for (int i = 0; i < sb_count; i++)
        {
            ulong best_mse = ulong.MaxValue;
            int best_gi = 0;
            tot_zero_mse_y += mse[0][i][0];
            for (int gi = 0; gi < cdef_info.nb_cdef_strengths; gi++)
            {
                ulong curr = mse[0][i][cdef_info.cdef_strengths[gi]];
                if (num_planes > 1) curr += mse[1][i][cdef_info.cdef_uv_strengths[gi]];
                if (curr < best_mse)
                {
                    best_gi = gi;
                    best_mse = curr;
                }
            }
            // When adaptive_cdef_mode is enabled, CDEF filter of chroma is disabled by
            // setting the MSE at chroma strength index 0 to zero, ensuring zero filter
            // strength always wins for chroma. Hence, best_mse reflects luma MSE only.
            tot_best_mse_y += best_mse;
            mi_params.mi_grid_base[cdef_search_ctx.sb_index[i]].cdef_strength =
                (sbyte)best_gi;
        }

        // Disable CDEF filter of luma if the MSE improvement over zero filter
        // strength is below the threshold.
        if (cm.current_frame.pyramid_level > 1 &&
            cpi.sf.lpf_sf.adaptive_cdef_mode > 0 && sb_count > 0)
        {
            // [subset] adaptive_cdef_mode is 0 at speed 11; never executed.
            throw new NotSupportedException("av1_cdef_search: adaptive_cdef_mode luma disable");
        }

        if (fast != 0)
        {
            for (int j = 0; j < cdef_info.nb_cdef_strengths; j++)
            {
                int luma_strength = cdef_info.cdef_strengths[j];
                int pri_strength = 0, sec_strength = 0;

                // STORE_CDEF_FILTER_STRENGTH(cdef_info->cdef_strengths[j], pick_method,
                //                            luma_strength);
                get_cdef_filter_strengths(pick_method, ref pri_strength, ref sec_strength,
                                          luma_strength);
                cdef_info.cdef_strengths[j] = pri_strength * CDEF_SEC_STRENGTHS + sec_strength;
                if (num_planes > 1)
                {
                    int chroma_strength = cdef_info.cdef_uv_strengths[j];
                    // STORE_CDEF_FILTER_STRENGTH(cdef_info->cdef_uv_strengths[j], pick_method,
                    //                            chroma_strength);
                    get_cdef_filter_strengths(pick_method, ref pri_strength, ref sec_strength,
                                              chroma_strength);
                    cdef_info.cdef_uv_strengths[j] =
                        pri_strength * CDEF_SEC_STRENGTHS + sec_strength;
                }
            }
        }

        // [subset] CDEF strength reduction (should_reduce_cdef_strengths): CDEF_ADAPTIVE only.

        cdef_info.cdef_damping = damping;
        // Deallocate CDEF search context buffers.
        av1_cdef_dealloc_data(cdef_search_ctx);
    }
}
