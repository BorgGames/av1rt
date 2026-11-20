using System;
using System.Collections.Generic;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.bitwriter;
using static Av1rt.Aom.encodemv;
using static Av1rt.Aom.entropymv;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.mcomp;
using static Av1rt.Aom.mcomp_structs;

namespace Av1rt.Tests.Motion;

/// <summary>nmv_context &lt;-&gt; the flat aom_cdf_prob array the shim uses (C struct order).</summary>
internal static class NmvFlat
{
    private static IEnumerable<ushort[]> Arrays(nmv_context ctx)
    {
        yield return ctx.joints_cdf;
        foreach (nmv_component c in ctx.comps)
        {
            yield return c.classes_cdf;
            foreach (ushort[] a in c.class0_fp_cdf) yield return a;
            yield return c.fp_cdf;
            yield return c.sign_cdf;
            yield return c.class0_hp_cdf;
            yield return c.hp_cdf;
            yield return c.class0_cdf;
            foreach (ushort[] a in c.bits_cdf) yield return a;
        }
    }

    public static int Size
    {
        get
        {
            int n = 0;
            foreach (ushort[] a in Arrays(new nmv_context())) n += a.Length;
            return n;
        }
    }

    public static nmv_context ToContext(ushort[] flat)
    {
        var ctx = new nmv_context();
        int k = 0;
        foreach (ushort[] a in Arrays(ctx))
        {
            Array.Copy(flat, k, a, 0, a.Length);
            k += a.Length;
        }
        Assert.Equal(flat.Length, k);
        return ctx;
    }

    public static ushort[] FromContext(nmv_context ctx)
    {
        var flat = new ushort[Size];
        int k = 0;
        foreach (ushort[] a in Arrays(ctx))
        {
            Array.Copy(a, 0, flat, k, a.Length);
            k += a.Length;
        }
        return flat;
    }

    public static ushort[] Default()
    {
        var flat = new ushort[E6Shim.e6_nmvc_size()];
        Assert.Equal(0, E6Shim.e6_default_nmvc(flat, flat.Length));
        return flat;
    }

    /// <summary>Replaces every CDF with a random valid inverse CDF (counter slot 0).</summary>
    public static ushort[] Random(Random rng)
    {
        var ctx = new nmv_context();
        foreach (ushort[] a in Arrays(ctx))
        {
            int nsyms = a.Length - 1;
            var cuts = new int[nsyms - 1];
            for (int i = 0; i < cuts.Length; i++) cuts[i] = rng.Next(1, 32768);
            Array.Sort(cuts);
            for (int i = 0; i < nsyms - 1; i++) a[i] = (ushort)(32768 - cuts[i]);
            a[nsyms - 1] = 0;
            a[nsyms] = 0;
        }
        return FromContext(ctx);
    }
}

internal sealed class CostTables
{
    public readonly int[] Joint = new int[MV_JOINTS];
    public readonly int[] Comp0 = new int[MV_VALS];
    public readonly int[] Comp1 = new int[MV_VALS];

    public Ptr<int>[] Comp => new[] { new Ptr<int>(Comp0, MV_MAX), new Ptr<int>(Comp1, MV_MAX) };

    /// <summary>libaom's tables for the given context / precision.</summary>
    public static CostTables FromLibaom(ushort[] nmvc, int precision)
    {
        var t = new CostTables();
        E6Shim.e6_build_nmv_cost_table(nmvc, precision, t.Joint, t.Comp0, t.Comp1);
        return t;
    }
}

public class MvCostTests
{
    [Fact]
    public void NmvContextLayoutMatchesLibaom()
    {
        Assert.Equal(E6Shim.e6_nmvc_size(), NmvFlat.Size);
        ushort[] flat = NmvFlat.Default();
        Assert.Equal(flat, NmvFlat.FromContext(NmvFlat.ToContext(flat)));
    }

    [Theory]
    [InlineData(-1, 0)] // MV_SUBPEL_NONE, default CDFs
    [InlineData(0, 0)]  // MV_SUBPEL_LOW_PRECISION
    [InlineData(1, 0)]  // MV_SUBPEL_HIGH_PRECISION
    [InlineData(-1, 1)] // random CDFs
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    [InlineData(1, 4)]
    public void BuildNmvCostTableMatchesLibaom(int precision, int seed)
    {
        ushort[] flat = seed == 0 ? NmvFlat.Default() : NmvFlat.Random(new Random(seed));
        CostTables expected = CostTables.FromLibaom(flat, precision);

        var actual = new CostTables();
        av1_build_nmv_cost_table(actual.Joint, actual.Comp, NmvFlat.ToContext(flat), precision);

        Assert.Equal(expected.Joint, actual.Joint);
        Assert.Equal(expected.Comp0, actual.Comp0);
        Assert.Equal(expected.Comp1, actual.Comp1);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(-1, 2)]
    public void MvBitCostMatchesLibaom(int precision, int seed)
    {
        var rng = new Random(100 + seed);
        ushort[] flat = seed == 0 ? NmvFlat.Default() : NmvFlat.Random(rng);
        CostTables t = CostTables.FromLibaom(flat, precision);
        Ptr<int>[] comp = t.Comp;

        for (int i = 0; i < 20000; i++)
        {
            int refRow = rng.Next(-4000, 4001), refCol = rng.Next(-4000, 4001);
            int dRow = RandomDiff(rng), dCol = RandomDiff(rng);
            int weight = rng.Next(4) switch { 0 => 108, 1 => 120, 2 => 128, _ => rng.Next(0, 256) };
            var mv = new MV((short)(refRow + dRow), (short)(refCol + dCol));
            var refMv = new MV((short)refRow, (short)refCol);

            int expected = E6Shim.e6_mv_bit_cost(mv.row, mv.col, refRow, refCol, t.Joint, t.Comp0, t.Comp1, weight);
            int actual = av1_mv_bit_cost(mv, refMv, t.Joint, comp, weight);
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>A diff in [-MV_MAX, MV_MAX], biased toward small values (all MV classes covered).</summary>
    internal static int RandomDiff(Random rng)
    {
        if (rng.Next(4) == 0) return 0;
        int mag = 1 + rng.Next(1 << rng.Next(0, 15));
        mag = Math.Min(mag, MV_MAX);
        return rng.Next(2) == 0 ? -mag : mag;
    }
}

public class MvEncodeTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(0, 1, 2)]
    [InlineData(-1, 1, 3)]
    [InlineData(1, 0, 4)]
    [InlineData(0, 0, 5)]
    [InlineData(-1, 0, 6)]
    public void EncodeMvMatchesLibaom(int usehp, int allowUpdateCdf, int seed)
    {
        var rng = new Random(seed);
        const int count = 4000;
        var mvs = new short[count * 2];
        var refs = new short[count * 2];
        for (int i = 0; i < count; i++)
        {
            int dRow, dCol;
            do
            {
                dRow = MvCostTests.RandomDiff(rng);
                dCol = MvCostTests.RandomDiff(rng);
            } while (dRow == 0 && dCol == 0);  // MV_JOINT_ZERO is never coded
            int refRow = rng.Next(-2000, 2001), refCol = rng.Next(-2000, 2001);
            refs[2 * i] = (short)refRow;
            refs[2 * i + 1] = (short)refCol;
            mvs[2 * i] = (short)(refRow + dRow);
            mvs[2 * i + 1] = (short)(refCol + dCol);
        }

        ushort[] cNmvc = NmvFlat.Default();
        var csCtx = NmvFlat.ToContext(cNmvc);
        var expected = new byte[count * 16 + 1024];
        int expectedLength = E6Shim.e6_encode_mvs(mvs, refs, count, usehp, allowUpdateCdf, cNmvc, expected,
                                                  expected.Length, out int expectedMaxMag);
        Assert.True(expectedLength > 0);

        var cpi = new AV1_COMP();
        cpi.common.features.cur_frame_force_integer_mv = false;
        cpi.sf.mv_sf.auto_mv_step_size = 1;
        var td = new ThreadData();
        var w = new aom_writer();
        var buffer = new byte[expected.Length];
        aom_start_encode(w, buffer, buffer.Length);
        w.allow_update_cdf = (byte)allowUpdateCdf;
        for (int i = 0; i < count; i++)
        {
            var mv = new MV(mvs[2 * i], mvs[2 * i + 1]);
            var refMv = new MV(refs[2 * i], refs[2 * i + 1]);
            av1_encode_mv(cpi, w, td, mv, refMv, csCtx, usehp);
        }
        Assert.True(aom_stop_encode(w) >= 0);

        Assert.Equal(expectedLength, (int)w.pos);
        Assert.True(buffer.AsSpan(0, expectedLength).SequenceEqual(expected.AsSpan(0, expectedLength)));
        Assert.Equal(cNmvc, NmvFlat.FromContext(csCtx));
        Assert.Equal(expectedMaxMag, td.max_mv_magnitude);
    }
}

public class RefMvTests
{
    [Fact]
    public void FindBestRefMvsFromStackMatchesLibaom()
    {
        var rng = new Random(7);
        for (int iter = 0; iter < 5000; iter++)
        {
            int allowHp = rng.Next(2), isInteger = rng.Next(4) == 0 ? 1 : 0;
            int refFrame = rng.Next(LAST_FRAME, ALTREF_FRAME + 1);
            int count = rng.Next(0, USABLE_REF_MV_STACK_SIZE + 1);
            var stack = new uint[USABLE_REF_MV_STACK_SIZE];
            for (int i = 0; i < stack.Length; i++) stack[i] = RandomMv(rng);
            uint global = RandomMv(rng);

            E6Shim.e6_find_best_ref_mvs(allowHp, refFrame, isInteger, stack, count, global,
                                        out uint expectedNearest, out uint expectedNear);

            var ext = new MB_MODE_INFO_EXT();
            for (int i = 0; i < stack.Length; i++) ext.ref_mv_stack[refFrame][i].this_mv.as_int = stack[i];
            ext.ref_mv_count[refFrame] = (byte)count;
            ext.global_mvs[refFrame].as_int = global;
            int_mv nearest = default, near = default;
            av1_find_best_ref_mvs_from_stack(allowHp, ext, refFrame, ref nearest, ref near, isInteger);

            Assert.Equal(expectedNearest, nearest.as_int);
            Assert.Equal(expectedNear, near.as_int);
        }
    }

    private static readonly (int, int)[] CompoundPairs =
    {
        (LAST_FRAME, LAST2_FRAME), (LAST_FRAME, LAST3_FRAME), (LAST_FRAME, GOLDEN_FRAME),
        (BWDREF_FRAME, ALTREF_FRAME),
        (LAST_FRAME, BWDREF_FRAME), (LAST2_FRAME, ALTREF2_FRAME), (LAST3_FRAME, ALTREF_FRAME),
        (GOLDEN_FRAME, BWDREF_FRAME), (GOLDEN_FRAME, ALTREF_FRAME), (LAST_FRAME, ALTREF_FRAME),
    };

    [Fact]
    public void GetRefMvFromStackMatchesLibaom()
    {
        var rng = new Random(8);
        const int n = USABLE_REF_MV_STACK_SIZE;
        for (int iter = 0; iter < 3000; iter++)
        {
            var stackThis = new uint[MODE_CTX_REF_FRAMES * n];
            var stackComp = new uint[MODE_CTX_REF_FRAMES * n];
            var counts = new byte[MODE_CTX_REF_FRAMES];
            var globals = new uint[REF_FRAMES];
            for (int i = 0; i < stackThis.Length; i++)
            {
                stackThis[i] = RandomMv(rng);
                stackComp[i] = RandomMv(rng);
            }
            for (int i = 0; i < counts.Length; i++) counts[i] = (byte)rng.Next(0, n + 1);
            for (int i = 0; i < globals.Length; i++) globals[i] = RandomMv(rng);

            int rf0, rf1, refIdx;
            if (rng.Next(2) == 0)
            {
                rf0 = rng.Next(INTRA_FRAME, ALTREF_FRAME + 1);
                rf1 = rng.Next(2) == 0 ? NONE_FRAME : INTRA_FRAME;
                refIdx = 0;
            }
            else
            {
                (rf0, rf1) = CompoundPairs[rng.Next(CompoundPairs.Length)];
                refIdx = rng.Next(2);
            }
            int refMvIdx = rng.Next(0, n);

            uint expected = E6Shim.e6_get_ref_mv_from_stack(refIdx, rf0, rf1, refMvIdx, stackThis, stackComp,
                                                            counts, globals);

            var ext = new MB_MODE_INFO_EXT();
            for (int t = 0; t < MODE_CTX_REF_FRAMES; t++)
            {
                for (int i = 0; i < n; i++)
                {
                    ext.ref_mv_stack[t][i].this_mv.as_int = stackThis[t * n + i];
                    ext.ref_mv_stack[t][i].comp_mv.as_int = stackComp[t * n + i];
                }
                ext.ref_mv_count[t] = counts[t];
            }
            for (int t = 0; t < REF_FRAMES; t++) ext.global_mvs[t].as_int = globals[t];
            var refFrame = new int[] { rf0, rf1 };  // MV_REFERENCE_FRAME is int

            int_mv actual = av1_get_ref_mv_from_stack(refIdx, refFrame, refMvIdx, ext);
            Assert.Equal(expected, actual.as_int);
        }
    }

    private static uint RandomMv(Random rng)
    {
        int row = rng.Next(3) == 0 ? rng.Next(-32768, 32768) : rng.Next(-600, 601);
        int col = rng.Next(3) == 0 ? rng.Next(-32768, 32768) : rng.Next(-600, 601);
        var m = new int_mv();
        m.as_mv.row = (short)row;
        m.as_mv.col = (short)col;
        return m.as_int;
    }
}

public class SearchSiteTests
{
    private static int[] Serialize(search_site_config cfg)
    {
        int steps = MAX_MVSEARCH_STEPS * 2, sites = 16 + 1;
        var output = new int[E6Shim.e6_search_site_ints()];
        int k = 0;
        output[k++] = cfg.num_search_steps;
        output[k++] = cfg.stride;
        for (int i = 0; i < steps; i++) output[k++] = cfg.searches_per_step[i];
        for (int i = 0; i < steps; i++) output[k++] = cfg.radius[i];
        for (int i = 0; i < steps; i++)
        {
            for (int j = 0; j < sites; j++)
            {
                output[k++] = cfg.site[i][j].mv.row;
                output[k++] = cfg.site[i][j].mv.col;
                output[k++] = cfg.site[i][j].offset;
            }
        }
        Assert.Equal(output.Length, k);
        return output;
    }

    public static IEnumerable<object[]> Cases()
    {
        int[] strides = { 16, 64, 288, 704, 1344, 4032 };
        for (int method = -1; method < NUM_DISTINCT_SEARCH_METHODS; method++)
            for (int level = 0; level <= 1; level++)
                foreach (int stride in strides)
                    yield return new object[] { method, level, stride };
    }

    /// <summary>method -1 is av1_init_motion_fpf; 0..5 index av1_init_motion_compensation.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void SearchSiteConfigMatchesLibaom(int method, int level, int stride)
    {
        var expected = new int[E6Shim.e6_search_site_ints()];
        E6Shim.e6_init_search_site(method, stride, level, expected);

        var cfg = new search_site_config();
        if (method < 0) av1_init_motion_fpf(cfg, stride);
        else av1_init_motion_compensation[method](cfg, stride, level);

        Assert.Equal(expected, Serialize(cfg));
    }

    [Fact]
    public void InitSearchRangeMatchesLibaom()
    {
        for (int size = -4; size < 3000; size++)
            Assert.Equal(E6Shim.e6_init_search_range(size), av1_init_search_range(size));
    }
}
