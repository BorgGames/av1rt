// Port of libaom aom_dsp/x86/intrapred_x86.h

global using static Av1rt.Aom.intrapred_x86;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static class intrapred_x86
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static __m128i dc_sum_16_sse2(Ptr<byte> @ref)
    {
        __m128i x = _mm_load_si128(@ref);
        __m128i zero = _mm_setzero_si128();
        x = _mm_sad_epu8(x, zero);
        __m128i high = _mm_unpackhi_epi64(x, x);
        return _mm_add_epi16(x, high);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static __m128i dc_sum_32_sse2(Ptr<byte> @ref)
    {
        __m128i x0 = _mm_load_si128(@ref);
        __m128i x1 = _mm_load_si128(@ref + 16);
        __m128i zero = _mm_setzero_si128();
        x0 = _mm_sad_epu8(x0, zero);
        x1 = _mm_sad_epu8(x1, zero);
        x0 = _mm_add_epi16(x0, x1);
        __m128i high = _mm_unpackhi_epi64(x0, x0);
        return _mm_add_epi16(x0, high);
    }
}
