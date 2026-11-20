// Port of libaom av1/common/common_data.h
// The tables are generated from the C initializers by tools/gen/ctables.py.

global using static Av1rt.Aom.common_data;

using System;

namespace Av1rt.Aom;

// [port] C declares partition_context_lookup with an anonymous struct type; this names it.
internal partial struct partition_context_lookup_entry
{
    public PARTITION_CONTEXT above;
    public PARTITION_CONTEXT left;
}

internal static partial class common_data
{
    // av1_ss_size_lookup[BLOCK_SIZES_ALL][2][2] is defined in common_data.c (not part of p1a).

    // <gen:consts>
    internal const int TXSIZE_CAT_INVALID = (-1);
    internal const int TX_SIZE_W_MIN = 4;
    internal const int TX_SIZE_H_MIN = 4;

    // uint8_t mi_size_wide_log2[BLOCK_SIZES_ALL]
    internal static readonly byte[] mi_size_wide_log2 = {
        0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 0, 2, 1, 3, 2, 4
    };

    // uint8_t mi_size_high_log2[BLOCK_SIZES_ALL]
    internal static readonly byte[] mi_size_high_log2 = {
        0, 1, 0, 1, 2, 1, 2, 3, 2, 3, 4, 3, 4, 5, 4, 5, 2, 0, 3, 1, 4, 2
    };

    // uint8_t mi_size_wide[BLOCK_SIZES_ALL]
    internal static readonly byte[] mi_size_wide = {
        1, 1, 2, 2, 2, 4, 4, 4, 8, 8, 8, 16, 16, 16, 32, 32, 1, 4, 2, 8, 4, 16
    };

    // uint8_t mi_size_high[BLOCK_SIZES_ALL]
    internal static readonly byte[] mi_size_high = {
        1, 2, 1, 2, 4, 2, 4, 8, 4, 8, 16, 8, 16, 32, 16, 32, 4, 1, 8, 2, 16, 4
    };

    // uint8_t block_size_wide[BLOCK_SIZES_ALL]
    internal static readonly byte[] block_size_wide = {
        4, 4, 8, 8, 8, 16, 16, 16, 32, 32, 32, 64, 64, 64, 128, 128, 4, 16, 8, 32, 16, 64
    };

    // uint8_t block_size_high[BLOCK_SIZES_ALL]
    internal static readonly byte[] block_size_high = {
        4, 8, 4, 8, 16, 8, 16, 32, 16, 32, 64, 32, 64, 128, 64, 128, 16, 4, 32, 8, 64, 16
    };

    // uint8_t size_group_lookup[BLOCK_SIZES_ALL]
    internal static readonly byte[] size_group_lookup = {
        0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 0, 0, 1, 1, 2, 2
    };

    // uint8_t num_pels_log2_lookup[BLOCK_SIZES_ALL]
    internal static readonly byte[] num_pels_log2_lookup = {
        4, 5, 5, 6, 7, 7, 8, 9, 9, 10, 11, 11, 12, 13, 13, 14, 6, 6, 8, 8, 10, 10
    };

    // BLOCK_SIZE subsize_lookup[EXT_PARTITION_TYPES][SQR_BLOCK_SIZES]
    internal static readonly BLOCK_SIZE[][] subsize_lookup = {
        new BLOCK_SIZE[] {
            BLOCK_4X4, BLOCK_8X8, BLOCK_16X16, BLOCK_32X32, BLOCK_64X64, BLOCK_128X128
        },
        new BLOCK_SIZE[] {
            BLOCK_INVALID, BLOCK_8X4, BLOCK_16X8, BLOCK_32X16, BLOCK_64X32, BLOCK_128X64
        },
        new BLOCK_SIZE[] {
            BLOCK_INVALID, BLOCK_4X8, BLOCK_8X16, BLOCK_16X32, BLOCK_32X64, BLOCK_64X128
        },
        new BLOCK_SIZE[] {
            BLOCK_INVALID, BLOCK_4X4, BLOCK_8X8, BLOCK_16X16, BLOCK_32X32, BLOCK_64X64
        },
        new BLOCK_SIZE[] {
            BLOCK_INVALID, BLOCK_INVALID, BLOCK_16X8, BLOCK_32X16, BLOCK_64X32, BLOCK_128X64
        },
        new BLOCK_SIZE[] {
            BLOCK_INVALID, BLOCK_INVALID, BLOCK_16X8, BLOCK_32X16, BLOCK_64X32, BLOCK_128X64
        },
        new BLOCK_SIZE[] {
            BLOCK_INVALID, BLOCK_INVALID, BLOCK_8X16, BLOCK_16X32, BLOCK_32X64, BLOCK_64X128
        },
        new BLOCK_SIZE[] {
            BLOCK_INVALID, BLOCK_INVALID, BLOCK_8X16, BLOCK_16X32, BLOCK_32X64, BLOCK_64X128
        },
        new BLOCK_SIZE[] {
            BLOCK_INVALID, BLOCK_INVALID, BLOCK_16X4, BLOCK_32X8, BLOCK_64X16, BLOCK_INVALID
        },
        new BLOCK_SIZE[] {
            BLOCK_INVALID, BLOCK_INVALID, BLOCK_4X16, BLOCK_8X32, BLOCK_16X64, BLOCK_INVALID
        }
    };

    // TX_SIZE max_txsize_lookup[BLOCK_SIZES_ALL]
    internal static readonly TX_SIZE[] max_txsize_lookup = {
        TX_4X4, TX_4X4, TX_4X4, TX_8X8, TX_8X8, TX_8X8, TX_16X16, TX_16X16, TX_16X16, TX_32X32,
        TX_32X32, TX_32X32, TX_64X64, TX_64X64, TX_64X64, TX_64X64, TX_4X4, TX_4X4, TX_8X8, TX_8X8,
        TX_16X16, TX_16X16
    };

    // TX_SIZE max_txsize_rect_lookup[BLOCK_SIZES_ALL]
    internal static readonly TX_SIZE[] max_txsize_rect_lookup = {
        TX_4X4, TX_4X8, TX_8X4, TX_8X8, TX_8X16, TX_16X8, TX_16X16, TX_16X32, TX_32X16, TX_32X32,
        TX_32X64, TX_64X32, TX_64X64, TX_64X64, TX_64X64, TX_64X64, TX_4X16, TX_16X4, TX_8X32,
        TX_32X8, TX_16X64, TX_64X16
    };

    // TX_TYPE_1D vtx_tab[TX_TYPES]
    internal static readonly TX_TYPE_1D[] vtx_tab = {
        DCT_1D, ADST_1D, DCT_1D, ADST_1D, FLIPADST_1D, DCT_1D, FLIPADST_1D, ADST_1D, FLIPADST_1D,
        IDTX_1D, DCT_1D, IDTX_1D, ADST_1D, IDTX_1D, FLIPADST_1D, IDTX_1D
    };

    // TX_TYPE_1D htx_tab[TX_TYPES]
    internal static readonly TX_TYPE_1D[] htx_tab = {
        DCT_1D, DCT_1D, ADST_1D, ADST_1D, DCT_1D, FLIPADST_1D, FLIPADST_1D, FLIPADST_1D, ADST_1D,
        IDTX_1D, IDTX_1D, DCT_1D, IDTX_1D, ADST_1D, IDTX_1D, FLIPADST_1D
    };

    // TX_SIZE sub_tx_size_map[TX_SIZES_ALL]
    internal static readonly TX_SIZE[] sub_tx_size_map = {
        TX_4X4, TX_4X4, TX_8X8, TX_16X16, TX_32X32, TX_4X4, TX_4X4, TX_8X8, TX_8X8, TX_16X16,
        TX_16X16, TX_32X32, TX_32X32, TX_4X8, TX_8X4, TX_8X16, TX_16X8, TX_16X32, TX_32X16
    };

    // TX_SIZE txsize_horz_map[TX_SIZES_ALL]
    internal static readonly TX_SIZE[] txsize_horz_map = {
        TX_4X4, TX_8X8, TX_16X16, TX_32X32, TX_64X64, TX_4X4, TX_8X8, TX_8X8, TX_16X16, TX_16X16,
        TX_32X32, TX_32X32, TX_64X64, TX_4X4, TX_16X16, TX_8X8, TX_32X32, TX_16X16, TX_64X64
    };

    // TX_SIZE txsize_vert_map[TX_SIZES_ALL]
    internal static readonly TX_SIZE[] txsize_vert_map = {
        TX_4X4, TX_8X8, TX_16X16, TX_32X32, TX_64X64, TX_8X8, TX_4X4, TX_16X16, TX_8X8, TX_32X32,
        TX_16X16, TX_64X64, TX_32X32, TX_16X16, TX_4X4, TX_32X32, TX_8X8, TX_64X64, TX_16X16
    };

    // int tx_size_wide_log2[TX_SIZES_ALL]
    internal static readonly int[] tx_size_wide_log2 = {
        2, 3, 4, 5, 6, 2, 3, 3, 4, 4, 5, 5, 6, 2, 4, 3, 5, 4, 6
    };

    // int tx_size_high_log2[TX_SIZES_ALL]
    internal static readonly int[] tx_size_high_log2 = {
        2, 3, 4, 5, 6, 3, 2, 4, 3, 5, 4, 6, 5, 4, 2, 5, 3, 6, 4
    };

    // int tx_size_wide[TX_SIZES_ALL]
    internal static readonly int[] tx_size_wide = {
        4, 8, 16, 32, 64, 4, 8, 8, 16, 16, 32, 32, 64, 4, 16, 8, 32, 16, 64
    };

    // int tx_size_high[TX_SIZES_ALL]
    internal static readonly int[] tx_size_high = {
        4, 8, 16, 32, 64, 8, 4, 16, 8, 32, 16, 64, 32, 16, 4, 32, 8, 64, 16
    };

    // int tx_size_wide_unit[TX_SIZES_ALL]
    internal static readonly int[] tx_size_wide_unit = {
        1, 2, 4, 8, 16, 1, 2, 2, 4, 4, 8, 8, 16, 1, 4, 2, 8, 4, 16
    };

    // int tx_size_high_unit[TX_SIZES_ALL]
    internal static readonly int[] tx_size_high_unit = {
        1, 2, 4, 8, 16, 2, 1, 4, 2, 8, 4, 16, 8, 4, 1, 8, 2, 16, 4
    };

    // int tx_size_wide_unit_log2[TX_SIZES_ALL]
    internal static readonly int[] tx_size_wide_unit_log2 = {
        0, 1, 2, 3, 4, 0, 1, 1, 2, 2, 3, 3, 4, 0, 2, 1, 3, 2, 4
    };

    // int tx_size_high_unit_log2[TX_SIZES_ALL]
    internal static readonly int[] tx_size_high_unit_log2 = {
        0, 1, 2, 3, 4, 1, 0, 2, 1, 3, 2, 4, 3, 2, 0, 3, 1, 4, 2
    };

    // int8_t txsize_log2_minus4[TX_SIZES_ALL]
    internal static readonly sbyte[] txsize_log2_minus4 = {
        0, 2, 4, 6, 6, 1, 1, 3, 3, 5, 5, 6, 6, 2, 2, 4, 4, 5, 5
    };

    // TX_SIZE txsize_sqr_map[TX_SIZES_ALL]
    internal static readonly TX_SIZE[] txsize_sqr_map = {
        TX_4X4, TX_8X8, TX_16X16, TX_32X32, TX_64X64, TX_4X4, TX_4X4, TX_8X8, TX_8X8, TX_16X16,
        TX_16X16, TX_32X32, TX_32X32, TX_4X4, TX_4X4, TX_8X8, TX_8X8, TX_16X16, TX_16X16
    };

    // TX_SIZE txsize_sqr_up_map[TX_SIZES_ALL]
    internal static readonly TX_SIZE[] txsize_sqr_up_map = {
        TX_4X4, TX_8X8, TX_16X16, TX_32X32, TX_64X64, TX_8X8, TX_8X8, TX_16X16, TX_16X16, TX_32X32,
        TX_32X32, TX_64X64, TX_64X64, TX_16X16, TX_16X16, TX_32X32, TX_32X32, TX_64X64, TX_64X64
    };

    // int tx_size_2d[TX_SIZES_ALL + 1]
    internal static readonly int[] tx_size_2d = {
        16, 64, 256, 1024, 4096, 32, 32, 128, 128, 512, 512, 2048, 2048, 64, 64, 256, 256, 1024,
        1024, 0
    };

    // BLOCK_SIZE txsize_to_bsize[TX_SIZES_ALL]
    internal static readonly BLOCK_SIZE[] txsize_to_bsize = {
        BLOCK_4X4, BLOCK_8X8, BLOCK_16X16, BLOCK_32X32, BLOCK_64X64, BLOCK_4X8, BLOCK_8X4,
        BLOCK_8X16, BLOCK_16X8, BLOCK_16X32, BLOCK_32X16, BLOCK_32X64, BLOCK_64X32, BLOCK_4X16,
        BLOCK_16X4, BLOCK_8X32, BLOCK_32X8, BLOCK_16X64, BLOCK_64X16
    };

    // TX_SIZE tx_mode_to_biggest_tx_size[TX_MODES]
    internal static readonly TX_SIZE[] tx_mode_to_biggest_tx_size = {
        TX_4X4, TX_64X64, TX_64X64
    };

    // partition_context_lookup_entry partition_context_lookup[BLOCK_SIZES_ALL]
    internal static readonly partition_context_lookup_entry[] partition_context_lookup = {
        new() { above = 31, left = 31 }, new() { above = 31, left = 30 },
        new() { above = 30, left = 31 }, new() { above = 30, left = 30 },
        new() { above = 30, left = 28 }, new() { above = 28, left = 30 },
        new() { above = 28, left = 28 }, new() { above = 28, left = 24 },
        new() { above = 24, left = 28 }, new() { above = 24, left = 24 },
        new() { above = 24, left = 16 }, new() { above = 16, left = 24 },
        new() { above = 16, left = 16 }, new() { above = 16, left = 0 },
        new() { above = 0, left = 16 }, new() { above = 0, left = 0 },
        new() { above = 31, left = 28 }, new() { above = 28, left = 31 },
        new() { above = 30, left = 24 }, new() { above = 24, left = 30 },
        new() { above = 28, left = 16 }, new() { above = 16, left = 28 }
    };

    // int intra_mode_context[INTRA_MODES]
    internal static readonly int[] intra_mode_context = {
        0, 1, 2, 3, 4, 4, 4, 4, 3, 0, 1, 2, 0
    };

    // int quant_dist_weight[4][2]
    internal static readonly int[][] quant_dist_weight = {
        new int[] { 2, 3 },
        new int[] { 2, 5 },
        new int[] { 2, 7 },
        new int[] { 1, MAX_FRAME_DISTANCE }
    };

    // int quant_dist_lookup_table[4][2]
    internal static readonly int[][] quant_dist_lookup_table = {
        new int[] { 9, 7 },
        new int[] { 11, 5 },
        new int[] { 12, 4 },
        new int[] { 13, 3 }
    };
    // </gen:consts>
}
