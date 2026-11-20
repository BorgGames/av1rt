using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.pred_common;
using static Av1rt.Aom.scale;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.Prediction;

// av1_setup_scale_factors_for_frame, pred_common contexts, av1_set_entropy_contexts.
public class ContextTests
{
    // Copy of av1_ss_size_lookup (common_data.c, another package; still a stub in this tree), so
    // the tests do not depend on get_plane_block_size.
    private static readonly int[,,] SsSizeLookup =
    {
        { { BLOCK_4X4, BLOCK_4X4 }, { BLOCK_4X4, BLOCK_4X4 } },
        { { BLOCK_4X8, BLOCK_4X4 }, { BLOCK_INVALID, BLOCK_4X4 } },
        { { BLOCK_8X4, BLOCK_INVALID }, { BLOCK_4X4, BLOCK_4X4 } },
        { { BLOCK_8X8, BLOCK_8X4 }, { BLOCK_4X8, BLOCK_4X4 } },
        { { BLOCK_8X16, BLOCK_8X8 }, { BLOCK_INVALID, BLOCK_4X8 } },
        { { BLOCK_16X8, BLOCK_INVALID }, { BLOCK_8X8, BLOCK_8X4 } },
        { { BLOCK_16X16, BLOCK_16X8 }, { BLOCK_8X16, BLOCK_8X8 } },
        { { BLOCK_16X32, BLOCK_16X16 }, { BLOCK_INVALID, BLOCK_8X16 } },
        { { BLOCK_32X16, BLOCK_INVALID }, { BLOCK_16X16, BLOCK_16X8 } },
        { { BLOCK_32X32, BLOCK_32X16 }, { BLOCK_16X32, BLOCK_16X16 } },
        { { BLOCK_32X64, BLOCK_32X32 }, { BLOCK_INVALID, BLOCK_16X32 } },
        { { BLOCK_64X32, BLOCK_INVALID }, { BLOCK_32X32, BLOCK_32X16 } },
        { { BLOCK_64X64, BLOCK_64X32 }, { BLOCK_32X64, BLOCK_32X32 } },
        { { BLOCK_64X128, BLOCK_64X64 }, { BLOCK_INVALID, BLOCK_32X64 } },
        { { BLOCK_128X64, BLOCK_INVALID }, { BLOCK_64X64, BLOCK_64X32 } },
        { { BLOCK_128X128, BLOCK_128X64 }, { BLOCK_64X128, BLOCK_64X64 } },
        { { BLOCK_4X16, BLOCK_4X8 }, { BLOCK_INVALID, BLOCK_4X8 } },
        { { BLOCK_16X4, BLOCK_INVALID }, { BLOCK_8X4, BLOCK_8X4 } },
        { { BLOCK_8X32, BLOCK_8X16 }, { BLOCK_INVALID, BLOCK_4X16 } },
        { { BLOCK_32X8, BLOCK_INVALID }, { BLOCK_16X8, BLOCK_16X4 } },
        { { BLOCK_16X64, BLOCK_16X32 }, { BLOCK_INVALID, BLOCK_8X32 } },
        { { BLOCK_64X16, BLOCK_INVALID }, { BLOCK_32X16, BLOCK_32X8 } },
    };

    internal static int PlaneBsize(int bsize, int ssX, int ssY) => SsSizeLookup[bsize, ssX, ssY];

    [Fact]
    public void SetupScaleFactorsMatchesLibaom()
    {
        var rng = new Random(400);
        var expected = new int[4];
        for (int trial = 0; trial < 20000; trial++)
        {
            int ow, oh, tw, th;
            switch (rng.Next(4))
            {
                case 0:  // identical sizes (the realtime case)
                    ow = tw = rng.Next(1, 8193);
                    oh = th = rng.Next(1, 8193);
                    break;
                case 1:  // valid ratios: other within [this / 2, this * 16]
                    tw = rng.Next(1, 4097);
                    th = rng.Next(1, 4097);
                    ow = Math.Max(1, (int)(tw * (0.5 + rng.NextDouble() * 15.5)));
                    oh = Math.Max(1, (int)(th * (0.5 + rng.NextDouble() * 15.5)));
                    break;
                default:
                    ow = rng.Next(1, 16385);
                    oh = rng.Next(1, 16385);
                    tw = rng.Next(1, 16385);
                    th = rng.Next(1, 16385);
                    break;
            }
            E3Shim.aomshim_e3_setup_scale_factors(ow, oh, tw, th, expected);
            var sf = new scale_factors();
            av1_setup_scale_factors_for_frame(sf, ow, oh, tw, th);
            Assert.Equal(expected, new[] { sf.x_scale_fp, sf.y_scale_fp, sf.x_step_q4, sf.y_step_q4 });
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void SingleRefContextMatchesLibaom(int which)
    {
        var rng = new Random(410 + which);
        var xd = new MACROBLOCKD();
        var counts = new byte[REF_FRAMES];
        for (int trial = 0; trial < 20000; trial++)
        {
            int max = rng.Next(3) == 0 ? 3 : 17;
            for (int i = 0; i < counts.Length; i++) counts[i] = (byte)rng.Next(max);
            int expected = E3Shim.aomshim_e3_single_ref_ctx(counts, which);
            counts.CopyTo(xd.neighbors_ref_counts, 0);
            int actual = which switch
            {
                1 => av1_get_pred_context_single_ref_p1(xd),
                3 => av1_get_pred_context_single_ref_p3(xd),
                4 => av1_get_pred_context_single_ref_p4(xd),
                _ => av1_get_pred_context_single_ref_p5(xd),
            };
            Assert.Equal(expected, actual);
        }
    }

    // Random mode info cell (shim layout): bsize, mode, ref0, ref1, mv0, mv1, use_intrabc, filters.
    internal static void RandomCell(Random rng, int[] cells, int index, bool allowCompound = true)
    {
        int o = index * 8;
        int ref0 = rng.Next(4) == 0 ? INTRA_FRAME : rng.Next(LAST_FRAME, ALTREF_FRAME + 1);
        int ref1 = NONE_FRAME;
        if (allowCompound && ref0 > INTRA_FRAME)
        {
            int r = rng.Next(5);
            if (r == 0) ref1 = rng.Next(LAST_FRAME, ALTREF_FRAME + 1);
            else if (r == 1) ref1 = INTRA_FRAME;  // inter-intra style
        }
        cells[o + 0] = rng.Next(BLOCK_SIZES_ALL);
        cells[o + 1] = ref0 > INTRA_FRAME ? rng.Next(NEARESTMV, NEW_NEWMV + 1) : rng.Next(INTRA_MODES);
        cells[o + 2] = ref0;
        cells[o + 3] = ref1;
        cells[o + 4] = (ushort)rng.Next(-600, 601) | (rng.Next(-600, 601) << 16);
        cells[o + 5] = (ushort)rng.Next(-600, 601) | (rng.Next(-600, 601) << 16);
        cells[o + 6] = rng.Next(16) == 0 ? 1 : 0;
        cells[o + 7] = rng.Next(3) | (rng.Next(3) << 16);
    }

    internal static MB_MODE_INFO ToMbmi(int[] cells, int index)
    {
        int o = index * 8;
        var m = new MB_MODE_INFO
        {
            bsize = cells[o + 0],
            mode = cells[o + 1],
            use_intrabc = (byte)(cells[o + 6] & 1),
            motion_mode = SIMPLE_TRANSLATION,
        };
        m.ref_frame[0] = cells[o + 2];
        m.ref_frame[1] = cells[o + 3];
        m.mv[0].as_int = unchecked((uint)cells[o + 4]);
        m.mv[1].as_int = unchecked((uint)cells[o + 5]);
        m.interp_filters.as_int = unchecked((uint)cells[o + 7]);
        m.interinter_comp.type = COMPOUND_AVERAGE;
        return m;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void NeighbourContextMatchesLibaom(int which)
    {
        var rng = new Random(420 + which);
        var cells = new int[4 * 8];
        var grid = new MB_MODE_INFO[4];
        var xd = new MACROBLOCKD();
        for (int trial = 0; trial < 20000; trial++)
        {
            for (int i = 0; i < 4; i++) RandomCell(rng, cells, i);
            // The current block is inter (these contexts are only coded for inter blocks).
            if (cells[3 * 8 + 2] <= INTRA_FRAME) cells[3 * 8 + 2] = rng.Next(LAST_FRAME, ALTREF_FRAME + 1);
            bool up = rng.Next(4) != 0, left = rng.Next(4) != 0;
            int expected = E3Shim.aomshim_e3_neighbour_ctx(cells, up ? 1 : 0, left ? 1 : 0, which);

            for (int i = 0; i < 4; i++) grid[i] = ToMbmi(cells, i);
            xd.mi = new Ptr<MB_MODE_INFO>(grid, 3);
            xd.mi_stride = 2;
            xd.up_available = up;
            xd.left_available = left;
            xd.above_mbmi = up ? grid[1] : null;
            xd.left_mbmi = left ? grid[2] : null;
            int actual = which switch
            {
                0 => av1_get_intra_inter_context(xd),
                1 => av1_get_comp_reference_type_context(xd),
                2 => av1_get_pred_context_switchable_interp(xd, 0),
                _ => av1_get_pred_context_switchable_interp(xd, 1),
            };
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void SetEntropyContextsMatchesLibaom()
    {
        var rng = new Random(430);
        var xd = new MACROBLOCKD();
        const int N = 64;
        var aboveC = new sbyte[N];
        var leftC = new sbyte[N];
        var aboveS = new int[N];
        var leftS = new int[N];
        for (int trial = 0; trial < 20000; trial++)
        {
            int plane = rng.Next(3);
            int ssX = plane == 0 ? 0 : rng.Next(2), ssY = plane == 0 ? 0 : rng.Next(2);
            int bsize = rng.Next(BLOCK_SIZES_ALL);
            int planeBsize = PlaneBsize(bsize, ssX, ssY);
            if (planeBsize == BLOCK_INVALID) continue;
            int tx = rng.Next(TX_SIZES_ALL);
            int bw = mi_size_wide[planeBsize], bh = mi_size_high[planeBsize];
            if (tx_size_wide_unit[tx] > bw || tx_size_high_unit[tx] > bh) continue;
            int aoff = rng.Next(bw / tx_size_wide_unit[tx]) * tx_size_wide_unit[tx];
            int loff = rng.Next(bh / tx_size_high_unit[tx]) * tx_size_high_unit[tx];
            int hasEob = rng.Next(2);
            int toRight = rng.Next(2) == 0 ? 0 : -32 * rng.Next(0, 32);
            int toBottom = rng.Next(2) == 0 ? 0 : -32 * rng.Next(0, 32);
            // Transform blocks are only coded when they start inside the visible area
            // (max_block_wide / max_block_high), otherwise C would memset a negative count.
            int blocksWide = (block_size_wide[planeBsize] + (toRight < 0 ? toRight >> (3 + ssX) : 0)) >> 2;
            int blocksHigh = (block_size_high[planeBsize] + (toBottom < 0 ? toBottom >> (3 + ssY) : 0)) >> 2;
            if (aoff >= blocksWide || loff >= blocksHigh) continue;
            for (int i = 0; i < N; i++)
            {
                aboveC[i] = (sbyte)rng.Next(-3, 4);
                leftC[i] = (sbyte)rng.Next(-3, 4);
                aboveS[i] = aboveC[i];
                leftS[i] = leftC[i];
            }
            E3Shim.aomshim_e3_set_entropy_contexts(plane, ssX, ssY, toRight, toBottom, planeBsize, tx, hasEob,
                                                   aoff, loff, aboveC, leftC);

            macroblockd_plane pd = xd.plane[plane];
            pd.subsampling_x = ssX;
            pd.subsampling_y = ssY;
            pd.above_entropy_context = aboveS;
            pd.left_entropy_context = leftS;
            xd.mb_to_right_edge = toRight;
            xd.mb_to_bottom_edge = toBottom;
            av1_set_entropy_contexts(xd, pd, plane, planeBsize, tx, hasEob, aoff, loff);

            for (int i = 0; i < N; i++)
            {
                Assert.Equal(aboveC[i], aboveS[i]);
                Assert.Equal(leftC[i], leftS[i]);
            }
        }
    }
}
