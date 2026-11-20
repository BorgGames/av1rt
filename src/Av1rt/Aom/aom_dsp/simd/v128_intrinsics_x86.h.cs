// Port of libaom aom_dsp/simd/v128_intrinsics_x86.h (the parts av1/common/cdef_block_simd.h uses)

global using v128 = System.Runtime.Intrinsics.Vector128<long>;
global using static Av1rt.Aom.v128_intrinsics_x86;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Av1rt.Aom;

// [port] `typedef __m128i v128;`. SIMD_INLINE functions are aggressively inlined; `void *p`
// parameters are generic Ptr<T> (or spans): 16-byte loads / stores whatever the element type.
// Where the C header has `#if defined(__SSSE3__)` / `__SSE4_1__` alternatives, the SSSE3 /
// SSE4.1 branch is ported (an AVX2 compilation unit has them; the alternatives compute the
// same values). The `#define` macros taking immediates are inlined functions.
// As in v64_intrinsics_x86.h.cs, each function calls System.Runtime.Intrinsics directly (the
// C body is in the comment above it) to keep one inlining level per operation.
// Only the functions the ported SIMD code calls are here; add more from the C header as needed.
internal static class v128_intrinsics_x86
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    // (uint32_t)_mm_cvtsi128_si32(a)
    [MethodImpl(AI)] internal static uint v128_low_u32(v128 a) => a.AsUInt32().ToScalar();

    // _mm_unpacklo_epi64(a, v64_zero())
    [MethodImpl(AI)] internal static v64 v128_low_v64(v128 a) => Sse2.MoveScalar(a);  // movq: same result

    // _mm_srli_si128(a, 8)
    [MethodImpl(AI)] internal static v64 v128_high_v64(v128 a) => Sse2.ShiftRightLogical128BitLane(a.AsByte(), 8).AsInt64();

    // _mm_unpacklo_epi64(b, a)
    [MethodImpl(AI)] internal static v128 v128_from_v64(v64 a, v64 b) => Sse2.UnpackLow(b, a);

    // _mm_set_epi32((int)a, (int)b, (int)c, (int)d)
    [MethodImpl(AI)] internal static v128 v128_from_32(uint a, uint b, uint c, uint d) =>
        Vector128.Create((int)d, (int)c, (int)b, (int)a).AsInt64();

    // _mm_load_si128((__m128i *)p)
    [MethodImpl(AI)] internal static v128 v128_load_aligned<T>(Ptr<T> p) where T : unmanaged =>
        Vector128.Create<T>(p.Array, p.Offset).AsInt64();

    // _mm_lddqu_si128((__m128i *)p)
    [MethodImpl(AI)] internal static v128 v128_load_unaligned<T>(Ptr<T> p) where T : unmanaged =>
        Vector128.Create<T>(p.Array, p.Offset).AsInt64();

    // _mm_storeu_si128((__m128i *)p, a)
    [MethodImpl(AI)] internal static void v128_store_unaligned<T>(Ptr<T> p, v128 a) where T : unmanaged =>
        a.As<long, T>().CopyTo(p.Array, p.Offset);
    [MethodImpl(AI)] internal static void v128_store_unaligned<T>(Span<T> p, v128 a) where T : unmanaged =>
        a.As<long, T>().CopyTo(p);

    // The following function requires an immediate.
    // c ? _mm_alignr_epi8(a, b, c) : b
    [MethodImpl(AI)] internal static v128 v128_align(v128 a, v128 b, uint c) =>
        c != 0 ? Ssse3.AlignRight(a.AsByte(), b.AsByte(), (byte)c).AsInt64() : b;

    // _mm_setzero_si128()
    [MethodImpl(AI)] internal static v128 v128_zero() => Vector128<long>.Zero;

    // _mm_set1_epi16((short)x)
    [MethodImpl(AI)] internal static v128 v128_dup_16(ushort x) => Vector128.Create((short)x).AsInt64();

    // _mm_set1_epi32((int)x)
    [MethodImpl(AI)] internal static v128 v128_dup_32(uint x) => Vector128.Create((int)x).AsInt64();

    // _mm_add_epi16(a, b)
    [MethodImpl(AI)] internal static v128 v128_add_16(v128 a, v128 b) => Sse2.Add(a.AsInt16(), b.AsInt16()).AsInt64();

    // _mm_add_epi32(a, b)
    [MethodImpl(AI)] internal static v128 v128_add_32(v128 a, v128 b) => Sse2.Add(a.AsInt32(), b.AsInt32()).AsInt64();

    // _mm_sub_epi16(a, b)
    [MethodImpl(AI)] internal static v128 v128_sub_16(v128 a, v128 b) => Sse2.Subtract(a.AsInt16(), b.AsInt16()).AsInt64();

    // _mm_unpacklo_epi16(b, a)
    [MethodImpl(AI)] internal static v128 v128_ziplo_16(v128 a, v128 b) => Sse2.UnpackLow(b.AsInt16(), a.AsInt16()).AsInt64();

    // _mm_unpackhi_epi16(b, a)
    [MethodImpl(AI)] internal static v128 v128_ziphi_16(v128 a, v128 b) => Sse2.UnpackHigh(b.AsInt16(), a.AsInt16()).AsInt64();

    // _mm_unpacklo_epi32(b, a)
    [MethodImpl(AI)] internal static v128 v128_ziplo_32(v128 a, v128 b) => Sse2.UnpackLow(b.AsInt32(), a.AsInt32()).AsInt64();

    // _mm_unpackhi_epi32(b, a)
    [MethodImpl(AI)] internal static v128 v128_ziphi_32(v128 a, v128 b) => Sse2.UnpackHigh(b.AsInt32(), a.AsInt32()).AsInt64();

    // _mm_unpacklo_epi64(b, a)
    [MethodImpl(AI)] internal static v128 v128_ziplo_64(v128 a, v128 b) => Sse2.UnpackLow(b, a);

    // _mm_unpackhi_epi64(b, a)
    [MethodImpl(AI)] internal static v128 v128_ziphi_64(v128 a, v128 b) => Sse2.UnpackHigh(b, a);

    // _mm_packs_epi32(b, a)
    [MethodImpl(AI)] internal static v128 v128_pack_s32_s16(v128 a, v128 b) =>
        Sse2.PackSignedSaturate(b.AsInt32(), a.AsInt32()).AsInt64();

    // _mm_packus_epi16(b, a)
    [MethodImpl(AI)] internal static v128 v128_pack_s16_u8(v128 a, v128 b) =>
        Sse2.PackUnsignedSaturate(b.AsInt16(), a.AsInt16()).AsInt64();

    // _mm_packs_epi16(b, a)
    [MethodImpl(AI)] internal static v128 v128_pack_s16_s8(v128 a, v128 b) =>
        Sse2.PackSignedSaturate(b.AsInt16(), a.AsInt16()).AsInt64();

    // _mm_shuffle_epi8(x, pattern)
    [MethodImpl(AI)] internal static v128 v128_shuffle_8(v128 x, v128 pattern) =>
        Ssse3.Shuffle(x.AsByte(), pattern.AsByte()).AsInt64();

    // _mm_mullo_epi32(a, b)
    [MethodImpl(AI)] internal static v128 v128_mullo_s32(v128 a, v128 b) => Sse41.MultiplyLow(a.AsInt32(), b.AsInt32()).AsInt64();

    // _mm_madd_epi16(a, b)
    [MethodImpl(AI)] internal static v128 v128_madd_s16(v128 a, v128 b) =>
        Sse2.MultiplyAddAdjacent(a.AsInt16(), b.AsInt16()).AsInt64();

    // _mm_movemask_epi8(a)
    [MethodImpl(AI)] internal static uint v128_movemask_8(v128 a) => (uint)Sse2.MoveMask(a.AsByte());

    // _mm_max_epi32(a, b)
    [MethodImpl(AI)] internal static v128 v128_max_s32(v128 a, v128 b) => Sse41.Max(a.AsInt32(), b.AsInt32()).AsInt64();

    // _mm_cmpeq_epi32(a, b)
    [MethodImpl(AI)] internal static v128 v128_cmpeq_32(v128 a, v128 b) => Sse2.CompareEqual(a.AsInt32(), b.AsInt32()).AsInt64();

    // _mm_sra_epi16(a, _mm_cvtsi32_si128((int)c))
    [MethodImpl(AI)] internal static v128 v128_shr_s16(v128 a, uint c) =>
        Sse2.ShiftRightArithmetic(a.AsInt16(), Vector128.CreateScalar((int)c).AsInt16()).AsInt64();

    /* These intrinsics require immediate values, so we must use #defines
       to enforce that. */
    // _mm_slli_si128(a, (c) & 127)
    [MethodImpl(AI)] internal static v128 v128_shl_n_byte(v128 a, int c) =>
        Sse2.ShiftLeftLogical128BitLane(a.AsByte(), (byte)((c) & 127)).AsInt64();
    // _mm_srli_si128(a, (c) & 127)
    [MethodImpl(AI)] internal static v128 v128_shr_n_byte(v128 a, int c) =>
        Sse2.ShiftRightLogical128BitLane(a.AsByte(), (byte)((c) & 127)).AsInt64();
}
