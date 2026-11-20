// Port of libaom av1/common/scan.h

// <gen:aliases>
global using SCAN_MODE = int;
// </gen:aliases>

global using static Av1rt.Aom.scan;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class scan
{
    // <gen:consts>
    internal const int MAX_NEIGHBORS = 2;

    // SCAN_MODE
    internal const int SCAN_MODE_ZIG_ZAG = 0;
    internal const int SCAN_MODE_COL_DIAG = 1;
    internal const int SCAN_MODE_ROW_DIAG = 2;
    internal const int SCAN_MODE_COL_1D = 3;
    internal const int SCAN_MODE_ROW_1D = 4;
    internal const int SCAN_MODES = 5;
    // </gen:consts>

    // av1_scan_orders[TX_SIZES_ALL][TX_TYPES] is defined in scan.c (not part of p1a).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SCAN_ORDER get_default_scan(TX_SIZE tx_size,
                                                TX_TYPE tx_type)
    {
        return av1_scan_orders[tx_size][tx_type];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SCAN_ORDER get_scan(TX_SIZE tx_size, TX_TYPE tx_type)
    {
        return get_default_scan(tx_size, tx_type);
    }
}
