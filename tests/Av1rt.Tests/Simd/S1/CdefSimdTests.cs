using System;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using Xunit.Abstractions;

using static Av1rt.Aom.cdef;
using static Av1rt.Aom.cdef_block;
using static Av1rt.Aom.cdef_block_avx2;

namespace Av1rt.Tests.Simd.S1;

/// <summary>
/// The AVX2 CDEF kernels (av1/common/x86/cdef_block_avx2.c + cdef_block_simd.h) against the
/// C ports (already verified against libaom in E4/CdefKernelTests).
/// </summary>
public class CdefSimdTests
{
    private readonly ITestOutputHelper _out;

    public CdefSimdTests(ITestOutputHelper output) => _out = output;

    // An 8x8 block in one of several shapes: noise, flat, a directional ramp (plus noise), a hard
    // step, stripes, or an extreme (all 0 / all max / 0-max checkerboard).
    private static void FillBlock(Random rng, ushort[] buf, int offset, int stride, int max)
    {
        int shape = rng.Next(8), basev = rng.Next(max + 1), noise = rng.Next(4);
        int dx = rng.Next(-2, 3), dy = rng.Next(-2, 3), slope = rng.Next(1, 12);
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
            {
                int v = shape switch
                {
                    0 => rng.Next(max + 1),
                    1 => basev,
                    2 => basev + slope * (i * dx + j * dy) + rng.Next(-noise, noise + 1),
                    3 => (i * dx + j * dy) >= 0 ? max : 0,
                    4 => basev + (((i * dx + j * dy) & 2) != 0 ? 3 * slope : 0) + rng.Next(-noise, noise + 1),
                    5 => 0,
                    6 => max,
                    _ => ((i + j) & 1) != 0 ? max : 0,
                };
                buf[offset + i * stride + j] = (ushort)Math.Clamp(v, 0, max);
            }
    }

    [Fact]
    public void FindDirMatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(101);
        for (int iter = 0; iter < 40000; iter++)
        {
            int stride = 8 + rng.Next(0, 3) * 8 + rng.Next(0, 5);
            // 8-bit (coeff_shift 0) is the subset; 10/12-bit inputs exercise the shift.
            int coeffShift = rng.Next(4) == 0 ? rng.Next(1, 5) : 0;
            int max = (256 << coeffShift) - 1;
            var buf = new ushort[stride * 12 + 32];
            int off1 = rng.Next(0, stride * 2), off2 = rng.Next(0, stride * 2);
            FillBlock(rng, buf, off1, stride, max);
            if (rng.Next(2) == 0) FillBlock(rng, buf, off2, stride, max);

            int varC = -1, varS = -2;
            int dirC = cdef_find_dir_c(new Ptr<ushort>(buf, off1), stride, ref varC, coeffShift);
            int dirS = cdef_find_dir_avx2(new Ptr<ushort>(buf, off1), stride, ref varS, coeffShift);
            Assert.True((dirC, varC) == (dirS, varS), $"find_dir iter {iter}: C ({dirC}, {varC}) AVX2 ({dirS}, {varS})");

            int v1c = -1, v2c = -1, o1c = -1, o2c = -1;
            int v1s = -2, v2s = -2, o1s = -2, o2s = -2;
            cdef_find_dir_dual_c(new Ptr<ushort>(buf, off1), new Ptr<ushort>(buf, off2), stride,
                                 ref v1c, ref v2c, coeffShift, ref o1c, ref o2c);
            cdef_find_dir_dual_avx2(new Ptr<ushort>(buf, off1), new Ptr<ushort>(buf, off2), stride,
                                    ref v1s, ref v2s, coeffShift, ref o1s, ref o2s);
            Assert.True((o1c, o2c, v1c, v2c) == (o1s, o2s, v1s, v2s), $"find_dir_dual iter {iter}");
        }
    }

    // A CDEF input buffer (CDEF_BSTRIDE-strided 16-bit pixels, 0..255) for a bw x bh block at
    // inOffset, with CDEF_VERY_LARGE in the border regions a frame boundary would pad (left /
    // right / above / below, any combination, as libaom's cdef_test does) or sprinkled.
    private static ushort[] RandomInput(Random rng, int inOffset, int bw, int bh)
    {
        var inbuf = new ushort[CDEF_INBUF_SIZE];
        const int max = 255;
        int lo = rng.Next(4) == 0 ? rng.Next(max + 1) : 0;
        int bits = rng.Next(1, 9);
        for (int y = -CDEF_VBORDER; y < bh + CDEF_VBORDER; y++)
            for (int x = -CDEF_HBORDER; x < bw + CDEF_HBORDER; x++)
                inbuf[inOffset + y * CDEF_BSTRIDE + x] = (ushort)Math.Min(max, lo + (rng.Next(65536) & ((1 << bits) - 1)));
        // Smooth / directional / extreme content over the region the filters read.
        for (int by = -1; by <= bh / 8 + 1; by++)
            for (int bx = -1; bx <= bw / 8 + 1; bx++)
            {
                int off = inOffset + by * 8 * CDEF_BSTRIDE + bx * 8;
                if (off >= 0 && off + 7 * CDEF_BSTRIDE + 7 < inbuf.Length && rng.Next(3) != 0)
                    FillBlock(rng, inbuf, off, CDEF_BSTRIDE, max);
            }
        int boundary = rng.Next(20);
        for (int y = -CDEF_VBORDER; y < bh + CDEF_VBORDER; y++)
            for (int x = -CDEF_HBORDER; x < bw + CDEF_HBORDER; x++)
            {
                bool outside = x < 0 || y < 0 || x >= bw || y >= bh;
                bool large = boundary < 16
                    ? ((boundary & 1) != 0 && x < 0) || ((boundary & 2) != 0 && x >= bw) ||
                      ((boundary & 4) != 0 && y < 0) || ((boundary & 8) != 0 && y >= bh)
                    : outside && rng.Next(4) == 0;
                if (large) inbuf[inOffset + y * CDEF_BSTRIDE + x] = CDEF_VERY_LARGE;
            }
        return inbuf;
    }

    private static readonly cdef_filter_block_func<byte>[] FiltersC =
        { cdef_filter_8_0_c, cdef_filter_8_1_c, cdef_filter_8_2_c, cdef_filter_8_3_c };
    private static readonly cdef_filter_block_func<byte>[] FiltersAvx2 =
        { cdef_filter_8_0_avx2, cdef_filter_8_1_avx2, cdef_filter_8_2_avx2, cdef_filter_8_3_avx2 };

    [Theory]
    [InlineData(8, 8)]
    [InlineData(4, 4)]
    [InlineData(4, 8)]
    [InlineData(8, 4)]
    public void FilterMatchesC(int bw, int bh)
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(bw * 10 + bh);
        int[] secStrengths = { 0, 1, 2, 4 };
        int cases = 0;
        for (int variant = 0; variant < 4; variant++)
            for (int pri = 0; pri <= 19; pri++)
                foreach (int sec in secStrengths)
                    for (int dir = 0; dir < 8; dir++)
                        for (int rep = 0; rep < 3; rep++)
                        {
                            // Encoder damping is 2..6 (3..6, minus 1 for chroma); go wider.
                            int priDamping = rng.Next(0, 9), secDamping = rng.Next(0, 9);
                            // The block anywhere a 64x64 (luma) / 32x32 (chroma) filter block puts it.
                            int by = rng.Next(0, 64 / bh), bx = rng.Next(0, 64 / bw);
                            int inOffset = (CDEF_VBORDER + by * bh) * CDEF_BSTRIDE + CDEF_HBORDER + bx * bw;
                            ushort[] inbuf = RandomInput(rng, inOffset, bw, bh);
                            int dstride = bw + rng.Next(0, 3) * 8 + rng.Next(0, 5);
                            int dstOff = rng.Next(0, 8);
                            var dstC = new byte[dstOff + dstride * bh + 8];
                            rng.NextBytes(dstC);
                            var dstS = (byte[])dstC.Clone();
                            FiltersC[variant](new Ptr<byte>(dstC, dstOff), dstride, new Ptr<ushort>(inbuf, inOffset), pri, sec,
                                              dir, priDamping, secDamping, 0, bw, bh);
                            FiltersAvx2[variant](new Ptr<byte>(dstS, dstOff), dstride, new Ptr<ushort>(inbuf, inOffset), pri,
                                                 sec, dir, priDamping, secDamping, 0, bw, bh);
                            Assert.True(dstC.AsSpan().SequenceEqual(dstS),
                                        $"{bw}x{bh} variant {variant} pri {pri} sec {sec} dir {dir} damping {priDamping}/{secDamping}");
                            cases++;
                        }
        _out.WriteLine($"{cases} cases");
    }

    [Fact]
    public void CopyRect8To16MatchesC()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(77);
        for (int iter = 0; iter < 5000; iter++)
        {
            // libaom asserts height % 2 == 0 (the encoder copies 2 * CDEF_VBORDER + 4 << n rows).
            int w = rng.Next(4) == 0 ? new[] { 4, 8, 16, 32, 36, 40, 48, 64, 72, 80, 144 }[rng.Next(11)] : rng.Next(1, 150);
            int h = 2 * rng.Next(1, 40);
            int sstride = w + rng.Next(0, 40), dstride = w + rng.Next(0, 40);
            int soff = rng.Next(0, 16), doff = rng.Next(0, 16);
            var src = new byte[soff + sstride * (h - 1) + w];
            rng.NextBytes(src);
            if (rng.Next(8) == 0) Array.Fill(src, (byte)(rng.Next(2) * 255));
            var dstC = new ushort[doff + dstride * (h - 1) + w];
            for (int i = 0; i < dstC.Length; i++) dstC[i] = (ushort)rng.Next(65536);
            var dstS = (ushort[])dstC.Clone();
            cdef_copy_rect8_8bit_to_16bit_c(new Ptr<ushort>(dstC, doff), dstride, new Ptr<byte>(src, soff), sstride, w, h);
            cdef_copy_rect8_8bit_to_16bit_avx2(new Ptr<ushort>(dstS, doff), dstride, new Ptr<byte>(src, soff), sstride, w, h);
            Assert.True(dstC.AsSpan().SequenceEqual(dstS), $"{w}x{h}");
        }
    }

    // Rough speed of the hot kernels; always passes. Only a Release run gives meaningful numbers
    // (dotnet test -c Release --filter "FullyQualifiedName~Simd.S1.CdefSimdTests.Speed"
    // --logger "console;verbosity=detailed"); a Debug build just runs it briefly.
    [Fact]
    public void Speed()
    {
        if (!Avx2.IsSupported) return;
        var rng = new Random(5);
        int inOffset = (CDEF_VBORDER + 8) * CDEF_BSTRIDE + CDEF_HBORDER + 16;
        ushort[] inbuf = RandomInput(rng, inOffset, 8, 8);
        var dst = new byte[8 * 8];
#if DEBUG
        const int n = 2000;
#else
        const int n = 200000;
#endif
        int var = 0, v2 = 0, o1 = 0, o2 = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < n; i++) cdef_filter_8_0_c(dst, 8, new Ptr<ushort>(inbuf, inOffset), 6, 2, i & 7, 5, 5, 0, 8, 8);
            double c0 = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            sw.Restart();
            for (int i = 0; i < n; i++) cdef_filter_8_0_avx2(dst, 8, new Ptr<ushort>(inbuf, inOffset), 6, 2, i & 7, 5, 5, 0, 8, 8);
            double s0 = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            sw.Restart();
            for (int i = 0; i < n; i++) cdef_filter_8_1_c(dst, 8, new Ptr<ushort>(inbuf, inOffset), 6, 0, i & 7, 5, 5, 0, 8, 8);
            double c1 = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            sw.Restart();
            for (int i = 0; i < n; i++) cdef_filter_8_1_avx2(dst, 8, new Ptr<ushort>(inbuf, inOffset), 6, 0, i & 7, 5, 5, 0, 8, 8);
            double s1 = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            sw.Restart();
            for (int i = 0; i < n; i++) cdef_filter_8_0_c(dst, 4, new Ptr<ushort>(inbuf, inOffset), 6, 2, i & 7, 5, 5, 0, 4, 4);
            double c4 = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            sw.Restart();
            for (int i = 0; i < n; i++) cdef_filter_8_0_avx2(dst, 4, new Ptr<ushort>(inbuf, inOffset), 6, 2, i & 7, 5, 5, 0, 4, 4);
            double s4 = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            sw.Restart();
            for (int i = 0; i < n; i++) cdef_find_dir_c(new Ptr<ushort>(inbuf, inOffset), CDEF_BSTRIDE, ref var, 0);
            double cd = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            sw.Restart();
            for (int i = 0; i < n; i++) cdef_find_dir_avx2(new Ptr<ushort>(inbuf, inOffset), CDEF_BSTRIDE, ref var, 0);
            double sd = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            sw.Restart();
            for (int i = 0; i < n; i++)
                cdef_find_dir_dual_c(new Ptr<ushort>(inbuf, inOffset), new Ptr<ushort>(inbuf, inOffset + 8), CDEF_BSTRIDE,
                                     ref var, ref v2, 0, ref o1, ref o2);
            double cdd = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            sw.Restart();
            for (int i = 0; i < n; i++)
                cdef_find_dir_dual_avx2(new Ptr<ushort>(inbuf, inOffset), new Ptr<ushort>(inbuf, inOffset + 8), CDEF_BSTRIDE,
                                        ref var, ref v2, 0, ref o1, ref o2);
            double sdd = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            var src = new byte[80 * 68];
            var cdst = new ushort[CDEF_INBUF_SIZE];
            sw.Restart();
            for (int i = 0; i < n / 20; i++) cdef_copy_rect8_8bit_to_16bit_c(cdst, CDEF_BSTRIDE, src, 80, 80, 68);
            double cc = sw.Elapsed.TotalMilliseconds * 1e6 / (n / 20);
            sw.Restart();
            for (int i = 0; i < n / 20; i++) cdef_copy_rect8_8bit_to_16bit_avx2(cdst, CDEF_BSTRIDE, src, 80, 80, 68);
            double sc = sw.Elapsed.TotalMilliseconds * 1e6 / (n / 20);
            if (pass == 1)
            {
                _out.WriteLine($"cdef_filter_8_0 8x8: C {c0:F0} ns, AVX2 {s0:F0} ns ({c0 / s0:F1}x)");
                _out.WriteLine($"cdef_filter_8_1 8x8: C {c1:F0} ns, AVX2 {s1:F0} ns ({c1 / s1:F1}x)");
                _out.WriteLine($"cdef_filter_8_0 4x4: C {c4:F0} ns, AVX2 {s4:F0} ns ({c4 / s4:F1}x)");
                _out.WriteLine($"cdef_find_dir: C {cd:F0} ns, AVX2 {sd:F0} ns ({cd / sd:F1}x)");
                _out.WriteLine($"cdef_find_dir_dual: C {cdd:F0} ns, AVX2 {sdd:F0} ns ({cdd / sdd:F1}x)");
                _out.WriteLine($"cdef_copy_rect8_8bit_to_16bit 80x68: C {cc:F0} ns, AVX2 {sc:F0} ns ({cc / sc:F1}x)");
            }
        }
    }
}
