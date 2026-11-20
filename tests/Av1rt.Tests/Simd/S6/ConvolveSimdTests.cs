using System;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aom_convolve;
using static Av1rt.Aom.aom_convolve_copy_avx2_h;
using static Av1rt.Aom.aom_filter;
using static Av1rt.Aom.convolve;
using static Av1rt.Aom.convolve_2d_avx2;
using static Av1rt.Aom.convolve_avx2;
using static Av1rt.Aom.filter;

namespace Av1rt.Tests.Simd.S6;

/// <summary>
/// av1/common/x86/convolve_avx2.c, convolve_2d_avx2.c and aom_dsp/x86/aom_convolve_copy_avx2.c
/// against the C port: every filter (4-tap variants via small sizes, 12-tap MULTITAP_SHARP2),
/// every subpel phase, every size pair 2..128, random strides and offsets, extreme pixels.
/// The whole destination buffer is compared, so writes outside the block are caught too.
/// </summary>
public class ConvolveSimdTests
{
    private static readonly int[] Sizes = { 2, 4, 8, 16, 32, 64, 128 };

    // The SIMD kernels read up to 16 bytes past the C footprint on the right (full-register
    // loads) and one row below it (12-tap 2D w <= 4); the margins cover the taps plus that.
    private const int Top = 16, Bottom = 16, Left = 16, Right = 48;

    private sealed class Buffers
    {
        public readonly byte[] Src = new byte[(Left + 128 + Right + 64) * (Top + 128 + Bottom)];
        public readonly byte[] Expected = new byte[(128 + 64) * 128 + 64];
        public readonly byte[] Actual = new byte[(128 + 64) * 128 + 64];
        public int SrcStride, DstStride, DstOffset;

        public void Fill(Random rng, int w)
        {
            SrcStride = Left + w + Right + rng.Next(64);
            DstStride = w + rng.Next(64);
            DstOffset = rng.Next(64);
            switch (rng.Next(8))
            {
                case 0: Array.Fill(Src, (byte)255); break;
                case 1: Array.Fill(Src, (byte)0); break;
                case 2:
                    for (int i = 0; i < Src.Length; i++) Src[i] = (byte)(rng.Next(2) * 255);
                    break;
                case 3:
                    for (int i = 0; i < Src.Length; i++) Src[i] = (byte)(rng.Next(2) == 0 ? rng.Next(4) : 255 - rng.Next(4));
                    break;
                case 4:
                    {
                        int a = rng.Next(256), d = rng.Next(-8, 9);
                        for (int i = 0; i < Src.Length; i++) Src[i] = (byte)Math.Clamp(a + d * (i % SrcStride), 0, 255);
                        break;
                    }
                default: rng.NextBytes(Src); break;
            }
            rng.NextBytes(Expected);
            Expected.CopyTo(Actual, 0);
        }

        public Ptr<byte> SrcPtr => new(Src, Top * SrcStride + Left);
        public Ptr<byte> ExpectedPtr => new(Expected, DstOffset);
        public Ptr<byte> ActualPtr => new(Actual, DstOffset);
    }

    public enum Kind { X, Y, TwoD, Copy }

    private static void Check(Kind kind, Buffers b, int w, int h, int fx, int fy, int sx, int sy)
    {
        InterpFilterParams px = av1_get_interp_filter_params_with_block_size(fx, w);
        InterpFilterParams py = av1_get_interp_filter_params_with_block_size(fy, h);
        ConvolveParams cp_c = get_conv_params_no_round(0, 0, Ptr<ushort>.Null, 0, 0, 8);
        ConvolveParams cp_v = get_conv_params_no_round(0, 0, Ptr<ushort>.Null, 0, 0, 8);
        switch (kind)
        {
            case Kind.X:
                av1_convolve_x_sr_c(b.SrcPtr, b.SrcStride, b.ExpectedPtr, b.DstStride, w, h, px, sx, ref cp_c);
                av1_convolve_x_sr_avx2(b.SrcPtr, b.SrcStride, b.ActualPtr, b.DstStride, w, h, px, sx, ref cp_v);
                break;
            case Kind.Y:
                av1_convolve_y_sr_c(b.SrcPtr, b.SrcStride, b.ExpectedPtr, b.DstStride, w, h, py, sy);
                av1_convolve_y_sr_avx2(b.SrcPtr, b.SrcStride, b.ActualPtr, b.DstStride, w, h, py, sy);
                break;
            case Kind.TwoD:
                av1_convolve_2d_sr_c(b.SrcPtr, b.SrcStride, b.ExpectedPtr, b.DstStride, w, h, px, py, sx, sy,
                                     ref cp_c);
                av1_convolve_2d_sr_avx2(b.SrcPtr, b.SrcStride, b.ActualPtr, b.DstStride, w, h, px, py, sx, sy,
                                        ref cp_v);
                break;
            default:
                aom_convolve_copy_c(b.SrcPtr, b.SrcStride, b.ExpectedPtr, b.DstStride, w, h);
                aom_convolve_copy_avx2(b.SrcPtr, b.SrcStride, b.ActualPtr, b.DstStride, w, h);
                break;
        }
        if (!b.Expected.AsSpan().SequenceEqual(b.Actual))
        {
            int i = b.Expected.AsSpan().CommonPrefixLength(b.Actual) - b.DstOffset;
            Assert.Fail($"{kind} {w}x{h} filters {fx}/{fy} subpel {sx}/{sy}: first difference at " +
                        $"row {i / b.DstStride} col {i % b.DstStride}");
        }
    }

    // libaom's convolve tests skip 2D pairs mixing the 12-tap filter with an 8-tap one: the
    // SIMD 2D kernels require both or neither to be 12-tap (never mixed by the encoder).
    private static bool ValidPair(Kind kind, int fx, int fy) =>
        kind != Kind.TwoD || (fx == MULTITAP_SHARP2) == (fy == MULTITAP_SHARP2);

    // Every filter (pair) x every subpel phase (pair), with random sizes.
    [Theory]
    [InlineData(Kind.X)]
    [InlineData(Kind.Y)]
    [InlineData(Kind.TwoD)]
    public void EveryFilterAndPhaseMatchesC(Kind kind)
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(600 + (int)kind);
        var b = new Buffers();
        for (int fx = 0; fx < INTERP_FILTERS_ALL; fx++)
        {
            for (int fy = 0; fy < INTERP_FILTERS_ALL; fy++)
            {
                if (kind == Kind.X && fy != 0) continue;
                if (kind == Kind.Y && fx != 0) continue;
                if (!ValidPair(kind, fx, fy)) continue;
                for (int sx = 0; sx < SUBPEL_SHIFTS; sx++)
                {
                    for (int sy = 0; sy < SUBPEL_SHIFTS; sy++)
                    {
                        if (kind == Kind.X && sy != 0) continue;
                        if (kind == Kind.Y && sx != 0) continue;
                        // Phase 0 is never passed in its direction (the facade skips it) but is a
                        // valid kernel row, so it is exercised too.
                        int trials = kind == Kind.TwoD ? 2 : 12;
                        for (int t = 0; t < trials; t++)
                        {
                            int w = Sizes[rng.Next(Sizes.Length)], h = Sizes[rng.Next(Sizes.Length)];
                            if (kind == Kind.TwoD && rng.Next(4) != 0)
                            {
                                w = Math.Min(w, 32);
                                h = Math.Min(h, 32);
                            }
                            b.Fill(rng, w);
                            Check(kind, b, w, h, fx, fy, sx, sy);
                        }
                    }
                }
            }
        }
    }

    // Every size pair x every filter (pair), random phases.
    [Theory]
    [InlineData(Kind.X)]
    [InlineData(Kind.Y)]
    [InlineData(Kind.TwoD)]
    public void EverySizeMatchesC(Kind kind)
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(610 + (int)kind);
        var b = new Buffers();
        foreach (int w in Sizes)
        {
            foreach (int h in Sizes)
            {
                for (int fx = 0; fx < INTERP_FILTERS_ALL; fx++)
                {
                    for (int fy = 0; fy < INTERP_FILTERS_ALL; fy++)
                    {
                        if (kind == Kind.X && fy != 0) continue;
                        if (kind == Kind.Y && fx != 0) continue;
                        if (!ValidPair(kind, fx, fy)) continue;
                        int trials = kind == Kind.TwoD ? 3 : 8;
                        for (int t = 0; t < trials; t++)
                        {
                            // Bias towards the special phases (8 = half pel: 2-tap averaging).
                            int sx = kind == Kind.Y ? 0 : rng.Next(3) == 0 ? 8 : 1 + rng.Next(SUBPEL_SHIFTS - 1);
                            int sy = kind == Kind.X ? 0 : rng.Next(3) == 0 ? 8 : 1 + rng.Next(SUBPEL_SHIFTS - 1);
                            b.Fill(rng, w);
                            Check(kind, b, w, h, fx, fy, sx, sy);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void ConvolveCopyMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(620);
        var b = new Buffers();
        foreach (int w in Sizes)
        {
            foreach (int h in Sizes)
            {
                for (int t = 0; t < 20; t++)
                {
                    b.Fill(rng, w);
                    Check(Kind.Copy, b, w, h, 0, 0, 0, 0);
                }
            }
        }
    }

    // The forwarders reach the SIMD kernels when AVX2 is on and agree with the C port.
    [Fact]
    public void FacadeMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(630);
        var b = new Buffers();
        for (int iter = 0; iter < 3000; iter++)
        {
            int w = Sizes[rng.Next(Sizes.Length)], h = Sizes[rng.Next(Sizes.Length)];
            int f = rng.Next(INTERP_FILTERS_ALL);
            int fx = f == MULTITAP_SHARP2 ? f : rng.Next(MULTITAP_SHARP2);
            int fy = f == MULTITAP_SHARP2 ? f : rng.Next(MULTITAP_SHARP2);
            int sx = rng.Next(2) == 0 ? 0 : rng.Next(SUBPEL_SHIFTS);
            int sy = rng.Next(2) == 0 ? 0 : rng.Next(SUBPEL_SHIFTS);
            b.Fill(rng, w);
            InterpFilterParams[] p =
            {
                av1_get_interp_filter_params_with_block_size(fx, w),
                av1_get_interp_filter_params_with_block_size(fy, h),
            };
            ConvolveParams cp = get_conv_params_no_round(0, 0, Ptr<ushort>.Null, 0, 0, 8);
            av1_convolve_2d_facade(b.SrcPtr, b.SrcStride, b.ActualPtr, b.DstStride, w, h, p, sx,
                                   SUBPEL_SHIFTS, sy, SUBPEL_SHIFTS, 0, ref cp);
            cp = get_conv_params_no_round(0, 0, Ptr<ushort>.Null, 0, 0, 8);
            if (sx == 0 && sy == 0)
                aom_convolve_copy_c(b.SrcPtr, b.SrcStride, b.ExpectedPtr, b.DstStride, w, h);
            else if (sy == 0)
                av1_convolve_x_sr_c(b.SrcPtr, b.SrcStride, b.ExpectedPtr, b.DstStride, w, h, p[0], sx, ref cp);
            else if (sx == 0)
                av1_convolve_y_sr_c(b.SrcPtr, b.SrcStride, b.ExpectedPtr, b.DstStride, w, h, p[1], sy);
            else
                av1_convolve_2d_sr_c(b.SrcPtr, b.SrcStride, b.ExpectedPtr, b.DstStride, w, h, p[0], p[1], sx,
                                     sy, ref cp);
            Assert.True(b.Expected.AsSpan().SequenceEqual(b.Actual),
                        $"facade {w}x{h} filters {fx}/{fy} subpel {sx}/{sy}");
        }
    }
}
