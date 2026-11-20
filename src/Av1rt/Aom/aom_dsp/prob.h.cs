// Port of libaom aom_dsp/prob.h

global using static Av1rt.Aom.prob;
global using aom_cdf_prob = ushort;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class prob
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CDF_SIZE(int x) => x + 1;

    internal const int CDF_PROB_BITS = 15;
    internal const int CDF_PROB_TOP = 1 << CDF_PROB_BITS;

    /*The value stored in an iCDF is CDF_PROB_TOP minus the actual cumulative
      probability (an "inverse" CDF).
      This function converts from one representation to the other (and is its own
      inverse).*/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AOM_ICDF(int x) => CDF_PROB_TOP - x;

    // [port] AOM_CDFn(a0, ...) expand to the n + 1 initializer values of one CDF in C:
    // AOM_ICDF(a0), ..., AOM_ICDF(CDF_PROB_TOP), 0 (the trailing 0 is the adaptation counter).
    // Here each returns that CDF as a new ushort[CDF_SIZE(n)], so a C table
    // `{ { AOM_CDF2(a) }, { AOM_CDF2(b) } }` becomes `{ AOM_CDF2(a), AOM_CDF2(b) }` (ushort[][]).
    internal static aom_cdf_prob[] AOM_CDF2(int a0) =>
        new aom_cdf_prob[] { (aom_cdf_prob)AOM_ICDF(a0), (aom_cdf_prob)AOM_ICDF(CDF_PROB_TOP), 0 };

    internal static aom_cdf_prob[] AOM_CDF3(int a0, int a1) =>
        new aom_cdf_prob[] { (aom_cdf_prob)AOM_ICDF(a0), (aom_cdf_prob)AOM_ICDF(a1), (aom_cdf_prob)AOM_ICDF(CDF_PROB_TOP), 0 };

    internal static aom_cdf_prob[] AOM_CDF4(int a0, int a1, int a2) => make_cdf(a0, a1, a2);

    internal static aom_cdf_prob[] AOM_CDF5(int a0, int a1, int a2, int a3) => make_cdf(a0, a1, a2, a3);

    internal static aom_cdf_prob[] AOM_CDF6(int a0, int a1, int a2, int a3, int a4) => make_cdf(a0, a1, a2, a3, a4);

    internal static aom_cdf_prob[] AOM_CDF7(int a0, int a1, int a2, int a3, int a4, int a5) =>
        make_cdf(a0, a1, a2, a3, a4, a5);

    internal static aom_cdf_prob[] AOM_CDF8(int a0, int a1, int a2, int a3, int a4, int a5, int a6) =>
        make_cdf(a0, a1, a2, a3, a4, a5, a6);

    internal static aom_cdf_prob[] AOM_CDF9(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7) =>
        make_cdf(a0, a1, a2, a3, a4, a5, a6, a7);

    internal static aom_cdf_prob[] AOM_CDF10(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7,
                                             int a8) =>
        make_cdf(a0, a1, a2, a3, a4, a5, a6, a7, a8);

    internal static aom_cdf_prob[] AOM_CDF11(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7,
                                             int a8, int a9) =>
        make_cdf(a0, a1, a2, a3, a4, a5, a6, a7, a8, a9);

    internal static aom_cdf_prob[] AOM_CDF12(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7,
                                             int a8, int a9, int a10) =>
        make_cdf(a0, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10);

    internal static aom_cdf_prob[] AOM_CDF13(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7,
                                             int a8, int a9, int a10, int a11) =>
        make_cdf(a0, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11);

    internal static aom_cdf_prob[] AOM_CDF14(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7,
                                             int a8, int a9, int a10, int a11, int a12) =>
        make_cdf(a0, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12);

    internal static aom_cdf_prob[] AOM_CDF15(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7,
                                             int a8, int a9, int a10, int a11, int a12, int a13) =>
        make_cdf(a0, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13);

    internal static aom_cdf_prob[] AOM_CDF16(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7,
                                             int a8, int a9, int a10, int a11, int a12, int a13, int a14) =>
        make_cdf(a0, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14);

    // AOM_ICDF of each value, then AOM_ICDF(CDF_PROB_TOP) and the 0 counter.
    private static aom_cdf_prob[] make_cdf(params int[] a)
    {
        var r = new aom_cdf_prob[a.Length + 2];
        for (int i = 0; i < a.Length; i++) r[i] = (aom_cdf_prob)AOM_ICDF(a[i]);
        r[a.Length] = (aom_cdf_prob)AOM_ICDF(CDF_PROB_TOP);
        r[a.Length + 1] = 0;
        return r;
    }

    // [subset] get_prob never runs in the subset and is not ported.

    // [port] C declares `int8_t val`; every caller passes a symbol index in [0, nsymbs) with
    // nsymbs <= 16, so the int8_t conversion never changes the value. Taking int saves the 88
    // call sites a cast.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void update_cdf(Ptr<aom_cdf_prob> cdf, int val, int nsymbs)
    {
        update_cdf(cdf.AsSpan(0, nsymbs + 1), val);
    }

    // [port] update_cdf on the nsymbs + 1 entries of the cdf (the probabilities and the
    // adaptation counter). With the loop bound taken from the span's length the JIT drops the
    // per-entry bounds checks.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void update_cdf(Span<aom_cdf_prob> cdf, int val)
    {
        int nsymbs = cdf.Length - 1;
        Debug.Assert(nsymbs < 17);
        Debug.Assert(val >= 0 && val < nsymbs);
        int count = cdf[nsymbs];

        // rate is computed in the spec as:
        //  3 + ( cdf[N] > 15 ) + ( cdf[N] > 31 ) + Min(FloorLog2(N), 2)
        // In this case cdf[N] is |count|.
        // Min(FloorLog2(N), 2) is 1 for nsymbs == {2, 3} and 2 for all
        // nsymbs > 3. So the equation becomes:
        //  4 + (count > 15) + (count > 31) + (nsymbs > 3).
        // Note that the largest value for count is 32 (it is not incremented beyond
        // 32). So using that information:
        //  count >> 4 is 0 for count from 0 to 15.
        //  count >> 4 is 1 for count from 16 to 31.
        //  count >> 4 is 2 for count == 31.
        // Now, the equation becomes:
        //  4 + (count >> 4) + (nsymbs > 3).
        int rate = 4 + (count >> 4) + B(nsymbs > 3);

        // C: do { ... } while (++i < nsymbs - 1); nsymbs >= 2, so the bodies match.
        for (int i = 0; i < cdf.Length - 2; i++)
        {
            // Compute both and select, a branch on val is hard to predict.
            int c = cdf[i];
            int up = c + ((CDF_PROB_TOP - c) >> rate);
            int down = c - (c >> rate);
            cdf[i] = (aom_cdf_prob)(i < val ? up : down);
        }
        cdf[nsymbs] = (aom_cdf_prob)(count + B(count < 32));
    }
}
