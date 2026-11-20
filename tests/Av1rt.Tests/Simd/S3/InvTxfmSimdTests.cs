using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Av1rt.Tests.Transform;
using Xunit;
using Xunit.Abstractions;

using static Av1rt.Aom.av1_inv_txfm_avx2;
using static Av1rt.Aom.av1_inv_txfm_ssse3;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.idct;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.Simd.S3;

/// <summary>
/// The AVX2 / SSSE3 lowbd inverse transforms (av1/common/x86/av1_inv_txfm_avx2.c,
/// av1_inv_txfm_ssse3.c) against the C inverse transform: the C# port of av1_inv_txfm_add_c for
/// the sizes it has, and pure-C libaom (aomshim_e2) for every size (TX64 and the 1:4 sizes are
/// not in the C# subset). Inputs are made the way libaom's own av1_inv_txfm2d_test.cc makes them
/// (the 16-bit SIMD only matches C on reachable coefficients): the C forward transform of a
/// residual block, high-frequency coefficients zeroed past a random eob, optionally with the last
/// coefficient forced to INT16_MAX * 100 / 141 (the gt_int16 test) or run through a quantize /
/// dequantize round trip with an encoder-like eob.
/// </summary>
public class InvTxfmSimdTests
{
    private readonly ITestOutputHelper _out;

    public InvTxfmSimdTests(ITestOutputHelper output) => _out = output;

    public static readonly TheoryData<int> AllTxSizes = AllSizes();

    private static TheoryData<int> AllSizes()
    {
        var d = new TheoryData<int>();
        for (int t = 0; t < TX_SIZES_ALL; t++) d.Add(t);
        return d;
    }

    // C# port of av1_inv_txfm_add_c covers these (no TX64, no 1:4).
    private static bool CsCovers(int txSize) =>
        txSize is TX_4X4 or TX_8X8 or TX_16X16 or TX_32X32 or TX_4X8 or TX_8X4 or TX_8X16 or
            TX_16X8 or TX_16X32 or TX_32X16;

    // libaom's IsTxSizeTypeValid plus the DCT / identity mixes a 32-point side also has
    // (av1_txfm_type_ls); a 64-point side only has DCT.
    private static List<int> TxTypes(int txSize)
    {
        if (tx_size_wide[txSize] == 64 || tx_size_high[txSize] == 64) return new List<int> { DCT_DCT };
        return TransformTests.ValidTxTypes(txSize);
    }

    private static TxfmParam MakeParam(int txSize, int txType, int eob) => new()
    {
        tx_type = txType,
        tx_size = txSize,
        lossless = 0,
        bd = 8,
        is_hbd = 0,
        tx_set_type = EXT_TX_SET_ALL16,
        eob = eob,
    };

    // ACMRandom::Rand8Extremes: near 0 or near 255.
    private static int Rand8Extremes(Random rng)
    {
        int r = rng.Next(256);
        return (byte)(r < 128 ? r << 4 : r >> 4);
    }

    // A w x h residual block (stride w) in [-255, 255].
    private static void FillResidual(short[] src, int w, int h, int mode, Random rng)
    {
        int basev = rng.Next(-255, 256), slope = rng.Next(-8, 9), noise = rng.Next(1, 32);
        for (int r = 0; r < h; r++)
        {
            for (int c = 0; c < w; c++)
            {
                int v = mode switch
                {
                    0 => 255, // libaom's cnt == 0 block
                    1 => -255,
                    2 => ((r + c) & 1) == 0 ? 255 : -255,
                    3 => (r & 1) == 0 ? 255 : -255,
                    4 => (c & 1) == 0 ? -255 : 255,
                    5 => rng.Next(2) == 0 ? 255 : -255,
                    6 or 7 or 8 => Rand8Extremes(rng), // libaom's random block
                    9 or 10 => rng.Next(-255, 256),
                    11 => Math.Clamp(basev + slope * (r + c) + rng.Next(-noise, noise + 1), -255, 255),
                    _ => rng.Next(-noise, noise + 1), // small, realistic residual
                };
                src[r * w + c] = (short)v;
            }
        }
    }

    private static void FillDst(byte[] dst, int mode, Random rng)
    {
        switch (mode)
        {
            case 0: Array.Fill(dst, (byte)128); break;
            case 1: Array.Fill(dst, (byte)0); break;
            case 2: Array.Fill(dst, (byte)255); break;
            default: rng.NextBytes(dst); break;
        }
    }

    private enum Variant { RandomEob, GtInt16, Quantized }

    // Runs one block through the reference(s) and the SIMD entry points; returns false when the
    // case has no nonzero coefficient (the encoder never inverse-transforms those).
    private static bool RunCase(int txSize, int txType, short[] scan, int cnt, Variant variant, Random rng,
                                bool checkSsse3)
    {
        int w = tx_size_wide[txSize], h = tx_size_high[txSize];
        int maxEob = scan.Length;
        var src = new short[w * h];
        FillResidual(src, w, h, cnt == 0 ? 0 : rng.Next(14), rng);
        var fwd = new int[w * h];
        E2Shim.e2_fwd_txfm(src, fwd, w, txSize, txType);
        var coeff = new int[maxEob];
        Array.Copy(fwd, coeff, maxEob);

        int eob;
        if (variant == Variant.Quantized)
        {
            // Quantize / dequantize like the encoder (av1_quantize_fp_facade: dqcoeff =
            // (qcoeff * dqv) >> log_scale), eob = last nonzero position in scan order + 1.
            int logScale = av1_get_tx_scale(txSize);
            int dcQ = rng.Next(4, 1337), acQ = rng.Next(4, 1829);
            if (rng.Next(4) == 0) { dcQ = rng.Next(4, 64); acQ = rng.Next(4, 64); }
            eob = 0;
            for (int i = 0; i < maxEob; i++)
            {
                int pos = scan[i];
                int q = pos == 0 ? dcQ : acQ;
                int c = coeff[pos];
                int a = Math.Abs(c);
                int qc = (int)(((long)a * (1 << logScale) + (q >> 1)) / q);
                int dq = (qc * q) >> logScale;
                coeff[pos] = c < 0 ? -dq : dq;
                if (dq != 0) eob = i + 1;
            }
            if (eob == 0) return false;
        }
        else
        {
            // libaom: eob = AOMMIN(cnt + 1, eobmax); zero high-frequency coefficients past it.
            eob = cnt < maxEob ? cnt + 1 : rng.Next(1, maxEob + 1);
            for (int i = eob; i < maxEob; i++) coeff[scan[i]] = 0;
            if (variant == Variant.GtInt16) coeff[scan[eob - 1]] = short.MaxValue * 100 / 141;
        }

        int stride = w + rng.Next(3) * 8 + (rng.Next(2) == 0 ? 0 : rng.Next(1, 8));
        var dstRef = new byte[(h - 1) * stride + w + rng.Next(2) * 16];
        FillDst(dstRef, cnt == 0 ? 0 : rng.Next(10), rng);
        var dstAvx2 = (byte[])dstRef.Clone();
        var dstSsse3 = (byte[])dstRef.Clone();
        var dstCs = (byte[])dstRef.Clone();

        string ctx = $"tx_size={txSize} ({w}x{h}) tx_type={txType} variant={variant} cnt={cnt} eob={eob} stride={stride}";

        E2Shim.e2_inv_txfm_add((int[])coeff.Clone(), dstRef, stride, txSize, txType, eob);
        TxfmParam p = MakeParam(txSize, txType, eob);
        var coeffIn = (int[])coeff.Clone();
        av1_inv_txfm_add_avx2(coeffIn, dstAvx2, stride, in p);
        TransformTests.AssertSame(dstRef, dstAvx2, "avx2 vs libaom C: " + ctx);
        TransformTests.AssertSame(coeff, coeffIn, "avx2 modified its input: " + ctx);
        if (checkSsse3)
        {
            av1_inv_txfm_add_ssse3((int[])coeff.Clone(), dstSsse3, stride, in p);
            TransformTests.AssertSame(dstRef, dstSsse3, "ssse3 vs libaom C: " + ctx);
        }
        if (CsCovers(txSize))
        {
            av1_inv_txfm_add_c((int[])coeff.Clone(), dstCs, stride, in p);
            TransformTests.AssertSame(dstCs, dstAvx2, "avx2 vs C# _c: " + ctx);
        }
        return true;
    }

    // libaom AV1LbdInvTxfm2d.match: eob sweeps 1..eobmax, then eobmax + random blocks; plus extra
    // random eobs.
    [Theory]
    [MemberData(nameof(AllTxSizes))]
    public void InvTxfmAdd_MatchesC(int txSize)
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(0x53A0 + txSize);
        int cases = 0;
        foreach (int txType in TxTypes(txSize))
        {
            var scan = new short[av1_get_max_eob(txSize)];
            var iscan = new short[scan.Length];
            Assert.Equal(scan.Length, E2Shim.e2_get_scan(txSize, txType, scan, iscan));
            int iters = scan.Length + 500;
            for (int cnt = 0; cnt < iters; cnt++)
            {
                if (RunCase(txSize, txType, scan, cnt, Variant.RandomEob, rng, checkSsse3: true)) cases++;
            }
        }
        _out.WriteLine($"tx_size {txSize}: {cases} cases");
    }

    // libaom AV1LbdInvTxfm2d.gt_int16 tests only these types: with the forced 23238 coefficient
    // the 16-bit kernels of the others (ADST_ADST, V_ADST, ...) do not match C, and libaom does
    // not claim they do.
    private static readonly int[] GtInt16Types =
    {
        DCT_DCT, ADST_DCT, FLIPADST_DCT, IDTX, V_DCT, H_DCT, H_ADST, H_FLIPADST,
    };

    // libaom AV1LbdInvTxfm2d.gt_int16.
    [Theory]
    [MemberData(nameof(AllTxSizes))]
    public void InvTxfmAdd_GtInt16_MatchesC(int txSize)
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(0x53B0 + txSize);
        int cases = 0;
        foreach (int txType in TxTypes(txSize))
        {
            if (Array.IndexOf(GtInt16Types, txType) < 0) continue;
            var scan = new short[av1_get_max_eob(txSize)];
            var iscan = new short[scan.Length];
            E2Shim.e2_get_scan(txSize, txType, scan, iscan);
            int iters = scan.Length + 100;
            for (int cnt = 0; cnt < iters; cnt++)
            {
                if (RunCase(txSize, txType, scan, cnt, Variant.GtInt16, rng, checkSsse3: true)) cases++;
            }
        }
        _out.WriteLine($"tx_size {txSize}: {cases} cases");
    }

    // Encoder-like coefficients: quantized / dequantized forward transforms, eob from the
    // quantizer.
    [Theory]
    [MemberData(nameof(AllTxSizes))]
    public void InvTxfmAdd_Quantized_MatchesC(int txSize)
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(0x53C0 + txSize);
        int cases = 0;
        foreach (int txType in TxTypes(txSize))
        {
            var scan = new short[av1_get_max_eob(txSize)];
            var iscan = new short[scan.Length];
            E2Shim.e2_get_scan(txSize, txType, scan, iscan);
            for (int it = 0; it < 1500; it++)
            {
                if (RunCase(txSize, txType, scan, 1 + it, Variant.Quantized, rng, checkSsse3: true)) cases++;
            }
        }
        _out.WriteLine($"tx_size {txSize}: {cases} cases");
    }

    // The av1_inv_txfm_add dispatcher picks the AVX2 kernel when SIMD is on.
    [Fact]
    public void Dispatcher_MatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(0x53D0);
        foreach (int txSize in new[] { TX_4X4, TX_8X8, TX_16X16, TX_32X32, TX_8X16, TX_32X16 })
        {
            int w = tx_size_wide[txSize], h = tx_size_high[txSize];
            int maxEob = av1_get_max_eob(txSize);
            foreach (int txType in TxTypes(txSize))
            {
                var scan = new short[maxEob];
                var iscan = new short[maxEob];
                E2Shim.e2_get_scan(txSize, txType, scan, iscan);
                for (int it = 0; it < 50; it++)
                {
                    var src = new short[w * h];
                    FillResidual(src, w, h, rng.Next(14), rng);
                    var coeff = new int[w * h];
                    E2Shim.e2_fwd_txfm(src, coeff, w, txSize, txType);
                    int eob = rng.Next(1, maxEob + 1);
                    for (int i = eob; i < maxEob; i++) coeff[scan[i]] = 0;
                    var dstC = new byte[w * h];
                    rng.NextBytes(dstC);
                    var dstD = (byte[])dstC.Clone();
                    TxfmParam p = MakeParam(txSize, txType, eob);
                    av1_inv_txfm_add_c((int[])coeff.Clone(), dstC, w, in p);
                    av1_inv_txfm_add(coeff, dstD, w, in p);
                    TransformTests.AssertSame(dstC, dstD, $"dispatch tx_size={txSize} tx_type={txType} eob={eob}");
                }
            }
        }
    }
}
