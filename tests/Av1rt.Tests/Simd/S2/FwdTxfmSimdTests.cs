using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.av1_fwd_txfm2d_avx2;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.hybrid_fwd_txfm;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.Simd.S2;

// Differential tests of the AVX2 (+ SSE2) lowbd forward transform port
// (av1/encoder/x86/av1_fwd_txfm2d_avx2.c, av1_fwd_txfm_sse2.c) against the C port, on 8-bit
// residuals in [-255, 255] (libaom's own test range for the lowbd transforms).
public class FwdTxfmSimdTests
{
    // Every TX_SIZE of the subset (no TX64, no 1:4 sizes).
    public static readonly TheoryData<int> TxSizes = new()
    {
        TX_4X4, TX_8X8, TX_16X16, TX_32X32, TX_4X8, TX_8X4, TX_8X16, TX_16X8, TX_16X32, TX_32X16,
    };

    // TX_TYPEs the AVX2 dispatcher implements: all 16 when both dimensions are <= 16 points;
    // DCT_DCT, IDTX, V_DCT and H_DCT when a dimension is 32 (libaom's 32-point tables are NULL
    // for the rest, which the encoder never asks for: a 32-point size only gets the DCT_ONLY or
    // DCT_IDTX transform set).
    internal static List<int> SimdTxTypes(int txSize)
    {
        var types = new List<int>();
        int w = tx_size_wide[txSize], h = tx_size_high[txSize];
        for (int t = 0; t < TX_TYPES; t++)
        {
            if ((w == 32 || h == 32) && t != DCT_DCT && t != IDTX && t != V_DCT && t != H_DCT)
                continue;
            types.Add(t);
        }
        return types;
    }

    private static TxfmParam MakeParam(int txSize, int txType) => new()
    {
        tx_type = txType,
        tx_size = txSize,
        lossless = 0,
        bd = 8,
        is_hbd = 0,
        tx_set_type = EXT_TX_SET_ALL16,
        eob = 0,
    };

    private static int[] FwdC(short[] src, int stride, int txSize, int txType)
    {
        var o = new int[tx_size_wide[txSize] * tx_size_high[txSize]];
        TxfmParam p = MakeParam(txSize, txType);
        av1_lowbd_fwd_txfm_c(src, o, stride, ref p);
        return o;
    }

    private static void Check(short[] src, int stride, int txSize, int txType, string ctx)
    {
        int n = tx_size_wide[txSize] * tx_size_high[txSize];
        var expected = new int[n];
        var actual = new int[n];
        Array.Fill(expected, 0x7777);
        Array.Fill(actual, 0x7777);
        TxfmParam pc = MakeParam(txSize, txType);
        TxfmParam ps = MakeParam(txSize, txType);
        av1_lowbd_fwd_txfm_c(src, expected, stride, ref pc);
        av1_lowbd_fwd_txfm_avx2(src, actual, stride, ref ps);
        SimdAssert.Same(expected, actual, ctx);
    }

    private static void Fill(short[] src, int w, int h, int stride, int kase, Random rng)
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
                    5 => r < h / 2 == c < w / 2 ? 255 : -255,
                    6 => 0,
                    7 => rng.Next(2) == 0 ? 255 : -255,
                    8 => rng.Next(-16, 17),
                    _ => rng.Next(-255, 256),
                };
                src[r * stride + c] = (short)v;
            }
        }
    }

    private const int FixedCases = 9;
    private const int RandomCases = 40;

    [Theory]
    [MemberData(nameof(TxSizes))]
    public void LowbdFwdTxfm_Avx2MatchesC(int txSize)
    {
        if (!Avx2.IsSupported) return; // skipped: no AVX2 on this machine
        int w = tx_size_wide[txSize], h = tx_size_high[txSize];
        var rng = new Random(0x5200 + txSize);
        foreach (int txType in SimdTxTypes(txSize))
        {
            for (int kase = 0; kase < FixedCases + RandomCases; kase++)
            {
                int stride = w + (kase % 3) * 7; // stride == w and stride > w
                var src = new short[h * stride];
                Fill(src, w, h, stride, kase, rng);
                Check(src, stride, txSize, txType, $"fwd tx_size={txSize} tx_type={txType} case={kase}");
            }
        }
    }

    // For every output coefficient k, the +-255 residual with the sign of the k-th basis
    // function (estimated from the transform of 255-impulses) maximises |coeff[k]|: the inputs
    // that push the 16-bit intermediate stages of the SIMD version hardest.
    [Theory]
    [MemberData(nameof(TxSizes))]
    public void LowbdFwdTxfm_Avx2MatchesC_BasisSignPatterns(int txSize)
    {
        if (!Avx2.IsSupported) return; // skipped: no AVX2 on this machine
        int w = tx_size_wide[txSize], h = tx_size_high[txSize];
        int n = w * h;
        foreach (int txType in SimdTxTypes(txSize))
        {
            var impulse = new int[n][];
            var src = new short[n];
            for (int x = 0; x < n; x++)
            {
                Array.Clear(src);
                src[x] = 255;
                impulse[x] = FwdC(src, w, txSize, txType);
            }
            for (int k = 0; k < n; k++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    for (int x = 0; x < n; x++)
                        src[x] = (short)(impulse[x][k] >= 0 ? 255 * sign : -255 * sign);
                    Check(src, w, txSize, txType, $"fwd basis tx_size={txSize} tx_type={txType} k={k} sign={sign}");
                }
            }
        }
    }

    // (Lossless 4x4 goes back to av1_lowbd_fwd_txfm_c, whose WHT is not in the subset.)
}

internal static class SimdAssert
{
    internal static void Same<T>(T[] expected, T[] actual, string context) where T : IEquatable<T>
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            if (!expected[i].Equals(actual[i]))
                Assert.Fail($"{context}: first mismatch at [{i}]: C={expected[i]} SIMD={actual[i]}");
        }
    }
}
