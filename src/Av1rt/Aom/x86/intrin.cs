// Intel intrinsic names over System.Runtime.Intrinsics, so libaom's x86 SIMD code
// (aom_dsp/x86, av1/common/x86, av1/encoder/x86) can be ported line by line.

global using __m128i = System.Runtime.Intrinsics.Vector128<long>;
global using __m256i = System.Runtime.Intrinsics.Vector256<long>;
global using __m128 = System.Runtime.Intrinsics.Vector128<float>;
global using __m128d = System.Runtime.Intrinsics.Vector128<double>;
global using __m256 = System.Runtime.Intrinsics.Vector256<float>;
global using __m256d = System.Runtime.Intrinsics.Vector256<double>;
global using static Av1rt.Aom.intrin;

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Av1rt.Aom;

/// <summary>
/// Whether the SIMD kernels are used. The ported x86 kernels (<c>*_sse2</c>, <c>*_ssse3</c>,
/// <c>*_sse4_1</c>, <c>*_avx2</c>) are all gated on AVX2, which every x64 CPU that has it pairs
/// with SSE2 to SSE4.2 and SSSE3. Set the environment variable <c>AV1RT_SIMD=0</c> to force the
/// C versions (for isolating a SIMD mismatch).
/// </summary>
internal static class Simd
{
    internal static readonly bool UseAvx2 =
        Avx2.IsSupported && Environment.GetEnvironmentVariable("AV1RT_SIMD") != "0";
}

/// <summary>
/// The Intel intrinsics libaom uses, as one-line wrappers with C's untyped register semantics:
/// <c>__m128i</c> is <see cref="Vector128{T}"/> of long and every wrapper reinterprets to the
/// lane type the instruction works on (free). Immediate operands are <c>int</c> like in C; the
/// wrappers are aggressively inlined so constant arguments reach the JIT as immediates.
/// </summary>
/// <remarks>
/// <para>
/// Memory operands are <see cref="Ptr{T}"/> (or spans) instead of <c>(const __m128i *)p</c>
/// casts: a load reads 16 (32) bytes starting at the pointer, whatever its element type, and is
/// bounds checked against the underlying array. <c>__m128i</c> arrays are plain
/// <c>Span&lt;__m128i&gt;</c> / <c>__m128i[]</c> and are indexed directly.
/// </para>
/// <para>
/// AVX-512 only intrinsics (<c>_mm_srai_epi64</c>, <c>_mm_sra_epi64</c>, ...) and MMX
/// (<c>__m64</c>) ones are deliberately missing.
/// </para>
/// </remarks>
internal static partial class intrin
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    // ------------------------------------------------------------------------------------------
    // Loads and stores (bounds checked). Generic over the element type of the C pointer.
    // ------------------------------------------------------------------------------------------

    [MethodImpl(AI)] private static ReadOnlySpan<byte> Bytes<T>(Ptr<T> p, int count) where T : unmanaged =>
        MemoryMarshal.AsBytes(new ReadOnlySpan<T>(p.Array, p.Offset, count));
    [MethodImpl(AI)] private static Span<byte> WBytes<T>(Ptr<T> p, int count) where T : unmanaged =>
        MemoryMarshal.AsBytes(new Span<T>(p.Array, p.Offset, count));

    // 16 bytes
    [MethodImpl(AI)] internal static __m128i _mm_loadu_si128<T>(Ptr<T> p) where T : unmanaged =>
        Vector128.Create<T>(new ReadOnlySpan<T>(p.Array, p.Offset, Vector128<T>.Count)).As<T, long>();
    [MethodImpl(AI)] internal static __m128i _mm_loadu_si128<T>(ReadOnlySpan<T> s) where T : unmanaged =>
        Vector128.Create<T>(s).As<T, long>();
    [MethodImpl(AI)] internal static __m128i _mm_loadu_si128<T>(Span<T> s) where T : unmanaged =>
        Vector128.Create<T>((ReadOnlySpan<T>)s).As<T, long>();
    [MethodImpl(AI)] internal static __m128i _mm_load_si128<T>(Ptr<T> p) where T : unmanaged => _mm_loadu_si128(p);
    [MethodImpl(AI)] internal static __m128i _mm_load_si128<T>(ReadOnlySpan<T> s) where T : unmanaged => _mm_loadu_si128(s);
    [MethodImpl(AI)] internal static __m128i _mm_load_si128<T>(Span<T> s) where T : unmanaged => _mm_loadu_si128(s);
    [MethodImpl(AI)] internal static __m128i _mm_lddqu_si128<T>(Ptr<T> p) where T : unmanaged => _mm_loadu_si128(p);

    [MethodImpl(AI)] internal static void _mm_storeu_si128<T>(Ptr<T> p, __m128i a) where T : unmanaged =>
        a.As<long, T>().CopyTo(new Span<T>(p.Array, p.Offset, Vector128<T>.Count));
    [MethodImpl(AI)] internal static void _mm_storeu_si128<T>(Span<T> s, __m128i a) where T : unmanaged =>
        a.As<long, T>().CopyTo(s);
    [MethodImpl(AI)] internal static void _mm_store_si128<T>(Ptr<T> p, __m128i a) where T : unmanaged => _mm_storeu_si128(p, a);
    [MethodImpl(AI)] internal static void _mm_store_si128<T>(Span<T> s, __m128i a) where T : unmanaged => _mm_storeu_si128(s, a);

    // 8 bytes (low half; the high half of the register is zeroed)
    [MethodImpl(AI)] internal static __m128i _mm_loadl_epi64<T>(Ptr<T> p) where T : unmanaged =>
        Vector128.CreateScalar(BinaryPrimitives.ReadInt64LittleEndian(Bytes(p, Vector64<T>.Count)));
    [MethodImpl(AI)] internal static __m128i _mm_loadl_epi64<T>(ReadOnlySpan<T> s) where T : unmanaged =>
        Vector128.CreateScalar(BinaryPrimitives.ReadInt64LittleEndian(MemoryMarshal.AsBytes(s)));
    [MethodImpl(AI)] internal static __m128i _mm_loadl_epi64<T>(Span<T> s) where T : unmanaged =>
        Vector128.CreateScalar(BinaryPrimitives.ReadInt64LittleEndian(MemoryMarshal.AsBytes(s)));
    [MethodImpl(AI)] internal static void _mm_storel_epi64<T>(Ptr<T> p, __m128i a) where T : unmanaged =>
        BinaryPrimitives.WriteInt64LittleEndian(WBytes(p, Vector64<T>.Count), a.ToScalar());
    [MethodImpl(AI)] internal static void _mm_storel_epi64<T>(Span<T> s, __m128i a) where T : unmanaged =>
        BinaryPrimitives.WriteInt64LittleEndian(MemoryMarshal.AsBytes(s), a.ToScalar());

    // 4 bytes (the rest of the register is zeroed)
    [MethodImpl(AI)] internal static __m128i _mm_loadu_si32<T>(Ptr<T> p) where T : unmanaged =>
        Vector128.CreateScalar(BinaryPrimitives.ReadInt32LittleEndian(Bytes(p, Vector64<T>.Count / 2))).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_loadu_si32<T>(ReadOnlySpan<T> s) where T : unmanaged =>
        Vector128.CreateScalar(BinaryPrimitives.ReadInt32LittleEndian(MemoryMarshal.AsBytes(s))).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_loadu_si32<T>(Span<T> s) where T : unmanaged =>
        Vector128.CreateScalar(BinaryPrimitives.ReadInt32LittleEndian(MemoryMarshal.AsBytes(s))).AsInt64();
    [MethodImpl(AI)] internal static void _mm_storeu_si32<T>(Ptr<T> p, __m128i a) where T : unmanaged =>
        BinaryPrimitives.WriteInt32LittleEndian(WBytes(p, Vector64<T>.Count / 2), a.AsInt32().ToScalar());
    [MethodImpl(AI)] internal static void _mm_storeu_si32<T>(Span<T> s, __m128i a) where T : unmanaged =>
        BinaryPrimitives.WriteInt32LittleEndian(MemoryMarshal.AsBytes(s), a.AsInt32().ToScalar());

    // High 8 bytes (libaom loads/stores the upper half through the pd/pi forms).
    [MethodImpl(AI)] internal static __m128d _mm_loadh_pd<T>(__m128d a, Ptr<T> p) where T : unmanaged =>
        a.AsInt64().WithElement(1, BinaryPrimitives.ReadInt64LittleEndian(Bytes(p, Vector64<T>.Count))).AsDouble();
    [MethodImpl(AI)] internal static __m128 _mm_loadh_pi<T>(__m128 a, Ptr<T> p) where T : unmanaged =>
        a.AsInt64().WithElement(1, BinaryPrimitives.ReadInt64LittleEndian(Bytes(p, Vector64<T>.Count))).AsSingle();
    [MethodImpl(AI)] internal static void _mm_storeh_pi<T>(Ptr<T> p, __m128 a) where T : unmanaged =>
        BinaryPrimitives.WriteInt64LittleEndian(WBytes(p, Vector64<T>.Count), a.AsInt64().GetElement(1));
    [MethodImpl(AI)] internal static void _mm_storeh_pd<T>(Ptr<T> p, __m128d a) where T : unmanaged =>
        BinaryPrimitives.WriteInt64LittleEndian(WBytes(p, Vector64<T>.Count), a.AsInt64().GetElement(1));

    /// <summary>AVX2 masked load of four ints; only lanes whose mask sign bit is set are read.</summary>
    [MethodImpl(AI)] internal static __m128i _mm_maskload_epi32(Ptr<int> p, __m128i mask)
    {
        Vector128<int> m = mask.AsInt32();
        return Vector128.Create(m.GetElement(0) < 0 ? p[0] : 0, m.GetElement(1) < 0 ? p[1] : 0,
                                m.GetElement(2) < 0 ? p[2] : 0, m.GetElement(3) < 0 ? p[3] : 0).AsInt64();
    }

    // 32 bytes
    [MethodImpl(AI)] internal static __m256i _mm256_loadu_si256<T>(Ptr<T> p) where T : unmanaged =>
        Vector256.Create<T>(new ReadOnlySpan<T>(p.Array, p.Offset, Vector256<T>.Count)).As<T, long>();
    [MethodImpl(AI)] internal static __m256i _mm256_loadu_si256<T>(ReadOnlySpan<T> s) where T : unmanaged =>
        Vector256.Create<T>(s).As<T, long>();
    [MethodImpl(AI)] internal static __m256i _mm256_loadu_si256<T>(Span<T> s) where T : unmanaged =>
        Vector256.Create<T>((ReadOnlySpan<T>)s).As<T, long>();
    [MethodImpl(AI)] internal static __m256i _mm256_load_si256<T>(Ptr<T> p) where T : unmanaged => _mm256_loadu_si256(p);
    [MethodImpl(AI)] internal static __m256i _mm256_load_si256<T>(ReadOnlySpan<T> s) where T : unmanaged => _mm256_loadu_si256(s);
    [MethodImpl(AI)] internal static __m256i _mm256_load_si256<T>(Span<T> s) where T : unmanaged => _mm256_loadu_si256(s);
    [MethodImpl(AI)] internal static __m256i _mm256_lddqu_si256<T>(Ptr<T> p) where T : unmanaged => _mm256_loadu_si256(p);

    [MethodImpl(AI)] internal static void _mm256_storeu_si256<T>(Ptr<T> p, __m256i a) where T : unmanaged =>
        a.As<long, T>().CopyTo(new Span<T>(p.Array, p.Offset, Vector256<T>.Count));
    [MethodImpl(AI)] internal static void _mm256_storeu_si256<T>(Span<T> s, __m256i a) where T : unmanaged =>
        a.As<long, T>().CopyTo(s);
    [MethodImpl(AI)] internal static void _mm256_store_si256<T>(Ptr<T> p, __m256i a) where T : unmanaged => _mm256_storeu_si256(p, a);
    [MethodImpl(AI)] internal static void _mm256_store_si256<T>(Span<T> s, __m256i a) where T : unmanaged => _mm256_storeu_si256(s, a);

    [MethodImpl(AI)] internal static __m256d _mm256_broadcast_pd<T>(Ptr<T> p) where T : unmanaged
    {
        Vector128<double> v = _mm_loadu_si128(p).AsDouble();
        return Vector256.Create(v, v);
    }

    // ------------------------------------------------------------------------------------------
    // 128-bit integer
    // ------------------------------------------------------------------------------------------

    [MethodImpl(AI)] internal static __m128i _mm_setzero_si128() => Vector128<long>.Zero;
    [MethodImpl(AI)] internal static __m128i _mm_set1_epi8(int a) => Vector128.Create((sbyte)a).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_set1_epi16(int a) => Vector128.Create((short)a).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_set1_epi32(int a) => Vector128.Create(a).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_set1_epi64x(long a) => Vector128.Create(a);
    [MethodImpl(AI)] internal static __m128i _mm_set_epi64x(long e1, long e0) => Vector128.Create(e0, e1);
    [MethodImpl(AI)] internal static __m128i _mm_set_epi32(int e3, int e2, int e1, int e0) => Vector128.Create(e0, e1, e2, e3).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_setr_epi32(int e0, int e1, int e2, int e3) => Vector128.Create(e0, e1, e2, e3).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_set_epi16(int e7, int e6, int e5, int e4, int e3, int e2, int e1, int e0) =>
        Vector128.Create((short)e0, (short)e1, (short)e2, (short)e3, (short)e4, (short)e5, (short)e6, (short)e7).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_setr_epi16(int e0, int e1, int e2, int e3, int e4, int e5, int e6, int e7) =>
        Vector128.Create((short)e0, (short)e1, (short)e2, (short)e3, (short)e4, (short)e5, (short)e6, (short)e7).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_set_epi8(int e15, int e14, int e13, int e12, int e11, int e10, int e9, int e8,
                                                          int e7, int e6, int e5, int e4, int e3, int e2, int e1, int e0) =>
        Vector128.Create((sbyte)e0, (sbyte)e1, (sbyte)e2, (sbyte)e3, (sbyte)e4, (sbyte)e5, (sbyte)e6, (sbyte)e7,
                         (sbyte)e8, (sbyte)e9, (sbyte)e10, (sbyte)e11, (sbyte)e12, (sbyte)e13, (sbyte)e14, (sbyte)e15).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_setr_epi8(int e0, int e1, int e2, int e3, int e4, int e5, int e6, int e7,
                                                           int e8, int e9, int e10, int e11, int e12, int e13, int e14, int e15) =>
        Vector128.Create((sbyte)e0, (sbyte)e1, (sbyte)e2, (sbyte)e3, (sbyte)e4, (sbyte)e5, (sbyte)e6, (sbyte)e7,
                         (sbyte)e8, (sbyte)e9, (sbyte)e10, (sbyte)e11, (sbyte)e12, (sbyte)e13, (sbyte)e14, (sbyte)e15).AsInt64();

    [MethodImpl(AI)] internal static __m128i _mm_cvtsi32_si128(int a) => Vector128.CreateScalar(a).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cvtsi64_si128(long a) => Vector128.CreateScalar(a);
    [MethodImpl(AI)] internal static int _mm_cvtsi128_si32(__m128i a) => a.AsInt32().ToScalar();
    [MethodImpl(AI)] internal static long _mm_cvtsi128_si64(__m128i a) => a.ToScalar();

    [MethodImpl(AI)] internal static __m128 _mm_castsi128_ps(__m128i a) => a.AsSingle();
    [MethodImpl(AI)] internal static __m128d _mm_castsi128_pd(__m128i a) => a.AsDouble();
    [MethodImpl(AI)] internal static __m128i _mm_castps_si128(__m128 a) => a.AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_castpd_si128(__m128d a) => a.AsInt64();
    [MethodImpl(AI)] internal static __m128 _mm_castpd_ps(__m128d a) => a.AsSingle();
    [MethodImpl(AI)] internal static __m128d _mm_castps_pd(__m128 a) => a.AsDouble();

    // Arithmetic
    [MethodImpl(AI)] internal static __m128i _mm_add_epi8(__m128i a, __m128i b) => Sse2.Add(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_add_epi16(__m128i a, __m128i b) => Sse2.Add(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_add_epi32(__m128i a, __m128i b) => Sse2.Add(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_add_epi64(__m128i a, __m128i b) => Sse2.Add(a, b);
    [MethodImpl(AI)] internal static __m128i _mm_adds_epi8(__m128i a, __m128i b) => Sse2.AddSaturate(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_adds_epi16(__m128i a, __m128i b) => Sse2.AddSaturate(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_adds_epu8(__m128i a, __m128i b) => Sse2.AddSaturate(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_adds_epu16(__m128i a, __m128i b) => Sse2.AddSaturate(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sub_epi8(__m128i a, __m128i b) => Sse2.Subtract(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sub_epi16(__m128i a, __m128i b) => Sse2.Subtract(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sub_epi32(__m128i a, __m128i b) => Sse2.Subtract(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sub_epi64(__m128i a, __m128i b) => Sse2.Subtract(a, b);
    [MethodImpl(AI)] internal static __m128i _mm_subs_epi8(__m128i a, __m128i b) => Sse2.SubtractSaturate(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_subs_epi16(__m128i a, __m128i b) => Sse2.SubtractSaturate(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_subs_epu8(__m128i a, __m128i b) => Sse2.SubtractSaturate(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_subs_epu16(__m128i a, __m128i b) => Sse2.SubtractSaturate(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_avg_epu8(__m128i a, __m128i b) => Sse2.Average(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_avg_epu16(__m128i a, __m128i b) => Sse2.Average(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_madd_epi16(__m128i a, __m128i b) => Sse2.MultiplyAddAdjacent(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_maddubs_epi16(__m128i a, __m128i b) => Ssse3.MultiplyAddAdjacent(a.AsByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_mulhi_epi16(__m128i a, __m128i b) => Sse2.MultiplyHigh(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_mulhi_epu16(__m128i a, __m128i b) => Sse2.MultiplyHigh(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_mulhrs_epi16(__m128i a, __m128i b) => Ssse3.MultiplyHighRoundScale(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_mullo_epi16(__m128i a, __m128i b) => Sse2.MultiplyLow(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_mullo_epi32(__m128i a, __m128i b) => Sse41.MultiplyLow(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_mul_epu32(__m128i a, __m128i b) => Sse2.Multiply(a.AsUInt32(), b.AsUInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_mul_epi32(__m128i a, __m128i b) => Sse41.Multiply(a.AsInt32(), b.AsInt32());
    [MethodImpl(AI)] internal static __m128i _mm_sad_epu8(__m128i a, __m128i b) => Sse2.SumAbsoluteDifferences(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_hadd_epi16(__m128i a, __m128i b) => Ssse3.HorizontalAdd(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_hadd_epi32(__m128i a, __m128i b) => Ssse3.HorizontalAdd(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_hadds_epi16(__m128i a, __m128i b) => Ssse3.HorizontalAddSaturate(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_abs_epi8(__m128i a) => Ssse3.Abs(a.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_abs_epi16(__m128i a) => Ssse3.Abs(a.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_abs_epi32(__m128i a) => Ssse3.Abs(a.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sign_epi8(__m128i a, __m128i b) => Ssse3.Sign(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sign_epi16(__m128i a, __m128i b) => Ssse3.Sign(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sign_epi32(__m128i a, __m128i b) => Ssse3.Sign(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_minpos_epu16(__m128i a) => Sse41.MinHorizontal(a.AsUInt16()).AsInt64();

    // Min / max
    [MethodImpl(AI)] internal static __m128i _mm_max_epi8(__m128i a, __m128i b) => Sse41.Max(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_max_epu8(__m128i a, __m128i b) => Sse2.Max(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_max_epi16(__m128i a, __m128i b) => Sse2.Max(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_max_epu16(__m128i a, __m128i b) => Sse41.Max(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_max_epi32(__m128i a, __m128i b) => Sse41.Max(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_max_epu32(__m128i a, __m128i b) => Sse41.Max(a.AsUInt32(), b.AsUInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_min_epi8(__m128i a, __m128i b) => Sse41.Min(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_min_epu8(__m128i a, __m128i b) => Sse2.Min(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_min_epi16(__m128i a, __m128i b) => Sse2.Min(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_min_epu16(__m128i a, __m128i b) => Sse41.Min(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_min_epi32(__m128i a, __m128i b) => Sse41.Min(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_min_epu32(__m128i a, __m128i b) => Sse41.Min(a.AsUInt32(), b.AsUInt32()).AsInt64();

    // Logic
    [MethodImpl(AI)] internal static __m128i _mm_and_si128(__m128i a, __m128i b) => Sse2.And(a, b);
    /// <summary>(~a) &amp; b, like the instruction (note: Vector128.AndNot is a &amp; ~b).</summary>
    [MethodImpl(AI)] internal static __m128i _mm_andnot_si128(__m128i a, __m128i b) => Sse2.AndNot(a, b);
    [MethodImpl(AI)] internal static __m128i _mm_or_si128(__m128i a, __m128i b) => Sse2.Or(a, b);
    [MethodImpl(AI)] internal static __m128i _mm_xor_si128(__m128i a, __m128i b) => Sse2.Xor(a, b);
    [MethodImpl(AI)] internal static int _mm_test_all_zeros(__m128i mask, __m128i a) => Sse41.TestZ(mask, a) ? 1 : 0;
    [MethodImpl(AI)] internal static int _mm_testz_si128(__m128i a, __m128i b) => Sse41.TestZ(a, b) ? 1 : 0;
    [MethodImpl(AI)] internal static int _mm_movemask_epi8(__m128i a) => Sse2.MoveMask(a.AsByte());

    // Compare
    [MethodImpl(AI)] internal static __m128i _mm_cmpeq_epi8(__m128i a, __m128i b) => Sse2.CompareEqual(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cmpeq_epi16(__m128i a, __m128i b) => Sse2.CompareEqual(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cmpeq_epi32(__m128i a, __m128i b) => Sse2.CompareEqual(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cmpeq_epi64(__m128i a, __m128i b) => Sse41.CompareEqual(a, b);
    [MethodImpl(AI)] internal static __m128i _mm_cmpgt_epi8(__m128i a, __m128i b) => Sse2.CompareGreaterThan(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cmpgt_epi16(__m128i a, __m128i b) => Sse2.CompareGreaterThan(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cmpgt_epi32(__m128i a, __m128i b) => Sse2.CompareGreaterThan(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cmpgt_epi64(__m128i a, __m128i b) => Sse42.CompareGreaterThan(a, b);
    [MethodImpl(AI)] internal static __m128i _mm_cmplt_epi8(__m128i a, __m128i b) => Sse2.CompareLessThan(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cmplt_epi16(__m128i a, __m128i b) => Sse2.CompareLessThan(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cmplt_epi32(__m128i a, __m128i b) => Sse2.CompareLessThan(a.AsInt32(), b.AsInt32()).AsInt64();

    // Shifts. Counts above the lane width give 0 (or all sign bits), like the instructions.
    [MethodImpl(AI)] internal static __m128i _mm_slli_epi16(__m128i a, int imm) => Sse2.ShiftLeftLogical(a.AsInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_slli_epi32(__m128i a, int imm) => Sse2.ShiftLeftLogical(a.AsInt32(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_slli_epi64(__m128i a, int imm) => Sse2.ShiftLeftLogical(a, (byte)imm);
    [MethodImpl(AI)] internal static __m128i _mm_srli_epi16(__m128i a, int imm) => Sse2.ShiftRightLogical(a.AsUInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_srli_epi32(__m128i a, int imm) => Sse2.ShiftRightLogical(a.AsUInt32(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_srli_epi64(__m128i a, int imm) => Sse2.ShiftRightLogical(a.AsUInt64(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_srai_epi16(__m128i a, int imm) => Sse2.ShiftRightArithmetic(a.AsInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_srai_epi32(__m128i a, int imm) => Sse2.ShiftRightArithmetic(a.AsInt32(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sll_epi16(__m128i a, __m128i count) => Sse2.ShiftLeftLogical(a.AsInt16(), count.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sll_epi32(__m128i a, __m128i count) => Sse2.ShiftLeftLogical(a.AsInt32(), count.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sll_epi64(__m128i a, __m128i count) => Sse2.ShiftLeftLogical(a, count);
    [MethodImpl(AI)] internal static __m128i _mm_srl_epi16(__m128i a, __m128i count) => Sse2.ShiftRightLogical(a.AsUInt16(), count.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_srl_epi32(__m128i a, __m128i count) => Sse2.ShiftRightLogical(a.AsUInt32(), count.AsUInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_srl_epi64(__m128i a, __m128i count) => Sse2.ShiftRightLogical(a.AsUInt64(), count.AsUInt64()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sra_epi16(__m128i a, __m128i count) => Sse2.ShiftRightArithmetic(a.AsInt16(), count.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sra_epi32(__m128i a, __m128i count) => Sse2.ShiftRightArithmetic(a.AsInt32(), count.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_sllv_epi32(__m128i a, __m128i count) => Avx2.ShiftLeftLogicalVariable(a.AsInt32(), count.AsUInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_srlv_epi32(__m128i a, __m128i count) => Avx2.ShiftRightLogicalVariable(a.AsUInt32(), count.AsUInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_srav_epi32(__m128i a, __m128i count) => Avx2.ShiftRightArithmeticVariable(a.AsInt32(), count.AsUInt32()).AsInt64();
    /// <summary>Byte shift of the whole register (pslldq).</summary>
    [MethodImpl(AI)] internal static __m128i _mm_slli_si128(__m128i a, int imm) => Sse2.ShiftLeftLogical128BitLane(a.AsByte(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_bslli_si128(__m128i a, int imm) => _mm_slli_si128(a, imm);
    /// <summary>Byte shift of the whole register (psrldq).</summary>
    [MethodImpl(AI)] internal static __m128i _mm_srli_si128(__m128i a, int imm) => Sse2.ShiftRightLogical128BitLane(a.AsByte(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_bsrli_si128(__m128i a, int imm) => _mm_srli_si128(a, imm);
    /// <summary>palignr: the 32-byte concatenation a:b (a high) shifted right by imm bytes.</summary>
    [MethodImpl(AI)] internal static __m128i _mm_alignr_epi8(__m128i a, __m128i b, int imm) => Ssse3.AlignRight(a.AsByte(), b.AsByte(), (byte)imm).AsInt64();

    // Pack / unpack / widen
    [MethodImpl(AI)] internal static __m128i _mm_packs_epi16(__m128i a, __m128i b) => Sse2.PackSignedSaturate(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_packs_epi32(__m128i a, __m128i b) => Sse2.PackSignedSaturate(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_packus_epi16(__m128i a, __m128i b) => Sse2.PackUnsignedSaturate(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_packus_epi32(__m128i a, __m128i b) => Sse41.PackUnsignedSaturate(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_unpacklo_epi8(__m128i a, __m128i b) => Sse2.UnpackLow(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_unpacklo_epi16(__m128i a, __m128i b) => Sse2.UnpackLow(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_unpacklo_epi32(__m128i a, __m128i b) => Sse2.UnpackLow(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_unpacklo_epi64(__m128i a, __m128i b) => Sse2.UnpackLow(a, b);
    [MethodImpl(AI)] internal static __m128i _mm_unpackhi_epi8(__m128i a, __m128i b) => Sse2.UnpackHigh(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_unpackhi_epi16(__m128i a, __m128i b) => Sse2.UnpackHigh(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_unpackhi_epi32(__m128i a, __m128i b) => Sse2.UnpackHigh(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_unpackhi_epi64(__m128i a, __m128i b) => Sse2.UnpackHigh(a, b);
    [MethodImpl(AI)] internal static __m128i _mm_cvtepu8_epi16(__m128i a) => Sse41.ConvertToVector128Int16(a.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cvtepi8_epi16(__m128i a) => Sse41.ConvertToVector128Int16(a.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cvtepu8_epi32(__m128i a) => Sse41.ConvertToVector128Int32(a.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cvtepi16_epi32(__m128i a) => Sse41.ConvertToVector128Int32(a.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cvtepu16_epi32(__m128i a) => Sse41.ConvertToVector128Int32(a.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_cvtepi32_epi64(__m128i a) => Sse41.ConvertToVector128Int64(a.AsInt32());
    [MethodImpl(AI)] internal static __m128i _mm_cvtepu32_epi64(__m128i a) => Sse41.ConvertToVector128Int64(a.AsUInt32());

    // Shuffle / blend / insert / extract
    [MethodImpl(AI)] internal static __m128i _mm_shuffle_epi32(__m128i a, int imm) => Sse2.Shuffle(a.AsInt32(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_shufflelo_epi16(__m128i a, int imm) => Sse2.ShuffleLow(a.AsInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_shufflehi_epi16(__m128i a, int imm) => Sse2.ShuffleHigh(a.AsInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_shuffle_epi8(__m128i a, __m128i b) => Ssse3.Shuffle(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128 _mm_shuffle_ps(__m128 a, __m128 b, int imm) => Sse.Shuffle(a, b, (byte)imm);
    [MethodImpl(AI)] internal static __m128d _mm_shuffle_pd(__m128d a, __m128d b, int imm) => Sse2.Shuffle(a, b, (byte)imm);
    [MethodImpl(AI)] internal static __m128i _mm_blendv_epi8(__m128i a, __m128i b, __m128i mask) => Sse41.BlendVariable(a.AsByte(), b.AsByte(), mask.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_blend_epi16(__m128i a, __m128i b, int imm) => Sse41.Blend(a.AsInt16(), b.AsInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_blend_epi32(__m128i a, __m128i b, int imm) => Avx2.Blend(a.AsInt32(), b.AsInt32(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_insert_epi8(__m128i a, int i, int imm) => Sse41.Insert(a.AsByte(), (byte)i, (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_insert_epi16(__m128i a, int i, int imm) => Sse2.Insert(a.AsInt16(), (short)i, (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_insert_epi32(__m128i a, int i, int imm) => Sse41.Insert(a.AsInt32(), i, (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_insert_epi64(__m128i a, long i, int imm) => Sse41.X64.Insert(a, i, (byte)imm);
    /// <summary>Zero-extended, like pextrb.</summary>
    [MethodImpl(AI)] internal static int _mm_extract_epi8(__m128i a, int imm) => Sse41.Extract(a.AsByte(), (byte)imm);
    /// <summary>Zero-extended, like pextrw.</summary>
    [MethodImpl(AI)] internal static int _mm_extract_epi16(__m128i a, int imm) => Sse2.Extract(a.AsUInt16(), (byte)imm);
    [MethodImpl(AI)] internal static int _mm_extract_epi32(__m128i a, int imm) => Sse41.Extract(a.AsInt32(), (byte)imm);
    [MethodImpl(AI)] internal static long _mm_extract_epi64(__m128i a, int imm) => Sse41.X64.Extract(a, (byte)imm);
    [MethodImpl(AI)] internal static __m128i _mm_broadcastb_epi8(__m128i a) => Avx2.BroadcastScalarToVector128(a.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_broadcastw_epi16(__m128i a) => Avx2.BroadcastScalarToVector128(a.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_broadcastd_epi32(__m128i a) => Avx2.BroadcastScalarToVector128(a.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm_broadcastq_epi64(__m128i a) => Avx2.BroadcastScalarToVector128(a);

    // ------------------------------------------------------------------------------------------
    // 256-bit integer
    // ------------------------------------------------------------------------------------------

    [MethodImpl(AI)] internal static void _mm256_zeroupper() { }
    [MethodImpl(AI)] internal static __m256i _mm256_setzero_si256() => Vector256<long>.Zero;
    [MethodImpl(AI)] internal static __m256i _mm256_set1_epi8(int a) => Vector256.Create((sbyte)a).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_set1_epi16(int a) => Vector256.Create((short)a).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_set1_epi32(int a) => Vector256.Create(a).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_set1_epi64x(long a) => Vector256.Create(a);
    [MethodImpl(AI)] internal static __m256i _mm256_set_epi64x(long e3, long e2, long e1, long e0) => Vector256.Create(e0, e1, e2, e3);
    [MethodImpl(AI)] internal static __m256i _mm256_setr_epi64x(long e0, long e1, long e2, long e3) => Vector256.Create(e0, e1, e2, e3);
    [MethodImpl(AI)] internal static __m256i _mm256_set_epi32(int e7, int e6, int e5, int e4, int e3, int e2, int e1, int e0) =>
        Vector256.Create(e0, e1, e2, e3, e4, e5, e6, e7).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_setr_epi32(int e0, int e1, int e2, int e3, int e4, int e5, int e6, int e7) =>
        Vector256.Create(e0, e1, e2, e3, e4, e5, e6, e7).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_set_epi16(int e15, int e14, int e13, int e12, int e11, int e10, int e9, int e8,
                                                              int e7, int e6, int e5, int e4, int e3, int e2, int e1, int e0) =>
        Vector256.Create((short)e0, (short)e1, (short)e2, (short)e3, (short)e4, (short)e5, (short)e6, (short)e7,
                         (short)e8, (short)e9, (short)e10, (short)e11, (short)e12, (short)e13, (short)e14, (short)e15).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_setr_epi16(int e0, int e1, int e2, int e3, int e4, int e5, int e6, int e7,
                                                               int e8, int e9, int e10, int e11, int e12, int e13, int e14, int e15) =>
        Vector256.Create((short)e0, (short)e1, (short)e2, (short)e3, (short)e4, (short)e5, (short)e6, (short)e7,
                         (short)e8, (short)e9, (short)e10, (short)e11, (short)e12, (short)e13, (short)e14, (short)e15).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_set_epi8(
        int e31, int e30, int e29, int e28, int e27, int e26, int e25, int e24, int e23, int e22, int e21, int e20, int e19, int e18, int e17, int e16,
        int e15, int e14, int e13, int e12, int e11, int e10, int e9, int e8, int e7, int e6, int e5, int e4, int e3, int e2, int e1, int e0) =>
        _mm256_setr_epi8(e0, e1, e2, e3, e4, e5, e6, e7, e8, e9, e10, e11, e12, e13, e14, e15,
                         e16, e17, e18, e19, e20, e21, e22, e23, e24, e25, e26, e27, e28, e29, e30, e31);
    [MethodImpl(AI)] internal static __m256i _mm256_setr_epi8(
        int e0, int e1, int e2, int e3, int e4, int e5, int e6, int e7, int e8, int e9, int e10, int e11, int e12, int e13, int e14, int e15,
        int e16, int e17, int e18, int e19, int e20, int e21, int e22, int e23, int e24, int e25, int e26, int e27, int e28, int e29, int e30, int e31) =>
        Vector256.Create((sbyte)e0, (sbyte)e1, (sbyte)e2, (sbyte)e3, (sbyte)e4, (sbyte)e5, (sbyte)e6, (sbyte)e7,
                         (sbyte)e8, (sbyte)e9, (sbyte)e10, (sbyte)e11, (sbyte)e12, (sbyte)e13, (sbyte)e14, (sbyte)e15,
                         (sbyte)e16, (sbyte)e17, (sbyte)e18, (sbyte)e19, (sbyte)e20, (sbyte)e21, (sbyte)e22, (sbyte)e23,
                         (sbyte)e24, (sbyte)e25, (sbyte)e26, (sbyte)e27, (sbyte)e28, (sbyte)e29, (sbyte)e30, (sbyte)e31).AsInt64();
    /// <summary>hi in the upper 128 bits, lo in the lower.</summary>
    [MethodImpl(AI)] internal static __m256i _mm256_set_m128i(__m128i hi, __m128i lo) => Vector256.Create(lo, hi);
    [MethodImpl(AI)] internal static __m256i _mm256_setr_m128i(__m128i lo, __m128i hi) => Vector256.Create(lo, hi);
    [MethodImpl(AI)] internal static __m256d _mm256_set_m128d(__m128d hi, __m128d lo) => Vector256.Create(lo, hi);

    [MethodImpl(AI)] internal static __m256 _mm256_castsi256_ps(__m256i a) => a.AsSingle();
    [MethodImpl(AI)] internal static __m256d _mm256_castsi256_pd(__m256i a) => a.AsDouble();
    [MethodImpl(AI)] internal static __m256i _mm256_castps_si256(__m256 a) => a.AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_castpd_si256(__m256d a) => a.AsInt64();
    [MethodImpl(AI)] internal static __m128i _mm256_castsi256_si128(__m256i a) => a.GetLower();
    /// <summary>The upper half is zeroed (C leaves it undefined).</summary>
    [MethodImpl(AI)] internal static __m256i _mm256_castsi128_si256(__m128i a) => a.ToVector256();
    [MethodImpl(AI)] internal static __m256i _mm256_zextsi128_si256(__m128i a) => a.ToVector256();

    // Arithmetic
    [MethodImpl(AI)] internal static __m256i _mm256_add_epi8(__m256i a, __m256i b) => Avx2.Add(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_add_epi16(__m256i a, __m256i b) => Avx2.Add(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_add_epi32(__m256i a, __m256i b) => Avx2.Add(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_add_epi64(__m256i a, __m256i b) => Avx2.Add(a, b);
    [MethodImpl(AI)] internal static __m256i _mm256_adds_epi8(__m256i a, __m256i b) => Avx2.AddSaturate(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_adds_epi16(__m256i a, __m256i b) => Avx2.AddSaturate(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_adds_epu8(__m256i a, __m256i b) => Avx2.AddSaturate(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_adds_epu16(__m256i a, __m256i b) => Avx2.AddSaturate(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sub_epi8(__m256i a, __m256i b) => Avx2.Subtract(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sub_epi16(__m256i a, __m256i b) => Avx2.Subtract(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sub_epi32(__m256i a, __m256i b) => Avx2.Subtract(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sub_epi64(__m256i a, __m256i b) => Avx2.Subtract(a, b);
    [MethodImpl(AI)] internal static __m256i _mm256_subs_epi8(__m256i a, __m256i b) => Avx2.SubtractSaturate(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_subs_epi16(__m256i a, __m256i b) => Avx2.SubtractSaturate(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_subs_epu8(__m256i a, __m256i b) => Avx2.SubtractSaturate(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_subs_epu16(__m256i a, __m256i b) => Avx2.SubtractSaturate(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_avg_epu8(__m256i a, __m256i b) => Avx2.Average(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_avg_epu16(__m256i a, __m256i b) => Avx2.Average(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_madd_epi16(__m256i a, __m256i b) => Avx2.MultiplyAddAdjacent(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_maddubs_epi16(__m256i a, __m256i b) => Avx2.MultiplyAddAdjacent(a.AsByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_mulhi_epi16(__m256i a, __m256i b) => Avx2.MultiplyHigh(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_mulhi_epu16(__m256i a, __m256i b) => Avx2.MultiplyHigh(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_mulhrs_epi16(__m256i a, __m256i b) => Avx2.MultiplyHighRoundScale(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_mullo_epi16(__m256i a, __m256i b) => Avx2.MultiplyLow(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_mullo_epi32(__m256i a, __m256i b) => Avx2.MultiplyLow(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_mul_epu32(__m256i a, __m256i b) => Avx2.Multiply(a.AsUInt32(), b.AsUInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_mul_epi32(__m256i a, __m256i b) => Avx2.Multiply(a.AsInt32(), b.AsInt32());
    [MethodImpl(AI)] internal static __m256i _mm256_sad_epu8(__m256i a, __m256i b) => Avx2.SumAbsoluteDifferences(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_hadd_epi16(__m256i a, __m256i b) => Avx2.HorizontalAdd(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_hadd_epi32(__m256i a, __m256i b) => Avx2.HorizontalAdd(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_hadds_epi16(__m256i a, __m256i b) => Avx2.HorizontalAddSaturate(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_abs_epi8(__m256i a) => Avx2.Abs(a.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_abs_epi16(__m256i a) => Avx2.Abs(a.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_abs_epi32(__m256i a) => Avx2.Abs(a.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sign_epi8(__m256i a, __m256i b) => Avx2.Sign(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sign_epi16(__m256i a, __m256i b) => Avx2.Sign(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sign_epi32(__m256i a, __m256i b) => Avx2.Sign(a.AsInt32(), b.AsInt32()).AsInt64();

    // Min / max
    [MethodImpl(AI)] internal static __m256i _mm256_max_epi8(__m256i a, __m256i b) => Avx2.Max(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_max_epu8(__m256i a, __m256i b) => Avx2.Max(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_max_epi16(__m256i a, __m256i b) => Avx2.Max(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_max_epu16(__m256i a, __m256i b) => Avx2.Max(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_max_epi32(__m256i a, __m256i b) => Avx2.Max(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_max_epu32(__m256i a, __m256i b) => Avx2.Max(a.AsUInt32(), b.AsUInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_min_epi8(__m256i a, __m256i b) => Avx2.Min(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_min_epu8(__m256i a, __m256i b) => Avx2.Min(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_min_epi16(__m256i a, __m256i b) => Avx2.Min(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_min_epu16(__m256i a, __m256i b) => Avx2.Min(a.AsUInt16(), b.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_min_epi32(__m256i a, __m256i b) => Avx2.Min(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_min_epu32(__m256i a, __m256i b) => Avx2.Min(a.AsUInt32(), b.AsUInt32()).AsInt64();

    // Logic
    [MethodImpl(AI)] internal static __m256i _mm256_and_si256(__m256i a, __m256i b) => Avx2.And(a, b);
    /// <summary>(~a) &amp; b, like the instruction (note: Vector256.AndNot is a &amp; ~b).</summary>
    [MethodImpl(AI)] internal static __m256i _mm256_andnot_si256(__m256i a, __m256i b) => Avx2.AndNot(a, b);
    [MethodImpl(AI)] internal static __m256i _mm256_or_si256(__m256i a, __m256i b) => Avx2.Or(a, b);
    [MethodImpl(AI)] internal static __m256i _mm256_xor_si256(__m256i a, __m256i b) => Avx2.Xor(a, b);
    [MethodImpl(AI)] internal static int _mm256_testz_si256(__m256i a, __m256i b) => Avx.TestZ(a, b) ? 1 : 0;
    [MethodImpl(AI)] internal static int _mm256_movemask_epi8(__m256i a) => Avx2.MoveMask(a.AsByte());

    // Compare
    [MethodImpl(AI)] internal static __m256i _mm256_cmpeq_epi8(__m256i a, __m256i b) => Avx2.CompareEqual(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_cmpeq_epi16(__m256i a, __m256i b) => Avx2.CompareEqual(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_cmpeq_epi32(__m256i a, __m256i b) => Avx2.CompareEqual(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_cmpeq_epi64(__m256i a, __m256i b) => Avx2.CompareEqual(a, b);
    [MethodImpl(AI)] internal static __m256i _mm256_cmpgt_epi8(__m256i a, __m256i b) => Avx2.CompareGreaterThan(a.AsSByte(), b.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_cmpgt_epi16(__m256i a, __m256i b) => Avx2.CompareGreaterThan(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_cmpgt_epi32(__m256i a, __m256i b) => Avx2.CompareGreaterThan(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_cmpgt_epi64(__m256i a, __m256i b) => Avx2.CompareGreaterThan(a, b);

    // Shifts (each 128-bit lane separately for the byte shifts, like the instructions)
    [MethodImpl(AI)] internal static __m256i _mm256_slli_epi16(__m256i a, int imm) => Avx2.ShiftLeftLogical(a.AsInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_slli_epi32(__m256i a, int imm) => Avx2.ShiftLeftLogical(a.AsInt32(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_slli_epi64(__m256i a, int imm) => Avx2.ShiftLeftLogical(a, (byte)imm);
    [MethodImpl(AI)] internal static __m256i _mm256_srli_epi16(__m256i a, int imm) => Avx2.ShiftRightLogical(a.AsUInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_srli_epi32(__m256i a, int imm) => Avx2.ShiftRightLogical(a.AsUInt32(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_srli_epi64(__m256i a, int imm) => Avx2.ShiftRightLogical(a.AsUInt64(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_srai_epi16(__m256i a, int imm) => Avx2.ShiftRightArithmetic(a.AsInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_srai_epi32(__m256i a, int imm) => Avx2.ShiftRightArithmetic(a.AsInt32(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sll_epi16(__m256i a, __m128i count) => Avx2.ShiftLeftLogical(a.AsInt16(), count.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sll_epi32(__m256i a, __m128i count) => Avx2.ShiftLeftLogical(a.AsInt32(), count.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sll_epi64(__m256i a, __m128i count) => Avx2.ShiftLeftLogical(a, count);
    [MethodImpl(AI)] internal static __m256i _mm256_srl_epi16(__m256i a, __m128i count) => Avx2.ShiftRightLogical(a.AsUInt16(), count.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_srl_epi32(__m256i a, __m128i count) => Avx2.ShiftRightLogical(a.AsUInt32(), count.AsUInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_srl_epi64(__m256i a, __m128i count) => Avx2.ShiftRightLogical(a.AsUInt64(), count.AsUInt64()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sra_epi16(__m256i a, __m128i count) => Avx2.ShiftRightArithmetic(a.AsInt16(), count.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sra_epi32(__m256i a, __m128i count) => Avx2.ShiftRightArithmetic(a.AsInt32(), count.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sllv_epi32(__m256i a, __m256i count) => Avx2.ShiftLeftLogicalVariable(a.AsInt32(), count.AsUInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_sllv_epi64(__m256i a, __m256i count) => Avx2.ShiftLeftLogicalVariable(a, count.AsUInt64());
    [MethodImpl(AI)] internal static __m256i _mm256_srlv_epi32(__m256i a, __m256i count) => Avx2.ShiftRightLogicalVariable(a.AsUInt32(), count.AsUInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_srlv_epi64(__m256i a, __m256i count) => Avx2.ShiftRightLogicalVariable(a.AsUInt64(), count.AsUInt64()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_srav_epi32(__m256i a, __m256i count) => Avx2.ShiftRightArithmeticVariable(a.AsInt32(), count.AsUInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_slli_si256(__m256i a, int imm) => Avx2.ShiftLeftLogical128BitLane(a.AsByte(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_bslli_epi128(__m256i a, int imm) => _mm256_slli_si256(a, imm);
    [MethodImpl(AI)] internal static __m256i _mm256_srli_si256(__m256i a, int imm) => Avx2.ShiftRightLogical128BitLane(a.AsByte(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_bsrli_epi128(__m256i a, int imm) => _mm256_srli_si256(a, imm);
    /// <summary>vpalignr: per 128-bit lane, a:b (a high) shifted right by imm bytes.</summary>
    [MethodImpl(AI)] internal static __m256i _mm256_alignr_epi8(__m256i a, __m256i b, int imm) => Avx2.AlignRight(a.AsByte(), b.AsByte(), (byte)imm).AsInt64();

    // Pack / unpack / widen (pack and unpack work within each 128-bit lane)
    [MethodImpl(AI)] internal static __m256i _mm256_packs_epi16(__m256i a, __m256i b) => Avx2.PackSignedSaturate(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_packs_epi32(__m256i a, __m256i b) => Avx2.PackSignedSaturate(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_packus_epi16(__m256i a, __m256i b) => Avx2.PackUnsignedSaturate(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_packus_epi32(__m256i a, __m256i b) => Avx2.PackUnsignedSaturate(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_unpacklo_epi8(__m256i a, __m256i b) => Avx2.UnpackLow(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_unpacklo_epi16(__m256i a, __m256i b) => Avx2.UnpackLow(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_unpacklo_epi32(__m256i a, __m256i b) => Avx2.UnpackLow(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_unpacklo_epi64(__m256i a, __m256i b) => Avx2.UnpackLow(a, b);
    [MethodImpl(AI)] internal static __m256i _mm256_unpackhi_epi8(__m256i a, __m256i b) => Avx2.UnpackHigh(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_unpackhi_epi16(__m256i a, __m256i b) => Avx2.UnpackHigh(a.AsInt16(), b.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_unpackhi_epi32(__m256i a, __m256i b) => Avx2.UnpackHigh(a.AsInt32(), b.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_unpackhi_epi64(__m256i a, __m256i b) => Avx2.UnpackHigh(a, b);
    [MethodImpl(AI)] internal static __m256i _mm256_cvtepu8_epi16(__m128i a) => Avx2.ConvertToVector256Int16(a.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_cvtepi8_epi16(__m128i a) => Avx2.ConvertToVector256Int16(a.AsSByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_cvtepu8_epi32(__m128i a) => Avx2.ConvertToVector256Int32(a.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_cvtepi16_epi32(__m128i a) => Avx2.ConvertToVector256Int32(a.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_cvtepu16_epi32(__m128i a) => Avx2.ConvertToVector256Int32(a.AsUInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_cvtepi32_epi64(__m128i a) => Avx2.ConvertToVector256Int64(a.AsInt32());
    [MethodImpl(AI)] internal static __m256i _mm256_cvtepu32_epi64(__m128i a) => Avx2.ConvertToVector256Int64(a.AsUInt32());

    // Shuffle / permute / blend / broadcast / insert / extract
    [MethodImpl(AI)] internal static __m256i _mm256_shuffle_epi32(__m256i a, int imm) => Avx2.Shuffle(a.AsInt32(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_shufflelo_epi16(__m256i a, int imm) => Avx2.ShuffleLow(a.AsInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_shufflehi_epi16(__m256i a, int imm) => Avx2.ShuffleHigh(a.AsInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_shuffle_epi8(__m256i a, __m256i b) => Avx2.Shuffle(a.AsByte(), b.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256 _mm256_shuffle_ps(__m256 a, __m256 b, int imm) => Avx.Shuffle(a, b, (byte)imm);
    [MethodImpl(AI)] internal static __m256d _mm256_shuffle_pd(__m256d a, __m256d b, int imm) => Avx.Shuffle(a, b, (byte)imm);
    [MethodImpl(AI)] internal static __m256i _mm256_permute4x64_epi64(__m256i a, int imm) => Avx2.Permute4x64(a, (byte)imm);
    [MethodImpl(AI)] internal static __m256i _mm256_permutevar8x32_epi32(__m256i a, __m256i idx) => Avx2.PermuteVar8x32(a.AsInt32(), idx.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_permute2x128_si256(__m256i a, __m256i b, int imm) => Avx2.Permute2x128(a, b, (byte)imm);
    [MethodImpl(AI)] internal static __m256i _mm256_permute2f128_si256(__m256i a, __m256i b, int imm) => Avx.Permute2x128(a, b, (byte)imm);
    [MethodImpl(AI)] internal static __m256i _mm256_blend_epi16(__m256i a, __m256i b, int imm) => Avx2.Blend(a.AsInt16(), b.AsInt16(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_blend_epi32(__m256i a, __m256i b, int imm) => Avx2.Blend(a.AsInt32(), b.AsInt32(), (byte)imm).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_blendv_epi8(__m256i a, __m256i b, __m256i mask) => Avx2.BlendVariable(a.AsByte(), b.AsByte(), mask.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_broadcastb_epi8(__m128i a) => Avx2.BroadcastScalarToVector256(a.AsByte()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_broadcastw_epi16(__m128i a) => Avx2.BroadcastScalarToVector256(a.AsInt16()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_broadcastd_epi32(__m128i a) => Avx2.BroadcastScalarToVector256(a.AsInt32()).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_broadcastq_epi64(__m128i a) => Avx2.BroadcastScalarToVector256(a);
    [MethodImpl(AI)] internal static __m256i _mm256_broadcastsi128_si256(__m128i a) => Vector256.Create(a, a);
    [MethodImpl(AI)] internal static __m128i _mm256_extracti128_si256(__m256i a, int imm) => Avx2.ExtractVector128(a, (byte)imm);
    [MethodImpl(AI)] internal static __m128i _mm256_extractf128_si256(__m256i a, int imm) => Avx.ExtractVector128(a, (byte)imm);
    [MethodImpl(AI)] internal static __m256i _mm256_inserti128_si256(__m256i a, __m128i b, int imm) => Avx2.InsertVector128(a, b, (byte)imm);
    [MethodImpl(AI)] internal static __m256i _mm256_insertf128_si256(__m256i a, __m128i b, int imm) => Avx.InsertVector128(a, b, (byte)imm);
    /// <summary>Zero-extended, like gcc/clang/MSVC.</summary>
    [MethodImpl(AI)] internal static int _mm256_extract_epi8(__m256i a, int imm) => a.AsByte().GetElement(imm & 31);
    /// <summary>Zero-extended, like gcc/clang/MSVC.</summary>
    [MethodImpl(AI)] internal static int _mm256_extract_epi16(__m256i a, int imm) => a.AsUInt16().GetElement(imm & 15);
    [MethodImpl(AI)] internal static int _mm256_extract_epi32(__m256i a, int imm) => a.AsInt32().GetElement(imm & 7);
    [MethodImpl(AI)] internal static long _mm256_extract_epi64(__m256i a, int imm) => a.GetElement(imm & 3);
    [MethodImpl(AI)] internal static __m256i _mm256_insert_epi16(__m256i a, int i, int imm) => a.AsInt16().WithElement(imm & 15, (short)i).AsInt64();
    [MethodImpl(AI)] internal static __m256i _mm256_insert_epi32(__m256i a, int i, int imm) => a.AsInt32().WithElement(imm & 7, i).AsInt64();
}
