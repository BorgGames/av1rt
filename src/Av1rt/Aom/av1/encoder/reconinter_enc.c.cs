// Port of libaom av1/encoder/reconinter_enc.c
// The `#include "av1/common/reconinter_template.inc"` (IS_DEC = 0) instantiation is in
// av1/common/reconinter_template.inc.cs (same partial class).

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class reconinter_enc
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void enc_calc_subpel_params(
        in MV src_mv, ref InterPredParams inter_pred_params,
        ref Ptr<byte> pre, ref SubpelParams subpel_params, ref int src_stride)
    {
        ref buf_2d pre_buf = ref inter_pred_params.ref_frame_buf;
        init_subpel_params(in src_mv, ref inter_pred_params, ref subpel_params, pre_buf.width,
                           pre_buf.height);
        pre = pre_buf.buf0 +
              (subpel_params.pos_y >> SCALE_SUBPEL_BITS) * pre_buf.stride +
              (subpel_params.pos_x >> SCALE_SUBPEL_BITS);
        src_stride = pre_buf.stride;
    }

    // [subset] av1_enc_build_one_inter_predictor, av1_enc_build_inter_predictor_y: never
    // executed (only reached from OBMC / the non-nonrd RD paths).

    private static void enc_build_inter_predictors(AV1_COMMON cm, MACROBLOCKD xd,
                                                   int plane, MB_MODE_INFO mi,
                                                   int bw, int bh, int mi_x, int mi_y)
    {
        build_inter_predictors(cm, xd, plane, mi, /*build_for_obmc=*/0, bw, bh, mi_x,
                               mi_y);
    }

    internal static void av1_enc_build_inter_predictor_y_nonrd(MACROBLOCKD xd,
                                                               ref InterPredParams inter_pred_params,
                                                               in SubpelParams subpel_params)
    {
        macroblockd_plane pd = xd.plane[AOM_PLANE_Y];

        MB_MODE_INFO mbmi = xd.mi[0];
        ref buf_2d dst_buf = ref pd.dst;
        ref buf_2d pre_buf = ref pd.pre[0];
        Ptr<byte> src =
            pre_buf.buf0 +
            (subpel_params.pos_y >> SCALE_SUBPEL_BITS) * pre_buf.stride +
            (subpel_params.pos_x >> SCALE_SUBPEL_BITS);
        Ptr<byte> dst = dst_buf.buf;
        int src_stride = pre_buf.stride;
        int dst_stride = dst_buf.stride;
        inter_pred_params.ref_frame_buf = pre_buf;

        // Initialize interp filter for single reference mode.
        init_interp_filter_params(inter_pred_params.interp_filter_params,
                                  in mbmi.interp_filters.as_filters, pd.width,
                                  pd.height, /*is_intrabc=*/0);

        av1_make_inter_predictor(src, src_stride, dst, dst_stride, ref inter_pred_params,
                                 in subpel_params);
    }

    // PORT-NOTE: `const BUFFER_SET *ctx` may be NULL; it is only read on the inter-intra path,
    // which is [subset], so it is passed as a nullable pointer and otherwise unused.
    internal static void av1_enc_build_inter_predictor(AV1_COMMON cm, MACROBLOCKD xd,
                                                       int mi_row, int mi_col,
                                                       Ptr<BUFFER_SET> ctx, BLOCK_SIZE bsize,
                                                       int plane_from, int plane_to)
    {
        for (int plane = plane_from; plane <= plane_to; ++plane)
        {
            if (plane != 0 && !xd.is_chroma_ref) break;
            int mi_x = mi_col * MI_SIZE;
            int mi_y = mi_row * MI_SIZE;
            enc_build_inter_predictors(cm, xd, plane, xd.mi[0], xd.plane[plane].width,
                                       xd.plane[plane].height, mi_x, mi_y);

            if (is_interintra_pred(xd.mi[0]) != 0)
            {
                // [subset] av1_build_interintra_predictor: inter-intra is disabled in the subset.
                _ = ctx;
                _ = bsize;
                throw new NotSupportedException("av1_enc_build_inter_predictor: interintra");
            }
        }
    }

    // [subset] setup_address_for_obmc, build_obmc_prediction,
    // av1_build_prediction_by_above_preds, av1_build_prediction_by_left_preds,
    // av1_build_obmc_inter_predictors_sb: OBMC is disabled.
    // [subset] av1_build_inter_predictors_for_planes_single_buf, build_masked_compound,
    // build_masked_compound_highbd, build_wedge_inter_predictor_from_buf,
    // av1_build_wedge_inter_predictor_from_buf: compound prediction is never built.
    // [subset] aom_upsampled_pred_scaled, aom_upsampled_pred_c, aom_comp_avg_upsampled_pred_c,
    // aom_comp_mask_upsampled_pred, aom_highbd_*: never executed (the nonrd path does not use
    // upsampled sub-pixel search).
}
