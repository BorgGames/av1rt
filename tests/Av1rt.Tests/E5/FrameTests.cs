using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.extend;
using static Av1rt.Aom.lookahead;
using static Av1rt.Aom.yv12config;
using static Av1rt.Aom.yv12extend;

namespace Av1rt.Tests.E5;

// Differential tests of the frame buffer code of work package e5 (yv12extend.c, extend.c,
// lookahead.c) against libaom: both sides allocate the frame with aom_alloc_frame_buffer from
// the same random content (borders included), run the operation and compare every byte of the
// allocation.
public class FrameTests
{
    private static readonly (int w, int h)[] Sizes =
    {
        (64, 64), (65, 33), (17, 9), (1, 1), (352, 288), (1366, 766), (1920, 1080),
    };
    private static readonly int[] Borders = { 32, 64, AOM_ENC_NO_SCALE_BORDER, AOM_BORDER_IN_PIXELS };
    private static readonly (int x, int y)[] Subsamplings = { (1, 1), (0, 0), (1, 0) };

    private static YV12_BUFFER_CONFIG Alloc(int w, int h, int ss_x, int ss_y, int border)
    {
        var ybf = new YV12_BUFFER_CONFIG();
        Assert.Equal(0, aom_alloc_frame_buffer(ybf, w, h, ss_x, ss_y, 0, border, 0, false, 0));
        return ybf;
    }

    private static void AssertLayout(int[] layout, YV12_BUFFER_CONFIG ybf)
    {
        Assert.Equal(layout[0], ybf.frame_size);
        Assert.Equal(layout[1], ybf.y_buffer.Offset);
        Assert.Equal(layout[2], ybf.u_buffer.Offset);
        Assert.Equal(layout[3], ybf.v_buffer.Offset);
        Assert.Equal(layout[4], ybf.y_stride);
        Assert.Equal(layout[5], ybf.uv_stride);
    }

    private static void AssertSameBuffer(byte[] expected, YV12_BUFFER_CONFIG ybf)
    {
        Assert.Equal(expected.Length, ybf.frame_size);
        Assert.True(expected.AsSpan().SequenceEqual(ybf.buffer_alloc.AsSpan(0, ybf.frame_size)),
                    "frame buffers differ");
    }

    [Fact]
    public void ExtendFrameBordersMatchesLibaom()
    {
        var rng = new Random(521);
        foreach (var (w, h) in Sizes)
        {
            foreach (int border in Borders)
            {
                foreach (var (ssx, ssy) in Subsamplings)
                {
                    YV12_BUFFER_CONFIG ybf = Alloc(w, h, ssx, ssy, border);
                    rng.NextBytes(ybf.buffer_alloc);
                    byte[] expected = (byte[])ybf.buffer_alloc.Clone();
                    int[] layout = new int[6];
                    Assert.Equal(ybf.frame_size,
                                 E5Shim.aomshim_e5_extend(w, h, ssx, ssy, border, 0, 3, 0, 0, expected, expected.Length, layout));
                    AssertLayout(layout, ybf);

                    aom_extend_frame_borders(ybf, 3);
                    AssertSameBuffer(expected, ybf);
                }
            }
        }
    }

    [Fact]
    public void ExtendFrameBordersPlaneRowMatchesLibaom()
    {
        var rng = new Random(522);
        foreach (var (w, h) in Sizes)
        {
            foreach (int border in new[] { AOM_BORDER_IN_PIXELS, 64 })
            {
                YV12_BUFFER_CONFIG ybf = Alloc(w, h, 1, 1, border);
                for (int plane = 0; plane < 3; plane++)
                {
                    int crop_h = ybf.crop_heights[plane > 0 ? 1 : 0];
                    // Whole plane, first row band, a middle band and the last band (the row
                    // based extension of the encoder works in 64 or 128 pixel bands).
                    var ranges = new (int, int)[]
                    {
                        (0, crop_h), (0, Math.Min(16, crop_h)),
                        (Math.Min(16, crop_h), Math.Min(48, crop_h)),
                        (Math.Max(0, crop_h - 32), crop_h), (crop_h / 2, crop_h / 2),
                    };
                    foreach (var (v_start, v_end) in ranges)
                    {
                        rng.NextBytes(ybf.buffer_alloc);
                        byte[] expected = (byte[])ybf.buffer_alloc.Clone();
                        int[] layout = new int[6];
                        Assert.Equal(ybf.frame_size,
                                     E5Shim.aomshim_e5_extend(w, h, 1, 1, border, 1, plane, v_start, v_end, expected, expected.Length, layout));
                        AssertLayout(layout, ybf);

                        aom_extend_frame_borders_plane_row(ybf, plane, v_start, v_end);
                        AssertSameBuffer(expected, ybf);
                    }
                }
            }
        }
    }

    [Fact]
    public void Yv12CopyMatchesLibaom()
    {
        var rng = new Random(523);
        foreach (var (w, h) in Sizes)
        {
            for (int op = 0; op < 3; op++)
            {
                for (int use_crop = 0; use_crop < 2; use_crop++)
                {
                    YV12_BUFFER_CONFIG src = Alloc(w, h, 1, 1, 32);
                    YV12_BUFFER_CONFIG dst = Alloc(w, h, 1, 1, AOM_BORDER_IN_PIXELS);
                    rng.NextBytes(src.buffer_alloc);
                    rng.NextBytes(dst.buffer_alloc);
                    byte[] sbuf = (byte[])src.buffer_alloc.Clone();
                    byte[] expected = (byte[])dst.buffer_alloc.Clone();
                    Assert.Equal(dst.frame_size,
                                 E5Shim.aomshim_e5_copy(op, w, h, 32, w, h, AOM_BORDER_IN_PIXELS, 1, 1, use_crop, sbuf, sbuf.Length, expected, expected.Length));

                    switch (op)
                    {
                        case 0: aom_yv12_copy_y(src, dst, use_crop); break;
                        case 1: aom_yv12_copy_u(src, dst, use_crop); break;
                        default: aom_yv12_copy_v(src, dst, use_crop); break;
                    }
                    AssertSameBuffer(expected, dst);
                }
            }
        }
    }

    [Fact]
    public void CopyAndExtendFrameMatchesLibaom()
    {
        var rng = new Random(524);
        foreach (var (w, h) in Sizes)
        {
            foreach (int sborder in new[] { 32, 64 })
            {
                foreach (int dborder in new[] { AOM_BORDER_IN_PIXELS, AOM_ENC_NO_SCALE_BORDER })
                {
                    YV12_BUFFER_CONFIG src = Alloc(w, h, 1, 1, sborder);
                    YV12_BUFFER_CONFIG dst = Alloc(w, h, 1, 1, dborder);
                    rng.NextBytes(src.buffer_alloc);
                    rng.NextBytes(dst.buffer_alloc);
                    byte[] sbuf = (byte[])src.buffer_alloc.Clone();
                    byte[] expected = (byte[])dst.buffer_alloc.Clone();
                    Assert.Equal(dst.frame_size,
                                 E5Shim.aomshim_e5_copy(3, w, h, sborder, w, h, dborder, 1, 1, 0, sbuf, sbuf.Length, expected, expected.Length));

                    av1_copy_and_extend_frame(src, dst);
                    AssertSameBuffer(expected, dst);
                }
            }
        }
    }

    [Theory]
    [InlineData(0, 64, 48, 1)]
    [InlineData(1, 64, 48, 2)]
    [InlineData(3, 33, 17, 3)]
    [InlineData(5, 40, 24, 4)]
    public void LookaheadMatchesLibaom(int depth, int width, int height, int seed)
    {
        var rng = new Random(530 + seed);
        const int nops = 300;
        int[] ops = new int[2 * nops];
        for (int i = 0; i < nops; i++)
        {
            int op = rng.Next(5);
            ops[2 * i] = op == 4 ? 0 : op;  // pushes twice as likely
            ops[2 * i + 1] = ops[2 * i] switch
            {
                0 => rng.Next(1000),
                1 => rng.Next(4) == 0 ? 1 : 0,
                2 => rng.Next(-2, 4),
                _ => 0,
            };
        }
        int[] expected = new int[2 * nops];
        Assert.Equal(nops, E5Shim.aomshim_e5_lookahead(width, height, depth, nops, ops, expected));

        lookahead_ctx ctx = av1_lookahead_init(width, height, 1, 1, 0, depth, AOM_BORDER_IN_PIXELS, 0, 0, false, false);
        Assert.NotNull(ctx);
        YV12_BUFFER_CONFIG src = Alloc(width, height, 1, 1, 32);
        int[] actual = new int[2 * nops];
        for (int i = 0; i < nops; i++)
        {
            int op = ops[2 * i], arg = ops[2 * i + 1];
            int r0 = -1, r1 = -1;
            lookahead_entry e = null;
            if (op == 0)
            {
                Array.Fill(src.buffer_alloc, (byte)((arg + 1) & 0xFF));
                r0 = av1_lookahead_push(ctx, src, 1000 * i, 1000 * i + 999, 0, false, arg);
            }
            else if (op == 1)
            {
                e = av1_lookahead_pop(ctx, arg, ENCODE_STAGE);
            }
            else if (op == 2)
            {
                e = av1_lookahead_peek(ctx, arg, ENCODE_STAGE);
            }
            else
            {
                r0 = av1_lookahead_depth(ctx, ENCODE_STAGE);
                r1 = av1_lookahead_pop_sz(ctx, ENCODE_STAGE);
            }
            if (e != null)
            {
                r0 = Array.IndexOf(ctx.buf, e);
                r1 = e.display_idx;
            }
            if (e != null && op == 1)
            {
                // A popped entry holds the pushed frame, copied and extended. (A peek with a
                // negative index can return a slot that was never pushed.)
                Assert.Equal((byte)((e.flags + 1) & 0xFF), e.img.y_buffer[0]);
                Assert.Equal((byte)((e.flags + 1) & 0xFF), e.img.v_buffer[-e.img.uv_stride - 1]);
            }
            actual[2 * i] = r0;
            actual[2 * i + 1] = r1;
        }
        Assert.Equal(expected, actual);
        av1_lookahead_destroy(ctx);
        Assert.Null(ctx.buf);
    }
}
