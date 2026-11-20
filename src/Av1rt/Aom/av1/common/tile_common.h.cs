// Port of libaom av1/common/tile_common.h

global using static Av1rt.Aom.tile_common;

namespace Av1rt.Aom;

internal partial struct TileInfo
{
    public int mi_row_start, mi_row_end;
    public int mi_col_start, mi_col_end;
    public int tile_row;
    public int tile_col;
}

internal static partial class tile_common
{
    // Define tile maximum width and area
    // There is no maximum height since height is limited by area and width limits
    // The minimum tile width or height is fixed at one superblock
    // <gen:consts>
    internal const int DEFAULT_MAX_NUM_TG = 1;
    internal const int MAX_TILE_WIDTH = (4096);
    internal const int MAX_TILE_AREA = (4096 * 2304);
    // </gen:consts>
}
