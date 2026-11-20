// Port of libaom av1/common/av1_inv_txfm1d_cfg.h

global using static Av1rt.Aom.av1_inv_txfm1d_cfg;

namespace Av1rt.Aom;

internal static partial class av1_inv_txfm1d_cfg
{
    // <gen:consts>
    internal const int INV_COS_BIT = 12;

    // int8_t inv_start_range[TX_SIZES_ALL]
    internal static readonly sbyte[] inv_start_range = {
        5, 6, 7, 7, 7, 5, 5, 6, 6, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7
    };

    // int8_t av1_inv_txfm_shift_ls[TX_SIZES_ALL][2]
    internal static readonly sbyte[][] av1_inv_txfm_shift_ls = {
        new sbyte[] { 0, -4 },
        new sbyte[] { -1, -4 },
        new sbyte[] { -2, -4 },
        new sbyte[] { -2, -4 },
        new sbyte[] { -2, -4 },
        new sbyte[] { 0, -4 },
        new sbyte[] { 0, -4 },
        new sbyte[] { -1, -4 },
        new sbyte[] { -1, -4 },
        new sbyte[] { -1, -4 },
        new sbyte[] { -1, -4 },
        new sbyte[] { -1, -4 },
        new sbyte[] { -1, -4 },
        new sbyte[] { -1, -4 },
        new sbyte[] { -1, -4 },
        new sbyte[] { -2, -4 },
        new sbyte[] { -2, -4 },
        new sbyte[] { -2, -4 },
        new sbyte[] { -2, -4 }
    };
    // </gen:consts>
}
