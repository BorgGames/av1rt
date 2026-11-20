// Port of libaom av1/common/seg_common.h

// <gen:aliases>
global using SEG_LVL_FEATURES = int;
// </gen:aliases>

global using static Av1rt.Aom.seg_common;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] Class with CopyFrom: embedded in AV1_COMMON and copied whole by the encoder
// (e.g. cm->seg saved/restored around the frame size search).
internal sealed partial class segmentation
{
    public byte enabled;
    public byte update_map;
    public byte update_data;
    public byte temporal_update;

    public readonly short[][] feature_data = new_jagged<short>(MAX_SEGMENTS, SEG_LVL_MAX);
    public readonly uint[] feature_mask = new uint[MAX_SEGMENTS];
    public int last_active_segid;  // The highest numbered segment id that has some
                                   // enabled feature.
    public byte segid_preskip;  // Whether the segment id will be read before the
                                // skip syntax element.
                                // 1: the segment id will be read first.
                                // 0: the skip syntax element will be read first.

    /// <summary>C struct assignment: deep copy of every field.</summary>
    public void CopyFrom(segmentation other)
    {
        enabled = other.enabled;
        update_map = other.update_map;
        update_data = other.update_data;
        temporal_update = other.temporal_update;
        av1_copy(feature_data, other.feature_data);
        av1_copy(feature_mask, other.feature_mask);
        last_active_segid = other.last_active_segid;
        segid_preskip = other.segid_preskip;
    }

    /// <summary>memset(seg, 0, sizeof(*seg)) / av1_zero(*seg).</summary>
    public void Clear()
    {
        enabled = 0;
        update_map = 0;
        update_data = 0;
        temporal_update = 0;
        av1_zero(feature_data);
        av1_zero(feature_mask);
        last_active_segid = 0;
        segid_preskip = 0;
    }
}

// <gen:classes>
internal sealed partial class segmentation_probs
{
    public readonly ushort[][] pred_cdf = new_jagged<ushort>(SEG_TEMPORAL_PRED_CTXS, CDF_SIZE(2));
    public readonly ushort[][] spatial_pred_seg_cdf = new_jagged<ushort>(SPATIAL_PREDICTION_PROBS, CDF_SIZE(MAX_SEGMENTS));

    /// <summary>C struct assignment (<c>*this = *other</c>): deep copy of every field.</summary>
    public void CopyFrom(segmentation_probs other)
    {
        av1_copy(pred_cdf, other.pred_cdf);
        av1_copy(spatial_pred_seg_cdf, other.spatial_pred_seg_cdf);
    }
}
// </gen:classes>

internal static partial class seg_common
{
    // <gen:consts>
    internal const int MAX_SEGMENTS = 8;
    internal const int SEG_TREE_PROBS = (MAX_SEGMENTS - 1);
    internal const int SEG_TEMPORAL_PRED_CTXS = 3;
    internal const int SPATIAL_PREDICTION_PROBS = 3;

    // SEG_LVL_FEATURES
    internal const int SEG_LVL_ALT_Q = 0;
    internal const int SEG_LVL_ALT_LF_Y_V = 1;
    internal const int SEG_LVL_ALT_LF_Y_H = 2;
    internal const int SEG_LVL_ALT_LF_U = 3;
    internal const int SEG_LVL_ALT_LF_V = 4;
    internal const int SEG_LVL_REF_FRAME = 5;
    internal const int SEG_LVL_SKIP = 6;
    internal const int SEG_LVL_GLOBALMV = 7;
    internal const int SEG_LVL_MAX = 8;
    // </gen:consts>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int segfeature_active(segmentation seg,
                                          byte segment_id,
                                          SEG_LVL_FEATURES feature_id)
    {
        return B(seg.enabled != 0 && (seg.feature_mask[segment_id] & (1u << feature_id)) != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void segfeatures_copy(segmentation dst,
                                          segmentation src)
    {
        int i, j;
        for (i = 0; i < MAX_SEGMENTS; i++)
        {
            dst.feature_mask[i] = src.feature_mask[i];
            for (j = 0; j < SEG_LVL_MAX; j++)
            {
                dst.feature_data[i][j] = src.feature_data[i][j];
            }
        }
        dst.segid_preskip = src.segid_preskip;
        dst.last_active_segid = src.last_active_segid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_segdata(segmentation seg, int segment_id,
                                    SEG_LVL_FEATURES feature_id)
    {
        return seg.feature_data[segment_id][feature_id];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_segment_id(Ptr<byte> segment_ids, int mi_offset,
                                        int x_mis, int y_mis, int mi_stride,
                                        byte segment_id)
    {
        segment_ids += mi_offset;
        for (int y = 0; y < y_mis; ++y)
        {
            memset(segment_ids + y * mi_stride, segment_id, x_mis);
        }
    }
}
