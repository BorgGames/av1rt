// Port of libaom aom_dsp/x86/mem_sse2.h

global using static Av1rt.Aom.mem_sse2;

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Av1rt.Aom;

// [port] `const void *` parameters that are only dereferenced are generic Ptr<T> (the loads read
// 2/4/8 bytes from the pointer whatever its element type, like the C memcpy); the ones that do
// byte arithmetic on the pointer (`(int8_t *)src + byte_stride`) are Ptr<byte>. `__m128i *`
// array parameters are Span<__m128i> (PORTING 9).
internal static class mem_sse2
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    [MethodImpl(AI)] internal static short loadu_int16<T>(Ptr<T> src) where T : unmanaged =>
        BinaryPrimitives.ReadInt16LittleEndian(
            MemoryMarshal.AsBytes(new ReadOnlySpan<T>(src.Array, src.Offset, Vector64<T>.Count / 4)));

    [MethodImpl(AI)] internal static int loadu_int32<T>(Ptr<T> src) where T : unmanaged =>
        BinaryPrimitives.ReadInt32LittleEndian(
            MemoryMarshal.AsBytes(new ReadOnlySpan<T>(src.Array, src.Offset, Vector64<T>.Count / 2)));

    [MethodImpl(AI)] internal static long loadu_int64<T>(Ptr<T> src) where T : unmanaged =>
        BinaryPrimitives.ReadInt64LittleEndian(
            MemoryMarshal.AsBytes(new ReadOnlySpan<T>(src.Array, src.Offset, Vector64<T>.Count)));

    [MethodImpl(AI)] internal static void _mm_storeh_epi64<T>(Ptr<T> d, __m128i s) where T : unmanaged =>
        _mm_storeh_pi(d, _mm_castsi128_ps(s));

    [MethodImpl(AI)] internal static __m128i loadh_epi64<T>(Ptr<T> src, __m128i s) where T : unmanaged =>
        _mm_castps_si128(_mm_loadh_pi(_mm_castsi128_ps(s), src));

    [MethodImpl(AI)]
    internal static __m128i load_8bit_4x4_to_1_reg_sse2(Ptr<byte> src, int byte_stride)
    {
        return _mm_setr_epi32(loadu_int32(src + 0 * byte_stride),
                              loadu_int32(src + 1 * byte_stride),
                              loadu_int32(src + 2 * byte_stride),
                              loadu_int32(src + 3 * byte_stride));
    }

    [MethodImpl(AI)]
    internal static __m128i load_8bit_8x2_to_1_reg_sse2(Ptr<byte> src, int byte_stride)
    {
        __m128i dst;
        dst = _mm_loadl_epi64(src + 0 * byte_stride);
        dst = loadh_epi64(src + 1 * byte_stride, dst);
        return dst;
    }

    [MethodImpl(AI)]
    internal static void store_8bit_8x4_from_16x2(Span<__m128i> s, Ptr<byte> d, int stride)
    {
        _mm_storel_epi64(d + 0 * stride, s[0]);
        _mm_storeh_epi64(d + 1 * stride, s[0]);
        _mm_storel_epi64(d + 2 * stride, s[1]);
        _mm_storeh_epi64(d + 3 * stride, s[1]);
    }

    [MethodImpl(AI)]
    internal static void store_8bit_4x4(Span<__m128i> s, Ptr<byte> d, int stride)
    {
        _mm_storeu_si32(d + 0 * stride, s[0]);  // *(int *)(d + 0 * stride) = _mm_cvtsi128_si32(s[0])
        _mm_storeu_si32(d + 1 * stride, s[1]);
        _mm_storeu_si32(d + 2 * stride, s[2]);
        _mm_storeu_si32(d + 3 * stride, s[3]);
    }

    [MethodImpl(AI)]
    internal static void store_8bit_4x4_sse2(__m128i s, Ptr<byte> d, int stride)
    {
        Span<__m128i> ss = stackalloc __m128i[4];

        ss[0] = s;
        ss[1] = _mm_srli_si128(s, 4);
        ss[2] = _mm_srli_si128(s, 8);
        ss[3] = _mm_srli_si128(s, 12);
        store_8bit_4x4(ss, d, stride);
    }

    [MethodImpl(AI)]
    internal static void load_8bit_4x4(Ptr<byte> s, int stride, Span<__m128i> d)
    {
        d[0] = _mm_cvtsi32_si128(loadu_int32(s + 0 * stride));
        d[1] = _mm_cvtsi32_si128(loadu_int32(s + 1 * stride));
        d[2] = _mm_cvtsi32_si128(loadu_int32(s + 2 * stride));
        d[3] = _mm_cvtsi32_si128(loadu_int32(s + 3 * stride));
    }

    [MethodImpl(AI)]
    internal static void load_8bit_4x8(Ptr<byte> s, int stride, Span<__m128i> d)
    {
        load_8bit_4x4(s + 0 * stride, stride, d.Slice(0));
        load_8bit_4x4(s + 4 * stride, stride, d.Slice(4));
    }

    [MethodImpl(AI)]
    internal static void load_8bit_8x4(Ptr<byte> s, int stride, Span<__m128i> d)
    {
        d[0] = _mm_loadl_epi64(s + 0 * stride);
        d[1] = _mm_loadl_epi64(s + 1 * stride);
        d[2] = _mm_loadl_epi64(s + 2 * stride);
        d[3] = _mm_loadl_epi64(s + 3 * stride);
    }

    [MethodImpl(AI)]
    internal static void loadu_8bit_16x4(Ptr<byte> s, int stride, Span<__m128i> d)
    {
        d[0] = _mm_loadu_si128(s + 0 * stride);
        d[1] = _mm_loadu_si128(s + 1 * stride);
        d[2] = _mm_loadu_si128(s + 2 * stride);
        d[3] = _mm_loadu_si128(s + 3 * stride);
    }

    [MethodImpl(AI)]
    internal static void load_8bit_8x8(Ptr<byte> s, int stride, Span<__m128i> d)
    {
        load_8bit_8x4(s + 0 * stride, stride, d.Slice(0));
        load_8bit_8x4(s + 4 * stride, stride, d.Slice(4));
    }

    [MethodImpl(AI)]
    internal static void load_8bit_16x8(Ptr<byte> s, int stride, Span<__m128i> d)
    {
        d[0] = _mm_load_si128(s + 0 * stride);
        d[1] = _mm_load_si128(s + 1 * stride);
        d[2] = _mm_load_si128(s + 2 * stride);
        d[3] = _mm_load_si128(s + 3 * stride);
        d[4] = _mm_load_si128(s + 4 * stride);
        d[5] = _mm_load_si128(s + 5 * stride);
        d[6] = _mm_load_si128(s + 6 * stride);
        d[7] = _mm_load_si128(s + 7 * stride);
    }

    [MethodImpl(AI)]
    internal static void loadu_8bit_16x8(Ptr<byte> s, int stride, Span<__m128i> d)
    {
        loadu_8bit_16x4(s + 0 * stride, stride, d.Slice(0));
        loadu_8bit_16x4(s + 4 * stride, stride, d.Slice(4));
    }

    [MethodImpl(AI)]
    internal static void store_8bit_8x8(Span<__m128i> s, Ptr<byte> d, int stride)
    {
        _mm_storel_epi64(d + 0 * stride, s[0]);
        _mm_storel_epi64(d + 1 * stride, s[1]);
        _mm_storel_epi64(d + 2 * stride, s[2]);
        _mm_storel_epi64(d + 3 * stride, s[3]);
        _mm_storel_epi64(d + 4 * stride, s[4]);
        _mm_storel_epi64(d + 5 * stride, s[5]);
        _mm_storel_epi64(d + 6 * stride, s[6]);
        _mm_storel_epi64(d + 7 * stride, s[7]);
    }

    [MethodImpl(AI)]
    internal static void storeu_8bit_16x4(Span<__m128i> s, Ptr<byte> d, int stride)
    {
        _mm_storeu_si128(d + 0 * stride, s[0]);
        _mm_storeu_si128(d + 1 * stride, s[1]);
        _mm_storeu_si128(d + 2 * stride, s[2]);
        _mm_storeu_si128(d + 3 * stride, s[3]);
    }
}
