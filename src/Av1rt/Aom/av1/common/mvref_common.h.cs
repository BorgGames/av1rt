// Port of libaom av1/common/mvref_common.h

global using static Av1rt.Aom.mvref_common;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal partial struct POSITION
{
    public int row;
    public int col;

    public POSITION(int row, int col)
    {
        this.row = row;
        this.col = col;
    }
}

internal static partial class mvref_common
{
    // <gen:consts>
    internal const int MVREF_ROW_COLS = 3;
    internal const int REFMVS_LIMIT = ((1 << 12) - 1);
    internal const int MV_BORDER = (16 << 3);
    internal const int INTRABC_DELAY_PIXELS = 256;
    internal const int INTRABC_DELAY_SB64 = (INTRABC_DELAY_PIXELS / 64);

    // MV_REFERENCE_FRAME ref_frame_map[TOTAL_COMP_REFS][2]
    internal static readonly MV_REFERENCE_FRAME[][] ref_frame_map = {
        new MV_REFERENCE_FRAME[] { LAST_FRAME, BWDREF_FRAME },
        new MV_REFERENCE_FRAME[] { LAST2_FRAME, BWDREF_FRAME },
        new MV_REFERENCE_FRAME[] { LAST3_FRAME, BWDREF_FRAME },
        new MV_REFERENCE_FRAME[] { GOLDEN_FRAME, BWDREF_FRAME },
        new MV_REFERENCE_FRAME[] { LAST_FRAME, ALTREF2_FRAME },
        new MV_REFERENCE_FRAME[] { LAST2_FRAME, ALTREF2_FRAME },
        new MV_REFERENCE_FRAME[] { LAST3_FRAME, ALTREF2_FRAME },
        new MV_REFERENCE_FRAME[] { GOLDEN_FRAME, ALTREF2_FRAME },
        new MV_REFERENCE_FRAME[] { LAST_FRAME, ALTREF_FRAME },
        new MV_REFERENCE_FRAME[] { LAST2_FRAME, ALTREF_FRAME },
        new MV_REFERENCE_FRAME[] { LAST3_FRAME, ALTREF_FRAME },
        new MV_REFERENCE_FRAME[] { GOLDEN_FRAME, ALTREF_FRAME },
        new MV_REFERENCE_FRAME[] { LAST_FRAME, LAST2_FRAME },
        new MV_REFERENCE_FRAME[] { LAST_FRAME, LAST3_FRAME },
        new MV_REFERENCE_FRAME[] { LAST_FRAME, GOLDEN_FRAME },
        new MV_REFERENCE_FRAME[] { BWDREF_FRAME, ALTREF_FRAME },
        new MV_REFERENCE_FRAME[] { LAST2_FRAME, LAST3_FRAME },
        new MV_REFERENCE_FRAME[] { LAST2_FRAME, GOLDEN_FRAME },
        new MV_REFERENCE_FRAME[] { LAST3_FRAME, GOLDEN_FRAME },
        new MV_REFERENCE_FRAME[] { BWDREF_FRAME, ALTREF2_FRAME },
        new MV_REFERENCE_FRAME[] { ALTREF2_FRAME, ALTREF_FRAME }
    };

    // ushort compound_mode_ctx_map[3][COMP_NEWMV_CTXS]
    internal static readonly ushort[][] compound_mode_ctx_map = {
        new ushort[] { 0, 1, 1, 1, 1 },
        new ushort[] { 1, 2, 3, 4, 4 },
        new ushort[] { 4, 4, 5, 6, 7 }
    };
    // </gen:consts>

    // [subset] get_relative_dist: never executed (order hints are not used in the realtime
    // subset).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void clamp_mv_ref(ref MV mv, int bw, int bh, MACROBLOCKD xd)
    {
        SubpelMvLimits mv_limits = new SubpelMvLimits
        {
            col_min = xd.mb_to_left_edge - GET_MV_SUBPEL(bw) - MV_BORDER,
            col_max = xd.mb_to_right_edge + GET_MV_SUBPEL(bw) + MV_BORDER,
            row_min = xd.mb_to_top_edge - GET_MV_SUBPEL(bh) - MV_BORDER,
            row_max = xd.mb_to_bottom_edge + GET_MV_SUBPEL(bh) + MV_BORDER
        };
        clamp_mv(ref mv, in mv_limits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int_mv get_block_mv(MB_MODE_INFO candidate, int which_mv)
    {
        return candidate.mv[which_mv];
    }

    // Checks that the given mi_row, mi_col and search point
    // are inside the borders of the tile.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_inside(in TileInfo tile, int mi_col, int mi_row,
                                  in POSITION mi_pos)
    {
        return B(!(mi_row + mi_pos.row < tile.mi_row_start ||
                   mi_col + mi_pos.col < tile.mi_col_start ||
                   mi_row + mi_pos.row >= tile.mi_row_end ||
                   mi_col + mi_pos.col >= tile.mi_col_end));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int find_valid_row_offset(in TileInfo tile, int mi_row,
                                              int row_offset)
    {
        return clamp(row_offset, tile.mi_row_start - mi_row,
                     tile.mi_row_end - mi_row - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int find_valid_col_offset(in TileInfo tile, int mi_col,
                                              int col_offset)
    {
        return clamp(col_offset, tile.mi_col_start - mi_col,
                     tile.mi_col_end - mi_col - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void lower_mv_precision(ref MV mv, int allow_hp, int is_integer)
    {
        if (is_integer != 0)
        {
            integer_mv_precision(ref mv);
        }
        else
        {
            if (allow_hp == 0)
            {
                if ((mv.row & 1) != 0) mv.row += (short)(mv.row > 0 ? -1 : 1);
                if ((mv.col & 1) != 0) mv.col += (short)(mv.col > 0 ? -1 : 1);
            }
        }
    }

    // [port] Never executed on its own: reached only from av1_ref_frame_type's compound
    // branch (compound prediction is disabled in the realtime subset); ported so that
    // function stays complete.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static sbyte get_uni_comp_ref_idx(Ptr<MV_REFERENCE_FRAME> rf)
    {
        // Single ref pred
        if (rf[1] <= INTRA_FRAME) return -1;

        // Bi-directional comp ref pred
        if ((rf[0] < BWDREF_FRAME) && (rf[1] >= BWDREF_FRAME)) return -1;

        for (sbyte ref_idx = 0; ref_idx < TOTAL_UNIDIR_COMP_REFS; ++ref_idx)
        {
            if (rf[0] == comp_ref0(ref_idx) && rf[1] == comp_ref1(ref_idx))
                return ref_idx;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static sbyte av1_ref_frame_type(Ptr<MV_REFERENCE_FRAME> rf)
    {
        if (rf[1] > INTRA_FRAME)
        {
            sbyte uni_comp_ref_idx = get_uni_comp_ref_idx(rf);
            if (uni_comp_ref_idx >= 0)
            {
                Debug.Assert((REF_FRAMES + FWD_REFS * BWD_REFS + uni_comp_ref_idx) <
                             MODE_CTX_REF_FRAMES);
                return (sbyte)(REF_FRAMES + FWD_REFS * BWD_REFS + uni_comp_ref_idx);
            }
            else
            {
                return (sbyte)(REF_FRAMES + FWD_RF_OFFSET(rf[0]) +
                               BWD_RF_OFFSET(rf[1]) * FWD_REFS);
            }
        }

        return (sbyte)rf[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_set_ref_frame(Ptr<MV_REFERENCE_FRAME> rf,
                                           MV_REFERENCE_FRAME ref_frame_type)
    {
        if (ref_frame_type >= REF_FRAMES)
        {
            rf[0] = ref_frame_map[ref_frame_type - REF_FRAMES][0];
            rf[1] = ref_frame_map[ref_frame_type - REF_FRAMES][1];
        }
        else
        {
            Debug.Assert(ref_frame_type > NONE_FRAME);
            rf[0] = ref_frame_type;
            rf[1] = NONE_FRAME;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short av1_mode_context_analyzer(
        Ptr<short> mode_context, Ptr<MV_REFERENCE_FRAME> rf)
    {
        sbyte ref_frame = av1_ref_frame_type(rf);

        if (rf[1] <= INTRA_FRAME) return mode_context[ref_frame];

        short newmv_ctx = (short)(mode_context[ref_frame] & NEWMV_CTX_MASK);
        short refmv_ctx =
            (short)((mode_context[ref_frame] >> REFMV_OFFSET) & REFMV_CTX_MASK);

        short comp_ctx = (short)compound_mode_ctx_map[refmv_ctx >> 1][AOMMIN(
            (int)newmv_ctx, COMP_NEWMV_CTXS - 1)];
        return comp_ctx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte av1_drl_ctx(Ptr<ushort> ref_mv_weight, int ref_idx)
    {
        if (ref_mv_weight[ref_idx] >= REF_CAT_LEVEL &&
            ref_mv_weight[ref_idx + 1] >= REF_CAT_LEVEL)
            return 0;

        if (ref_mv_weight[ref_idx] >= REF_CAT_LEVEL &&
            ref_mv_weight[ref_idx + 1] < REF_CAT_LEVEL)
            return 1;

        if (ref_mv_weight[ref_idx] < REF_CAT_LEVEL &&
            ref_mv_weight[ref_idx + 1] < REF_CAT_LEVEL)
            return 2;

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_collect_neighbors_ref_counts(MACROBLOCKD xd)
    {
        av1_zero(xd.neighbors_ref_counts);

        byte[] ref_counts = xd.neighbors_ref_counts;

        MB_MODE_INFO above_mbmi = xd.above_mbmi;
        MB_MODE_INFO left_mbmi = xd.left_mbmi;
        bool above_in_image = xd.up_available;
        bool left_in_image = xd.left_available;

        // Above neighbor
        if (above_in_image && is_inter_block(above_mbmi) != 0)
        {
            ref_counts[above_mbmi.ref_frame[0]]++;
            if (has_second_ref(above_mbmi) != 0)
            {
                ref_counts[above_mbmi.ref_frame[1]]++;
            }
        }

        // Left neighbor
        if (left_in_image && is_inter_block(left_mbmi) != 0)
        {
            ref_counts[left_mbmi.ref_frame[0]]++;
            if (has_second_ref(left_mbmi) != 0)
            {
                ref_counts[left_mbmi.ref_frame[1]]++;
            }
        }
    }

    // [subset] av1_find_ref_dv, av1_is_dv_valid: never executed (intra block copy is not
    // used in the realtime subset).
}
