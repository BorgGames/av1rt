using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aom_convolve;
using static Av1rt.Aom.aom_filter;
using static Av1rt.Aom.convolve;
using static Av1rt.Aom.filter;

namespace Av1rt.Tests.Prediction;

[Trait("Category", "Native")]
public class ConvolveTests
{
    private static readonly int[] Sizes = { 2, 4, 8, 16, 32, 64, 128 };
    private const int Margin = 8;  // >= taps / 2 for the 12-tap filter.
    private const int SrcStride = Margin + 128 + Margin + 5;
    private const int DstStride = 128 + 3;

    private sealed class Buffers
    {
        public readonly byte[] Src = new byte[SrcStride * (Margin + 128 + Margin)];
        public readonly byte[] Expected = new byte[DstStride * 128];
        public readonly byte[] Actual = new byte[DstStride * 128];

        public void Fill(Random rng)
        {
            switch (rng.Next(5))
            {
                case 0: Array.Fill(Src, (byte)255); break;
                case 1:
                    for (int i = 0; i < Src.Length; i++) Src[i] = (byte)(rng.Next(2) * 255);
                    break;
                default: rng.NextBytes(Src); break;
            }
            rng.NextBytes(Expected);
            Expected.CopyTo(Actual, 0);
        }
    }

    // kind: 0 2d_sr, 1 x_sr, 2 y_sr, 3 facade, 4 copy (matches the shim).
    private static void RunCSharp(int kind, Buffers b, int w, int h, int fx, int fy, int sx, int sy)
    {
        InterpFilterParams px = av1_get_interp_filter_params_with_block_size(fx, w);
        InterpFilterParams py = av1_get_interp_filter_params_with_block_size(fy, h);
        ConvolveParams cp = get_conv_params_no_round(0, 0, Ptr<ushort>.Null, 0, 0, 8);
        var src = new Ptr<byte>(b.Src, Margin * SrcStride + Margin);
        Ptr<byte> dst = b.Actual;
        switch (kind)
        {
            case 0: av1_convolve_2d_sr_c(src, SrcStride, dst, DstStride, w, h, px, py, sx, sy, ref cp); break;
            case 1: av1_convolve_x_sr_c(src, SrcStride, dst, DstStride, w, h, px, sx, ref cp); break;
            case 2: av1_convolve_y_sr_c(src, SrcStride, dst, DstStride, w, h, py, sy); break;
            case 3:
                av1_convolve_2d_facade(src, SrcStride, dst, DstStride, w, h, new[] { px, py }, sx, SUBPEL_SHIFTS,
                                       sy, SUBPEL_SHIFTS, 0, ref cp);
                break;
            default: aom_convolve_copy_c(src, SrcStride, dst, DstStride, w, h); break;
        }
    }

    private static void Check(int kind, Buffers b, int w, int h, int fx, int fy, int sx, int sy)
    {
        // The facade dispatches to av1_convolve_2d_sr, whose SIMD versions (libaom's and the
        // port's) require both or neither filter to be the 12-tap MULTITAP_SHARP2; libaom's
        // av1_convolve_test skips mixed pairs likewise, and the encoder never builds one.
        if (kind == 3 && sx != 0 && sy != 0 && (fx == MULTITAP_SHARP2) != (fy == MULTITAP_SHARP2))
            return;
        E3Shim.aomshim_e3_convolve(kind, ref b.Src[Margin * SrcStride + Margin], SrcStride, ref b.Expected[0],
                                   DstStride, w, h, fx, fy, sx, sy);
        RunCSharp(kind, b, w, h, fx, fy, sx, sy);
        Assert.True(b.Expected.AsSpan().SequenceEqual(b.Actual),
                    $"kind {kind} {w}x{h} filters {fx}/{fy} subpel {sx}/{sy}");
    }

    private static int RandomSize(Random rng) => Sizes[rng.Next(Sizes.Length)];

    // Every filter pair (all InterpFilter values, 4-tap variants via small sizes) x every
    // horizontal and vertical subpel phase, plus every size pair.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void KernelsMatchLibaom(int kind)
    {
        var rng = new Random(300 + kind);
        var b = new Buffers();
        for (int fx = 0; fx < INTERP_FILTERS_ALL; fx++)
        {
            for (int fy = 0; fy < INTERP_FILTERS_ALL; fy++)
            {
                if (kind == 1 && fy != 0) continue;  // x-only kernel ignores the vertical filter
                if (kind == 2 && fx != 0) continue;  // y-only kernel ignores the horizontal filter
                for (int sx = 0; sx < SUBPEL_SHIFTS; sx++)
                {
                    for (int sy = 0; sy < SUBPEL_SHIFTS; sy++)
                    {
                        if (kind == 1 && sy != 0) continue;
                        if (kind == 2 && sx != 0) continue;
                        // The sr kernels are only called with a non-zero phase in their direction;
                        // still exercise phase 0 (it is a valid kernel row).
                        int w = RandomSize(rng), h = RandomSize(rng);
                        if (kind != 1 && kind != 2 && rng.Next(4) != 0)
                        {
                            w = Math.Min(w, 32);
                            h = Math.Min(h, 32);
                        }
                        b.Fill(rng);
                        Check(kind, b, w, h, fx, fy, sx, sy);
                    }
                }
            }
        }

        foreach (int w in Sizes)
        {
            foreach (int h in Sizes)
            {
                for (int trial = 0; trial < 3; trial++)
                {
                    int fx = rng.Next(INTERP_FILTERS_ALL), fy = rng.Next(INTERP_FILTERS_ALL);
                    int sx = kind == 2 ? 0 : rng.Next(SUBPEL_SHIFTS);
                    int sy = kind == 1 ? 0 : rng.Next(SUBPEL_SHIFTS);
                    b.Fill(rng);
                    Check(kind, b, w, h, fx, fy, sx, sy);
                }
            }
        }
    }

    [Fact]
    public void ConvolveCopyMatchesLibaom()
    {
        var rng = new Random(310);
        var b = new Buffers();
        foreach (int w in Sizes)
        {
            foreach (int h in Sizes)
            {
                b.Fill(rng);
                Check(4, b, w, h, 0, 0, 0, 0);
            }
        }
    }
}
