// Port of libaom av1/encoder/av1_fwd_txfm1d_cfg.h
global using static Av1rt.Aom.av1_fwd_txfm1d_cfg;

namespace Av1rt.Aom;

internal static partial class av1_fwd_txfm1d_cfg
{
    internal static readonly sbyte[][] av1_fwd_txfm_shift_ls = {
        new sbyte[] { 2, 0, 0 },
        new sbyte[] { 2, -1, 0 },
        new sbyte[] { 2, -2, 0 },
        new sbyte[] { 2, -4, 0 },
        new sbyte[] { 0, -2, -2 },
        new sbyte[] { 2, -1, 0 },
        new sbyte[] { 2, -1, 0 },
        new sbyte[] { 2, -2, 0 },
        new sbyte[] { 2, -2, 0 },
        new sbyte[] { 2, -4, 0 },
        new sbyte[] { 2, -4, 0 },
        new sbyte[] { 0, -2, -2 },
        new sbyte[] { 2, -4, -2 },
        new sbyte[] { 2, -1, 0 },
        new sbyte[] { 2, -1, 0 },
        new sbyte[] { 2, -2, 0 },
        new sbyte[] { 2, -2, 0 },
        new sbyte[] { 0, -2, 0 },
        new sbyte[] { 2, -4, 0 },
    };

    internal static readonly sbyte[][] av1_fwd_cos_bit_col = {
        new sbyte[] { 13, 13, 13, 0, 0 },
        new sbyte[] { 13, 13, 13, 12, 0 },
        new sbyte[] { 13, 13, 13, 12, 13 },
        new sbyte[] { 0, 13, 13, 12, 13 },
        new sbyte[] { 0, 0, 13, 12, 13 },
    };

    internal static readonly sbyte[][] av1_fwd_cos_bit_row = {
        new sbyte[] { 13, 13, 12, 0, 0 },
        new sbyte[] { 13, 13, 13, 12, 0 },
        new sbyte[] { 13, 13, 12, 13, 12 },
        new sbyte[] { 0, 12, 13, 12, 11 },
        new sbyte[] { 0, 0, 12, 11, 10 },
    };
}
