using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.entropymv;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.filter;
using static Av1rt.Aom.mcomp;
using static Av1rt.Aom.mcomp_structs;
using static Av1rt.Aom.mv;
using static Av1rt.Aom.scale;

namespace Av1rt.Tests.Motion;

/// <summary>A reference plane and a source plane (same layout, with borders) where the source is the
/// reference displaced by a known shift.</summary>
internal sealed class SearchPlanes
{
    public const int W = 192, H = 160, B = 72;
    public const int Stride = W + 2 * B;
    public readonly byte[] Ref = new byte[(H + 2 * B) * Stride];
    public readonly byte[] Src = new byte[(H + 2 * B) * Stride];

    public SearchPlanes(Random rng, bool smooth)
    {
        if (smooth)
        {
            // Smooth texture, source displaced by a fractional shift: sub-pel search has work to do.
            double sx = rng.NextDouble() * 24 - 12, sy = rng.NextDouble() * 24 - 12;
            double a = 0.05 + rng.NextDouble() * 0.25, b = 0.02 + rng.NextDouble() * 0.2;
            double c = 0.04 + rng.NextDouble() * 0.25, d = 0.01 + rng.NextDouble() * 0.2;
            double G(double x, double y) => 128 + 60 * Math.Sin(a * x + b * y) + 40 * Math.Cos(c * y - d * x);
            for (int y = 0; y < H + 2 * B; y++)
            {
                for (int x = 0; x < Stride; x++)
                {
                    Ref[y * Stride + x] = Clip(G(x, y) + rng.Next(-3, 4));
                    Src[y * Stride + x] = Clip(G(x + sx, y + sy) + rng.Next(-3, 4));
                }
            }
        }
        else
        {
            // Noise, source displaced by an integer shift: a sharp minimum on a rough SAD surface.
            int dx = rng.Next(-14, 15), dy = rng.Next(-14, 15);
            rng.NextBytes(Ref);
            for (int y = 0; y < H + 2 * B; y++)
            {
                for (int x = 0; x < Stride; x++)
                {
                    int ry = y + dy, rx = x + dx;
                    Src[y * Stride + x] = ry >= 0 && ry < H + 2 * B && rx >= 0 && rx < Stride
                        ? Clip(Ref[ry * Stride + rx] + rng.Next(-6, 7))
                        : (byte)rng.Next(256);
                }
            }
        }
    }

    private static byte Clip(double v) => (byte)Math.Clamp((int)Math.Round(v), 0, 255);

    public static int BlockOffset(int by, int bx) => (B + by) * Stride + (B + bx);
}

public class MotionSearchTests
{
    private static readonly int[] Bsizes =
    {
        BLOCK_8X8, BLOCK_8X16, BLOCK_16X8, BLOCK_16X16, BLOCK_16X32, BLOCK_32X16, BLOCK_32X32, BLOCK_64X64,
    };

    private static bool HasSkipSad(int bsize) => bsize != BLOCK_8X8 && bsize != BLOCK_16X8;

    private static readonly int[] Methods = { FAST_DIAMOND, FAST_DIAMOND, FAST_BIGDIA, VFAST_DIAMOND, HEX, BIGDIA };

    // ---- vfp: libaom's C kernels through the shim, so only the mcomp.c logic is compared ----

    private static void MultiSad(int bsize, int n, bool skip, Ptr<byte> a, int aStride, Ptr<Ptr<byte>> b,
                                 int bStride, Ptr<uint> sads)
    {
        byte[] plane = b[0].Array;
        var offsets = new int[4];
        for (int i = 0; i < 4; i++)
        {
            if (i >= n && b[i].IsNull) continue;
            Assert.Same(plane, b[i].Array);
            offsets[i] = b[i].Offset;
        }
        var result = new uint[4];
        if (skip) E6Shim.e6_sdsx4d(bsize, a.Array, a.Offset, aStride, plane, offsets, bStride, result);
        else E6Shim.e6_sdxnd(bsize, n, a.Array, a.Offset, aStride, plane, offsets, bStride, result);
        for (int i = 0; i < (skip ? 4 : n); i++) sads[i] = result[i];
    }

    private static aom_variance_fn_ptr_t MakeVfp(int bsize)
    {
        var v = new aom_variance_fn_ptr_t();
        v.sdf = (Ptr<byte> a, int aStride, Ptr<byte> b, int bStride) =>
            E6Shim.e6_sdf(bsize, a.Array, a.Offset, aStride, b.Array, b.Offset, bStride);
        v.sdx4df = (Ptr<byte> a, int aStride, Ptr<Ptr<byte>> b, int bStride, Ptr<uint> sads) =>
            MultiSad(bsize, 4, false, a, aStride, b, bStride, sads);
        v.sdx3df = (Ptr<byte> a, int aStride, Ptr<Ptr<byte>> b, int bStride, Ptr<uint> sads) =>
            MultiSad(bsize, 3, false, a, aStride, b, bStride, sads);
        if (HasSkipSad(bsize))
        {
            v.sdsf = (Ptr<byte> a, int aStride, Ptr<byte> b, int bStride) =>
                E6Shim.e6_sdsf(bsize, a.Array, a.Offset, aStride, b.Array, b.Offset, bStride);
            v.sdsx4df = (Ptr<byte> a, int aStride, Ptr<Ptr<byte>> b, int bStride, Ptr<uint> sads) =>
                MultiSad(bsize, 4, true, a, aStride, b, bStride, sads);
        }
        v.vf = (Ptr<byte> a, int aStride, Ptr<byte> b, int bStride, ref uint sse) =>
            E6Shim.e6_vf(bsize, a.Array, a.Offset, aStride, b.Array, b.Offset, bStride, out sse);
        v.svf = (Ptr<byte> a, int aStride, int xoff, int yoff, Ptr<byte> b, int bStride, ref uint sse) =>
            E6Shim.e6_svf(bsize, a.Array, a.Offset, aStride, xoff, yoff, b.Array, b.Offset, bStride, out sse);
        return v;
    }

    private static MV_COST_PARAMS CostParams(MV refMv, CostTables t, int errorPerBit, int sadPerBit)
    {
        var p = new MV_COST_PARAMS();
        p.ref_mv = refMv;
        p.full_ref_mv = get_fullmv_from_mv(refMv);
        p.mv_cost_type = MV_COST_ENTROPY;
        p.error_per_bit = errorPerBit;
        p.sad_per_bit = sadPerBit;
        p.mvjcost = t.Joint;
        p.mvcost[0] = new Ptr<int>(t.Comp0, MV_MAX);
        p.mvcost[1] = new Ptr<int>(t.Comp1, MV_MAX);
        return p;
    }

    private static buf_2d Buf(byte[] plane, int offset) =>
        new() { buf = new Ptr<byte>(plane, offset), stride = SearchPlanes.Stride };

    private sealed class Case
    {
        public int Bsize, Method, StepParam, ErrorPerBit, SadPerBit, UseCostList, PruneMesh, UseSkipSad;
        public int By, Bx;
        public FULLPEL_MV StartMv;
        public MV RefMv;
        public FullMvLimits Limits;

        public int Offset => SearchPlanes.BlockOffset(By, Bx);

        public int[] FullPelInts() => new[]
        {
            Bsize, Method, StepParam, StartMv.row, StartMv.col, RefMv.row, RefMv.col,
            Limits.col_min, Limits.col_max, Limits.row_min, Limits.row_max, ErrorPerBit, SadPerBit,
            UseCostList, PruneMesh, UseSkipSad,
        };
    }

    private static Case RandomCase(Random rng)
    {
        var c = new Case();
        c.Bsize = Bsizes[rng.Next(Bsizes.Length)];
        c.Method = Methods[rng.Next(Methods.Length)];
        c.StepParam = rng.Next(0, MAX_MVSEARCH_STEPS);
        int bw = block_size_wide[c.Bsize], bh = block_size_high[c.Bsize];
        c.By = rng.Next(0, SearchPlanes.H - bh + 1);
        c.Bx = rng.Next(0, SearchPlanes.W - bw + 1);

        // x->mv_limits: keep every read (plus the sub-pel filter taps) inside the bordered planes.
        const int margin = 8;
        int colMin = -(c.Bx + SearchPlanes.B - margin), colMax = SearchPlanes.W + SearchPlanes.B - margin - c.Bx - bw;
        int rowMin = -(c.By + SearchPlanes.B - margin), rowMax = SearchPlanes.H + SearchPlanes.B - margin - c.By - bh;
        if (rng.Next(3) == 0)
        {
            // A tight window: exercises the check_bounds / av1_is_fullmv_in_range fallbacks.
            int cc = rng.Next(Math.Max(colMin, -30), Math.Min(colMax, 30) + 1), hw = rng.Next(0, 12);
            int rc = rng.Next(Math.Max(rowMin, -30), Math.Min(rowMax, 30) + 1), hh = rng.Next(0, 12);
            colMin = Math.Max(colMin, cc - hw);
            colMax = Math.Min(colMax, cc + hw);
            rowMin = Math.Max(rowMin, rc - hh);
            rowMax = Math.Min(rowMax, rc + hh);
        }
        c.Limits = new FullMvLimits { col_min = colMin, col_max = colMax, row_min = rowMin, row_max = rowMax };

        c.StartMv = new FULLPEL_MV((short)rng.Next(-24, 25), (short)rng.Next(-24, 25));
        c.RefMv = new MV((short)rng.Next(-24 * 8, 24 * 8 + 1), (short)rng.Next(-24 * 8, 24 * 8 + 1));
        if (rng.Next(3) == 0) c.RefMv = get_mv_from_fullmv(c.StartMv);
        c.ErrorPerBit = rng.Next(1, 400);
        c.SadPerBit = rng.Next(1, 40);
        c.UseCostList = rng.Next(2);
        c.PruneMesh = rng.Next(2);
        c.UseSkipSad = HasSkipSad(c.Bsize) && rng.Next(3) == 0 ? 1 : 0;
        return c;
    }

    private static int[] RunFullPel(Case c, SearchPlanes p, CostTables t, search_site_config[] sites)
    {
        var ms = new FULLPEL_MOTION_SEARCH_PARAMS();
        ms.bsize = c.Bsize;
        ms.vfp = MakeVfp(c.Bsize);
        ms.ms_buffers.@ref = Buf(p.Ref, c.Offset);
        ms.ms_buffers.src = Buf(p.Src, c.Offset);
        av1_set_ms_compound_refs(ref ms.ms_buffers, Ptr<byte>.Null, Ptr<byte>.Null, 0, 0);
        av1_set_mv_search_method(ref ms, sites, c.Method);
        ms.mesh_search_mv_diff_threshold = 4;
        ms.prune_mesh_search = c.PruneMesh;
        ms.mv_limits = c.Limits;
        av1_set_mv_search_range(ref ms.mv_limits, c.RefMv);
        ms.mv_cost_params = CostParams(c.RefMv, t, c.ErrorPerBit, c.SadPerBit);
        ms.sdf = ms.vfp.sdf;
        ms.sdx4df = ms.vfp.sdx4df;
        ms.sdx3df = ms.vfp.sdx3df;
        if (c.UseSkipSad != 0)
        {
            ms.sdf = ms.vfp.sdsf;
            ms.sdx4df = ms.vfp.sdsx4df;
            ms.sdx3df = ms.vfp.sdsx4df;
        }

        var costList = new int[5];
        FULLPEL_MV best = default;
        var stats = new FULLPEL_MV_STATS();
        int var = av1_full_pixel_search(c.StartMv, ms, c.StepParam,
                                        c.UseCostList != 0 ? costList : Ptr<int>.Null, ref best, ref stats,
                                        Ptr<FULLPEL_MV>.Null);
        return new[]
        {
            var, best.row, best.col, (int)stats.distortion, (int)stats.sse, stats.err_cost,
            costList[0], costList[1], costList[2], costList[3], costList[4],
            ms.mv_limits.col_min, ms.mv_limits.col_max, ms.mv_limits.row_min, ms.mv_limits.row_max,
        };
    }

    private static int[] RunSubPel(Case c, SearchPlanes p, CostTables t, int[] sub)
    {
        // sub: the shim's e6_subpel_pruned_more input (see shim_e6.c).
        var sms = new SUBPEL_MOTION_SEARCH_PARAMS();
        sms.allow_hp = sub[11];
        sms.forced_stop = sub[12];
        sms.iters_per_step = sub[13];
        sms.cost_list = sub[14] != 0 ? new[] { sub[19], sub[20], sub[21], sub[22], sub[23] } : Ptr<int>.Null;
        av1_set_subpel_mv_search_range(ref sms.mv_limits, c.Limits, c.RefMv);
        sms.mv_cost_params = CostParams(c.RefMv, t, c.ErrorPerBit, c.SadPerBit);
        sms.var_params.vfp = MakeVfp(c.Bsize);
        sms.var_params.subpel_search_type = USE_2_TAPS;
        sms.var_params.w = block_size_wide[c.Bsize];
        sms.var_params.h = block_size_high[c.Bsize];
        sms.var_params.ms_buffers.@ref = Buf(p.Ref, c.Offset);
        sms.var_params.ms_buffers.src = Buf(p.Src, c.Offset);
        av1_set_ms_compound_refs(ref sms.var_params.ms_buffers, Ptr<byte>.Null, Ptr<byte>.Null, 0, 0);

        FULLPEL_MV_STATS? stats = sub[15] != 0
            ? new FULLPEL_MV_STATS { distortion = (uint)sub[16], sse = (uint)sub[17], err_cost = sub[18] }
            : null;

        var xd = new MACROBLOCKD();
        xd.mi = new[] { new MB_MODE_INFO() };
        xd.block_ref_scale_factors[0] = new scale_factors
        {
            x_scale_fp = REF_NO_SCALE, y_scale_fp = REF_NO_SCALE, x_step_q4 = 16, y_step_q4 = 16,
        };
        var cm = new AV1_COMMON();

        MV best = default;
        int distortion = 0;
        uint sse1 = 0;
        int besterr = av1_find_best_sub_pixel_tree_pruned_more(
            xd, cm, sms, new MV((short)sub[1], (short)sub[2]), stats, ref best, ref distortion, ref sse1,
            Ptr<int_mv>.Null);
        return new[] { besterr, best.row, best.col, distortion, (int)sse1 };
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void FullAndSubPelSearchMatchLibaom(int seed, bool smooth)
    {
        var rng = new Random(seed);
        var planes = new SearchPlanes(rng, smooth);
        CostTables tables = CostTables.FromLibaom(NmvFlat.Default(), MV_SUBPEL_HIGH_PRECISION);

        var sites = new search_site_config[NUM_DISTINCT_SEARCH_METHODS];
        for (int i = 0; i < sites.Length; i++)
        {
            sites[i] = new search_site_config();
            av1_init_motion_compensation[i](sites[i], SearchPlanes.Stride, 0);
        }

        for (int n = 0; n < 250; n++)
        {
            Case c = RandomCase(rng);

            var expected = new int[15];
            E6Shim.e6_full_pixel_search(c.FullPelInts(), planes.Src, c.Offset, SearchPlanes.Stride, planes.Ref,
                                        c.Offset, SearchPlanes.Stride, tables.Joint, tables.Comp0, tables.Comp1,
                                        expected);
            int[] actual = RunFullPel(c, planes, tables, sites);
            Assert.True(expected.AsSpan().SequenceEqual(actual),
                        $"full-pel case {n}: expected [{string.Join(",", expected)}] got [{string.Join(",", actual)}]");

            // Sub-pel refinement from the full-pel result, as the nonrd NEWMV search does.
            MV start = get_mv_from_fullmv(new FULLPEL_MV((short)expected[1], (short)expected[2]));
            for (int k = 0; k < 3; k++)
            {
                int hasCostList = c.UseCostList != 0 && rng.Next(4) != 0 ? 1 : 0;
                var sub = new[]
                {
                    c.Bsize, start.row, start.col, c.RefMv.row, c.RefMv.col,
                    c.Limits.col_min, c.Limits.col_max, c.Limits.row_min, c.Limits.row_max,
                    c.ErrorPerBit, c.SadPerBit,
                    rng.Next(2), rng.Next(EIGHTH_PEL, FULL_PEL + 1), rng.Next(1, 3), hasCostList, rng.Next(2),
                    expected[3], expected[4], expected[5],
                    expected[6], expected[7], expected[8], expected[9], expected[10],
                };
                var subExpected = new int[5];
                E6Shim.e6_subpel_pruned_more(sub, planes.Src, c.Offset, SearchPlanes.Stride, planes.Ref, c.Offset,
                                             SearchPlanes.Stride, tables.Joint, tables.Comp0, tables.Comp1,
                                             subExpected);
                int[] subActual = RunSubPel(c, planes, tables, sub);
                Assert.True(subExpected.AsSpan().SequenceEqual(subActual),
                            $"sub-pel case {n}.{k} [{string.Join(",", sub)}]: expected " +
                            $"[{string.Join(",", subExpected)}] got [{string.Join(",", subActual)}]");
            }
        }
    }
}
