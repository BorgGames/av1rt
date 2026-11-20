// Port of libaom av1/encoder/encode_strategy.h
global using static Av1rt.Aom.encode_strategy;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class encode_strategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int is_frame_droppable(
        RTC_REF rtc_ref,
        in ExtRefreshFrameFlagsInfo ext_refresh_frame_flags)
    {
        // Droppable frame is only used by external refresh flags. VoD setting won't
        // trigger its use case.
        if (rtc_ref.set_ref_frame_config != 0)
            return rtc_ref.non_reference_frame;
        else if (ext_refresh_frame_flags.update_pending)
            return B(!(ext_refresh_frame_flags.alt_ref_frame ||
                       ext_refresh_frame_flags.alt2_ref_frame ||
                       ext_refresh_frame_flags.bwd_ref_frame ||
                       ext_refresh_frame_flags.golden_frame ||
                       ext_refresh_frame_flags.last_frame));
        else
            return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_current_frame_ref_type(AV1_COMP cpi)
    {
        // We choose the reference "type" of this frame from the flags which indicate
        // which reference frames will be refreshed by it. More than one of these
        // flags may be set, so the order here implies an order of precedence. This is
        // just used to choose the primary_ref_frame (as the most recent reference
        // buffer of the same reference-type as the current frame).

        switch (cpi.ppi.gf_group.layer_depth[cpi.gf_frame_index])
        {
            case 0: return 0;
            case 1: return 1;
            case MAX_ARF_LAYERS:
            case MAX_ARF_LAYERS + 1: return 4;
            default: return 7;
        }
    }
}
