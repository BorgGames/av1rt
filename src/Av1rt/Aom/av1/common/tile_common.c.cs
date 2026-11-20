// Port of libaom av1/common/tile_common.c

using System.Diagnostics;

namespace Av1rt.Aom;

// [subset] av1_get_uniform_tile_size is used by the decoder only (av1_dx_iface.c) and is not
// ported. av1_is_min_tile_width_satisfied never runs either; it is kept because it is tiny and
// superres_scale.c asserts it.
internal static partial class tile_common
{
    internal static void av1_tile_init(ref TileInfo tile, AV1_COMMON cm, int row, int col)
    {
        av1_tile_set_row(ref tile, cm, row);
        av1_tile_set_col(ref tile, cm, col);
    }

    // Find smallest k>=0 such that (blk_size << k) >= target
    private static int tile_log2(int blk_size, int target)
    {
        int k;
        for (k = 0; (blk_size << k) < target; k++)
        {
        }
        return k;
    }

    internal static void av1_get_tile_limits(AV1_COMMON cm)
    {
        SequenceHeader seq_params = cm.seq_params;
        CommonTileParams tiles = cm.tiles;
        int sb_cols =
            CEIL_POWER_OF_TWO(cm.mi_params.mi_cols, seq_params.mib_size_log2);
        int sb_rows =
            CEIL_POWER_OF_TWO(cm.mi_params.mi_rows, seq_params.mib_size_log2);

        int sb_size_log2 = seq_params.mib_size_log2 + MI_SIZE_LOG2;
        tiles.max_width_sb = MAX_TILE_WIDTH >> sb_size_log2;

        int max_tile_area_sb = MAX_TILE_AREA >> (2 * sb_size_log2);

        tiles.min_log2_cols = tile_log2(tiles.max_width_sb, sb_cols);
        tiles.max_log2_cols = tile_log2(1, AOMMIN(sb_cols, MAX_TILE_COLS));
        tiles.max_log2_rows = tile_log2(1, AOMMIN(sb_rows, MAX_TILE_ROWS));
        tiles.min_log2 = tile_log2(max_tile_area_sb, sb_cols * sb_rows);
        tiles.min_log2 = AOMMAX(tiles.min_log2, tiles.min_log2_cols);
    }

    internal static void av1_calculate_tile_cols(SequenceHeader seq_params,
                                                 int cm_mi_rows, int cm_mi_cols,
                                                 CommonTileParams tiles)
    {
        int sb_cols = CEIL_POWER_OF_TWO(cm_mi_cols, seq_params.mib_size_log2);
        int sb_rows = CEIL_POWER_OF_TWO(cm_mi_rows, seq_params.mib_size_log2);
        int i;

        // This will be overridden if there is at least two columns of tiles
        // (otherwise there is no inner tile width)
        tiles.min_inner_width = -1;

        if (tiles.uniform_spacing != 0)
        {
            int start_sb;
            int size_sb = CEIL_POWER_OF_TWO(sb_cols, tiles.log2_cols);
            Debug.Assert(size_sb > 0);
            for (i = 0, start_sb = 0; start_sb < sb_cols; i++)
            {
                tiles.col_start_sb[i] = start_sb;
                start_sb += size_sb;
            }
            tiles.cols = i;
            tiles.col_start_sb[i] = sb_cols;
            tiles.min_log2_rows = AOMMAX(tiles.min_log2 - tiles.log2_cols, 0);
            tiles.max_height_sb = sb_rows >> tiles.min_log2_rows;

            tiles.width = size_sb << seq_params.mib_size_log2;
            tiles.width = AOMMIN(tiles.width, cm_mi_cols);
            if (tiles.cols > 1)
            {
                tiles.min_inner_width = tiles.width;
            }
        }
        else
        {
            int max_tile_area_sb = (sb_rows * sb_cols);
            int widest_tile_sb = 1;
            int narrowest_inner_tile_sb = 65536;
            tiles.log2_cols = tile_log2(1, tiles.cols);
            for (i = 0; i < tiles.cols; i++)
            {
                int size_sb = tiles.col_start_sb[i + 1] - tiles.col_start_sb[i];
                widest_tile_sb = AOMMAX(widest_tile_sb, size_sb);
                // ignore the rightmost tile in frame for determining the narrowest
                if (i < tiles.cols - 1)
                    narrowest_inner_tile_sb = AOMMIN(narrowest_inner_tile_sb, size_sb);
            }
            if (tiles.min_log2 != 0)
            {
                max_tile_area_sb >>= (tiles.min_log2 + 1);
            }
            tiles.max_height_sb = AOMMAX(max_tile_area_sb / widest_tile_sb, 1);
            if (tiles.cols > 1)
            {
                tiles.min_inner_width = narrowest_inner_tile_sb
                                        << seq_params.mib_size_log2;
            }
        }
    }

    internal static void av1_calculate_tile_rows(SequenceHeader seq_params,
                                                 int cm_mi_rows, CommonTileParams tiles)
    {
        int sb_rows = CEIL_POWER_OF_TWO(cm_mi_rows, seq_params.mib_size_log2);
        int start_sb, size_sb, i;

        if (tiles.uniform_spacing != 0)
        {
            size_sb = CEIL_POWER_OF_TWO(sb_rows, tiles.log2_rows);
            Debug.Assert(size_sb > 0);
            for (i = 0, start_sb = 0; start_sb < sb_rows; i++)
            {
                tiles.row_start_sb[i] = start_sb;
                start_sb += size_sb;
            }
            tiles.rows = i;
            tiles.row_start_sb[i] = sb_rows;

            tiles.height = size_sb << seq_params.mib_size_log2;
            tiles.height = AOMMIN(tiles.height, cm_mi_rows);
        }
        else
        {
            tiles.log2_rows = tile_log2(1, tiles.rows);
        }
    }

    internal static void av1_tile_set_row(ref TileInfo tile, AV1_COMMON cm, int row)
    {
        Debug.Assert(row < cm.tiles.rows);
        int mi_row_start = cm.tiles.row_start_sb[row]
                           << cm.seq_params.mib_size_log2;
        int mi_row_end = cm.tiles.row_start_sb[row + 1]
                         << cm.seq_params.mib_size_log2;
        tile.tile_row = row;
        tile.mi_row_start = mi_row_start;
        tile.mi_row_end = AOMMIN(mi_row_end, cm.mi_params.mi_rows);
        Debug.Assert(tile.mi_row_end > tile.mi_row_start);
    }

    internal static void av1_tile_set_col(ref TileInfo tile, AV1_COMMON cm, int col)
    {
        Debug.Assert(col < cm.tiles.cols);
        int mi_col_start = cm.tiles.col_start_sb[col]
                           << cm.seq_params.mib_size_log2;
        int mi_col_end = cm.tiles.col_start_sb[col + 1]
                         << cm.seq_params.mib_size_log2;
        tile.tile_col = col;
        tile.mi_col_start = mi_col_start;
        tile.mi_col_end = AOMMIN(mi_col_end, cm.mi_params.mi_cols);
        Debug.Assert(tile.mi_col_end > tile.mi_col_start);
    }

    internal static int av1_get_sb_rows_in_tile(AV1_COMMON cm, in TileInfo tile)
    {
        return CEIL_POWER_OF_TWO(tile.mi_row_end - tile.mi_row_start,
                                 cm.seq_params.mib_size_log2);
    }

    internal static int av1_get_sb_cols_in_tile(AV1_COMMON cm, in TileInfo tile)
    {
        return CEIL_POWER_OF_TWO(tile.mi_col_end - tile.mi_col_start,
                                 cm.seq_params.mib_size_log2);
    }

    internal static int av1_is_min_tile_width_satisfied(AV1_COMMON cm)
    {
        // Disable check if there is a single tile col in the frame
        if (cm.tiles.cols == 1) return 1;

        return B((cm.tiles.min_inner_width << MI_SIZE_LOG2) >=
                 (64 << av1_superres_scaled(cm)));
    }
}
