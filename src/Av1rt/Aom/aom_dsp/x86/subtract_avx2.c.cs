// Port of libaom aom_dsp/x86/subtract_avx2.c

global using static Av1rt.Aom.subtract_avx2;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static class subtract_avx2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void subtract_block_4xn_avx2(
        int rows, Ptr<short> diff_ptr, int diff_stride, Ptr<byte> src_ptr,
        int src_stride, Ptr<byte> pred_ptr, int pred_stride)
    {
        Debug.Assert(rows >= 2 && (rows & 1) == 0);
        int j = rows >> 1;
        // Process 2 rows of 4 pixels per iteration using 128-bit XMM registers
        // rather than 4 rows in a 256-bit YMM register:
        // 1. xx_loadl_32 + _mm_unpacklo_epi32 loads directly from memory into XMM
        //    without GPR-to-XMM inserts (_mm_set_epi32 / vpinsrd on Port 5).
        // 2. Staying in a single 128-bit lane avoids the cross-lane latency of
        //    _mm256_cvtepu8_epi16 and _mm256_extracti128_si256 needed for 8-byte
        //    per-row stores.
        // 3. Stepping pointers by stride each row avoids 3 * stride LEA addressing
        //    and eliminates a separate 2-row remainder loop.
        do
        {
            __m128i s0 = xx_loadl_32(src_ptr);
            src_ptr += src_stride;
            __m128i s1 = xx_loadl_32(src_ptr);
            src_ptr += src_stride;
            __m128i p0 = xx_loadl_32(pred_ptr);
            pred_ptr += pred_stride;
            __m128i p1 = xx_loadl_32(pred_ptr);
            pred_ptr += pred_stride;

            __m128i d =
                _mm_sub_epi16(_mm_cvtepu8_epi16(_mm_unpacklo_epi32(s0, s1)),
                              _mm_cvtepu8_epi16(_mm_unpacklo_epi32(p0, p1)));

            _mm_storel_epi64(diff_ptr, d);
            diff_ptr += diff_stride;
            _mm_storeh_pi(diff_ptr, _mm_castsi128_ps(d));
            diff_ptr += diff_stride;
        } while (--j != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void subtract_block_8xn_avx2(
        int rows, Ptr<short> diff_ptr, int diff_stride, Ptr<byte> src_ptr,
        int src_stride, Ptr<byte> pred_ptr, int pred_stride)
    {
        Debug.Assert(rows >= 2 && (rows & 1) == 0);
        int j = rows >> 1;
        // Process 2 rows of 8 pixels per iteration using a 256-bit register.
        do
        {
            __m128i s0 = _mm_loadl_epi64(src_ptr);
            src_ptr += src_stride;
            __m128i s1 = _mm_loadl_epi64(src_ptr);
            src_ptr += src_stride;
            __m128i p0 = _mm_loadl_epi64(pred_ptr);
            pred_ptr += pred_stride;
            __m128i p1 = _mm_loadl_epi64(pred_ptr);
            pred_ptr += pred_stride;

            __m256i d01 =
                _mm256_sub_epi16(_mm256_cvtepu8_epi16(_mm_unpacklo_epi64(s0, s1)),
                                 _mm256_cvtepu8_epi16(_mm_unpacklo_epi64(p0, p1)));

            _mm_storeu_si128(diff_ptr, _mm256_castsi256_si128(d01));
            diff_ptr += diff_stride;
            _mm_storeu_si128(diff_ptr, _mm256_extracti128_si256(d01, 1));
            diff_ptr += diff_stride;
        } while (--j != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void subtract32_avx2(Ptr<short> diff_ptr, Ptr<byte> src_ptr,
                                        Ptr<byte> pred_ptr)
    {
        __m256i s = _mm256_lddqu_si256(src_ptr);
        __m256i p = _mm256_lddqu_si256(pred_ptr);
        __m256i set_one_minusone = _mm256_set1_epi32(unchecked((int)0xff01ff01));
        __m256i diff0 = _mm256_unpacklo_epi8(s, p);
        __m256i diff1 = _mm256_unpackhi_epi8(s, p);
        diff0 = _mm256_maddubs_epi16(diff0, set_one_minusone);
        diff1 = _mm256_maddubs_epi16(diff1, set_one_minusone);
        _mm256_store_si256(diff_ptr,
                           _mm256_permute2x128_si256(diff0, diff1, 0x20));
        _mm256_store_si256(diff_ptr + 16,
                           _mm256_permute2x128_si256(diff0, diff1, 0x31));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void subtract_block_16xn_avx2(
        int rows, Ptr<short> diff_ptr, int diff_stride, Ptr<byte> src_ptr,
        int src_stride, Ptr<byte> pred_ptr, int pred_stride)
    {
        Debug.Assert(rows >= 2 && (rows & 1) == 0);
        int j = rows >> 1;
        do
        {
            __m128i s0 = _mm_lddqu_si128(src_ptr);
            src_ptr += src_stride;
            __m128i s1 = _mm_lddqu_si128(src_ptr);
            src_ptr += src_stride;
            __m128i p0 = _mm_lddqu_si128(pred_ptr);
            pred_ptr += pred_stride;
            __m128i p1 = _mm_lddqu_si128(pred_ptr);
            pred_ptr += pred_stride;
            __m256i d0 =
                _mm256_sub_epi16(_mm256_cvtepu8_epi16(s0), _mm256_cvtepu8_epi16(p0));
            __m256i d1 =
                _mm256_sub_epi16(_mm256_cvtepu8_epi16(s1), _mm256_cvtepu8_epi16(p1));
            _mm256_store_si256(diff_ptr, d0);
            diff_ptr += diff_stride;
            _mm256_store_si256(diff_ptr, d1);
            diff_ptr += diff_stride;
        } while (--j != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void subtract_block_32xn_avx2(
        int rows, Ptr<short> diff_ptr, int diff_stride, Ptr<byte> src_ptr,
        int src_stride, Ptr<byte> pred_ptr, int pred_stride)
    {
        Debug.Assert(rows > 0);
        int j = rows;
        do
        {
            subtract32_avx2(diff_ptr, src_ptr, pred_ptr);
            src_ptr += src_stride;
            pred_ptr += pred_stride;
            diff_ptr += diff_stride;
        } while (--j != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void subtract_block_64xn_avx2(
        int rows, Ptr<short> diff_ptr, int diff_stride, Ptr<byte> src_ptr,
        int src_stride, Ptr<byte> pred_ptr, int pred_stride)
    {
        Debug.Assert(rows > 0);
        int j = rows;
        do
        {
            subtract32_avx2(diff_ptr, src_ptr, pred_ptr);
            subtract32_avx2(diff_ptr + 32, src_ptr + 32, pred_ptr + 32);
            src_ptr += src_stride;
            pred_ptr += pred_stride;
            diff_ptr += diff_stride;
        } while (--j != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void subtract_block_128xn_avx2(
        int rows, Ptr<short> diff_ptr, int diff_stride, Ptr<byte> src_ptr,
        int src_stride, Ptr<byte> pred_ptr, int pred_stride)
    {
        Debug.Assert(rows > 0);
        int j = rows;
        do
        {
            subtract32_avx2(diff_ptr, src_ptr, pred_ptr);
            subtract32_avx2(diff_ptr + 32, src_ptr + 32, pred_ptr + 32);
            subtract32_avx2(diff_ptr + 64, src_ptr + 64, pred_ptr + 64);
            subtract32_avx2(diff_ptr + 96, src_ptr + 96, pred_ptr + 96);
            src_ptr += src_stride;
            pred_ptr += pred_stride;
            diff_ptr += diff_stride;
        } while (--j != 0);
    }

    internal static void aom_subtract_block_avx2(int rows, int cols, Ptr<short> diff_ptr,
                                                 int diff_stride, Ptr<byte> src_ptr,
                                                 int src_stride, Ptr<byte> pred_ptr,
                                                 int pred_stride)
    {
        // [port] The 32-byte alignment assert on diff_ptr / diff_stride (cols >= 16) is about
        // the aligned stores in C; managed stores here are unaligned.
        switch (cols)
        {
            case 4:
                subtract_block_4xn_avx2(rows, diff_ptr, diff_stride, src_ptr, src_stride,
                                        pred_ptr, pred_stride);
                break;
            case 8:
                subtract_block_8xn_avx2(rows, diff_ptr, diff_stride, src_ptr, src_stride,
                                        pred_ptr, pred_stride);
                break;
            case 16:
                subtract_block_16xn_avx2(rows, diff_ptr, diff_stride, src_ptr, src_stride,
                                         pred_ptr, pred_stride);
                break;
            case 32:
                subtract_block_32xn_avx2(rows, diff_ptr, diff_stride, src_ptr, src_stride,
                                         pred_ptr, pred_stride);
                break;
            case 64:
                subtract_block_64xn_avx2(rows, diff_ptr, diff_stride, src_ptr, src_stride,
                                         pred_ptr, pred_stride);
                break;
            default:
                Debug.Assert(cols == 128);
                subtract_block_128xn_avx2(rows, diff_ptr, diff_stride, src_ptr,
                                          src_stride, pred_ptr, pred_stride);
                break;
        }
    }
}
