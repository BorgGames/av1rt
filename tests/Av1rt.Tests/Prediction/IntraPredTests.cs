using System;
using System.Reflection;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.reconintra;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.Prediction;

public class IntraPredTests
{
    // Same order as the shim's family table.
    private static readonly string[] Families = { "dc", "dc_top", "dc_left", "dc_128", "v", "h", "smooth" };

    private static intra_pred_fn Kernel(int family, int txSize)
    {
        string name = $"aom_{Families[family]}_predictor_{tx_size_wide[txSize]}x{tx_size_high[txSize]}_c";
        MethodInfo mi = typeof(intrapred).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(mi);
        return (intra_pred_fn)Delegate.CreateDelegate(typeof(intra_pred_fn), mi);
    }

    // Random edge bytes, sometimes constant extremes (0 / 255) to hit rounding boundaries.
    private static void FillEdge(Random rng, byte[] a)
    {
        switch (rng.Next(6))
        {
            case 0: Array.Fill(a, (byte)0); break;
            case 1: Array.Fill(a, (byte)255); break;
            case 2:
                for (int i = 0; i < a.Length; i++) a[i] = (byte)(rng.Next(2) * 255);
                break;
            default: rng.NextBytes(a); break;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void KernelsMatchLibaom(int family)
    {
        var rng = new Random(100 + family);
        const int Margin = 16;
        for (int tx = 0; tx < TX_SIZES_ALL; tx++)
        {
            intra_pred_fn fn = Kernel(family, tx);
            int w = tx_size_wide[tx], h = tx_size_high[tx];
            for (int trial = 0; trial < 24; trial++)
            {
                var above = new byte[Margin + 2 * 64 + Margin];
                var left = new byte[Margin + 2 * 64 + Margin];
                FillEdge(rng, above);
                FillEdge(rng, left);
                int stride = w + rng.Next(0, 9);
                var expected = new byte[stride * h + 8];
                rng.NextBytes(expected);
                var actual = (byte[])expected.Clone();

                E3Shim.aomshim_e3_intra_kernel(family, tx, ref expected[0], stride, ref above[Margin], ref left[Margin]);
                fn(actual, stride, new Ptr<byte>(above, Margin), new Ptr<byte>(left, Margin));

                Assert.True(expected.AsSpan().SequenceEqual(actual),
                            $"{Families[family]} {w}x{h} trial {trial}");
            }
        }
    }

    [Theory]
    [InlineData(DC_PRED)]
    [InlineData(V_PRED)]
    [InlineData(H_PRED)]
    [InlineData(SMOOTH_PRED)]
    public void PredictIntraBlockMatchesLibaom(int mode)
    {
        var rng = new Random(200 + mode);
        av1_init_intra_predictors();
        const int Origin = 8;
        const int Stride = Origin + 128 + 24;
        const int Rows = Origin + 128 + 8;
        var xd = new MACROBLOCKD();
        xd.mi = new[] { new MB_MODE_INFO { bsize = BLOCK_64X64 } };

        for (int tx = 0; tx < TX_SIZES_ALL; tx++)
        {
            int txw = tx_size_wide[tx], txh = tx_size_high[tx];
            for (int trial = 0; trial < 40; trial++)
            {
                int plane = rng.Next(2);
                int ssX = plane == 0 ? 0 : rng.Next(2);
                int ssY = plane == 0 ? 0 : rng.Next(2);
                int wpx = Math.Min(128, txw << rng.Next(2));
                int hpx = Math.Min(128, txh << rng.Next(2));
                int colOff = rng.Next(wpx / txw) * (txw >> 2);
                int rowOff = rng.Next(hpx / txh) * (txh >> 2);
                bool up = rng.Next(4) != 0, left = rng.Next(4) != 0;
                bool cup = rng.Next(4) != 0, cleft = rng.Next(4) != 0;
                // Distances to the frame edges in 1/8 luma pels (mostly inside, sometimes clipped).
                int toRight = 8 * rng.Next(-(wpx << ssX), 65);
                int toBottom = 8 * rng.Next(-(hpx << ssY), 65);
                int edgeFilter = rng.Next(2);

                var expected = new byte[Stride * Rows];
                FillEdge(rng, expected);
                if (rng.Next(3) == 0) rng.NextBytes(expected);
                var actual = (byte[])expected.Clone();
                int pos = (Origin + rowOff * 4) * Stride + Origin + colOff * 4;

                E3Shim.aomshim_e3_predict_intra_block(plane, ssX, ssY, up ? 1 : 0, left ? 1 : 0, cup ? 1 : 0,
                                                      cleft ? 1 : 0, toRight, toBottom, edgeFilter, wpx, hpx,
                                                      tx, mode, ref expected[pos], Stride, colOff, rowOff);

                for (int p = 0; p < 3; p++)
                {
                    xd.plane[p].subsampling_x = p == plane ? ssX : 0;
                    xd.plane[p].subsampling_y = p == plane ? ssY : 0;
                }
                xd.up_available = up;
                xd.left_available = left;
                xd.chroma_up_available = cup;
                xd.chroma_left_available = cleft;
                xd.mb_to_right_edge = toRight;
                xd.mb_to_bottom_edge = toBottom;
                var buf = new Ptr<byte>(actual, pos);
                av1_predict_intra_block(xd, BLOCK_64X64, edgeFilter, wpx, hpx, tx, mode, 0, 0,
                                        FILTER_INTRA_MODES, buf, Stride, buf, Stride, colOff, rowOff, plane);

                Assert.True(expected.AsSpan().SequenceEqual(actual),
                            $"mode {mode} tx {txw}x{txh} trial {trial} plane {plane} up {up} left {left}");
            }
        }
    }
}
