// Port of libaom av1/encoder/interp_search.h
global using static Av1rt.Aom.interp_search;

namespace Av1rt.Aom;

internal static partial class interp_search
{
    internal const int MAX_INTERP_FILTER_STATS = 128;
    internal const int DUAL_FILTER_SET_SIZE = (SWITCHABLE_FILTERS * SWITCHABLE_FILTERS);

    // [subset] INTERPOLATION_FILTER_STATS, HandleInterModeArgs and filter_sets: only used by
    // the rd inter mode search (rdopt.c / interp_search.c).
}
