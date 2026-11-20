// Port of libaom av1/encoder/segmentation.c

namespace Av1rt.Aom;

// [port] The class is segmentation_h (see segmentation.h.cs): `segmentation` is p1a's
// struct segmentation type.
// [subset] av1_clear_segdata never runs in the subset (static segment features for alt-ref
// frames, encoder_utils.c) and is not ported.
internal static partial class segmentation_h
{
    internal static void av1_enable_segmentation(segmentation seg)
    {
        seg.enabled = 1;
        seg.update_map = 1;
        seg.update_data = 1;
        seg.temporal_update = 0;
    }

    internal static void av1_disable_segmentation(segmentation seg)
    {
        seg.enabled = 0;
        seg.update_map = 0;
        seg.update_data = 0;
        seg.temporal_update = 0;
    }

    internal static void av1_reuse_ref_segment_features(AV1_COMMON cm)
    {
        segmentation seg = cm.seg;
        RefCntBuffer @ref = cm.prev_frame;
        // Frames without a primary reference frame always send the features.
        if (seg.update_data == 0 || cm.features.primary_ref_frame == PRIMARY_REF_NONE ||
            @ref == null || @ref.seg.enabled == 0)
        {
            return;
        }
        for (int i = 0; i < MAX_SEGMENTS; i++)
        {
            if (seg.feature_mask[i] != @ref.seg.feature_mask[i]) return;
            for (int j = 0; j < SEG_LVL_MAX; j++)
            {
                if (segfeature_active(seg, (byte)i, j) != 0 &&
                    get_segdata(seg, i, j) != get_segdata(@ref.seg, i, j))
                {
                    return;
                }
            }
        }
        seg.update_data = 0;
    }

    internal static void av1_disable_segfeature(segmentation seg, int segment_id,
                                                SEG_LVL_FEATURES feature_id)
    {
        seg.feature_mask[segment_id] &= ~(1u << feature_id);
    }

    internal static void av1_reset_segment_features(AV1_COMMON cm)
    {
        segmentation seg = cm.seg;

        // Set up default state for MB feature flags
        seg.enabled = 0;
        seg.update_map = 0;
        seg.update_data = 0;
        av1_clearall_segfeatures(seg);
    }
}
