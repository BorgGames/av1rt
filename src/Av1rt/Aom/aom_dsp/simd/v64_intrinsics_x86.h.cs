// Port of libaom aom_dsp/simd/v64_intrinsics_x86.h (the parts av1/common/cdef_block_simd.h uses)

global using v64 = System.Runtime.Intrinsics.Vector128<long>;
global using static Av1rt.Aom.v64_intrinsics_x86;

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Av1rt.Aom;

// [port] `typedef __m128i v64;` (the low 64 bits are the value). SIMD_INLINE functions are
// aggressively inlined; `const void *p` / `void *p` are generic Ptr<T>: the loads and stores
// read / write 4 or 8 bytes whatever the element type, like the C casts.
// The functions call System.Runtime.Intrinsics directly (the instruction the C intrinsic named
// in the comment compiles to) rather than through the intrin.cs wrappers: one inlining level
// per operation keeps big kernels built from hundreds of them (cdef filter_block_4x4) within
// the JIT's per-method inlining limits, so everything is inlined.
// Only the functions the ported SIMD code calls are here; add more from the C header as needed.
internal static class v64_intrinsics_x86
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    // (uint32_t)_mm_cvtsi128_si32(a)
    [MethodImpl(AI)] internal static uint v64_low_u32(v64 a) => a.AsUInt32().ToScalar();

    // (uint32_t)_mm_cvtsi128_si32(_mm_srli_si128(a, 4))
    [MethodImpl(AI)] internal static uint v64_high_u32(v64 a) => Sse41.Extract(a.AsUInt32(), 1);

    // *((uint32_t *)p) = a
    // [port] Only byte destinations store a u32 in the ported code: one checked 4-byte
    // little-endian store.
    [MethodImpl(AI)] internal static void u32_store_aligned(Ptr<byte> p, uint a) =>
        BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(p.Array, p.Offset, 4), a);

    // _mm_loadl_epi64((__m128i *)p)
    // [port] A bounds-checked 8-byte load into a GPR and movq: the JIT keeps this in registers,
    // where Vector64.ToVector128() went through a stack slot (a store-forwarding stall per load)
    // in filter_block_4x4. The bounds check is the span constructor's single unsigned compare
    // (Vector64.Create(T[], int) does three).
    [MethodImpl(AI)] internal static v64 v64_load_aligned<T>(Ptr<T> p) where T : unmanaged =>
        Vector128.CreateScalar(MemoryMarshal.Read<long>(
            MemoryMarshal.AsBytes(new ReadOnlySpan<T>(p.Array, p.Offset, Vector64<T>.Count))));

    // _mm_loadl_epi64((__m128i *)p)
    [MethodImpl(AI)] internal static v64 v64_load_unaligned<T>(Ptr<T> p) where T : unmanaged =>
        v64_load_aligned(p);

    // _mm_storel_epi64((__m128i *)p, a)
    [MethodImpl(AI)] internal static void v64_store_aligned<T>(Ptr<T> p, v64 a) where T : unmanaged =>
        MemoryMarshal.Write(MemoryMarshal.AsBytes(new Span<T>(p.Array, p.Offset, Vector64<T>.Count)),
                            a.ToScalar());

    // _mm_setzero_si128()
    [MethodImpl(AI)] internal static v64 v64_zero() => Vector128<long>.Zero;
}
