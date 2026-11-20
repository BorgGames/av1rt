// Port of libaom av1/encoder/temporal_filter.h
global using static Av1rt.Aom.temporal_filter;

namespace Av1rt.Aom;

// Temporal filtering is out of the subset (no ARFs in the realtime lag-0 path).
internal static partial class temporal_filter
{
    // Block size used in temporal filtering.
    internal const int TF_BLOCK_SIZE = BLOCK_64X64;

    // [subset] BH/BW, TF_* tuning constants, TemporalFilterCtx,
    // TemporalFilterData, TEMPORAL_FILTER_INFO, AV1TemporalFilterSync and the
    // temporal_filter.c prototypes.
}
