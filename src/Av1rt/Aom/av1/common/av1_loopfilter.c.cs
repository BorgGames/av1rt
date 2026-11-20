// Port of libaom av1/common/av1_loopfilter.c

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class av1_loopfilter
{
    // enum USE_FILTER_TYPE (UENUM1BYTE); the type itself is passed as int.
    private const int USE_SINGLE = 0;
    private const int USE_DUAL = 1;
    private const int USE_QUAD = 2;

    private static readonly SEG_LVL_FEATURES[][] seg_lvl_lf_lut = {
        new[] { SEG_LVL_ALT_LF_Y_V, SEG_LVL_ALT_LF_Y_H },
        new[] { SEG_LVL_ALT_LF_U, SEG_LVL_ALT_LF_U },
        new[] { SEG_LVL_ALT_LF_V, SEG_LVL_ALT_LF_V }
    };

    private static readonly int[][] delta_lf_id_lut = { new[] { 0, 1 },
                                                        new[] { 2, 2 },
                                                        new[] { 3, 3 } };

    private static readonly int[] mode_lf_lut = {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,  // INTRA_MODES
        1, 1, 0, 1,                             // INTER_MODES (GLOBALMV == 0)
        1, 1, 1, 1, 1, 1, 0, 1  // INTER_COMPOUND_MODES (GLOBAL_GLOBALMV == 0)
    };

    private static void update_sharpness(loop_filter_info_n lfi, int sharpness_lvl)
    {
        int lvl;

        // For each possible value for the loop filter fill out limits
        for (lvl = 0; lvl <= MAX_LOOP_FILTER; lvl++)
        {
            // Set loop filter parameters that control sharpness.
            int block_inside_limit = lvl >> (B(sharpness_lvl > 0) + B(sharpness_lvl > 4));

            if (sharpness_lvl > 0)
            {
                if (block_inside_limit > (9 - sharpness_lvl))
                    block_inside_limit = (9 - sharpness_lvl);
            }

            if (block_inside_limit < 1) block_inside_limit = 1;

            memset<byte>(lfi.lfthr[lvl].lim, (byte)block_inside_limit, SIMD_WIDTH);
            memset<byte>(lfi.lfthr[lvl].mblim, (byte)(2 * (lvl + 2) + block_inside_limit),
                         SIMD_WIDTH);
        }
    }

    private static byte get_filter_level(AV1_COMMON cm,
                                         loop_filter_info_n lfi_n,
                                         int dir_idx, int plane,
                                         MB_MODE_INFO mbmi)
    {
        int segment_id = mbmi.segment_id;
        if (cm.delta_q_info.delta_lf_present_flag != 0)
        {
            // [subset] Per-block loop-filter deltas (delta_lf_present_flag): deltaq_mode is 0 in
            // the subset, so delta_lf is never present; never executed.
            throw new NotSupportedException("get_filter_level: delta_lf_present_flag");
        }
        else
        {
            return lfi_n.lvl[plane][segment_id][dir_idx][mbmi.ref_frame[0]]
                            [mode_lf_lut[mbmi.mode]];
        }
    }

    internal static void av1_loop_filter_init(AV1_COMMON cm)
    {
        Debug.Assert(MB_MODE_COUNT == mode_lf_lut.Length);
        loop_filter_info_n lfi = cm.lf_info;
        loopfilter lf = cm.lf;
        int lvl;

        // init limits for given sharpness
        update_sharpness(lfi, lf.sharpness_level);

        // init hev threshold const vectors
        for (lvl = 0; lvl <= MAX_LOOP_FILTER; lvl++)
            memset<byte>(lfi.lfthr[lvl].hev_thr, (byte)(lvl >> 4), SIMD_WIDTH);
    }

    // Update the loop filter for the current frame.
    // This should be called before loop_filter_rows(),
    // av1_loop_filter_frame() calls this function directly.
    internal static void av1_loop_filter_frame_init(AV1_COMMON cm, int plane_start,
                                                    int plane_end)
    {
        Span<int> filt_lvl = stackalloc int[MAX_MB_PLANE];
        Span<int> filt_lvl_r = stackalloc int[MAX_MB_PLANE];
        int plane;
        int seg_id;
        // n_shift is the multiplier for lf_deltas
        // the multiplier is 1 for when filter_lvl is between 0 and 31;
        // 2 when filter_lvl is between 32 and 63
        loop_filter_info_n lfi = cm.lf_info;
        loopfilter lf = cm.lf;
        segmentation seg = cm.seg;

        // update sharpness limits
        update_sharpness(lfi, lf.sharpness_level);

        filt_lvl[0] = cm.lf.filter_level[0];
        filt_lvl[1] = cm.lf.filter_level_u;
        filt_lvl[2] = cm.lf.filter_level_v;

        filt_lvl_r[0] = cm.lf.filter_level[1];
        filt_lvl_r[1] = cm.lf.filter_level_u;
        filt_lvl_r[2] = cm.lf.filter_level_v;

        Debug.Assert(plane_start >= AOM_PLANE_Y);
        Debug.Assert(plane_end <= MAX_MB_PLANE);

        for (plane = plane_start; plane < plane_end; plane++)
        {
            if (plane == 0 && filt_lvl[0] == 0 && filt_lvl_r[0] == 0)
                break;
            else if (plane == 1 && filt_lvl[1] == 0)
                continue;
            else if (plane == 2 && filt_lvl[2] == 0)
                continue;

            for (seg_id = 0; seg_id < MAX_SEGMENTS; seg_id++)
            {
                for (int dir = 0; dir < 2; ++dir)
                {
                    int lvl_seg = (dir == 0) ? filt_lvl[plane] : filt_lvl_r[plane];
                    SEG_LVL_FEATURES seg_lf_feature_id = seg_lvl_lf_lut[plane][dir];
                    if (segfeature_active(seg, (byte)seg_id, seg_lf_feature_id) != 0)
                    {
                        int data = get_segdata(cm.seg, seg_id, seg_lf_feature_id);
                        lvl_seg = clamp(lvl_seg + data, 0, MAX_LOOP_FILTER);
                    }

                    if (lf.mode_ref_delta_enabled == 0)
                    {
                        // we could get rid of this if we assume that deltas are set to
                        // zero when not in use; encoder always uses deltas
                        byte[][] lvl_dir = lfi.lvl[plane][seg_id][dir];
                        for (int r = 0; r < REF_FRAMES; r++)
                            memset<byte>(lvl_dir[r], (byte)lvl_seg, MAX_MODE_LF_DELTAS);
                    }
                    else
                    {
                        int @ref, mode;
                        int scale = 1 << (lvl_seg >> 5);
                        int intra_lvl = lvl_seg + lf.ref_deltas[INTRA_FRAME] * scale;
                        lfi.lvl[plane][seg_id][dir][INTRA_FRAME][0] =
                            (byte)clamp(intra_lvl, 0, MAX_LOOP_FILTER);

                        for (@ref = LAST_FRAME; @ref < REF_FRAMES; ++@ref)
                        {
                            for (mode = 0; mode < MAX_MODE_LF_DELTAS; ++mode)
                            {
                                int inter_lvl = lvl_seg + lf.ref_deltas[@ref] * scale +
                                                lf.mode_deltas[mode] * scale;
                                lfi.lvl[plane][seg_id][dir][@ref][mode] =
                                    (byte)clamp(inter_lvl, 0, MAX_LOOP_FILTER);
                            }
                        }
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TX_SIZE get_transform_size(MACROBLOCKD xd, MB_MODE_INFO mbmi,
                                              int mi_row, int mi_col, int plane,
                                              int ss_x, int ss_y)
    {
        Debug.Assert(mbmi != null);
        if (xd != null && xd.lossless[mbmi.segment_id] != 0) return TX_4X4;

        TX_SIZE tx_size = (plane == AOM_PLANE_Y)
                              ? mbmi.tx_size
                              : av1_get_max_uv_txsize(mbmi.bsize, ss_x, ss_y);
        Debug.Assert(tx_size < TX_SIZES_ALL);
        if ((plane == AOM_PLANE_Y) && is_inter_block(mbmi) != 0 && mbmi.skip_txfm == 0)
        {
            BLOCK_SIZE sb_type = mbmi.bsize;
            int blk_row = mi_row & (mi_size_high[sb_type] - 1);
            int blk_col = mi_col & (mi_size_wide[sb_type] - 1);
            TX_SIZE mb_tx_size =
                mbmi.inter_tx_size[av1_get_txb_size_index(sb_type, blk_row, blk_col)];
            Debug.Assert(mb_tx_size < TX_SIZES_ALL);
            tx_size = mb_tx_size;
        }

        return tx_size;
    }

    // [subset] tx_dim_to_filter_length and set_lpf_parameters: only used by
    // av1_filter_block_plane_vert / av1_filter_block_plane_horz (lpf_opt_level == 0), which
    // never execute (speed 11 always uses the *_opt paths); not ported.

    // [port] Generated from the C initializers (scratch script, values copied, not retyped).
    private static readonly uint[][] vert_filter_length_luma = {
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8 },
        new uint[] { 4, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
        new uint[] { 4, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 4, 8, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14, 14 },
    };

    private static readonly uint[][] horz_filter_length_luma = {
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
        new uint[] { 4, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8 },
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
        new uint[] { 4, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
        new uint[] { 4, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8, 8, 8, 8, 4, 8, 8, 8, 8 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
        new uint[] { 4, 8, 14, 14, 14, 8, 4, 14, 8, 14, 14, 14, 14, 14, 4, 14, 8, 14, 14 },
    };

    private static readonly uint[][] vert_filter_length_chroma = {
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6 },
    };

    private static readonly uint[][] horz_filter_length_chroma = {
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
        new uint[] { 4, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6, 6, 6, 6, 4, 6, 6, 6, 6 },
    };

    // [port] `AV1_DEBLOCKING_PARAMETERS *params` points into the caller's params_buf, so it is a
    // Ptr; `TX_SIZE *tx_size` likewise; `int *min_dim` is a ref.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_one_param_for_line_luma(
        Ptr<AV1_DEBLOCKING_PARAMETERS> @params, Ptr<TX_SIZE> tx_size,
        AV1_COMMON cm, MACROBLOCKD xd,
        EDGE_DIR edge_dir, uint mi_col, uint mi_row,
        macroblockd_plane plane_ptr, int coord,
        bool is_first_block, TX_SIZE prev_tx_size, int mode_step,
        ref int min_dim)
    {
        Debug.Assert(mi_col << MI_SIZE_LOG2 < (uint)plane_ptr.dst.width &&
                     mi_row << MI_SIZE_LOG2 < (uint)plane_ptr.dst.height);
        int is_vert = B(edge_dir == VERT_EDGE);
        // reset to initial values
        @params.Value.filter_length = 0;

        MB_MODE_INFO[] grid = cm.mi_params.mi_grid_base;
        int mi = (int)(mi_row * (uint)cm.mi_params.mi_stride + mi_col);
        MB_MODE_INFO mbmi = grid[mi];
        Debug.Assert(mbmi != null);

        TX_SIZE ts =
            get_transform_size(xd, grid[mi], (int)mi_row, (int)mi_col, AOM_PLANE_Y, 0, 0);

#if DEBUG
        {
            uint transform_masks =
                (uint)(is_vert != 0 ? tx_size_wide[ts] - 1 : tx_size_high[ts] - 1);
            int tu_edge = (((uint)(coord * MI_SIZE) & transform_masks) != 0) ? (0) : (1);
            Debug.Assert(tu_edge != 0);
        }
#endif
        // If we are not the first block, then coord is always true, so
        // !is_first_block is technically redundant. But we are keeping it here so the
        // compiler can compile away this conditional if we pass in is_first_block :=
        // false
        bool curr_skipped = false;
        if (!is_first_block || coord != 0)
        {
            MB_MODE_INFO mi_prev = grid[mi - mode_step];
            int pv_row = is_vert != 0 ? (int)mi_row : (int)(mi_row - 1);
            int pv_col = is_vert != 0 ? (int)(mi_col - 1) : (int)mi_col;
            TX_SIZE pv_ts =
                is_first_block
                    ? get_transform_size(xd, mi_prev, pv_row, pv_col, AOM_PLANE_Y, 0, 0)
                    : prev_tx_size;
            if (is_first_block)
            {
                min_dim = is_vert != 0 ? block_size_high[mi_prev.bsize]
                                       : block_size_wide[mi_prev.bsize];
            }
            Debug.Assert(mi_prev != null);
            byte level =
                get_filter_level(cm, cm.lf_info, edge_dir, AOM_PLANE_Y, mbmi);
            if (level == 0)
            {
                level =
                    get_filter_level(cm, cm.lf_info, edge_dir, AOM_PLANE_Y, mi_prev);
            }

            int pu_edge = B(mi_prev != mbmi);

            // The quad loop filter assumes that all the transform blocks within a
            // 8x16/16x8/16x16 prediction block are of the same size.
            Debug.Assert(!(pu_edge == 0 && (mbmi.bsize >= BLOCK_8X16 && mbmi.bsize <= BLOCK_16X16)) ||
                         pv_ts == ts);

            if (pu_edge == 0)
            {
                curr_skipped = mbmi.skip_txfm != 0 && is_inter_block(mbmi) != 0;
            }
            if ((pu_edge != 0 || !curr_skipped) && level != 0)
            {
                @params.Value.filter_length = (byte)(is_vert != 0 ? vert_filter_length_luma[ts][pv_ts]
                                                                  : horz_filter_length_luma[ts][pv_ts]);

                // prepare common parameters
                loop_filter_thresh limits = cm.lf_info.lfthr[level];
                @params.Value.lfthr = limits;
            }
        }
        int block_dim =
            is_vert != 0 ? block_size_high[mbmi.bsize] : block_size_wide[mbmi.bsize];
        min_dim = AOMMIN(min_dim, block_dim);

        tx_size.Value = ts;
    }

    // Similar to set_lpf_parameters, but does so one row/col at a time to reduce
    // calls to \ref get_transform_size and \ref get_filter_level
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_lpf_parameters_for_line_luma(
        Ptr<AV1_DEBLOCKING_PARAMETERS> params_buf, Ptr<TX_SIZE> tx_buf,
        AV1_COMMON cm, MACROBLOCKD xd,
        EDGE_DIR edge_dir, uint mi_col, uint mi_row,
        macroblockd_plane plane_ptr, uint mi_range,
        int mode_step, ref int min_dim)
    {
        int is_vert = B(edge_dir == VERT_EDGE);

        Ptr<AV1_DEBLOCKING_PARAMETERS> @params = params_buf;
        Ptr<TX_SIZE> tx_size = tx_buf;
        ref uint counter_ptr = ref (is_vert != 0 ? ref mi_col : ref mi_row);
        TX_SIZE prev_tx_size = TX_INVALID;

        // Unroll the first iteration of the loop
        set_one_param_for_line_luma(@params, tx_size, cm, xd, edge_dir, mi_col, mi_row,
                                    plane_ptr, (int)counter_ptr, true, prev_tx_size,
                                    mode_step, ref min_dim);

        // Advance
        int advance_units =
            is_vert != 0 ? tx_size_wide_unit[tx_size.Value] : tx_size_high_unit[tx_size.Value];
        prev_tx_size = tx_size.Value;
        counter_ptr += (uint)advance_units;
        @params += advance_units;
        tx_size += advance_units;

        while (counter_ptr < mi_range)
        {
            set_one_param_for_line_luma(@params, tx_size, cm, xd, edge_dir, mi_col,
                                        mi_row, plane_ptr, (int)counter_ptr, false,
                                        prev_tx_size, mode_step, ref min_dim);

            // Advance
            advance_units =
                is_vert != 0 ? tx_size_wide_unit[tx_size.Value] : tx_size_high_unit[tx_size.Value];
            prev_tx_size = tx_size.Value;
            counter_ptr += (uint)advance_units;
            @params += advance_units;
            tx_size += advance_units;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_one_param_for_line_chroma(
        Ptr<AV1_DEBLOCKING_PARAMETERS> @params, Ptr<TX_SIZE> tx_size,
        AV1_COMMON cm, MACROBLOCKD xd,
        EDGE_DIR edge_dir, uint mi_col, uint mi_row, int coord,
        bool is_first_block, TX_SIZE prev_tx_size,
        macroblockd_plane plane_ptr, int mode_step,
        int scale_horz, int scale_vert, ref int min_dim, int plane,
        int joint_filter_chroma)
    {
        int is_vert = B(edge_dir == VERT_EDGE);
        Debug.Assert((mi_col << MI_SIZE_LOG2) <
                         (uint)(plane_ptr.dst.width << scale_horz) &&
                     (mi_row << MI_SIZE_LOG2) <
                         (uint)(plane_ptr.dst.height << scale_vert));
        // reset to initial values
        @params.Value.filter_length = 0;

        // for sub8x8 block, chroma prediction mode is obtained from the
        // bottom/right mi structure of the co-located 8x8 luma block. so for chroma
        // plane, mi_row and mi_col should map to the bottom/right mi structure,
        // i.e, both mi_row and mi_col should be odd number for chroma plane.
        mi_row |= (uint)scale_vert;
        mi_col |= (uint)scale_horz;
        MB_MODE_INFO[] grid = cm.mi_params.mi_grid_base;
        int mi = (int)(mi_row * (uint)cm.mi_params.mi_stride + mi_col);
        MB_MODE_INFO mbmi = grid[mi];
        Debug.Assert(mbmi != null);

        TX_SIZE ts = get_transform_size(xd, grid[mi], (int)mi_row, (int)mi_col, plane,
                                        scale_horz, scale_vert);
        tx_size.Value = ts;

#if DEBUG
        {
            uint transform_masks =
                (uint)(is_vert != 0 ? tx_size_wide[ts] - 1 : tx_size_high[ts] - 1);
            int tu_edge = (((uint)(coord * MI_SIZE) & transform_masks) != 0) ? (0) : (1);
            Debug.Assert(tu_edge != 0);
        }
#endif

        // If we are not the first block, then coord is always true, so
        // !is_first_block is technically redundant. But we are keeping it here so the
        // compiler can compile away this conditional if we pass in is_first_block :=
        // false
        bool curr_skipped = false;
        if (!is_first_block || coord != 0)
        {
            MB_MODE_INFO mi_prev = grid[mi - mode_step];
            Debug.Assert(mi_prev != null);
            int pv_row = is_vert != 0 ? (int)(mi_row) : (int)(mi_row - (1u << scale_vert));
            int pv_col = is_vert != 0 ? (int)(mi_col - (1u << scale_horz)) : (int)(mi_col);
            TX_SIZE pv_ts =
                is_first_block ? get_transform_size(xd, mi_prev, pv_row, pv_col, plane,
                                                    scale_horz, scale_vert)
                               : prev_tx_size;
            if (is_first_block)
            {
                min_dim = is_vert != 0 ? tx_size_high[pv_ts] : tx_size_wide[pv_ts];
            }

            byte level = get_filter_level(cm, cm.lf_info, edge_dir, plane, mbmi);
            if (level == 0)
            {
                level = get_filter_level(cm, cm.lf_info, edge_dir, plane, mi_prev);
            }
            // [port] The NDEBUG-only check that the V level equals the U level when
            // joint_filter_chroma is set is omitted.
            int pu_edge = B(mi_prev != mbmi);

            if (pu_edge == 0)
            {
                curr_skipped = mbmi.skip_txfm != 0 && is_inter_block(mbmi) != 0;
            }
            // For realtime mode, u and v have the same level
            if ((!curr_skipped || pu_edge != 0) && level != 0)
            {
                @params.Value.filter_length = (byte)(is_vert != 0 ? vert_filter_length_chroma[ts][pv_ts]
                                                                  : horz_filter_length_chroma[ts][pv_ts]);

                loop_filter_thresh[] limits = cm.lf_info.lfthr;
                @params.Value.lfthr = limits[level];
            }
        }
        int tx_dim = is_vert != 0 ? tx_size_high[ts] : tx_size_wide[ts];
        min_dim = AOMMIN(min_dim, tx_dim);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_lpf_parameters_for_line_chroma(
        Ptr<AV1_DEBLOCKING_PARAMETERS> params_buf, Ptr<TX_SIZE> tx_buf,
        AV1_COMMON cm, MACROBLOCKD xd,
        EDGE_DIR edge_dir, uint mi_col, uint mi_row,
        macroblockd_plane plane_ptr, uint mi_range,
        int mode_step, int scale_horz, int scale_vert,
        ref int min_dim, int plane, int joint_filter_chroma)
    {
        int is_vert = B(edge_dir == VERT_EDGE);

        Ptr<AV1_DEBLOCKING_PARAMETERS> @params = params_buf;
        Ptr<TX_SIZE> tx_size = tx_buf;
        ref uint counter_ptr = ref (is_vert != 0 ? ref mi_col : ref mi_row);
        int scale = is_vert != 0 ? scale_horz : scale_vert;
        TX_SIZE prev_tx_size = TX_INVALID;

        // Unroll the first iteration of the loop
        set_one_param_for_line_chroma(@params, tx_size, cm, xd, edge_dir, mi_col,
                                      mi_row, (int)counter_ptr, true, prev_tx_size,
                                      plane_ptr, mode_step, scale_horz, scale_vert,
                                      ref min_dim, plane, joint_filter_chroma);

        // Advance
        int advance_units =
            is_vert != 0 ? tx_size_wide_unit[tx_size.Value] : tx_size_high_unit[tx_size.Value];
        prev_tx_size = tx_size.Value;
        counter_ptr += (uint)(advance_units << scale);
        @params += advance_units;
        tx_size += advance_units;

        while (counter_ptr < mi_range)
        {
            set_one_param_for_line_chroma(@params, tx_size, cm, xd, edge_dir, mi_col,
                                          mi_row, (int)counter_ptr, false, prev_tx_size,
                                          plane_ptr, mode_step, scale_horz, scale_vert,
                                          ref min_dim, plane, joint_filter_chroma);

            // Advance
            advance_units =
                is_vert != 0 ? tx_size_wide_unit[tx_size.Value] : tx_size_high_unit[tx_size.Value];
            prev_tx_size = tx_size.Value;
            counter_ptr += (uint)(advance_units << scale);
            @params += advance_units;
            tx_size += advance_units;
        }
    }

    // [subset] The CONFIG_AV1_HIGHBITDEPTH branches (aom_highbd_lpf_*) of filter_vert,
    // filter_vert_chroma, filter_horz and filter_horz_chroma are dropped; seq_params is then
    // unused, as in the C 8-bit build.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void filter_vert(Ptr<byte> dst, int dst_stride,
                                    in AV1_DEBLOCKING_PARAMETERS @params,
                                    SequenceHeader seq_params,
                                    int use_filter_type)
    {
        loop_filter_thresh limits = @params.lfthr;
        if (use_filter_type == USE_QUAD)
        {
            // Only one set of loop filter parameters (mblim, lim and hev_thr) is
            // passed as argument to quad loop filter because quad loop filter is
            // called for those cases where all the 4 set of loop filter parameters
            // are equal.
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_vertical_4_quad(dst, dst_stride, limits.mblim, limits.lim,
                                            limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_vertical_6_quad(dst, dst_stride, limits.mblim, limits.lim,
                                            limits.hev_thr);
                    break;
                // apply 8-tap filtering
                case 8:
                    aom_lpf_vertical_8_quad(dst, dst_stride, limits.mblim, limits.lim,
                                            limits.hev_thr);
                    break;
                // apply 14-tap filtering
                case 14:
                    aom_lpf_vertical_14_quad(dst, dst_stride, limits.mblim, limits.lim,
                                             limits.hev_thr);
                    break;
                // no filtering
                default: break;
            }
        }
        else if (use_filter_type == USE_DUAL)
        {
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_vertical_4_dual(dst, dst_stride, limits.mblim, limits.lim,
                                            limits.hev_thr, limits.mblim, limits.lim,
                                            limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_vertical_6_dual(dst, dst_stride, limits.mblim, limits.lim,
                                            limits.hev_thr, limits.mblim, limits.lim,
                                            limits.hev_thr);
                    break;
                // apply 8-tap filtering
                case 8:
                    aom_lpf_vertical_8_dual(dst, dst_stride, limits.mblim, limits.lim,
                                            limits.hev_thr, limits.mblim, limits.lim,
                                            limits.hev_thr);
                    break;
                // apply 14-tap filtering
                case 14:
                    aom_lpf_vertical_14_dual(dst, dst_stride, limits.mblim, limits.lim,
                                             limits.hev_thr, limits.mblim, limits.lim,
                                             limits.hev_thr);
                    break;
                // no filtering
                default: break;
            }
        }
        else
        {
            Debug.Assert(use_filter_type == USE_SINGLE);
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_vertical_4(dst, dst_stride, limits.mblim, limits.lim,
                                       limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_vertical_6(dst, dst_stride, limits.mblim, limits.lim,
                                       limits.hev_thr);
                    break;
                // apply 8-tap filtering
                case 8:
                    aom_lpf_vertical_8(dst, dst_stride, limits.mblim, limits.lim,
                                       limits.hev_thr);
                    break;
                // apply 14-tap filtering
                case 14:
                    aom_lpf_vertical_14(dst, dst_stride, limits.mblim, limits.lim,
                                        limits.hev_thr);
                    break;
                // no filtering
                default: break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void filter_vert_chroma(Ptr<byte> u_dst, Ptr<byte> v_dst,
                                           int dst_stride,
                                           in AV1_DEBLOCKING_PARAMETERS @params,
                                           SequenceHeader seq_params,
                                           int use_filter_type)
    {
        loop_filter_thresh u_limits = @params.lfthr;
        loop_filter_thresh v_limits = @params.lfthr;
        if (use_filter_type == USE_QUAD)
        {
            // Only one set of loop filter parameters (mblim, lim and hev_thr) is
            // passed as argument to quad loop filter because quad loop filter is
            // called for those cases where all the 4 set of loop filter parameters
            // are equal.
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_vertical_4_quad(u_dst, dst_stride, u_limits.mblim,
                                            u_limits.lim, u_limits.hev_thr);
                    aom_lpf_vertical_4_quad(v_dst, dst_stride, v_limits.mblim,
                                            v_limits.lim, v_limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_vertical_6_quad(u_dst, dst_stride, u_limits.mblim,
                                            u_limits.lim, u_limits.hev_thr);
                    aom_lpf_vertical_6_quad(v_dst, dst_stride, v_limits.mblim,
                                            v_limits.lim, v_limits.hev_thr);
                    break;
                case 8:
                case 14: Debug.Assert(false); break;
                // no filtering
                default: break;
            }
        }
        else if (use_filter_type == USE_DUAL)
        {
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_vertical_4_dual(u_dst, dst_stride, u_limits.mblim,
                                            u_limits.lim, u_limits.hev_thr,
                                            u_limits.mblim, u_limits.lim,
                                            u_limits.hev_thr);
                    aom_lpf_vertical_4_dual(v_dst, dst_stride, v_limits.mblim,
                                            v_limits.lim, v_limits.hev_thr,
                                            v_limits.mblim, v_limits.lim,
                                            v_limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_vertical_6_dual(u_dst, dst_stride, u_limits.mblim,
                                            u_limits.lim, u_limits.hev_thr,
                                            u_limits.mblim, u_limits.lim,
                                            u_limits.hev_thr);
                    aom_lpf_vertical_6_dual(v_dst, dst_stride, v_limits.mblim,
                                            v_limits.lim, v_limits.hev_thr,
                                            v_limits.mblim, v_limits.lim,
                                            v_limits.hev_thr);
                    break;
                case 8:
                case 14: Debug.Assert(false); break;
                // no filtering
                default: break;
            }
        }
        else
        {
            Debug.Assert(use_filter_type == USE_SINGLE);
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_vertical_4(u_dst, dst_stride, u_limits.mblim, u_limits.lim,
                                       u_limits.hev_thr);
                    aom_lpf_vertical_4(v_dst, dst_stride, v_limits.mblim, v_limits.lim,
                                       u_limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_vertical_6(u_dst, dst_stride, u_limits.mblim, u_limits.lim,
                                       u_limits.hev_thr);
                    aom_lpf_vertical_6(v_dst, dst_stride, v_limits.mblim, v_limits.lim,
                                       v_limits.hev_thr);
                    break;
                case 8:
                case 14: Debug.Assert(false); break;
                // no filtering
                default: break;
            }
        }
    }

    // [subset] av1_filter_block_plane_vert: lpf_opt_level == 0 path, never executed (speed 11
    // always uses av1_filter_block_plane_vert_opt / _opt_chroma).

    internal static void av1_filter_block_plane_vert_opt(
        AV1_COMMON cm, MACROBLOCKD xd,
        macroblockd_plane plane_ptr, uint mi_row,
        uint mi_col, Ptr<AV1_DEBLOCKING_PARAMETERS> params_buf,
        Ptr<TX_SIZE> tx_buf, int num_mis_in_lpf_unit_height_log2)
    {
        Ptr<byte> dst_ptr = plane_ptr.dst.buf;
        int dst_stride = plane_ptr.dst.stride;
        // Ensure that mi_cols/mi_rows are calculated based on frame dimension aligned
        // to MI_SIZE.
        int plane_mi_cols =
            CEIL_POWER_OF_TWO(plane_ptr.dst.width, MI_SIZE_LOG2);
        int plane_mi_rows =
            CEIL_POWER_OF_TWO(plane_ptr.dst.height, MI_SIZE_LOG2);
        // Whenever 'pipeline_lpf_mt_with_enc' is enabled, height of the unit to
        // filter (i.e., y_range) is calculated based on the size of the superblock
        // used.
        int y_range = AOMMIN((int)((uint)plane_mi_rows - mi_row),
                             (1 << num_mis_in_lpf_unit_height_log2));
        // Width of the unit to filter (i.e., x_range) should always be calculated
        // based on maximum superblock size as this function is called for mi_col = 0,
        // MAX_MIB_SIZE, 2 * MAX_MIB_SIZE etc.
        int x_range = AOMMIN((int)((uint)plane_mi_cols - mi_col), MAX_MIB_SIZE);
        const int mode_step = 1;
        for (int y = 0; y < y_range; y++)
        {
            uint curr_y = mi_row + (uint)y;
            uint x_start = mi_col;
            uint x_end = mi_col + (uint)x_range;
            int min_block_height = block_size_high[BLOCK_128X128];
            set_lpf_parameters_for_line_luma(params_buf, tx_buf, cm, xd, VERT_EDGE,
                                             x_start, curr_y, plane_ptr, x_end,
                                             mode_step, ref min_block_height);

            Ptr<AV1_DEBLOCKING_PARAMETERS> @params = params_buf;
            Ptr<TX_SIZE> tx_size = tx_buf;
            int use_filter_type = USE_SINGLE;

            Ptr<byte> p = dst_ptr + y * MI_SIZE * dst_stride;

            if ((y & 3) == 0 && (y + 3) < y_range && min_block_height >= 16)
            {
                // If we are on a row which is a multiple of 4, and the minimum height is
                // 16 pixels, then the current and right 3 cols must contain the same
                // prediction block. This is because dim 16 can only happen every unit of
                // 4 mi's.
                use_filter_type = USE_QUAD;
                y += 3;
            }
            else if ((y + 1) < y_range && min_block_height >= 8)
            {
                use_filter_type = USE_DUAL;
                y += 1;
            }

            for (int x = 0; x < x_range;)
            {
                if (tx_size.Value == TX_INVALID)
                {
                    @params.Value.filter_length = 0;
                    tx_size.Value = TX_4X4;
                }

                filter_vert(p, dst_stride, in @params.Value, cm.seq_params, use_filter_type);

                // advance the destination pointer
                int advance_units = tx_size_wide_unit[tx_size.Value];
                x += advance_units;
                p += advance_units * MI_SIZE;
                @params += advance_units;
                tx_size += advance_units;
            }
        }
    }

    // [port] `const MACROBLOCKD_PLANE *plane_ptr` is a Ptr because joint chroma filtering reads
    // plane_ptr[1] (the V plane following U in the planes array).
    internal static void av1_filter_block_plane_vert_opt_chroma(
        AV1_COMMON cm, MACROBLOCKD xd,
        Ptr<macroblockd_plane> plane_ptr, uint mi_row,
        uint mi_col, Ptr<AV1_DEBLOCKING_PARAMETERS> params_buf,
        Ptr<TX_SIZE> tx_buf, int plane, bool joint_filter_chroma,
        int num_mis_in_lpf_unit_height_log2)
    {
        int scale_horz = plane_ptr.Value.subsampling_x;
        int scale_vert = plane_ptr.Value.subsampling_y;
        int dst_stride = plane_ptr.Value.dst.stride;
        // Ensure that mi_cols/mi_rows are calculated based on frame dimension aligned
        // to MI_SIZE.
        int mi_cols =
            ((plane_ptr.Value.dst.width << scale_horz) + MI_SIZE - 1) >> MI_SIZE_LOG2;
        int mi_rows =
            ((plane_ptr.Value.dst.height << scale_vert) + MI_SIZE - 1) >> MI_SIZE_LOG2;
        int plane_mi_rows = ROUND_POWER_OF_TWO(mi_rows, scale_vert);
        int plane_mi_cols = ROUND_POWER_OF_TWO(mi_cols, scale_horz);
        int y_range =
            AOMMIN((int)((uint)plane_mi_rows - (mi_row >> scale_vert)),
                   ((1 << num_mis_in_lpf_unit_height_log2) >> scale_vert));
        int x_range = AOMMIN((int)((uint)plane_mi_cols - (mi_col >> scale_horz)),
                             (MAX_MIB_SIZE >> scale_horz));
        int mode_step = 1 << scale_horz;

        for (int y = 0; y < y_range; y++)
        {
            uint curr_y = mi_row + (uint)(y << scale_vert);
            uint x_start = mi_col + (uint)(0 << scale_horz);
            uint x_end = mi_col + (uint)(x_range << scale_horz);
            int min_height = tx_size_high[TX_64X64];
            set_lpf_parameters_for_line_chroma(params_buf, tx_buf, cm, xd, VERT_EDGE,
                                               x_start, curr_y, plane_ptr.Value, x_end,
                                               mode_step, scale_horz, scale_vert,
                                               ref min_height, plane, B(joint_filter_chroma));

            Ptr<AV1_DEBLOCKING_PARAMETERS> @params = params_buf;
            Ptr<TX_SIZE> tx_size = tx_buf;
            int use_filter_type = USE_SINGLE;
            int y_inc = 0;

            if ((y & 3) == 0 && (y + 3) < y_range && min_height >= 16)
            {
                // If we are on a row which is a multiple of 4, and the minimum height is
                // 16 pixels, then the current and below 3 rows must contain the same tx
                // block. This is because dim 16 can only happen every unit of 4 mi's.
                use_filter_type = USE_QUAD;
                y_inc = 3;
            }
            else if (y % 2 == 0 && (y + 1) < y_range && min_height >= 8)
            {
                // If we are on an even row, and the minimum height is 8 pixels, then the
                // current and below rows must contain the same tx block. This is because
                // dim 4 can only happen every unit of 2**0, and 8 every unit of 2**1,
                // etc.
                use_filter_type = USE_DUAL;
                y_inc = 1;
            }

            for (int x = 0; x < x_range;)
            {
                // inner loop always filter vertical edges in a MI block. If MI size
                // is 8x8, it will filter the vertical edge aligned with a 8x8 block.
                // If 4x4 transform is used, it will then filter the internal edge
                //  aligned with a 4x4 block
                if (tx_size.Value == TX_INVALID)
                {
                    @params.Value.filter_length = 0;
                    tx_size.Value = TX_4X4;
                }

                int offset = y * MI_SIZE * dst_stride + x * MI_SIZE;
                if (joint_filter_chroma)
                {
                    Ptr<byte> u_dst = plane_ptr[0].dst.buf + offset;
                    Ptr<byte> v_dst = plane_ptr[1].dst.buf + offset;
                    filter_vert_chroma(u_dst, v_dst, dst_stride, in @params.Value, cm.seq_params,
                                       use_filter_type);
                }
                else
                {
                    Ptr<byte> dst_ptr = plane_ptr.Value.dst.buf + offset;
                    filter_vert(dst_ptr, dst_stride, in @params.Value, cm.seq_params,
                                use_filter_type);
                }

                // advance the destination pointer
                int advance_units = tx_size_wide_unit[tx_size.Value];
                x += advance_units;
                @params += advance_units;
                tx_size += advance_units;
            }
            y += y_inc;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void filter_horz(Ptr<byte> dst, int dst_stride,
                                    in AV1_DEBLOCKING_PARAMETERS @params,
                                    SequenceHeader seq_params,
                                    int use_filter_type)
    {
        loop_filter_thresh limits = @params.lfthr;
        if (use_filter_type == USE_QUAD)
        {
            // Only one set of loop filter parameters (mblim, lim and hev_thr) is
            // passed as argument to quad loop filter because quad loop filter is
            // called for those cases where all the 4 set of loop filter parameters
            // are equal.
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_horizontal_4_quad(dst, dst_stride, limits.mblim, limits.lim,
                                              limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_horizontal_6_quad(dst, dst_stride, limits.mblim, limits.lim,
                                              limits.hev_thr);
                    break;
                // apply 8-tap filtering
                case 8:
                    aom_lpf_horizontal_8_quad(dst, dst_stride, limits.mblim, limits.lim,
                                              limits.hev_thr);
                    break;
                // apply 14-tap filtering
                case 14:
                    aom_lpf_horizontal_14_quad(dst, dst_stride, limits.mblim, limits.lim,
                                               limits.hev_thr);
                    break;
                // no filtering
                default: break;
            }
        }
        else if (use_filter_type == USE_DUAL)
        {
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_horizontal_4_dual(dst, dst_stride, limits.mblim, limits.lim,
                                              limits.hev_thr, limits.mblim, limits.lim,
                                              limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_horizontal_6_dual(dst, dst_stride, limits.mblim, limits.lim,
                                              limits.hev_thr, limits.mblim, limits.lim,
                                              limits.hev_thr);
                    break;
                // apply 8-tap filtering
                case 8:
                    aom_lpf_horizontal_8_dual(dst, dst_stride, limits.mblim, limits.lim,
                                              limits.hev_thr, limits.mblim, limits.lim,
                                              limits.hev_thr);
                    break;
                // apply 14-tap filtering
                case 14:
                    aom_lpf_horizontal_14_dual(dst, dst_stride, limits.mblim, limits.lim,
                                               limits.hev_thr, limits.mblim, limits.lim,
                                               limits.hev_thr);
                    break;
                // no filtering
                default: break;
            }
        }
        else
        {
            Debug.Assert(use_filter_type == USE_SINGLE);
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_horizontal_4(dst, dst_stride, limits.mblim, limits.lim,
                                         limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_horizontal_6(dst, dst_stride, limits.mblim, limits.lim,
                                         limits.hev_thr);
                    break;
                // apply 8-tap filtering
                case 8:
                    aom_lpf_horizontal_8(dst, dst_stride, limits.mblim, limits.lim,
                                         limits.hev_thr);
                    break;
                // apply 14-tap filtering
                case 14:
                    aom_lpf_horizontal_14(dst, dst_stride, limits.mblim, limits.lim,
                                          limits.hev_thr);
                    break;
                // no filtering
                default: break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void filter_horz_chroma(Ptr<byte> u_dst, Ptr<byte> v_dst,
                                           int dst_stride,
                                           in AV1_DEBLOCKING_PARAMETERS @params,
                                           SequenceHeader seq_params,
                                           int use_filter_type)
    {
        loop_filter_thresh u_limits = @params.lfthr;
        loop_filter_thresh v_limits = @params.lfthr;
        if (use_filter_type == USE_QUAD)
        {
            // Only one set of loop filter parameters (mblim, lim and hev_thr) is
            // passed as argument to quad loop filter because quad loop filter is
            // called for those cases where all the 4 set of loop filter parameters
            // are equal.
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_horizontal_4_quad(u_dst, dst_stride, u_limits.mblim,
                                              u_limits.lim, u_limits.hev_thr);
                    aom_lpf_horizontal_4_quad(v_dst, dst_stride, v_limits.mblim,
                                              v_limits.lim, v_limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_horizontal_6_quad(u_dst, dst_stride, u_limits.mblim,
                                              u_limits.lim, u_limits.hev_thr);
                    aom_lpf_horizontal_6_quad(v_dst, dst_stride, v_limits.mblim,
                                              v_limits.lim, v_limits.hev_thr);
                    break;
                case 8:
                case 14: Debug.Assert(false); break;
                // no filtering
                default: break;
            }
        }
        else if (use_filter_type == USE_DUAL)
        {
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_horizontal_4_dual(u_dst, dst_stride, u_limits.mblim,
                                              u_limits.lim, u_limits.hev_thr,
                                              u_limits.mblim, u_limits.lim,
                                              u_limits.hev_thr);
                    aom_lpf_horizontal_4_dual(v_dst, dst_stride, v_limits.mblim,
                                              v_limits.lim, v_limits.hev_thr,
                                              v_limits.mblim, v_limits.lim,
                                              v_limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_horizontal_6_dual(u_dst, dst_stride, u_limits.mblim,
                                              u_limits.lim, u_limits.hev_thr,
                                              u_limits.mblim, u_limits.lim,
                                              u_limits.hev_thr);
                    aom_lpf_horizontal_6_dual(v_dst, dst_stride, v_limits.mblim,
                                              v_limits.lim, v_limits.hev_thr,
                                              v_limits.mblim, v_limits.lim,
                                              v_limits.hev_thr);
                    break;
                case 8:
                case 14: Debug.Assert(false); break;
                // no filtering
                default: break;
            }
        }
        else
        {
            Debug.Assert(use_filter_type == USE_SINGLE);
            switch (@params.filter_length)
            {
                // apply 4-tap filtering
                case 4:
                    aom_lpf_horizontal_4(u_dst, dst_stride, u_limits.mblim, u_limits.lim,
                                         u_limits.hev_thr);
                    aom_lpf_horizontal_4(v_dst, dst_stride, v_limits.mblim, v_limits.lim,
                                         u_limits.hev_thr);
                    break;
                case 6:  // apply 6-tap filter for chroma plane only
                    aom_lpf_horizontal_6(u_dst, dst_stride, u_limits.mblim, u_limits.lim,
                                         u_limits.hev_thr);
                    aom_lpf_horizontal_6(v_dst, dst_stride, v_limits.mblim, v_limits.lim,
                                         v_limits.hev_thr);
                    break;
                case 8:
                case 14: Debug.Assert(false); break;
                // no filtering
                default: break;
            }
        }
    }

    // [subset] av1_filter_block_plane_horz: lpf_opt_level == 0 path, never executed.

    internal static void av1_filter_block_plane_horz_opt(
        AV1_COMMON cm, MACROBLOCKD xd,
        macroblockd_plane plane_ptr, uint mi_row,
        uint mi_col, Ptr<AV1_DEBLOCKING_PARAMETERS> params_buf,
        Ptr<TX_SIZE> tx_buf, int num_mis_in_lpf_unit_height_log2)
    {
        Ptr<byte> dst_ptr = plane_ptr.dst.buf;
        int dst_stride = plane_ptr.dst.stride;
        // Ensure that mi_cols/mi_rows are calculated based on frame dimension aligned
        // to MI_SIZE.
        int plane_mi_cols =
            CEIL_POWER_OF_TWO(plane_ptr.dst.width, MI_SIZE_LOG2);
        int plane_mi_rows =
            CEIL_POWER_OF_TWO(plane_ptr.dst.height, MI_SIZE_LOG2);
        int y_range = AOMMIN((int)((uint)plane_mi_rows - mi_row),
                             (1 << num_mis_in_lpf_unit_height_log2));
        int x_range = AOMMIN((int)((uint)plane_mi_cols - mi_col), MAX_MIB_SIZE);

        int mode_step = cm.mi_params.mi_stride;
        for (int x = 0; x < x_range; x++)
        {
            uint curr_x = mi_col + (uint)x;
            uint y_start = mi_row;
            uint y_end = mi_row + (uint)y_range;
            int min_block_width = block_size_high[BLOCK_128X128];
            set_lpf_parameters_for_line_luma(params_buf, tx_buf, cm, xd, HORZ_EDGE,
                                             curr_x, y_start, plane_ptr, y_end,
                                             mode_step, ref min_block_width);

            Ptr<AV1_DEBLOCKING_PARAMETERS> @params = params_buf;
            Ptr<TX_SIZE> tx_size = tx_buf;
            int filter_type = USE_SINGLE;

            Ptr<byte> p = dst_ptr + x * MI_SIZE;

            if ((x & 3) == 0 && (x + 3) < x_range && min_block_width >= 16)
            {
                // If we are on a col which is a multiple of 4, and the minimum width is
                // 16 pixels, then the current and right 3 cols must contain the same
                // prediction block. This is because dim 16 can only happen every unit of
                // 4 mi's.
                filter_type = USE_QUAD;
                x += 3;
            }
            else if ((x + 1) < x_range && min_block_width >= 8)
            {
                filter_type = USE_DUAL;
                x += 1;
            }

            for (int y = 0; y < y_range;)
            {
                if (tx_size.Value == TX_INVALID)
                {
                    @params.Value.filter_length = 0;
                    tx_size.Value = TX_4X4;
                }

                filter_horz(p, dst_stride, in @params.Value, cm.seq_params, filter_type);

                // advance the destination pointer
                int advance_units = tx_size_high_unit[tx_size.Value];
                y += advance_units;
                p += advance_units * dst_stride * MI_SIZE;
                @params += advance_units;
                tx_size += advance_units;
            }
        }
    }

    // [port] plane_ptr is a Ptr for the same reason as in av1_filter_block_plane_vert_opt_chroma.
    internal static void av1_filter_block_plane_horz_opt_chroma(
        AV1_COMMON cm, MACROBLOCKD xd,
        Ptr<macroblockd_plane> plane_ptr, uint mi_row,
        uint mi_col, Ptr<AV1_DEBLOCKING_PARAMETERS> params_buf,
        Ptr<TX_SIZE> tx_buf, int plane, bool joint_filter_chroma,
        int num_mis_in_lpf_unit_height_log2)
    {
        int scale_horz = plane_ptr.Value.subsampling_x;
        int scale_vert = plane_ptr.Value.subsampling_y;
        int dst_stride = plane_ptr.Value.dst.stride;
        // Ensure that mi_cols/mi_rows are calculated based on frame dimension aligned
        // to MI_SIZE.
        int mi_cols =
            ((plane_ptr.Value.dst.width << scale_horz) + MI_SIZE - 1) >> MI_SIZE_LOG2;
        int mi_rows =
            ((plane_ptr.Value.dst.height << scale_vert) + MI_SIZE - 1) >> MI_SIZE_LOG2;
        int plane_mi_rows = ROUND_POWER_OF_TWO(mi_rows, scale_vert);
        int plane_mi_cols = ROUND_POWER_OF_TWO(mi_cols, scale_horz);
        int y_range =
            AOMMIN((int)((uint)plane_mi_rows - (mi_row >> scale_vert)),
                   ((1 << num_mis_in_lpf_unit_height_log2) >> scale_vert));
        int x_range = AOMMIN((int)((uint)plane_mi_cols - (mi_col >> scale_horz)),
                             (MAX_MIB_SIZE >> scale_horz));
        int mode_step = cm.mi_params.mi_stride << scale_vert;
        for (int x = 0; x < x_range; x++)
        {
            uint y_start = mi_row + (uint)(0 << scale_vert);
            uint curr_x = mi_col + (uint)(x << scale_horz);
            uint y_end = mi_row + (uint)(y_range << scale_vert);
            int min_width = tx_size_wide[TX_64X64];
            set_lpf_parameters_for_line_chroma(params_buf, tx_buf, cm, xd, HORZ_EDGE,
                                               curr_x, y_start, plane_ptr.Value, y_end,
                                               mode_step, scale_horz, scale_vert,
                                               ref min_width, plane, B(joint_filter_chroma));

            Ptr<AV1_DEBLOCKING_PARAMETERS> @params = params_buf;
            Ptr<TX_SIZE> tx_size = tx_buf;
            int use_filter_type = USE_SINGLE;
            int x_inc = 0;

            if ((x & 3) == 0 && (x + 3) < x_range && min_width >= 16)
            {
                // If we are on a col which is a multiple of 4, and the minimum width is
                // 16 pixels, then the current and right 3 cols must contain the same tx
                // block. This is because dim 16 can only happen every unit of 4 mi's.
                use_filter_type = USE_QUAD;
                x_inc = 3;
            }
            else if (x % 2 == 0 && (x + 1) < x_range && min_width >= 8)
            {
                // If we are on an even col, and the minimum width is 8 pixels, then the
                // current and left cols must contain the same tx block. This is because
                // dim 4 can only happen every unit of 2**0, and 8 every unit of 2**1,
                // etc.
                use_filter_type = USE_DUAL;
                x_inc = 1;
            }

            for (int y = 0; y < y_range;)
            {
                // inner loop always filter vertical edges in a MI block. If MI size
                // is 8x8, it will first filter the vertical edge aligned with a 8x8
                // block. If 4x4 transform is used, it will then filter the internal
                // edge aligned with a 4x4 block
                if (tx_size.Value == TX_INVALID)
                {
                    @params.Value.filter_length = 0;
                    tx_size.Value = TX_4X4;
                }

                int offset = y * MI_SIZE * dst_stride + x * MI_SIZE;
                if (joint_filter_chroma)
                {
                    Ptr<byte> u_dst = plane_ptr[0].dst.buf + offset;
                    Ptr<byte> v_dst = plane_ptr[1].dst.buf + offset;
                    filter_horz_chroma(u_dst, v_dst, dst_stride, in @params.Value, cm.seq_params,
                                       use_filter_type);
                }
                else
                {
                    Ptr<byte> dst_ptr = plane_ptr.Value.dst.buf + offset;
                    filter_horz(dst_ptr, dst_stride, in @params.Value, cm.seq_params,
                                use_filter_type);
                }

                // advance the destination pointer
                int advance_units = tx_size_high_unit[tx_size.Value];
                y += advance_units;
                @params += advance_units;
                tx_size += advance_units;
            }
            x += x_inc;
        }
    }
}
