using System;
using Av1rt.Aom;
using Av1rt.Tests.E1;
using Xunit;
using static Av1rt.Aom.bitwriter;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.encodetxb;
using static Av1rt.Aom.entropy;
using static Av1rt.Aom.entropymode;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.scan;
using static Av1rt.Aom.txb_common;

namespace Av1rt.Tests.F1;

/// <summary>
/// av1_txb_init_levels_c, av1_get_nz_map_contexts_c and av1_write_coeffs_txb (with
/// av1_write_tx_type) against libaom: output bytes and the adapted FRAME_CONTEXT.
/// </summary>
public class CoeffTests
{
    private const int TX_SIZES_ALL_ = 19, TX_TYPES_ = 16, TX_32X32_ = 3;

    private static int RandomLevel(Random rng)
    {
        int r = rng.Next(100);
        if (r < 50) return 1;
        if (r < 70) return 2;
        if (r < 80) return 3;
        if (r < 92) return rng.Next(4, 16);
        if (r < 98) return rng.Next(16, 200);
        return rng.Next(200, 1 << 16);
    }

    private static int RandomCoeff(Random rng) => rng.Next(2) == 0 ? RandomLevel(rng) : -RandomLevel(rng);

    [Fact]
    public void TxbInitLevelsMatchesLibaom()
    {
        var rng = new Random(21);
        for (int tx_size = 0; tx_size < TX_SIZES_ALL_; tx_size++)
        {
            int width = get_txb_wide(tx_size), height = get_txb_high(tx_size);
            for (int iter = 0; iter < 50; iter++)
            {
                var coeff = new int[width * height];
                for (int i = 0; i < coeff.Length; i++)
                    coeff[i] = rng.Next(3) == 0 ? 0 : RandomCoeff(rng);
                var expected = new byte[TX_PAD_2D];
                rng.NextBytes(expected);
                var actual = (byte[])expected.Clone();
                F1Shim.f1_txb_init_levels(coeff, width, height, expected);
                av1_txb_init_levels_c(coeff, width, height, set_levels(actual, height));
                Assert.Equal(expected, actual);
            }
        }
    }

    private static bool TxTypeAllowed(int tx_size, int tx_type, int is_inter, int reduced) =>
        av1_ext_tx_used[av1_get_ext_tx_set_type(tx_size, is_inter, reduced)][tx_type] != 0;

    [Fact]
    public void NzMapContextsMatchesLibaom()
    {
        var rng = new Random(22);
        for (int tx_size = 0; tx_size < TX_SIZES_ALL_; tx_size++)
        {
            int width = get_txb_wide(tx_size), height = get_txb_high(tx_size);
            int maxEob = av1_get_max_eob(tx_size);
            for (int tx_type = 0; tx_type < TX_TYPES_; tx_type++)
            {
                if (!TxTypeAllowed(tx_size, tx_type, 0, 0) && !TxTypeAllowed(tx_size, tx_type, 1, 0))
                    continue;
                for (int iter = 0; iter < 20; iter++)
                {
                    var coeff = new int[width * height];
                    for (int i = 0; i < coeff.Length; i++)
                        coeff[i] = rng.Next(3) == 0 ? 0 : RandomCoeff(rng);
                    var levels = new byte[TX_PAD_2D];
                    F1Shim.f1_txb_init_levels(coeff, width, height, levels);
                    int eob = iter == 0 ? 1 : iter == 1 ? maxEob : rng.Next(1, maxEob + 1);

                    var expected = new sbyte[MAX_TX_SQUARE];
                    for (int i = 0; i < expected.Length; i++) expected[i] = (sbyte)rng.Next(-128, 128);
                    var actual = (sbyte[])expected.Clone();
                    F1Shim.f1_get_nz_map_contexts(levels, tx_size, tx_type, eob, expected);
                    av1_get_nz_map_contexts_c(set_levels(levels, height), get_scan(tx_size, tx_type).scan, eob,
                                              tx_size, tx_type_to_class[tx_type], actual);
                    Assert.Equal(expected, actual);
                }
            }
        }
    }

    // Per block: tx_size, tx_type, plane, is_inter, mode, uv_mode, eob, entropy_ctx (CB_* of
    // the shim).
    private const int CB_COUNT = 8;

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    [InlineData(1, 4)]
    [InlineData(0, 5)]
    [InlineData(1, 6)]
    [InlineData(1, 7)]
    [InlineData(1, 8)]
    public void WriteCoeffsTxbMatchesLibaom(int allowUpdateCdf, int seed)
    {
        var rng = new Random(100 + seed);
        for (int tx_size = 0; tx_size < TX_SIZES_ALL_; tx_size++)
            RunCoeffStream(rng, tx_size, allowUpdateCdf, 120);
        // Mixed sizes in one stream.
        RunCoeffStream(rng, -1, allowUpdateCdf, 600);
    }

    private static void RunCoeffStream(Random rng, int fixedTxSize, int allowUpdateCdf, int count)
    {
        int base_qindex = rng.Next(8) == 0 ? 0 : rng.Next(1, 256);
        int reduced = rng.Next(4) == 0 ? 1 : 0;

        // Default CDFs for base_qindex (the E1 port, checked against libaom in E1).
        var cm = new AV1_COMMON { fc = new FRAME_CONTEXT() };
        cm.quant_params.base_qindex = base_qindex;
        cm.features.reduced_tx_set_used = reduced != 0;
        av1_init_mode_probs(cm.fc);
        av1_default_coef_probs(cm);
        var image = new FrameContextImage(cm.fc);
        byte[] cFc = image.ToBytes();

        // The C# side of the shim's setup.
        var x = new MACROBLOCK();
        MACROBLOCKD xd = x.e_mbd;
        var mbmi = new MB_MODE_INFO();
        xd.mi = new Ptr<MB_MODE_INFO>(new[] { mbmi });
        xd.tile_ctx = cm.fc;
        var txTypeMap = new byte[1];
        xd.tx_type_map = txTypeMap;
        xd.tx_type_map_stride = 1;
        for (int p = 1; p < 3; p++)
        {
            xd.plane[p].subsampling_x = 1;
            xd.plane[p].subsampling_y = 1;
        }
        x.mbmi_ext_frame = new MB_MODE_INFO_EXT_FRAME[1];
        var cb = new CB_COEFF_BUFFER();
        var tcoeff = new int[1024];
        var eobs = new ushort[1];
        var ectx = new byte[1];
        for (int p = 0; p < 3; p++)
        {
            cb.tcoeff[p] = tcoeff;
            cb.eobs[p] = eobs;
            cb.entropy_ctx[p] = ectx;
        }
        x.cb_coef_buff = cb;

        var parameters = new int[count * CB_COUNT];
        var coeffs = new int[count * 1024];
        for (int i = 0; i < count; i++)
        {
            int tx_size = fixedTxSize >= 0 ? fixedTxSize : rng.Next(TX_SIZES_ALL_);
            int plane = rng.Next(3);
            int is_inter = rng.Next(2);
            int mode = is_inter != 0 ? 13 /* NEARESTMV */ : rng.Next(13);
            int uv_mode = rng.Next(14);
            int tx_type;
            if (plane == 0 && txsize_sqr_up_map[tx_size] <= TX_32X32_)
            {
                do tx_type = rng.Next(TX_TYPES_);
                while (!TxTypeAllowed(tx_size, tx_type, is_inter, reduced));
            }
            else
            {
                tx_type = rng.Next(TX_TYPES_);
            }
            int maxEob = av1_get_max_eob(tx_size);
            int r = rng.Next(10);
            int eob = r == 0 ? 0 : r < 6 ? rng.Next(1, Math.Min(16, maxEob) + 1) : rng.Next(1, maxEob + 1);
            int ctx = rng.Next(13) | (rng.Next(3) << 5);

            // The effective tx type (the scan the writer will use), as the writer derives it.
            mbmi.ref_frame[0] = is_inter != 0 ? 1 /* LAST_FRAME */ : 0 /* INTRA_FRAME */;
            mbmi.ref_frame[1] = -1;
            mbmi.mode = mode;
            mbmi.uv_mode = uv_mode;
            txTypeMap[0] = (byte)tx_type;
            int effType = av1_get_tx_type(xd, plane == 0 ? 0 : 1, 0, 0, tx_size, reduced);
            Ptr<short> scanOrder = get_scan(tx_size, effType).scan;
            for (int c = 0; c < eob; c++)
            {
                int pos = scanOrder[c];
                coeffs[i * 1024 + pos] =
                    c == eob - 1 ? RandomCoeff(rng) : (rng.Next(5) < 2 ? 0 : RandomCoeff(rng));
            }

            int o = i * CB_COUNT;
            parameters[o] = tx_size;
            parameters[o + 1] = tx_type;
            parameters[o + 2] = plane;
            parameters[o + 3] = is_inter;
            parameters[o + 4] = mode;
            parameters[o + 5] = uv_mode;
            parameters[o + 6] = eob;
            parameters[o + 7] = ctx;
        }

        var expected = new byte[4 << 20];
        int expectedLength = F1Shim.f1_write_coeffs_txb(cFc, base_qindex, reduced, allowUpdateCdf, count,
                                                        parameters, coeffs, expected, expected.Length);
        Assert.True(expectedLength > 0);

        var w = new aom_writer();
        var buffer = new byte[expected.Length];
        aom_start_encode(w, buffer, buffer.Length);
        w.allow_update_cdf = (byte)allowUpdateCdf;
        for (int i = 0; i < count; i++)
        {
            int o = i * CB_COUNT;
            mbmi.ref_frame[0] = parameters[o + 3] != 0 ? 1 : 0;
            mbmi.ref_frame[1] = -1;
            mbmi.mode = parameters[o + 4];
            mbmi.uv_mode = parameters[o + 5];
            txTypeMap[0] = (byte)parameters[o + 1];
            eobs[0] = (ushort)parameters[o + 6];
            ectx[0] = (byte)parameters[o + 7];
            Array.Copy(coeffs, i * 1024, tcoeff, 0, 1024);
            av1_write_coeffs_txb(cm, x, w, 0, 0, parameters[o + 2], 0, parameters[o]);
        }
        Assert.True(aom_stop_encode(w) >= 0);

        Assert.Equal(expectedLength, (int)w.pos);
        Assert.True(buffer.AsSpan(0, expectedLength).SequenceEqual(expected.AsSpan(0, expectedLength)),
                    $"tx_size {fixedTxSize}: coded bytes differ");
        image.AssertEqualTo(cFc);
    }
}
