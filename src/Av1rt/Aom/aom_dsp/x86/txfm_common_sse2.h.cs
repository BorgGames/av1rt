// Port of libaom aom_dsp/x86/txfm_common_sse2.h

global using static Av1rt.Aom.txfm_common_sse2;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static class txfm_common_sse2
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    // [port] Macro -> method. Arguments are int like the C call sites (cospi[] entries and their
    // negations); the (uint16_t) casts keep the low 16 bits as in C.
    [MethodImpl(AI)]
    internal static __m128i pair_set_epi16(int a, int b) =>
        _mm_set1_epi32((int)((uint)(ushort)(a) | (((uint)(ushort)(b)) << 16)));

    // Reverse the 8 16 bit words in __m128i
    [MethodImpl(AI)]
    internal static __m128i mm_reverse_epi16(__m128i x)
    {
        __m128i a = _mm_shufflelo_epi16(x, 0x1b);
        __m128i b = _mm_shufflehi_epi16(a, 0x1b);
        return _mm_shuffle_epi32(b, 0x4e);
    }

    // [port] Macro -> method.
    [MethodImpl(AI)]
    internal static __m128i octa_set_epi16(int a, int b, int c, int d, int e, int f, int g, int h) =>
        _mm_setr_epi16((short)(a), (short)(b), (short)(c), (short)(d),
                       (short)(e), (short)(f), (short)(g), (short)(h));
}
