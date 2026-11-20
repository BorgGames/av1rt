using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.av1_loopfilter;

namespace Av1rt.Tests.E4;

/// <summary>av1/common/av1_loopfilter.c: threshold and level tables against libaom.</summary>
[Trait("Category", "Native")]
public class LoopFilterTableTests
{
    private static void AssertThresholds(byte[] expected, AV1_COMMON cm, string what)
    {
        for (int l = 0; l <= 63; l++)
        {
            loop_filter_thresh t = cm.lf_info.lfthr[l];
            Assert.True(expected.AsSpan(l * 48, 16).SequenceEqual(t.mblim), $"{what}: mblim level {l}");
            Assert.True(expected.AsSpan(l * 48 + 16, 16).SequenceEqual(t.lim), $"{what}: lim level {l}");
            Assert.True(expected.AsSpan(l * 48 + 32, 16).SequenceEqual(t.hev_thr), $"{what}: hev_thr level {l}");
        }
    }

    [Fact]
    public void LoopFilterInitMatchesLibaom()
    {
        for (int sharpness = 0; sharpness <= 7; sharpness++)
        {
            var expected = new byte[64 * 48];
            E4Shim.aomshim_e4_lf_init(sharpness, expected);
            var cm = new AV1_COMMON();
            cm.lf.sharpness_level = sharpness;
            av1_loop_filter_init(cm);
            AssertThresholds(expected, cm, $"sharpness {sharpness}");
        }
    }

    [Fact]
    public void LoopFilterFrameInitMatchesLibaom()
    {
        var rng = new Random(77);
        (int, int)[] ranges = { (0, 3), (0, 1), (1, 3), (0, 2), (2, 3) };
        for (int iter = 0; iter < 3000; iter++)
        {
            int Level() => rng.Next(4) == 0 ? 0 : rng.Next(64);
            int[] levels = { Level(), Level(), Level(), Level(), rng.Next(8) };
            int deltaEnabled = rng.Next(2);
            var refDeltas = new sbyte[8];
            var modeDeltas = new sbyte[2];
            for (int i = 0; i < 8; i++) refDeltas[i] = (sbyte)rng.Next(-63, 64);
            for (int i = 0; i < 2; i++) modeDeltas[i] = (sbyte)rng.Next(-63, 64);
            int segEnabled = rng.Next(2);
            var segData = new short[64];
            var segMask = new uint[8];
            for (int s = 0; s < 8; s++)
            {
                segMask[s] = (uint)rng.Next(256);
                for (int f = 0; f < 8; f++) segData[s * 8 + f] = (short)rng.Next(-63, 64);
            }
            (int start, int end) = ranges[rng.Next(ranges.Length)];

            var lvl = new byte[3 * 8 * 2 * 8 * 2];
            var thr = new byte[64 * 48];
            E4Shim.aomshim_e4_lf_frame_init(levels, deltaEnabled, refDeltas, modeDeltas, segEnabled, segData,
                                            segMask, start, end, lvl, thr);

            var cm = new AV1_COMMON();
            cm.lf.filter_level[0] = levels[0];
            cm.lf.filter_level[1] = levels[1];
            cm.lf.filter_level_u = levels[2];
            cm.lf.filter_level_v = levels[3];
            cm.lf.sharpness_level = levels[4];
            cm.lf.mode_ref_delta_enabled = (byte)deltaEnabled;
            Array.Copy(refDeltas, cm.lf.ref_deltas, 8);
            Array.Copy(modeDeltas, cm.lf.mode_deltas, 2);
            cm.seg.enabled = (byte)segEnabled;
            for (int s = 0; s < 8; s++)
            {
                cm.seg.feature_mask[s] = segMask[s];
                for (int f = 0; f < 8; f++) cm.seg.feature_data[s][f] = segData[s * 8 + f];
            }
            av1_loop_filter_frame_init(cm, start, end);

            int k = 0;
            for (int p = 0; p < 3; p++)
                for (int s = 0; s < 8; s++)
                    for (int d = 0; d < 2; d++)
                        for (int r = 0; r < 8; r++)
                            for (int m = 0; m < 2; m++, k++)
                                Assert.True(lvl[k] == cm.lf_info.lvl[p][s][d][r][m],
                                            $"iteration {iter}: lvl[{p}][{s}][{d}][{r}][{m}] = {cm.lf_info.lvl[p][s][d][r][m]}, libaom {lvl[k]}");
            AssertThresholds(thr, cm, $"iteration {iter}");
        }
    }
}
