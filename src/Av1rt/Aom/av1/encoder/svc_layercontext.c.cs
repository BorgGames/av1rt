// Port of libaom av1/encoder/svc_layercontext.c

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

// [subset] SVC is out of the subset; svc_layercontext.c has no owning package. Only
// av1_svc_set_last_source is ported (by g1): av1_encode_strategy calls it for a single-layer
// stream after a dropped frame (rc.prev_frame_is_dropped), which depends on the bitrate, not on
// the configuration. Its spatial-layer branches need use_svc and are dropped.
internal static partial class svc_layercontext
{
    internal static void av1_svc_set_last_source(AV1_COMP cpi, EncodeFrameInput frame_input,
                                                 YV12_BUFFER_CONFIG prev_source)
    {
        frame_input.last_source = prev_source != null ? prev_source : null;
        if (cpi.svc.source_last_TL0.buffer_alloc_sz == 0) return;
        if (cpi.ppi.use_svc == 0 && cpi.rc.prev_frame_is_dropped != 0 &&
            cpi.rc.frame_number_encoded > 0)
        {
            frame_input.last_source = cpi.svc.source_last_TL0;
        }
        else
        {
            // [subset] The spatial-layer cases (base layer whose LAST reference is not the
            // previous superframe, enhancement layers) need use_svc. For a single layer without
            // a dropped previous frame and spatial_layer_id == 0, C only looks at
            // current_superframe, which is never advanced without SVC, so nothing changes.
            if (cpi.ppi.use_svc != 0)
                throw new NotSupportedException("SVC is not supported");
            Debug.Assert(cpi.svc.spatial_layer_id == 0);
            if (cpi.svc.current_superframe > 0)
                throw new NotSupportedException("SVC is not supported");
        }
    }
}
