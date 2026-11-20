// Port of libaom aom_dsp/simd/v256_intrinsics_x86.h (the parts av1/common/cdef_block_simd.h uses)

global using v256 = System.Runtime.Intrinsics.Vector256<long>;
global using static Av1rt.Aom.v256_intrinsics_x86;

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Av1rt.Aom;

// [port] The `__AVX2__` half of the header: `typedef __m256i v256;` over v128_intrinsics_x86.h.
// SIMD_INLINE functions are aggressively inlined; the `#define` macros taking immediates are
// inlined functions. As in v64_intrinsics_x86.h.cs, each function calls
// System.Runtime.Intrinsics directly (the C body is in the comment above it) to keep one
// inlining level per operation.
// Only the functions the ported SIMD code calls are here; add more from the C header as needed.
internal static class v256_intrinsics_x86
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    // [port] <xmmintrin.h> _MM_SHUFFLE, private so it cannot clash with another port's copy.
    [MethodImpl(AI)] private static byte _MM_SHUFFLE(int fp3, int fp2, int fp1, int fp0) =>
        (byte)((fp3 << 6) | (fp2 << 4) | (fp1 << 2) | fp0);

    // _mm256_castsi256_si128(a)
    [MethodImpl(AI)] internal static v128 v256_low_v128(v256 a) => a.GetLower();

    // _mm256_extracti128_si256(a, 1)
    [MethodImpl(AI)] internal static v128 v256_high_v128(v256 a) => Avx2.ExtractVector128(a, 1);

    // gcc seems to be missing _mm256_set_m128i()
    // _mm256_inserti128_si256(_mm256_castsi128_si256(b), a, 1)
    [MethodImpl(AI)] internal static v256 v256_from_v128(v128 a, v128 b) => Vector256.Create(b, a);

    // v256_from_v128(v128_from_v64(a, b), v128_from_v64(c, d))
    [MethodImpl(AI)] internal static v256 v256_from_v64(v64 a, v64 b, v64 c, v64 d) =>
        Vector256.Create(Sse2.UnpackLow(d, c), Sse2.UnpackLow(b, a));

    // _mm256_setzero_si256()
    [MethodImpl(AI)] internal static v256 v256_zero() => Vector256<long>.Zero;

    // _mm256_set1_epi16((short)x)
    [MethodImpl(AI)] internal static v256 v256_dup_16(ushort x) => Vector256.Create((short)x).AsInt64();

    // _mm256_add_epi16(a, b)
    [MethodImpl(AI)] internal static v256 v256_add_16(v256 a, v256 b) => Avx2.Add(a.AsInt16(), b.AsInt16()).AsInt64();

    // _mm256_sub_epi16(a, b)
    [MethodImpl(AI)] internal static v256 v256_sub_16(v256 a, v256 b) => Avx2.Subtract(a.AsInt16(), b.AsInt16()).AsInt64();

    // _mm256_subs_epu16(a, b)
    [MethodImpl(AI)] internal static v256 v256_ssub_u16(v256 a, v256 b) =>
        Avx2.SubtractSaturate(a.AsUInt16(), b.AsUInt16()).AsInt64();

    // _mm256_abs_epi16(a)
    [MethodImpl(AI)] internal static v256 v256_abs_s16(v256 a) => Avx2.Abs(a.AsInt16()).AsInt64();

    // _mm256_permute4x64_epi64(_mm256_packus_epi16(b, a), _MM_SHUFFLE(3, 1, 2, 0))
    [MethodImpl(AI)] internal static v256 v256_pack_s16_u8(v256 a, v256 b) =>
        Avx2.Permute4x64(Avx2.PackUnsignedSaturate(b.AsInt16(), a.AsInt16()).AsInt64(), _MM_SHUFFLE(3, 1, 2, 0));

    // _mm256_xor_si256(a, b)
    [MethodImpl(AI)] internal static v256 v256_xor(v256 a, v256 b) => Avx2.Xor(a, b);

    // _mm256_and_si256(a, b)
    [MethodImpl(AI)] internal static v256 v256_and(v256 a, v256 b) => Avx2.And(a, b);

    // _mm256_mullo_epi16(a, b)
    [MethodImpl(AI)] internal static v256 v256_mullo_s16(v256 a, v256 b) =>
        Avx2.MultiplyLow(a.AsInt16(), b.AsInt16()).AsInt64();

    // _mm256_max_epu8(a, b)
    [MethodImpl(AI)] internal static v256 v256_max_u8(v256 a, v256 b) => Avx2.Max(a.AsByte(), b.AsByte()).AsInt64();

    // _mm256_min_epi16(a, b)
    [MethodImpl(AI)] internal static v256 v256_min_s16(v256 a, v256 b) => Avx2.Min(a.AsInt16(), b.AsInt16()).AsInt64();

    // _mm256_max_epi16(a, b)
    [MethodImpl(AI)] internal static v256 v256_max_s16(v256 a, v256 b) => Avx2.Max(a.AsInt16(), b.AsInt16()).AsInt64();

    // _mm256_cmpgt_epi16(b, a)
    [MethodImpl(AI)] internal static v256 v256_cmplt_s16(v256 a, v256 b) =>
        Avx2.CompareGreaterThan(b.AsInt16(), a.AsInt16()).AsInt64();

    // _mm256_srl_epi16(a, _mm_cvtsi32_si128((int)c))
    [MethodImpl(AI)] internal static v256 v256_shr_u16(v256 a, uint c) =>
        Avx2.ShiftRightLogical(a.AsUInt16(), Vector128.CreateScalar((int)c).AsUInt16()).AsInt64();

    // _mm256_srai_epi16(a, c)
    [MethodImpl(AI)] internal static v256 v256_shr_n_s16(v256 a, int c) => Avx2.ShiftRightArithmetic(a.AsInt16(), (byte)c).AsInt64();
}
