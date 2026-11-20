// Port of libaom aom_dsp/x86/synonyms.h and aom_dsp/x86/synonyms_avx2.h

global using static Av1rt.Aom.synonyms;

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Av1rt.Aom;

// [port] `const void *` parameters are generic Ptr<T> (or spans): the loads read 4/8/16/32
// bytes from the pointer whatever its element type, like the C memcpy / casts.
internal static class synonyms
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    // Loads and stores to do away with the tedium of casting the address
    // to the right type.
    [MethodImpl(AI)] internal static __m128i xx_loadl_32<T>(Ptr<T> a) where T : unmanaged => _mm_loadu_si32(a);
    [MethodImpl(AI)] internal static __m128i xx_loadl_32<T>(Span<T> a) where T : unmanaged => _mm_loadu_si32(a);
    [MethodImpl(AI)] internal static __m128i xx_loadl_32<T>(ReadOnlySpan<T> a) where T : unmanaged => _mm_loadu_si32(a);

    [MethodImpl(AI)] internal static __m128i xx_loadl_64<T>(Ptr<T> a) where T : unmanaged => _mm_loadl_epi64(a);
    [MethodImpl(AI)] internal static __m128i xx_loadl_64<T>(Span<T> a) where T : unmanaged => _mm_loadl_epi64(a);
    [MethodImpl(AI)] internal static __m128i xx_loadl_64<T>(ReadOnlySpan<T> a) where T : unmanaged => _mm_loadl_epi64(a);

    [MethodImpl(AI)] internal static __m128i xx_load_128<T>(Ptr<T> a) where T : unmanaged => _mm_load_si128(a);
    [MethodImpl(AI)] internal static __m128i xx_load_128<T>(Span<T> a) where T : unmanaged => _mm_load_si128(a);
    [MethodImpl(AI)] internal static __m128i xx_load_128<T>(ReadOnlySpan<T> a) where T : unmanaged => _mm_load_si128(a);

    [MethodImpl(AI)] internal static __m128i xx_loadu_128<T>(Ptr<T> a) where T : unmanaged => _mm_loadu_si128(a);
    [MethodImpl(AI)] internal static __m128i xx_loadu_128<T>(Span<T> a) where T : unmanaged => _mm_loadu_si128(a);
    [MethodImpl(AI)] internal static __m128i xx_loadu_128<T>(ReadOnlySpan<T> a) where T : unmanaged => _mm_loadu_si128(a);

    // Load 64 bits from each of hi and low, and pack into an SSE register
    // Since directly loading as `int64_t`s and using _mm_set_epi64 may violate
    // the strict aliasing rule, this takes a different approach
    [MethodImpl(AI)] internal static __m128i xx_loadu_2x64<T>(Ptr<T> hi, Ptr<T> lo) where T : unmanaged =>
        _mm_unpacklo_epi64(_mm_loadl_epi64(lo), _mm_loadl_epi64(hi));

    // [port] Only byte and 16-bit element pointers make sense for a 2-byte store.
    [MethodImpl(AI)] internal static void xx_storel_16(Ptr<byte> a, __m128i v) =>
        BinaryPrimitives.WriteUInt16LittleEndian(a.AsSpan(2), (ushort)_mm_cvtsi128_si32(v));
    [MethodImpl(AI)] internal static void xx_storel_16(Ptr<ushort> a, __m128i v) => a[0] = (ushort)_mm_cvtsi128_si32(v);
    [MethodImpl(AI)] internal static void xx_storel_16(Ptr<short> a, __m128i v) => a[0] = (short)_mm_cvtsi128_si32(v);

    [MethodImpl(AI)] internal static void xx_storel_32<T>(Ptr<T> a, __m128i v) where T : unmanaged => _mm_storeu_si32(a, v);
    [MethodImpl(AI)] internal static void xx_storel_32<T>(Span<T> a, __m128i v) where T : unmanaged => _mm_storeu_si32(a, v);

    [MethodImpl(AI)] internal static void xx_storel_64<T>(Ptr<T> a, __m128i v) where T : unmanaged => _mm_storel_epi64(a, v);
    [MethodImpl(AI)] internal static void xx_storel_64<T>(Span<T> a, __m128i v) where T : unmanaged => _mm_storel_epi64(a, v);

    [MethodImpl(AI)] internal static void xx_store_128<T>(Ptr<T> a, __m128i v) where T : unmanaged => _mm_store_si128(a, v);
    [MethodImpl(AI)] internal static void xx_store_128<T>(Span<T> a, __m128i v) where T : unmanaged => _mm_store_si128(a, v);

    [MethodImpl(AI)] internal static void xx_storeu_128<T>(Ptr<T> a, __m128i v) where T : unmanaged => _mm_storeu_si128(a, v);
    [MethodImpl(AI)] internal static void xx_storeu_128<T>(Span<T> a, __m128i v) where T : unmanaged => _mm_storeu_si128(a, v);

    // Fill an SSE register using an interleaved pair of values, ie. set the
    // 8 channels to {a, b, a, b, a, b, a, b}, using the same channel ordering
    // as when a register is stored to / loaded from memory.
    //
    // This is useful for rearranging filter kernels for use with the _mm_madd_epi16
    // instruction
    [MethodImpl(AI)] internal static __m128i xx_set2_epi16(short a, short b) => _mm_setr_epi16(a, b, a, b, a, b, a, b);

    [MethodImpl(AI)] internal static __m128i xx_round_epu16(__m128i v_val_w) => _mm_avg_epu16(v_val_w, _mm_setzero_si128());

    [MethodImpl(AI)] internal static __m128i xx_roundn_epu16(__m128i v_val_w, int bits)
    {
        __m128i v_s_w = _mm_srli_epi16(v_val_w, bits - 1);
        return _mm_avg_epu16(v_s_w, _mm_setzero_si128());
    }

    [MethodImpl(AI)] internal static __m128i xx_roundn_epu32(__m128i v_val_d, int bits)
    {
        __m128i v_bias_d = _mm_set1_epi32((1 << bits) >> 1);
        __m128i v_tmp_d = _mm_add_epi32(v_val_d, v_bias_d);
        return _mm_srli_epi32(v_tmp_d, bits);
    }

    [MethodImpl(AI)] internal static __m128i xx_roundn_epi16_unsigned(__m128i v_val_d, int bits)
    {
        __m128i v_bias_d = _mm_set1_epi16((1 << bits) >> 1);
        __m128i v_tmp_d = _mm_add_epi16(v_val_d, v_bias_d);
        return _mm_srai_epi16(v_tmp_d, bits);
    }

    // This is equivalent to ROUND_POWER_OF_TWO(v_val_d, bits)
    [MethodImpl(AI)] internal static __m128i xx_roundn_epi32_unsigned(__m128i v_val_d, int bits)
    {
        __m128i v_bias_d = _mm_set1_epi32((1 << bits) >> 1);
        __m128i v_tmp_d = _mm_add_epi32(v_val_d, v_bias_d);
        return _mm_srai_epi32(v_tmp_d, bits);
    }

    [MethodImpl(AI)] internal static __m128i xx_roundn_epi16(__m128i v_val_d, int bits)
    {
        __m128i v_bias_d = _mm_set1_epi16((1 << bits) >> 1);
        __m128i v_sign_d = _mm_srai_epi16(v_val_d, 15);
        __m128i v_tmp_d = _mm_add_epi16(_mm_add_epi16(v_val_d, v_bias_d), v_sign_d);
        return _mm_srai_epi16(v_tmp_d, bits);
    }

    // ---- synonyms_avx2.h ----

    [MethodImpl(AI)] internal static __m256i yy_load_256<T>(Ptr<T> a) where T : unmanaged => _mm256_load_si256(a);
    [MethodImpl(AI)] internal static __m256i yy_load_256<T>(Span<T> a) where T : unmanaged => _mm256_load_si256(a);
    [MethodImpl(AI)] internal static __m256i yy_load_256<T>(ReadOnlySpan<T> a) where T : unmanaged => _mm256_load_si256(a);
    [MethodImpl(AI)] internal static __m256i yy_loadu_256<T>(Ptr<T> a) where T : unmanaged => _mm256_loadu_si256(a);
    [MethodImpl(AI)] internal static __m256i yy_loadu_256<T>(Span<T> a) where T : unmanaged => _mm256_loadu_si256(a);
    [MethodImpl(AI)] internal static __m256i yy_loadu_256<T>(ReadOnlySpan<T> a) where T : unmanaged => _mm256_loadu_si256(a);
    [MethodImpl(AI)] internal static void yy_store_256<T>(Ptr<T> a, __m256i v) where T : unmanaged => _mm256_store_si256(a, v);
    [MethodImpl(AI)] internal static void yy_store_256<T>(Span<T> a, __m256i v) where T : unmanaged => _mm256_store_si256(a, v);
    [MethodImpl(AI)] internal static void yy_storeu_256<T>(Ptr<T> a, __m256i v) where T : unmanaged => _mm256_storeu_si256(a, v);
    [MethodImpl(AI)] internal static void yy_storeu_256<T>(Span<T> a, __m256i v) where T : unmanaged => _mm256_storeu_si256(a, v);

    // Fill an AVX register using an interleaved pair of values, ie. set the
    // 16 channels to {a, b} repeated 8 times, using the same channel ordering
    // as when a register is stored to / loaded from memory.
    //
    // This is useful for rearranging filter kernels for use with the _mm_madd_epi16
    // instruction
    [MethodImpl(AI)] internal static __m256i yy_set2_epi16(short a, short b) =>
        _mm256_setr_epi16(a, b, a, b, a, b, a, b, a, b, a, b, a, b, a, b);

    // Some compilers don't have _mm256_set_m128i defined in immintrin.h. We
    // therefore define an equivalent function using a different intrinsic.
    // ([ hi ], [ lo ]) -> [ hi ][ lo ]
    [MethodImpl(AI)] internal static __m256i yy_set_m128i(__m128i hi, __m128i lo) =>
        _mm256_insertf128_si256(_mm256_castsi128_si256(lo), hi, 1);

    // This behaves similarly to _mm256_set_epi64x(), but avoids undefined
    // sanitizer warnings when loading values from unaligned buffers using
    // `*(int64_t *)val`.
    [MethodImpl(AI)] internal static __m256i yy_loadu_4x64<T>(Ptr<T> e3, Ptr<T> e2, Ptr<T> e1, Ptr<T> e0) where T : unmanaged
    {
        __m128d v0 = _mm_castsi128_pd(_mm_loadl_epi64(e0));
        __m128d v01 = _mm_loadh_pd(v0, e1);
        __m128d v2 = _mm_castsi128_pd(_mm_loadl_epi64(e2));
        __m128d v23 = _mm_loadh_pd(v2, e3);
        return yy_set_m128i(_mm_castpd_si128(v23), _mm_castpd_si128(v01));
    }

    [MethodImpl(AI)] internal static __m256i yy_loadu2_128<T>(Ptr<T> hi, Ptr<T> lo) where T : unmanaged
    {
        __m128i mhi = _mm_loadu_si128(hi);
        __m128i mlo = _mm_loadu_si128(lo);
        return yy_set_m128i(mhi, mlo);
    }

    [MethodImpl(AI)] internal static void yy_storeu2_128<T>(Ptr<T> hi, Ptr<T> lo, __m256i a) where T : unmanaged
    {
        _mm_storeu_si128(hi, _mm256_extracti128_si256(a, 1));
        _mm_storeu_si128(lo, _mm256_castsi256_si128(a));
    }

    [MethodImpl(AI)] internal static __m256i yy_roundn_epu16(__m256i v_val_w, int bits)
    {
        __m256i v_s_w = _mm256_srli_epi16(v_val_w, bits - 1);
        return _mm256_avg_epu16(v_s_w, _mm256_setzero_si256());
    }
}
