// Intrinsics added by package s4 (Hadamard/SATD, averages, projections, coefficient helpers).

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Av1rt.Aom;

internal static partial class intrin
{
    [MethodImpl(AI)] internal static __m256 _mm256_cvtepi32_ps(__m256i a) => Avx.ConvertToVector256Single(a.AsInt32());

    /// <summary>
    /// vpgatherdd: lane i is the 4 bytes at byte offset <c>vindex[i] * scale</c> from
    /// <paramref name="base_addr"/> (C passes <c>(const int *)p</c> whatever p points to).
    /// </summary>
    /// <remarks>
    /// The instruction needs a raw pointer, so it is emulated with eight bounds-checked scalar
    /// reads against the whole underlying array (negative offsets are allowed, as in C, as long
    /// as they stay inside the array).
    /// </remarks>
    [MethodImpl(AI)] internal static __m256i _mm256_i32gather_epi32<T>(Ptr<T> base_addr, __m256i vindex, int scale) where T : unmanaged
    {
        ReadOnlySpan<byte> b = MemoryMarshal.AsBytes(new ReadOnlySpan<T>(base_addr.Array));
        int o = MemoryMarshal.AsBytes(new ReadOnlySpan<T>(base_addr.Array, 0, base_addr.Offset)).Length;
        Vector256<int> i = vindex.AsInt32();
        return Vector256.Create(
            BinaryPrimitives.ReadInt32LittleEndian(b.Slice(o + i.GetElement(0) * scale, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(b.Slice(o + i.GetElement(1) * scale, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(b.Slice(o + i.GetElement(2) * scale, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(b.Slice(o + i.GetElement(3) * scale, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(b.Slice(o + i.GetElement(4) * scale, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(b.Slice(o + i.GetElement(5) * scale, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(b.Slice(o + i.GetElement(6) * scale, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(b.Slice(o + i.GetElement(7) * scale, 4))).AsInt64();
    }
}
