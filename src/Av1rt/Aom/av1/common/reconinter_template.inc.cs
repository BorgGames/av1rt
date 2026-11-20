// Port of libaom av1/common/reconinter_template.inc
// [port] The template is #included once in the encoder (reconinter_enc.c, IS_DEC = 0); the
// decoder instantiation (decodeframe.c, IS_DEC = 1) is not part of the port. The IS_DEC = 0
// variant is ported here as members of reconinter_enc.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class reconinter_enc
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void build_one_inter_predictor(
        Ptr<byte> dst, int dst_stride, in MV src_mv,
        ref InterPredParams inter_pred_params)
    {
        SubpelParams subpel_params = default;
        Ptr<byte> src = default;
        int src_stride = 0;
        enc_calc_subpel_params(in src_mv, ref inter_pred_params, ref src, ref subpel_params,
                               ref src_stride);
        if (inter_pred_params.comp_mode == UNIFORM_SINGLE ||
            inter_pred_params.comp_mode == UNIFORM_COMP)
        {
            av1_make_inter_predictor(src, src_stride, dst, dst_stride,
                                     ref inter_pred_params, in subpel_params);
        }
        else
        {
            // [subset] av1_make_masked_inter_predictor: masked compound prediction never
            // executes in the subset.
            throw new NotSupportedException("build_one_inter_predictor: MASK_COMP");
        }
    }

    // True if the following hold:
    //  1. Not intrabc and not build_for_obmc
    //  2. At least one dimension is size 4 with subsampling
    //  3. If sub-sampled, none of the previous blocks around the sub-sample
    //     are intrabc or inter-blocks
    private static bool is_sub8x8_inter(MACROBLOCKD xd, int plane, BLOCK_SIZE bsize,
                                        int is_intrabc, int build_for_obmc)
    {
        if (is_intrabc != 0 || build_for_obmc != 0)
        {
            return false;
        }

        macroblockd_plane pd = xd.plane[plane];
        int ss_x = pd.subsampling_x;
        int ss_y = pd.subsampling_y;
        int is_sub4_x = B((block_size_wide[bsize] == 4) && ss_x != 0);
        int is_sub4_y = B((block_size_high[bsize] == 4) && ss_y != 0);
        if (is_sub4_x == 0 && is_sub4_y == 0)
        {
            return false;
        }

        // For sub8x8 chroma blocks, we may be covering more than one luma block's
        // worth of pixels. Thus (mi_x, mi_y) may not be the correct coordinates for
        // the top-left corner of the prediction source - the correct top-left corner
        // is at (pre_x, pre_y).
        int row_start = is_sub4_y != 0 ? -1 : 0;
        int col_start = is_sub4_x != 0 ? -1 : 0;

        for (int row = row_start; row <= 0; ++row)
        {
            for (int col = col_start; col <= 0; ++col)
            {
                MB_MODE_INFO this_mbmi = xd.mi[row * xd.mi_stride + col];
                if (is_inter_block(this_mbmi) == 0) return false;
                if (is_intrabc_block(this_mbmi) != 0) return false;
            }
        }
        return true;
    }

    // [port] Not executed in the oracle runs (no 4xN / Nx4 inter blocks were chosen), but it
    // depends only on the block size, so it is ported.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void build_inter_predictors_sub8x8(AV1_COMMON cm,
                                                      MACROBLOCKD xd, int plane,
                                                      MB_MODE_INFO mi,
                                                      int mi_x, int mi_y)
    {
        BLOCK_SIZE bsize = mi.bsize;
        macroblockd_plane pd = xd.plane[plane];
        bool ss_x = pd.subsampling_x != 0;
        bool ss_y = pd.subsampling_y != 0;
        int b4_w = block_size_wide[bsize] >> B(ss_x);
        int b4_h = block_size_high[bsize] >> B(ss_y);
        BLOCK_SIZE plane_bsize = get_plane_block_size(bsize, B(ss_x), B(ss_y));
        int b8_w = block_size_wide[plane_bsize];
        int b8_h = block_size_high[plane_bsize];
        int is_compound = has_second_ref(mi);
        Debug.Assert(is_compound == 0);
        Debug.Assert(is_intrabc_block(mi) == 0);

        // For sub8x8 chroma blocks, we may be covering more than one luma block's
        // worth of pixels. Thus (mi_x, mi_y) may not be the correct coordinates for
        // the top-left corner of the prediction source - the correct top-left corner
        // is at (pre_x, pre_y).
        int row_start = (block_size_high[bsize] == 4) && ss_y ? -1 : 0;
        int col_start = (block_size_wide[bsize] == 4) && ss_x ? -1 : 0;
        int pre_x = (mi_x + MI_SIZE * col_start) >> B(ss_x);
        int pre_y = (mi_y + MI_SIZE * row_start) >> B(ss_y);

        int row = row_start;
        for (int y = 0; y < b8_h; y += b4_h)
        {
            int col = col_start;
            for (int x = 0; x < b8_w; x += b4_w)
            {
                MB_MODE_INFO this_mbmi = xd.mi[row * xd.mi_stride + col];
                ref buf_2d dst_buf = ref pd.dst;
                Ptr<byte> dst = dst_buf.buf + dst_buf.stride * y + x;
                int @ref = 0;
                RefCntBuffer ref_buf =
                    get_ref_frame_buf(cm, this_mbmi.ref_frame[@ref]);
                scale_factors ref_scale_factors =
                    get_ref_scale_factors_const(cm, this_mbmi.ref_frame[@ref]);
                scale_factors sf = ref_scale_factors;
                buf_2d pre_buf = new buf_2d
                {
                    buf = Ptr<byte>.Null,
                    buf0 = (plane == 1) ? ref_buf.buf.u_buffer : ref_buf.buf.v_buffer,
                    width = ref_buf.buf.uv_crop_width,
                    height = ref_buf.buf.uv_crop_height,
                    stride = ref_buf.buf.uv_stride,
                };

                MV mv = this_mbmi.mv[@ref].as_mv;

                InterPredParams inter_pred_params = default;
                av1_init_inter_params(ref inter_pred_params, b4_w, b4_h, pre_y + y,
                                      pre_x + x, pd.subsampling_x, pd.subsampling_y,
                                      xd.bd, is_cur_buf_hbd(xd), mi.use_intrabc, sf,
                                      in pre_buf, this_mbmi.interp_filters);
                inter_pred_params.conv_params =
                    get_conv_params_no_round(@ref, plane, Ptr<CONV_BUF_TYPE>.Null, 0,
                                             is_compound, xd.bd);

                build_one_inter_predictor(dst, dst_buf.stride, in mv, ref inter_pred_params);

                ++col;
            }
            ++row;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void build_inter_predictors_8x8_and_bigger(
        AV1_COMMON cm, MACROBLOCKD xd, int plane, MB_MODE_INFO mi,
        int build_for_obmc, int bw, int bh, int mi_x, int mi_y)
    {
        int is_compound = has_second_ref(mi);
        int is_intrabc = is_intrabc_block(mi);
        Debug.Assert(IMPLIES(is_intrabc != 0, is_compound == 0));
        macroblockd_plane pd = xd.plane[plane];
        ref buf_2d dst_buf = ref pd.dst;
        Ptr<byte> dst = dst_buf.buf;

        // [port] int is_global[2]: at most two entries; kept in locals.
        int is_global_0 = 0, is_global_1 = 0;
        for (int @ref = 0; @ref < 1 + is_compound; ++@ref)
        {
            ref WarpedMotionParams wm = ref xd.global_motion[mi.ref_frame[@ref]];
            int g = is_global_mv_block(mi, wm.wmtype);
            if (@ref == 0) is_global_0 = g; else is_global_1 = g;
        }

        BLOCK_SIZE bsize = mi.bsize;
        int ss_x = pd.subsampling_x;
        int ss_y = pd.subsampling_y;
        int row_start =
            (block_size_high[bsize] == 4) && ss_y != 0 && build_for_obmc == 0 ? -1 : 0;
        int col_start =
            (block_size_wide[bsize] == 4) && ss_x != 0 && build_for_obmc == 0 ? -1 : 0;
        int pre_x = (mi_x + MI_SIZE * col_start) >> ss_x;
        int pre_y = (mi_y + MI_SIZE * row_start) >> ss_y;

        for (int @ref = 0; @ref < 1 + is_compound; ++@ref)
        {
            scale_factors sf =
                is_intrabc != 0 ? cm.sf_identity : xd.block_ref_scale_factors[@ref];
            ref buf_2d pre_buf = ref (is_intrabc != 0 ? ref pd.dst : ref pd.pre[@ref]);
            MV mv = mi.mv[@ref].as_mv;
            WarpTypesAllowed warp_types = new WarpTypesAllowed
            {
                global_warp_allowed = @ref == 0 ? is_global_0 : is_global_1,
                local_warp_allowed = B(mi.motion_mode == WARPED_CAUSAL),
            };

            InterPredParams inter_pred_params = default;
            av1_init_inter_params(ref inter_pred_params, bw, bh, pre_y, pre_x,
                                  pd.subsampling_x, pd.subsampling_y, xd.bd,
                                  is_cur_buf_hbd(xd), mi.use_intrabc, sf, in pre_buf,
                                  mi.interp_filters);
            if (is_compound != 0)
            {
                // [subset] av1_init_comp_mode: compound prediction is never built in the
                // subset.
                throw new NotSupportedException("build_inter_predictors_8x8_and_bigger: compound");
            }
            inter_pred_params.conv_params = get_conv_params_no_round(
                @ref, plane, xd.tmp_conv_dst, MAX_SB_SIZE, is_compound, xd.bd);

            av1_dist_wtd_comp_weight_assign(
                cm, mi, ref inter_pred_params.conv_params.fwd_offset,
                ref inter_pred_params.conv_params.bck_offset,
                ref inter_pred_params.conv_params.use_dist_wtd_comp_avg, is_compound);

            if (build_for_obmc == 0)
                av1_init_warp_params(ref inter_pred_params, in warp_types, @ref, xd, mi);

            if (is_masked_compound_type(mi.interinter_comp.type) != 0)
            {
                // [subset] Masked compound (wedge / diff-weighted): never selected in the
                // subset (interinter_comp.type is COMPOUND_AVERAGE for single prediction).
                throw new NotSupportedException(
                    "build_inter_predictors_8x8_and_bigger: masked compound");
            }

            build_one_inter_predictor(dst, dst_buf.stride, in mv, ref inter_pred_params);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void build_inter_predictors(AV1_COMMON cm, MACROBLOCKD xd,
                                               int plane, MB_MODE_INFO mi,
                                               int build_for_obmc, int bw, int bh,
                                               int mi_x, int mi_y)
    {
        if (is_sub8x8_inter(xd, plane, mi.bsize, is_intrabc_block(mi),
                            build_for_obmc))
        {
            Debug.Assert(bw < 8 || bh < 8);
            build_inter_predictors_sub8x8(cm, xd, plane, mi, mi_x, mi_y);
        }
        else
        {
            build_inter_predictors_8x8_and_bigger(cm, xd, plane, mi, build_for_obmc, bw,
                                                  bh, mi_x, mi_y);
        }
    }
}
