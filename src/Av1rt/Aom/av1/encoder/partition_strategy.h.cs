// Port of libaom av1/encoder/partition_strategy.h
global using static Av1rt.Aom.partition_strategy;

namespace Av1rt.Aom;

internal static partial class partition_strategy
{
    // [subset] Everything in this header serves the rd partition search or
    // !CONFIG_REALTIME_ONLY code: the ML partition pruning prototypes,
    // set_offsets_for_motion_search (simple motion search), is_full_sb,
    // use_auto_max_partition / dim_to_size / set_max_min_partition_size (!CONFIG_REALTIME_ONLY)
    // and the CONFIG_PARTITION_SEARCH_ORDER helpers.
}
