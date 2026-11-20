// Port of libaom av1/common/reconinter.c
// [subset] Compound (masked / diff-weighted / distance-weighted), warped motion, OBMC and
// inter-intra prediction never execute in the realtime subset; their functions are not ported
// (see the [subset] comments below). The template instantiation from
// reconinter_template.inc lives in reconinter_template.inc.cs.

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class reconinter
{
    // This function will determine whether or not to create a warped
    // prediction.
    // [port] `WarpedMotionParams *final_warp_params` is never NULL at the only call site
    // (av1_init_warp_params), so it is a ref parameter.
    private static int allow_warp(MB_MODE_INFO mbmi,
                                  in WarpTypesAllowed warp_types,
                                  in WarpedMotionParams gm_params,
                                  int build_for_obmc, scale_factors sf,
                                  ref WarpedMotionParams final_warp_params)
    {
        // Note: As per the spec, we must test the fixed point scales here, which are
        // at a higher precision (1 << 14) than the xs and ys in subpel_params (that
        // have 1 << 10 precision).
        if (av1_is_scaled(sf) != 0) return 0;

        final_warp_params = default_warp_params;

        if (build_for_obmc != 0) return 0;

        if (warp_types.local_warp_allowed != 0 && mbmi.wm_params.invalid == 0)
        {
            final_warp_params = mbmi.wm_params;
            return 1;
        }
        else if (warp_types.global_warp_allowed != 0 && gm_params.invalid == 0)
        {
            final_warp_params = gm_params;
            return 1;
        }

        return 0;
    }

    internal static void av1_init_warp_params(ref InterPredParams inter_pred_params,
                                              in WarpTypesAllowed warp_types, int @ref,
                                              MACROBLOCKD xd, MB_MODE_INFO mi)
    {
        if (inter_pred_params.block_height < 8 || inter_pred_params.block_width < 8)
            return;

        if (xd.cur_frame_force_integer_mv != 0) return;

        if (allow_warp(mi, in warp_types, in xd.global_motion[mi.ref_frame[@ref]], 0,
                       inter_pred_params.scale_factors,
                       ref inter_pred_params.warp_params) != 0)
        {
            // CONFIG_REALTIME_ONLY && !CONFIG_AV1_DECODER is false in the oracle build (the
            // decoder is enabled), so no error is raised here.
            inter_pred_params.mode = WARP_PRED;
        }
    }

    internal static void av1_make_inter_predictor(Ptr<byte> src, int src_stride, Ptr<byte> dst,
                                                  int dst_stride,
                                                  ref InterPredParams inter_pred_params,
                                                  in SubpelParams subpel_params)
    {
        Debug.Assert(inter_pred_params.conv_params.is_compound == 0 ||
                     !inter_pred_params.conv_params.dst.IsNull);

        if (inter_pred_params.mode == TRANSLATION_PRED)
        {
            inter_predictor(src, src_stride, dst, dst_stride, in subpel_params,
                            inter_pred_params.block_width,
                            inter_pred_params.block_height,
                            ref inter_pred_params.conv_params,
                            inter_pred_params.interp_filter_params);
        }
        // !CONFIG_REALTIME_ONLY || CONFIG_AV1_DECODER
        else if (inter_pred_params.mode == WARP_PRED)
        {
            // [subset] av1_warp_plane: warped motion is disabled (allow_warp never returns 1 in
            // the subset: no local warp, global motion is identity).
            throw new NotSupportedException("av1_make_inter_predictor: WARP_PRED");
        }
        else
        {
            Debug.Assert(false, "Unsupported inter_pred_params->mode");
        }
    }

    /* clang-format off */
    // DECLARE_ALIGNED(16, static const uint8_t,
    //                 wedge_signflip_lookup[BLOCK_SIZES_ALL][MAX_WEDGE_TYPES])
    private static readonly byte[][] wedge_signflip_lookup = {
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, },
        new byte[] { 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, },
        new byte[] { 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, },
        new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, },
        new byte[] { 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, },
        new byte[] { 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, },
        new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, },
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, },
        new byte[] { 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 1, 0, 1, },
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, },  // not used
    };
    /* clang-format on */

    // [subset] wedge_masks_data.inc (wedge_mask_buf / wedge_masks): the wedge masks are only
    // read by masked compound and wedge inter-intra prediction, which never execute in the
    // subset; av1_wedge_params_lookup[].masks is left null. Only wedge_types (read by
    // av1_is_wedge_used) is needed by the encoder.

    private static wedge_code_type wc(WedgeDirectionType direction, int x_offset, int y_offset) =>
        new wedge_code_type { direction = direction, x_offset = x_offset, y_offset = y_offset };

    private static readonly wedge_code_type[] wedge_codebook_16_hgtw = {
        wc(WEDGE_OBLIQUE27, 4, 4),  wc(WEDGE_OBLIQUE63, 4, 4),
        wc(WEDGE_OBLIQUE117, 4, 4), wc(WEDGE_OBLIQUE153, 4, 4),
        wc(WEDGE_HORIZONTAL, 4, 2), wc(WEDGE_HORIZONTAL, 4, 4),
        wc(WEDGE_HORIZONTAL, 4, 6), wc(WEDGE_VERTICAL, 4, 4),
        wc(WEDGE_OBLIQUE27, 4, 2),  wc(WEDGE_OBLIQUE27, 4, 6),
        wc(WEDGE_OBLIQUE153, 4, 2), wc(WEDGE_OBLIQUE153, 4, 6),
        wc(WEDGE_OBLIQUE63, 2, 4),  wc(WEDGE_OBLIQUE63, 6, 4),
        wc(WEDGE_OBLIQUE117, 2, 4), wc(WEDGE_OBLIQUE117, 6, 4),
    };

    private static readonly wedge_code_type[] wedge_codebook_16_hltw = {
        wc(WEDGE_OBLIQUE27, 4, 4),  wc(WEDGE_OBLIQUE63, 4, 4),
        wc(WEDGE_OBLIQUE117, 4, 4), wc(WEDGE_OBLIQUE153, 4, 4),
        wc(WEDGE_VERTICAL, 2, 4),   wc(WEDGE_VERTICAL, 4, 4),
        wc(WEDGE_VERTICAL, 6, 4),   wc(WEDGE_HORIZONTAL, 4, 4),
        wc(WEDGE_OBLIQUE27, 4, 2),  wc(WEDGE_OBLIQUE27, 4, 6),
        wc(WEDGE_OBLIQUE153, 4, 2), wc(WEDGE_OBLIQUE153, 4, 6),
        wc(WEDGE_OBLIQUE63, 2, 4),  wc(WEDGE_OBLIQUE63, 6, 4),
        wc(WEDGE_OBLIQUE117, 2, 4), wc(WEDGE_OBLIQUE117, 6, 4),
    };

    private static readonly wedge_code_type[] wedge_codebook_16_heqw = {
        wc(WEDGE_OBLIQUE27, 4, 4),  wc(WEDGE_OBLIQUE63, 4, 4),
        wc(WEDGE_OBLIQUE117, 4, 4), wc(WEDGE_OBLIQUE153, 4, 4),
        wc(WEDGE_HORIZONTAL, 4, 2), wc(WEDGE_HORIZONTAL, 4, 6),
        wc(WEDGE_VERTICAL, 2, 4),   wc(WEDGE_VERTICAL, 6, 4),
        wc(WEDGE_OBLIQUE27, 4, 2),  wc(WEDGE_OBLIQUE27, 4, 6),
        wc(WEDGE_OBLIQUE153, 4, 2), wc(WEDGE_OBLIQUE153, 4, 6),
        wc(WEDGE_OBLIQUE63, 2, 4),  wc(WEDGE_OBLIQUE63, 6, 4),
        wc(WEDGE_OBLIQUE117, 2, 4), wc(WEDGE_OBLIQUE117, 6, 4),
    };

    private static wedge_params_type wp(int wedge_types, wedge_code_type[] codebook,
                                        BLOCK_SIZE bsize) =>
        new wedge_params_type
        {
            wedge_types = wedge_types,
            codebook = codebook,
            signflip = wedge_signflip_lookup[bsize],
            masks = null,  // [subset] wedge_masks[bsize]: see above.
        };

    private static readonly wedge_params_type wp_none = default;

    internal static readonly wedge_params_type[] av1_wedge_params_lookup = {
        wp_none,
        wp_none,
        wp_none,
        wp(MAX_WEDGE_TYPES, wedge_codebook_16_heqw, BLOCK_8X8),
        wp(MAX_WEDGE_TYPES, wedge_codebook_16_hgtw, BLOCK_8X16),
        wp(MAX_WEDGE_TYPES, wedge_codebook_16_hltw, BLOCK_16X8),
        wp(MAX_WEDGE_TYPES, wedge_codebook_16_heqw, BLOCK_16X16),
        wp(MAX_WEDGE_TYPES, wedge_codebook_16_hgtw, BLOCK_16X32),
        wp(MAX_WEDGE_TYPES, wedge_codebook_16_hltw, BLOCK_32X16),
        wp(MAX_WEDGE_TYPES, wedge_codebook_16_heqw, BLOCK_32X32),
        wp_none,
        wp_none,
        wp_none,
        wp_none,
        wp_none,
        wp_none,
        wp_none,
        wp_none,
        wp(MAX_WEDGE_TYPES, wedge_codebook_16_hgtw, BLOCK_8X32),
        wp(MAX_WEDGE_TYPES, wedge_codebook_16_hltw, BLOCK_32X8),
        wp_none,
        wp_none,
    };

    // [subset] av1_get_contiguous_soft_mask, av1_get_compound_type_mask, diffwtd_mask_d16,
    // av1_build_compound_diffwtd_mask_d16_c, diffwtd_mask, av1_build_compound_diffwtd_mask_c,
    // build_smooth_interintra_mask: compound / inter-intra masks never execute in the subset.
    // [subset] *_highbd_*: high bitdepth is compiled out.

    // No-op now that the wedge / inter-intra mask buffers are precomputed at
    // codegen time and stored in `.rdata`. The symbol is kept exported because
    // decoder.c, encoder.c, and unit tests still call it.
    internal static void av1_init_wedge_masks() { }

    // [subset] build_masked_compound_no_round, av1_make_masked_inter_predictor: masked compound
    // prediction never executes in the subset.

    internal static void av1_dist_wtd_comp_weight_assign(AV1_COMMON cm,
                                                         MB_MODE_INFO mbmi, ref int fwd_offset,
                                                         ref int bck_offset,
                                                         ref int use_dist_wtd_comp_avg,
                                                         int is_compound)
    {
        if (is_compound == 0 || mbmi.compound_idx != 0)
        {
            fwd_offset = 8;
            bck_offset = 8;
            use_dist_wtd_comp_avg = 0;
            return;
        }

        // [subset] Distance-weighted compound: compound prediction is never built in the subset
        // (and it needs get_relative_dist, i.e. order hints, which are disabled).
        throw new NotSupportedException("av1_dist_wtd_comp_weight_assign: compound");
    }

    internal static void av1_setup_dst_planes(macroblockd_plane[] planes, BLOCK_SIZE bsize,
                                              YV12_BUFFER_CONFIG src, int mi_row, int mi_col,
                                              int plane_start, int plane_end)
    {
        // We use AOMMIN(num_planes, MAX_MB_PLANE) instead of num_planes to quiet
        // the static analysis warnings.
        for (int i = plane_start; i < AOMMIN(plane_end, MAX_MB_PLANE); ++i)
        {
            macroblockd_plane pd = planes[i];
            int is_uv = B(i > 0);
            setup_pred_plane(ref pd.dst, bsize, src.buffers[i], src.crop_widths[is_uv],
                             src.crop_heights[is_uv], src.strides[is_uv], mi_row,
                             mi_col, null, pd.subsampling_x, pd.subsampling_y);
        }
    }

    internal static void av1_setup_pre_planes(MACROBLOCKD xd, int idx,
                                              YV12_BUFFER_CONFIG src, int mi_row, int mi_col,
                                              scale_factors sf,
                                              int num_planes)
    {
        if (src != null)
        {
            // We use AOMMIN(num_planes, MAX_MB_PLANE) instead of num_planes to quiet
            // the static analysis warnings.
            for (int i = 0; i < AOMMIN(num_planes, MAX_MB_PLANE); ++i)
            {
                macroblockd_plane pd = xd.plane[i];
                int is_uv = B(i > 0);
                setup_pred_plane(ref pd.pre[idx], xd.mi[0].bsize, src.buffers[i],
                                 src.crop_widths[is_uv], src.crop_heights[is_uv],
                                 src.strides[is_uv], mi_row, mi_col, sf,
                                 pd.subsampling_x, pd.subsampling_y);
            }
        }
    }

    // [subset] obmc_mask_*, av1_get_obmc_mask, increment_int_ptr,
    // av1_count_overlappable_neighbors, av1_skip_u4x4_pred_in_obmc,
    // modify_neighbor_predictor_for_obmc, build_obmc_inter_pred_above,
    // build_obmc_inter_pred_left, av1_build_obmc_inter_prediction, av1_setup_obmc_dst_bufs,
    // av1_setup_build_prediction_by_above_pred, av1_setup_build_prediction_by_left_pred: OBMC is
    // disabled in the subset.
    // [subset] combine_interintra, av1_build_intra_predictors_for_interintra,
    // av1_combine_interintra, av1_build_interintra_predictor: inter-intra is disabled.
}
