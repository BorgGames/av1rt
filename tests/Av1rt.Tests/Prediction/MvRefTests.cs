using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.mv;
using static Av1rt.Aom.mvref_common;

namespace Av1rt.Tests.Prediction;

// av1_find_mv_refs on random mode-info grids (single reference frames and INTRA_FRAME; the
// compound branches are [subset] in the port).
[Trait("Category", "Native")]
public class MvRefTests
{
    private static readonly int[] SbSizes = { BLOCK_64X64, BLOCK_128X128 };

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void FindMvRefsMatchesLibaom(int seed, bool useMvRefList)
    {
        var rng = new Random(500 + seed);
        const int S = MAX_REF_MV_STACK_SIZE, L = MAX_MV_REF_CANDIDATES, R = MODE_CTX_REF_FRAMES;
        const int Trials = 1500;
        int withCandidates = 0, withFullList = 0;
        for (int trial = 0; trial < Trials; trial++)
        {
            int miRows = rng.Next(2, 49), miCols = rng.Next(2, 49);
            const int Pad = 34;
            int gridRows = miRows + Pad, stride = miCols + Pad;
            int sbSize = SbSizes[rng.Next(2)];
            int sbMi = mi_size_wide[sbSize];

            // Current block: fits in the superblock, aligned to its size.
            int bsize;
            do bsize = rng.Next(BLOCK_SIZES_ALL);
            while (mi_size_wide[bsize] > sbMi || mi_size_high[bsize] > sbMi);
            int bw = mi_size_wide[bsize], bh = mi_size_high[bsize];
            int miRow = rng.Next((miRows + bh - 1) / bh) * bh;
            int miCol = rng.Next((miCols + bw - 1) / bw) * bw;

            var cells = new int[gridRows * stride * 8];
            for (int i = 0; i < gridRows * stride; i++) ContextTests.RandomCell(rng, cells, i);
            // Make neighbours often share the same reference frames / motion vectors.
            if (rng.Next(2) == 0)
            {
                int commonRef = rng.Next(LAST_FRAME, ALTREF_FRAME + 1);
                int commonMv = (ushort)rng.Next(-64, 65) | (rng.Next(-64, 65) << 16);
                for (int i = 0; i < gridRows * stride; i++)
                {
                    if (rng.Next(3) == 0) cells[i * 8 + 2] = commonRef;
                    if (rng.Next(3) == 0) cells[i * 8 + 4] = commonMv;
                }
            }
            int cur = (miRow * stride + miCol) * 8;
            cells[cur + 0] = bsize;
            int partition = rng.Next(EXT_PARTITION_TYPES);
            int lastVert = rng.Next(2), firstHorz = rng.Next(2);
            int allowHp = rng.Next(2), forceInt = 0;
            var signBias = new int[REF_FRAMES];
            for (int i = 0; i < REF_FRAMES; i++) signBias[i] = rng.Next(4) == 0 ? 1 : 0;
            int refFrame = rng.Next(INTRA_FRAME, ALTREF_FRAME + 1);

            // Outputs start from the same random contents on both sides.
            var countC = new byte[R];
            var stackC = new int[R * S * 2];
            var weightC = new ushort[R * S];
            var listC = new int[R * L];
            var gmC = new int[REF_FRAMES];
            var ctxC = new short[R];
            rng.NextBytes(countC);
            for (int i = 0; i < stackC.Length; i++) stackC[i] = rng.Next();
            for (int i = 0; i < weightC.Length; i++) weightC[i] = (ushort)rng.Next(65536);
            for (int i = 0; i < listC.Length; i++) listC[i] = rng.Next();
            for (int i = 0; i < gmC.Length; i++) gmC[i] = rng.Next();
            for (int i = 0; i < ctxC.Length; i++) ctxC[i] = (short)rng.Next(-32768, 32768);

            var countS = (byte[])countC.Clone();
            var stackS = new CANDIDATE_MV[R][];
            var weightS = new ushort[R][];
            var listS = useMvRefList ? new int_mv[R][] : null;
            var gmS = new int_mv[REF_FRAMES];
            var ctxS = (short[])ctxC.Clone();
            for (int r = 0; r < R; r++)
            {
                stackS[r] = new CANDIDATE_MV[S];
                weightS[r] = new ushort[S];
                for (int k = 0; k < S; k++)
                {
                    stackS[r][k].this_mv.as_int = unchecked((uint)stackC[(r * S + k) * 2]);
                    stackS[r][k].comp_mv.as_int = unchecked((uint)stackC[(r * S + k) * 2 + 1]);
                    weightS[r][k] = weightC[r * S + k];
                }
                if (listS != null)
                {
                    listS[r] = new int_mv[L];
                    for (int k = 0; k < L; k++) listS[r][k].as_int = unchecked((uint)listC[r * L + k]);
                }
            }
            for (int i = 0; i < REF_FRAMES; i++) gmS[i].as_int = unchecked((uint)gmC[i]);

            E3Shim.aomshim_e3_find_mv_refs(miRows, miCols, gridRows, stride, cells, miRow, miCol, partition,
                                           lastVert, firstHorz, sbSize, allowHp, forceInt, signBias, refFrame,
                                           useMvRefList ? 1 : 0, countC, stackC, weightC, listC, gmC, ctxC);

            // C# side: same grid, AV1_COMMON and MACROBLOCKD set-up as the shim.
            var grid = new MB_MODE_INFO[gridRows * stride];
            for (int i = 0; i < grid.Length; i++) grid[i] = ContextTests.ToMbmi(cells, i);
            var cm = new AV1_COMMON();
            cm.seq_params = new SequenceHeader { sb_size = sbSize };
            cm.mi_params.mi_rows = miRows;
            cm.mi_params.mi_cols = miCols;
            cm.features.allow_high_precision_mv = allowHp != 0;
            cm.features.cur_frame_force_integer_mv = forceInt != 0;
            cm.features.allow_ref_frame_mvs = false;
            for (int i = 0; i < REF_FRAMES; i++)
            {
                cm.global_motion[i] = default_warp_params;
                cm.ref_frame_sign_bias[i] = signBias[i];
            }
            var xd = new MACROBLOCKD();
            xd.mi = new Ptr<MB_MODE_INFO>(grid, miRow * stride + miCol);
            xd.mi_stride = stride;
            xd.mi_row = miRow;
            xd.mi_col = miCol;
            MB_MODE_INFO mbmi = xd.mi[0];
            mbmi.partition = partition;
            xd.width = (byte)bw;
            xd.height = (byte)bh;
            xd.tile.mi_row_start = 0;
            xd.tile.mi_row_end = miRows;
            xd.tile.mi_col_start = 0;
            xd.tile.mi_col_end = miCols;
            xd.up_available = miRow > 0;
            xd.left_available = miCol > 0;
            xd.mb_to_top_edge = -(miRow * MI_SIZE * 8);
            xd.mb_to_bottom_edge = (miRows - bh - miRow) * MI_SIZE * 8;
            xd.mb_to_left_edge = -(miCol * MI_SIZE * 8);
            xd.mb_to_right_edge = (miCols - bw - miCol) * MI_SIZE * 8;
            xd.is_last_vertical_rect = lastVert != 0;
            xd.is_first_horizontal_rect = firstHorz != 0;

            av1_find_mv_refs(cm, xd, mbmi, refFrame, countS, stackS, weightS, listS, gmS, ctxS);

            if (countS[refFrame] > 0) withCandidates++;
            if (countS[refFrame] >= MAX_MV_REF_CANDIDATES) withFullList++;
            string at = $"trial {trial} ref {refFrame} bsize {bsize} pos {miRow},{miCol} of {miRows}x{miCols}";
            Assert.True(countC.AsSpan().SequenceEqual(countS), "ref_mv_count " + at);
            Assert.True(ctxC.AsSpan().SequenceEqual(ctxS), "mode_context " + at);
            for (int i = 0; i < REF_FRAMES; i++) Assert.Equal((uint)gmC[i], gmS[i].as_int);
            for (int r = 0; r < R; r++)
            {
                for (int k = 0; k < S; k++)
                {
                    Assert.True((uint)stackC[(r * S + k) * 2] == stackS[r][k].this_mv.as_int, "this_mv " + at);
                    Assert.True((uint)stackC[(r * S + k) * 2 + 1] == stackS[r][k].comp_mv.as_int, "comp_mv " + at);
                    Assert.True(weightC[r * S + k] == weightS[r][k], "weight " + at);
                }
                if (listS != null)
                    for (int k = 0; k < L; k++)
                        Assert.True((uint)listC[r * L + k] == listS[r][k].as_int, "mv_ref_list " + at);
            }
        }
        // The random grids must actually produce candidate lists.
        Assert.True(withCandidates > Trials / 3, $"only {withCandidates} trials found candidates");
        Assert.True(withFullList > Trials / 10, $"only {withFullList} trials found >= 2 candidates");
    }
}
