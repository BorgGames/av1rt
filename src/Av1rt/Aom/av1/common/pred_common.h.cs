// Port of libaom av1/common/pred_common.h

global using static Av1rt.Aom.pred_common;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class pred_common
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte get_segment_id(
        CommonModeInfoParams mi_params, Ptr<byte> segment_ids,
        BLOCK_SIZE bsize, int mi_row, int mi_col)
    {
        int mi_offset = mi_row * mi_params.mi_cols + mi_col;
        int bw = mi_size_wide[bsize];
        int bh = mi_size_high[bsize];
        int xmis = AOMMIN(mi_params.mi_cols - mi_col, bw);
        int ymis = AOMMIN(mi_params.mi_rows - mi_row, bh);
        int seg_stride = mi_params.mi_cols;
        byte segment_id = MAX_SEGMENTS;

        for (int y = 0; y < ymis; ++y)
        {
            for (int x = 0; x < xmis; ++x)
            {
                segment_id =
                    AOMMIN(segment_id, segment_ids[mi_offset + y * seg_stride + x]);
            }
        }

        Debug.Assert(segment_id < MAX_SEGMENTS);
        return segment_id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte av1_get_spatial_seg_pred(AV1_COMMON cm,
                                                  MACROBLOCKD xd,
                                                  ref int cdf_index,
                                                  int skip_over4x4)
    {
        int step_size = skip_over4x4 != 0 ? 2 : 1;
        byte prev_ul = byte.MaxValue;  // top left segment_id
        byte prev_l = byte.MaxValue;   // left segment_id
        byte prev_u = byte.MaxValue;   // top segment_id
        int mi_row = xd.mi_row;
        int mi_col = xd.mi_col;
        CommonModeInfoParams mi_params = cm.mi_params;
        Ptr<byte> seg_map = cm.cur_frame.seg_map;
        if ((xd.up_available) && (xd.left_available))
        {
            prev_ul = get_segment_id(mi_params, seg_map, BLOCK_4X4, mi_row - step_size,
                                     mi_col - step_size);
        }
        if (xd.up_available)
        {
            prev_u = get_segment_id(mi_params, seg_map, BLOCK_4X4, mi_row - step_size,
                                    mi_col - 0);
        }
        if (xd.left_available)
        {
            prev_l = get_segment_id(mi_params, seg_map, BLOCK_4X4, mi_row - 0,
                                    mi_col - step_size);
        }
        Debug.Assert(IMPLIES(prev_ul != byte.MaxValue,
                             prev_u != byte.MaxValue && prev_l != byte.MaxValue));

        // Pick CDF index based on number of matching/out-of-bounds segment IDs.
        if (prev_ul == byte.MaxValue) /* Edge cases */
            cdf_index = 0;
        else if ((prev_ul == prev_u) && (prev_ul == prev_l))
            cdf_index = 2;
        else if ((prev_ul == prev_u) || (prev_ul == prev_l) || (prev_u == prev_l))
            cdf_index = 1;
        else
            cdf_index = 0;

        // If 2 or more are identical returns that as predictor, otherwise prev_l.
        if (prev_u == byte.MaxValue)  // edge case
            return prev_l == byte.MaxValue ? (byte)0 : prev_l;
        if (prev_l == byte.MaxValue)  // edge case
            return prev_u;
        return (prev_ul == prev_u) ? prev_u : prev_l;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte av1_get_pred_context_seg_id(MACROBLOCKD xd)
    {
        MB_MODE_INFO above_mi = xd.above_mbmi;
        MB_MODE_INFO left_mi = xd.left_mbmi;
        int above_sip = (above_mi != null) ? above_mi.seg_id_predicted : 0;
        int left_sip = (left_mi != null) ? left_mi.seg_id_predicted : 0;

        return (byte)(above_sip + left_sip);
    }

    // [subset] get_comp_index_context, get_comp_group_idx_context,
    // av1_get_skip_mode_context: never executed.

    // f1 (bitstream.c write_inter_segment_id): the temporal segment-id predictor path is
    // data-dependent (seg.temporal_update follows rd_counts.seg_tmp_pred_cost).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<ushort> av1_get_pred_cdf_seg_id(
        segmentation_probs segp, MACROBLOCKD xd)
    {
        return segp.pred_cdf[av1_get_pred_context_seg_id(xd)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_get_skip_txfm_context(MACROBLOCKD xd)
    {
        MB_MODE_INFO above_mi = xd.above_mbmi;
        MB_MODE_INFO left_mi = xd.left_mbmi;
        int above_skip_txfm = above_mi != null ? above_mi.skip_txfm : 0;
        int left_skip_txfm = left_mi != null ? left_mi.skip_txfm : 0;
        return above_skip_txfm + left_skip_txfm;
    }

    // [subset] av1_get_palette_bsize_ctx, av1_get_palette_mode_ctx,
    // av1_get_reference_mode_cdf, av1_get_skip_txfm_cdf, av1_get_comp_reference_type_cdf,
    // av1_get_pred_cdf_uni_comp_ref_p{,1,2}, av1_get_pred_cdf_comp_ref_p{,1,2},
    // av1_get_pred_cdf_comp_bwdref_p{,1}: never executed.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<ushort> av1_get_pred_cdf_single_ref_p1(
        MACROBLOCKD xd)
    {
        return xd.tile_ctx
            .single_ref_cdf[av1_get_pred_context_single_ref_p1(xd)][0];
    }

    // [subset] av1_get_pred_cdf_single_ref_p2: never executed.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<ushort> av1_get_pred_cdf_single_ref_p3(
        MACROBLOCKD xd)
    {
        return xd.tile_ctx
            .single_ref_cdf[av1_get_pred_context_single_ref_p3(xd)][2];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<ushort> av1_get_pred_cdf_single_ref_p4(
        MACROBLOCKD xd)
    {
        return xd.tile_ctx
            .single_ref_cdf[av1_get_pred_context_single_ref_p4(xd)][3];
    }

    // f1 (bitstream.c write_ref_frames): reached when a block uses LAST3_FRAME/GOLDEN_FRAME.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<ushort> av1_get_pred_cdf_single_ref_p5(
        MACROBLOCKD xd)
    {
        return xd.tile_ctx
            .single_ref_cdf[av1_get_pred_context_single_ref_p5(xd)][4];
    }

    // [subset] av1_get_pred_cdf_single_ref_p6: never executed.

    // Returns a context number for the given MB prediction signal
    // The mode info data structure has a one element border above and to the
    // left of the entries corresponding to real blocks.
    // The prediction flags in these dummy entries are initialized to 0.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_tx_size_context(MACROBLOCKD xd)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        MB_MODE_INFO above_mbmi = xd.above_mbmi;
        MB_MODE_INFO left_mbmi = xd.left_mbmi;
        TX_SIZE max_tx_size = max_txsize_rect_lookup[mbmi.bsize];
        int max_tx_wide = tx_size_wide[max_tx_size];
        int max_tx_high = tx_size_high[max_tx_size];
        bool has_above = xd.up_available;
        bool has_left = xd.left_available;

        int above = B(xd.above_txfm_context[0] >= max_tx_wide);
        int left = B(xd.left_txfm_context[0] >= max_tx_high);

        if (has_above)
            if (is_inter_block(above_mbmi) != 0)
                above = B(block_size_wide[above_mbmi.bsize] >= max_tx_wide);

        if (has_left)
            if (is_inter_block(left_mbmi) != 0)
                left = B(block_size_high[left_mbmi.bsize] >= max_tx_high);

        if (has_above && has_left)
            return (above + left);
        else if (has_above)
            return above;
        else if (has_left)
            return left;
        else
            return 0;
    }
}
