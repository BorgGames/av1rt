// Port of libaom av1/common/seg_common.c

using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class seg_common
{
    // <gen:tables>
    // int seg_feature_data_signed[SEG_LVL_MAX]
    private static readonly int[] seg_feature_data_signed = {
        1, 1, 1, 1, 1, 0, 0, 0
    };

    // int seg_feature_data_max[SEG_LVL_MAX]
    private static readonly int[] seg_feature_data_max = {
        MAXQ, MAX_LOOP_FILTER, MAX_LOOP_FILTER, MAX_LOOP_FILTER, MAX_LOOP_FILTER, 7, 0, 0
    };
    // </gen:tables>

    // These functions provide access to new segment level features.
    // Eventually these function may be "optimized out" but for the moment,
    // the coding mechanism is still subject to change so these provide a
    // convenient single point of change.

    internal static void av1_clearall_segfeatures(segmentation seg)
    {
        av1_zero(seg.feature_data);
        av1_zero(seg.feature_mask);
    }

    internal static void av1_calculate_segdata(segmentation seg)
    {
        seg.segid_preskip = 0;
        seg.last_active_segid = 0;
        for (int i = 0; i < MAX_SEGMENTS; i++)
        {
            for (int j = 0; j < SEG_LVL_MAX; j++)
            {
                if ((seg.feature_mask[i] & (1u << j)) != 0)
                {
                    seg.segid_preskip |= (byte)B(j >= SEG_LVL_REF_FRAME);
                    seg.last_active_segid = i;
                }
            }
        }
    }

    internal static void av1_enable_segfeature(segmentation seg, int segment_id,
                                               SEG_LVL_FEATURES feature_id)
    {
        seg.feature_mask[segment_id] |= 1u << feature_id;
    }

    internal static int av1_seg_feature_data_max(SEG_LVL_FEATURES feature_id)
    {
        return seg_feature_data_max[feature_id];
    }

    internal static int av1_is_segfeature_signed(SEG_LVL_FEATURES feature_id)
    {
        return seg_feature_data_signed[feature_id];
    }

    // The 'seg_data' given for each segment can be either deltas (from the default
    // value chosen for the frame) or absolute values.
    //
    // Valid range for abs values is (0-127 for MB_LVL_ALT_Q), (0-63 for
    // SEGMENT_ALT_LF)
    // Valid range for delta values are (+/-127 for MB_LVL_ALT_Q), (+/-63 for
    // SEGMENT_ALT_LF)
    //
    // abs_delta = SEGMENT_DELTADATA (deltas) abs_delta = SEGMENT_ABSDATA (use
    // the absolute values given).

    internal static void av1_set_segdata(segmentation seg, int segment_id,
                                         SEG_LVL_FEATURES feature_id, int seg_data)
    {
        if (seg_data < 0)
        {
            Debug.Assert(seg_feature_data_signed[feature_id] != 0);
            Debug.Assert(-seg_data <= seg_feature_data_max[feature_id]);
        }
        else
        {
            Debug.Assert(seg_data <= seg_feature_data_max[feature_id]);
        }

        seg.feature_data[segment_id][feature_id] = (short)seg_data;
    }

    // TBD? Functions to read and write segment data with range / validity checking
}
