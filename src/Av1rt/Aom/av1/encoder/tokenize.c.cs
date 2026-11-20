// Port of libaom av1/encoder/tokenize.c
//
// [subset] cost_and_tokenize_map, get_palette_params, get_color_map_params,
// av1_cost_color_map and av1_tokenize_color_map: palette is off in the subset.

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class tokenize
{
    private static void tokenize_vartx(ThreadData td, TX_SIZE tx_size,
                                       BLOCK_SIZE plane_bsize, int blk_row, int blk_col,
                                       int block, int plane, tokenize_b_args arg)
    {
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        macroblockd_plane pd = xd.plane[plane];
        int max_blocks_high = max_block_high(xd, plane_bsize, plane);
        int max_blocks_wide = max_block_wide(xd, plane_bsize, plane);

        if (blk_row >= max_blocks_high || blk_col >= max_blocks_wide) return;

        TX_SIZE plane_tx_size =
            plane != 0 ? av1_get_max_uv_txsize(mbmi.bsize, pd.subsampling_x,
                                               pd.subsampling_y)
                       : mbmi.inter_tx_size[av1_get_txb_size_index(plane_bsize, blk_row,
                                                                   blk_col)];

        if (tx_size == plane_tx_size || plane != 0)
        {
            plane_bsize =
                get_plane_block_size(mbmi.bsize, pd.subsampling_x, pd.subsampling_y);

            tokenize_b_args args = arg;
            if (args.allow_update_cdf != 0)
                // [subset] av1_update_and_record_txb_context: see encodetxb.c.cs.
                throw new NotSupportedException(
                    "[subset] av1_update_and_record_txb_context (allow_update_cdf)");
            else
                av1_record_txb_context(plane, block, blk_row, blk_col, plane_bsize,
                                       tx_size, arg);
        }
        else
        {
            // Half the block size in transform block unit.
            TX_SIZE sub_txs = sub_tx_size_map[tx_size];
            int bsw = tx_size_wide_unit[sub_txs];
            int bsh = tx_size_high_unit[sub_txs];
            int step = bsw * bsh;
            int row_end =
                AOMMIN(tx_size_high_unit[tx_size], max_blocks_high - blk_row);
            int col_end =
                AOMMIN(tx_size_wide_unit[tx_size], max_blocks_wide - blk_col);

            Debug.Assert(bsw > 0 && bsh > 0);

            for (int row = 0; row < row_end; row += bsh)
            {
                int offsetr = blk_row + row;
                for (int col = 0; col < col_end; col += bsw)
                {
                    int offsetc = blk_col + col;

                    tokenize_vartx(td, sub_txs, plane_bsize, offsetr, offsetc, block, plane,
                                   arg);
                    block += step;
                }
            }
        }
    }

    // [port] C `struct tokenize_b_args arg` stack local: per-thread scratch object
    // (av1_tokenize_sb_vartx does not re-enter itself).
    [ThreadStatic] private static tokenize_b_args tsv_arg;

    // `int *rate` may be NULL (every caller in the realtime path passes NULL): Ptr<int>.
    internal static void av1_tokenize_sb_vartx(AV1_COMP cpi, ThreadData td,
                                               RUN_TYPE dry_run, BLOCK_SIZE bsize, Ptr<int> rate,
                                               byte allow_update_cdf)
    {
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        AV1_COMMON cm = cpi.common;
        MACROBLOCK x = td.mb;
        MACROBLOCKD xd = x.e_mbd;
        int mi_row = xd.mi_row;
        int mi_col = xd.mi_col;
        if (mi_row >= cm.mi_params.mi_rows || mi_col >= cm.mi_params.mi_cols)
            return;

        int num_planes = av1_num_planes(cm);
        MB_MODE_INFO mbmi = xd.mi[0];
        tokenize_b_args arg = tsv_arg ??= new tokenize_b_args();
        arg.cpi = cpi;
        arg.td = td;
        arg.this_rate = 0;
        arg.allow_update_cdf = allow_update_cdf;
        arg.dry_run = dry_run;

        if (mbmi.skip_txfm != 0)
        {
            av1_reset_entropy_context(xd, bsize, num_planes);
            return;
        }

        for (int plane = 0; plane < num_planes; ++plane)
        {
            if (plane != 0 && !xd.is_chroma_ref) break;
            macroblockd_plane pd = xd.plane[plane];
            int ss_x = pd.subsampling_x;
            int ss_y = pd.subsampling_y;
            BLOCK_SIZE plane_bsize = get_plane_block_size(bsize, ss_x, ss_y);
            Debug.Assert(plane_bsize < BLOCK_SIZES_ALL);
            int mi_width = mi_size_wide[plane_bsize];
            int mi_height = mi_size_high[plane_bsize];
            TX_SIZE max_tx_size = get_vartx_max_txsize(xd, plane_bsize, plane);
            BLOCK_SIZE txb_size = txsize_to_bsize[max_tx_size];
            int bw = mi_size_wide[txb_size];
            int bh = mi_size_high[txb_size];
            int block = 0;
            int step =
                tx_size_wide_unit[max_tx_size] * tx_size_high_unit[max_tx_size];

            BLOCK_SIZE max_unit_bsize =
                get_plane_block_size(BLOCK_64X64, ss_x, ss_y);
            int mu_blocks_wide = mi_size_wide[max_unit_bsize];
            int mu_blocks_high = mi_size_high[max_unit_bsize];

            mu_blocks_wide = AOMMIN(mi_width, mu_blocks_wide);
            mu_blocks_high = AOMMIN(mi_height, mu_blocks_high);

            for (int idy = 0; idy < mi_height; idy += mu_blocks_high)
            {
                for (int idx = 0; idx < mi_width; idx += mu_blocks_wide)
                {
                    int unit_height = AOMMIN(mu_blocks_high + idy, mi_height);
                    int unit_width = AOMMIN(mu_blocks_wide + idx, mi_width);
                    for (int blk_row = idy; blk_row < unit_height; blk_row += bh)
                    {
                        for (int blk_col = idx; blk_col < unit_width; blk_col += bw)
                        {
                            tokenize_vartx(td, max_tx_size, plane_bsize, blk_row, blk_col,
                                           block, plane, arg);
                            block += step;
                        }
                    }
                }
            }
        }
        if (!rate.IsNull) rate.Value += arg.this_rate;
    }
}
