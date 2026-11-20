using System;
using Av1rt.Aom;
using Xunit;

#nullable enable
using static Av1rt.Aom.cdef;
using static Av1rt.Aom.cdef_block;

namespace Av1rt.Tests.E4;

/// <summary>av1/common/cdef_block.c and cdef.c kernels against libaom.</summary>
public class CdefKernelTests
{
    // An 8x8 block in one of several shapes: noise, flat, a directional ramp in each of the 8
    // CDEF directions (plus noise), or a hard step.
    private static void FillBlock(Random rng, ushort[] buf, int offset, int stride, int max)
    {
        int shape = rng.Next(5), basev = rng.Next(max + 1), noise = rng.Next(4);
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
                    _ => basev + (((i * dx + j * dy) & 2) != 0 ? 3 * slope : 0) + rng.Next(-noise, noise + 1),
                };
                buf[offset + i * stride + j] = (ushort)Math.Clamp(v, 0, max);
            }
    }

    [Fact]
    public void FindDirMatchesLibaom()
    {
        var rng = new Random(31);
        const int stride = 20;
        for (int iter = 0; iter < 20000; iter++)
        {
            int coeffShift = rng.Next(4) == 0 ? 2 : 0;
            int max = (256 << coeffShift) - 1;
            var buf = new ushort[stride * 20];
            FillBlock(rng, buf, 3 * stride + 2, stride, max);
            FillBlock(rng, buf, 3 * stride + 10, stride, max);

            int expected = E4Shim.CdefFindDir(buf, 3 * stride + 2, stride, out int expectedVar, coeffShift);
            int var = -1;
            int actual = cdef_find_dir_c(new Ptr<ushort>(buf, 3 * stride + 2), stride, ref var, coeffShift);
            Assert.Equal((expected, expectedVar), (actual, var));

            E4Shim.CdefFindDirDual(buf, 3 * stride + 2, 3 * stride + 10, stride, out int ev1, out int ev2, coeffShift,
                                   out int eo1, out int eo2);
            int v1 = -1, v2 = -1, o1 = -1, o2 = -1;
            cdef_find_dir_dual_c(new Ptr<ushort>(buf, 3 * stride + 2), new Ptr<ushort>(buf, 3 * stride + 10), stride,
                                 ref v1, ref v2, coeffShift, ref o1, ref o2);
            Assert.Equal((eo1, eo2, ev1, ev2), (o1, o2, v1, v2));
        }
    }

    // CDEF input block: CDEF_BSTRIDE-strided 16-bit pixels around the block, with
    // CDEF_VERY_LARGE in some border regions (frame boundaries) or sprinkled.
    private static ushort[] RandomInput(Random rng, int inOffset, int bw, int bh)
    {
        var inbuf = new ushort[CDEF_INBUF_SIZE];
        int max = 255;
        for (int y = -CDEF_VBORDER; y < bh + CDEF_VBORDER; y++)
            for (int x = -CDEF_HBORDER; x < bw + CDEF_HBORDER; x++)
                inbuf[inOffset + y * CDEF_BSTRIDE + x] = (ushort)rng.Next(max + 1);
        // Smooth/directional content over the region the filters read (block +-3 rows / cols).
        for (int by = -1; by <= bh / 8 + 1; by++)
            for (int bx = -1; bx <= bw / 8 + 1; bx++)
            {
                int off = inOffset + by * 8 * CDEF_BSTRIDE + bx * 8;
                if (off >= 0 && off + 7 * CDEF_BSTRIDE + 7 < inbuf.Length && rng.Next(3) != 0)
                    FillBlock(rng, inbuf, off, CDEF_BSTRIDE, max);
            }
        int boundary = rng.Next(6);
        for (int y = -CDEF_VBORDER; y < bh + CDEF_VBORDER; y++)
            for (int x = -CDEF_HBORDER; x < bw + CDEF_HBORDER; x++)
            {
                bool large = boundary switch
                {
                    1 => x < 0,
                    2 => y < 0,
                    3 => x >= bw,
                    4 => y >= bh,
                    5 => (x < 0 || y < 0 || x >= bw || y >= bh) && rng.Next(4) == 0,
                    _ => false,
                };
                if (large) inbuf[inOffset + y * CDEF_BSTRIDE + x] = CDEF_VERY_LARGE;
            }
        return inbuf;
    }

    private static readonly cdef_filter_block_func<byte>[] Filters =
        { cdef_filter_8_0_c, cdef_filter_8_1_c, cdef_filter_8_2_c, cdef_filter_8_3_c };

    [Theory]
    [InlineData(8, 8)]
    [InlineData(4, 8)]
    [InlineData(8, 4)]
    [InlineData(4, 4)]
    public void FilterBlockMatchesLibaom(int bw, int bh)
    {
        var rng = new Random(bw * 10 + bh);
        int inOffset = CDEF_VBORDER * CDEF_BSTRIDE + CDEF_HBORDER;
        int[] secStrengths = { 0, 1, 2, 4 };
        const int dstride = 12;
        for (int variant = 0; variant < 4; variant++)
            for (int pri = 0; pri < 16; pri++)
                foreach (int sec in secStrengths)
                    for (int dir = 0; dir < 8; dir++)
                        for (int damping = 2; damping <= 6; damping++)
                        {
                            ushort[] inbuf = RandomInput(rng, inOffset, bw, bh);
                            int priDamping = damping, secDamping = rng.Next(2, 7);
                            var dst = new byte[dstride * 10];
                            rng.NextBytes(dst);
                            var expected = (byte[])dst.Clone();
                            E4Shim.CdefFilter8(variant, expected, dstride, inbuf, inOffset, pri, sec, dir, priDamping,
                                               secDamping, 0, bw, bh);
                            Filters[variant](dst, dstride, new Ptr<ushort>(inbuf, inOffset), pri, sec, dir, priDamping,
                                             secDamping, 0, bw, bh);
                            Assert.True(expected.AsSpan().SequenceEqual(dst),
                                        $"variant {variant} pri {pri} sec {sec} dir {dir} damping {priDamping}/{secDamping}");
                        }
    }

    [Fact]
    public void CopyRect8To16MatchesLibaom()
    {
        var rng = new Random(9);
        for (int iter = 0; iter < 500; iter++)
        {
            int w = rng.Next(1, 80), h = rng.Next(1, 70);
            int sstride = w + rng.Next(0, 9), dstride = w + rng.Next(0, 9);
            var src = new byte[sstride * h];
            rng.NextBytes(src);
            var expected = new ushort[dstride * h];
            var actual = new ushort[dstride * h];
            for (int i = 0; i < expected.Length; i++) expected[i] = actual[i] = (ushort)rng.Next(65536);
            E4Shim.aomshim_e4_cdef_copy_rect8(expected, dstride, src, sstride, w, h);
            cdef_copy_rect8_8bit_to_16bit_c(actual, dstride, src, sstride, w, h);
            Assert.True(expected.AsSpan().SequenceEqual(actual), $"{w}x{h}");
        }
    }

    // av1_cdef_filter_fb: direction search (aom_cdef_find_dir), adjust_strength and the filter
    // dispatch, over luma (with the per-search dirinit or the per-frame NULL) then both chroma
    // planes reusing the luma directions.
    [Fact]
    public void FilterFbMatchesLibaom()
    {
        var rng = new Random(123);
        int inOffset = CDEF_VBORDER * CDEF_BSTRIDE + CDEF_HBORDER;
        const int dstride = 136;
        for (int iter = 0; iter < 1500; iter++)
        {
            int nb = rng.Next(2) == 0 ? 8 : 16; // 64 or 128 luma pixels
            bool useDirinit = rng.Next(2) == 0;
            int damping = 3 + rng.Next(4);
            var expDir = new int[256];
            var expVar = new int[256];
            int[]? expDirinit = useDirinit ? new int[1] : null;
            int[][] dir = new int[16][], var = new int[16][];
            for (int i = 0; i < 16; i++) { dir[i] = new int[16]; var[i] = new int[16]; }
            int[]? dirinit = useDirinit ? new int[1] : null;

            // The filtered 8x8 blocks (the same list for all planes, as in cdef_fb_col).
            var list = new byte[512];
            var dlist = new cdef_list[256];
            int count = 0;
            int density = rng.Next(1, 11);
            for (int by = 0; by < nb; by++)
                for (int bx = 0; bx < nb; bx++)
                    if (rng.Next(10) < density)
                    {
                        list[2 * count] = (byte)by;
                        list[2 * count + 1] = (byte)bx;
                        dlist[count] = new cdef_list { by = (byte)by, bx = (byte)bx };
                        count++;
                    }

            for (int pli = 0; pli < 3; pli++)
            {
                int dec = pli > 0 ? 1 : 0;
                int bsz = 8 >> dec;
                ushort[] inbuf = RandomInput(rng, inOffset, nb * bsz, nb * bsz);
                int level = rng.Next(16);
                int sec = new[] { 0, 1, 2, 4 }[rng.Next(4)];
                // The dirinit path's zero-strength 16-bit copy is not ported ([subset]).
                if (useDirinit && level == 0 && sec == 0) level = 1;

                var expected = new byte[dstride * 130];
                rng.NextBytes(expected);
                var actual = (byte[])expected.Clone();
                E4Shim.CdefFilterFb(expected, dstride, inbuf, inOffset, dec, dec, expDir, expDirinit, expVar, pli, list,
                                    count, level, sec, damping, 0);
                av1_cdef_filter_fb(actual, Ptr<ushort>.Null, dstride, new Ptr<ushort>(inbuf, inOffset), dec, dec, dir,
                                   dirinit != null ? new Ptr<int>(dirinit, 0) : Ptr<int>.Null, var, pli, dlist, count,
                                   level, sec, damping, 0);

                string what = $"iteration {iter} plane {pli}";
                Assert.True(expected.AsSpan().SequenceEqual(actual), what + ": dst");
                for (int i = 0; i < 16; i++)
                    for (int j = 0; j < 16; j++)
                    {
                        Assert.True(expDir[i * 16 + j] == dir[i][j], $"{what}: dir[{i}][{j}]");
                        Assert.True(expVar[i * 16 + j] == var[i][j], $"{what}: var[{i}][{j}]");
                    }
                if (useDirinit) Assert.Equal(expDirinit![0], dirinit![0]);
            }
        }
    }

    [Fact]
    public void ComputeSbListMatchesLibaom()
    {
        var rng = new Random(55);
        int[] sizes = { 12 /* BLOCK_64X64 */, 13 /* 64X128 */, 14 /* 128X64 */, 15 /* 128X128 */ };
        for (int iter = 0; iter < 300; iter++)
        {
            int miRows = 2 * rng.Next(1, 40), miCols = 2 * rng.Next(1, 40);
            int miStride = miCols + rng.Next(0, 9);
            var skip = new byte[miRows * miStride];
            int pct = rng.Next(101);
            for (int i = 0; i < skip.Length; i++) skip[i] = (byte)(rng.Next(100) < pct ? 1 : 0);
            var grid = new MB_MODE_INFO[skip.Length];
            for (int i = 0; i < grid.Length; i++) grid[i] = new MB_MODE_INFO { skip_txfm = skip[i] };
            var mp = new CommonModeInfoParams { mi_rows = miRows, mi_cols = miCols, mi_stride = miStride, mi_grid_base = grid };

            for (int fbr = 0; fbr * 16 < miRows; fbr++)
                for (int fbc = 0; fbc * 16 < miCols; fbc++)
                    foreach (int bs in sizes)
                    {
                        var expected = new byte[2 * 1024];
                        int n = E4Shim.aomshim_e4_cdef_compute_sb_list(miRows, miCols, miStride, skip, fbr * 16, fbc * 16,
                                                                       bs, expected);
                        var dlist = new cdef_list[1024];
                        int m = av1_cdef_compute_sb_list(mp, fbr * 16, fbc * 16, dlist, bs);
                        Assert.Equal(n, m);
                        for (int i = 0; i < n; i++)
                            Assert.Equal((expected[2 * i], expected[2 * i + 1]), (dlist[i].by, dlist[i].bx));
                    }
        }
    }
}
