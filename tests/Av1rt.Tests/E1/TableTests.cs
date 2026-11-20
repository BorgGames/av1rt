using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.cost;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.scan;
using static Av1rt.Aom.txb_common;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.E1;

/// <summary>scan.c, txb_common.c and cost.c tables compared whole with libaom's.</summary>
[Trait("Category", "Native")]
public class TableTests
{
    // The 64-point sizes use the scans of their 32-point adjusted size.
    private static int ScanLength(int tx_size) => tx_size_2d[av1_get_adjusted_tx_size(tx_size)];

    [Fact]
    public void ScanOrdersMatchLibaom()
    {
        Assert.Equal(TX_SIZES_ALL, av1_scan_orders.Length);
        for (int tx_size = 0; tx_size < TX_SIZES_ALL; tx_size++)
        {
            Assert.Equal(TX_TYPES, av1_scan_orders[tx_size].Length);
            int n = ScanLength(tx_size);
            for (int tx_type = 0; tx_type < TX_TYPES; tx_type++)
            {
                SCAN_ORDER so = get_scan(tx_size, tx_type);
                Assert.Same(av1_scan_orders[tx_size][tx_type], so);
                Assert.Equal(0, so.scan.Offset);
                Assert.Equal(0, so.iscan.Offset);
                Assert.Equal(n, so.scan.Array.Length);
                Assert.Equal(n, so.iscan.Array.Length);
                var scan = new short[n];
                var iscan = new short[n];
                E1Shim.aomshim_e1_scan_order(tx_size, tx_type, scan, iscan, n);
                Assert.Equal(scan, so.scan.Array);
                Assert.Equal(iscan, so.iscan.Array);
            }
        }
    }

    [Fact]
    public void TxbPaddedIdxMatchesLibaom()
    {
        Assert.Equal(MAX_TX_SIZE_LOG2 - MIN_TX_SIZE_LOG2, av1_txb_padded_idx.Length);
        for (int i = 0; i < av1_txb_padded_idx.Length; i++)
        {
            short[] t = av1_txb_padded_idx[i].Array;
            Assert.Equal(32 << (i + MIN_TX_SIZE_LOG2), t.Length);
            var c = new short[t.Length];
            E1Shim.aomshim_e1_txb_padded_idx(i, c, c.Length);
            Assert.Equal(c, t);
        }
    }

    [Fact]
    public void NzMapCtxOffsetMatchesLibaom()
    {
        Assert.Equal(TX_SIZES_ALL, av1_nz_map_ctx_offset.Length);
        for (int tx_size = 0; tx_size < TX_SIZES_ALL; tx_size++)
        {
            sbyte[] t = av1_nz_map_ctx_offset[tx_size].Array;
            var c = new sbyte[t.Length];
            E1Shim.aomshim_e1_nz_map_ctx_offset(tx_size, c, c.Length);
            Assert.Equal(c, t);
        }
    }

    [Fact]
    public void EobTablesMatchLibaom()
    {
        var start = new short[12];
        var bits = new short[12];
        E1Shim.aomshim_e1_eob_tables(start, bits);
        Assert.Equal(start, av1_eob_group_start);
        Assert.Equal(bits, av1_eob_offset_bits);
    }

    [Fact]
    public void ProbCostMatchesLibaom()
    {
        var c = new ushort[129];
        E1Shim.aomshim_e1_prob_cost(c);
        Assert.Equal(c, av1_prob_cost);
    }

    [Fact]
    public void CostLutLowMatchesLibaom()
    {
        av1_init_cost_luts();
        av1_init_cost_luts();  // aom_once: the second call is a no-op
        var c = new ushort[COST_LUT_LOW];
        E1Shim.aomshim_e1_cost_lut_low(c);
        Assert.Equal(c, av1_cost_lut_low);
    }
}
