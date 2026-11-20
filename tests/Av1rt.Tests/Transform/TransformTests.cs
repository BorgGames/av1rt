using System;
using System.Collections.Generic;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.hybrid_fwd_txfm;
using static Av1rt.Aom.idct;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.Transform;

// Differential tests of the 2-D forward / inverse transforms against the pure-C libaom build
// (tools/oracle/shim/shim_e2.c).
public class TransformTests
{
    // Every TX_SIZE of the subset (TX64 and the 1:4 sizes are excluded, see the [subset] notes
    // in av1_fwd_txfm2d.c.cs / av1_inv_txfm2d.c.cs).
    public static readonly TheoryData<int> TxSizes = new()
    {
        TX_4X4, TX_8X8, TX_16X16, TX_32X32, TX_4X8, TX_8X4, TX_8X16, TX_16X8, TX_16X32, TX_32X16,
    };

    // TX_TYPEs whose 1-D transforms exist for the given size: a 32-point dimension only has DCT
    // and identity (av1_txfm_type_ls), every other combination is valid for <= 16 points.
    internal static List<int> ValidTxTypes(int txSize)
    {
        var types = new List<int>();
        int w = tx_size_wide[txSize], h = tx_size_high[txSize];
        for (int t = 0; t < TX_TYPES; t++)
        {
            int v = vtx_tab[t], hz = htx_tab[t];
            if (h == 32 && v != DCT_1D && v != IDTX_1D) continue;
            if (w == 32 && hz != DCT_1D && hz != IDTX_1D) continue;
            types.Add(t);
        }
        return types;
    }

    internal static void AssertSame<T>(T[] expected, T[] actual, string context)
        where T : IEquatable<T>
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            if (!expected[i].Equals(actual[i]))
                Assert.Fail($"{context}: first mismatch at [{i}]: C={expected[i]} C#={actual[i]}");
        }
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

    private const int RandomIters = 24;
    private const int ExtremeCases = 6;

    private static void FillResidual(short[] src, int w, int h, int stride, int kase, Random rng)
    {
        Array.Fill(src, (short)0x5a5a); // garbage outside the block (stride padding)
        for (int r = 0; r < h; r++)
        {
            for (int c = 0; c < w; c++)
            {
                int v = kase switch
                {
                    0 => 255,
                    1 => -255,
                    2 => ((r + c) & 1) == 0 ? 255 : -255,
                    3 => (r & 1) == 0 ? 255 : -255,
                    4 => (c & 1) == 0 ? -255 : 255,
                    5 => rng.Next(2) == 0 ? 255 : -255,
                    _ => rng.Next(-255, 256),
                };
                src[r * stride + c] = (short)v;
            }
        }
    }

    [Theory]
    [MemberData(nameof(TxSizes))]
    public void FwdTxfm_MatchesLibaom(int txSize)
    {
        int w = tx_size_wide[txSize], h = tx_size_high[txSize];
        var rng = new Random(0xE2F0 + txSize);
        foreach (int txType in ValidTxTypes(txSize))
        {
            for (int kase = 0; kase < ExtremeCases + RandomIters; kase++)
            {
                int stride = w + (kase % 3) * 5; // stride == w and stride > w
                var src = new short[h * stride];
                FillResidual(src, w, h, stride, kase, rng);

                var expected = new int[w * h];
                var actual = new int[w * h];
                Array.Fill(expected, 0x7777);
                Array.Fill(actual, 0x7777);

                E2Shim.e2_fwd_txfm(src, expected, stride, txSize, txType);
                TxfmParam p = MakeParam(txSize, txType, 0);
                av1_lowbd_fwd_txfm(src, actual, stride, ref p);

                AssertSame(expected, actual, $"fwd tx_size={txSize} tx_type={txType} case={kase}");
            }
        }
    }

    private static int RandomCoeff(Random rng, int kase)
    {
        switch (kase)
        {
            case 0: return short.MaxValue;
            case 1: return short.MinValue;
            case 2: return rng.Next(2) == 0 ? short.MaxValue : short.MinValue;
            case 3: return rng.Next(2) == 0 ? (1 << 20) : -(1 << 20); // beyond the input clamp
            default:
                int sel = rng.Next(8);
                if (sel < 5) return rng.Next(-512, 513);
                if (sel < 7) return rng.Next(short.MinValue, short.MaxValue + 1);
                return rng.Next(-(1 << 18), (1 << 18) + 1);
        }
    }

    [Theory]
    [MemberData(nameof(TxSizes))]
    public void InvTxfmAdd_MatchesLibaom(int txSize)
    {
        int w = tx_size_wide[txSize], h = tx_size_high[txSize];
        int maxEob = blockd.av1_get_max_eob(txSize);
        Assert.Equal(w * h, maxEob);
        var rng = new Random(0xE2A0 + txSize);
        var scan = new short[maxEob];
        var iscan = new short[maxEob];
        foreach (int txType in ValidTxTypes(txSize))
        {
            Assert.Equal(maxEob, E2Shim.e2_get_scan(txSize, txType, scan, iscan));
            for (int kase = 0; kase < 4 + RandomIters; kase++)
            {
                // eob: full block for the extreme cases, random (incl. 1 = DC only) otherwise.
                int eob = kase < 4 ? maxEob : (kase == 4 ? 1 : rng.Next(1, maxEob + 1));
                var coeff = new int[maxEob];
                for (int i = 0; i < eob; i++) coeff[scan[i]] = RandomCoeff(rng, kase);

                int stride = w + (kase % 3) * 7;
                var dstC = new byte[h * stride];
                rng.NextBytes(dstC);
                var dstCs = (byte[])dstC.Clone();

                E2Shim.e2_inv_txfm_add((int[])coeff.Clone(), dstC, stride, txSize, txType, eob);
                TxfmParam p = MakeParam(txSize, txType, eob);
                // The C function, not the av1_inv_txfm_add dispatcher: these extreme coefficients
                // are outside what the 16-bit SIMD kernels match C on (Simd/S3 covers those).
                av1_inv_txfm_add_c(coeff, dstCs, stride, in p);

                AssertSame(dstC, dstCs, $"inv tx_size={txSize} tx_type={txType} case={kase} eob={eob}");
            }
        }
    }
}
