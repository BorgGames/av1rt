// Port of libaom aom_dsp/x86/intrapred_asm_sse2.asm

global using static Av1rt.Aom.intrapred_asm_sse2;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] The x86inc assembly kernels rewritten as SSE2 intrinsics that follow the asm instruction
// by instruction (movd/movq/mova -> _mm_loadu_si32/_mm_loadl_epi64/_mm_load_si128, psadbw ->
// _mm_sad_epu8, punpck*/pshuflw/pshufd -> the matching unpack/shuffle, packuswb -> _mm_packus_epi16,
// the GLOBAL(pw_*) / GLOBAL(dc_128) constants -> _mm_set1_*). They compute exactly the C result.
// [subset] The 32x32 kernels (dc/dc_top/dc_left/dc_128/v/h_predictor_32x32) are not ported: the avx2
// intrinsics versions are the best variant for that size (intrapred_avx2.c.cs).
internal static class intrapred_asm_sse2
{
    // [port] cglobal dc_predictor_4x4: the 4 above and 4 left pixels in one register, one psadbw.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_predictor_4x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m2 = _mm_loadu_si32(left);      // movd      m2, [leftq]
        __m128i m0 = _mm_loadu_si32(above);     // movd      m0, [aboveq]
        __m128i m1 = _mm_setzero_si128();       // pxor      m1, m1
        m0 = _mm_unpacklo_epi32(m0, m2);        // punpckldq m0, m2
        m0 = _mm_sad_epu8(m0, m1);              // psadbw    m0, m1
        m0 = _mm_add_epi16(m0, _mm_set1_epi16(4)); // paddw  m0, [GLOBAL(pw_4)]
        m0 = _mm_srai_epi16(m0, 3);             // psraw     m0, 3
        m0 = _mm_shufflelo_epi16(m0, 0x0);      // pshuflw   m0, m0, 0x0
        m0 = _mm_packus_epi16(m0, m0);          // packuswb  m0, m0
        _mm_storeu_si32(dst, m0);
        _mm_storeu_si32(dst + stride, m0);
        dst += stride * 2;
        _mm_storeu_si32(dst, m0);
        _mm_storeu_si32(dst + stride, m0);
    }

    // [port] cglobal dc_left_predictor_4x4.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_left_predictor_4x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m1 = _mm_setzero_si128();       // pxor      m1, m1
        __m128i m0 = _mm_loadu_si32(left);      // movd      m0, [leftq]
        m0 = _mm_sad_epu8(m0, m1);              // psadbw    m0, m1
        m0 = _mm_add_epi16(m0, _mm_set1_epi16(2)); // paddw  m0, [GLOBAL(pw2_4)]
        m0 = _mm_srai_epi16(m0, 2);             // psraw     m0, 2
        m0 = _mm_shufflelo_epi16(m0, 0x0);      // pshuflw   m0, m0, 0x0
        m0 = _mm_packus_epi16(m0, m0);          // packuswb  m0, m0
        _mm_storeu_si32(dst, m0);
        _mm_storeu_si32(dst + stride, m0);
        dst += stride * 2;
        _mm_storeu_si32(dst, m0);
        _mm_storeu_si32(dst + stride, m0);
    }

    // [port] cglobal dc_top_predictor_4x4.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_top_predictor_4x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m1 = _mm_setzero_si128();       // pxor      m1, m1
        __m128i m0 = _mm_loadu_si32(above);     // movd      m0, [aboveq]
        m0 = _mm_sad_epu8(m0, m1);              // psadbw    m0, m1
        m0 = _mm_add_epi16(m0, _mm_set1_epi16(2)); // paddw  m0, [GLOBAL(pw2_4)]
        m0 = _mm_srai_epi16(m0, 2);             // psraw     m0, 2
        m0 = _mm_shufflelo_epi16(m0, 0x0);      // pshuflw   m0, m0, 0x0
        m0 = _mm_packus_epi16(m0, m0);          // packuswb  m0, m0
        _mm_storeu_si32(dst, m0);
        _mm_storeu_si32(dst + stride, m0);
        dst += stride * 2;
        _mm_storeu_si32(dst, m0);
        _mm_storeu_si32(dst + stride, m0);
    }

    // [port] cglobal dc_predictor_8x8.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_predictor_8x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m1 = _mm_setzero_si128();       // pxor      m1, m1
        __m128i m0 = _mm_loadl_epi64(above);    // movq      m0, [aboveq]
        __m128i m2 = _mm_loadl_epi64(left);     // movq      m2, [leftq]
        m0 = _mm_sad_epu8(m0, m1);              // psadbw    m0, m1
        m2 = _mm_sad_epu8(m2, m1);              // psadbw    m2, m1
        m0 = _mm_add_epi16(m0, m2);             // paddw     m0, m2
        m0 = _mm_add_epi16(m0, _mm_set1_epi16(8)); // paddw  m0, [GLOBAL(pw_8)]
        m0 = _mm_srai_epi16(m0, 4);             // psraw     m0, 4
        m0 = _mm_unpacklo_epi8(m0, m0);         // punpcklbw m0, m0
        m0 = _mm_shufflelo_epi16(m0, 0x0);      // pshuflw   m0, m0, 0x0
        store_8x8(dst, stride, m0);
    }

    // [port] cglobal dc_top_predictor_8x8.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_top_predictor_8x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m1 = _mm_setzero_si128();       // pxor      m1, m1
        __m128i m0 = _mm_loadl_epi64(above);    // movq      m0, [aboveq]
        m0 = _mm_sad_epu8(m0, m1);              // psadbw    m0, m1
        m0 = _mm_add_epi16(m0, _mm_set1_epi16(4)); // paddw  m0, [GLOBAL(pw2_8)]
        m0 = _mm_srai_epi16(m0, 3);             // psraw     m0, 3
        m0 = _mm_unpacklo_epi8(m0, m0);         // punpcklbw m0, m0
        m0 = _mm_shufflelo_epi16(m0, 0x0);      // pshuflw   m0, m0, 0x0
        store_8x8(dst, stride, m0);
    }

    // [port] cglobal dc_left_predictor_8x8.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_left_predictor_8x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m1 = _mm_setzero_si128();       // pxor      m1, m1
        __m128i m0 = _mm_loadl_epi64(left);     // movq      m0, [leftq]
        m0 = _mm_sad_epu8(m0, m1);              // psadbw    m0, m1
        m0 = _mm_add_epi16(m0, _mm_set1_epi16(4)); // paddw  m0, [GLOBAL(pw2_8)]
        m0 = _mm_srai_epi16(m0, 3);             // psraw     m0, 3
        m0 = _mm_unpacklo_epi8(m0, m0);         // punpcklbw m0, m0
        m0 = _mm_shufflelo_epi16(m0, 0x0);      // pshuflw   m0, m0, 0x0
        store_8x8(dst, stride, m0);
    }

    // [port] cglobal dc_128_predictor_4x4: movd of GLOBAL(dc_128).
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_128_predictor_4x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m0 = _mm_set1_epi8(unchecked((sbyte)128));
        _mm_storeu_si32(dst, m0);
        _mm_storeu_si32(dst + stride, m0);
        _mm_storeu_si32(dst + stride * 2, m0);
        _mm_storeu_si32(dst + stride * 3, m0);
    }

    // [port] cglobal dc_128_predictor_8x8: movq of GLOBAL(dc_128).
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_128_predictor_8x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m0 = _mm_set1_epi8(unchecked((sbyte)128));
        store_8x8(dst, stride, m0);
    }

    // [port] cglobal dc_predictor_16x16. movhlps m2, m0 only feeds the low qword, which
    // _mm_unpackhi_epi64(m0, m0) reproduces.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_predictor_16x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m1 = _mm_setzero_si128();       // pxor      m1, m1
        __m128i m0 = _mm_load_si128(above);     // mova      m0, [aboveq]
        __m128i m2 = _mm_load_si128(left);      // mova      m2, [leftq]
        m0 = _mm_sad_epu8(m0, m1);              // psadbw    m0, m1
        m2 = _mm_sad_epu8(m2, m1);              // psadbw    m2, m1
        m0 = _mm_add_epi16(m0, m2);             // paddw     m0, m2
        m2 = _mm_unpackhi_epi64(m0, m0);        // movhlps   m2, m0
        m0 = _mm_add_epi16(m0, m2);             // paddw     m0, m2
        m0 = _mm_add_epi16(m0, _mm_set1_epi16(16)); // paddw m0, [GLOBAL(pw_16)]
        m0 = _mm_srai_epi16(m0, 5);             // psraw     m0, 5
        m0 = _mm_shufflelo_epi16(m0, 0x0);      // pshuflw   m0, m0, 0x0
        m0 = _mm_unpacklo_epi64(m0, m0);        // punpcklqdq m0, m0
        m0 = _mm_packus_epi16(m0, m0);          // packuswb  m0, m0
        store_16x16(dst, stride, m0);
    }

    // [port] cglobal dc_top_predictor_16x16.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_top_predictor_16x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m1 = _mm_setzero_si128();       // pxor      m1, m1
        __m128i m0 = _mm_load_si128(above);     // mova      m0, [aboveq]
        m0 = _mm_sad_epu8(m0, m1);              // psadbw    m0, m1
        __m128i m2 = _mm_unpackhi_epi64(m0, m0); // movhlps  m2, m0
        m0 = _mm_add_epi16(m0, m2);             // paddw     m0, m2
        m0 = _mm_add_epi16(m0, _mm_set1_epi16(8)); // paddw  m0, [GLOBAL(pw2_16)]
        m0 = _mm_srai_epi16(m0, 4);             // psraw     m0, 4
        m0 = _mm_shufflelo_epi16(m0, 0x0);      // pshuflw   m0, m0, 0x0
        m0 = _mm_unpacklo_epi64(m0, m0);        // punpcklqdq m0, m0
        m0 = _mm_packus_epi16(m0, m0);          // packuswb  m0, m0
        store_16x16(dst, stride, m0);
    }

    // [port] cglobal dc_left_predictor_16x16.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_left_predictor_16x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m1 = _mm_setzero_si128();       // pxor      m1, m1
        __m128i m0 = _mm_load_si128(left);      // mova      m0, [leftq]
        m0 = _mm_sad_epu8(m0, m1);              // psadbw    m0, m1
        __m128i m2 = _mm_unpackhi_epi64(m0, m0); // movhlps  m2, m0
        m0 = _mm_add_epi16(m0, m2);             // paddw     m0, m2
        m0 = _mm_add_epi16(m0, _mm_set1_epi16(8)); // paddw  m0, [GLOBAL(pw2_16)]
        m0 = _mm_srai_epi16(m0, 4);             // psraw     m0, 4
        m0 = _mm_shufflelo_epi16(m0, 0x0);      // pshuflw   m0, m0, 0x0
        m0 = _mm_unpacklo_epi64(m0, m0);        // punpcklqdq m0, m0
        m0 = _mm_packus_epi16(m0, m0);          // packuswb  m0, m0
        store_16x16(dst, stride, m0);
    }

    // [port] cglobal dc_128_predictor_16x16: mova of GLOBAL(dc_128).
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_dc_128_predictor_16x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m0 = _mm_set1_epi8(unchecked((sbyte)128));
        store_16x16(dst, stride, m0);
    }

    // [port] cglobal v_predictor_4x4.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_v_predictor_4x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m0 = _mm_loadu_si32(above);     // movd      m0, [aboveq]
        _mm_storeu_si32(dst, m0);
        _mm_storeu_si32(dst + stride, m0);
        dst += stride * 2;
        _mm_storeu_si32(dst, m0);
        _mm_storeu_si32(dst + stride, m0);
    }

    // [port] cglobal v_predictor_8x8.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_v_predictor_8x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m0 = _mm_loadl_epi64(above);    // movq      m0, [aboveq]
        store_8x8(dst, stride, m0);
    }

    // [port] cglobal v_predictor_16x16.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_v_predictor_16x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m0 = _mm_load_si128(above);     // mova      m0, [aboveq]
        store_16x16(dst, stride, m0);
    }

    // [port] cglobal h_predictor_4x4: left[0..3] each repeated 4 times, one dword per row.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_h_predictor_4x4_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m0 = _mm_loadu_si32(left);      // movd      m0, [leftq]
        m0 = _mm_unpacklo_epi8(m0, m0);         // punpcklbw m0, m0
        m0 = _mm_unpacklo_epi8(m0, m0);         // punpcklbw m0, m0
        __m128i m1 = _mm_shuffle_epi32(m0, 0x1); // pshufd   m1, m0, 0x1
        _mm_storeu_si32(dst, m0);
        _mm_storeu_si32(dst + stride, m1);
        __m128i m2 = _mm_shuffle_epi32(m0, 0x2); // pshufd   m2, m0, 0x2
        dst += stride * 2;
        __m128i m3 = _mm_shuffle_epi32(m0, 0x3); // pshufd   m3, m0, 0x3
        _mm_storeu_si32(dst, m2);
        _mm_storeu_si32(dst + stride, m3);
    }

    // [port] cglobal h_predictor_8x8: two iterations of 4 rows.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_h_predictor_8x8_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        __m128i m0 = _mm_loadl_epi64(left);     // movq      m0, [leftq]
        m0 = _mm_unpacklo_epi8(m0, m0);         // punpcklbw m0, m0 ; l1 l1 l2 l2 ... l8 l8
        for (int line = -2; line != 0; line++)
        {
            __m128i m1 = _mm_shufflelo_epi16(m0, 0x0);  // l1 l1 l1 l1 l1 l1 l1 l1
            __m128i m2 = _mm_shufflelo_epi16(m0, 0x55); // l2 l2 l2 l2 l2 l2 l2 l2
            _mm_storel_epi64(dst, m1);
            _mm_storel_epi64(dst + stride, m2);
            m1 = _mm_shufflelo_epi16(m0, 0xaa);
            m2 = _mm_shufflelo_epi16(m0, 0xff);
            _mm_storel_epi64(dst + stride * 2, m1);
            _mm_storel_epi64(dst + stride * 3, m2);
            m0 = _mm_shuffle_epi32(m0, 0xe);    // pshufd m0, m0, 0xe ; [63:0] l5 l5 l6 l6 l7 l7 l8 l8
            dst += stride * 4;
        }
    }

    // [port] cglobal h_predictor_16x16: four iterations of 4 rows, 4 left pixels each.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_h_predictor_16x16_sse2(Ptr<byte> dst, int stride, Ptr<byte> above, Ptr<byte> left)
    {
        for (int line = -4; line != 0; line++)
        {
            __m128i m0 = _mm_loadu_si32(left);  // movd      m0, [leftq]
            m0 = _mm_unpacklo_epi8(m0, m0);     // punpcklbw m0, m0
            m0 = _mm_unpacklo_epi8(m0, m0);     // punpcklbw m0, m0 ; l1 to l4 each repeated 4 times
            __m128i m1 = _mm_shuffle_epi32(m0, 0x0);  // l1 repeated 16 times
            __m128i m2 = _mm_shuffle_epi32(m0, 0x55); // l2 repeated 16 times
            _mm_store_si128(dst, m1);
            _mm_store_si128(dst + stride, m2);
            m1 = _mm_shuffle_epi32(m0, 0xaa);
            m2 = _mm_shuffle_epi32(m0, 0xff);
            _mm_store_si128(dst + stride * 2, m1);
            _mm_store_si128(dst + stride * 3, m2);
            left += 4;
            dst += stride * 4;
        }
    }

    // [port] The 8 movq stores of the 8x8 kernels (two groups of 4 rows: dst, +stride, +stride*2,
    // +stride3, then lea dstq, [dstq+strideq*4]).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void store_8x8(Ptr<byte> dst, int stride, __m128i m0)
    {
        for (int i = 0; i < 2; i++)
        {
            _mm_storel_epi64(dst, m0);
            _mm_storel_epi64(dst + stride, m0);
            _mm_storel_epi64(dst + stride * 2, m0);
            _mm_storel_epi64(dst + stride * 3, m0);
            dst += stride * 4;
        }
    }

    // [port] The .loop of the 16x16 kernels: lines4 = 4 iterations of 4 mova stores.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void store_16x16(Ptr<byte> dst, int stride, __m128i m0)
    {
        for (int lines4 = 4; lines4 != 0; lines4--)
        {
            _mm_store_si128(dst, m0);
            _mm_store_si128(dst + stride, m0);
            _mm_store_si128(dst + stride * 2, m0);
            _mm_store_si128(dst + stride * 3, m0);
            dst += stride * 4;
        }
    }
}
