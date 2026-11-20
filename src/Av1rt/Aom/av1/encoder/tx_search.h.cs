// Port of libaom av1/encoder/tx_search.h
global using FAST_TX_SEARCH_MODE = int;
global using static Av1rt.Aom.tx_search;

namespace Av1rt.Aom;

internal static partial class tx_search
{
    // Set this macro as 1 to collect data about tx size selection.
    internal const int COLLECT_TX_SIZE_DATA = 0;

    // enum FAST_TX_SEARCH_MODE
    internal const int FTXS_NONE = 0;
    internal const int FTXS_DCT_AND_1D_DCT_ONLY = 1 << 0;
    internal const int FTXS_DISABLE_TRELLIS_OPT = 1 << 1;
    internal const int FTXS_USE_TRANSFORM_DOMAIN = 1 << 2;

    // [subset] tx_size_cost: only used by the rd transform search. (The executed reference in
    // rd.c is the ModeCosts::tx_size_cost field, not this function.)
}
