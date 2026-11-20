using System;
using System.Linq;
using Av1rt.Aom;
using Xunit;
using Xunit.Abstractions;
using static Av1rt.Aom.aomcx;
using static Av1rt.Aom.block;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.enc_enums;
using static Av1rt.Aom.encoder;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.speed_features;
using static Av1rt.Aom.var_based_part;

namespace Av1rt.Tests.F3;

// Differential tests of var_based_part.c against libaom (shim_f3.c).
public class VarBasedPartTests
{
    private readonly ITestOutputHelper _output;

    public VarBasedPartTests(ITestOutputHelper output) => _output = output;

    private static readonly (int w, int h)[] Resolutions =
    {
        (176, 144), (320, 180), (320, 240), (352, 288), (640, 360), (640, 480), (854, 480), (1280, 720),
        (1920, 1080), (2560, 1440), (3840, 2160),
    };

    // av1_set_variance_partition_thresholds() -> set_vbp_thresholds() and its helpers, over
    // qindex, resolution, key/inter frames, noise level, content, speed features.
    [Fact]
    public void VariancePartitionThresholdsMatchLibaom()
    {
        var rng = new Random(0xF3);
        for (int iter = 0; iter < 20000; ++iter)
        {
            var (w, h) = Resolutions[rng.Next(Resolutions.Length)];
            int[] p = P.Default(w, h);
            p[P.FRAME_TYPE] = rng.Next(3) == 0 ? KEY_FRAME : INTER_FRAME;
            p[P.BASE_QINDEX] = rng.Next(256);
            p[P.QINDEX] = rng.Next(4) == 0 ? p[P.BASE_QINDEX] : rng.Next(256);
            p[P.LOWSUMDIFF] = rng.Next(2);
            p[P.SPEED] = 7 + rng.Next(5);
            p[P.MODE] = rng.Next(8) == 0 ? ALLINTRA : REALTIME;
            p[P.CONTENT] = rng.Next(4) == 0 ? AOM_CONTENT_SCREEN : AOM_CONTENT_DEFAULT;
            p[P.FORCE_LARGE_INTRA] = rng.Next(2);
            p[P.SPLIT_SHIFT] = 8 + rng.Next(3);
            p[P.PREFER_LARGE] = rng.Next(4);
            p[P.VAR_PART_QIDX] = rng.Next(5);
            p[P.DISABLE_8X8_QIDX] = rng.Next(2);
            p[P.NOISE_ENABLED] = rng.Next(2);
            p[P.NOISE_THRESH] = rng.Next(1, 200);
            p[P.NOISE_VALUE] = rng.Next(0, 500);
            p[P.FRAME_NUMBER] = rng.Next(2) == 0 ? rng.Next(70) : rng.Next(1000);
            p[P.NON_REF_FRAME] = rng.Next(2);
            p[P.FRAME_SOURCE_SAD] = rng.Next(2) == 0 ? 0 : rng.Next(100000);
            p[P.HIGH_MOTION_SCREEN] = rng.Next(2);
            p[P.PARTITION_SEARCH_TYPE] = rng.Next(10) == 0 ? FIXED_PARTITION : VAR_BASED_PARTITION;

            var expected = new long[6];
            F3Shim.aomshim_f3_vbp_thresholds(p, expected);

            AV1_COMP cpi = F3Context.Create(p);
            for (int i = 0; i < 5; ++i) cpi.vbp_info.thresholds[i] = -12345;
            cpi.vbp_info.threshold_minmax = -12345;
            av1_set_variance_partition_thresholds(cpi, p[P.QINDEX], p[P.LOWSUMDIFF]);
            long[] actual = cpi.vbp_info.thresholds.Append(cpi.vbp_info.threshold_minmax).ToArray();
            Assert.True(expected.SequenceEqual(actual),
                        $"p=[{P.Describe(p)}]: C [{string.Join(",", expected)}] C# [{string.Join(",", actual)}]");
        }
    }

    // av1_get_force_skip_low_temp_var() / av1_get_force_skip_low_temp_var_small_sb() for every
    // block size at every aligned position of a superblock.
    [Fact]
    public void ForceSkipLowTempVarMatchesLibaom()
    {
        var rng = new Random(0x105);
        var variance_low = new byte[105];
        for (int iter = 0; iter < 40; ++iter)
        {
            for (int i = 0; i < variance_low.Length; ++i) variance_low[i] = (byte)(rng.Next(3) == 0 ? 1 : 0);
            if (iter == 0) Array.Fill(variance_low, (byte)1);
            for (int small = 0; small < 2; ++small)
            {
                int sbMi = small != 0 ? 16 : 32;
                for (int bsize = 0; bsize < BLOCK_SIZES_ALL; ++bsize)
                {
                    int bw = mi_size_wide[bsize], bh = mi_size_high[bsize];
                    if (bw > sbMi || bh > sbMi) continue;
                    for (int r = 0; r < sbMi; r += bh)
                    {
                        for (int c = 0; c < sbMi; c += bw)
                        {
                            // The superblock origin is arbitrary: only the low bits matter.
                            int miRow = sbMi * rng.Next(8) + r, miCol = sbMi * rng.Next(8) + c;
                            int expected = F3Shim.aomshim_f3_force_skip_low_temp_var(variance_low, miRow, miCol,
                                                                                      bsize, small);
                            int actual = small != 0
                                ? av1_get_force_skip_low_temp_var_small_sb(variance_low, miRow, miCol, bsize)
                                : av1_get_force_skip_low_temp_var(variance_low, miRow, miCol, bsize);
                            Assert.True(expected == actual,
                                        $"small={small} bsize={bsize} mi=({miRow},{miCol}): C {expected} C# {actual}");
                        }
                    }
                }
            }
        }
    }

    private void CompareVbp(string name, int[] p, byte[] frames, ulong[] srcSad, int[] sbInfo)
    {
        uint[] thresh = F3Context.ThreshExitPart();
        F3Context.VbpResult c = F3Context.RunC(p, frames, thresh, srcSad, sbInfo);
        F3Context.VbpResult cs = F3Context.RunPort(p, frames, thresh, srcSad, sbInfo);
        Assert.Equal(c.NumSb, cs.NumSb);
        Assert.True(c.Thresholds.SequenceEqual(cs.Thresholds),
                    $"{name}: thresholds C [{string.Join(",", c.Thresholds)}] C# [{string.Join(",", cs.Thresholds)}]");
        for (int sb = 0; sb < c.NumSb; ++sb)
        {
            int o = sb * F3Context.SbOutCount;
            for (int i = 0; i < F3Context.SbOutCount; ++i)
            {
                Assert.True(c.SbOut[o + i] == cs.SbOut[o + i],
                            $"{name}: sb {sb} out[{i}] C {c.SbOut[o + i]} C# {cs.SbOut[o + i]} " +
                            $"(sb info [{string.Join(",", sbInfo.Skip(sb * F3Context.SbInfoCount).Take(F3Context.SbInfoCount))}])");
            }
        }
        for (int i = 0; i < c.VtOut.Length; ++i)
        {
            if (c.VtOut[i] != cs.VtOut[i])
            {
                int perSb = c.VtOut.Length / c.NumSb;
                Assert.Fail($"{name}: variance tree differs at sb {i / perSb} int {i % perSb}: C {c.VtOut[i]} C# {cs.VtOut[i]}");
            }
        }
        var (_, miCols, _, _) = F3Context.Dims(p);
        for (int i = 0; i < c.GridOut.Length; ++i)
        {
            Assert.True(c.GridOut[i] == cs.GridOut[i],
                        $"{name}: mi grid ({i / miCols},{i % miCols}) C {c.GridOut[i]} C# {cs.GridOut[i]}");
        }

        // Summary of what the case exercised.
        int sbs = c.NumSb, exits = 0, notLast = 0, colorSens = 0;
        for (int sb = 0; sb < sbs; ++sb)
        {
            int o = sb * F3Context.SbOutCount;
            if (c.SbOut[o + 1] == 1) ++exits;
            if (c.SbOut[o + 4] != LAST_FRAME) ++notLast;
            if (c.SbOut[o + 7] != 0 || c.SbOut[o + 8] != 0) ++colorSens;
        }
        var sizes = c.GridOut.Where(b => b >= 0).GroupBy(b => b).OrderBy(g => g.Key)
                     .Select(g => $"{g.Key}:{g.Count()}");
        _output.WriteLine($"{name}: {sbs} SBs, zeromv exits {exits}, non-LAST ref {notLast}, color sens {colorSens}, " +
                          $"block sizes [{string.Join(" ", sizes)}]");
    }

    private static int[] SbInfo(Random rng, int numSb, int maxSad, bool segments)
    {
        var info = new int[numSb * F3Context.SbInfoCount];
        for (int sb = 0; sb < numSb; ++sb)
        {
            int o = sb * F3Context.SbInfoCount;
            info[o] = segments ? rng.Next(3) : 0;
            info[o + 1] = rng.Next(maxSad + 1);
            info[o + 2] = rng.Next(maxSad + 1);
            info[o + 3] = rng.Next(4) == 0 ? 1 : 0;
            info[o + 4] = rng.Next(2);
        }
        return info;
    }

    private static ulong[] SrcSad(Random rng, int width, int height)
    {
        var sad = new ulong[((width + 63) / 64 + 1) * ((height + 63) / 64 + 1)];
        for (int i = 0; i < sad.Length; ++i) sad[i] = (ulong)rng.Next(80000);
        return sad;
    }

    // av1_choose_var_based_partitioning() on key frames (4x4-average variance tree): every
    // superblock of the frame, compared on the resulting mi grid, the variance tree, the
    // variance_low flags and the per-superblock state.
    [Theory]
    [InlineData(352, 288, 0, 1)]
    [InlineData(320, 240, 0, 2)]
    [InlineData(640, 360, 1, 3)]
    [InlineData(200, 136, 1, 4)]
    [InlineData(1280, 720, 0, 5)]
    public void ChooseVarBasedPartitioningKeyFrameMatchesLibaom(int width, int height, int sb128, int seed)
    {
        var rng = new Random(seed);
        for (int iter = 0; iter < 4; ++iter)
        {
            int[] p = P.Default(width, height);
            p[P.SB128] = sb128;
            p[P.FRAME_TYPE] = KEY_FRAME;
            p[P.BASE_QINDEX] = rng.Next(256);
            p[P.QINDEX] = p[P.BASE_QINDEX];
            p[P.FORCE_LARGE_INTRA] = iter % 2;
            p[P.SPLIT_SHIFT] = 8 + rng.Next(2);
            p[P.MI_ALLOC_BSIZE] = iter == 3 ? BLOCK_4X4 : BLOCK_8X8;
            p[P.SHORT_CIRCUIT] = iter == 2 ? 0 : 1;
            var (_, _, numSb, _) = F3Context.Dims(p);
            byte[] frames = F3Context.MakeFrames(width, height, seed * 100 + iter, 3);
            CompareVbp($"key {width}x{height} sb128={sb128} iter={iter} q={p[P.QINDEX]}", p, frames,
                       SrcSad(rng, width, height), SbInfo(rng, numSb, kLowSad, false));
        }
    }

    // av1_choose_var_based_partitioning() on inter frames: reference selection (LAST/GOLDEN),
    // chroma_check, the 8x8-average variance tree, the zero-mv superblock early exit
    // (set_force_zeromv_skip_for_sb), cyclic-refresh segments and the src_sad_blk_64x64 input.
    [Theory]
    [InlineData(320, 240, 0, 11)]
    [InlineData(352, 288, 0, 12)]
    [InlineData(176, 144, 0, 13)]
    [InlineData(640, 360, 1, 14)]
    [InlineData(640, 480, 0, 15)]
    [InlineData(1280, 720, 1, 16)]
    [InlineData(1280, 720, 0, 17)]
    public void ChooseVarBasedPartitioningInterFrameMatchesLibaom(int width, int height, int sb128, int seed)
    {
        var rng = new Random(seed);
        for (int iter = 0; iter < 6; ++iter)
        {
            int[] p = P.Default(width, height);
            p[P.SB128] = sb128;
            p[P.FRAME_TYPE] = INTER_FRAME;
            p[P.BASE_QINDEX] = rng.Next(256);
            p[P.QINDEX] = p[P.BASE_QINDEX];
            p[P.PREFER_LARGE] = iter < 3 ? 3 : rng.Next(4);
            p[P.VAR_PART_QIDX] = rng.Next(5);
            p[P.USE_RTC_TF] = rng.Next(3);
            // estimate_motion_for_var_based_partition: 0, or 3 (then source_sad_nonrd stays <= kMedSad so
            // the integral-projection motion search is not run; neighbours are unavailable).
            p[P.EST_MOTION] = iter % 2 == 0 ? 0 : 3;
            p[P.FRAMES_SINCE_KEY] = iter % 3 == 0 ? 10 : 40;
            p[P.FRAME_SOURCE_SAD] = rng.Next(40000);
            p[P.AVG_SOURCE_SAD] = rng.Next(60000);
            p[P.HIGH_SOURCE_SAD] = rng.Next(4) == 0 ? 1 : 0;
            p[P.NOISE_ENABLED] = rng.Next(2);
            p[P.NOISE_THRESH] = 100;
            p[P.NOISE_VALUE] = rng.Next(300);
            p[P.FRAME_NUMBER] = rng.Next(200);
            p[P.REF_FRAME_FLAGS] = iter % 3 == 2 ? AOM_LAST_FLAG : AOM_LAST_FLAG | AOM_GOLD_FLAG;
            p[P.SEG_ENABLED] = iter >= 4 ? 1 : 0;
            p[P.SEG_DELTA_Q1] = -rng.Next(30);
            p[P.SEG_DELTA_Q2] = -rng.Next(60);
            p[P.USE_SRC_SAD_BLK] = rng.Next(2);
            p[P.NUM_TEMPORAL_LAYERS] = iter == 5 ? 3 : 1;
            p[P.MI_ALLOC_BSIZE] = iter == 1 ? BLOCK_4X4 : BLOCK_8X8;
            var (_, _, numSb, _) = F3Context.Dims(p);
            int refNoise = iter switch { 0 => 0, 1 => 1, 2 => 2, 3 => 8, _ => rng.Next(6) };
            byte[] frames = F3Context.MakeFrames(width, height, seed * 100 + iter, refNoise);
            int maxSad = p[P.EST_MOTION] == 0 ? kHighSad : kMedSad;
            CompareVbp($"inter {width}x{height} sb128={sb128} iter={iter} q={p[P.QINDEX]} noise={refNoise}", p, frames,
                       SrcSad(rng, width, height), SbInfo(rng, numSb, maxSad, p[P.SEG_ENABLED] != 0));
        }
    }
}
