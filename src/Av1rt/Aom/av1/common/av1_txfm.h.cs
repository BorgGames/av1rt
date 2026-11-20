// Port of libaom av1/common/av1_txfm.h

// <gen:aliases>
global using TXFM_TYPE = int;
// </gen:aliases>

global using static Av1rt.Aom.av1_txfm;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal delegate void TxfmFunc(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
                                Ptr<sbyte> stage_range);

internal delegate void FwdTxfm2dFunc(Ptr<short> input, Ptr<int> output, int stride,
                                     TX_TYPE tx_type, int bd);

// [port] Struct: callers keep it as a local and fill it through a pointer
// (av1_get_fwd_txfm_cfg / av1_get_inv_txfm_cfg). stage_range_col/row are only indexed and
// copied into, so they are inline arrays (use span CopyTo/Clear for memcpy/av1_zero).
internal partial struct TXFM_2D_FLIP_CFG
{
    public TX_SIZE tx_size;
    public int ud_flip;  // flip upside down
    public int lr_flip;  // flip left to right
    public Ptr<sbyte> shift;
    public sbyte cos_bit_col;
    public sbyte cos_bit_row;
    public Arr12<sbyte> stage_range_col; // [MAX_TXFM_STAGE_NUM]
    public Arr12<sbyte> stage_range_row; // [MAX_TXFM_STAGE_NUM]
    public TXFM_TYPE txfm_type_col;
    public TXFM_TYPE txfm_type_row;
    public int stage_num_col;
    public int stage_num_row;
}

internal static partial class av1_txfm
{
    // <gen:consts>
    internal const int DO_RANGE_CHECK_CLAMP = 0;
    internal const int MAX_TXFM_STAGE_NUM = 12;
    internal const int NewSqrt2Bits = ((int)12);
    internal const int MAX_TXWH_IDX = 5;

    // TXFM_TYPE
    internal const int TXFM_TYPE_DCT4 = 0;
    internal const int TXFM_TYPE_DCT8 = 1;
    internal const int TXFM_TYPE_DCT16 = 2;
    internal const int TXFM_TYPE_DCT32 = 3;
    internal const int TXFM_TYPE_DCT64 = 4;
    internal const int TXFM_TYPE_ADST4 = 5;
    internal const int TXFM_TYPE_ADST8 = 6;
    internal const int TXFM_TYPE_ADST16 = 7;
    internal const int TXFM_TYPE_IDENTITY4 = 8;
    internal const int TXFM_TYPE_IDENTITY8 = 9;
    internal const int TXFM_TYPE_IDENTITY16 = 10;
    internal const int TXFM_TYPE_IDENTITY32 = 11;
    internal const int TXFM_TYPES = 12;
    internal const int TXFM_TYPE_INVALID = 13;

    // int32_t av1_cospi_arr_data_inline[4][64]
    private static readonly int[][] av1_cospi_arr_data_inline = {
        new int[] {
            1024, 1024, 1023, 1021, 1019, 1016, 1013, 1009, 1004, 999, 993, 987, 980, 972, 964,
            955, 946, 936, 926, 915, 903, 891, 878, 865, 851, 837, 822, 807, 792, 775, 759, 742,
            724, 706, 688, 669, 650, 630, 610, 590, 569, 548, 526, 505, 483, 460, 438, 415, 392,
            369, 345, 321, 297, 273, 249, 224, 200, 175, 150, 125, 100, 75, 50, 25
        },
        new int[] {
            2048, 2047, 2046, 2042, 2038, 2033, 2026, 2018, 2009, 1998, 1987, 1974, 1960, 1945,
            1928, 1911, 1892, 1872, 1851, 1829, 1806, 1782, 1757, 1730, 1703, 1674, 1645, 1615,
            1583, 1551, 1517, 1483, 1448, 1412, 1375, 1338, 1299, 1260, 1220, 1179, 1138, 1096,
            1053, 1009, 965, 921, 876, 830, 784, 737, 690, 642, 595, 546, 498, 449, 400, 350, 301,
            251, 201, 151, 100, 50
        },
        new int[] {
            4096, 4095, 4091, 4085, 4076, 4065, 4052, 4036, 4017, 3996, 3973, 3948, 3920, 3889,
            3857, 3822, 3784, 3745, 3703, 3659, 3612, 3564, 3513, 3461, 3406, 3349, 3290, 3229,
            3166, 3102, 3035, 2967, 2896, 2824, 2751, 2675, 2598, 2520, 2440, 2359, 2276, 2191,
            2106, 2019, 1931, 1842, 1751, 1660, 1567, 1474, 1380, 1285, 1189, 1092, 995, 897, 799,
            700, 601, 501, 401, 301, 201, 101
        },
        new int[] {
            8192, 8190, 8182, 8170, 8153, 8130, 8103, 8071, 8035, 7993, 7946, 7895, 7839, 7779,
            7713, 7643, 7568, 7489, 7405, 7317, 7225, 7128, 7027, 6921, 6811, 6698, 6580, 6458,
            6333, 6203, 6070, 5933, 5793, 5649, 5501, 5351, 5197, 5040, 4880, 4717, 4551, 4383,
            4212, 4038, 3862, 3683, 3503, 3320, 3135, 2948, 2760, 2570, 2378, 2185, 1990, 1795,
            1598, 1401, 1202, 1003, 803, 603, 402, 201
        }
    };

    // int32_t av1_sinpi_arr_data_inline[4][5]
    private static readonly int[][] av1_sinpi_arr_data_inline = {
        new int[] { 0, 330, 621, 836, 951 },
        new int[] { 0, 660, 1241, 1672, 1901 },
        new int[] { 0, 1321, 2482, 3344, 3803 },
        new int[] { 0, 2642, 4964, 6689, 7606 }
    };
    // </gen:consts>

    // av1_cospi_arr_data[4][64] and av1_sinpi_arr_data[4][5] are defined in av1_txfm.c
    // (not part of p1a); the inline copies below are what cospi_arr/sinpi_arr use.

    internal const int cos_bit_min = 10;

    // 2^12 * sqrt(2)
    internal const int NewSqrt2 = 5793;
    // 2^12 / sqrt(2)
    internal const int NewInvSqrt2 = 2896;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<int> cospi_arr(int n)
    {
        return av1_cospi_arr_data_inline[n - cos_bit_min];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Ptr<int> sinpi_arr(int n)
    {
        return av1_sinpi_arr_data_inline[n - cos_bit_min];
    }

    // The reduced bit-width and permuted arrays are only used in the Arm Neon
    // implementations in av1_fwd_txfm2d_neon.c and highbd_fwd_txfm_neon.c for now.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int range_check_value(int value, sbyte bit)
    {
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long range_check_value64(long value, sbyte bit)
    {
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int round_shift(long value, int bit)
    {
        Debug.Assert(bit >= 1);
        return (int)((value + (1L << (bit - 1))) >> bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int half_btf(int w0, int in0, int w1, int in1,
                                 int bit)
    {
        long result_64 = (long)(w0 * in0) + (long)(w1 * in1);
        long intermediate = result_64 + (1L << (bit - 1));
        // NOTE(rachelbarker): The value 'result_64' may not necessarily fit
        // into 32 bits. However, the result of this function is nominally
        // ROUND_POWER_OF_TWO_64(result_64, bit)
        // and that is required to fit into stage_range[stage] many bits
        // (checked by range_check_buf()).
        //
        // Here we've unpacked that rounding operation, and it can be shown
        // that the value of 'intermediate' here *does* fit into 32 bits
        // for any conformant bitstream.
        // The upshot is that, if you do all this calculation using
        // wrapping 32-bit arithmetic instead of (non-wrapping) 64-bit arithmetic,
        // then you'll still get the correct result.
        // To provide a check on this logic, we assert that 'intermediate'
        // would fit into an int32 if range checking is enabled.
        return (int)(intermediate >> bit);
    }

    // [port] Kept although it is a high-bitdepth helper: the C (non-SIMD) av1_inv_txfm_add_c
    // reconstructs 8-bit blocks through the highbd 2-D inverse transform on a uint16_t copy of
    // dst, so this runs in the 8-bit realtime path.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ushort highbd_clip_pixel_add(ushort dest, tran_high_t trans,
                                                 int bd)
    {
        return clip_pixel_highbd(dest + (int)trans, bd);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void get_flip_cfg(TX_TYPE tx_type, ref int ud_flip, ref int lr_flip)
    {
        Debug.Assert((uint)tx_type < TX_TYPES);
        // ud_flip is 1 for FLIPADST_DCT (4), FLIPADST_FLIPADST (6),
        // FLIPADST_ADST (8), V_FLIPADST (14) -> mask 0x4150.
        // lr_flip is 1 for DCT_FLIPADST (5), FLIPADST_FLIPADST (6),
        // ADST_FLIPADST (7), H_FLIPADST (15) -> mask 0x80E0.
        ud_flip = (0x4150 >> tx_type) & 1;
        lr_flip = (0x80E0 >> tx_type) & 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void set_flip_cfg(TX_TYPE tx_type, ref TXFM_2D_FLIP_CFG cfg)
    {
        get_flip_cfg(tx_type, ref cfg.ud_flip, ref cfg.lr_flip);
    }

    // Utility function that returns the log of the ratio of the col and row
    // sizes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_rect_tx_log_ratio(int col, int row)
    {
        if (col == row) return 0;
        if (col > row)
        {
            if (col == row * 2) return 1;
            if (col == row * 4) return 2;
            Debug.Assert(false, "Unsupported transform size");
        }
        else
        {
            if (row == col * 2) return -1;
            if (row == col * 4) return -2;
            Debug.Assert(false, "Unsupported transform size");
        }
        return 0;  // Invalid
    }

    // av1_txfm_type_ls[5][TX_TYPES_1D] and av1_txfm_stage_num_list[TXFM_TYPES] are defined
    // in av1_txfm.c (not part of p1a).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_txw_idx(TX_SIZE tx_size)
    {
        return tx_size_wide_log2[tx_size] - tx_size_wide_log2[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_txh_idx(TX_SIZE tx_size)
    {
        return tx_size_high_log2[tx_size] - tx_size_high_log2[0];
    }
}
