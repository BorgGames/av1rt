// Port of libaom av1/common/x86/av1_inv_txfm_avx2.h

global using static Av1rt.Aom.av1_inv_txfm_avx2;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class av1_inv_txfm_avx2
{
    // half input is zero
    // [port] Macro -> method; `in` is read before out0/out1 are written, as in the macro.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void btf_16_w16_0_avx2(int w0, int w1, __m256i @in, ref __m256i out0, ref __m256i out1)
    {
        __m256i _w0 = _mm256_set1_epi16(w0 * 8);
        __m256i _w1 = _mm256_set1_epi16(w1 * 8);
        __m256i _in = @in;
        out0 = _mm256_mulhrs_epi16(_in, _w0);
        out1 = _mm256_mulhrs_epi16(_in, _w1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void round_shift_avx2(Span<__m256i> input, Span<__m256i> output,
                                          int size)
    {
        __m256i scale = _mm256_set1_epi16(NewInvSqrt2 * 8);
        for (int i = 0; i < size; ++i)
        {
            output[i] = _mm256_mulhrs_epi16(input[i], scale);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void write_recon_w16_avx2(__m256i res, Ptr<byte> output)
    {
        __m128i pred = _mm_loadu_si128(output);
        __m256i u = _mm256_adds_epi16(_mm256_cvtepu8_epi16(pred), res);
        __m128i y = _mm256_castsi256_si128(
            _mm256_permute4x64_epi64(_mm256_packus_epi16(u, u), 168));
        _mm_storeu_si128(output, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void lowbd_write_buffer_16xn_avx2(Span<__m256i> @in, Ptr<byte> output,
                                                      int stride, int flipud,
                                                      int height)
    {
        int j = flipud != 0 ? (height - 1) : 0;
        int step = flipud != 0 ? -1 : 1;
        for (int i = 0; i < height; ++i, j += step)
        {
            write_recon_w16_avx2(@in[j], output + i * stride);
        }
    }
}
