using System;
using System.Runtime.InteropServices;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.Pickmode;

/// <summary>P/Invoke surface of tools/oracle/shim/shim_f2.c (libaom nonrd_opt.c).</summary>
internal static class F2Shim
{
    private const string Dll = "aomshim_f2";

    // io[] layouts (mirror the enums in shim_f2.c).
    public const int YRD_IN_SSE = 0, YRD_OUT_RATE = 1, YRD_OUT_DIST = 2, YRD_OUT_SSE = 3,
                     YRD_OUT_SKIP_TXFM = 4, YRD_OUT_SKIPPABLE = 5, YRD_IO_COUNT = 6;
    public const int UV_OUT_TOT_SSE = 0, UV_OUT_RATE = 1, UV_OUT_DIST = 2, UV_OUT_SKIP_TXFM = 3,
                     UV_IO_COUNT = 4;

    [DllImport(Dll)]
    public static extern void aomshim_f2_block_yrd(int bsize, int txSize, byte[] src, int srcStride,
                                                   byte[] dst, int dstStride, int mbToRightEdge,
                                                   int mbToBottomEdge, short[] roundFp, short[] quantFp,
                                                   short[] dequant, [In, Out] long[] io,
                                                   [Out] byte[] blkSkip);

    [DllImport(Dll)]
    public static extern void aomshim_f2_model_rd_for_sb_uv(int planeBsize, int startPlane, int stopPlane,
                                                            byte[] srcU, byte[] srcV, int srcStride,
                                                            byte[] dstU, byte[] dstV, int dstStride,
                                                            short[] dequantUv, int colorSensU,
                                                            int colorSensV, int rdmult, [Out] long[] io);
}

/// <summary>Differential tests of the public nonrd_opt.c building blocks against libaom.
/// Not covered here: the file-static helpers of nonrd_pickmode.c (calculate_tx_size,
/// model_skip_for_sb_y_large, set_early_term_based_on_uv_plane, ...) and the entry points
/// av1_nonrd_pick_inter_mode_sb / av1_nonrd_pick_intra_mode / av1_estimate_intra_mode, which
/// need a whole encoder state; they are covered by the end-to-end bit-exact tests.</summary>
[Trait("Category", "Native")]
public class NonrdOptTests
{
    private const int Stride = 64 + 24;

    // A source block and a prediction that differs from it by a residual of a random scale, so
    // both skippable and non-skippable blocks occur.
    private static (byte[] src, byte[] pred) MakeBlock(Random rng, int w, int h)
    {
        var src = new byte[Stride * (h + 2)];
        var pred = new byte[Stride * (h + 2)];
        int[] scales = { 0, 1, 2, 4, 12, 40, 255 };
        int scale = scales[rng.Next(scales.Length)];
        bool smooth = rng.Next(2) == 0;
        int b0 = rng.Next(256), gx = rng.Next(-3, 4), gy = rng.Next(-3, 4);
        for (int i = 0; i < src.Length; ++i)
        {
            int r = i / Stride, c = i % Stride;
            int p = smooth ? Math.Clamp(b0 + gx * c + gy * r, 0, 255) : rng.Next(256);
            pred[i] = (byte)p;
            src[i] = (byte)Math.Clamp(p + rng.Next(-scale, scale + 1), 0, 255);
        }
        return (src, pred);
    }

    // One row of av1_build_quantizer()'s tables: index 0 = DC, 1..7 = AC.
    private static void MakeQuant(Random rng, out short[] roundFp, out short[] quantFp, out short[] dequant)
    {
        roundFp = new short[8];
        quantFp = new short[8];
        dequant = new short[8];
        int dc = rng.Next(4, 1337), ac = rng.Next(4, 1829);
        for (int i = 0; i < 8; ++i)
        {
            int q = i == 0 ? dc : ac;
            dequant[i] = (short)q;
            quantFp[i] = (short)((1 << 16) / q);
            roundFp[i] = (short)((64 * q) >> 7);
        }
    }

    private static readonly int[] YrdBsizes =
    {
        BLOCK_8X8, BLOCK_8X16, BLOCK_16X8, BLOCK_16X16, BLOCK_16X32, BLOCK_32X16, BLOCK_32X32,
        BLOCK_32X64, BLOCK_64X32, BLOCK_64X64, BLOCK_8X32, BLOCK_32X8, BLOCK_16X64, BLOCK_64X16,
    };

    [Fact]
    public void BlockYrdMatchesLibaom()
    {
        var rng = new Random(0xF2B1);
        int skippableCases = 0, codedCases = 0;
        for (int iter = 0; iter < 3000; ++iter)
        {
            int bsize = YrdBsizes[rng.Next(YrdBsizes.Length)];
            int bw = common_data.block_size_wide[bsize], bh = common_data.block_size_high[bsize];
            // TX_16X16 needs both dimensions >= 16; TX_8X8 fits every size here.
            int txSize = bw >= 16 && bh >= 16 && rng.Next(2) == 0 ? TX_16X16 : TX_8X8;
            // Blocks crossing the right / bottom frame edge, by whole 8x8 units (frame sizes are
            // multiples of 8), keeping at least one transform block inside.
            int outW = rng.Next(3) == 0 ? rng.Next(0, bw / 8) * 8 : 0;
            int outH = rng.Next(3) == 0 ? rng.Next(0, bh / 8) * 8 : 0;
            int mbToRight = -outW * 8, mbToBottom = -outH * 8;
            var (src, pred) = MakeBlock(rng, bw, bh);
            MakeQuant(rng, out short[] roundFp, out short[] quantFp, out short[] dequant);
            long sseIn = rng.Next(4) == 0 ? long.MaxValue : rng.Next(0, 1 << 24);

            var io = new long[F2Shim.YRD_IO_COUNT];
            io[F2Shim.YRD_IN_SSE] = sseIn;
            var expectedBlkSkip = new byte[MAX_MIB_SIZE * MAX_MIB_SIZE];
            var cPred = (byte[])pred.Clone();
            F2Shim.aomshim_f2_block_yrd(bsize, txSize, src, Stride, cPred, Stride, mbToRight, mbToBottom,
                                        roundFp, quantFp, dequant, io, expectedBlkSkip);

            var x = new MACROBLOCK();
            x.plane[0].src.buf = src;
            x.plane[0].src.stride = Stride;
            x.plane[0].src_diff = new short[MAX_SB_SQUARE];
            x.plane[0].coeff = new int[MAX_SB_SQUARE];
            x.plane[0].round_fp_QTX = roundFp;
            x.plane[0].quant_fp_QTX = quantFp;
            x.plane[0].dequant_QTX = dequant;
            x.e_mbd.plane[0].dst.buf = (byte[])pred.Clone();
            x.e_mbd.plane[0].dst.stride = Stride;
            x.e_mbd.mb_to_right_edge = mbToRight;
            x.e_mbd.mb_to_bottom_edge = mbToBottom;
            var rdc = new RD_STATS();
            rd.av1_invalid_rd_stats(ref rdc);
            rdc.sse = sseIn;
            int skippable = -1;
            nonrd_opt.av1_block_yrd(x, ref rdc, ref skippable, bsize, txSize);

            string ctx = $"iter {iter} bsize {bsize} tx {txSize} edge ({mbToRight},{mbToBottom}) sse {sseIn}";
            Assert.True(io[F2Shim.YRD_OUT_RATE] == rdc.rate, $"rate: {ctx}");
            Assert.True(io[F2Shim.YRD_OUT_DIST] == rdc.dist, $"dist: {ctx}");
            Assert.True(io[F2Shim.YRD_OUT_SSE] == rdc.sse, $"sse: {ctx}");
            Assert.True(io[F2Shim.YRD_OUT_SKIP_TXFM] == rdc.skip_txfm, $"skip_txfm: {ctx}");
            Assert.True(io[F2Shim.YRD_OUT_SKIPPABLE] == skippable, $"skippable: {ctx}");
            Assert.True(expectedBlkSkip.AsSpan().SequenceEqual(x.txfm_search_info.blk_skip), $"blk_skip: {ctx}");
            if (skippable != 0) skippableCases++; else codedCases++;
        }
        // Both outcomes must be exercised for the comparison to mean anything.
        Assert.True(skippableCases > 300 && codedCases > 300, $"skippable {skippableCases} coded {codedCases}");
    }

    private static readonly (int bsize, aom_variance_fn_t vf)[] UvBsizes =
    {
        (BLOCK_4X4, variance_h.aom_variance4x4), (BLOCK_4X8, variance_h.aom_variance4x8),
        (BLOCK_8X4, variance_h.aom_variance8x4), (BLOCK_8X8, variance_h.aom_variance8x8),
        (BLOCK_8X16, variance_h.aom_variance8x16), (BLOCK_16X8, variance_h.aom_variance16x8),
        (BLOCK_16X16, variance_h.aom_variance16x16), (BLOCK_16X32, variance_h.aom_variance16x32),
        (BLOCK_32X16, variance_h.aom_variance32x16), (BLOCK_32X32, variance_h.aom_variance32x32),
    };

    [Fact]
    public void ModelRdForSbUvMatchesLibaom()
    {
        var rng = new Random(0xF2C2);
        int skipCases = 0, codedCases = 0;
        for (int iter = 0; iter < 3000; ++iter)
        {
            var (bsize, vf) = UvBsizes[rng.Next(UvBsizes.Length)];
            int bw = common_data.block_size_wide[bsize], bh = common_data.block_size_high[bsize];
            var (srcU, dstU) = MakeBlock(rng, bw, bh);
            var (srcV, dstV) = MakeBlock(rng, bw, bh);
            short[] dq = { (short)rng.Next(4, 1337), (short)rng.Next(4, 1829),
                           (short)rng.Next(4, 1337), (short)rng.Next(4, 1829) };
            int csU = rng.Next(2), csV = rng.Next(2);
            int rdmult = rng.Next(4) == 0 ? rng.Next(1, 64) : rng.Next(1, 400000);
            // Callers use (U, V) together or one plane at a time (av1_estimate_block_intra).
            int start = rng.Next(3) switch { 0 => 1, 1 => 1, _ => 2 };
            int stop = start == 1 && rng.Next(3) != 0 ? 2 : start;

            var io = new long[F2Shim.UV_IO_COUNT];
            F2Shim.aomshim_f2_model_rd_for_sb_uv(bsize, start, stop, srcU, srcV, Stride, dstU, dstV, Stride,
                                                 dq, csU, csV, rdmult, io);

            var cpi = new AV1_COMP();
            cpi.ppi = new AV1_PRIMARY();
            cpi.ppi.fn_ptr[bsize].vf = vf;
            var x = new MACROBLOCK();
            byte[][] srcs = { srcU, srcV }, dsts = { dstU, dstV };
            for (int j = 0; j < 2; ++j)
            {
                var row = new short[8];
                for (int i = 0; i < 8; ++i) row[i] = i == 0 ? dq[2 * j] : dq[2 * j + 1];
                x.plane[1 + j].src.buf = srcs[j];
                x.plane[1 + j].src.stride = Stride;
                x.plane[1 + j].dequant_QTX = row;
                x.e_mbd.plane[1 + j].dst.buf = dsts[j];
                x.e_mbd.plane[1 + j].dst.stride = Stride;
            }
            x.color_sensitivity[0] = (byte)csU;
            x.color_sensitivity[1] = (byte)csV;
            x.rdmult = rdmult;
            var rdc = new RD_STATS();
            rd.av1_invalid_rd_stats(ref rdc);
            long totSse = nonrd_opt.av1_model_rd_for_sb_uv(cpi, bsize, x, x.e_mbd, ref rdc, start, stop);

            string ctx = $"iter {iter} bsize {bsize} planes {start}-{stop} cs ({csU},{csV}) rdmult {rdmult}";
            Assert.True(io[F2Shim.UV_OUT_TOT_SSE] == totSse, $"tot_sse: {ctx}");
            Assert.True(io[F2Shim.UV_OUT_RATE] == rdc.rate, $"rate: {ctx}");
            Assert.True(io[F2Shim.UV_OUT_DIST] == rdc.dist, $"dist: {ctx}");
            Assert.True(io[F2Shim.UV_OUT_SKIP_TXFM] == rdc.skip_txfm, $"skip_txfm: {ctx}");
            if (rdc.skip_txfm != 0) skipCases++; else if (rdc.rate > 0) codedCases++;
        }
        // Both outcomes must be exercised for the comparison to mean anything.
        Assert.True(skipCases > 300 && codedCases > 300, $"skip {skipCases} coded {codedCases}");
    }
}
