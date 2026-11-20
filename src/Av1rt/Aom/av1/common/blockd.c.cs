// Port of libaom av1/common/blockd.c

using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class blockd
{
    internal static PREDICTION_MODE av1_left_block_mode(MB_MODE_INFO left_mi)
    {
        if (left_mi == null) return DC_PRED;
        Debug.Assert(is_inter_block(left_mi) == 0 || is_intrabc_block(left_mi) != 0);
        return left_mi.mode;
    }

    internal static PREDICTION_MODE av1_above_block_mode(MB_MODE_INFO above_mi)
    {
        if (above_mi == null) return DC_PRED;
        Debug.Assert(is_inter_block(above_mi) == 0 || is_intrabc_block(above_mi) != 0);
        return above_mi.mode;
    }

    internal static void av1_set_entropy_contexts(MACROBLOCKD xd,
                                                  macroblockd_plane pd, int plane,
                                                  BLOCK_SIZE plane_bsize, TX_SIZE tx_size,
                                                  int has_eob, int aoff, int loff)
    {
        Ptr<ENTROPY_CONTEXT> a = pd.above_entropy_context + aoff;
        Ptr<ENTROPY_CONTEXT> l = pd.left_entropy_context + loff;
        int txs_wide = tx_size_wide_unit[tx_size];
        int txs_high = tx_size_high_unit[tx_size];

        // above
        if (has_eob != 0 && xd.mb_to_right_edge < 0)
        {
            int blocks_wide = max_block_wide(xd, plane_bsize, plane);
            int above_contexts = AOMMIN(txs_wide, blocks_wide - aoff);
            memset(a, has_eob, above_contexts);
            memset(a + above_contexts, 0, txs_wide - above_contexts);
        }
        else
        {
            memset(a, has_eob, txs_wide);
        }

        // left
        if (has_eob != 0 && xd.mb_to_bottom_edge < 0)
        {
            int blocks_high = max_block_high(xd, plane_bsize, plane);
            int left_contexts = AOMMIN(txs_high, blocks_high - loff);
            memset(l, has_eob, left_contexts);
            memset(l + left_contexts, 0, txs_high - left_contexts);
        }
        else
        {
            memset(l, has_eob, txs_high);
        }
    }

    internal static void av1_reset_entropy_context(MACROBLOCKD xd, BLOCK_SIZE bsize,
                                                   int num_planes)
    {
        Debug.Assert(bsize < BLOCK_SIZES_ALL);
        int nplanes = 1 + (num_planes - 1) * B(xd.is_chroma_ref);
        for (int i = 0; i < nplanes; i++)
        {
            macroblockd_plane pd = xd.plane[i];
            BLOCK_SIZE plane_bsize =
                get_plane_block_size(bsize, pd.subsampling_x, pd.subsampling_y);
            int txs_wide = mi_size_wide[plane_bsize];
            int txs_high = mi_size_high[plane_bsize];
            memset(pd.above_entropy_context, 0, txs_wide);
            memset(pd.left_entropy_context, 0, txs_high);
        }
    }

    // [subset] av1_reset_loop_filter_delta: never executed (delta loop filter is off in the
    // realtime subset).

    internal static void av1_reset_loop_restoration(MACROBLOCKD xd, int num_planes)
    {
        for (int p = 0; p < num_planes; ++p)
        {
            set_default_wiener(ref xd.wiener_info[p]);
            set_default_sgrproj(ref xd.sgrproj_info[p]);
        }
    }

    internal static void av1_setup_block_planes(MACROBLOCKD xd, int ss_x, int ss_y,
                                                int num_planes)
    {
        int i;

        for (i = 0; i < num_planes; i++)
        {
            xd.plane[i].plane_type = get_plane_type(i);
            xd.plane[i].subsampling_x = i != 0 ? ss_x : 0;
            xd.plane[i].subsampling_y = i != 0 ? ss_y : 0;
        }
        for (i = num_planes; i < MAX_MB_PLANE; i++)
        {
            xd.plane[i].subsampling_x = 1;
            xd.plane[i].subsampling_y = 1;
        }
    }
}
