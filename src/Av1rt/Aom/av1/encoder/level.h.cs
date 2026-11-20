// Port of libaom av1/encoder/level.h
global using static Av1rt.Aom.level;

namespace Av1rt.Aom;

// Level conformance tracking (av1_init_level_info / av1_update_level_info /
// av1_get_seq_level_idx in level.c) is out of the subset: keep_level_stats is 0
// unless a target level is configured. Only the parameter block that
// encoder.c / av1_cx_iface.c initialise is ported.
internal static partial class level
{
    // [subset] AV1LevelSpec, AV1LevelStats, FrameWindowBuffer, FrameRecord,
    // DECODER_MODEL*, TARGET_LEVEL_FAIL_IDS and the level.c prototypes are only
    // used by the level conformance tracking.
}

internal sealed partial class AV1LevelParams
{
    // Specifies the level that the coded video sequence conforms to for each
    // operating point.
    public readonly AV1_LEVEL[] target_seq_level_idx = new AV1_LEVEL[MAX_NUM_OPERATING_POINTS];
    // Bit mask to indicate whether to keep level stats for corresponding
    // operating points.
    public uint keep_level_stats;
    // Level information for each operating point.
    public readonly AV1LevelInfo[] level_info = new AV1LevelInfo[MAX_NUM_OPERATING_POINTS];
}

// PORT-NOTE: placeholder. The C AV1LevelInfo (level_spec, level_stats,
// decoder_models[], frame_window_buffer) only serves level conformance
// tracking, which is out of the subset. encoder.c allocates one per operating
// point when keep_level_stats has the bit set, and frees them on teardown.
internal sealed partial class AV1LevelInfo
{
}
