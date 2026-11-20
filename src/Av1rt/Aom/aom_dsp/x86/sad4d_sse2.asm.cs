// Port of libaom aom_dsp/x86/sad4d_sse2.asm

global using static Av1rt.Aom.sad4d_sse2;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] libaom's SSE2 x4d SAD for widths 8 and 4 is assembly (x86inc). These are intrinsics
// versions with the asm symbols' names that follow the asm's SADNXN4D (PROCESS_8x2x4 /
// PROCESS_4x2x4: two rows per step, all four references against the same source register) and
// compute exactly what aom_sad*x4d_c / aom_sad_skip_*x4d_c compute. Only the variants the RTCD
// picks on an AVX2 machine (CONFIG_REALTIME_ONLY sizes) are written.
internal static class sad4d_sse2
{
    // SADNXN4D 8, N (%3 = skip): PROCESS_8x2x4 per two rows; m4..m7 accumulate ref1..ref4.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sad8xnx4d_sse2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref,
                                       int ref_stride, Ptr<uint> res, int n, int skip)
    {
        Ptr<byte> ref1 = @ref[0], ref2 = @ref[1], ref3 = @ref[2], ref4 = @ref[3];
        if (skip != 0)
        {
            src_stride <<= 1;
            ref_stride <<= 1;
        }
        __m128i m4 = _mm_setzero_si128(), m5 = _mm_setzero_si128();
        __m128i m6 = _mm_setzero_si128(), m7 = _mm_setzero_si128();
        for (int cnt = (n / 2) >> skip; cnt > 0; cnt--)
        {
            __m128i m0 = loadh_epi64(src + src_stride, _mm_loadl_epi64(src));
            __m128i m1 = loadh_epi64(ref1 + ref_stride, _mm_loadl_epi64(ref1));
            __m128i m2 = loadh_epi64(ref2 + ref_stride, _mm_loadl_epi64(ref2));
            m4 = _mm_add_epi32(m4, _mm_sad_epu8(m1, m0));
            m5 = _mm_add_epi32(m5, _mm_sad_epu8(m2, m0));
            m1 = loadh_epi64(ref3 + ref_stride, _mm_loadl_epi64(ref3));
            m2 = loadh_epi64(ref4 + ref_stride, _mm_loadl_epi64(ref4));
            m6 = _mm_add_epi32(m6, _mm_sad_epu8(m1, m0));
            m7 = _mm_add_epi32(m7, _mm_sad_epu8(m2, m0));
            // ADVANCE_END_OF_TWO_LINES
            src += src_stride * 2;
            ref1 += ref_stride * 2;
            ref2 += ref_stride * 2;
            ref3 += ref_stride * 2;
            ref4 += ref_stride * 2;
        }

        // %1 > 4
        m5 = _mm_slli_si128(m5, 4);
        m7 = _mm_slli_si128(m7, 4);
        m4 = _mm_or_si128(m4, m5);
        m6 = _mm_or_si128(m6, m7);
        m5 = m4;
        m7 = m6;
        m4 = _mm_unpacklo_epi64(m4, m6);
        m5 = _mm_unpackhi_epi64(m5, m7);
        m4 = _mm_add_epi32(m4, m5);
        if (skip != 0) m4 = _mm_slli_epi32(m4, 1);
        _mm_storeu_si128(res, m4);
    }

    // SADNXN4D 4, N: PROCESS_4x2x4 per two rows; m6 accumulates ref1|ref2, m7 ref3|ref4.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void sad4xnx4d_sse2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref,
                                       int ref_stride, Ptr<uint> res, int n)
    {
        Ptr<byte> ref1 = @ref[0], ref2 = @ref[1], ref3 = @ref[2], ref4 = @ref[3];
        __m128i m6 = _mm_setzero_si128(), m7 = _mm_setzero_si128();
        for (int cnt = n / 2; cnt > 0; cnt--)
        {
            __m128i m0 = _mm_unpacklo_epi32(_mm_cvtsi32_si128(loadu_int32(src)),
                                            _mm_cvtsi32_si128(loadu_int32(src + src_stride)));
            __m128i m1 = _mm_unpacklo_epi32(_mm_cvtsi32_si128(loadu_int32(ref1)),
                                            _mm_cvtsi32_si128(loadu_int32(ref1 + ref_stride)));
            __m128i m2 = _mm_unpacklo_epi32(_mm_cvtsi32_si128(loadu_int32(ref2)),
                                            _mm_cvtsi32_si128(loadu_int32(ref2 + ref_stride)));
            __m128i m3 = _mm_unpacklo_epi32(_mm_cvtsi32_si128(loadu_int32(ref3)),
                                            _mm_cvtsi32_si128(loadu_int32(ref3 + ref_stride)));
            __m128i m4 = _mm_unpacklo_epi32(_mm_cvtsi32_si128(loadu_int32(ref4)),
                                            _mm_cvtsi32_si128(loadu_int32(ref4 + ref_stride)));
            m0 = _mm_unpacklo_epi64(m0, m0);  // movlhps m0, m0
            m1 = _mm_unpacklo_epi64(m1, m2);  // movlhps m1, m2
            m3 = _mm_unpacklo_epi64(m3, m4);  // movlhps m3, m4
            m6 = _mm_add_epi32(m6, _mm_sad_epu8(m1, m0));
            m7 = _mm_add_epi32(m7, _mm_sad_epu8(m3, m0));
            // ADVANCE_END_OF_TWO_LINES
            src += src_stride * 2;
            ref1 += ref_stride * 2;
            ref2 += ref_stride * 2;
            ref3 += ref_stride * 2;
            ref4 += ref_stride * 2;
        }

        m6 = _mm_shuffle_epi32(m6, 0x08);
        m7 = _mm_shuffle_epi32(m7, 0x08);
        _mm_storel_epi64(res + 0, m6);
        _mm_storel_epi64(res + 2, m7);
    }

    // SADNXN4D 8, 16 / 8, 8 / 8, 4 / 4, 8 / 4, 4
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_sad8x16x4d_sse2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        sad8xnx4d_sse2(src, src_stride, @ref, ref_stride, res, 16, 0);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_sad8x8x4d_sse2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        sad8xnx4d_sse2(src, src_stride, @ref, ref_stride, res, 8, 0);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_sad8x4x4d_sse2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        sad8xnx4d_sse2(src, src_stride, @ref, ref_stride, res, 4, 0);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_sad4x8x4d_sse2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        sad4xnx4d_sse2(src, src_stride, @ref, ref_stride, res, 8);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_sad4x4x4d_sse2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        sad4xnx4d_sse2(src, src_stride, @ref, ref_stride, res, 4);

    // SADNXN4D 8, 16, 1
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void aom_sad_skip_8x16x4d_sse2(Ptr<byte> src, int src_stride, Ptr<Ptr<byte>> @ref, int ref_stride, Ptr<uint> res) =>
        sad8xnx4d_sse2(src, src_stride, @ref, ref_stride, res, 16, 1);
}
