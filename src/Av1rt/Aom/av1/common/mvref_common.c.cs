// Port of libaom av1/common/mvref_common.c

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class mvref_common
{
    // [subset] div_mult, av1_get_mv_projection: only used by the temporal MV projection
    // (allow_ref_frame_mvs), which is off in the realtime subset.

    // [subset] av1_copy_frame_mvs: only called when
    // seq_params->order_hint_info.enable_ref_frame_mvs is set (off in the subset).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void add_ref_mv_candidate(
        MB_MODE_INFO candidate, Ptr<MV_REFERENCE_FRAME> rf,
        ref byte refmv_count, ref byte ref_match_count, ref byte newmv_count,
        Ptr<CANDIDATE_MV> ref_mv_stack, Ptr<ushort> ref_mv_weight,
        Ptr<int_mv> gm_mv_candidates, Ptr<WarpedMotionParams> gm_params,
        ushort weight)
    {
        if (is_inter_block(candidate) == 0) return;
        Debug.Assert(weight % 2 == 0);
        int index, @ref;

        if (rf[1] == NONE_FRAME)
        {
            // single reference frame
            for (@ref = 0; @ref < 2; ++@ref)
            {
                if (candidate.ref_frame[@ref] == rf[0])
                {
                    int is_gm_block =
                        is_global_mv_block(candidate, gm_params[rf[0]].wmtype);
                    int_mv this_refmv =
                        is_gm_block != 0 ? gm_mv_candidates[0] : get_block_mv(candidate, @ref);
                    for (index = 0; index < refmv_count; ++index)
                    {
                        if (ref_mv_stack[index].this_mv.as_int == this_refmv.as_int)
                        {
                            ref_mv_weight[index] += weight;
                            break;
                        }
                    }

                    // Add a new item to the list.
                    if (index == refmv_count && refmv_count < MAX_REF_MV_STACK_SIZE)
                    {
                        ref_mv_stack[index].this_mv = this_refmv;
                        ref_mv_weight[index] = weight;
                        ++refmv_count;
                    }
                    if (have_newmv_in_inter_mode(candidate.mode) != 0) ++newmv_count;
                    ++ref_match_count;
                }
            }
        }
        else
        {
            // compound reference frame
            // [subset] Compound reference MV candidates: av1_find_mv_refs is only called for
            // single reference frames in the realtime subset (compound modes are pruned before
            // their MV search; see setup_compound_params_from_comp_idx in nonrd_pickmode.c).
            throw new NotSupportedException("add_ref_mv_candidate: compound reference frames");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void scan_row_mbmi(AV1_COMMON cm, MACROBLOCKD xd,
                                      int mi_col, Ptr<MV_REFERENCE_FRAME> rf,
                                      int row_offset, Ptr<CANDIDATE_MV> ref_mv_stack,
                                      Ptr<ushort> ref_mv_weight, ref byte refmv_count,
                                      ref byte ref_match_count, ref byte newmv_count,
                                      Ptr<int_mv> gm_mv_candidates, int max_row_offset,
                                      ref int processed_rows)
    {
        int end_mi = AOMMIN(xd.width, cm.mi_params.mi_cols - mi_col);
        end_mi = AOMMIN(end_mi, mi_size_wide[BLOCK_64X64]);
        int width_8x8 = mi_size_wide[BLOCK_8X8];
        int width_16x16 = mi_size_wide[BLOCK_16X16];
        int col_offset = 0;
        // TODO(jingning): Revisit this part after cb4x4 is stable.
        if (Math.Abs(row_offset) > 1)
        {
            col_offset = 1;
            if ((mi_col & 0x01) != 0 && xd.width < width_8x8) --col_offset;
        }
        int use_step_16 = B(xd.width >= 16);
        Ptr<MB_MODE_INFO> candidate_mi0 = xd.mi + row_offset * xd.mi_stride;

        for (int i = 0; i < end_mi;)
        {
            MB_MODE_INFO candidate = candidate_mi0[col_offset + i];
            int candidate_bsize = candidate.bsize;
            int n4_w = mi_size_wide[candidate_bsize];
            int len = AOMMIN(xd.width, n4_w);
            if (use_step_16 != 0)
                len = AOMMAX(width_16x16, len);
            else if (Math.Abs(row_offset) > 1)
                len = AOMMAX(len, width_8x8);

            ushort weight = 2;
            if (xd.width >= width_8x8 && xd.width <= n4_w)
            {
                ushort inc = (ushort)AOMMIN(-max_row_offset + row_offset + 1,
                                            mi_size_high[candidate_bsize]);
                // Obtain range used in weight calculation.
                weight = AOMMAX(weight, inc);
                // Update processed rows.
                processed_rows = inc - row_offset - 1;
            }

            add_ref_mv_candidate(candidate, rf, ref refmv_count, ref ref_match_count,
                                 ref newmv_count, ref_mv_stack, ref_mv_weight,
                                 gm_mv_candidates, cm.global_motion, (ushort)(len * weight));

            i += len;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void scan_col_mbmi(AV1_COMMON cm, MACROBLOCKD xd,
                                      int mi_row, Ptr<MV_REFERENCE_FRAME> rf,
                                      int col_offset, Ptr<CANDIDATE_MV> ref_mv_stack,
                                      Ptr<ushort> ref_mv_weight, ref byte refmv_count,
                                      ref byte ref_match_count, ref byte newmv_count,
                                      Ptr<int_mv> gm_mv_candidates, int max_col_offset,
                                      ref int processed_cols)
    {
        int end_mi = AOMMIN(xd.height, cm.mi_params.mi_rows - mi_row);
        end_mi = AOMMIN(end_mi, mi_size_high[BLOCK_64X64]);
        int n8_h_8 = mi_size_high[BLOCK_8X8];
        int n8_h_16 = mi_size_high[BLOCK_16X16];
        int i;
        int row_offset = 0;
        if (Math.Abs(col_offset) > 1)
        {
            row_offset = 1;
            if ((mi_row & 0x01) != 0 && xd.height < n8_h_8) --row_offset;
        }
        int use_step_16 = B(xd.height >= 16);

        for (i = 0; i < end_mi;)
        {
            MB_MODE_INFO candidate =
                xd.mi[(row_offset + i) * xd.mi_stride + col_offset];
            int candidate_bsize = candidate.bsize;
            int n4_h = mi_size_high[candidate_bsize];
            int len = AOMMIN(xd.height, n4_h);
            if (use_step_16 != 0)
                len = AOMMAX(n8_h_16, len);
            else if (Math.Abs(col_offset) > 1)
                len = AOMMAX(len, n8_h_8);

            int weight = 2;
            if (xd.height >= n8_h_8 && xd.height <= n4_h)
            {
                int inc = AOMMIN(-max_col_offset + col_offset + 1,
                                 mi_size_wide[candidate_bsize]);
                // Obtain range used in weight calculation.
                weight = AOMMAX(weight, inc);
                // Update processed cols.
                processed_cols = inc - col_offset - 1;
            }

            add_ref_mv_candidate(candidate, rf, ref refmv_count, ref ref_match_count,
                                 ref newmv_count, ref_mv_stack, ref_mv_weight,
                                 gm_mv_candidates, cm.global_motion, (ushort)(len * weight));

            i += len;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void scan_blk_mbmi(AV1_COMMON cm, MACROBLOCKD xd,
                                      int mi_row, int mi_col,
                                      Ptr<MV_REFERENCE_FRAME> rf, int row_offset,
                                      int col_offset, Ptr<CANDIDATE_MV> ref_mv_stack,
                                      Ptr<ushort> ref_mv_weight,
                                      ref byte ref_match_count, ref byte newmv_count,
                                      Ptr<int_mv> gm_mv_candidates,
                                      ref byte refmv_count)
    {
        ref TileInfo tile = ref xd.tile;
        POSITION mi_pos;

        mi_pos.row = row_offset;
        mi_pos.col = col_offset;

        if (is_inside(in tile, mi_col, mi_row, in mi_pos) != 0)
        {
            MB_MODE_INFO candidate =
                xd.mi[mi_pos.row * xd.mi_stride + mi_pos.col];
            int len = mi_size_wide[BLOCK_8X8];

            add_ref_mv_candidate(candidate, rf, ref refmv_count, ref ref_match_count,
                                 ref newmv_count, ref_mv_stack, ref_mv_weight,
                                 gm_mv_candidates, cm.global_motion, (ushort)(2 * len));
        }  // Analyze a single 8x8 block motion information.
    }

    private static int has_top_right(AV1_COMMON cm, MACROBLOCKD xd,
                                     int mi_row, int mi_col, int bs)
    {
        int sb_mi_size = mi_size_wide[cm.seq_params.sb_size];
        int mask_row = mi_row & (sb_mi_size - 1);
        int mask_col = mi_col & (sb_mi_size - 1);

        if (bs > mi_size_wide[BLOCK_64X64]) return 0;

        // In a split partition all apart from the bottom right has a top right
        int has_tr = B(!((mask_row & bs) != 0 && (mask_col & bs) != 0));

        // bs > 0 and bs is a power of 2
        Debug.Assert(bs > 0 && (bs & (bs - 1)) == 0);

        // For each 4x4 group of blocks, when the bottom right is decoded the blocks
        // to the right have not been decoded therefore the bottom right does
        // not have a top right
        while (bs < sb_mi_size)
        {
            if ((mask_col & bs) != 0)
            {
                if ((mask_col & (2 * bs)) != 0 && (mask_row & (2 * bs)) != 0)
                {
                    has_tr = 0;
                    break;
                }
            }
            else
            {
                break;
            }
            bs <<= 1;
        }

        // In a VERTICAL or VERTICAL_4 partition, all partition before the last one
        // always have a top right (as the block above will have been decoded).
        if (xd.width < xd.height)
        {
            if (!xd.is_last_vertical_rect) has_tr = 1;
        }

        // In a HORIZONTAL or HORIZONTAL_4 partition, partitions after the first one
        // never have a top right (as the block to the right won't have been decoded).
        if (xd.width > xd.height)
        {
            if (!xd.is_first_horizontal_rect) has_tr = 0;
        }

        // The bottom left square of a Vertical A (in the old format) does
        // not have a top right as it is decoded before the right hand
        // rectangle of the partition
        if (xd.mi[0].partition == PARTITION_VERT_A)
        {
            if (xd.width == xd.height)
                if ((mask_row & bs) != 0) has_tr = 0;
        }

        return has_tr;
    }

    // [subset] check_sb_border, add_tpl_ref_mv: only used when
    // cm->features.allow_ref_frame_mvs is set (off in the realtime subset).
    // [subset] process_compound_ref_mv_candidate: compound reference frames only (see
    // add_ref_mv_candidate).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void process_single_ref_mv_candidate(
        MB_MODE_INFO candidate, AV1_COMMON cm,
        MV_REFERENCE_FRAME ref_frame, ref byte refmv_count,
        Ptr<CANDIDATE_MV> ref_mv_stack,
        Ptr<ushort> ref_mv_weight)
    {
        for (int rf_idx = 0; rf_idx < 2; ++rf_idx)
        {
            if (candidate.ref_frame[rf_idx] > INTRA_FRAME)
            {
                int_mv this_mv = candidate.mv[rf_idx];
                if (cm.ref_frame_sign_bias[candidate.ref_frame[rf_idx]] !=
                    cm.ref_frame_sign_bias[ref_frame])
                {
                    this_mv.as_mv.row = (short)-this_mv.as_mv.row;
                    this_mv.as_mv.col = (short)-this_mv.as_mv.col;
                }
                int stack_idx;
                for (stack_idx = 0; stack_idx < refmv_count; ++stack_idx)
                {
                    int_mv stack_mv = ref_mv_stack[stack_idx].this_mv;
                    if (this_mv.as_int == stack_mv.as_int) break;
                }

                if (stack_idx == refmv_count)
                {
                    ref_mv_stack[stack_idx].this_mv = this_mv;

                    // TODO(jingning): Set an arbitrary small number here. The weight
                    // doesn't matter as long as it is properly initialized.
                    ref_mv_weight[stack_idx] = 2;
                    ++refmv_count;
                }
            }
        }
    }

    // [port] `MV_REFERENCE_FRAME rf[2]` locals of setup_ref_mv_list / av1_find_mv_refs and the
    // `int_mv gm_mv[2]` local of av1_find_mv_refs: per-thread scratch (both functions are leaves
    // and are not re-entered while the buffers are live).
    [ThreadStatic] private static MV_REFERENCE_FRAME[] s_setup_rf;
    [ThreadStatic] private static MV_REFERENCE_FRAME[] s_find_rf;
    [ThreadStatic] private static int_mv[] s_gm_mv;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void setup_ref_mv_list(
        AV1_COMMON cm, MACROBLOCKD xd, MV_REFERENCE_FRAME ref_frame,
        ref byte refmv_count,
        Ptr<CANDIDATE_MV> ref_mv_stack,
        Ptr<ushort> ref_mv_weight,
        Ptr<int_mv> mv_ref_list, Ptr<int_mv> gm_mv_candidates,
        int mi_row, int mi_col, Ptr<short> mode_context)
    {
        int bs = AOMMAX(xd.width, xd.height);
        int has_tr = has_top_right(cm, xd, mi_row, mi_col, bs);
        Ptr<MV_REFERENCE_FRAME> rf = s_setup_rf ??= new MV_REFERENCE_FRAME[2];

        ref TileInfo tile = ref xd.tile;
        int max_row_offset = 0, max_col_offset = 0;
        int row_adj = B((xd.height < mi_size_high[BLOCK_8X8]) && (mi_row & 0x01) != 0);
        int col_adj = B((xd.width < mi_size_wide[BLOCK_8X8]) && (mi_col & 0x01) != 0);
        int processed_rows = 0;
        int processed_cols = 0;

        av1_set_ref_frame(rf, ref_frame);
        mode_context[ref_frame] = 0;
        refmv_count = 0;

        // Find valid maximum row/col offset.
        if (xd.up_available)
        {
            max_row_offset = -(MVREF_ROW_COLS << 1) + row_adj;

            if (xd.height < mi_size_high[BLOCK_8X8])
                max_row_offset = -(2 << 1) + row_adj;

            max_row_offset = find_valid_row_offset(in tile, mi_row, max_row_offset);
        }

        if (xd.left_available)
        {
            max_col_offset = -(MVREF_ROW_COLS << 1) + col_adj;

            if (xd.width < mi_size_wide[BLOCK_8X8])
                max_col_offset = -(2 << 1) + col_adj;

            max_col_offset = find_valid_col_offset(in tile, mi_col, max_col_offset);
        }

        byte col_match_count = 0;
        byte row_match_count = 0;
        byte newmv_count = 0;

        // Scan the first above row mode info. row_offset = -1;
        if (Math.Abs(max_row_offset) >= 1)
            scan_row_mbmi(cm, xd, mi_col, rf, -1, ref_mv_stack, ref_mv_weight,
                          ref refmv_count, ref row_match_count, ref newmv_count, gm_mv_candidates,
                          max_row_offset, ref processed_rows);
        // Scan the first left column mode info. col_offset = -1;
        if (Math.Abs(max_col_offset) >= 1)
            scan_col_mbmi(cm, xd, mi_row, rf, -1, ref_mv_stack, ref_mv_weight,
                          ref refmv_count, ref col_match_count, ref newmv_count, gm_mv_candidates,
                          max_col_offset, ref processed_cols);
        // Check top-right boundary
        if (has_tr != 0)
            scan_blk_mbmi(cm, xd, mi_row, mi_col, rf, -1, xd.width, ref_mv_stack,
                          ref_mv_weight, ref row_match_count, ref newmv_count,
                          gm_mv_candidates, ref refmv_count);

        byte nearest_match = (byte)(B(row_match_count > 0) + B(col_match_count > 0));
        byte nearest_refmv_count = refmv_count;

        // TODO(yunqing): for comp_search, do it for all 3 cases.
        for (int idx = 0; idx < nearest_refmv_count; ++idx)
            ref_mv_weight[idx] += REF_CAT_LEVEL;

        if (cm.features.allow_ref_frame_mvs)
        {
            // [subset] Temporal MV candidates (add_tpl_ref_mv): allow_ref_frame_mvs is off in
            // the realtime subset.
            throw new NotSupportedException("setup_ref_mv_list: allow_ref_frame_mvs");
        }

        byte dummy_newmv_count = 0;

        // Scan the second outer area.
        scan_blk_mbmi(cm, xd, mi_row, mi_col, rf, -1, -1, ref_mv_stack, ref_mv_weight,
                      ref row_match_count, ref dummy_newmv_count, gm_mv_candidates,
                      ref refmv_count);

        for (int idx = 2; idx <= MVREF_ROW_COLS; ++idx)
        {
            int row_offset = -(idx << 1) + 1 + row_adj;
            int col_offset = -(idx << 1) + 1 + col_adj;

            if (Math.Abs(row_offset) <= Math.Abs(max_row_offset) &&
                Math.Abs(row_offset) > processed_rows)
                scan_row_mbmi(cm, xd, mi_col, rf, row_offset, ref_mv_stack, ref_mv_weight,
                              ref refmv_count, ref row_match_count, ref dummy_newmv_count,
                              gm_mv_candidates, max_row_offset, ref processed_rows);

            if (Math.Abs(col_offset) <= Math.Abs(max_col_offset) &&
                Math.Abs(col_offset) > processed_cols)
                scan_col_mbmi(cm, xd, mi_row, rf, col_offset, ref_mv_stack, ref_mv_weight,
                              ref refmv_count, ref col_match_count, ref dummy_newmv_count,
                              gm_mv_candidates, max_col_offset, ref processed_cols);
        }

        byte ref_match_count = (byte)(B(row_match_count > 0) + B(col_match_count > 0));

        switch (nearest_match)
        {
            case 0:
                if (ref_match_count >= 1) mode_context[ref_frame] |= 1;
                if (ref_match_count == 1)
                    mode_context[ref_frame] |= (1 << REFMV_OFFSET);
                else if (ref_match_count >= 2)
                    mode_context[ref_frame] |= (2 << REFMV_OFFSET);
                break;
            case 1:
                mode_context[ref_frame] |= (short)((newmv_count > 0) ? 2 : 3);
                if (ref_match_count == 1)
                    mode_context[ref_frame] |= (3 << REFMV_OFFSET);
                else if (ref_match_count >= 2)
                    mode_context[ref_frame] |= (4 << REFMV_OFFSET);
                break;
            case 2:
            default:
                if (newmv_count >= 1)
                    mode_context[ref_frame] |= 4;
                else
                    mode_context[ref_frame] |= 5;

                mode_context[ref_frame] |= (5 << REFMV_OFFSET);
                break;
        }

        // Rank the likelihood and assign nearest and near mvs.
        int len = nearest_refmv_count;
        while (len > 0)
        {
            int nr_len = 0;
            for (int idx = 1; idx < len; ++idx)
            {
                if (ref_mv_weight[idx - 1] < ref_mv_weight[idx])
                {
                    CANDIDATE_MV tmp_mv = ref_mv_stack[idx - 1];
                    ushort tmp_ref_mv_weight = ref_mv_weight[idx - 1];
                    ref_mv_stack[idx - 1] = ref_mv_stack[idx];
                    ref_mv_stack[idx] = tmp_mv;
                    ref_mv_weight[idx - 1] = ref_mv_weight[idx];
                    ref_mv_weight[idx] = tmp_ref_mv_weight;
                    nr_len = idx;
                }
            }
            len = nr_len;
        }

        len = refmv_count;
        while (len > nearest_refmv_count)
        {
            int nr_len = nearest_refmv_count;
            for (int idx = nearest_refmv_count + 1; idx < len; ++idx)
            {
                if (ref_mv_weight[idx - 1] < ref_mv_weight[idx])
                {
                    CANDIDATE_MV tmp_mv = ref_mv_stack[idx - 1];
                    ushort tmp_ref_mv_weight = ref_mv_weight[idx - 1];
                    ref_mv_stack[idx - 1] = ref_mv_stack[idx];
                    ref_mv_stack[idx] = tmp_mv;
                    ref_mv_weight[idx - 1] = ref_mv_weight[idx];
                    ref_mv_weight[idx] = tmp_ref_mv_weight;
                    nr_len = idx;
                }
            }
            len = nr_len;
        }

        int mi_width = AOMMIN(mi_size_wide[BLOCK_64X64], xd.width);
        mi_width = AOMMIN(mi_width, cm.mi_params.mi_cols - mi_col);
        int mi_height = AOMMIN(mi_size_high[BLOCK_64X64], xd.height);
        mi_height = AOMMIN(mi_height, cm.mi_params.mi_rows - mi_row);
        int mi_size = AOMMIN(mi_width, mi_height);
        if (rf[1] > NONE_FRAME)
        {
            // [subset] Compound reference MV list (process_compound_ref_mv_candidate and the
            // comp_list fill-up): never reached, see add_ref_mv_candidate.
            throw new NotSupportedException("setup_ref_mv_list: compound reference frames");
        }
        else
        {
            // Handle single reference frame extension
            for (int idx = 0; Math.Abs(max_row_offset) >= 1 && idx < mi_size &&
                              refmv_count < MAX_MV_REF_CANDIDATES;)
            {
                MB_MODE_INFO candidate = xd.mi[-xd.mi_stride + idx];
                process_single_ref_mv_candidate(candidate, cm, ref_frame, ref refmv_count,
                                                ref_mv_stack, ref_mv_weight);
                idx += mi_size_wide[candidate.bsize];
            }

            for (int idx = 0; Math.Abs(max_col_offset) >= 1 && idx < mi_size &&
                              refmv_count < MAX_MV_REF_CANDIDATES;)
            {
                MB_MODE_INFO candidate = xd.mi[idx * xd.mi_stride - 1];
                process_single_ref_mv_candidate(candidate, cm, ref_frame, ref refmv_count,
                                                ref_mv_stack, ref_mv_weight);
                idx += mi_size_high[candidate.bsize];
            }

            for (int idx = 0; idx < refmv_count; ++idx)
            {
                clamp_mv_ref(ref ref_mv_stack[idx].this_mv.as_mv, xd.width << MI_SIZE_LOG2,
                             xd.height << MI_SIZE_LOG2, xd);
            }

            if (!mv_ref_list.IsNull)
            {
                for (int idx = refmv_count; idx < MAX_MV_REF_CANDIDATES; ++idx)
                    mv_ref_list[idx].as_int = gm_mv_candidates[0].as_int;

                for (int idx = 0; idx < AOMMIN(MAX_MV_REF_CANDIDATES, (int)refmv_count);
                     ++idx)
                {
                    mv_ref_list[idx].as_int = ref_mv_stack[idx].this_mv.as_int;
                }
            }
        }
    }

    // [port] C: uint8_t ref_mv_count[MODE_CTX_REF_FRAMES],
    //    CANDIDATE_MV ref_mv_stack[][MAX_REF_MV_STACK_SIZE],
    //    uint16_t ref_mv_weight[][MAX_REF_MV_STACK_SIZE],
    //    int_mv mv_ref_list[][MAX_MV_REF_CANDIDATES] (may be NULL), int_mv *global_mvs (may be
    //    NULL), int16_t *mode_context. The 2-D arrays are jagged arrays indexed [ref_frame].
    internal static void av1_find_mv_refs(AV1_COMMON cm, MACROBLOCKD xd,
                                          MB_MODE_INFO mi, MV_REFERENCE_FRAME ref_frame,
                                          Ptr<byte> ref_mv_count,
                                          CANDIDATE_MV[][] ref_mv_stack,
                                          ushort[][] ref_mv_weight,
                                          int_mv[][] mv_ref_list, Ptr<int_mv> global_mvs,
                                          Ptr<short> mode_context)
    {
        int mi_row = xd.mi_row;
        int mi_col = xd.mi_col;
        Ptr<int_mv> gm_mv = s_gm_mv ??= new int_mv[2];

        if (ref_frame == INTRA_FRAME)
        {
            gm_mv[0].as_int = gm_mv[1].as_int = 0;
            if (!global_mvs.IsNull)
            {
                global_mvs[ref_frame].as_int = INVALID_MV;
            }
        }
        else
        {
            BLOCK_SIZE bsize = mi.bsize;
            int allow_high_precision_mv = B(cm.features.allow_high_precision_mv);
            int force_integer_mv = B(cm.features.cur_frame_force_integer_mv);
            if (ref_frame < REF_FRAMES)
            {
                gm_mv[0] = gm_get_motion_vector(in cm.global_motion[ref_frame],
                                                allow_high_precision_mv, bsize, mi_col,
                                                mi_row, force_integer_mv);
                gm_mv[1].as_int = 0;
                if (!global_mvs.IsNull) global_mvs[ref_frame] = gm_mv[0];
            }
            else
            {
                Ptr<MV_REFERENCE_FRAME> rf = s_find_rf ??= new MV_REFERENCE_FRAME[2];
                av1_set_ref_frame(rf, ref_frame);
                gm_mv[0] = gm_get_motion_vector(in cm.global_motion[rf[0]],
                                                allow_high_precision_mv, bsize, mi_col,
                                                mi_row, force_integer_mv);
                gm_mv[1] = gm_get_motion_vector(in cm.global_motion[rf[1]],
                                                allow_high_precision_mv, bsize, mi_col,
                                                mi_row, force_integer_mv);
            }
        }

        setup_ref_mv_list(cm, xd, ref_frame, ref ref_mv_count[ref_frame],
                          ref_mv_stack[ref_frame], ref_mv_weight[ref_frame],
                          mv_ref_list != null ? mv_ref_list[ref_frame] : Ptr<int_mv>.Null, gm_mv,
                          mi_row, mi_col, mode_context);
    }

    // [subset] av1_find_best_ref_mvs: never executed (the encoder uses
    // av1_find_best_ref_mvs_from_stack).

    internal static void av1_setup_frame_buf_refs(AV1_COMMON cm)
    {
        cm.cur_frame.order_hint = cm.current_frame.order_hint;
        cm.cur_frame.display_order_hint = cm.current_frame.display_order_hint;
        cm.cur_frame.pyramid_level = cm.current_frame.pyramid_level;
        cm.cur_frame.base_qindex = cm.quant_params.base_qindex;
        cm.cur_frame.filter_level[0] = -1;
        cm.cur_frame.filter_level[1] = -1;
        MV_REFERENCE_FRAME ref_frame;
        for (ref_frame = LAST_FRAME; ref_frame <= ALTREF_FRAME; ++ref_frame)
        {
            RefCntBuffer buf = get_ref_frame_buf(cm, ref_frame);
            if (buf != null)
            {
                cm.cur_frame.ref_order_hints[ref_frame - LAST_FRAME] = buf.order_hint;
                cm.cur_frame.ref_display_order_hint[ref_frame - LAST_FRAME] =
                    buf.display_order_hint;
            }
        }
    }

    internal static void av1_setup_frame_sign_bias(AV1_COMMON cm)
    {
        MV_REFERENCE_FRAME ref_frame;
        for (ref_frame = LAST_FRAME; ref_frame <= ALTREF_FRAME; ++ref_frame)
        {
            RefCntBuffer buf = get_ref_frame_buf(cm, ref_frame);
            if (cm.seq_params.order_hint_info.enable_order_hint != 0 && buf != null)
            {
                // [subset] Order hints are disabled in the realtime subset
                // (enable_order_hint = 0), so the sign bias is always 0.
                throw new NotSupportedException("av1_setup_frame_sign_bias: enable_order_hint");
            }
            else
            {
                cm.ref_frame_sign_bias[ref_frame] = 0;
            }
        }
    }

    // [subset] get_block_position, motion_field_projection, av1_setup_motion_field: only used
    // when allow_ref_frame_mvs is set (off in the subset).

    // cm->ref_frame_side is calculated here, and will be used in
    // av1_copy_frame_mvs() to affect how mvs are copied.
    internal static void av1_calculate_ref_frame_side(AV1_COMMON cm)
    {
        ref OrderHintInfo order_hint_info = ref cm.seq_params.order_hint_info;

        cm.ref_frame_side.AsSpan().Clear();
        if (order_hint_info.enable_order_hint == 0) return;

        // [subset] The per-reference side computation needs order hints, which are disabled in
        // the realtime subset (enable_order_hint = 0).
        throw new NotSupportedException("av1_calculate_ref_frame_side: enable_order_hint");
    }

    // [subset] record_samples, av1_selectSamples, av1_findSamples: warped motion is disabled.

    internal static void av1_setup_skip_mode_allowed(AV1_COMMON cm)
    {
        ref OrderHintInfo order_hint_info = ref cm.seq_params.order_hint_info;
        ref SkipModeInfo skip_mode_info = ref cm.current_frame.skip_mode_info;

        skip_mode_info.skip_mode_allowed = 0;
        skip_mode_info.ref_frame_idx_0 = INVALID_IDX;
        skip_mode_info.ref_frame_idx_1 = INVALID_IDX;

        if (order_hint_info.enable_order_hint == 0 || frame_is_intra_only(cm) != 0 ||
            cm.current_frame.reference_mode == SINGLE_REFERENCE)
            return;

        // [subset] Skip mode needs order hints, which are disabled in the realtime subset
        // (enable_order_hint = 0): the function always returns above.
        throw new NotSupportedException("av1_setup_skip_mode_allowed: enable_order_hint");
    }

    // [subset] compare_ref_frame_info, set_ref_frame_info, av1_set_frame_refs: never executed
    // (frame_refs_short_signaling is off).
}
