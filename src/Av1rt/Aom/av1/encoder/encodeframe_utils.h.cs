// Port of libaom av1/encoder/encodeframe_utils.h
global using PART4_TYPES = int;
global using AB_PART_TYPE = int;
global using RECT_PART_TYPE = int;
global using SB_MULTI_PASS_MODE = int;
global using static Av1rt.Aom.encodeframe_utils;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class encodeframe_utils
{
    internal const int WRITE_FEATURE_TO_FILE = 0;

    internal const int FEATURE_SIZE_SMS_SPLIT_FAST = 6;
    internal const int FEATURE_SIZE_SMS_SPLIT = 17;
    internal const int FEATURE_SIZE_SMS_PRUNE_PART = 25;
    internal const int FEATURE_SIZE_SMS_TERM_NONE = 28;
    internal const int FEATURE_SIZE_FP_SMS_TERM_NONE = 20;
    internal const int FEATURE_SIZE_MAX_MIN_PART_PRED = 13;
    internal const int MAX_NUM_CLASSES_MAX_MIN_PART_PRED = 4;

    internal const int FEATURE_SMS_NONE_FLAG = 1;
    internal const int FEATURE_SMS_SPLIT_FLAG = (1 << 1);
    internal const int FEATURE_SMS_RECT_FLAG = (1 << 2);

    internal const int FEATURE_SMS_PRUNE_PART_FLAG =
        (FEATURE_SMS_NONE_FLAG | FEATURE_SMS_SPLIT_FLAG | FEATURE_SMS_RECT_FLAG);
    internal const int FEATURE_SMS_SPLIT_MODEL_FLAG =
        (FEATURE_SMS_NONE_FLAG | FEATURE_SMS_SPLIT_FLAG);

    // Number of sub-partitions in rectangular partition types.
    internal const int SUB_PARTITIONS_RECT = 2;

    // Number of sub-partitions in split partition type.
    internal const int SUB_PARTITIONS_SPLIT = 4;

    // Number of sub-partitions in AB partition types.
    internal const int SUB_PARTITIONS_AB = 3;

    // Number of sub-partitions in 4-way partition types.
    internal const int SUB_PARTITIONS_PART4 = 4;

    // enum PART4_TYPES
    internal const int HORZ4 = 0;
    internal const int VERT4 = 1;
    internal const int NUM_PART4_TYPES = 2;
    // enum AB_PART_TYPE
    internal const int HORZ_A = 0;
    internal const int HORZ_B = 1;
    internal const int VERT_A = 2;
    internal const int VERT_B = 3;
    internal const int NUM_AB_PARTS = 4;
    // enum RECT_PART_TYPE
    internal const int HORZ = 0;
    internal const int VERT = 1;
    internal const int NUM_RECT_PARTS = 2;
    // anonymous enum
    internal const int PICK_MODE_RD = 0;
    internal const int PICK_MODE_NONRD = 1;
    // enum SB_MULTI_PASS_MODE
    internal const int SB_SINGLE_PASS = 0; // Single pass encoding: all ctxs get updated normally
    internal const int SB_DRY_PASS = 1; // First pass of multi-pass: does not update the ctxs
    internal const int SB_WET_PASS = 2; // Second pass of multi-pass: finalize and update the ctx

    // [subset] PartitionSearchState helpers (av1_disable_square_split_partition,
    // av1_disable_rect_partitions, av1_disable_all_splits, av1_set_square_split_only,
    // av1_blk_has_rows_and_cols, av1_is_whole_blk_in_frame), update_filter_type_cdf,
    // set_rdmult and do_split_check: only used by the rd partition search.
    // [subset] read_one_frame_stats: !CONFIG_REALTIME_ONLY.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_dealloc_mb_data(MACROBLOCK mb, int num_planes)
    {
        // [subset] aom_free(mb->txfm_search_info.mb_rd_record), aom_free(mb->inter_modes_info)
        // and aom_free(mb->winner_mode_stats): those MACROBLOCK buffers are dropped (never
        // allocated with use_nonrd_pick_mode).

        av1_dealloc_src_diff_buf(mb, num_planes);

        mb.e_mbd.seg_mask = Ptr<byte>.Null;

        mb.dqcoeff_buf = Ptr<int>.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void allocate_winner_mode_stats(AV1_COMP cpi,
                                                    MACROBLOCK mb)
    {
        SPEED_FEATURES sf = cpi.sf;
        // The winner_mode_stats buffer is not required in these cases.
        if (is_stat_generation_stage(cpi) != 0 ||
            (sf.rt_sf.use_nonrd_pick_mode != 0 && sf.rt_sf.hybrid_intra_pickmode == 0) ||
            (sf.winner_mode_sf.multi_winner_mode_type == MULTI_WINNER_MODE_OFF))
            return;

        // [subset] mb->winner_mode_stats allocation: only for the rd mode search.
        throw new NotSupportedException("allocate_winner_mode_stats");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_alloc_mb_data(AV1_COMP cpi,
                                           MACROBLOCK mb)
    {
        AV1_COMMON cm = cpi.common;
        SPEED_FEATURES sf = cpi.sf;
        if (sf.rt_sf.use_nonrd_pick_mode == 0)
        {
            // [subset] mb_rd_record (sf.rd_sf.use_mb_rd_hash) and inter_modes_info are only
            // used by the rd mode search and are dropped from MACROBLOCK.
            throw new NotSupportedException("av1_alloc_mb_data: !use_nonrd_pick_mode");
        }

        av1_alloc_src_diff_buf(cm, mb);

        if (frame_is_intra_only(cm) == 0)
        {
            mb.e_mbd.seg_mask = new byte[2 * MAX_SB_SQUARE];
        }

        allocate_winner_mode_stats(cpi, mb);

        int max_sb_square_y = 1
                              << num_pels_log2_lookup[cm.seq_params.sb_size];
        mb.dqcoeff_buf = new int[max_sb_square_y];
    }

    // This function will compute the number of reference frames to be disabled
    // based on selective_ref_frame speed feature.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint get_num_refs_to_disable(
        AV1_COMP cpi, in int ref_frame_flags,
        Ptr<uint> ref_display_order_hint,
        uint cur_frame_display_index)
    {
        uint num_refs_to_disable = 0;
        if (cpi.sf.inter_sf.selective_ref_frame >= 3)
        {
            num_refs_to_disable++;
            if (cpi.sf.inter_sf.selective_ref_frame >= 6)
            {
                // Disable LAST2_FRAME  and ALTREF2_FRAME
                num_refs_to_disable += 2;
            }
            else if (cpi.sf.inter_sf.selective_ref_frame == 5 &&
                     (ref_frame_flags & av1_ref_frame_flag_list[LAST2_FRAME]) != 0)
            {
                int last2_frame_dist = av1_encoder_get_relative_dist(
                    (int)ref_display_order_hint[LAST2_FRAME - LAST_FRAME],
                    (int)cur_frame_display_index);
                // Disable LAST2_FRAME if it is a temporally distant frame
                if (Math.Abs(last2_frame_dist) > 2)
                {
                    num_refs_to_disable++;
                }
                // [subset] the is_stat_consumption_stage_twopass() branch:
                // !CONFIG_REALTIME_ONLY.
            }
        }
        return num_refs_to_disable;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_max_allowed_ref_frames(
        AV1_COMP cpi, in int ref_frame_flags,
        Ptr<uint> ref_display_order_hint,
        uint cur_frame_display_index)
    {
        uint max_reference_frames =
            cpi.oxcf.ref_frm_cfg.max_reference_frames;
        uint num_refs_to_disable = get_num_refs_to_disable(
            cpi, in ref_frame_flags, ref_display_order_hint, cur_frame_display_index);
        uint max_allowed_refs_for_given_speed =
            INTER_REFS_PER_FRAME - num_refs_to_disable;
        // C: AOMMIN on two unsigned ints, converted to the int return type.
        return (int)Math.Min(max_allowed_refs_for_given_speed, max_reference_frames);
    }

    // Enforce the number of references for each arbitrary frame based on user
    // options and speed.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void enforce_max_ref_frames(
        AV1_COMP cpi, ref int ref_frame_flags,
        Ptr<uint> ref_display_order_hint,
        uint cur_frame_display_index)
    {
        MV_REFERENCE_FRAME ref_frame;
        int total_valid_refs = 0;

        for (ref_frame = LAST_FRAME; ref_frame <= ALTREF_FRAME; ++ref_frame)
        {
            if ((ref_frame_flags & av1_ref_frame_flag_list[ref_frame]) != 0)
            {
                total_valid_refs++;
            }
        }

        int max_allowed_refs = get_max_allowed_ref_frames(
            cpi, in ref_frame_flags, ref_display_order_hint, cur_frame_display_index);

        for (int i = 0; i < 4 && total_valid_refs > max_allowed_refs; ++i)
        {
            MV_REFERENCE_FRAME ref_frame_to_disable = disable_order[i];

            if ((ref_frame_flags & av1_ref_frame_flag_list[ref_frame_to_disable]) == 0)
            {
                continue;
            }

            switch (ref_frame_to_disable)
            {
                case LAST3_FRAME: ref_frame_flags &= ~AOM_LAST3_FLAG; break;
                case LAST2_FRAME: ref_frame_flags &= ~AOM_LAST2_FLAG; break;
                case ALTREF2_FRAME: ref_frame_flags &= ~AOM_ALT2_FLAG; break;
                // C bug kept as is: BWDREF_FRAME clears AOM_GOLD_FLAG.
                case BWDREF_FRAME: ref_frame_flags &= ~AOM_GOLD_FLAG; break;
                default: Debug.Assert(false); break;
            }
            --total_valid_refs;
        }
        Debug.Assert(total_valid_refs <= max_allowed_refs);
    }
}

internal partial struct RD_RECT_PART_WIN_INFO
{
    internal Arr2<int> rect_part_win; // [NUM_RECT_PARTS]
}

// Class: a local of the partition search functions, passed by pointer to
// av1_save_context()/av1_restore_context(); never copied as a whole.
internal sealed partial class RD_SEARCH_MACROBLOCK_CONTEXT
{
    internal readonly ENTROPY_CONTEXT[] a = new ENTROPY_CONTEXT[MAX_MIB_SIZE * MAX_MB_PLANE];
    internal readonly ENTROPY_CONTEXT[] l = new ENTROPY_CONTEXT[MAX_MIB_SIZE * MAX_MB_PLANE];
    internal readonly PARTITION_CONTEXT[] sa = new PARTITION_CONTEXT[MAX_MIB_SIZE];
    internal readonly PARTITION_CONTEXT[] sl = new PARTITION_CONTEXT[MAX_MIB_SIZE];
    internal Ptr<TXFM_CONTEXT> p_ta;
    internal Ptr<TXFM_CONTEXT> p_tl;
    internal readonly TXFM_CONTEXT[] ta = new TXFM_CONTEXT[MAX_MIB_SIZE];
    internal readonly TXFM_CONTEXT[] tl = new TXFM_CONTEXT[MAX_MIB_SIZE];
}

// [subset] SB_FIRST_PASS_STATS (multi-pass SB encoding, sf.rt_sf... / rd only),
// PartitionBlkParams, PartitionTimingStats and PartitionSearchState (rd partition search) are
// not ported.
