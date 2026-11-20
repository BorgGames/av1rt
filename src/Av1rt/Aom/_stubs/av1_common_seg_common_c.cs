// Stubs for av1/common/seg_common.c: delete this file when av1/common/seg_common.c is ported.
global using static Av1rt.Aom.stubs_av1_common_seg_common_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_common_seg_common_c
{
    // av1/common/seg_common.c (e1)
    internal static void av1_clearall_segfeatures(segmentation seg) =>
        throw new NotImplementedException();

    internal static void av1_enable_segfeature(segmentation seg, int segment_id,
                                               SEG_LVL_FEATURES feature_id) =>
        throw new NotImplementedException();

    internal static void av1_set_segdata(segmentation seg, int segment_id,
                                         SEG_LVL_FEATURES feature_id, int seg_data) =>
        throw new NotImplementedException();
}
