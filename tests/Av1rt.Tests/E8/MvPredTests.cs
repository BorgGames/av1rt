using System;
using System.Runtime.InteropServices;
using Av1rt.Aom;
using Xunit;
using Xunit.Abstractions;
using static Av1rt.Aom.enums;

namespace Av1rt.Tests.E8;

// av1_mv_pred (with enc_clamp_mv) against libaom. Needs av1_get_ref_mv_from_stack (encodemv.c).
[Trait("Category", "Native")]
public class MvPredTests
{
    private readonly ITestOutputHelper _output;

    public MvPredTests(ITestOutputHelper output) => _output = output;

    [DllImport("aomshim_e8")]
    private static extern void aomshim_e8_mv_pred(byte[] src, int srcStride, ref byte refCenter, int refStride, int bsize,
                                                  int miRow, int miCol, int miRows, int miCols, int refFrame, int[] mvs,
                                                  int refMvCount, int gmRow, int gmCol, int[] output);

    private const int Border = 200;

    // Plain SAD for the test's block sizes (the port's aom_sad kernels belong to another package).
    private static aom_sad_fn_t Sad(int bw) => (a, aStride, b, bStride) =>
    {
        uint sad = 0;
        for (int r = 0; r < bw; ++r)
            for (int c = 0; c < bw; ++c)
                sad += (uint)Math.Abs(a[r * aStride + c] - b[r * bStride + c]);
        return sad;
    };

    [Fact]
    public void MvPredMatchesLibaom() => Deferred.Run(_output, () =>
    {
        var rng = new Random(0x5AD);
        const int frameW = 320, frameH = 192;
        int refStride = frameW + 2 * Border;
        var refFrame = new byte[refStride * (frameH + 2 * Border)];
        for (int i = 0; i < refFrame.Length; ++i) refFrame[i] = (byte)rng.Next(256);

        foreach (int bsize in new[] { BLOCK_16X16, BLOCK_64X64 })
        {
            int bw = bsize == BLOCK_64X64 ? 64 : 16;
            int srcStride = bw + 8;
            var src = new byte[srcStride * bw];
            for (int iter = 0; iter < 400; ++iter)
            {
                for (int i = 0; i < src.Length; ++i) src[i] = (byte)rng.Next(256);
                int miRow = rng.Next(frameH / 4);
                int miCol = rng.Next(frameW / 4);
                int refFrameIdx = 1 + rng.Next(7);
                int Mv() => rng.Next(4) switch
                {
                    0 => 0,
                    1 => rng.Next(-24, 25),
                    2 => rng.Next(-4000, 4001),
                    _ => rng.Next(-32000, 32001),
                };
                int[] mvs = { Mv(), Mv(), Mv(), Mv() };
                if (iter % 5 == 0) { mvs[2] = mvs[0]; mvs[3] = mvs[1]; }
                int count = rng.Next(3);
                int gmRow = Mv(), gmCol = Mv();

                var expected = new int[4];
                int center = (Border + miRow * 4) * refStride + Border + miCol * 4;
                aomshim_e8_mv_pred(src, srcStride, ref refFrame[center], refStride, bsize, miRow, miCol,
                                   frameH / 4, frameW / 4, refFrameIdx, mvs, count, gmRow, gmCol, expected);

                var cpi = new AV1_COMP();
                cpi.ppi = new AV1_PRIMARY();
                cpi.ppi.fn_ptr[bsize].sdf = Sad(bw);
                cpi.common.mi_params.mi_rows = frameH / 4;
                cpi.common.mi_params.mi_cols = frameW / 4;
                MACROBLOCK x = cpi.td.mb;
                x.plane[0].src.buf = src;
                x.plane[0].src.stride = srcStride;
                x.e_mbd.mi_row = miRow;
                x.e_mbd.mi_col = miCol;
                x.e_mbd.width = (byte)(bw / 4);
                x.e_mbd.height = (byte)(bw / 4);
                x.mbmi_ext.ref_mv_stack[refFrameIdx][0].this_mv.as_mv.row = (short)mvs[0];
                x.mbmi_ext.ref_mv_stack[refFrameIdx][0].this_mv.as_mv.col = (short)mvs[1];
                x.mbmi_ext.ref_mv_stack[refFrameIdx][1].this_mv.as_mv.row = (short)mvs[2];
                x.mbmi_ext.ref_mv_stack[refFrameIdx][1].this_mv.as_mv.col = (short)mvs[3];
                x.mbmi_ext.ref_mv_count[refFrameIdx] = (byte)count;
                x.mbmi_ext.global_mvs[refFrameIdx].as_mv.row = (short)gmRow;
                x.mbmi_ext.global_mvs[refFrameIdx].as_mv.col = (short)gmCol;
                x.pred_mv_sad[refFrameIdx] = -1;
                x.pred_mv0_sad[refFrameIdx] = -1;
                x.pred_mv1_sad[refFrameIdx] = -1;
                rd.av1_mv_pred(cpi, x, new Ptr<byte>(refFrame, center), refStride, refFrameIdx, bsize);
                int[] actual = { x.pred_mv_sad[refFrameIdx], x.pred_mv0_sad[refFrameIdx], x.pred_mv1_sad[refFrameIdx],
                                 (int)x.max_mv_context[refFrameIdx] };
                Assert.True(expected.AsSpan().SequenceEqual(actual),
                            $"bsize {bsize} mi ({miRow},{miCol}) mvs [{string.Join(",", mvs)}] count {count} gm ({gmRow},{gmCol}): " +
                            $"C [{string.Join(",", expected)}] C# [{string.Join(",", actual)}]");
            }
        }
    });
}
