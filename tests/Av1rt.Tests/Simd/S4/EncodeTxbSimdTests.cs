using System;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.encodetxb;
using static Av1rt.Aom.encodetxb_avx2;
using static Av1rt.Aom.encodetxb_sse2;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.scan;
using static Av1rt.Aom.txb_common;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.Simd.S4;

// Differential tests of av1_txb_init_levels_avx2 and av1_get_nz_map_contexts_sse2 against the
// _c ports, for every transform size (and every transform type for the contexts).
public class EncodeTxbSimdTests
{
    // Quantized coefficient content: 0 mostly small with zeros, 1 random |c| <= 32767,
    // 2 all +-32767, 3 large beyond int16 (positive saturation), 4 all zero, 5 levels around the
    // 127 clamp.
    private static int Coeff(Random rng, int mode)
    {
        switch (mode)
        {
            case 0:
                int r = rng.Next(8);
                return r < 4 ? 0 : r < 7 ? rng.Next(-3, 4) : rng.Next(-200, 201);
            case 1: return rng.Next(-32767, 32768);
            case 2: return rng.Next(2) == 0 ? -32767 : 32767;
            case 3: return rng.Next(2) == 0 ? rng.Next(32768, int.MaxValue) : rng.Next(-32767, 32768);
            case 4: return 0;
            default: return rng.Next(-130, 131);
        }
    }

    [Fact]
    public void TxbInitLevelsAvx2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(421);
        for (int iter = 0; iter < 400; iter++)
        {
            for (int tx_size = 0; tx_size < TX_SIZES_ALL; tx_size++)
            {
                int width = get_txb_wide(tx_size);
                int height = get_txb_high(tx_size);
                int mode = iter < 12 ? iter % 6 : rng.Next(6);
                // The coefficients end exactly at the end of the array: an over-read throws.
                int off = rng.Next(0, 17);
                var coeff = new int[off + width * height];
                for (int i = 0; i < width * height; i++) coeff[off + i] = Coeff(rng, mode);

                var want = new byte[TX_PAD_2D];
                rng.NextBytes(want);
                var got = (byte[])want.Clone();
                av1_txb_init_levels_c(new Ptr<int>(coeff, off), width, height, set_levels(want, height));
                av1_txb_init_levels_avx2(new Ptr<int>(coeff, off), width, height, set_levels(got, height));

                // Everything but the TX_PAD_END tail, which only the C version zeroes.
                int stride = height + TX_PAD_HOR;
                int n = TX_PAD_TOP * stride + (width + TX_PAD_BOTTOM) * stride;
                for (int i = 0; i < n; i++)
                    Assert.True(want[i] == got[i], $"tx_size {tx_size} mode {mode}: levels[{i}] {got[i]} vs C {want[i]}");
            }
        }
    }

    private static void CheckNzMap(int tx_size, int tx_type, int eob, byte[] levels_buf, int iter)
    {
        int height = get_txb_high(tx_size);
        Ptr<short> scan = av1_scan_orders[tx_size][tx_type].scan;
        int tx_class = tx_type_to_class[tx_type];
        var want = new sbyte[MAX_TX_SQUARE];
        var got = new sbyte[MAX_TX_SQUARE];
        av1_get_nz_map_contexts_c(set_levels(levels_buf, height), scan, eob, tx_size, tx_class, want);
        av1_get_nz_map_contexts_sse2(set_levels(levels_buf, height), scan, eob, tx_size, tx_class, got);
        // Only the contexts of the first eob scan positions are defined (and read).
        for (int i = 0; i < eob; i++)
        {
            int pos = scan[i];
            Assert.True(want[pos] == got[pos],
                        $"iter {iter} tx_size {tx_size} tx_type {tx_type} eob {eob}: ctx[{pos}] {got[pos]} vs C {want[pos]}");
        }
    }

    [Fact]
    public void GetNzMapContextsSse2MatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(422);
        var levels_buf = new byte[TX_PAD_2D];
        for (int tx_size = 0; tx_size < TX_SIZES_ALL; tx_size++)
        {
            int width = get_txb_wide(tx_size);
            int height = get_txb_high(tx_size);
            int bhl = get_txb_bhl(tx_size);
            int n = width * height;
            Ptr<byte> levels = set_levels(levels_buf, height);
            for (int tx_type = 0; tx_type < TX_TYPES; tx_type++)
            {
                Ptr<short> scan = av1_scan_orders[tx_size][tx_type].scan;
                for (int iter = 0; iter < 48; iter++)
                {
                    int eob = iter < 16 ? Math.Min(iter + 1, n) : iter == 16 ? n : rng.Next(1, n + 1);
                    // libaom's EncodeTxbTest: zero levels, random [0, 127] at the first eob scan
                    // positions.
                    Array.Clear(levels_buf);
                    for (int c = 0; c < eob; c++)
                        levels[get_padded_idx(scan[c], bhl)] = (byte)rng.Next(0, 128);
                    CheckNzMap(tx_size, tx_type, eob, levels_buf, iter);

                    // As the encoder makes them: av1_txb_init_levels of coefficients that are zero
                    // after the eob.
                    var coeff = new int[n];
                    int mode = rng.Next(3);
                    for (int c = 0; c < eob; c++)
                        coeff[scan[c]] = mode == 0 ? rng.Next(-3, 4) : mode == 1 ? rng.Next(-300, 301) : 1;
                    rng.NextBytes(levels_buf);
                    av1_txb_init_levels_avx2(coeff, width, height, levels);
                    CheckNzMap(tx_size, tx_type, eob, levels_buf, iter);

                    // The C contexts do not depend on the TX_PAD_END bytes that
                    // av1_txb_init_levels_avx2 leaves alone (C zeroes them).
                    var garbage = new sbyte[MAX_TX_SQUARE];
                    var zeroed = new sbyte[MAX_TX_SQUARE];
                    int tx_class = tx_type_to_class[tx_type];
                    av1_get_nz_map_contexts_c(levels, scan, eob, tx_size, tx_class, garbage);
                    av1_txb_init_levels_c(coeff, width, height, levels);
                    av1_get_nz_map_contexts_c(levels, scan, eob, tx_size, tx_class, zeroed);
                    Assert.Equal(zeroed, garbage);
                }
            }
        }
    }
}
