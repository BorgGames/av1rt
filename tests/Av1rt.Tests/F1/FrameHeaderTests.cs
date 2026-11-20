using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.bitstream;

namespace Av1rt.Tests.F1;

/// <summary>
/// av1_write_obu_tg_tile_headers (OBU header + write_uncompressed_header_obu + tile group
/// header) on random, self-consistent frame parameters, against libaom: bytes, header sizes
/// and the saved bit buffer position (where the tile size / context tile id are patched in).
/// </summary>
public class FrameHeaderTests
{
    [Theory]
    [InlineData(176, 144, 12)]
    [InlineData(352, 288, 12)]
    [InlineData(640, 360, 12)]
    [InlineData(1280, 720, 12)]
    [InlineData(1366, 766, 12)]
    [InlineData(1920, 1080, 15)]
    [InlineData(3840, 2160, 15)]
    [InlineData(3840, 2160, 12)]
    public void FrameHeaderMatchesLibaom(int width, int height, int sbSize)
    {
        var rng = new Random(width * 7 + height * 3 + sbSize);
        for (int iter = 0; iter < 400; iter++)
        {
            int[] seq = F1Params.RandomSeq(rng, width, height, sbSize, forFrame: true);
            int[] fh = F1Params.RandomFrame(rng, seq);
            Compare(seq, fh, iter);
        }
    }

    [Fact]
    public void ReducedStillPictureHeaderMatchesLibaom()
    {
        var rng = new Random(5);
        for (int iter = 0; iter < 100; iter++)
        {
            int[] seq = F1Params.RandomSeq(rng, rng.Next(16, 2000), rng.Next(16, 2000), rng.Next(2) == 0 ? 12 : 15,
                                           forFrame: true, stillPicture: true);
            int[] fh = F1Params.RandomFrame(rng, seq);
            Compare(seq, fh, iter);
        }
    }

    /// <summary>The realtime subset's typical shape: inter frames, no order hint / frame ids,
    /// segmentation on (cyclic refresh), CDEF on, one tile group, 1..16 tiles.</summary>
    [Fact]
    public void RealtimeSubsetFrameHeaderMatchesLibaom()
    {
        var rng = new Random(6);
        for (int iter = 0; iter < 500; iter++)
        {
            int[] seq = F1Params.RandomSeq(rng, 1920, 1080, 15, forFrame: true);
            seq[F1Params.SEQ_PROFILE] = 0;
            seq[F1Params.SEQ_BIT_DEPTH] = 8;
            seq[F1Params.SEQ_MONOCHROME] = 0;
            seq[F1Params.SEQ_SUBSAMPLING_X] = seq[F1Params.SEQ_SUBSAMPLING_Y] = 1;
            seq[F1Params.SEQ_ENABLE_ORDER_HINT] = 0;
            seq[F1Params.SEQ_FRAME_ID_NUMBERS_PRESENT_FLAG] = 0;
            seq[F1Params.SEQ_FORCE_SCREEN_CONTENT_TOOLS] = 0;
            seq[F1Params.SEQ_FORCE_INTEGER_MV] = 2;
            seq[F1Params.SEQ_ENABLE_SUPERRES] = 0;
            seq[F1Params.SEQ_ENABLE_CDEF] = 1;
            int[] fh = F1Params.RandomFrame(rng, seq);
            fh[F1Params.FH_NUM_TG] = 1;
            fh[F1Params.FH_SEG_ENABLED] = 1;
            fh[F1Params.FH_SHOW_EXISTING_FRAME] = 0;
            fh[F1Params.FH_SUPERRES_SCALE_DENOMINATOR] = 8;
            Compare(seq, fh, iter);
        }
    }

    private static void Compare(int[] seq, int[] fh, int iter)
    {
        var expected = new byte[1024];
        var output = new int[7];
        F1Shim.f1_write_obu_tg_tile_headers(seq, fh, expected, output);

        AV1_COMP cpi = F1Params.BuildCpi(seq, fh);
        var actual = new byte[1024];
        var savedWb = new aom_write_bit_buffer[1];
        var totalSize = new uint[1];
        var pb = new PackBSParams
        {
            saved_wb = savedWb,
            total_size = totalSize,
            dst = actual,
            tile_data_curr = actual,
            obu_extn_header = (byte)fh[F1Params.FH_OBU_EXTN_HEADER],
        };
        av1_write_obu_tg_tile_headers(cpi, cpi.td.mb.e_mbd, pb, fh[F1Params.FH_TILE_IDX]);

        string where = $"iter {iter}";
        Assert.True(output[0] == pb.curr_tg_hdr_size, $"{where}: curr_tg_hdr_size {output[0]} vs {pb.curr_tg_hdr_size}");
        Assert.Equal(output[1], (int)pb.obu_header_size);
        Assert.Equal(output[2], (int)totalSize[0]);
        Assert.True(expected.AsSpan(0, output[2]).SequenceEqual(actual.AsSpan(0, output[2])), $"{where}: header bytes differ");
        Assert.Equal(output[3], savedWb[0].bit_buffer.IsNull ? -1 : savedWb[0].bit_buffer.Offset);
        Assert.Equal(output[4], (int)savedWb[0].bit_offset);
        Assert.Equal(output[5], cpi.frame_header_count);
        Assert.Equal(output[6], cpi.common.current_frame.frame_refs_short_signaling);
    }
}
