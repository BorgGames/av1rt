using System;
using System.Diagnostics;
using System.Runtime.Intrinsics;
using Av1rt.Aom;
using static Av1rt.Aom.intrin;
using __m128i = System.Runtime.Intrinsics.Vector128<long>;

/// <summary>Micro-benchmarks of individual ported kernels (C port vs SIMD port).</summary>
internal static class KernelBench
{
    public static int Run(string[] args)
    {
        var rnd = new Random(1);
        const int stride = 4096 + 160;
        var src = new byte[stride * 160];
        var refb = new byte[stride * 160];
        rnd.NextBytes(src);
        rnd.NextBytes(refb);
        Time("aom_sad32x32_c", () =>
        {
            uint s = 0;
            for (int i = 0; i < 4096; i += 32) s += sad_h.aom_sad32x32_c(new Ptr<byte>(src, i), stride, new Ptr<byte>(refb, i + 1), stride);
            return s;
        });
        Time("aom_sad32x32_avx2", () =>
        {
            uint s = 0;
            for (int i = 0; i < 4096; i += 32) s += sad_avx2.aom_sad32x32_avx2(new Ptr<byte>(src, i), stride, new Ptr<byte>(refb, i + 1), stride);
            return s;
        });
        var fc0 = new FRAME_CONTEXT();
        var fc1 = new FRAME_CONTEXT();
        Time("FRAME_CONTEXT.CopyFrom/128", () => { fc1.CopyFrom(fc0); return 0; });
        Console.WriteLine(ShiftVar(Vector128.Create(-1000, 1000, 77, -77).AsInt64(), args.Length + 1));
        return 0;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static __m128i ShiftVar(__m128i v, int bit) => _mm_srai_epi32(v, bit);

    private static void Time(string name, Func<uint> f)
    {
        uint check = f();
        for (int w = 0; w < 200; w++) f();
        var sw = Stopwatch.StartNew();
        int n = 0;
        while (sw.ElapsedMilliseconds < 1000) { f(); n++; }
        double ns = sw.Elapsed.TotalMilliseconds * 1e6 / (n * 128.0);
        Console.WriteLine($"{name,-24} {ns,8:F1} ns/call  (check {check})");
    }
}
