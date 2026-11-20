// Port of libaom aom_dsp/x86/sad_sse2.asm

global using static Av1rt.Aom.sad_sse2;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] libaom's SSE2 SAD for widths 16, 8 and 4 is assembly (x86inc). These are intrinsics
// versions with the asm symbols' names that follow the asm (same loads, 4 rows per iteration,
// pavgb against second_pred for _avg, doubled strides and a final doubling for sad_skip) and
// compute exactly what aom_sad*_c / aom_sad*_avg_c / aom_sad_skip_*_c compute. Only the
// variants the RTCD picks on an AVX2 machine (CONFIG_REALTIME_ONLY sizes) are written: the
// wider ones use the AVX2 versions.
internal static class sad_sse2
{
    // SAD16XN: 4 rows of 16 pixels per iteration.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint sad16xn_sse2(Ptr<byte> src, int src_stride, Ptr<byte> @ref,
                                     int ref_stride, int n_rows)
    {
        int src_stride3 = src_stride * 3;
        int ref_stride3 = ref_stride * 3;
        __m128i m0 = _mm_setzero_si128();
        do
        {
            __m128i m1 = _mm_loadu_si128(@ref);
            __m128i m2 = _mm_loadu_si128(@ref + ref_stride);
            __m128i m3 = _mm_loadu_si128(@ref + ref_stride * 2);
            __m128i m4 = _mm_loadu_si128(@ref + ref_stride3);
            m1 = _mm_sad_epu8(m1, _mm_loadu_si128(src));
            m2 = _mm_sad_epu8(m2, _mm_loadu_si128(src + src_stride));
            m3 = _mm_sad_epu8(m3, _mm_loadu_si128(src + src_stride * 2));
            m4 = _mm_sad_epu8(m4, _mm_loadu_si128(src + src_stride3));
            m1 = _mm_add_epi32(m1, m2);
            m3 = _mm_add_epi32(m3, m4);
            @ref += ref_stride * 4;
            m0 = _mm_add_epi32(m0, m1);
            src += src_stride * 4;
            m0 = _mm_add_epi32(m0, m3);
        } while (--n_rows > 0);

        m0 = _mm_add_epi32(m0, _mm_srli_si128(m0, 8));  // movhlps + paddd
        return (uint)_mm_cvtsi128_si32(m0);
    }

    // SAD16XN with %2 == 1 (avg): second_pred is packed (stride 16).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint sad16xn_avg_sse2(Ptr<byte> src, int src_stride, Ptr<byte> @ref,
                                         int ref_stride, Ptr<byte> second_pred, int n_rows)
    {
        int src_stride3 = src_stride * 3;
        int ref_stride3 = ref_stride * 3;
        __m128i m0 = _mm_setzero_si128();
        do
        {
            __m128i m1 = _mm_loadu_si128(@ref);
            __m128i m2 = _mm_loadu_si128(@ref + ref_stride);
            __m128i m3 = _mm_loadu_si128(@ref + ref_stride * 2);
            __m128i m4 = _mm_loadu_si128(@ref + ref_stride3);
            m1 = _mm_avg_epu8(m1, _mm_loadu_si128(second_pred + 16 * 0));
            m2 = _mm_avg_epu8(m2, _mm_loadu_si128(second_pred + 16 * 1));
            m3 = _mm_avg_epu8(m3, _mm_loadu_si128(second_pred + 16 * 2));
            m4 = _mm_avg_epu8(m4, _mm_loadu_si128(second_pred + 16 * 3));
            second_pred += 16 * 4;
            m1 = _mm_sad_epu8(m1, _mm_loadu_si128(src));
            m2 = _mm_sad_epu8(m2, _mm_loadu_si128(src + src_stride));
            m3 = _mm_sad_epu8(m3, _mm_loadu_si128(src + src_stride * 2));
            m4 = _mm_sad_epu8(m4, _mm_loadu_si128(src + src_stride3));
            m1 = _mm_add_epi32(m1, m2);
            m3 = _mm_add_epi32(m3, m4);
            @ref += ref_stride * 4;
            m0 = _mm_add_epi32(m0, m1);
            src += src_stride * 4;
            m0 = _mm_add_epi32(m0, m3);
        } while (--n_rows > 0);

        m0 = _mm_add_epi32(m0, _mm_srli_si128(m0, 8));
        return (uint)_mm_cvtsi128_si32(m0);
    }

    // SAD8XN: 4 rows of 8 pixels per iteration, two rows per register (movh + movhps).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint sad8xn_sse2(Ptr<byte> src, int src_stride, Ptr<byte> @ref,
                                    int ref_stride, int n_rows)
    {
        int src_stride3 = src_stride * 3;
        int ref_stride3 = ref_stride * 3;
        __m128i m0 = _mm_setzero_si128();
        do
        {
            __m128i m1 = loadh_epi64(@ref + ref_stride, _mm_loadl_epi64(@ref));
            __m128i m2 = loadh_epi64(@ref + ref_stride3, _mm_loadl_epi64(@ref + ref_stride * 2));
            __m128i m3 = loadh_epi64(src + src_stride, _mm_loadl_epi64(src));
            __m128i m4 = loadh_epi64(src + src_stride3, _mm_loadl_epi64(src + src_stride * 2));
            m1 = _mm_sad_epu8(m1, m3);
            m2 = _mm_sad_epu8(m2, m4);
            @ref += ref_stride * 4;
            m0 = _mm_add_epi32(m0, m1);
            src += src_stride * 4;
            m0 = _mm_add_epi32(m0, m2);
        } while (--n_rows > 0);

        m0 = _mm_add_epi32(m0, _mm_srli_si128(m0, 8));
        return (uint)_mm_cvtsi128_si32(m0);
    }

    // SAD8XN with %2 == 1 (avg): second_pred is packed (stride 8).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint sad8xn_avg_sse2(Ptr<byte> src, int src_stride, Ptr<byte> @ref,
                                        int ref_stride, Ptr<byte> second_pred, int n_rows)
    {
        int src_stride3 = src_stride * 3;
        int ref_stride3 = ref_stride * 3;
        __m128i m0 = _mm_setzero_si128();
        do
        {
            __m128i m1 = loadh_epi64(@ref + ref_stride, _mm_loadl_epi64(@ref));
            __m128i m2 = loadh_epi64(@ref + ref_stride3, _mm_loadl_epi64(@ref + ref_stride * 2));
            m1 = _mm_avg_epu8(m1, _mm_loadu_si128(second_pred + 16 * 0));
            m2 = _mm_avg_epu8(m2, _mm_loadu_si128(second_pred + 16 * 1));
            second_pred += 16 * 2;
            __m128i m3 = loadh_epi64(src + src_stride, _mm_loadl_epi64(src));
            __m128i m4 = loadh_epi64(src + src_stride3, _mm_loadl_epi64(src + src_stride * 2));
            m1 = _mm_sad_epu8(m1, m3);
            m2 = _mm_sad_epu8(m2, m4);
            @ref += ref_stride * 4;
            m0 = _mm_add_epi32(m0, m1);
            src += src_stride * 4;
            m0 = _mm_add_epi32(m0, m2);
        } while (--n_rows > 0);

        m0 = _mm_add_epi32(m0, _mm_srli_si128(m0, 8));
        return (uint)_mm_cvtsi128_si32(m0);
    }

    // SAD4XN: 4 rows of 4 pixels per iteration in one register (movd + punpckldq + movlhps).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint sad4xn_sse2(Ptr<byte> src, int src_stride, Ptr<byte> @ref,
                                    int ref_stride, int n_rows)
    {
        __m128i m0 = _mm_setzero_si128();
        do
        {
            __m128i m1 = load_8bit_4x4_to_1_reg_sse2(@ref, ref_stride);
            __m128i m2 = load_8bit_4x4_to_1_reg_sse2(src, src_stride);
            m1 = _mm_sad_epu8(m1, m2);
            @ref += ref_stride * 4;
            m0 = _mm_add_epi32(m0, m1);
            src += src_stride * 4;
        } while (--n_rows > 0);

        m0 = _mm_add_epi32(m0, _mm_srli_si128(m0, 8));
        return (uint)_mm_cvtsi128_si32(m0);
    }

    // SAD16XN 32 / 16 / 8                     ; sad16x32_sse2 ...
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad16x32_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad16xn_sse2(src_ptr, src_stride, ref_ptr, ref_stride, 32 / 4);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad16x16_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad16xn_sse2(src_ptr, src_stride, ref_ptr, ref_stride, 16 / 4);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad16x8_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad16xn_sse2(src_ptr, src_stride, ref_ptr, ref_stride, 8 / 4);

    // SAD16XN 32, 1 / 16, 1 / 8, 1             ; sad16x32_avg_sse2 ...
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad16x32_avg_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride, Ptr<byte> second_pred) =>
        sad16xn_avg_sse2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred, 32 / 4);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad16x16_avg_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride, Ptr<byte> second_pred) =>
        sad16xn_avg_sse2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred, 16 / 4);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad16x8_avg_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride, Ptr<byte> second_pred) =>
        sad16xn_avg_sse2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred, 8 / 4);

    // SAD16XN 32, 2 / 16, 2                    ; sad_skip_16x32_sse2 ...
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad_skip_16x32_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad16xn_sse2(src_ptr, src_stride * 2, ref_ptr, ref_stride * 2, 32 / 8) << 1;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad_skip_16x16_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad16xn_sse2(src_ptr, src_stride * 2, ref_ptr, ref_stride * 2, 16 / 8) << 1;

    // SAD8XN 16 / 8 / 4                        ; sad8x16_sse2 ...
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad8x16_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad8xn_sse2(src_ptr, src_stride, ref_ptr, ref_stride, 16 / 4);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad8x8_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad8xn_sse2(src_ptr, src_stride, ref_ptr, ref_stride, 8 / 4);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad8x4_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad8xn_sse2(src_ptr, src_stride, ref_ptr, ref_stride, 4 / 4);

    // SAD8XN 16, 1 / 8, 1                      ; sad8x16_avg_sse2 ...
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad8x16_avg_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride, Ptr<byte> second_pred) =>
        sad8xn_avg_sse2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred, 16 / 4);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad8x8_avg_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride, Ptr<byte> second_pred) =>
        sad8xn_avg_sse2(src_ptr, src_stride, ref_ptr, ref_stride, second_pred, 8 / 4);

    // SAD8XN 16, 2                             ; sad_skip_8x16_sse2
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad_skip_8x16_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad8xn_sse2(src_ptr, src_stride * 2, ref_ptr, ref_stride * 2, 16 / 8) << 1;

    // SAD4XN 8 / 4                             ; sad4x8_sse2, sad4x4_sse2
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad4x8_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad4xn_sse2(src_ptr, src_stride, ref_ptr, ref_stride, 8 / 4);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static uint aom_sad4x4_sse2(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr, int ref_stride) =>
        sad4xn_sse2(src_ptr, src_stride, ref_ptr, ref_stride, 4 / 4);
}
