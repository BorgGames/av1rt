using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aom_codec;
using static Av1rt.Aom.av1_quantize;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.idct;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.Transform;

// Quantizer tables built by libaom's own av1_build_quantizer (QUANTS / Dequants flattened in C
// struct field order, each field int16_t[QINDEX_RANGE][8]).
internal sealed class LibaomQuantTables
{
    private const int Rows = 256; // QINDEX_RANGE
    private const int RowLen = 8;
    private const int FieldLen = Rows * RowLen;

    // QUANTS field order (av1/encoder/av1_quantize.h).
    internal static readonly string[] QuantsFields =
    {
        "y_quant", "y_quant_shift", "y_zbin", "y_round",
        "y_quant_fp", "u_quant_fp", "v_quant_fp", "y_round_fp", "u_round_fp", "v_round_fp",
        "u_quant", "v_quant", "u_quant_shift", "v_quant_shift", "u_zbin", "v_zbin", "u_round",
        "v_round",
    };

    internal static readonly string[] DequantsFields = { "y_dequant_QTX", "u_dequant_QTX", "v_dequant_QTX" };

    public readonly short[] Quants;
    public readonly short[] Dequants;

    public LibaomQuantTables(int sharpness)
    {
        Assert.Equal(QuantsFields.Length * FieldLen, E2Shim.e2_quants_size());
        Assert.Equal(DequantsFields.Length * FieldLen, E2Shim.e2_dequants_size());
        Quants = new short[E2Shim.e2_quants_size()];
        Dequants = new short[E2Shim.e2_dequants_size()];
        E2Shim.e2_build_quantizer(8, 0, 0, 0, 0, 0, sharpness, Quants, Dequants);
    }

    public short[] Row(string field, int qindex)
    {
        int f = Array.IndexOf(QuantsFields, field);
        short[] src = Quants;
        if (f < 0)
        {
            f = Array.IndexOf(DequantsFields, field);
            src = Dequants;
        }
        Assert.True(f >= 0, field);
        return src.AsSpan(f * FieldLen + qindex * RowLen, RowLen).ToArray();
    }

    public static string Plane(int plane) => plane switch { 0 => "y", 1 => "u", _ => "v" };
}

// Differential tests of the quantizers (aom_dsp/quantize.c, av1/encoder/av1_quantize.c)
// against the pure-C libaom build (tools/oracle/shim/shim_e2.c).
public class QuantizeTests
{
    private static readonly int[] QIndices = { 0, 10, 37, 64, 100, 143, 190, 230, 255 };

    private static readonly LibaomQuantTables Tables = new(0);
    private static readonly LibaomQuantTables SharpTables = new(5);

    public static readonly TheoryData<int> TxSizes = TransformTests.TxSizes;

    // Mostly-sparse coefficients spread around the dequantizer step, with occasional large
    // values (the forward transform output of 8-bit residuals stays well below 2^20).
    private static int RandomCoeff(Random rng, int dq, int kase)
    {
        switch (kase)
        {
            case 0: return 0;
            case 1: return rng.Next(2) == 0 ? (1 << 20) : -(1 << 20);
            case 2: return rng.Next(-dq, dq + 1); // around the zero bin
            default:
                int sel = rng.Next(10);
                if (sel < 4) return 0;
                if (sel < 8) return rng.Next(-3 * dq, 3 * dq + 1);
                return rng.Next(-(1 << 18), (1 << 18) + 1);
        }
    }

    [Theory]
    [MemberData(nameof(TxSizes))]
    public void QuantizeBFacade_MatchesLibaom(int txSize)
    {
        int n = av1_get_max_eob(txSize);
        var rng = new Random(0xE2B0 + txSize);
        var scan = new short[n];
        var iscan = new short[n];
        foreach (int txType in TransformTests.ValidTxTypes(txSize))
        {
            E2Shim.e2_get_scan(txSize, txType, scan, iscan);
            var sc = new SCAN_ORDER(scan, iscan);
            foreach (LibaomQuantTables t in new[] { Tables, SharpTables })
            {
                foreach (int q in QIndices)
                {
                    for (int plane = 0; plane < 3; plane++)
                    {
                        string pl = LibaomQuantTables.Plane(plane);
                        short[] zbin = t.Row(pl + "_zbin", q);
                        short[] round = t.Row(pl + "_round", q);
                        short[] quant = t.Row(pl + "_quant", q);
                        short[] quantShift = t.Row(pl + "_quant_shift", q);
                        short[] dequant = t.Row(pl + "_dequant_QTX", q);
                        var p = new macroblock_plane
                        {
                            zbin_QTX = zbin,
                            round_QTX = round,
                            quant_QTX = quant,
                            quant_shift_QTX = quantShift,
                            dequant_QTX = dequant,
                        };
                        var qparam = new QUANT_PARAM
                        {
                            log_scale = av1_get_tx_scale(txSize),
                            tx_size = txSize,
                            qmatrix = Ptr<byte>.Null,
                            iqmatrix = Ptr<byte>.Null,
                            use_quant_b_adapt = 0,
                            use_optimize_b = 0,
                            xform_quant_idx = 1, // AV1_XFORM_QUANT_B
                        };

                        for (int kase = 0; kase < 6; kase++)
                        {
                            var coeff = new int[n];
                            for (int i = 0; i < n; i++) coeff[i] = RandomCoeff(rng, dequant[1], kase);
                            // [s2] With SIMD on, the facade's aom_quantize_b* are the AVX2
                            // versions, which (like libaom's) take the 16-bit coefficients the
                            // lowbd forward transform produces: keep the inputs in that domain.
                            if (Av1rt.Aom.Simd.UseAvx2)
                                for (int i = 0; i < n; i++) coeff[i] = Math.Clamp(coeff[i], -short.MaxValue, short.MaxValue);

                            var qC = new int[n];
                            var dqC = new int[n];
                            var eobC = new ushort[1];
                            var qCs = new int[n];
                            var dqCs = new int[n];
                            var eobCs = new ushort[1];
                            Array.Fill(qC, 0x1234);
                            Array.Fill(qCs, 0x1234);
                            Array.Fill(dqC, 0x4321);
                            Array.Fill(dqCs, 0x4321);

                            E2Shim.e2_quantize_b_facade(coeff, n, zbin, round, quant, quantShift, dequant,
                                                         qC, dqC, eobC, txSize, txType);
                            av1_quantize_b_facade(coeff, n, p, qCs, dqCs, eobCs, sc, in qparam);

                            string ctx = $"quantize_b tx_size={txSize} tx_type={txType} q={q} plane={plane} case={kase}";
                            TransformTests.AssertSame(eobC, eobCs, ctx + " eob");
                            TransformTests.AssertSame(qC, qCs, ctx + " qcoeff");
                            TransformTests.AssertSame(dqC, dqCs, ctx + " dqcoeff");
                        }
                    }
                }
            }
        }
    }

    // av1_quantize_lp is used by the nonrd RD estimate (block_yrd) on 4x4/8x8/16x16 Hadamard
    // coefficients with the *_fp tables.
    [Theory]
    [InlineData(TX_4X4)]
    [InlineData(TX_8X8)]
    [InlineData(TX_16X16)]
    public void QuantizeLp_MatchesLibaom(int txSize)
    {
        int n = av1_get_max_eob(txSize);
        var rng = new Random(0xE2C0 + txSize);
        var scan = new short[n];
        var iscan = new short[n];
        E2Shim.e2_get_scan(txSize, DCT_DCT, scan, iscan);
        foreach (LibaomQuantTables t in new[] { Tables, SharpTables })
        {
            foreach (int q in QIndices)
            {
                for (int plane = 0; plane < 3; plane++)
                {
                    string pl = LibaomQuantTables.Plane(plane);
                    short[] round = t.Row(pl + "_round_fp", q);
                    short[] quant = t.Row(pl + "_quant_fp", q);
                    short[] dequant = t.Row(pl + "_dequant_QTX", q);
                    for (int kase = 0; kase < 8; kase++)
                    {
                        var coeff = new short[n];
                        for (int i = 0; i < n; i++)
                        {
                            coeff[i] = kase switch
                            {
                                0 => short.MaxValue,
                                1 => short.MinValue,
                                2 => rng.Next(2) == 0 ? short.MaxValue : short.MinValue,
                                3 => (short)rng.Next(-dequant[1], dequant[1] + 1),
                                _ => rng.Next(4) == 0
                                    ? (short)rng.Next(short.MinValue, short.MaxValue + 1)
                                    : (short)rng.Next(-4 * dequant[1], 4 * dequant[1] + 1),
                            };
                        }

                        var qC = new short[n];
                        var dqC = new short[n];
                        var eobC = new ushort[1];
                        var qCs = new short[n];
                        var dqCs = new short[n];
                        var eobCs = new ushort[1];
                        Array.Fill(qC, (short)0x1234);
                        Array.Fill(qCs, (short)0x1234);

                        E2Shim.e2_quantize_lp(coeff, n, round, quant, qC, dqC, dequant, eobC, scan, iscan);
                        // [s2] The C port: the av1_quantize_lp forwarder dispatches to the AVX2
                        // version, which (like libaom's) cannot take INT16_MIN; it is tested
                        // against this in Simd/S2/QuantizeSimdTests.cs.
                        av1_quantize_lp_c(coeff, n, round, quant, qCs, dqCs, dequant, eobCs, scan, iscan);

                        string ctx = $"quantize_lp tx_size={txSize} q={q} plane={plane} case={kase}";
                        TransformTests.AssertSame(eobC, eobCs, ctx + " eob");
                        TransformTests.AssertSame(qC, qCs, ctx + " qcoeff");
                        TransformTests.AssertSame(dqC, dqCs, ctx + " dqcoeff");
                    }
                }
            }
        }
    }

    [Fact]
    public void QuantizerToQindex_MatchesLibaom()
    {
        for (int q = 0; q < 64; q++)
            Assert.Equal(E2Shim.e2_quantizer_to_qindex(q), av1_quantizer_to_qindex(q));
        for (int qindex = 0; qindex < 256; qindex++)
            Assert.Equal(E2Shim.e2_qindex_to_quantizer(qindex), av1_qindex_to_quantizer(qindex));
    }

    private static short[] FlattenQuants(QUANTS qs)
    {
        short[][][] fields =
        {
            qs.y_quant, qs.y_quant_shift, qs.y_zbin, qs.y_round,
            qs.y_quant_fp, qs.u_quant_fp, qs.v_quant_fp, qs.y_round_fp, qs.u_round_fp, qs.v_round_fp,
            qs.u_quant, qs.v_quant, qs.u_quant_shift, qs.v_quant_shift, qs.u_zbin, qs.v_zbin, qs.u_round,
            qs.v_round,
        };
        var flat = new short[fields.Length * 256 * 8];
        int o = 0;
        foreach (short[][] f in fields)
            foreach (short[] row in f)
                foreach (short v in row) flat[o++] = v;
        return flat;
    }

    private static short[] FlattenDequants(Dequants d)
    {
        var flat = new short[3 * 256 * 8];
        int o = 0;
        foreach (short[][] f in new[] { d.y_dequant_QTX, d.u_dequant_QTX, d.v_dequant_QTX })
            foreach (short[] row in f)
                foreach (short v in row) flat[o++] = v;
        return flat;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    public void BuildQuantizer_MatchesLibaom(int sharpness)
    {
        var expected = new LibaomQuantTables(sharpness);
        var quants = new QUANTS();
        var deq = new Dequants();
        av1_build_quantizer(AOM_BITS_8, 0, 0, 0, 0, 0, quants, deq, sharpness);
        TransformTests.AssertSame(expected.Quants, FlattenQuants(quants), $"QUANTS sharpness={sharpness}");
        TransformTests.AssertSame(expected.Dequants, FlattenDequants(deq), $"Dequants sharpness={sharpness}");
    }
}
