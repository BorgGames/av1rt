// Port of libaom av1/encoder/x86/encodetxb_avx2.c

global using static Av1rt.Aom.encodetxb_avx2;

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

// PORT-NOTE: bit-exact with av1_txb_init_levels_c for coefficients > -32768. The 16-bit
// saturating pack turns coeff <= -32768 into abs_epi16(-32768) = -32768, which packs to level 128
// instead of the C clamp's 127. The encoder never produces such a coefficient (every 8-bit
// quantizer clamps the magnitude to INT16_MAX before scaling, so |qcoeff| <= 32767).
// Unlike the C version it does not zero the TX_PAD_END bytes after the bottom padding rows;
// nothing in the encoder reads them (av1_get_nz_map_contexts and get_br_ctx stay inside the
// TX_PAD_HOR / TX_PAD_BOTTOM padding).
internal static partial class encodetxb_avx2
{
    internal static void av1_txb_init_levels_avx2(Ptr<tran_low_t> coeff, int width,
                                                  int height, Ptr<byte> levels)
    {
        Debug.Assert(TX_PAD_HOR == 4 && TX_PAD_BOTTOM == 4);
        __m256i y_zeros = _mm256_setzero_si256();
        __m128i zero = _mm256_castsi256_si128(y_zeros);

        // [port] Through spans: each iteration slices its coefficients and level bytes once, so
        // the loads and stores at constant offsets inside need no further checks.
        int stride = height + TX_PAD_HOR;
        ReadOnlySpan<tran_low_t> cs = coeff.AsSpan(0, width * height);
        Span<byte> lv = levels.AsSpan(0, (width + TX_PAD_BOTTOM) * stride);
        int ci = 0, li = 0;

        // Specialize each transform height with its constant padded stride
        // (height + TX_PAD_HOR) and zero the TX_PAD_BOTTOM (4) trailing rows directly
        // after the loop.
        if (height == 4)
        {
            Debug.Assert(width > 0 && width % 4 == 0);
            // Padded stride = 4 + 4 = 8; 4 rows of 4 coeffs + 4 zero-pad bytes per
            // iter.
            for (int i = 0; i < width; i += 4)
            {
                ReadOnlySpan<tran_low_t> c = cs.Slice(ci, 16);
                __m256i c0 = yy_loadu_256(c);
                __m256i c1 = yy_loadu_256(c.Slice(8));
                __m256i abs01 = _mm256_abs_epi16(_mm256_packs_epi32(c0, c1));
                __m256i abs01_8 = _mm256_packs_epi16(abs01, y_zeros);
                __m256i res_ = _mm256_shuffle_epi32(abs01_8, 0xd8);
                __m256i res = _mm256_permute4x64_epi64(res_, 0xd8);
                yy_storeu_256(lv.Slice(li, 32), res);
                li += 32;
                ci += 16;
            }
            // Zero 4 bottom padding rows (4 * 8 = 32 bytes).
            lv.Slice(li, 32).Clear();
        }
        else if (height == 8)
        {
            Debug.Assert(width > 0 && width % 4 == 0);
            // Padded stride = 8 + 4 = 12; 4 rows per iteration (4 * 12 = 48 bytes).
            for (int i = 0; i < width; i += 4)
            {
                ReadOnlySpan<tran_low_t> c = cs.Slice(ci, 32);
                __m256i coeffA = yy_loadu_256(c);
                __m256i coeffB = yy_loadu_256(c.Slice(8));
                __m256i coeffC = yy_loadu_256(c.Slice(16));
                __m256i coeffD = yy_loadu_256(c.Slice(24));
                __m256i coeffAB = _mm256_packs_epi32(coeffA, coeffB);
                __m256i coeffCD = _mm256_packs_epi32(coeffC, coeffD);
                __m256i absAB = _mm256_abs_epi16(coeffAB);
                __m256i absCD = _mm256_abs_epi16(coeffCD);
                __m256i absABCD = _mm256_packs_epi16(absAB, absCD);
                __m256i res_ = _mm256_permute4x64_epi64(absABCD, 0xd8);
                __m256i res = _mm256_shuffle_epi32(res_, 0xd8);
                __m128i res0 = _mm256_castsi256_si128(res);
                __m128i res1 = _mm256_extracti128_si256(res, 1);

                Span<byte> l = lv.Slice(li, 48);
                xx_storel_64(l, res0);
                xx_storel_32(l.Slice(8), zero);
                xx_storel_64(l.Slice(12), _mm_srli_si128(res0, 8));
                xx_storel_32(l.Slice(20), zero);
                xx_storel_64(l.Slice(24), res1);
                xx_storel_32(l.Slice(32), zero);
                xx_storel_64(l.Slice(36), _mm_srli_si128(res1, 8));
                xx_storel_32(l.Slice(44), zero);
                ci += 32;
                li += 48;
            }
            // Zero 4 bottom padding rows (4 * 12 = 48 bytes).
            lv.Slice(li, 48).Clear();
        }
        else if (height == 16)
        {
            Debug.Assert(width > 0 && width % 2 == 0);
            // Padded stride = 16 + 4 = 20; 2 rows per iteration (2 * 20 = 40 bytes).
            for (int i = 0; i < width; i += 2)
            {
                ReadOnlySpan<tran_low_t> c = cs.Slice(ci, 32);
                __m256i coeffA = yy_loadu_256(c);
                __m256i coeffB = yy_loadu_256(c.Slice(8));
                __m256i coeffC = yy_loadu_256(c.Slice(16));
                __m256i coeffD = yy_loadu_256(c.Slice(24));
                __m256i coeffAB = _mm256_packs_epi32(coeffA, coeffB);
                __m256i coeffCD = _mm256_packs_epi32(coeffC, coeffD);
                __m256i absAB = _mm256_abs_epi16(coeffAB);
                __m256i absCD = _mm256_abs_epi16(coeffCD);
                __m256i absABCD = _mm256_packs_epi16(absAB, absCD);
                __m256i res_ = _mm256_permute4x64_epi64(absABCD, 0xd8);
                __m256i res = _mm256_shuffle_epi32(res_, 0xd8);

                Span<byte> l = lv.Slice(li, 40);
                xx_storeu_128(l, _mm256_castsi256_si128(res));
                xx_storel_32(l.Slice(16), zero);
                xx_storeu_128(l.Slice(20), _mm256_extracti128_si256(res, 1));
                xx_storel_32(l.Slice(36), zero);
                ci += 32;
                li += 40;
            }
            // Zero 4 bottom padding rows (4 * 20 = 80 bytes).
            lv.Slice(li, 80).Clear();
        }
        else
        {
            Debug.Assert(height == 32 && width > 0);
            // Padded stride = 32 + 4 = 36; 1 row per iteration (36 bytes).
            for (int i = 0; i < width; i++)
            {
                ReadOnlySpan<tran_low_t> c = cs.Slice(ci, 32);
                __m256i coeffA = yy_loadu_256(c);
                __m256i coeffB = yy_loadu_256(c.Slice(8));
                __m256i coeffC = yy_loadu_256(c.Slice(16));
                __m256i coeffD = yy_loadu_256(c.Slice(24));
                __m256i coeffAB = _mm256_packs_epi32(coeffA, coeffB);
                __m256i coeffCD = _mm256_packs_epi32(coeffC, coeffD);
                __m256i absAB = _mm256_abs_epi16(coeffAB);
                __m256i absCD = _mm256_abs_epi16(coeffCD);
                __m256i absABCD = _mm256_packs_epi16(absAB, absCD);
                __m256i res_ = _mm256_permute4x64_epi64(absABCD, 0xd8);
                __m256i res = _mm256_shuffle_epi32(res_, 0xd8);

                Span<byte> l = lv.Slice(li, 36);
                yy_storeu_256(l, res);
                xx_storel_32(l.Slice(32), zero);
                ci += 32;
                li += 36;
            }
            // Zero 4 bottom padding rows (4 * 36 = 144 bytes).
            lv.Slice(li, 144).Clear();
        }
    }
}
