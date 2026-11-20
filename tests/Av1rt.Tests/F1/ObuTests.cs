using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.bitstream;
using static Av1rt.Aom.encodetxb;

namespace Av1rt.Tests.F1;

/// <summary>
/// OBU header, OBU size (leb128), sequence header OBU, av1_neg_interleave and
/// av1_get_eob_pos_token against libaom.
/// </summary>
public class ObuTests
{
    [Fact]
    public void ParameterLayoutsMatchShim() => F1Params.CheckLayouts();

    [Fact]
    public void ObuHeaderMatchesLibaom()
    {
        var rng = new Random(11);
        for (int obuType = 0; obuType < 16; obuType++)
            for (int nonzero = 0; nonzero < 2; nonzero++)
                for (int layer = 0; layer < 2; layer++)
                    for (int keep = 0; keep < 2; keep++)
                        for (int rep = 0; rep < 4; rep++)
                        {
                            int ext = nonzero != 0 ? rng.Next(256) : 0;
                            var expected = new byte[4];
                            var output = new int[1];
                            uint expectedSize = F1Shim.f1_write_obu_header(keep, obuType, nonzero, layer, ext,
                                                                           expected, output);

                            var lp = new AV1LevelParams { keep_level_stats = (uint)keep };
                            int count = 0;
                            var actual = new byte[4];
                            uint size = av1_write_obu_header(lp, ref count, obuType, nonzero != 0, layer != 0,
                                                             ext, actual);
                            Assert.Equal(expectedSize, size);
                            Assert.Equal(expected, actual);
                            Assert.Equal(output[0], count);
                        }
    }

    [Fact]
    public void UlebObuSizeMatchesLibaom()
    {
        var rng = new Random(12);
        var values = new System.Collections.Generic.List<int>
        {
            0, 1, 127, 128, 255, 16383, 16384, (1 << 21) - 1, 1 << 21, (1 << 28) - 1, 1 << 28,
            int.MaxValue
        };
        for (int i = 0; i < 200; i++) values.Add(rng.Next(1 << rng.Next(1, 31)));
        foreach (int v in values)
            for (int destSize = 1; destSize <= 8; destSize++)
            {
                var expected = new byte[8];
                int expectedRet = F1Shim.f1_write_uleb_obu_size((ulong)v, expected, destSize);
                var actual = new byte[8];
                int ret = av1_write_uleb_obu_size(v, actual, destSize);
                Assert.Equal(expectedRet, ret);
                Assert.Equal(expected, actual);
            }
    }

    [Theory]
    [InlineData(16, 16, 12)]
    [InlineData(176, 144, 12)]
    [InlineData(352, 288, 12)]
    [InlineData(640, 360, 12)]
    [InlineData(1280, 720, 12)]
    [InlineData(1366, 766, 12)]
    [InlineData(1920, 1080, 12)]
    [InlineData(1920, 1080, 15)]
    [InlineData(3840, 2160, 15)]
    [InlineData(7680, 4320, 15)]
    [InlineData(65536, 65536, 15)]
    public void SequenceHeaderMatchesLibaom(int width, int height, int sbSize)
    {
        var rng = new Random(width * 31 + height + sbSize);
        for (int iter = 0; iter < 300; iter++)
        {
            int[] seq = F1Params.RandomSeq(rng, width, height, sbSize, forFrame: false);
            if (seq[F1Params.SEQ_ENABLE_RESTORATION] != 0 && iter % 2 == 0)
                seq[F1Params.SEQ_ENABLE_RESTORATION] = 0;
            var expected = new byte[64];
            uint expectedSize = F1Shim.f1_write_sequence_header_obu(seq, expected, expected.Length);

            var actual = new byte[64];
            uint size = av1_write_sequence_header_obu(F1Params.BuildSeq(seq), actual, actual.Length);
            Assert.Equal(expectedSize, size);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void NegInterleaveMatchesLibaom()
    {
        for (int max = 1; max <= 8; max++)
            for (int r = 0; r <= max; r++)
                for (int x = 0; x < max; x++)
                    Assert.Equal(F1Shim.f1_neg_interleave(x, r, max), av1_neg_interleave(x, r, max));
    }

    [Fact]
    public void EobPosTokenMatchesLibaom()
    {
        for (int eob = 0; eob <= 1024; eob++)
        {
            int expected = F1Shim.f1_get_eob_pos_token(eob, out int expectedExtra);
            int extra = -1;
            Assert.Equal(expected, av1_get_eob_pos_token(eob, ref extra));
            Assert.Equal(expectedExtra, extra);
        }
    }
}
