// Port of av1/common/common_data.c (tables only; generated from the C source).
namespace Av1rt.Aom;

internal static partial class common_data
{
    // The Subsampled_Size table in the spec (Section 5.11.38. Get plane residual size function).
    // const BLOCK_SIZE av1_ss_size_lookup[BLOCK_SIZES_ALL][2][2], indexed [bsize][ss_x][ss_y].
    internal static readonly BLOCK_SIZE[][][] av1_ss_size_lookup =
    {
        new[] { new BLOCK_SIZE[] { BLOCK_4X4, BLOCK_4X4 }, new BLOCK_SIZE[] { BLOCK_4X4, BLOCK_4X4 } },
        new[] { new BLOCK_SIZE[] { BLOCK_4X8, BLOCK_4X4 }, new BLOCK_SIZE[] { BLOCK_INVALID, BLOCK_4X4 } },
        new[] { new BLOCK_SIZE[] { BLOCK_8X4, BLOCK_INVALID }, new BLOCK_SIZE[] { BLOCK_4X4, BLOCK_4X4 } },
        new[] { new BLOCK_SIZE[] { BLOCK_8X8, BLOCK_8X4 }, new BLOCK_SIZE[] { BLOCK_4X8, BLOCK_4X4 } },
        new[] { new BLOCK_SIZE[] { BLOCK_8X16, BLOCK_8X8 }, new BLOCK_SIZE[] { BLOCK_INVALID, BLOCK_4X8 } },
        new[] { new BLOCK_SIZE[] { BLOCK_16X8, BLOCK_INVALID }, new BLOCK_SIZE[] { BLOCK_8X8, BLOCK_8X4 } },
        new[] { new BLOCK_SIZE[] { BLOCK_16X16, BLOCK_16X8 }, new BLOCK_SIZE[] { BLOCK_8X16, BLOCK_8X8 } },
        new[] { new BLOCK_SIZE[] { BLOCK_16X32, BLOCK_16X16 }, new BLOCK_SIZE[] { BLOCK_INVALID, BLOCK_8X16 } },
        new[] { new BLOCK_SIZE[] { BLOCK_32X16, BLOCK_INVALID }, new BLOCK_SIZE[] { BLOCK_16X16, BLOCK_16X8 } },
        new[] { new BLOCK_SIZE[] { BLOCK_32X32, BLOCK_32X16 }, new BLOCK_SIZE[] { BLOCK_16X32, BLOCK_16X16 } },
        new[] { new BLOCK_SIZE[] { BLOCK_32X64, BLOCK_32X32 }, new BLOCK_SIZE[] { BLOCK_INVALID, BLOCK_16X32 } },
        new[] { new BLOCK_SIZE[] { BLOCK_64X32, BLOCK_INVALID }, new BLOCK_SIZE[] { BLOCK_32X32, BLOCK_32X16 } },
        new[] { new BLOCK_SIZE[] { BLOCK_64X64, BLOCK_64X32 }, new BLOCK_SIZE[] { BLOCK_32X64, BLOCK_32X32 } },
        new[] { new BLOCK_SIZE[] { BLOCK_64X128, BLOCK_64X64 }, new BLOCK_SIZE[] { BLOCK_INVALID, BLOCK_32X64 } },
        new[] { new BLOCK_SIZE[] { BLOCK_128X64, BLOCK_INVALID }, new BLOCK_SIZE[] { BLOCK_64X64, BLOCK_64X32 } },
        new[] { new BLOCK_SIZE[] { BLOCK_128X128, BLOCK_128X64 }, new BLOCK_SIZE[] { BLOCK_64X128, BLOCK_64X64 } },
        new[] { new BLOCK_SIZE[] { BLOCK_4X16, BLOCK_4X8 }, new BLOCK_SIZE[] { BLOCK_INVALID, BLOCK_4X8 } },
        new[] { new BLOCK_SIZE[] { BLOCK_16X4, BLOCK_INVALID }, new BLOCK_SIZE[] { BLOCK_8X4, BLOCK_8X4 } },
        new[] { new BLOCK_SIZE[] { BLOCK_8X32, BLOCK_8X16 }, new BLOCK_SIZE[] { BLOCK_INVALID, BLOCK_4X16 } },
        new[] { new BLOCK_SIZE[] { BLOCK_32X8, BLOCK_INVALID }, new BLOCK_SIZE[] { BLOCK_16X8, BLOCK_16X4 } },
        new[] { new BLOCK_SIZE[] { BLOCK_16X64, BLOCK_16X32 }, new BLOCK_SIZE[] { BLOCK_INVALID, BLOCK_8X32 } },
        new[] { new BLOCK_SIZE[] { BLOCK_64X16, BLOCK_INVALID }, new BLOCK_SIZE[] { BLOCK_32X16, BLOCK_32X8 } },
    };
}
