// Port of libaom av1/encoder/var_based_part.c
global using PART_EVAL_STATUS = int;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// C: typedef struct { VPVariance *part_variances; VPartVar *split[4]; } variance_node;
internal struct variance_node
{
    public VPVariance part_variances;
    public Arr4<VPartVar> split;
}

// [port] Per-thread scratch for av1_choose_var_based_partitioning(): the C stack locals
// (force_split[85], avg_16x16[4][4], ...) and the aom_malloc'd VP128x128 `vt`, reused across
// superblocks instead of being allocated per call. Every element is (re)initialized by the
// C code before it is read, exactly as the uninitialized C storage is.
internal sealed class VarBasedPartScratch
{
    public readonly VP128x128 vt = new();
    public readonly PART_EVAL_STATUS[] force_split = new PART_EVAL_STATUS[85];
    public readonly long[][] avg_16x16 = new_jagged<long>(4, 4);
    public readonly int[][] maxvar_16x16 = new_jagged<int>(4, 4);
    public readonly int[][] minvar_16x16 = new_jagged<int>(4, 4);
    public readonly int[] max_var_32x32 = new int[4];
    public readonly int[] min_var_32x32 = new int[4];
    public readonly long[] thresholds = new long[5];
    public readonly uint[] uv_sad = new uint[MAX_MB_PLANE - 1];
    public readonly scale_factors sf_no_scale = new();
}

internal static partial class var_based_part
{
    // Possible values for the force_split variable while evaluating variance based
    // partitioning.
    // Evaluate all partition types
    private const int PART_EVAL_ALL = 0;
    // Force PARTITION_SPLIT
    private const int PART_EVAL_ONLY_SPLIT = 1;
    // Force PARTITION_NONE
    private const int PART_EVAL_ONLY_NONE = 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void tree_to_node(object data, BLOCK_SIZE bsize,
                                     ref variance_node node)
    {
        node.part_variances = null;
        switch (bsize)
        {
            case BLOCK_128X128:
            {
                VP128x128 vt = (VP128x128)data;
                node.part_variances = vt.part_variances;
                // PORT-NOTE: for a 64x64 superblock vt->split (td->vt64x64) has a single
                // entry and C only forms (never dereferences) the out-of-range addresses
                // &vt->split[1..3]; such slots are left null here.
                for (int split_idx = 0; split_idx < 4; split_idx++)
                    node.split[split_idx] = split_idx < vt.split.Length
                                                ? vt.split[split_idx].part_variances.none
                                                : null;
                break;
            }
            case BLOCK_64X64:
            {
                VP64x64 vt = (VP64x64)data;
                node.part_variances = vt.part_variances;
                for (int split_idx = 0; split_idx < 4; split_idx++)
                    node.split[split_idx] = vt.split[split_idx].part_variances.none;
                break;
            }
            case BLOCK_32X32:
            {
                VP32x32 vt = (VP32x32)data;
                node.part_variances = vt.part_variances;
                for (int split_idx = 0; split_idx < 4; split_idx++)
                    node.split[split_idx] = vt.split[split_idx].part_variances.none;
                break;
            }
            case BLOCK_16X16:
            {
                VP16x16 vt = (VP16x16)data;
                node.part_variances = vt.part_variances;
                for (int split_idx = 0; split_idx < 4; split_idx++)
                    node.split[split_idx] = vt.split[split_idx].part_variances.none;
                break;
            }
            case BLOCK_8X8:
            {
                VP8x8 vt = (VP8x8)data;
                node.part_variances = vt.part_variances;
                for (int split_idx = 0; split_idx < 4; split_idx++)
                    node.split[split_idx] = vt.split[split_idx].part_variances.none;
                break;
            }
            default:
            {
                VP4x4 vt = (VP4x4)data;
                Debug.Assert(bsize == BLOCK_4X4);
                node.part_variances = vt.part_variances;
                for (int split_idx = 0; split_idx < 4; split_idx++)
                    node.split[split_idx] = vt.split[split_idx];
                break;
            }
        }
    }

    // Set variance values given sum square error, sum error, count.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fill_variance(uint s2, int s, int c, VPartVar v)
    {
        v.sum_square_error = s2;
        v.sum_error = s;
        v.log2_count = c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void get_variance(VPartVar v)
    {
        uint square_sum_error =
            (uint)(((long)v.sum_error * v.sum_error) >> v.log2_count);
        long variance =
            (long)256 * (v.sum_square_error - square_sum_error) >> v.log2_count;
        v.variance = (variance > int.MaxValue) ? int.MaxValue : (int)variance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sum_2_variances(VPartVar a, VPartVar b,
                                        VPartVar r)
    {
        Debug.Assert(a.log2_count == b.log2_count);
        fill_variance(a.sum_square_error + b.sum_square_error,
                      a.sum_error + b.sum_error, a.log2_count + 1, r);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fill_variance_tree(object data, BLOCK_SIZE bsize)
    {
        variance_node node = default;
        tree_to_node(data, bsize, ref node);
        sum_2_variances(node.split[0], node.split[1], node.part_variances.horz[0]);
        sum_2_variances(node.split[2], node.split[3], node.part_variances.horz[1]);
        sum_2_variances(node.split[0], node.split[2], node.part_variances.vert[0]);
        sum_2_variances(node.split[1], node.split[3], node.part_variances.vert[1]);
        sum_2_variances(node.part_variances.vert[0], node.part_variances.vert[1],
                        node.part_variances.none);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_block_size(AV1_COMP cpi, int mi_row, int mi_col,
                                       BLOCK_SIZE bsize)
    {
        if (cpi.common.mi_params.mi_cols > mi_col &&
            cpi.common.mi_params.mi_rows > mi_row)
        {
            CommonModeInfoParams mi_params = cpi.common.mi_params;
            int mi_grid_idx = get_mi_grid_idx(mi_params, mi_row, mi_col);
            int mi_alloc_idx = get_alloc_mi_idx(mi_params, mi_row, mi_col);
            MB_MODE_INFO mi = mi_params.mi_grid_base[mi_grid_idx] =
                mi_params.mi_alloc[mi_alloc_idx];
            mi.bsize = bsize;
        }
    }

    private static int set_vt_partitioning(AV1_COMP cpi, MACROBLOCKD xd,
                                           in TileInfo tile, object data,
                                           BLOCK_SIZE bsize, int mi_row, int mi_col,
                                           long threshold, BLOCK_SIZE bsize_min,
                                           PART_EVAL_STATUS force_split)
    {
        AV1_COMMON cm = cpi.common;
        variance_node vt = default;
        int block_width = mi_size_wide[bsize];
        int block_height = mi_size_high[bsize];
        int bs_width_check = block_width;
        int bs_height_check = block_height;
        int bs_width_vert_check = block_width >> 1;
        int bs_height_horiz_check = block_height >> 1;
        // On the right and bottom boundary we only need to check
        // if half the bsize fits, because boundary is extended
        // up to 64. So do this check only for sb_size = 64X64.
        if (cm.seq_params.sb_size == BLOCK_64X64)
        {
            if (tile.mi_col_end == cm.mi_params.mi_cols)
            {
                bs_width_check = (block_width >> 1) + 1;
                bs_width_vert_check = (block_width >> 2) + 1;
            }
            if (tile.mi_row_end == cm.mi_params.mi_rows)
            {
                bs_height_check = (block_height >> 1) + 1;
                bs_height_horiz_check = (block_height >> 2) + 1;
            }
        }

        Debug.Assert(block_height == block_width);
        tree_to_node(data, bsize, ref vt);

        if (mi_col + bs_width_check <= tile.mi_col_end &&
            mi_row + bs_height_check <= tile.mi_row_end &&
            force_split == PART_EVAL_ONLY_NONE)
        {
            set_block_size(cpi, mi_row, mi_col, bsize);
            return 1;
        }
        if (force_split == PART_EVAL_ONLY_SPLIT) return 0;

        // For bsize=bsize_min (16x16/8x8 for 8x8/4x4 downsampling), select if
        // variance is below threshold, otherwise split will be selected.
        // No check for vert/horiz split as too few samples for variance.
        if (bsize == bsize_min)
        {
            // Variance already computed to set the force_split.
            if (frame_is_intra_only(cm) != 0) get_variance(vt.part_variances.none);
            if (mi_col + bs_width_check <= tile.mi_col_end &&
                mi_row + bs_height_check <= tile.mi_row_end &&
                vt.part_variances.none.variance < threshold)
            {
                set_block_size(cpi, mi_row, mi_col, bsize);
                return 1;
            }
            return 0;
        }
        else if (bsize > bsize_min)
        {
            // Variance already computed to set the force_split.
            if (frame_is_intra_only(cm) != 0) get_variance(vt.part_variances.none);
            // For key frame: take split for bsize above 32X32 or very high variance.
            if (frame_is_intra_only(cm) != 0 &&
                (bsize > BLOCK_32X32 ||
                 vt.part_variances.none.variance > (threshold << 4)))
            {
                return 0;
            }
            // If variance is low, take the bsize (no split).
            if (mi_col + bs_width_check <= tile.mi_col_end &&
                mi_row + bs_height_check <= tile.mi_row_end &&
                vt.part_variances.none.variance < threshold)
            {
                set_block_size(cpi, mi_row, mi_col, bsize);
                return 1;
            }
            // Check vertical split.
            if (mi_row + bs_height_check <= tile.mi_row_end &&
                mi_col + bs_width_vert_check <= tile.mi_col_end)
            {
                BLOCK_SIZE subsize = get_partition_subsize(bsize, PARTITION_VERT);
                BLOCK_SIZE plane_bsize =
                    get_plane_block_size(subsize, xd.plane[AOM_PLANE_U].subsampling_x,
                                         xd.plane[AOM_PLANE_U].subsampling_y);
                get_variance(vt.part_variances.vert[0]);
                get_variance(vt.part_variances.vert[1]);
                if (vt.part_variances.vert[0].variance < threshold &&
                    vt.part_variances.vert[1].variance < threshold &&
                    plane_bsize < BLOCK_INVALID)
                {
                    set_block_size(cpi, mi_row, mi_col, subsize);
                    set_block_size(cpi, mi_row, mi_col + block_width / 2, subsize);
                    return 1;
                }
            }
            // Check horizontal split.
            if (mi_col + bs_width_check <= tile.mi_col_end &&
                mi_row + bs_height_horiz_check <= tile.mi_row_end)
            {
                BLOCK_SIZE subsize = get_partition_subsize(bsize, PARTITION_HORZ);
                BLOCK_SIZE plane_bsize =
                    get_plane_block_size(subsize, xd.plane[AOM_PLANE_U].subsampling_x,
                                         xd.plane[AOM_PLANE_U].subsampling_y);
                get_variance(vt.part_variances.horz[0]);
                get_variance(vt.part_variances.horz[1]);
                if (vt.part_variances.horz[0].variance < threshold &&
                    vt.part_variances.horz[1].variance < threshold &&
                    plane_bsize < BLOCK_INVALID)
                {
                    set_block_size(cpi, mi_row, mi_col, subsize);
                    set_block_size(cpi, mi_row + block_height / 2, mi_col, subsize);
                    return 1;
                }
            }
            return 0;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int all_blks_inside(int x16_idx, int y16_idx, int pixels_wide,
                                       int pixels_high)
    {
        int all_inside = 1;
        for (int idx = 0; idx < 4; idx++)
        {
            all_inside &= B((x16_idx + GET_BLK_IDX_X(idx, 3)) < pixels_wide);
            all_inside &= B((y16_idx + GET_BLK_IDX_Y(idx, 3)) < pixels_high);
        }
        return all_inside;
    }

    // [subset] fill_variance_8x8avg_highbd: CONFIG_AV1_HIGHBITDEPTH is 0.

    // [port] scratch for aom_avg_8x8_quad()'s output (C: int src_avg[4], dst_avg[4]).
    [ThreadStatic] private static int[] s_src_avg;
    [ThreadStatic] private static int[] s_dst_avg;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fill_variance_8x8avg_lowbd(
        Ptr<byte> src_buf, int src_stride, Ptr<byte> dst_buf,
        int dst_stride, int x16_idx, int y16_idx, VP16x16 vst, int pixels_wide,
        int pixels_high)
    {
        Span<uint> sse = stackalloc uint[4];
        Span<int> sum = stackalloc int[4];
        sse.Clear();
        sum.Clear();

        if (all_blks_inside(x16_idx, y16_idx, pixels_wide, pixels_high) != 0)
        {
            int[] src_avg = s_src_avg ??= new int[4];
            int[] dst_avg = s_dst_avg ??= new int[4];
            aom_avg_8x8_quad(src_buf, src_stride, x16_idx, y16_idx, src_avg);
            aom_avg_8x8_quad(dst_buf, dst_stride, x16_idx, y16_idx, dst_avg);
            for (int idx = 0; idx < 4; idx++)
            {
                sum[idx] = src_avg[idx] - dst_avg[idx];
                sse[idx] = (uint)(sum[idx] * sum[idx]);
            }
        }
        else
        {
            for (int idx = 0; idx < 4; idx++)
            {
                int x8_idx = x16_idx + GET_BLK_IDX_X(idx, 3);
                int y8_idx = y16_idx + GET_BLK_IDX_Y(idx, 3);
                if (x8_idx < pixels_wide && y8_idx < pixels_high)
                {
                    int src_avg =
                        (int)aom_avg_8x8(src_buf + y8_idx * src_stride + x8_idx, src_stride);
                    int dst_avg =
                        (int)aom_avg_8x8(dst_buf + y8_idx * dst_stride + x8_idx, dst_stride);
                    sum[idx] = src_avg - dst_avg;
                    sse[idx] = (uint)(sum[idx] * sum[idx]);
                }
            }
        }

        for (int idx = 0; idx < 4; idx++)
        {
            fill_variance(sse[idx], sum[idx], 0, vst.split[idx].part_variances.none);
        }
    }

    // Obtain parameters required to calculate variance (such as sum, sse, etc,.)
    // at 8x8 sub-block level for a given 16x16 block.
    // The function can be called only when is_key_frame is false since sum is
    // computed between source and reference frames.
    // [subset] the highbd_flag parameter is dropped (CONFIG_AV1_HIGHBITDEPTH is 0).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fill_variance_8x8avg(Ptr<byte> src_buf, int src_stride,
                                             Ptr<byte> dst_buf, int dst_stride,
                                             int x16_idx, int y16_idx, VP16x16 vst,
                                             int pixels_wide,
                                             int pixels_high)
    {
        fill_variance_8x8avg_lowbd(src_buf, src_stride, dst_buf, dst_stride, x16_idx,
                                   y16_idx, vst, pixels_wide, pixels_high);
    }

    // [subset] compute_minmax_8x8: only called when compute_minmax_variance is set, which
    // fill_variance_tree_leaves() hardcodes to 0.

    // Function to compute average and variance of 4x4 sub-block.
    // The function can be called only when is_key_frame is true since sum is
    // computed using source frame only.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fill_variance_4x4avg(Ptr<byte> src_buf, int src_stride,
                                             int x8_idx, int y8_idx, VP8x8 vst,
                                             int pixels_wide, int pixels_high,
                                             int border_offset_4x4)
    {
        for (int idx = 0; idx < 4; idx++)
        {
            int x4_idx = x8_idx + GET_BLK_IDX_X(idx, 2);
            int y4_idx = y8_idx + GET_BLK_IDX_Y(idx, 2);
            uint sse = 0;
            int sum = 0;
            if (x4_idx < pixels_wide - border_offset_4x4 &&
                y4_idx < pixels_high - border_offset_4x4)
            {
                int src_avg;
                int dst_avg = 128;
                src_avg = (int)aom_avg_4x4(src_buf + y4_idx * src_stride + x4_idx, src_stride);

                sum = src_avg - dst_avg;
                sse = (uint)(sum * sum);
            }
            fill_variance(sse, sum, 0, vst.split[idx].part_variances.none);
        }
    }

    private static long scale_part_thresh_content(long threshold_base, int speed,
                                                  int non_reference_frame,
                                                  int is_static)
    {
        long threshold = threshold_base;
        if (non_reference_frame != 0 && is_static == 0) threshold = (3 * threshold) >> 1;
        if (speed >= 8)
        {
            return (5 * threshold) >> 2;
        }
        return threshold;
    }

    private static readonly int[][] qindex_thr = {
        new[] { 200, 220 }, new[] { 140, 170 }, new[] { 120, 150 }, new[] { 200, 210 }, new[] { 170, 220 },
    };

    // Tune thresholds less or more aggressively to prefer larger partitions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void tune_thresh_based_on_qindex(
        AV1_COMP cpi, long[] thresholds, ulong block_sad, int current_qindex,
        int num_pixels, bool is_segment_id_boosted, int source_sad_nonrd,
        int lighting_change)
    {
        double weight;
        if (cpi.sf.rt_sf.prefer_large_partition_blocks >= 3)
        {
            const int win = 20;
            if (current_qindex < QINDEX_LARGE_BLOCK_THR - win)
                weight = 1.0;
            else if (current_qindex > QINDEX_LARGE_BLOCK_THR + win)
                weight = 0.0;
            else
                weight =
                    1.0 - (current_qindex - QINDEX_LARGE_BLOCK_THR + win) / (2 * win);
            if (num_pixels > RESOLUTION_480P)
            {
                for (int i = 0; i < 4; i++)
                {
                    thresholds[i] <<= 1;
                }
            }
            if (num_pixels <= RESOLUTION_288P)
            {
                thresholds[3] = long.MaxValue;
                if (is_segment_id_boosted == false)
                {
                    thresholds[1] <<= 2;
                    thresholds[2] <<= (source_sad_nonrd <= kLowSad) ? 5 : 4;
                }
                else
                {
                    thresholds[1] <<= 1;
                    thresholds[2] <<= 3;
                }
                // Allow for split to 8x8 for superblocks where part of it has
                // moving boundary. So allow for sb with source_sad above threshold,
                // and avoid very large source_sad or high source content, to avoid
                // too many 8x8 within superblock.
                ulong avg_source_sad_thresh = 25000;
                ulong block_sad_low = 25000;
                ulong block_sad_high = 50000;
                if (cpi.svc.temporal_layer_id == 0 &&
                    cpi.svc.number_temporal_layers > 1)
                {
                    // Increase the sad thresholds for base TL0, as reference/LAST is
                    // 2/4 frames behind (for 2/3 #TL).
                    avg_source_sad_thresh = 40000;
                    block_sad_high = 70000;
                }
                if (is_segment_id_boosted == false &&
                    cpi.rc.avg_source_sad < avg_source_sad_thresh &&
                    block_sad > block_sad_low && block_sad < block_sad_high &&
                    lighting_change == 0)
                {
                    thresholds[2] = (3 * thresholds[2]) >> 2;
                    thresholds[3] = thresholds[2] << 3;
                }
                // Condition the increase of partition thresholds on the segment
                // and the content. Avoid the increase for superblocks which have
                // high source sad, unless the whole frame has very high motion
                // (i.e, cpi->rc.avg_source_sad is very large, in which case all blocks
                // have high source sad).
            }
            else if (num_pixels > RESOLUTION_480P && is_segment_id_boosted == false &&
                     (source_sad_nonrd != kHighSad ||
                      cpi.rc.avg_source_sad > 50000))
            {
                thresholds[0] = (3 * thresholds[0]) >> 1;
                thresholds[3] = long.MaxValue;
                if (current_qindex > QINDEX_LARGE_BLOCK_THR)
                {
                    thresholds[1] =
                        (int)((1 - weight) * (thresholds[1] << 1) + weight * thresholds[1]);
                    thresholds[2] =
                        (int)((1 - weight) * (thresholds[2] << 1) + weight * thresholds[2]);
                }
            }
            else if (current_qindex > QINDEX_LARGE_BLOCK_THR &&
                     is_segment_id_boosted == false &&
                     (source_sad_nonrd != kHighSad ||
                      cpi.rc.avg_source_sad > 50000))
            {
                thresholds[1] =
                    (int)((1 - weight) * (thresholds[1] << 2) + weight * thresholds[1]);
                thresholds[2] =
                    (int)((1 - weight) * (thresholds[2] << 4) + weight * thresholds[2]);
                thresholds[3] = long.MaxValue;
            }
        }
        else if (cpi.sf.rt_sf.prefer_large_partition_blocks >= 2)
        {
            thresholds[1] <<= (source_sad_nonrd <= kLowSad) ? 2 : 0;
            thresholds[2] =
                (source_sad_nonrd <= kLowSad) ? (3 * thresholds[2]) : thresholds[2];
        }
        else if (cpi.sf.rt_sf.prefer_large_partition_blocks >= 1)
        {
            int fac = (source_sad_nonrd <= kLowSad) ? 2 : 1;
            if (current_qindex < QINDEX_LARGE_BLOCK_THR - 45)
                weight = 1.0;
            else if (current_qindex > QINDEX_LARGE_BLOCK_THR + 45)
                weight = 0.0;
            else
                weight = 1.0 - (current_qindex - QINDEX_LARGE_BLOCK_THR + 45) / (2 * 45);
            thresholds[1] =
                (int)((1 - weight) * (thresholds[1] << 1) + weight * thresholds[1]);
            thresholds[2] =
                (int)((1 - weight) * (thresholds[2] << 1) + weight * thresholds[2]);
            thresholds[3] =
                (int)((1 - weight) * (thresholds[3] << fac) + weight * thresholds[3]);
        }
        if (cpi.sf.part_sf.disable_8x8_part_based_on_qidx != 0 && (current_qindex < 128))
            thresholds[3] = long.MaxValue;
    }

    private static void set_vbp_thresholds_key_frame(AV1_COMP cpi, long[] thresholds,
                                                     long threshold_base,
                                                     int threshold_left_shift,
                                                     int num_pixels)
    {
        if (cpi.sf.rt_sf.force_large_partition_blocks_intra != 0)
        {
            int shift_steps =
                threshold_left_shift - (cpi.oxcf.mode == ALLINTRA ? 7 : 8);
            Debug.Assert(shift_steps >= 0);
            threshold_base <<= shift_steps;
        }
        thresholds[0] = threshold_base;
        thresholds[1] = threshold_base;
        if (num_pixels < RESOLUTION_720P)
        {
            thresholds[2] = threshold_base / 3;
            thresholds[3] = threshold_base >> 1;
        }
        else
        {
            int shift_val = 2;
            if (cpi.sf.rt_sf.force_large_partition_blocks_intra != 0)
            {
                shift_val = (cpi.oxcf.mode == ALLINTRA ? 1 : 0);
            }

            thresholds[2] = threshold_base >> shift_val;
            thresholds[3] = threshold_base >> shift_val;
        }
        thresholds[4] = threshold_base << 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void tune_thresh_based_on_resolution(
        AV1_COMP cpi, long[] thresholds, long threshold_base,
        int current_qindex, int source_sad_rd, int num_pixels)
    {
        if (num_pixels >= RESOLUTION_720P) thresholds[3] = thresholds[3] << 1;
        if (num_pixels <= RESOLUTION_288P)
        {
            int th_idx = 0;
            if (cpi.sf.rt_sf.var_part_based_on_qidx >= 1)
                th_idx =
                    (source_sad_rd <= kLowSad) ? cpi.sf.rt_sf.var_part_based_on_qidx : 0;
            if (cpi.sf.rt_sf.var_part_based_on_qidx >= 3)
                th_idx = cpi.sf.rt_sf.var_part_based_on_qidx;
            int qindex_low_thr = qindex_thr[th_idx][0];
            int qindex_high_thr = qindex_thr[th_idx][1];
            if (current_qindex >= qindex_high_thr)
            {
                threshold_base = (5 * threshold_base) >> 1;
                thresholds[1] = threshold_base >> 3;
                thresholds[2] = threshold_base << 2;
                thresholds[3] = threshold_base << 5;
            }
            else if (current_qindex < qindex_low_thr)
            {
                thresholds[1] = threshold_base >> 3;
                thresholds[2] = threshold_base >> 1;
                thresholds[3] = threshold_base << 3;
            }
            else
            {
                long qi_diff_low = current_qindex - qindex_low_thr;
                long qi_diff_high = qindex_high_thr - current_qindex;
                long threshold_diff = qindex_high_thr - qindex_low_thr;
                long threshold_base_high = (5 * threshold_base) >> 1;

                threshold_diff = threshold_diff > 0 ? threshold_diff : 1;
                threshold_base =
                    (qi_diff_low * threshold_base_high + qi_diff_high * threshold_base) /
                    threshold_diff;
                thresholds[1] = threshold_base >> 3;
                thresholds[2] = ((qi_diff_low * threshold_base) +
                                 qi_diff_high * (threshold_base >> 1)) /
                                threshold_diff;
                thresholds[3] = ((qi_diff_low * (threshold_base << 5)) +
                                 qi_diff_high * (threshold_base << 3)) /
                                threshold_diff;
            }
        }
        else if (num_pixels < RESOLUTION_720P)
        {
            thresholds[2] = (5 * threshold_base) >> 2;
        }
        else if (num_pixels < RESOLUTION_1080P)
        {
            thresholds[2] = threshold_base << 1;
        }
        else
        {
            // num_pixels >= RESOLUTION_1080P
            if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN)
            {
                if (num_pixels < RESOLUTION_1440P)
                {
                    thresholds[2] = (5 * threshold_base) >> 1;
                }
                else
                {
                    thresholds[2] = (7 * threshold_base) >> 1;
                }
            }
            else
            {
                if (cpi.oxcf.speed > 7)
                {
                    thresholds[2] = 6 * threshold_base;
                }
                else
                {
                    thresholds[2] = 3 * threshold_base;
                }
            }
        }
    }

    // Increase the base partition threshold, based on content and noise level.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long tune_base_thresh_content(AV1_COMP cpi,
                                                 long threshold_base,
                                                 int content_lowsumdiff,
                                                 int source_sad_nonrd,
                                                 int num_pixels)
    {
        AV1_COMMON cm = cpi.common;
        long updated_thresh_base = threshold_base;
        if (cpi.noise_estimate.enabled != 0 && content_lowsumdiff != 0 &&
            num_pixels > RESOLUTION_480P && cm.current_frame.frame_number > 60)
        {
            NOISE_LEVEL noise_level =
                av1_noise_estimate_extract_level(cpi.noise_estimate);
            if (noise_level == kHigh)
                updated_thresh_base = (5 * updated_thresh_base) >> 1;
            else if (noise_level == kMedium &&
                     cpi.sf.rt_sf.prefer_large_partition_blocks == 0)
                updated_thresh_base = (5 * updated_thresh_base) >> 2;
        }
        updated_thresh_base = scale_part_thresh_content(
            updated_thresh_base, cpi.oxcf.speed,
            cpi.ppi.rtc_ref.non_reference_frame, B(cpi.rc.frame_source_sad == 0));
        if (cpi.oxcf.speed >= 11 && source_sad_nonrd > kLowSad &&
            cpi.rc.high_motion_content_screen_rtc != 0)
            updated_thresh_base = updated_thresh_base << 4;
        return updated_thresh_base;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_vbp_thresholds(AV1_COMP cpi, long[] thresholds,
                                           ulong blk_sad, int qindex,
                                           int content_lowsumdiff,
                                           int source_sad_nonrd, int source_sad_rd,
                                           bool is_segment_id_boosted,
                                           int lighting_change)
    {
        AV1_COMMON cm = cpi.common;
        int is_key_frame = frame_is_intra_only(cm);
        int threshold_multiplier = is_key_frame != 0 ? 120 : 1;
        int ac_q = av1_ac_quant_QTX(qindex, 0, cm.seq_params.bit_depth);
        long threshold_base = (long)(threshold_multiplier * ac_q);
        int current_qindex = cm.quant_params.base_qindex;
        int threshold_left_shift = cpi.sf.rt_sf.var_part_split_threshold_shift;
        int num_pixels = cm.width * cm.height;

        if (is_key_frame != 0)
        {
            set_vbp_thresholds_key_frame(cpi, thresholds, threshold_base,
                                         threshold_left_shift, num_pixels);
            return;
        }

        threshold_base = tune_base_thresh_content(
            cpi, threshold_base, content_lowsumdiff, source_sad_nonrd, num_pixels);
        thresholds[0] = threshold_base >> 1;
        thresholds[1] = threshold_base;
        thresholds[3] = threshold_base << threshold_left_shift;

        tune_thresh_based_on_resolution(cpi, thresholds, threshold_base,
                                        current_qindex, source_sad_rd, num_pixels);

        tune_thresh_based_on_qindex(cpi, thresholds, blk_sad, current_qindex,
                                    num_pixels, is_segment_id_boosted,
                                    source_sad_nonrd, lighting_change);
    }

    private static readonly int[][] idx_64x64 = {
        new[] { 0, 0 }, new[] { 0, 8 }, new[] { 8, 0 }, new[] { 8, 8 }
    };

    // Set temporal variance low flag for superblock 64x64.
    // Only first 25 in the array are used in this case.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_low_temp_var_flag_64x64(CommonModeInfoParams mi_params,
                                                    PartitionSearchInfo part_info,
                                                    MACROBLOCKD xd, VP64x64 vt,
                                                    long[] thresholds,
                                                    int mi_col, int mi_row)
    {
        if (xd.mi[0].bsize == BLOCK_64X64)
        {
            if ((vt.part_variances).none.variance < (thresholds[0] >> 1))
                part_info.variance_low[0] = 1;
        }
        else if (xd.mi[0].bsize == BLOCK_64X32)
        {
            for (int part_idx = 0; part_idx < 2; part_idx++)
            {
                if (vt.part_variances.horz[part_idx].variance < (thresholds[0] >> 2))
                    part_info.variance_low[part_idx + 1] = 1;
            }
        }
        else if (xd.mi[0].bsize == BLOCK_32X64)
        {
            for (int part_idx = 0; part_idx < 2; part_idx++)
            {
                if (vt.part_variances.vert[part_idx].variance < (thresholds[0] >> 2))
                    part_info.variance_low[part_idx + 3] = 1;
            }
        }
        else
        {
            int[][] idx = idx_64x64; // C: static const int idx[4][2]
            for (int lvl1_idx = 0; lvl1_idx < 4; lvl1_idx++)
            {
                int idx_str = mi_params.mi_stride * (mi_row + idx[lvl1_idx][0]) +
                              mi_col + idx[lvl1_idx][1];
                // C: MB_MODE_INFO **this_mi = mi_params->mi_grid_base + idx_str;
                // (only dereferenced after the bounds check below)

                if (mi_params.mi_cols <= mi_col + idx[lvl1_idx][1] ||
                    mi_params.mi_rows <= mi_row + idx[lvl1_idx][0])
                    continue;

                MB_MODE_INFO this_mi = mi_params.mi_grid_base[idx_str];
                if (this_mi == null) continue;

                if (this_mi.bsize == BLOCK_32X32)
                {
                    long threshold_32x32 = (5 * thresholds[1]) >> 3;
                    if (vt.split[lvl1_idx].part_variances.none.variance < threshold_32x32)
                        part_info.variance_low[lvl1_idx + 5] = 1;
                }
                else
                {
                    // For 32x16 and 16x32 blocks, the flag is set on each 16x16 block
                    // inside.
                    if (this_mi.bsize == BLOCK_16X16 ||
                        this_mi.bsize == BLOCK_32X16 ||
                        this_mi.bsize == BLOCK_16X32)
                    {
                        for (int lvl2_idx = 0; lvl2_idx < 4; lvl2_idx++)
                        {
                            if (vt.split[lvl1_idx]
                                    .split[lvl2_idx]
                                    .part_variances.none.variance < (thresholds[2] >> 8))
                                part_info.variance_low[(lvl1_idx << 2) + lvl2_idx + 9] = 1;
                        }
                    }
                }
            }
        }
    }

    private static readonly int[][] idx64 = {
        new[] { 0, 0 }, new[] { 0, 16 }, new[] { 16, 0 }, new[] { 16, 16 }
    };
    private static readonly int[][] idx32 = {
        new[] { 0, 0 }, new[] { 0, 8 }, new[] { 8, 0 }, new[] { 8, 8 }
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_low_temp_var_flag_128x128(
        CommonModeInfoParams mi_params, PartitionSearchInfo part_info,
        MACROBLOCKD xd, VP128x128 vt, long[] thresholds, int mi_col,
        int mi_row)
    {
        if (xd.mi[0].bsize == BLOCK_128X128)
        {
            if (vt.part_variances.none.variance < (thresholds[0] >> 1))
                part_info.variance_low[0] = 1;
        }
        else if (xd.mi[0].bsize == BLOCK_128X64)
        {
            for (int part_idx = 0; part_idx < 2; part_idx++)
            {
                if (vt.part_variances.horz[part_idx].variance < (thresholds[0] >> 2))
                    part_info.variance_low[part_idx + 1] = 1;
            }
        }
        else if (xd.mi[0].bsize == BLOCK_64X128)
        {
            for (int part_idx = 0; part_idx < 2; part_idx++)
            {
                if (vt.part_variances.vert[part_idx].variance < (thresholds[0] >> 2))
                    part_info.variance_low[part_idx + 3] = 1;
            }
        }
        else
        {
            for (int lvl1_idx = 0; lvl1_idx < 4; lvl1_idx++)
            {
                int idx_str = mi_params.mi_stride * (mi_row + idx64[lvl1_idx][0]) +
                              mi_col + idx64[lvl1_idx][1];
                // C: MB_MODE_INFO **mi_64 = mi_params->mi_grid_base + idx_str;
                // PORT-NOTE: C reads *mi_64 before the frame-bounds check; mi_grid_base is
                // allocated for whole superblocks (calc_mi_size), so the read is in range.
                MB_MODE_INFO mi_64 = mi_params.mi_grid_base[idx_str];
                if (mi_64 == null) continue;
                if (mi_params.mi_cols <= mi_col + idx64[lvl1_idx][1] ||
                    mi_params.mi_rows <= mi_row + idx64[lvl1_idx][0])
                    continue;
                long threshold_64x64 = (5 * thresholds[1]) >> 3;
                if (mi_64.bsize == BLOCK_64X64)
                {
                    if (vt.split[lvl1_idx].part_variances.none.variance < threshold_64x64)
                        part_info.variance_low[5 + lvl1_idx] = 1;
                }
                else if (mi_64.bsize == BLOCK_64X32)
                {
                    for (int part_idx = 0; part_idx < 2; part_idx++)
                        if (vt.split[lvl1_idx].part_variances.horz[part_idx].variance <
                            (threshold_64x64 >> 1))
                            part_info.variance_low[9 + (lvl1_idx << 1) + part_idx] = 1;
                }
                else if (mi_64.bsize == BLOCK_32X64)
                {
                    for (int part_idx = 0; part_idx < 2; part_idx++)
                        if (vt.split[lvl1_idx].part_variances.vert[part_idx].variance <
                            (threshold_64x64 >> 1))
                            part_info.variance_low[17 + (lvl1_idx << 1) + part_idx] = 1;
                }
                else
                {
                    for (int lvl2_idx = 0; lvl2_idx < 4; lvl2_idx++)
                    {
                        int idx_str1 =
                            mi_params.mi_stride * idx32[lvl2_idx][0] + idx32[lvl2_idx][1];
                        MB_MODE_INFO mi_32 = mi_params.mi_grid_base[idx_str + idx_str1];
                        if (mi_32 == null) continue;

                        if (mi_params.mi_cols <=
                                mi_col + idx64[lvl1_idx][1] + idx32[lvl2_idx][1] ||
                            mi_params.mi_rows <=
                                mi_row + idx64[lvl1_idx][0] + idx32[lvl2_idx][0])
                            continue;
                        long threshold_32x32 = (5 * thresholds[2]) >> 3;
                        if (mi_32.bsize == BLOCK_32X32)
                        {
                            if (vt.split[lvl1_idx]
                                    .split[lvl2_idx]
                                    .part_variances.none.variance < threshold_32x32)
                                part_info.variance_low[25 + (lvl1_idx << 2) + lvl2_idx] = 1;
                        }
                        else
                        {
                            // For 32x16 and 16x32 blocks, the flag is set on each 16x16 block
                            // inside.
                            if (mi_32.bsize == BLOCK_16X16 ||
                                mi_32.bsize == BLOCK_32X16 ||
                                mi_32.bsize == BLOCK_16X32)
                            {
                                for (int lvl3_idx = 0; lvl3_idx < 4; lvl3_idx++)
                                {
                                    VPartVar none_var = vt.split[lvl1_idx]
                                                            .split[lvl2_idx]
                                                            .split[lvl3_idx]
                                                            .part_variances.none;
                                    if (none_var.variance < (thresholds[3] >> 8))
                                        part_info.variance_low[41 + (lvl1_idx << 4) +
                                                               (lvl2_idx << 2) + lvl3_idx] = 1;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_low_temp_var_flag(
        AV1_COMP cpi, PartitionSearchInfo part_info, MACROBLOCKD xd,
        VP128x128 vt, long[] thresholds, MV_REFERENCE_FRAME ref_frame_partition,
        int mi_col, int mi_row, bool is_small_sb)
    {
        AV1_COMMON cm = cpi.common;
        // Check temporal variance for bsize >= 16x16, if LAST_FRAME was selected.
        // If the temporal variance is small set the flag
        // variance_low for the block. The variance threshold can be adjusted, the
        // higher the more aggressive.
        if (ref_frame_partition == LAST_FRAME)
        {
            if (is_small_sb)
                set_low_temp_var_flag_64x64(cm.mi_params, part_info, xd,
                                            (vt.split[0]), thresholds, mi_col, mi_row);
            else
                set_low_temp_var_flag_128x128(cm.mi_params, part_info, xd, vt,
                                              thresholds, mi_col, mi_row);
        }
    }

    private static readonly int[][] pos_shift_16x16 = {
        new[] { 9, 10, 13, 14 }, new[] { 11, 12, 15, 16 }, new[] { 17, 18, 21, 22 }, new[] { 19, 20, 23, 24 }
    };

    internal static int av1_get_force_skip_low_temp_var_small_sb(Ptr<byte> variance_low,
                                                                 int mi_row, int mi_col,
                                                                 BLOCK_SIZE bsize)
    {
        // Relative indices of MB inside the superblock.
        int mi_x = mi_row & 0xF;
        int mi_y = mi_col & 0xF;
        // Relative indices of 16x16 block inside the superblock.
        int i = mi_x >> 2;
        int j = mi_y >> 2;
        int force_skip_low_temp_var = 0;
        // Set force_skip_low_temp_var based on the block size and block offset.
        switch (bsize)
        {
            case BLOCK_64X64: force_skip_low_temp_var = variance_low[0]; break;
            case BLOCK_64X32:
                if (mi_y == 0 && mi_x == 0)
                {
                    force_skip_low_temp_var = variance_low[1];
                }
                else if (mi_y == 0 && mi_x != 0)
                {
                    force_skip_low_temp_var = variance_low[2];
                }
                break;
            case BLOCK_32X64:
                if (mi_y == 0 && mi_x == 0)
                {
                    force_skip_low_temp_var = variance_low[3];
                }
                else if (mi_y != 0 && mi_x == 0)
                {
                    force_skip_low_temp_var = variance_low[4];
                }
                break;
            case BLOCK_32X32:
                if (mi_y == 0 && mi_x == 0)
                {
                    force_skip_low_temp_var = variance_low[5];
                }
                else if (mi_y != 0 && mi_x == 0)
                {
                    force_skip_low_temp_var = variance_low[6];
                }
                else if (mi_y == 0 && mi_x != 0)
                {
                    force_skip_low_temp_var = variance_low[7];
                }
                else if (mi_y != 0 && mi_x != 0)
                {
                    force_skip_low_temp_var = variance_low[8];
                }
                break;
            case BLOCK_32X16:
            case BLOCK_16X32:
            case BLOCK_16X16:
                force_skip_low_temp_var = variance_low[pos_shift_16x16[i][j]];
                break;
            default: break;
        }

        return force_skip_low_temp_var;
    }

    internal static int av1_get_force_skip_low_temp_var(Ptr<byte> variance_low, int mi_row,
                                                        int mi_col, BLOCK_SIZE bsize)
    {
        int force_skip_low_temp_var = 0;
        int x, y;
        x = (mi_col & 0x1F) >> 4;
        // y = (mi_row & 0x1F) >> 4;
        // const int idx64 = (y << 1) + x;
        y = (mi_row & 0x17) >> 3;
        int idx64 = y + x;

        x = (mi_col & 0xF) >> 3;
        // y = (mi_row & 0xF) >> 3;
        // const int idx32 = (y << 1) + x;
        y = (mi_row & 0xB) >> 2;
        int idx32 = y + x;

        x = (mi_col & 0x7) >> 2;
        // y = (mi_row & 0x7) >> 2;
        // const int idx16 = (y << 1) + x;
        y = (mi_row & 0x5) >> 1;
        int idx16 = y + x;
        // Set force_skip_low_temp_var based on the block size and block offset.
        switch (bsize)
        {
            case BLOCK_128X128: force_skip_low_temp_var = variance_low[0]; break;
            case BLOCK_128X64:
                Debug.Assert((mi_col & 0x1F) == 0);
                force_skip_low_temp_var = variance_low[1 + B((mi_row & 0x1F) != 0)];
                break;
            case BLOCK_64X128:
                Debug.Assert((mi_row & 0x1F) == 0);
                force_skip_low_temp_var = variance_low[3 + B((mi_col & 0x1F) != 0)];
                break;
            case BLOCK_64X64:
                // Location of this 64x64 block inside the 128x128 superblock
                force_skip_low_temp_var = variance_low[5 + idx64];
                break;
            case BLOCK_64X32:
            {
                x = (mi_col & 0x1F) >> 4;
                y = (mi_row & 0x1F) >> 3;
                /*
                .---------------.---------------.
                | x=0,y=0,idx=0 | x=0,y=0,idx=2 |
                :---------------+---------------:
                | x=0,y=1,idx=1 | x=1,y=1,idx=3 |
                :---------------+---------------:
                | x=0,y=2,idx=4 | x=1,y=2,idx=6 |
                :---------------+---------------:
                | x=0,y=3,idx=5 | x=1,y=3,idx=7 |
                '---------------'---------------'
                */
                int idx64x32 = (x << 1) + (y % 2) + ((y >> 1) << 2);
                force_skip_low_temp_var = variance_low[9 + idx64x32];
                break;
            }
            case BLOCK_32X64:
            {
                x = (mi_col & 0x1F) >> 3;
                y = (mi_row & 0x1F) >> 4;
                int idx32x64 = (y << 2) + x;
                force_skip_low_temp_var = variance_low[17 + idx32x64];
                break;
            }
            case BLOCK_32X32:
                force_skip_low_temp_var = variance_low[25 + (idx64 << 2) + idx32];
                break;
            case BLOCK_32X16:
            case BLOCK_16X32:
            case BLOCK_16X16:
                force_skip_low_temp_var =
                    variance_low[41 + (idx64 << 4) + (idx32 << 2) + idx16];
                break;
            default: break;
        }
        return force_skip_low_temp_var;
    }

    internal static void av1_set_variance_partition_thresholds(AV1_COMP cpi, int qindex,
                                                              int content_lowsumdiff)
    {
        SPEED_FEATURES sf = cpi.sf;
        if (sf.part_sf.partition_search_type != VAR_BASED_PARTITION)
        {
            return;
        }
        else
        {
            set_vbp_thresholds(cpi, cpi.vbp_info.thresholds, 0, qindex,
                               content_lowsumdiff, 0, 0, false, 0);
            // The threshold below is not changed locally.
            cpi.vbp_info.threshold_minmax = 15 + (qindex >> 3);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void chroma_check(AV1_COMP cpi, MACROBLOCK x, BLOCK_SIZE bsize,
                                     uint y_sad, uint y_sad_g,
                                     uint y_sad_alt, bool is_key_frame,
                                     bool zero_motion, uint[] uv_sad)
    {
        MACROBLOCKD xd = x.e_mbd;
        int source_sad_nonrd = x.content_state_sb.source_sad_nonrd;
        int shift_upper_limit = 1;
        int shift_lower_limit = 3;
        int fac_uv = 6;
        if (is_key_frame || cpi.oxcf.tool_cfg.enable_monochrome) return;

        // Use lower threshold (more conservative in setting color flag) for
        // higher resolutions non-screen, which tend to have more camera noise.
        // Since this may be used to skip compound mode in nonrd pickmode, which
        // is generally more effective for higher resolutions, better to be more
        // conservative.
        if (cpi.oxcf.tune_cfg.content != AOM_CONTENT_SCREEN)
        {
            if (cpi.common.width * cpi.common.height >= RESOLUTION_1080P)
                fac_uv = 3;
            else
                fac_uv = 5;
        }
        if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN &&
            cpi.rc.high_source_sad != 0)
        {
            shift_lower_limit = 7;
        }
        else if (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN &&
                 cpi.rc.percent_blocks_with_motion > 90 &&
                 cpi.rc.frame_source_sad > 10000 && source_sad_nonrd > kLowSad)
        {
            shift_lower_limit = 8;
            shift_upper_limit = 3;
        }
        else if (source_sad_nonrd >= kMedSad && x.source_variance > 500 &&
                 cpi.common.width * cpi.common.height >= 640 * 360)
        {
            shift_upper_limit = 2;
            shift_lower_limit = source_sad_nonrd > kMedSad ? 5 : 4;
        }

        MB_MODE_INFO mi = xd.mi[0];
        AV1_COMMON cm = cpi.common;
        YV12_BUFFER_CONFIG yv12 = get_ref_frame_yv12_buf(cm, LAST_FRAME);
        YV12_BUFFER_CONFIG yv12_g = get_ref_frame_yv12_buf(cm, GOLDEN_FRAME);
        YV12_BUFFER_CONFIG yv12_alt = get_ref_frame_yv12_buf(cm, ALTREF_FRAME);
        scale_factors sf =
            get_ref_scale_factors_const(cm, LAST_FRAME);
        scale_factors sf_g =
            get_ref_scale_factors_const(cm, GOLDEN_FRAME);
        scale_factors sf_alt =
            get_ref_scale_factors_const(cm, ALTREF_FRAME);
        buf_2d dst = default;
        uint uv_sad_g = 0;
        uint uv_sad_alt = 0;

        for (int plane = AOM_PLANE_U; plane < MAX_MB_PLANE; ++plane)
        {
            macroblock_plane p = x.plane[plane];
            macroblockd_plane pd = xd.plane[plane];
            BLOCK_SIZE bs =
                get_plane_block_size(bsize, pd.subsampling_x, pd.subsampling_y);

            if (bs != BLOCK_INVALID)
            {
                // For last:
                if (zero_motion)
                {
                    if (mi.ref_frame[0] == LAST_FRAME)
                    {
                        uv_sad[plane - 1] = cpi.ppi.fn_ptr[bs].sdf(
                            p.src.buf, p.src.stride, pd.pre[0].buf, pd.pre[0].stride);
                    }
                    else
                    {
                        Ptr<byte> src = (plane == 1) ? yv12.u_buffer : yv12.v_buffer;
                        setup_pred_plane(ref dst, xd.mi[0].bsize, src, yv12.uv_crop_width,
                                         yv12.uv_crop_height, yv12.uv_stride, xd.mi_row,
                                         xd.mi_col, sf, xd.plane[plane].subsampling_x,
                                         xd.plane[plane].subsampling_y);

                        uv_sad[plane - 1] = cpi.ppi.fn_ptr[bs].sdf(
                            p.src.buf, p.src.stride, dst.buf, dst.stride);
                    }
                }
                else
                {
                    uv_sad[plane - 1] = cpi.ppi.fn_ptr[bs].sdf(
                        p.src.buf, p.src.stride, pd.dst.buf, pd.dst.stride);
                }

                // For golden:
                if (y_sad_g != uint.MaxValue)
                {
                    Ptr<byte> src = (plane == 1) ? yv12_g.u_buffer : yv12_g.v_buffer;
                    setup_pred_plane(ref dst, xd.mi[0].bsize, src, yv12_g.uv_crop_width,
                                     yv12_g.uv_crop_height, yv12_g.uv_stride, xd.mi_row,
                                     xd.mi_col, sf_g, xd.plane[plane].subsampling_x,
                                     xd.plane[plane].subsampling_y);
                    uv_sad_g = cpi.ppi.fn_ptr[bs].sdf(p.src.buf, p.src.stride, dst.buf,
                                                      dst.stride);
                }

                // For altref:
                if (y_sad_alt != uint.MaxValue)
                {
                    Ptr<byte> src = (plane == 1) ? yv12_alt.u_buffer : yv12_alt.v_buffer;
                    setup_pred_plane(ref dst, xd.mi[0].bsize, src, yv12_alt.uv_crop_width,
                                     yv12_alt.uv_crop_height, yv12_alt.uv_stride,
                                     xd.mi_row, xd.mi_col, sf_alt,
                                     xd.plane[plane].subsampling_x,
                                     xd.plane[plane].subsampling_y);
                    uv_sad_alt = cpi.ppi.fn_ptr[bs].sdf(p.src.buf, p.src.stride,
                                                        dst.buf, dst.stride);
                }
            }

            if (uv_sad[plane - 1] > (y_sad >> shift_upper_limit))
                x.color_sensitivity_sb[COLOR_SENS_IDX(plane)] = 1;
            else if (uv_sad[plane - 1] < (y_sad >> shift_lower_limit))
                x.color_sensitivity_sb[COLOR_SENS_IDX(plane)] = 0;
            // Borderline case: to be refined at coding block level in nonrd_pickmode,
            // for coding block size < sb_size.
            else
                x.color_sensitivity_sb[COLOR_SENS_IDX(plane)] = 2;

            x.color_sensitivity_sb_g[COLOR_SENS_IDX(plane)] =
                (byte)B(uv_sad_g > y_sad_g / (uint)fac_uv);
            x.color_sensitivity_sb_alt[COLOR_SENS_IDX(plane)] =
                (byte)B(uv_sad_alt > y_sad_alt / (uint)fac_uv);
        }
    }

    private static void fill_variance_tree_leaves(
        AV1_COMP cpi, MACROBLOCK x, VP128x128 vt, PART_EVAL_STATUS[] force_split,
        long[][] avg_16x16, int[][] maxvar_16x16, int[][] minvar_16x16,
        long[] thresholds, Ptr<byte> src_buf, int src_stride,
        Ptr<byte> dst_buf, int dst_stride, bool is_key_frame,
        bool is_small_sb)
    {
        MACROBLOCKD xd = x.e_mbd;
        int num_64x64_blocks = is_small_sb ? 1 : 4;
        // TODO(kyslov) Bring back compute_minmax_variance with content type detection
        const int compute_minmax_variance = 0;
        int segment_id = xd.mi[0].segment_id;
        int pixels_wide = 128, pixels_high = 128;
        int border_offset_4x4 = 0;
        int temporal_denoising = cpi.sf.rt_sf.use_rtc_tf;
        // dst_buf pointer is not used for is_key_frame, so it should be NULL.
        Debug.Assert(!is_key_frame || dst_buf.IsNull);
        if (is_small_sb)
        {
            pixels_wide = 64;
            pixels_high = 64;
        }
        if (xd.mb_to_right_edge < 0) pixels_wide += (xd.mb_to_right_edge >> 3);
        if (xd.mb_to_bottom_edge < 0) pixels_high += (xd.mb_to_bottom_edge >> 3);
        // [subset] CONFIG_AV1_TEMPORAL_DENOISING is 0 (temporal_denoising |=
        // cpi->oxcf.noise_sensitivity is compiled out).
        // For temporal filtering or temporal denoiser enabled: since the source
        // is modified we need to avoid 4x4 avg along superblock boundary, since
        // simd code will load 8 pixels for 4x4 avg and so can access source
        // data outside superblock (while its being modified by temporal filter).
        // Temporal filtering is never done on key frames.
        if (!is_key_frame && temporal_denoising != 0) border_offset_4x4 = 4;
        for (int blk64_idx = 0; blk64_idx < num_64x64_blocks; blk64_idx++)
        {
            int x64_idx = GET_BLK_IDX_X(blk64_idx, 6);
            int y64_idx = GET_BLK_IDX_Y(blk64_idx, 6);
            int blk64_scale_idx = blk64_idx << 2;
            force_split[blk64_idx + 1] = PART_EVAL_ALL;

            for (int lvl1_idx = 0; lvl1_idx < 4; lvl1_idx++)
            {
                int x32_idx = x64_idx + GET_BLK_IDX_X(lvl1_idx, 5);
                int y32_idx = y64_idx + GET_BLK_IDX_Y(lvl1_idx, 5);
                int lvl1_scale_idx = (blk64_scale_idx + lvl1_idx) << 2;
                force_split[5 + blk64_scale_idx + lvl1_idx] = PART_EVAL_ALL;
                avg_16x16[blk64_idx][lvl1_idx] = 0;
                maxvar_16x16[blk64_idx][lvl1_idx] = 0;
                minvar_16x16[blk64_idx][lvl1_idx] = int.MaxValue;
                for (int lvl2_idx = 0; lvl2_idx < 4; lvl2_idx++)
                {
                    int x16_idx = x32_idx + GET_BLK_IDX_X(lvl2_idx, 4);
                    int y16_idx = y32_idx + GET_BLK_IDX_Y(lvl2_idx, 4);
                    int split_index = 21 + lvl1_scale_idx + lvl2_idx;
                    VP16x16 vst = vt.split[blk64_idx].split[lvl1_idx].split[lvl2_idx];
                    force_split[split_index] = PART_EVAL_ALL;
                    if (is_key_frame)
                    {
                        // Go down to 4x4 down-sampling for variance.
                        for (int lvl3_idx = 0; lvl3_idx < 4; lvl3_idx++)
                        {
                            int x8_idx = x16_idx + GET_BLK_IDX_X(lvl3_idx, 3);
                            int y8_idx = y16_idx + GET_BLK_IDX_Y(lvl3_idx, 3);
                            VP8x8 vst2 = vst.split[lvl3_idx];
                            fill_variance_4x4avg(src_buf, src_stride, x8_idx, y8_idx, vst2,
                                                 pixels_wide, pixels_high, border_offset_4x4);
                        }
                    }
                    else
                    {
                        fill_variance_8x8avg(src_buf, src_stride, dst_buf, dst_stride,
                                             x16_idx, y16_idx, vst,
                                             pixels_wide, pixels_high);

                        fill_variance_tree(vst, BLOCK_16X16);
                        VPartVar none_var = vt.split[blk64_idx]
                                                .split[lvl1_idx]
                                                .split[lvl2_idx]
                                                .part_variances.none;
                        get_variance(none_var);
                        int val_none_var = none_var.variance;
                        avg_16x16[blk64_idx][lvl1_idx] += val_none_var;
                        minvar_16x16[blk64_idx][lvl1_idx] =
                            AOMMIN(minvar_16x16[blk64_idx][lvl1_idx], val_none_var);
                        maxvar_16x16[blk64_idx][lvl1_idx] =
                            AOMMAX(maxvar_16x16[blk64_idx][lvl1_idx], val_none_var);
                        if (val_none_var > thresholds[3])
                        {
                            // 16X16 variance is above threshold for split, so force split to
                            // 8x8 for this 16x16 block (this also forces splits for upper
                            // levels).
                            force_split[split_index] = PART_EVAL_ONLY_SPLIT;
                            force_split[5 + blk64_scale_idx + lvl1_idx] = PART_EVAL_ONLY_SPLIT;
                            force_split[blk64_idx + 1] = PART_EVAL_ONLY_SPLIT;
                            force_split[0] = PART_EVAL_ONLY_SPLIT;
                        }
                        else if (cyclic_refresh_segment_id_boosted(segment_id) == 0 &&
                                 compute_minmax_variance != 0 && val_none_var > thresholds[2])
                        {
                            // [subset] compute_minmax_8x8() path: compute_minmax_variance
                            // is the constant 0 above.
                            throw new NotSupportedException(
                                "fill_variance_tree_leaves: compute_minmax_variance");
                        }
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void set_ref_frame_for_partition(
        AV1_COMP cpi, MACROBLOCK x, MACROBLOCKD xd,
        ref MV_REFERENCE_FRAME ref_frame_partition, MB_MODE_INFO mi,
        ref uint y_sad, ref uint y_sad_g, ref uint y_sad_alt,
        YV12_BUFFER_CONFIG yv12_g, YV12_BUFFER_CONFIG yv12_alt,
        int mi_row, int mi_col, int num_planes)
    {
        AV1_COMMON cm = cpi.common;
        double fac =
            (cpi.svc.spatial_layer_id > 0 && cpi.svc.has_lower_quality_layer != 0) ? 1.0
                                                                                   : 0.9;
        bool is_set_golden_ref_frame =
            y_sad_g < fac * y_sad && y_sad_g <= y_sad_alt;
        bool is_set_altref_ref_frame =
            y_sad_alt < fac * y_sad && y_sad_alt < y_sad_g;

        if (is_set_golden_ref_frame)
        {
            av1_setup_pre_planes(xd, 0, yv12_g, mi_row, mi_col,
                                 get_ref_scale_factors(cm, GOLDEN_FRAME), num_planes);
            mi.ref_frame[0] = GOLDEN_FRAME;
            mi.mv[0].as_int = 0;
            y_sad = y_sad_g;
            ref_frame_partition = GOLDEN_FRAME;
            x.nonrd_prune_ref_frame_search = 0;
            x.sb_me_partition = 0;
        }
        else if (is_set_altref_ref_frame)
        {
            av1_setup_pre_planes(xd, 0, yv12_alt, mi_row, mi_col,
                                 get_ref_scale_factors(cm, ALTREF_FRAME), num_planes);
            mi.ref_frame[0] = ALTREF_FRAME;
            mi.mv[0].as_int = 0;
            y_sad = y_sad_alt;
            ref_frame_partition = ALTREF_FRAME;
            x.nonrd_prune_ref_frame_search = 0;
            x.sb_me_partition = 0;
        }
        else
        {
            ref_frame_partition = LAST_FRAME;
            x.nonrd_prune_ref_frame_search =
                cpi.sf.rt_sf.nonrd_prune_ref_frame_search;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int mv_distance(in FULLPEL_MV mv0,
                                   in FULLPEL_MV mv1)
    {
        return Math.Abs(mv0.row - mv1.row) + Math.Abs(mv0.col - mv1.col);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void evaluate_neighbour_mvs(AV1_COMP cpi, MACROBLOCK x,
                                               ref uint y_sad, bool is_small_sb,
                                               int est_motion)
    {
        int source_sad_nonrd = x.content_state_sb.source_sad_nonrd;
        // TODO(yunqingwang@google.com): test if this condition works with other
        // speeds.
        if (est_motion > 2 && source_sad_nonrd > kMedSad) return;

        MACROBLOCKD xd = x.e_mbd;
        BLOCK_SIZE bsize = is_small_sb ? BLOCK_64X64 : BLOCK_128X128;
        MB_MODE_INFO mi = xd.mi[0];

        uint above_y_sad = uint.MaxValue;
        uint left_y_sad = uint.MaxValue;
        FULLPEL_MV above_mv = kZeroFullMv;
        FULLPEL_MV left_mv = kZeroFullMv;
        SubpelMvLimits subpel_mv_limits = default;
        MV dummy_mv = new MV(0, 0);
        av1_set_subpel_mv_search_range(ref subpel_mv_limits, in x.mv_limits, in dummy_mv);

        // Current best MV
        FULLPEL_MV best_mv = get_fullmv_from_mv(in mi.mv[0].as_mv);
        int multi = (est_motion > 2 && source_sad_nonrd > kLowSad) ? 7 : 8;

        if (xd.up_available)
        {
            MB_MODE_INFO above_mbmi = xd.above_mbmi;
            if (above_mbmi.mode >= INTRA_MODE_END &&
                above_mbmi.ref_frame[0] == LAST_FRAME)
            {
                MV temp = above_mbmi.mv[0].as_mv;
                clamp_mv(ref temp, in subpel_mv_limits);
                above_mv = get_fullmv_from_mv(in temp);

                if (mv_distance(in best_mv, in above_mv) > 0)
                {
                    Ptr<byte> ref_buf =
                        get_buf_from_fullmv(in xd.plane[0].pre[0], in above_mv);
                    above_y_sad = cpi.ppi.fn_ptr[bsize].sdf(
                        x.plane[0].src.buf, x.plane[0].src.stride, ref_buf,
                        xd.plane[0].pre[0].stride);
                }
            }
        }
        if (xd.left_available)
        {
            MB_MODE_INFO left_mbmi = xd.left_mbmi;
            if (left_mbmi.mode >= INTRA_MODE_END &&
                left_mbmi.ref_frame[0] == LAST_FRAME)
            {
                MV temp = left_mbmi.mv[0].as_mv;
                clamp_mv(ref temp, in subpel_mv_limits);
                left_mv = get_fullmv_from_mv(in temp);

                if (mv_distance(in best_mv, in left_mv) > 0 &&
                    mv_distance(in above_mv, in left_mv) > 0)
                {
                    Ptr<byte> ref_buf =
                        get_buf_from_fullmv(in xd.plane[0].pre[0], in left_mv);
                    left_y_sad = cpi.ppi.fn_ptr[bsize].sdf(
                        x.plane[0].src.buf, x.plane[0].src.stride, ref_buf,
                        xd.plane[0].pre[0].stride);
                }
            }
        }

        if (above_y_sad < (((uint)multi * y_sad) >> 3) && above_y_sad < left_y_sad)
        {
            y_sad = above_y_sad;
            mi.mv[0].as_mv = get_mv_from_fullmv(in above_mv);
            clamp_mv(ref mi.mv[0].as_mv, in subpel_mv_limits);
        }
        if (left_y_sad < (((uint)multi * y_sad) >> 3) && left_y_sad < above_y_sad)
        {
            y_sad = left_y_sad;
            mi.mv[0].as_mv = get_mv_from_fullmv(in left_mv);
            clamp_mv(ref mi.mv[0].as_mv, in subpel_mv_limits);
        }
    }

    private static void do_int_pro_motion_estimation(AV1_COMP cpi, MACROBLOCK x,
                                                     ref uint y_sad, int mi_row,
                                                     int mi_col, int source_sad_nonrd)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mi = xd.mi[0];
        int large_search = B(
            cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN ||
            (source_sad_nonrd > kMedSad && cm.width * cm.height > 1280 * 720 &&
             cpi.rc.high_motion_content_screen_rtc == 0));
        int max_sw =
            (cpi.oxcf.tune_cfg.content == AOM_CONTENT_SCREEN) ? 512 : 256;
        int increase_col_sw = B(
            source_sad_nonrd > kMedSad && cpi.rc.high_motion_content_screen_rtc == 0);
        int me_search_size_col = large_search != 0
                                     ? increase_col_sw != 0 ? max_sw : 96
                                     : block_size_wide[cm.seq_params.sb_size] >> 1;
        // Use larger search size row motion to capture
        // vertical scroll, which can be larger motion.
        int me_search_size_row = large_search != 0
                                     ? source_sad_nonrd > kMedSad ? max_sw : 192
                                     : block_size_high[cm.seq_params.sb_size] >> 1;
        if (cm.width * cm.height >= 3840 * 2160)
        {
            me_search_size_row = me_search_size_row << 1;
            me_search_size_col = me_search_size_col << 1;
        }
        uint y_sad_zero = 0; // C: uninitialized; written by av1_int_pro_motion_estimation()
        y_sad = av1_int_pro_motion_estimation(
            cpi, x, cm.seq_params.sb_size, mi_row, mi_col, in kZeroMv, ref y_sad_zero,
            me_search_size_col, me_search_size_row, 1, large_search);
        // The logic below selects whether the motion estimated in the
        // int_pro_motion() will be used in nonrd_pickmode.
        if (large_search != 0)
        {
            uint thresh_sad =
                (cm.seq_params.sb_size == BLOCK_128X128) ? 50000u : 20000u;
            if (y_sad < (y_sad_zero >> 1) && y_sad < thresh_sad)
            {
                x.sb_me_partition = 1;
                x.sb_me_mv.as_int = mi.mv[0].as_int;
                if (cpi.svc.temporal_layer_id == 0)
                {
                    if (Math.Abs((int)mi.mv[0].as_mv.col) > 16 && Math.Abs((int)mi.mv[0].as_mv.row) == 0)
                        x.sb_col_scroll++;
                    else if (Math.Abs((int)mi.mv[0].as_mv.row) > 16 && Math.Abs((int)mi.mv[0].as_mv.col) == 0)
                        x.sb_row_scroll++;
                }
            }
            else
            {
                x.sb_me_partition = 0;
                // Fall back to using zero motion.
                y_sad = y_sad_zero;
                mi.mv[0].as_int = 0;
            }
        }
    }

    private static void setup_planes(AV1_COMP cpi, MACROBLOCK x, ref uint y_sad,
                                     ref uint y_sad_g, ref uint y_sad_alt,
                                     ref uint y_sad_last,
                                     ref MV_REFERENCE_FRAME ref_frame_partition,
                                     scale_factors sf_no_scale, int mi_row,
                                     int mi_col, bool is_small_sb, bool scaled_ref_last)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        int num_planes = av1_num_planes(cm);
        bool scaled_ref_golden = false;
        bool scaled_ref_alt = false;
        BLOCK_SIZE bsize = is_small_sb ? BLOCK_64X64 : BLOCK_128X128;
        MB_MODE_INFO mi = xd.mi[0];
        YV12_BUFFER_CONFIG yv12 =
            scaled_ref_last ? av1_get_scaled_ref_frame(cpi, LAST_FRAME)
                            : get_ref_frame_yv12_buf(cm, LAST_FRAME);
        Debug.Assert(yv12 != null);
        YV12_BUFFER_CONFIG yv12_g = null;
        YV12_BUFFER_CONFIG yv12_alt = null;
        // Check if LAST is a reference. For spatial layers always use it as
        // reference scaling.
        int use_last_ref = B((cpi.ref_frame_flags & AOM_LAST_FLAG) != 0 ||
                             cpi.svc.number_spatial_layers > 1);
        int use_golden_ref = cpi.ref_frame_flags & AOM_GOLD_FLAG;
        int use_alt_ref = B(cpi.ppi.rtc_ref.set_ref_frame_config != 0 ||
                            cpi.sf.rt_sf.use_nonrd_altref_frame != 0 ||
                            (cpi.sf.rt_sf.use_comp_ref_nonrd != 0 &&
                             cpi.sf.rt_sf.ref_frame_comp_nonrd[2] == 1));

        // Check if GOLDEN should be used as reference for partitioning.
        // Allow for spatial layers if lower layer has same resolution.
        if ((cpi.svc.number_spatial_layers == 1 ||
             cpi.svc.has_lower_quality_layer != 0) &&
            use_golden_ref != 0 &&
            (x.content_state_sb.source_sad_nonrd != kZeroSad || use_last_ref == 0))
        {
            yv12_g = get_ref_frame_yv12_buf(cm, GOLDEN_FRAME);
            if (yv12_g != null && (yv12_g.y_crop_height != cm.height ||
                                   yv12_g.y_crop_width != cm.width))
            {
                yv12_g = av1_get_scaled_ref_frame(cpi, GOLDEN_FRAME);
                scaled_ref_golden = true;
            }
            if (yv12_g != null && (yv12_g != yv12 || use_last_ref == 0))
            {
                av1_setup_pre_planes(
                    xd, 0, yv12_g, mi_row, mi_col,
                    scaled_ref_golden ? null : get_ref_scale_factors(cm, GOLDEN_FRAME),
                    num_planes);
                y_sad_g = cpi.ppi.fn_ptr[bsize].sdf(
                    x.plane[AOM_PLANE_Y].src.buf, x.plane[AOM_PLANE_Y].src.stride,
                    xd.plane[AOM_PLANE_Y].pre[0].buf,
                    xd.plane[AOM_PLANE_Y].pre[0].stride);
            }
        }

        // Check if ALTREF should be used as reference for partitioning.
        // Allow for spatial layers if lower layer has same resolution.
        if ((cpi.svc.number_spatial_layers == 1 ||
             cpi.svc.has_lower_quality_layer != 0) &&
            use_alt_ref != 0 && (cpi.ref_frame_flags & AOM_ALT_FLAG) != 0 &&
            (x.content_state_sb.source_sad_nonrd != kZeroSad || use_last_ref == 0))
        {
            yv12_alt = get_ref_frame_yv12_buf(cm, ALTREF_FRAME);
            if (yv12_alt != null && (yv12_alt.y_crop_height != cm.height ||
                                     yv12_alt.y_crop_width != cm.width))
            {
                yv12_alt = av1_get_scaled_ref_frame(cpi, ALTREF_FRAME);
                scaled_ref_alt = true;
            }
            if (yv12_alt != null && (yv12_alt != yv12 || use_last_ref == 0))
            {
                av1_setup_pre_planes(
                    xd, 0, yv12_alt, mi_row, mi_col,
                    scaled_ref_alt ? null : get_ref_scale_factors(cm, ALTREF_FRAME),
                    num_planes);
                y_sad_alt = cpi.ppi.fn_ptr[bsize].sdf(
                    x.plane[AOM_PLANE_Y].src.buf, x.plane[AOM_PLANE_Y].src.stride,
                    xd.plane[AOM_PLANE_Y].pre[0].buf,
                    xd.plane[AOM_PLANE_Y].pre[0].stride);
            }
        }

        mi.ref_frame[0] = NONE_FRAME;
        mi.ref_frame[1] = NONE_FRAME;
        mi.bsize = cm.seq_params.sb_size;
        mi.mv[0].as_int = 0;
        mi.interp_filters = av1_broadcast_interp_filter(BILINEAR);
        mi.use_intrabc = 0;

        if (use_last_ref != 0)
        {
            av1_setup_pre_planes(
                xd, 0, yv12, mi_row, mi_col,
                scaled_ref_last ? null : get_ref_scale_factors(cm, LAST_FRAME),
                num_planes);
            mi.ref_frame[0] = LAST_FRAME;

            int source_sad_nonrd = x.content_state_sb.source_sad_nonrd;
            int est_motion = cpi.sf.rt_sf.estimate_motion_for_var_based_partition;
            // TODO(b/290596301): Look into adjusting this condition.
            // There is regression on color content when
            // estimate_motion_for_var_based_partition = 3 and high motion,
            // so for now force it to 2 based on superblock sad.
            if (est_motion > 2 && source_sad_nonrd > kMedSad) est_motion = 2;

            if ((est_motion == 1 || est_motion == 2) && xd.mb_to_right_edge >= 0 &&
                xd.mb_to_bottom_edge >= 0 && x.source_variance > 100 &&
                source_sad_nonrd > kLowSad)
            {
                do_int_pro_motion_estimation(cpi, x, ref y_sad, mi_row, mi_col,
                                             source_sad_nonrd);
            }

            if (y_sad == uint.MaxValue)
            {
                y_sad = cpi.ppi.fn_ptr[bsize].sdf(
                    x.plane[AOM_PLANE_Y].src.buf, x.plane[AOM_PLANE_Y].src.stride,
                    xd.plane[AOM_PLANE_Y].pre[0].buf,
                    xd.plane[AOM_PLANE_Y].pre[0].stride);
            }

            // Evaluate if neighbours' MVs give better predictions. Zero MV is tested
            // already, so only non-zero MVs are tested here. Here the neighbour blocks
            // are the first block above or left to this superblock.
            if (est_motion >= 2 && (xd.up_available || xd.left_available))
                evaluate_neighbour_mvs(cpi, x, ref y_sad, is_small_sb, est_motion);

            y_sad_last = y_sad;
        }

        // Pick the ref frame for partitioning, use golden or altref frame only if
        // its lower sad, bias to LAST with factor 0.9.
        set_ref_frame_for_partition(cpi, x, xd, ref ref_frame_partition, mi, ref y_sad,
                                    ref y_sad_g, ref y_sad_alt, yv12_g, yv12_alt, mi_row,
                                    mi_col, num_planes);

        // Only calculate the predictor for non-zero MV.
        if (mi.mv[0].as_int != 0)
        {
            if (!scaled_ref_last)
            {
                set_ref_ptrs(cm, xd, mi.ref_frame[0], mi.ref_frame[1]);
            }
            else
            {
                xd.block_ref_scale_factors[0] = sf_no_scale;
                xd.block_ref_scale_factors[1] = sf_no_scale;
            }
            av1_enc_build_inter_predictor(cm, xd, mi_row, mi_col, Ptr<BUFFER_SET>.Null,
                                          cm.seq_params.sb_size, AOM_PLANE_Y,
                                          num_planes - 1);
        }
    }

    // [subset] get_part_eval_based_on_sub_blk_var: only used when
    // sf->rt_sf.vbp_prune_16x16_split_using_min_max_sub_blk_var is set, which only the
    // allintra speed features do.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool is_set_force_zeromv_skip_based_on_src_sad(
        int set_zeromv_skip_based_on_source_sad, SOURCE_SAD source_sad_nonrd)
    {
        if (set_zeromv_skip_based_on_source_sad == 0) return false;

        if (set_zeromv_skip_based_on_source_sad >= 3)
            return source_sad_nonrd <= kLowSad;
        else if (set_zeromv_skip_based_on_source_sad >= 2)
            return source_sad_nonrd <= kVeryLowSad;
        else if (set_zeromv_skip_based_on_source_sad >= 1)
            return source_sad_nonrd == kZeroSad;

        return false;
    }

    // PORT-NOTE: never executed in the coverage runs only because their clips are shorter
    // than the frames_since_key > 30 its caller requires; it is data-dependent and
    // live at speed 11 (part_early_exit_zeromv = 2, set_zeromv_skip_based_on_source_sad = 2).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool set_force_zeromv_skip_for_sb(
        AV1_COMP cpi, MACROBLOCK x, in TileInfo tile, VP128x128 vt,
        uint[] uv_sad, int mi_row, int mi_col, uint y_sad,
        BLOCK_SIZE bsize)
    {
        AV1_COMMON cm = cpi.common;
        if (!is_set_force_zeromv_skip_based_on_src_sad(
                cpi.sf.rt_sf.set_zeromv_skip_based_on_source_sad,
                x.content_state_sb.source_sad_nonrd))
            return false;
        int shift = cpi.sf.rt_sf.increase_source_sad_thresh != 0 ? 1 : 0;
        int block_width = mi_size_wide[cm.seq_params.sb_size];
        int block_height = mi_size_high[cm.seq_params.sb_size];
        uint thresh_exit_part_y =
            cpi.zeromv_skip_thresh_exit_part[bsize] << shift;
        uint thresh_exit_part_uv =
            (uint)CALC_CHROMA_THRESH_FOR_ZEROMV_SKIP(thresh_exit_part_y) << shift;
        // Be more aggressive in UV threshold if source_sad >= VeryLowSad
        // to suppreess visual artifact caused by the speed feature:
        // set_zeromv_skip_based_on_source_sad = 2. For now only for
        // part_early_exit_zeromv = 1.
        if (x.content_state_sb.source_sad_nonrd >= kVeryLowSad &&
            cpi.sf.rt_sf.part_early_exit_zeromv == 1)
            thresh_exit_part_uv = thresh_exit_part_uv >> 3;
        if (mi_col + block_width <= tile.mi_col_end &&
            mi_row + block_height <= tile.mi_row_end && y_sad < thresh_exit_part_y &&
            uv_sad[0] < thresh_exit_part_uv && uv_sad[1] < thresh_exit_part_uv)
        {
            set_block_size(cpi, mi_row, mi_col, bsize);
            x.force_zeromv_skip_for_sb = 1;
            // aom_free(vt): vt is per-thread scratch here.
            _ = vt;
            // Partition shape is set here at SB level.
            // Exit needs to happen from av1_choose_var_based_partitioning().
            return true;
        }
        else if (x.content_state_sb.source_sad_nonrd == kZeroSad &&
                 cpi.sf.rt_sf.part_early_exit_zeromv >= 2)
            x.force_zeromv_skip_for_sb = 2;
        return false;
    }

    internal static int av1_choose_var_based_partitioning(AV1_COMP cpi, in TileInfo tile,
                                                          ThreadData td, MACROBLOCK x, int mi_row,
                                                          int mi_col)
    {
        AV1_COMMON cm = cpi.common;
        MACROBLOCKD xd = x.e_mbd;
        long[] vbp_thresholds = cpi.vbp_info.thresholds;
        VarBasedPartScratch scratch = td.vbp_scratch ??= new VarBasedPartScratch();
        PART_EVAL_STATUS[] force_split = scratch.force_split;
        long avg_64x64;
        int[] max_var_32x32 = scratch.max_var_32x32;
        int[] min_var_32x32 = scratch.min_var_32x32;
        int var_32x32;
        int var_64x64;
        int min_var_64x64 = int.MaxValue;
        int max_var_64x64 = 0;
        long[][] avg_16x16 = scratch.avg_16x16;
        int[][] maxvar_16x16 = scratch.maxvar_16x16;
        int[][] minvar_16x16 = scratch.minvar_16x16;
        Ptr<byte> src_buf;
        Ptr<byte> dst_buf;
        int dst_stride;
        uint[] uv_sad = scratch.uv_sad;
        NOISE_LEVEL noise_level = kLow;
        bool is_zero_motion = true;
        bool scaled_ref_last = false;
        scale_factors sf_no_scale = scratch.sf_no_scale;
        av1_setup_scale_factors_for_frame(sf_no_scale, cm.width, cm.height,
                                          cm.width, cm.height);

        bool is_key_frame =
            (frame_is_intra_only(cm) != 0 ||
             (cpi.ppi.use_svc != 0 &&
              cpi.svc.layer_context[cpi.svc.temporal_layer_id].is_key_frame != 0));

        Debug.Assert(cm.seq_params.sb_size == BLOCK_64X64 ||
                     cm.seq_params.sb_size == BLOCK_128X128);
        bool is_small_sb = (cm.seq_params.sb_size == BLOCK_64X64);
        int num_64x64_blocks = is_small_sb ? 1 : 4;

        uint y_sad = uint.MaxValue;
        uint y_sad_g = uint.MaxValue;
        uint y_sad_alt = uint.MaxValue;
        uint y_sad_last = uint.MaxValue;
        BLOCK_SIZE bsize = is_small_sb ? BLOCK_64X64 : BLOCK_128X128;

        // Force skip encoding for all superblocks on slide change for
        // non_reference_frames.
        if (cpi.sf.rt_sf.skip_encoding_non_reference_slide_change != 0 &&
            cpi.rc.high_source_sad != 0 && cpi.ppi.rtc_ref.non_reference_frame != 0)
        {
            // [subset] skip_encoding_non_reference_slide_change is only set for screen
            // content (av1_set_fixed_partitioning() path).
            throw new NotSupportedException(
                "av1_choose_var_based_partitioning: skip_encoding_non_reference_slide_change");
        }

        // Ref frame used in partitioning.
        MV_REFERENCE_FRAME ref_frame_partition = LAST_FRAME;

        long[] thresholds = scratch.thresholds;
        thresholds[0] = vbp_thresholds[0];
        thresholds[1] = vbp_thresholds[1];
        thresholds[2] = vbp_thresholds[2];
        thresholds[3] = vbp_thresholds[3];
        thresholds[4] = vbp_thresholds[4];

        int segment_id = xd.mi[0].segment_id;
        ulong blk_sad = 0;
        if (!cpi.src_sad_blk_64x64.IsNull &&
            cpi.svc.spatial_layer_id == cpi.svc.number_spatial_layers - 1)
        {
            int sb_size_by_mb = (cm.seq_params.sb_size == BLOCK_128X128)
                                    ? (cm.seq_params.mib_size >> 1)
                                    : cm.seq_params.mib_size;
            int sb_cols =
                (cm.mi_params.mi_cols + sb_size_by_mb - 1) / sb_size_by_mb;
            int sbi_col = mi_col / sb_size_by_mb;
            int sbi_row = mi_row / sb_size_by_mb;
            blk_sad = cpi.src_sad_blk_64x64[sbi_col + sbi_row * sb_cols];
        }

        bool is_segment_id_boosted =
            cpi.oxcf.q_cfg.aq_mode == CYCLIC_REFRESH_AQ && cm.seg.enabled != 0 &&
            cyclic_refresh_segment_id_boosted(segment_id) != 0;
        int sb_qindex =
            clamp(cm.delta_q_info.delta_q_present_flag != 0
                      ? cm.quant_params.base_qindex + x.delta_qindex
                      : cm.quant_params.base_qindex,
                  0, QINDEX_RANGE - 1);
        int qindex = is_segment_id_boosted || cpi.roi.delta_qp_enabled != 0
                         ? av1_get_qindex(cm.seg, segment_id, sb_qindex)
                         : sb_qindex;
        set_vbp_thresholds(
            cpi, thresholds, blk_sad, qindex, x.content_state_sb.low_sumdiff,
            x.content_state_sb.source_sad_nonrd, x.content_state_sb.source_sad_rd,
            is_segment_id_boosted, x.content_state_sb.lighting_change);

        src_buf = x.plane[AOM_PLANE_Y].src.buf;
        int src_stride = x.plane[AOM_PLANE_Y].src.stride;

        // Index for force_split: 0 for 64x64, 1-4 for 32x32 blocks,
        // 5-20 for the 16x16 blocks.
        force_split[0] = PART_EVAL_ALL;

        // Check if LAST frame is NULL, and if so, treat this frame
        // as a key frame, for the purpose of the superblock partitioning.
        // LAST == NULL can happen in cases where enhancement spatial layers are
        // enabled dyanmically and the only reference is the spatial(GOLDEN).
        // If LAST frame has a different resolution: set the scaled_ref_last flag
        // and check if ref_scaled is NULL.
        if (frame_is_intra_only(cm) == 0)
        {
            YV12_BUFFER_CONFIG @ref = get_ref_frame_yv12_buf(cm, LAST_FRAME);
            if (@ref == null)
            {
                is_key_frame = true;
            }
            else if (@ref.y_crop_height != cm.height ||
                     @ref.y_crop_width != cm.width)
            {
                scaled_ref_last = true;
                YV12_BUFFER_CONFIG ref_scaled =
                    av1_get_scaled_ref_frame(cpi, LAST_FRAME);
                if (ref_scaled == null) is_key_frame = true;
            }
        }

        x.source_variance = uint.MaxValue;
        // For nord_pickmode: compute source_variance, only for superblocks with
        // some motion for now. This input can then be used to bias the partitioning
        // or the chroma_check.
        if (cpi.sf.rt_sf.use_nonrd_pick_mode != 0 &&
            x.content_state_sb.source_sad_nonrd > kLowSad)
            x.source_variance = av1_get_perpixel_variance_facade(
                cpi, xd, in x.plane[0].src, cm.seq_params.sb_size, AOM_PLANE_Y);

        if (!is_key_frame)
        {
            setup_planes(cpi, x, ref y_sad, ref y_sad_g, ref y_sad_alt, ref y_sad_last,
                         ref ref_frame_partition, sf_no_scale, mi_row, mi_col,
                         is_small_sb, scaled_ref_last);

            MB_MODE_INFO mi = xd.mi[0];
            // Use reference SB directly for zero mv.
            if (mi.mv[0].as_int != 0)
            {
                dst_buf = xd.plane[AOM_PLANE_Y].dst.buf;
                dst_stride = xd.plane[AOM_PLANE_Y].dst.stride;
                is_zero_motion = false;
            }
            else
            {
                dst_buf = xd.plane[AOM_PLANE_Y].pre[0].buf;
                dst_stride = xd.plane[AOM_PLANE_Y].pre[0].stride;
            }
        }
        else
        {
            dst_buf = Ptr<byte>.Null;
            dst_stride = 0;
        }

        // check and set the color sensitivity of sb.
        av1_zero(uv_sad);
        chroma_check(cpi, x, bsize, y_sad_last, y_sad_g, y_sad_alt, is_key_frame,
                     is_zero_motion, uv_sad);

        x.force_zeromv_skip_for_sb = 0;

        // C: vt = aom_malloc(sizeof(*vt)) (AOM_CHECK_MEM_ERROR); per-thread scratch here.
        VP128x128 vt = scratch.vt;
        vt.split = td.vt64x64;

        // If the superblock is completely static (zero source sad) and
        // the y_sad (relative to LAST ref) is very small, take the sb_size partition
        // and exit, and force zeromv_last skip mode for nonrd_pickmode.
        // Only do this on the base segment (so the QP-boosted segment, if applied,
        // can still continue cleaning/ramping up the quality).
        // Condition on color uv_sad is also added.
        if (!is_key_frame && cpi.sf.rt_sf.part_early_exit_zeromv != 0 &&
            cpi.rc.frames_since_key > 30 && segment_id == CR_SEGMENT_ID_BASE &&
            ref_frame_partition == LAST_FRAME && xd.mi[0].mv[0].as_int == 0)
        {
            // Exit here, if zero mv skip flag is set at SB level.
            if (set_force_zeromv_skip_for_sb(cpi, x, tile, vt, uv_sad, mi_row, mi_col,
                                             y_sad, bsize))
                return 0;
        }

        if (cpi.noise_estimate.enabled != 0)
            noise_level = av1_noise_estimate_extract_level(cpi.noise_estimate);

        // Fill in the entire tree of 8x8 (for inter frames) or 4x4 (for key frames)
        // variances for splits.
        fill_variance_tree_leaves(cpi, x, vt, force_split, avg_16x16, maxvar_16x16,
                                  minvar_16x16, thresholds, src_buf, src_stride,
                                  dst_buf, dst_stride, is_key_frame, is_small_sb);

        avg_64x64 = 0;
        for (int blk64_idx = 0; blk64_idx < num_64x64_blocks; ++blk64_idx)
        {
            max_var_32x32[blk64_idx] = 0;
            min_var_32x32[blk64_idx] = int.MaxValue;
            int blk64_scale_idx = blk64_idx << 2;
            for (int lvl1_idx = 0; lvl1_idx < 4; lvl1_idx++)
            {
                int lvl1_scale_idx = (blk64_scale_idx + lvl1_idx) << 2;
                for (int lvl2_idx = 0; lvl2_idx < 4; lvl2_idx++)
                {
                    if (!is_key_frame) continue;
                    VP16x16 vtemp = vt.split[blk64_idx].split[lvl1_idx].split[lvl2_idx];
                    for (int lvl3_idx = 0; lvl3_idx < 4; lvl3_idx++)
                        fill_variance_tree(vtemp.split[lvl3_idx], BLOCK_8X8);
                    fill_variance_tree(vtemp, BLOCK_16X16);
                    // If variance of this 16x16 block is above the threshold, force block
                    // to split. This also forces a split on the upper levels.
                    get_variance(vtemp.part_variances.none);
                    if (vtemp.part_variances.none.variance > thresholds[3])
                    {
                        int split_index = 21 + lvl1_scale_idx + lvl2_idx;
                        // [subset] vbp_prune_16x16_split_using_min_max_sub_blk_var
                        // (get_part_eval_based_on_sub_blk_var) is only set by the allintra
                        // speed features.
                        if (cpi.sf.rt_sf.vbp_prune_16x16_split_using_min_max_sub_blk_var)
                            throw new NotSupportedException(
                                "av1_choose_var_based_partitioning: " +
                                "vbp_prune_16x16_split_using_min_max_sub_blk_var");
                        force_split[split_index] = PART_EVAL_ONLY_SPLIT;
                        force_split[5 + blk64_scale_idx + lvl1_idx] = PART_EVAL_ONLY_SPLIT;
                        force_split[blk64_idx + 1] = PART_EVAL_ONLY_SPLIT;
                        force_split[0] = PART_EVAL_ONLY_SPLIT;
                    }
                }
                fill_variance_tree(vt.split[blk64_idx].split[lvl1_idx], BLOCK_32X32);
                // If variance of this 32x32 block is above the threshold, or if its above
                // (some threshold of) the average variance over the sub-16x16 blocks,
                // then force this block to split. This also forces a split on the upper
                // (64x64) level.
                ulong frame_sad_thresh = 20000;
                int is_360p_or_smaller = B(cm.width * cm.height <= RESOLUTION_360P);
                if (cpi.svc.number_temporal_layers > 2 &&
                    cpi.svc.temporal_layer_id == 0)
                    frame_sad_thresh = frame_sad_thresh << 1;
                if (force_split[5 + blk64_scale_idx + lvl1_idx] == PART_EVAL_ALL)
                {
                    get_variance(vt.split[blk64_idx].split[lvl1_idx].part_variances.none);
                    var_32x32 =
                        vt.split[blk64_idx].split[lvl1_idx].part_variances.none.variance;
                    max_var_32x32[blk64_idx] = AOMMAX(var_32x32, max_var_32x32[blk64_idx]);
                    min_var_32x32[blk64_idx] = AOMMIN(var_32x32, min_var_32x32[blk64_idx]);
                    long max_min_var_16X16_diff =
                        (long)maxvar_16x16[blk64_idx][lvl1_idx] -
                        minvar_16x16[blk64_idx][lvl1_idx];

                    if (var_32x32 > thresholds[2] ||
                        (!is_key_frame && var_32x32 > (thresholds[2] >> 1) &&
                         var_32x32 > (avg_16x16[blk64_idx][lvl1_idx] >> 1)))
                    {
                        force_split[5 + blk64_scale_idx + lvl1_idx] = PART_EVAL_ONLY_SPLIT;
                        force_split[blk64_idx + 1] = PART_EVAL_ONLY_SPLIT;
                        force_split[0] = PART_EVAL_ONLY_SPLIT;
                    }
                    else if (!is_key_frame && is_360p_or_smaller != 0 &&
                             ((max_min_var_16X16_diff > (thresholds[2] >> 1) &&
                               maxvar_16x16[blk64_idx][lvl1_idx] > thresholds[2]) ||
                              (cpi.sf.rt_sf.prefer_large_partition_blocks != 0 &&
                               x.content_state_sb.source_sad_nonrd > kLowSad &&
                               cpi.rc.frame_source_sad < frame_sad_thresh &&
                               maxvar_16x16[blk64_idx][lvl1_idx] > (thresholds[2] >> 4) &&
                               maxvar_16x16[blk64_idx][lvl1_idx] >
                                   ((long)minvar_16x16[blk64_idx][lvl1_idx] << 2))))
                    {
                        force_split[5 + blk64_scale_idx + lvl1_idx] = PART_EVAL_ONLY_SPLIT;
                        force_split[blk64_idx + 1] = PART_EVAL_ONLY_SPLIT;
                        force_split[0] = PART_EVAL_ONLY_SPLIT;
                    }
                }
            }
            if (force_split[1 + blk64_idx] == PART_EVAL_ALL)
            {
                fill_variance_tree(vt.split[blk64_idx], BLOCK_64X64);
                get_variance(vt.split[blk64_idx].part_variances.none);
                var_64x64 = vt.split[blk64_idx].part_variances.none.variance;
                max_var_64x64 = AOMMAX(var_64x64, max_var_64x64);
                min_var_64x64 = AOMMIN(var_64x64, min_var_64x64);
                // If the difference of the max-min variances of sub-blocks or max
                // variance of a sub-block is above some threshold of then force this
                // block to split. Only checking this for noise level >= medium, if
                // encoder is in SVC or if we already forced large blocks.
                long max_min_var_32x32_diff =
                    (long)max_var_32x32[blk64_idx] - min_var_32x32[blk64_idx];
                int check_max_var = B(max_var_32x32[blk64_idx] > thresholds[1] >> 1);
                bool check_noise_lvl = noise_level >= kMedium ||
                                       cpi.ppi.use_svc != 0 ||
                                       cpi.sf.rt_sf.prefer_large_partition_blocks != 0;
                long set_threshold = 3 * (thresholds[1] >> 3);

                if (!is_key_frame && max_min_var_32x32_diff > set_threshold &&
                    check_max_var != 0 && check_noise_lvl)
                {
                    force_split[1 + blk64_idx] = PART_EVAL_ONLY_SPLIT;
                    force_split[0] = PART_EVAL_ONLY_SPLIT;
                }
                avg_64x64 += var_64x64;
            }
            if (is_small_sb) force_split[0] = PART_EVAL_ONLY_SPLIT;
        }

        if (force_split[0] == PART_EVAL_ALL)
        {
            fill_variance_tree(vt, BLOCK_128X128);
            get_variance(vt.part_variances.none);
            long set_avg_64x64 = (9 * avg_64x64) >> 5;
            if (!is_key_frame && vt.part_variances.none.variance > set_avg_64x64)
                force_split[0] = PART_EVAL_ONLY_SPLIT;

            if (!is_key_frame &&
                ((long)max_var_64x64 - min_var_64x64) > 3 * (thresholds[0] >> 3) &&
                max_var_64x64 > thresholds[0] >> 1)
                force_split[0] = PART_EVAL_ONLY_SPLIT;
        }

        if (mi_col + 32 > tile.mi_col_end || mi_row + 32 > tile.mi_row_end ||
            set_vt_partitioning(cpi, xd, tile, vt, BLOCK_128X128, mi_row, mi_col,
                                thresholds[0], BLOCK_16X16, force_split[0]) == 0)
        {
            for (int blk64_idx = 0; blk64_idx < num_64x64_blocks; ++blk64_idx)
            {
                int x64_idx = GET_BLK_IDX_X(blk64_idx, 4);
                int y64_idx = GET_BLK_IDX_Y(blk64_idx, 4);
                int blk64_scale_idx = blk64_idx << 2;

                // Now go through the entire structure, splitting every block size until
                // we get to one that's got a variance lower than our threshold.
                if (set_vt_partitioning(cpi, xd, tile, vt.split[blk64_idx], BLOCK_64X64,
                                        mi_row + y64_idx, mi_col + x64_idx, thresholds[1],
                                        BLOCK_16X16, force_split[1 + blk64_idx]) != 0)
                    continue;
                for (int lvl1_idx = 0; lvl1_idx < 4; ++lvl1_idx)
                {
                    int x32_idx = GET_BLK_IDX_X(lvl1_idx, 3);
                    int y32_idx = GET_BLK_IDX_Y(lvl1_idx, 3);
                    int lvl1_scale_idx = (blk64_scale_idx + lvl1_idx) << 2;
                    if (set_vt_partitioning(
                            cpi, xd, tile, vt.split[blk64_idx].split[lvl1_idx],
                            BLOCK_32X32, (mi_row + y64_idx + y32_idx),
                            (mi_col + x64_idx + x32_idx), thresholds[2], BLOCK_16X16,
                            force_split[5 + blk64_scale_idx + lvl1_idx]) != 0)
                        continue;
                    for (int lvl2_idx = 0; lvl2_idx < 4; ++lvl2_idx)
                    {
                        int x16_idx = GET_BLK_IDX_X(lvl2_idx, 2);
                        int y16_idx = GET_BLK_IDX_Y(lvl2_idx, 2);
                        int split_index = 21 + lvl1_scale_idx + lvl2_idx;
                        VP16x16 vtemp =
                            vt.split[blk64_idx].split[lvl1_idx].split[lvl2_idx];
                        if (set_vt_partitioning(cpi, xd, tile, vtemp, BLOCK_16X16,
                                                mi_row + y64_idx + y32_idx + y16_idx,
                                                mi_col + x64_idx + x32_idx + x16_idx,
                                                thresholds[3], BLOCK_8X8,
                                                force_split[split_index]) != 0)
                            continue;
                        for (int lvl3_idx = 0; lvl3_idx < 4; ++lvl3_idx)
                        {
                            int x8_idx = GET_BLK_IDX_X(lvl3_idx, 1);
                            int y8_idx = GET_BLK_IDX_Y(lvl3_idx, 1);
                            set_block_size(cpi, (mi_row + y64_idx + y32_idx + y16_idx + y8_idx),
                                           (mi_col + x64_idx + x32_idx + x16_idx + x8_idx),
                                           BLOCK_8X8);
                        }
                    }
                }
            }
        }

        if (cpi.sf.rt_sf.short_circuit_low_temp_var != 0)
        {
            set_low_temp_var_flag(cpi, x.part_search_info, xd, vt, thresholds,
                                  ref_frame_partition, mi_col, mi_row, is_small_sb);
        }

        // aom_free(vt): vt is per-thread scratch here.
        return 0;
    }
}
