using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.entropy;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.scan;
using static Av1rt.Aom.txb_common;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.E1;

/// <summary>
/// The txb_common.h context helpers that read the txb_common.c / scan.c tables, against the
/// same static inline C functions compiled into the shim.
/// </summary>
[Trait("Category", "Native")]
public class TxbContextTests
{
    [Fact]
    public void PaddedIdxMatchesLibaom()
    {
        for (int bhl = MIN_TX_SIZE_LOG2; bhl < MAX_TX_SIZE_LOG2; bhl++)
            for (int idx = 0; idx < av1_txb_padded_idx[bhl - MIN_TX_SIZE_LOG2].Array.Length; idx++)
                Assert.Equal(E1Shim.aomshim_e1_get_padded_idx(idx, bhl), get_padded_idx(idx, bhl));
    }

    [Fact]
    public void NzMapCtxFromStatsMatchesLibaom()
    {
        for (int tx_size = 0; tx_size < TX_SIZES_ALL; tx_size++)
        {
            int bhl = get_txb_bhl(tx_size);
            int n = get_txb_wide(tx_size) * get_txb_high(tx_size);
            for (int tx_class = 0; tx_class < TX_CLASSES; tx_class++)
                for (int coeff_idx = 0; coeff_idx < n; coeff_idx++)
                    for (int stats = 0; stats <= 15; stats++)
                        Assert.Equal(
                            E1Shim.aomshim_e1_get_nz_map_ctx_from_stats(stats, coeff_idx, bhl, tx_size, tx_class),
                            get_nz_map_ctx_from_stats(stats, coeff_idx, bhl, tx_size, tx_class));
        }
    }

    [Fact]
    public void LevelContextsMatchLibaom()
    {
        var rng = new Random(9);
        for (int tx_size = 0; tx_size < TX_SIZES_ALL; tx_size++)
        {
            int bhl = get_txb_bhl(tx_size);
            int width = get_txb_wide(tx_size);
            int height = get_txb_high(tx_size);
            for (int iter = 0; iter < 4; iter++)
            {
                var levels_buf = new byte[TX_PAD_2D];
                // Small levels mostly (as produced by the quantizer), sometimes large ones.
                for (int i = 0; i < levels_buf.Length; i++)
                    levels_buf[i] = (byte)(rng.Next(3) == 0 ? rng.Next(0, 128) : rng.Next(0, 4));
                int levels = TX_PAD_TOP * (height + TX_PAD_HOR);
                Ptr<byte> lv = new Ptr<byte>(levels_buf, levels);
                for (int tx_class = 0; tx_class < TX_CLASSES; tx_class++)
                {
                    for (int c = 0; c < width * height; c++)
                    {
                        int pos = get_padded_idx(c, bhl);
                        Assert.Equal(E1Shim.aomshim_e1_get_nz_mag(levels_buf, levels + pos, bhl, tx_class),
                                     get_nz_mag(lv + pos, bhl, tx_class));
                        Assert.Equal(E1Shim.aomshim_e1_get_br_ctx(levels_buf, levels, c, bhl, tx_class),
                                     get_br_ctx(lv, c, bhl, tx_class));
                    }
                }
            }
        }
    }

    [Fact]
    public void TxbCtxMatchesLibaom()
    {
        var rng = new Random(4);
        for (int plane_bsize = 0; plane_bsize < BLOCK_SIZES_ALL; plane_bsize++)
        {
            for (int tx_size = 0; tx_size < TX_SIZES_ALL; tx_size++)
            {
                if (tx_size_wide[tx_size] > block_size_wide[plane_bsize] ||
                    tx_size_high[tx_size] > block_size_high[plane_bsize])
                    continue;
                for (int plane = 0; plane < 2; plane++)
                {
                    for (int iter = 0; iter < 20; iter++)
                    {
                        // Entropy contexts as written by the encoder: cul_level (0..7) with the
                        // dc sign category (0..2) in bits 3..4; often all zero.
                        var a = new int[16];
                        var l = new int[16];
                        bool zero = rng.Next(4) == 0;
                        for (int i = 0; i < 16; i++)
                        {
                            a[i] = zero ? 0 : rng.Next(8) | (rng.Next(3) << COEFF_CONTEXT_BITS);
                            l[i] = zero ? 0 : rng.Next(8) | (rng.Next(3) << COEFF_CONTEXT_BITS);
                        }
                        E1Shim.aomshim_e1_get_txb_ctx(plane_bsize, tx_size, plane,
                                                      Array.ConvertAll(a, x => (sbyte)x),
                                                      Array.ConvertAll(l, x => (sbyte)x),
                                                      out int skip, out int dc);
                        var ctx = new TXB_CTX();
                        get_txb_ctx(plane_bsize, tx_size, plane, a, l, ref ctx);
                        Assert.Equal(skip, ctx.txb_skip_ctx);
                        Assert.Equal(dc, ctx.dc_sign_ctx);
                    }
                }
            }
        }
    }

    [Fact]
    public void TxbEntropyContextMatchesLibaom()
    {
        var rng = new Random(6);
        for (int tx_size = 0; tx_size < TX_SIZES_ALL; tx_size++)
        {
            int n = tx_size_2d[av1_get_adjusted_tx_size(tx_size)];
            for (int tx_type = 0; tx_type < TX_TYPES; tx_type++)
            {
                for (int iter = 0; iter < 8; iter++)
                {
                    var qcoeff = new int[1024];
                    int density = rng.Next(1, 8);
                    for (int i = 0; i < n; i++)
                        if (rng.Next(density) == 0) qcoeff[i] = rng.Next(-3, 4);
                    if (rng.Next(3) == 0) qcoeff[0] = rng.Next(-2000, 2000);
                    int eob = rng.Next(0, n + 1);
                    Assert.Equal(E1Shim.aomshim_e1_get_txb_entropy_context(qcoeff, tx_size, tx_type, eob),
                                 av1_get_txb_entropy_context(qcoeff, get_scan(tx_size, tx_type), eob));
                }
            }
        }
    }
}
