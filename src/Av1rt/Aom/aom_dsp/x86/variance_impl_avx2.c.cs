// Port of libaom aom_dsp/x86/variance_impl_avx2.c

global using static Av1rt.Aom.variance_impl_avx2;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] The statement macros (FILTER_SRC, MERGE_WITH_SRC, CALC_SUM_SSE_INSIDE_LOOP,
// CALC_SUM_AND_SSE, FILTER_SRC_128BIT) are aggressively inlined helpers taking the registers they
// update by ref; the load macros (LOAD_SRC_DST, AVG_NEXT_SRC, MERGE_NEXT_SRC, *_INSERT,
// LOAD_SRC_MERGE_128BIT) are expanded in place with the macro name in a comment. The function
// generating macros (MAKE_SUB_PIXEL_VAR_32XH, MAKE_SUB_PIXEL_VAR_16XH,
// MAKE_SUB_PIXEL_AVG_VAR_32XH, AOM_SUB_PIXEL_VAR_AVX2, AOM_SUB_PIXEL_AVG_VAR_AVX2) are one
// function each with the macro parameters as arguments, plus the instantiations
// (CONFIG_REALTIME_ONLY sizes) as one-line wrappers.
internal static class variance_impl_avx2
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    /* clang-format off */
    // DECLARE_ALIGNED(32, static const uint8_t, bilinear_filters_avx2[512])
    // [port] Only the 256 initialized bytes (8 filters x 32) are kept; offsets are <= 7 << 5.
    private static ReadOnlySpan<byte> bilinear_filters_avx2 => new byte[] {
      16,  0, 16,  0, 16,  0, 16,  0, 16,  0, 16,  0, 16,  0, 16,  0,
      16,  0, 16,  0, 16,  0, 16,  0, 16,  0, 16,  0, 16,  0, 16,  0,
      14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2,
      14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2,
      12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4,
      12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4,
      10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6,
      10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6,
       8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,
       8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,
       6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,
       6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,  6, 10,
       4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,
       4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,  4, 12,
       2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,
       2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,  2, 14,
    };
    /* clang-format on */

    [MethodImpl(AI)]
    private static void FILTER_SRC(__m256i filter, __m256i pw8,
                                   ref __m256i exp_src_lo, ref __m256i exp_src_hi)
    {
        /* filter the source */
        exp_src_lo = _mm256_maddubs_epi16(exp_src_lo, filter);
        exp_src_hi = _mm256_maddubs_epi16(exp_src_hi, filter);

        /* add 8 to source */
        exp_src_lo = _mm256_add_epi16(exp_src_lo, pw8);
        exp_src_hi = _mm256_add_epi16(exp_src_hi, pw8);

        /* divide source by 16 */
        exp_src_lo = _mm256_srai_epi16(exp_src_lo, 4);
        exp_src_hi = _mm256_srai_epi16(exp_src_hi, 4);
    }

    [MethodImpl(AI)]
    private static void MERGE_WITH_SRC(__m256i src_reg, __m256i reg,
                                       ref __m256i exp_src_lo, ref __m256i exp_src_hi)
    {
        exp_src_lo = _mm256_unpacklo_epi8(src_reg, reg);
        exp_src_hi = _mm256_unpackhi_epi8(src_reg, reg);
    }

    [MethodImpl(AI)]
    private static void CALC_SUM_SSE_INSIDE_LOOP(__m256i dst_reg, __m256i zero_reg,
                                                 ref __m256i exp_src_lo, ref __m256i exp_src_hi,
                                                 ref __m256i sum_reg, ref __m256i sse_reg)
    {
        /* expand each byte to 2 bytes */
        __m256i exp_dst_lo = _mm256_unpacklo_epi8(dst_reg, zero_reg);
        __m256i exp_dst_hi = _mm256_unpackhi_epi8(dst_reg, zero_reg);
        /* source - dest */
        exp_src_lo = _mm256_sub_epi16(exp_src_lo, exp_dst_lo);
        exp_src_hi = _mm256_sub_epi16(exp_src_hi, exp_dst_hi);
        /* caculate sum */
        sum_reg = _mm256_add_epi16(sum_reg, exp_src_lo);
        exp_src_lo = _mm256_madd_epi16(exp_src_lo, exp_src_lo);
        sum_reg = _mm256_add_epi16(sum_reg, exp_src_hi);
        exp_src_hi = _mm256_madd_epi16(exp_src_hi, exp_src_hi);
        /* calculate sse */
        sse_reg = _mm256_add_epi32(sse_reg, exp_src_lo);
        sse_reg = _mm256_add_epi32(sse_reg, exp_src_hi);
    }

    // final calculation to sum and sse
    // [port] Returns `sum`.
    [MethodImpl(AI)]
    private static int CALC_SUM_AND_SSE(__m256i sse_reg, __m256i sum_reg, __m256i zero_reg,
                                        ref uint sse)
    {
        __m256i res_cmp = _mm256_cmpgt_epi16(zero_reg, sum_reg);
        __m256i sse_reg_hi = _mm256_srli_si256(sse_reg, 8);
        __m256i sum_reg_lo = _mm256_unpacklo_epi16(sum_reg, res_cmp);
        __m256i sum_reg_hi = _mm256_unpackhi_epi16(sum_reg, res_cmp);
        sse_reg = _mm256_add_epi32(sse_reg, sse_reg_hi);
        sum_reg = _mm256_add_epi32(sum_reg_lo, sum_reg_hi);

        sse_reg_hi = _mm256_srli_si256(sse_reg, 4);
        sum_reg_hi = _mm256_srli_si256(sum_reg, 8);

        sse_reg = _mm256_add_epi32(sse_reg, sse_reg_hi);
        sum_reg = _mm256_add_epi32(sum_reg, sum_reg_hi);
        sse = (uint)(_mm_cvtsi128_si32(_mm256_castsi256_si128(sse_reg)) +
                     _mm_cvtsi128_si32(_mm256_extractf128_si256(sse_reg, 1)));
        sum_reg_hi = _mm256_srli_si256(sum_reg, 4);
        sum_reg = _mm256_add_epi32(sum_reg, sum_reg_hi);
        return _mm_cvtsi128_si32(_mm256_castsi256_si128(sum_reg)) +
               _mm_cvtsi128_si32(_mm256_extractf128_si256(sum_reg, 1));
    }

    [MethodImpl(AI)]
    private static void FILTER_SRC_128BIT(__m128i filter, __m128i pw8_128bit,
                                          ref __m128i src_lo, ref __m128i src_hi)
    {
        /* filter the source */
        src_lo = _mm_maddubs_epi16(src_lo, filter);
        src_hi = _mm_maddubs_epi16(src_hi, filter);

        /* add 8 to source */
        src_lo = _mm_add_epi16(src_lo, pw8_128bit);
        src_hi = _mm_add_epi16(src_hi, pw8_128bit);

        /* divide source by 16 */
        src_lo = _mm_srai_epi16(src_lo, 4);
        src_hi = _mm_srai_epi16(src_hi, 4);
    }

    // [port] Horizontal bilinear filter of 32 pixels into the even/odd byte layout of the
    // xy-bilinear loops below (the repeated `{ s0, s1, he, ho, p = packus(he, ho) }` blocks).
    [MethodImpl(AI)]
    private static __m256i filter_row_eo_32(Ptr<byte> src_ptr, __m256i xfilter, __m256i pw8)
    {
        __m256i s0 = _mm256_loadu_si256(src_ptr);
        __m256i s1 = _mm256_loadu_si256(src_ptr + 1);
        __m256i he = _mm256_maddubs_epi16(s0, xfilter);
        __m256i ho = _mm256_maddubs_epi16(s1, xfilter);
        he = _mm256_srai_epi16(_mm256_add_epi16(he, pw8), 4);
        ho = _mm256_srai_epi16(_mm256_add_epi16(ho, pw8), 4);
        return _mm256_packus_epi16(he, ho);
    }

    // [port] The same for two rows of 16 pixels (one per 128-bit lane).
    [MethodImpl(AI)]
    private static __m256i filter_rows_eo_16x2(Ptr<byte> src_ptr, int src_stride, __m256i xfilter,
                                               __m256i pw8)
    {
        __m256i s0 = _mm256_inserti128_si256(
            _mm256_castsi128_si256(_mm_loadu_si128(src_ptr)),
            _mm_loadu_si128(src_ptr + src_stride), 1);
        __m256i s1 = _mm256_inserti128_si256(
            _mm256_castsi128_si256(_mm_loadu_si128(src_ptr + 1)),
            _mm_loadu_si128(src_ptr + src_stride + 1), 1);
        __m256i he = _mm256_maddubs_epi16(s0, xfilter);
        __m256i ho = _mm256_maddubs_epi16(s1, xfilter);
        he = _mm256_srai_epi16(_mm256_add_epi16(he, pw8), 4);
        ho = _mm256_srai_epi16(_mm256_add_epi16(ho, pw8), 4);
        return _mm256_packus_epi16(he, ho);
    }

    // [port] Vertical filter of two even/odd rows against one dst row (pair) and accumulation
    // (the repeated v_ev / v_od / dst_ev / dst_od / diff blocks of the xy-bilinear loops).
    [MethodImpl(AI)]
    private static void filter_col_eo_accumulate(__m256i pa, __m256i pb, __m256i dst_reg,
                                                 __m256i yfilter, __m256i pw8, __m256i mask_00ff,
                                                 out __m256i diff_ev, out __m256i diff_od)
    {
        __m256i v_ev = _mm256_maddubs_epi16(_mm256_unpacklo_epi8(pa, pb), yfilter);
        __m256i v_od = _mm256_maddubs_epi16(_mm256_unpackhi_epi8(pa, pb), yfilter);
        v_ev = _mm256_srai_epi16(_mm256_add_epi16(v_ev, pw8), 4);
        v_od = _mm256_srai_epi16(_mm256_add_epi16(v_od, pw8), 4);

        __m256i dst_ev = _mm256_and_si256(dst_reg, mask_00ff);
        __m256i dst_od = _mm256_srli_epi16(dst_reg, 8);
        diff_ev = _mm256_sub_epi16(v_ev, dst_ev);
        diff_od = _mm256_sub_epi16(v_od, dst_od);
    }

    // [port] The final reduction of the xy-bilinear branches: returns f_sum, sets *sse = f_sse.
    [MethodImpl(AI)]
    private static int final_eo_sum_sse(__m256i sse_reg, __m256i sum_reg, ref uint sse)
    {
        __m256i sse_hi, sum_hi;
        int f_sse, f_sum;
        sum_reg = _mm256_madd_epi16(sum_reg, _mm256_set1_epi16(1));
        sse_hi = _mm256_srli_si256(sse_reg, 8);
        sum_hi = _mm256_srli_si256(sum_reg, 8);
        sse_reg = _mm256_add_epi32(sse_reg, sse_hi);
        sum_reg = _mm256_add_epi32(sum_reg, sum_hi);
        sse_hi = _mm256_srli_si256(sse_reg, 4);
        sum_hi = _mm256_srli_si256(sum_reg, 4);
        sse_reg = _mm256_add_epi32(sse_reg, sse_hi);
        sum_reg = _mm256_add_epi32(sum_reg, sum_hi);
        f_sse = _mm_cvtsi128_si32(_mm256_castsi256_si128(sse_reg)) +
                _mm_cvtsi128_si32(_mm256_extractf128_si256(sse_reg, 1));
        f_sum = _mm_cvtsi128_si32(_mm256_castsi256_si128(sum_reg)) +
                _mm_cvtsi128_si32(_mm256_extractf128_si256(sum_reg, 1));
        sse = (uint)f_sse;
        _mm256_zeroupper();
        return f_sum;
    }

    // MAKE_SUB_PIXEL_VAR_32XH(height, log2height): aom_sub_pixel_variance32x##height##_imp_avx2
    private static int aom_sub_pixel_variance32xh_imp_avx2(
        Ptr<byte> src, int src_stride, int x_offset, int y_offset,
        Ptr<byte> dst, int dst_stride, ref uint sse, int height)
    {
        __m256i src_reg, dst_reg, exp_src_lo = default, exp_src_hi = default;
        __m256i sse_reg, sum_reg;
        __m256i zero_reg;
        int i, sum;
        sum_reg = _mm256_setzero_si256();
        sse_reg = _mm256_setzero_si256();
        zero_reg = _mm256_setzero_si256();

        /* x_offset = 0 and y_offset = 0 */
        if (x_offset == 0)
        {
            if (y_offset == 0)
            {
                for (i = 0; i < height; i++)
                {
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    /* expend each byte to 2 bytes */
                    MERGE_WITH_SRC(src_reg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += src_stride;
                    dst += dst_stride;
                }
                /* x_offset = 0 and y_offset = 4 */
            }
            else if (y_offset == 4)
            {
                __m256i src_next_reg;
                for (i = 0; i < height; i++)
                {
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // AVG_NEXT_SRC(src_reg, src_stride)
                    src_next_reg = _mm256_loadu_si256(src + src_stride);
                    src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    /* expend each byte to 2 bytes */
                    MERGE_WITH_SRC(src_reg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += src_stride;
                    dst += dst_stride;
                }
                /* x_offset = 0 and y_offset = bilin interpolation */
            }
            else
            {
                __m256i filter, pw8, src_next_reg;

                y_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(y_offset));
                pw8 = _mm256_set1_epi16(8);
                for (i = 0; i < height; i++)
                {
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // MERGE_NEXT_SRC(src_reg, src_stride)
                    src_next_reg = _mm256_loadu_si256(src + src_stride);
                    MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += src_stride;
                    dst += dst_stride;
                }
            }
            /* x_offset = 4  and y_offset = 0 */
        }
        else if (x_offset == 4)
        {
            if (y_offset == 0)
            {
                __m256i src_next_reg;
                for (i = 0; i < height; i++)
                {
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // AVG_NEXT_SRC(src_reg, 1)
                    src_next_reg = _mm256_loadu_si256(src + 1);
                    src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    /* expand each byte to 2 bytes */
                    MERGE_WITH_SRC(src_reg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += src_stride;
                    dst += dst_stride;
                }
                /* x_offset = 4  and y_offset = 4 */
            }
            else if (y_offset == 4)
            {
                __m256i src_next_reg, src_avg;
                /* load source and another source starting from the next */
                /* following byte */
                src_reg = _mm256_loadu_si256(src);
                // AVG_NEXT_SRC(src_reg, 1)
                src_next_reg = _mm256_loadu_si256(src + 1);
                src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                for (i = 0; i < height; i++)
                {
                    src_avg = src_reg;
                    src += src_stride;
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // AVG_NEXT_SRC(src_reg, 1)
                    src_next_reg = _mm256_loadu_si256(src + 1);
                    src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    /* average between previous average to current average */
                    src_avg = _mm256_avg_epu8(src_avg, src_reg);
                    /* expand each byte to 2 bytes */
                    MERGE_WITH_SRC(src_avg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    /* save current source average */
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    dst += dst_stride;
                }
                /* x_offset = 4  and y_offset = bilin interpolation */
            }
            else
            {
                __m256i filter, pw8, src_next_reg, src_avg;
                y_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(y_offset));
                pw8 = _mm256_set1_epi16(8);
                /* load source and another source starting from the next */
                /* following byte */
                src_reg = _mm256_loadu_si256(src);
                // AVG_NEXT_SRC(src_reg, 1)
                src_next_reg = _mm256_loadu_si256(src + 1);
                src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                for (i = 0; i < height; i++)
                {
                    /* save current source average */
                    src_avg = src_reg;
                    src += src_stride;
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // AVG_NEXT_SRC(src_reg, 1)
                    src_next_reg = _mm256_loadu_si256(src + 1);
                    src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    MERGE_WITH_SRC(src_avg, src_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    dst += dst_stride;
                }
            }
            /* x_offset = bilin interpolation and y_offset = 0 */
        }
        else
        {
            if (y_offset == 0)
            {
                __m256i filter, pw8, src_next_reg;
                x_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(x_offset));
                pw8 = _mm256_set1_epi16(8);
                for (i = 0; i < height; i++)
                {
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // MERGE_NEXT_SRC(src_reg, 1)
                    src_next_reg = _mm256_loadu_si256(src + 1);
                    MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += src_stride;
                    dst += dst_stride;
                }
                /* x_offset = bilin interpolation and y_offset = 4 */
            }
            else if (y_offset == 4)
            {
                __m256i filter, pw8, src_next_reg, src_pack;
                x_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(x_offset));
                pw8 = _mm256_set1_epi16(8);
                src_reg = _mm256_loadu_si256(src);
                // MERGE_NEXT_SRC(src_reg, 1)
                src_next_reg = _mm256_loadu_si256(src + 1);
                MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                /* convert each 16 bit to 8 bit to each low and high lane source */
                src_pack = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                for (i = 0; i < height; i++)
                {
                    src += src_stride;
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // MERGE_NEXT_SRC(src_reg, 1)
                    src_next_reg = _mm256_loadu_si256(src + 1);
                    MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    src_reg = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                    /* average between previous pack to the current */
                    src_pack = _mm256_avg_epu8(src_pack, src_reg);
                    MERGE_WITH_SRC(src_pack, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src_pack = src_reg;
                    dst += dst_stride;
                }
                /* x_offset = bilin interpolation and y_offset = bilin interpolation
                 */
            }
            else
            {
                __m256i xfilter, yfilter, pw8, mask_00ff;
                __m256i p0, p1, p2;
                Ptr<byte> src_ptr = src;
                x_offset <<= 5;
                xfilter = _mm256_load_si256(bilinear_filters_avx2.Slice(x_offset));
                y_offset <<= 5;
                yfilter = _mm256_load_si256(bilinear_filters_avx2.Slice(y_offset));
                pw8 = _mm256_set1_epi16(8);
                mask_00ff = _mm256_set1_epi16(0x00ff);

                p0 = filter_row_eo_32(src_ptr, xfilter, pw8);
                src_ptr += src_stride;
                p1 = filter_row_eo_32(src_ptr, xfilter, pw8);
                src_ptr += src_stride;
                p2 = filter_row_eo_32(src_ptr, xfilter, pw8);
                src_ptr += src_stride;

                for (i = 0; i < height - 2; i += 2)
                {
                    __m256i p3, p4, dst_A, diff_A_ev, diff_A_od, dst_B, diff_B_ev, diff_B_od;
                    __m256i sum_comb, sse_comb;

                    p3 = filter_row_eo_32(src_ptr, xfilter, pw8);
                    src_ptr += src_stride;
                    p4 = filter_row_eo_32(src_ptr, xfilter, pw8);
                    src_ptr += src_stride;

                    dst_A = _mm256_loadu_si256(dst);
                    dst += dst_stride;
                    filter_col_eo_accumulate(p0, p1, dst_A, yfilter, pw8, mask_00ff,
                                             out diff_A_ev, out diff_A_od);

                    dst_B = _mm256_loadu_si256(dst);
                    dst += dst_stride;
                    filter_col_eo_accumulate(p1, p2, dst_B, yfilter, pw8, mask_00ff,
                                             out diff_B_ev, out diff_B_od);

                    sum_comb = _mm256_add_epi16(_mm256_add_epi16(diff_A_ev, diff_A_od),
                                                _mm256_add_epi16(diff_B_ev, diff_B_od));
                    sum_reg = _mm256_add_epi16(sum_reg, sum_comb);

                    sse_comb = _mm256_add_epi32(
                        _mm256_add_epi32(_mm256_madd_epi16(diff_A_ev, diff_A_ev),
                                         _mm256_madd_epi16(diff_A_od, diff_A_od)),
                        _mm256_add_epi32(_mm256_madd_epi16(diff_B_ev, diff_B_ev),
                                         _mm256_madd_epi16(diff_B_od, diff_B_od)));
                    sse_reg = _mm256_add_epi32(sse_reg, sse_comb);

                    p0 = p2;
                    p1 = p3;
                    p2 = p4;
                }

                {
                    __m256i dst_A, diff_A_ev, diff_A_od, dst_B, diff_B_ev, diff_B_od;
                    __m256i sum_comb, sse_comb;

                    dst_A = _mm256_loadu_si256(dst);
                    dst += dst_stride;
                    filter_col_eo_accumulate(p0, p1, dst_A, yfilter, pw8, mask_00ff,
                                             out diff_A_ev, out diff_A_od);

                    dst_B = _mm256_loadu_si256(dst);
                    dst += dst_stride;
                    filter_col_eo_accumulate(p1, p2, dst_B, yfilter, pw8, mask_00ff,
                                             out diff_B_ev, out diff_B_od);

                    sum_comb = _mm256_add_epi16(_mm256_add_epi16(diff_A_ev, diff_A_od),
                                                _mm256_add_epi16(diff_B_ev, diff_B_od));
                    sum_reg = _mm256_add_epi16(sum_reg, sum_comb);

                    sse_comb = _mm256_add_epi32(
                        _mm256_add_epi32(_mm256_madd_epi16(diff_A_ev, diff_A_ev),
                                         _mm256_madd_epi16(diff_A_od, diff_A_od)),
                        _mm256_add_epi32(_mm256_madd_epi16(diff_B_ev, diff_B_ev),
                                         _mm256_madd_epi16(diff_B_od, diff_B_od)));
                    sse_reg = _mm256_add_epi32(sse_reg, sse_comb);
                }

                return final_eo_sum_sse(sse_reg, sum_reg, ref sse);
            }
        }
        sum = CALC_SUM_AND_SSE(sse_reg, sum_reg, zero_reg, ref sse);
        _mm256_zeroupper();
        return sum;
    }

    // MAKE_SUB_PIXEL_VAR_32XH(64, 6), (32, 5), (16, 4)
    private static int aom_sub_pixel_variance32x64_imp_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                            Ptr<byte> dst, int dst_stride, ref uint sse) =>
        aom_sub_pixel_variance32xh_imp_avx2(src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse, 64);

    private static int aom_sub_pixel_variance32x32_imp_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                            Ptr<byte> dst, int dst_stride, ref uint sse) =>
        aom_sub_pixel_variance32xh_imp_avx2(src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse, 32);

    internal static uint aom_sub_pixel_variance32x64_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                          Ptr<byte> dst, int dst_stride, ref uint sse)
    {
        int sum = aom_sub_pixel_variance32xh_imp_avx2(src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse, 64);
        return sse - (uint)(((long)sum * sum) >> (5 + 6));
    }

    internal static uint aom_sub_pixel_variance32x32_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                          Ptr<byte> dst, int dst_stride, ref uint sse)
    {
        int sum = aom_sub_pixel_variance32xh_imp_avx2(src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse, 32);
        return sse - (uint)(((long)sum * sum) >> (5 + 5));
    }

    internal static uint aom_sub_pixel_variance32x16_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                          Ptr<byte> dst, int dst_stride, ref uint sse)
    {
        int sum = aom_sub_pixel_variance32xh_imp_avx2(src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse, 16);
        return sse - (uint)(((long)sum * sum) >> (5 + 4));
    }

    // AOM_SUB_PIXEL_VAR_AVX2(w, h, wf, hf, wlog2, hlog2) with wf = 32 (the only instantiated
    // value): the helper is aom_sub_pixel_variance32x##hf##_imp_avx2.
    [MethodImpl(AI)]
    private static uint AOM_SUB_PIXEL_VAR_AVX2(int w, int h, int hf, int wlog2, int hlog2,
                                               Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                               Ptr<byte> dst, int dst_stride, ref uint sse_ptr)
    {
        const int wf = 32;
        uint sse = 0;
        int se = 0;
        for (int i = 0; i < (w / wf); ++i)
        {
            Ptr<byte> src_ptr = src;
            Ptr<byte> dst_ptr = dst;
            for (int j = 0; j < (h / hf); ++j)
            {
                uint sse2 = 0;
                int se2 = aom_sub_pixel_variance32xh_imp_avx2(
                    src_ptr, src_stride, x_offset, y_offset, dst_ptr, dst_stride,
                    ref sse2, hf);
                dst_ptr += hf * dst_stride;
                src_ptr += hf * src_stride;
                se += se2;
                sse += sse2;
            }
            src += wf;
            dst += wf;
        }
        sse_ptr = sse;
        return sse - (uint)(((long)se * se) >> (wlog2 + hlog2));
    }

    // Note: hf = AOMMIN(h, 64) to avoid overflow in helper by capping height.
    internal static uint aom_sub_pixel_variance128x128_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                            Ptr<byte> dst, int dst_stride, ref uint sse_ptr) =>
        AOM_SUB_PIXEL_VAR_AVX2(128, 128, 64, 7, 7, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr);

    internal static uint aom_sub_pixel_variance128x64_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                           Ptr<byte> dst, int dst_stride, ref uint sse_ptr) =>
        AOM_SUB_PIXEL_VAR_AVX2(128, 64, 64, 7, 6, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr);

    internal static uint aom_sub_pixel_variance64x128_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                           Ptr<byte> dst, int dst_stride, ref uint sse_ptr) =>
        AOM_SUB_PIXEL_VAR_AVX2(64, 128, 64, 6, 7, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr);

    internal static uint aom_sub_pixel_variance64x64_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                          Ptr<byte> dst, int dst_stride, ref uint sse_ptr) =>
        AOM_SUB_PIXEL_VAR_AVX2(64, 64, 64, 6, 6, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr);

    internal static uint aom_sub_pixel_variance64x32_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                          Ptr<byte> dst, int dst_stride, ref uint sse_ptr) =>
        AOM_SUB_PIXEL_VAR_AVX2(64, 32, 32, 6, 5, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr);

    // MAKE_SUB_PIXEL_VAR_16XH(height, log2height)
    private static uint aom_sub_pixel_variance16xh_avx2(
        Ptr<byte> src, int src_stride, int x_offset, int y_offset,
        Ptr<byte> dst, int dst_stride, ref uint sse, int height, int log2height)
    {
        __m256i src_reg, dst_reg, exp_src_lo = default, exp_src_hi = default;
        __m256i sse_reg, sum_reg;
        __m256i zero_reg;
        int i, sum;
        sum_reg = _mm256_setzero_si256();
        sse_reg = _mm256_setzero_si256();
        zero_reg = _mm256_setzero_si256();

        /* x_offset = 0 and y_offset = 0 */
        if (x_offset == 0)
        {
            if (y_offset == 0)
            {
                for (i = 0; i < height; i += 2)
                {
                    // LOAD_SRC_DST_INSERT(src_stride, dst_stride)
                    src_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src)),
                        _mm_loadu_si128(src + src_stride), 1);
                    dst_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                        _mm_loadu_si128(dst + dst_stride), 1);
                    /* expend each byte to 2 bytes */
                    MERGE_WITH_SRC(src_reg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += (src_stride << 1);
                    dst += (dst_stride << 1);
                }
                /* x_offset = 0 and y_offset = 4 */
            }
            else if (y_offset == 4)
            {
                __m256i src_next_reg;
                for (i = 0; i < height; i += 2)
                {
                    // LOAD_SRC_DST_INSERT(src_stride, dst_stride)
                    src_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src)),
                        _mm_loadu_si128(src + src_stride), 1);
                    dst_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                        _mm_loadu_si128(dst + dst_stride), 1);
                    // AVG_NEXT_SRC_INSERT(src_reg, src_stride)
                    src_next_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src + src_stride)),
                        _mm_loadu_si128(src + (src_stride << 1)), 1);
                    /* average between current and next stride source */
                    src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    /* expend each byte to 2 bytes */
                    MERGE_WITH_SRC(src_reg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += (src_stride << 1);
                    dst += (dst_stride << 1);
                }
                /* x_offset = 0 and y_offset = bilin interpolation */
            }
            else
            {
                __m256i filter, pw8, src_next_reg;
                y_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(y_offset));
                pw8 = _mm256_set1_epi16(8);
                for (i = 0; i < height; i += 2)
                {
                    // LOAD_SRC_DST_INSERT(src_stride, dst_stride)
                    src_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src)),
                        _mm_loadu_si128(src + src_stride), 1);
                    dst_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                        _mm_loadu_si128(dst + dst_stride), 1);
                    // MERGE_NEXT_SRC_INSERT(src_reg, src_stride)
                    src_next_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src + src_stride)),
                        _mm_loadu_si128(src + (src_stride + src_stride)), 1);
                    MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += (src_stride << 1);
                    dst += (dst_stride << 1);
                }
            }
            /* x_offset = 4  and y_offset = 0 */
        }
        else if (x_offset == 4)
        {
            if (y_offset == 0)
            {
                __m256i src_next_reg;
                for (i = 0; i < height; i += 2)
                {
                    // LOAD_SRC_NEXT_BYTE_INSERT
                    src_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src)),
                        _mm_loadu_si128(src + src_stride), 1);
                    src_next_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src + 1)),
                        _mm_loadu_si128(src + src_stride + 1), 1);
                    // LOAD_DST_INSERT
                    dst_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                        _mm_loadu_si128(dst + dst_stride), 1);
                    /* average between current and next stride source */
                    src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    /* expand each byte to 2 bytes */
                    MERGE_WITH_SRC(src_reg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += (src_stride << 1);
                    dst += (dst_stride << 1);
                }
                /* x_offset = 4  and y_offset = 4 */
            }
            else if (y_offset == 4)
            {
                __m256i src_next_reg, src_avg, src_temp;
                /* load and insert source and next row source */
                // LOAD_SRC_NEXT_BYTE_INSERT
                src_reg = _mm256_inserti128_si256(
                    _mm256_castsi128_si256(_mm_loadu_si128(src)),
                    _mm_loadu_si128(src + src_stride), 1);
                src_next_reg = _mm256_inserti128_si256(
                    _mm256_castsi128_si256(_mm_loadu_si128(src + 1)),
                    _mm_loadu_si128(src + src_stride + 1), 1);
                src_avg = _mm256_avg_epu8(src_reg, src_next_reg);
                src += src_stride << 1;
                for (i = 0; i < height - 2; i += 2)
                {
                    // LOAD_SRC_NEXT_BYTE_INSERT
                    src_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src)),
                        _mm_loadu_si128(src + src_stride), 1);
                    src_next_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src + 1)),
                        _mm_loadu_si128(src + src_stride + 1), 1);
                    src_next_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    src_temp = _mm256_permute2x128_si256(src_avg, src_next_reg, 0x21);
                    src_temp = _mm256_avg_epu8(src_avg, src_temp);
                    // LOAD_DST_INSERT
                    dst_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                        _mm_loadu_si128(dst + dst_stride), 1);
                    /* expand each byte to 2 bytes */
                    MERGE_WITH_SRC(src_temp, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    /* save current source average */
                    src_avg = src_next_reg;
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    dst += dst_stride << 1;
                    src += src_stride << 1;
                }
                /* last 2 rows processing happens here */
                __m128i src_reg_0 = _mm_loadu_si128(src);
                __m128i src_reg_1 = _mm_loadu_si128(src + 1);
                src_reg_0 = _mm_avg_epu8(src_reg_0, src_reg_1);
                src_next_reg = _mm256_permute2x128_si256(
                    src_avg, _mm256_castsi128_si256(src_reg_0), 0x21);
                // LOAD_DST_INSERT
                dst_reg = _mm256_inserti128_si256(
                    _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                    _mm_loadu_si128(dst + dst_stride), 1);
                src_avg = _mm256_avg_epu8(src_avg, src_next_reg);
                MERGE_WITH_SRC(src_avg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
            }
            else
            {
                /* x_offset = 4  and y_offset = bilin interpolation */
                __m256i filter, pw8, src_next_reg, src_avg, src_temp;
                y_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(y_offset));
                pw8 = _mm256_set1_epi16(8);
                /* load and insert source and next row source */
                // LOAD_SRC_NEXT_BYTE_INSERT
                src_reg = _mm256_inserti128_si256(
                    _mm256_castsi128_si256(_mm_loadu_si128(src)),
                    _mm_loadu_si128(src + src_stride), 1);
                src_next_reg = _mm256_inserti128_si256(
                    _mm256_castsi128_si256(_mm_loadu_si128(src + 1)),
                    _mm_loadu_si128(src + src_stride + 1), 1);
                src_avg = _mm256_avg_epu8(src_reg, src_next_reg);
                src += src_stride << 1;
                for (i = 0; i < height - 2; i += 2)
                {
                    // LOAD_SRC_NEXT_BYTE_INSERT
                    src_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src)),
                        _mm_loadu_si128(src + src_stride), 1);
                    src_next_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src + 1)),
                        _mm_loadu_si128(src + src_stride + 1), 1);
                    src_next_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    src_temp = _mm256_permute2x128_si256(src_avg, src_next_reg, 0x21);
                    // LOAD_DST_INSERT
                    dst_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                        _mm_loadu_si128(dst + dst_stride), 1);
                    MERGE_WITH_SRC(src_avg, src_temp, ref exp_src_lo, ref exp_src_hi);
                    /* save current source average */
                    src_avg = src_next_reg;
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    dst += dst_stride << 1;
                    src += src_stride << 1;
                }
                /* last 2 rows processing happens here */
                __m128i src_reg_0 = _mm_loadu_si128(src);
                __m128i src_reg_1 = _mm_loadu_si128(src + 1);
                src_reg_0 = _mm_avg_epu8(src_reg_0, src_reg_1);
                src_next_reg = _mm256_permute2x128_si256(
                    src_avg, _mm256_castsi128_si256(src_reg_0), 0x21);
                // LOAD_DST_INSERT
                dst_reg = _mm256_inserti128_si256(
                    _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                    _mm_loadu_si128(dst + dst_stride), 1);
                MERGE_WITH_SRC(src_avg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
            }
            /* x_offset = bilin interpolation and y_offset = 0 */
        }
        else
        {
            if (y_offset == 0)
            {
                __m256i filter, pw8, src_next_reg;
                x_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(x_offset));
                pw8 = _mm256_set1_epi16(8);
                for (i = 0; i < height; i += 2)
                {
                    // LOAD_SRC_DST_INSERT(src_stride, dst_stride)
                    src_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src)),
                        _mm_loadu_si128(src + src_stride), 1);
                    dst_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                        _mm_loadu_si128(dst + dst_stride), 1);
                    // MERGE_NEXT_SRC_INSERT(src_reg, 1)
                    src_next_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src + 1)),
                        _mm_loadu_si128(src + (src_stride + 1)), 1);
                    MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += (src_stride << 1);
                    dst += (dst_stride << 1);
                }
                /* x_offset = bilin interpolation and y_offset = 4 */
            }
            else if (y_offset == 4)
            {
                __m256i filter, pw8, src_next_reg, src_pack;
                x_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(x_offset));
                pw8 = _mm256_set1_epi16(8);
                /* load and insert source and next row source */
                // LOAD_SRC_NEXT_BYTE_INSERT
                src_reg = _mm256_inserti128_si256(
                    _mm256_castsi128_si256(_mm_loadu_si128(src)),
                    _mm_loadu_si128(src + src_stride), 1);
                src_next_reg = _mm256_inserti128_si256(
                    _mm256_castsi128_si256(_mm_loadu_si128(src + 1)),
                    _mm_loadu_si128(src + src_stride + 1), 1);
                MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                /* convert each 16 bit to 8 bit to each low and high lane source */
                src_pack = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                src += src_stride << 1;
                for (i = 0; i < height - 2; i += 2)
                {
                    // LOAD_SRC_NEXT_BYTE_INSERT
                    src_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src)),
                        _mm_loadu_si128(src + src_stride), 1);
                    src_next_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(src + 1)),
                        _mm_loadu_si128(src + src_stride + 1), 1);
                    // LOAD_DST_INSERT
                    dst_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                        _mm_loadu_si128(dst + dst_stride), 1);
                    MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    src_reg = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                    src_next_reg = _mm256_permute2x128_si256(src_pack, src_reg, 0x21);
                    /* average between previous pack to the current */
                    src_pack = _mm256_avg_epu8(src_pack, src_next_reg);
                    MERGE_WITH_SRC(src_pack, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src_pack = src_reg;
                    src += src_stride << 1;
                    dst += dst_stride << 1;
                }
                /* last 2 rows processing happens here */
                // LOAD_SRC_MERGE_128BIT(filter)
                __m128i src_reg_0 = _mm_loadu_si128(src);
                __m128i src_reg_1 = _mm_loadu_si128(src + 1);
                __m128i src_lo = _mm_unpacklo_epi8(src_reg_0, src_reg_1);
                __m128i src_hi = _mm_unpackhi_epi8(src_reg_0, src_reg_1);
                __m128i filter_128bit = _mm256_castsi256_si128(filter);
                __m128i pw8_128bit = _mm256_castsi256_si128(pw8);
                // LOAD_DST_INSERT
                dst_reg = _mm256_inserti128_si256(
                    _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                    _mm_loadu_si128(dst + dst_stride), 1);
                FILTER_SRC_128BIT(filter_128bit, pw8_128bit, ref src_lo, ref src_hi);
                src_reg_0 = _mm_packus_epi16(src_lo, src_hi);
                src_next_reg = _mm256_permute2x128_si256(
                    src_pack, _mm256_castsi128_si256(src_reg_0), 0x21);
                /* average between previous pack to the current */
                src_pack = _mm256_avg_epu8(src_pack, src_next_reg);
                MERGE_WITH_SRC(src_pack, zero_reg, ref exp_src_lo, ref exp_src_hi);
                CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
            }
            else
            {
                __m256i xfilter, yfilter, pw8, mask_00ff;
                __m256i p0, p1;
                Ptr<byte> src_ptr = src;
                x_offset <<= 5;
                xfilter = _mm256_load_si256(bilinear_filters_avx2.Slice(x_offset));
                y_offset <<= 5;
                yfilter = _mm256_load_si256(bilinear_filters_avx2.Slice(y_offset));
                pw8 = _mm256_set1_epi16(8);
                mask_00ff = _mm256_set1_epi16(0x00ff);

                p0 = filter_rows_eo_16x2(src_ptr, src_stride, xfilter, pw8);
                src_ptr += src_stride << 1;
                p1 = filter_rows_eo_16x2(src_ptr, src_stride, xfilter, pw8);
                src_ptr += src_stride << 1;

                for (i = 0; i < height - 4; i += 2)
                {
                    __m256i p2, p_mix, diff_ev, diff_od, sum_comb, sse_comb;

                    p2 = filter_rows_eo_16x2(src_ptr, src_stride, xfilter, pw8);
                    src_ptr += src_stride << 1;

                    p_mix = _mm256_permute2x128_si256(p0, p1, 0x21);
                    dst_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                        _mm_loadu_si128(dst + dst_stride), 1);
                    dst += dst_stride << 1;
                    filter_col_eo_accumulate(p0, p_mix, dst_reg, yfilter, pw8, mask_00ff,
                                             out diff_ev, out diff_od);

                    sum_comb = _mm256_add_epi16(diff_ev, diff_od);
                    sum_reg = _mm256_add_epi16(sum_reg, sum_comb);

                    sse_comb = _mm256_add_epi32(_mm256_madd_epi16(diff_ev, diff_ev),
                                                _mm256_madd_epi16(diff_od, diff_od));
                    sse_reg = _mm256_add_epi32(sse_reg, sse_comb);

                    p0 = p1;
                    p1 = p2;
                }

                {
                    __m256i p_mix = _mm256_permute2x128_si256(p0, p1, 0x21);
                    __m256i diff_ev, diff_od, sum_comb, sse_comb;

                    dst_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                        _mm_loadu_si128(dst + dst_stride), 1);
                    dst += dst_stride << 1;
                    filter_col_eo_accumulate(p0, p_mix, dst_reg, yfilter, pw8, mask_00ff,
                                             out diff_ev, out diff_od);

                    sum_comb = _mm256_add_epi16(diff_ev, diff_od);
                    sum_reg = _mm256_add_epi16(sum_reg, sum_comb);

                    sse_comb = _mm256_add_epi32(_mm256_madd_epi16(diff_ev, diff_ev),
                                                _mm256_madd_epi16(diff_od, diff_od));
                    sse_reg = _mm256_add_epi32(sse_reg, sse_comb);
                }
                {
                    __m128i s0 = _mm_loadu_si128(src_ptr);
                    __m128i s1 = _mm_loadu_si128(src_ptr + 1);
                    __m128i he = _mm_maddubs_epi16(s0, _mm256_castsi256_si128(xfilter));
                    __m128i ho = _mm_maddubs_epi16(s1, _mm256_castsi256_si128(xfilter));
                    __m256i p_last, p_mix, diff_ev, diff_od, sum_comb, sse_comb;
                    he = _mm_srai_epi16(_mm_add_epi16(he, _mm256_castsi256_si128(pw8)), 4);
                    ho = _mm_srai_epi16(_mm_add_epi16(ho, _mm256_castsi256_si128(pw8)), 4);
                    p_last = _mm256_castsi128_si256(_mm_packus_epi16(he, ho));

                    p_mix = _mm256_permute2x128_si256(p1, p_last, 0x21);
                    dst_reg = _mm256_inserti128_si256(
                        _mm256_castsi128_si256(_mm_loadu_si128(dst)),
                        _mm_loadu_si128(dst + dst_stride), 1);
                    filter_col_eo_accumulate(p1, p_mix, dst_reg, yfilter, pw8, mask_00ff,
                                             out diff_ev, out diff_od);

                    sum_comb = _mm256_add_epi16(diff_ev, diff_od);
                    sum_reg = _mm256_add_epi16(sum_reg, sum_comb);

                    sse_comb = _mm256_add_epi32(_mm256_madd_epi16(diff_ev, diff_ev),
                                                _mm256_madd_epi16(diff_od, diff_od));
                    sse_reg = _mm256_add_epi32(sse_reg, sse_comb);
                }

                int f_sum = final_eo_sum_sse(sse_reg, sum_reg, ref sse);
                return sse - (uint)(((long)f_sum * f_sum) >> (4 + log2height));
            }
        }
        sum = CALC_SUM_AND_SSE(sse_reg, sum_reg, zero_reg, ref sse);
        _mm256_zeroupper();
        return sse - (uint)(((long)sum * sum) >> (4 + log2height));
    }

    // MAKE_SUB_PIXEL_VAR_16XH(32, 5), (16, 4), (8, 3)
    internal static uint aom_sub_pixel_variance16x32_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                          Ptr<byte> dst, int dst_stride, ref uint sse) =>
        aom_sub_pixel_variance16xh_avx2(src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse, 32, 5);

    internal static uint aom_sub_pixel_variance16x16_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                          Ptr<byte> dst, int dst_stride, ref uint sse) =>
        aom_sub_pixel_variance16xh_avx2(src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse, 16, 4);

    internal static uint aom_sub_pixel_variance16x8_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                         Ptr<byte> dst, int dst_stride, ref uint sse) =>
        aom_sub_pixel_variance16xh_avx2(src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse, 8, 3);

    // MAKE_SUB_PIXEL_AVG_VAR_32XH(height, log2height): sub_pixel_avg_variance32x##height##_imp_avx2
    private static int sub_pixel_avg_variance32xh_imp_avx2(
        Ptr<byte> src, int src_stride, int x_offset, int y_offset,
        Ptr<byte> dst, int dst_stride, Ptr<byte> sec, int sec_stride,
        ref uint sse, int height)
    {
        __m256i sec_reg;
        __m256i src_reg, dst_reg, exp_src_lo = default, exp_src_hi = default;
        __m256i sse_reg, sum_reg;
        __m256i zero_reg;
        int i, sum;
        sum_reg = _mm256_setzero_si256();
        sse_reg = _mm256_setzero_si256();
        zero_reg = _mm256_setzero_si256();

        /* x_offset = 0 and y_offset = 0 */
        if (x_offset == 0)
        {
            if (y_offset == 0)
            {
                for (i = 0; i < height; i++)
                {
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    sec_reg = _mm256_loadu_si256(sec);
                    src_reg = _mm256_avg_epu8(src_reg, sec_reg);
                    sec += sec_stride;
                    /* expend each byte to 2 bytes */
                    MERGE_WITH_SRC(src_reg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += src_stride;
                    dst += dst_stride;
                }
            }
            else if (y_offset == 4)
            {
                __m256i src_next_reg;
                for (i = 0; i < height; i++)
                {
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // AVG_NEXT_SRC(src_reg, src_stride)
                    src_next_reg = _mm256_loadu_si256(src + src_stride);
                    src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    sec_reg = _mm256_loadu_si256(sec);
                    src_reg = _mm256_avg_epu8(src_reg, sec_reg);
                    sec += sec_stride;
                    /* expend each byte to 2 bytes */
                    MERGE_WITH_SRC(src_reg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += src_stride;
                    dst += dst_stride;
                }
                /* x_offset = 0 and y_offset = bilin interpolation */
            }
            else
            {
                __m256i filter, pw8, src_next_reg;

                y_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(y_offset));
                pw8 = _mm256_set1_epi16(8);
                for (i = 0; i < height; i++)
                {
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // MERGE_NEXT_SRC(src_reg, src_stride)
                    src_next_reg = _mm256_loadu_si256(src + src_stride);
                    MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    src_reg = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                    sec_reg = _mm256_loadu_si256(sec);
                    src_reg = _mm256_avg_epu8(src_reg, sec_reg);
                    sec += sec_stride;
                    MERGE_WITH_SRC(src_reg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += src_stride;
                    dst += dst_stride;
                }
            }
            /* x_offset = 4  and y_offset = 0 */
        }
        else if (x_offset == 4)
        {
            if (y_offset == 0)
            {
                __m256i src_next_reg;
                for (i = 0; i < height; i++)
                {
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // AVG_NEXT_SRC(src_reg, 1)
                    src_next_reg = _mm256_loadu_si256(src + 1);
                    src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    sec_reg = _mm256_loadu_si256(sec);
                    src_reg = _mm256_avg_epu8(src_reg, sec_reg);
                    sec += sec_stride;
                    /* expand each byte to 2 bytes */
                    MERGE_WITH_SRC(src_reg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += src_stride;
                    dst += dst_stride;
                }
                /* x_offset = 4  and y_offset = 4 */
            }
            else if (y_offset == 4)
            {
                __m256i src_next_reg, src_avg;
                /* load source and another source starting from the next */
                /* following byte */
                src_reg = _mm256_loadu_si256(src);
                // AVG_NEXT_SRC(src_reg, 1)
                src_next_reg = _mm256_loadu_si256(src + 1);
                src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                for (i = 0; i < height; i++)
                {
                    /* save current source average */
                    src_avg = src_reg;
                    src += src_stride;
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // AVG_NEXT_SRC(src_reg, 1)
                    src_next_reg = _mm256_loadu_si256(src + 1);
                    src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    /* average between previous average to current average */
                    src_avg = _mm256_avg_epu8(src_avg, src_reg);
                    sec_reg = _mm256_loadu_si256(sec);
                    src_avg = _mm256_avg_epu8(src_avg, sec_reg);
                    sec += sec_stride;
                    /* expand each byte to 2 bytes */
                    MERGE_WITH_SRC(src_avg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    dst += dst_stride;
                }
                /* x_offset = 4  and y_offset = bilin interpolation */
            }
            else
            {
                __m256i filter, pw8, src_next_reg, src_avg;
                y_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(y_offset));
                pw8 = _mm256_set1_epi16(8);
                /* load source and another source starting from the next */
                /* following byte */
                src_reg = _mm256_loadu_si256(src);
                // AVG_NEXT_SRC(src_reg, 1)
                src_next_reg = _mm256_loadu_si256(src + 1);
                src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                for (i = 0; i < height; i++)
                {
                    /* save current source average */
                    src_avg = src_reg;
                    src += src_stride;
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // AVG_NEXT_SRC(src_reg, 1)
                    src_next_reg = _mm256_loadu_si256(src + 1);
                    src_reg = _mm256_avg_epu8(src_reg, src_next_reg);
                    MERGE_WITH_SRC(src_avg, src_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    src_avg = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                    sec_reg = _mm256_loadu_si256(sec);
                    src_avg = _mm256_avg_epu8(src_avg, sec_reg);
                    /* expand each byte to 2 bytes */
                    MERGE_WITH_SRC(src_avg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    sec += sec_stride;
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    dst += dst_stride;
                }
            }
            /* x_offset = bilin interpolation and y_offset = 0 */
        }
        else
        {
            if (y_offset == 0)
            {
                __m256i filter, pw8, src_next_reg;
                x_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(x_offset));
                pw8 = _mm256_set1_epi16(8);
                for (i = 0; i < height; i++)
                {
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // MERGE_NEXT_SRC(src_reg, 1)
                    src_next_reg = _mm256_loadu_si256(src + 1);
                    MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    src_reg = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                    sec_reg = _mm256_loadu_si256(sec);
                    src_reg = _mm256_avg_epu8(src_reg, sec_reg);
                    MERGE_WITH_SRC(src_reg, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    sec += sec_stride;
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    src += src_stride;
                    dst += dst_stride;
                }
                /* x_offset = bilin interpolation and y_offset = 4 */
            }
            else if (y_offset == 4)
            {
                __m256i filter, pw8, src_next_reg, src_pack;
                x_offset <<= 5;
                filter = _mm256_load_si256(bilinear_filters_avx2.Slice(x_offset));
                pw8 = _mm256_set1_epi16(8);
                src_reg = _mm256_loadu_si256(src);
                // MERGE_NEXT_SRC(src_reg, 1)
                src_next_reg = _mm256_loadu_si256(src + 1);
                MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                /* convert each 16 bit to 8 bit to each low and high lane source */
                src_pack = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                for (i = 0; i < height; i++)
                {
                    src += src_stride;
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // MERGE_NEXT_SRC(src_reg, 1)
                    src_next_reg = _mm256_loadu_si256(src + 1);
                    MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(filter, pw8, ref exp_src_lo, ref exp_src_hi);
                    src_reg = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                    /* average between previous pack to the current */
                    src_pack = _mm256_avg_epu8(src_pack, src_reg);
                    sec_reg = _mm256_loadu_si256(sec);
                    src_pack = _mm256_avg_epu8(src_pack, sec_reg);
                    sec += sec_stride;
                    MERGE_WITH_SRC(src_pack, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    src_pack = src_reg;
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    dst += dst_stride;
                }
                /* x_offset = bilin interpolation and y_offset = bilin interpolation
                 */
            }
            else
            {
                __m256i xfilter, yfilter, pw8, src_next_reg, src_pack;
                x_offset <<= 5;
                xfilter = _mm256_load_si256(bilinear_filters_avx2.Slice(x_offset));
                y_offset <<= 5;
                yfilter = _mm256_load_si256(bilinear_filters_avx2.Slice(y_offset));
                pw8 = _mm256_set1_epi16(8);
                /* load source and another source starting from the next */
                /* following byte */
                src_reg = _mm256_loadu_si256(src);
                // MERGE_NEXT_SRC(src_reg, 1)
                src_next_reg = _mm256_loadu_si256(src + 1);
                MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);

                FILTER_SRC(xfilter, pw8, ref exp_src_lo, ref exp_src_hi);
                /* convert each 16 bit to 8 bit to each low and high lane source */
                src_pack = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                for (i = 0; i < height; i++)
                {
                    src += src_stride;
                    // LOAD_SRC_DST
                    src_reg = _mm256_loadu_si256(src);
                    dst_reg = _mm256_loadu_si256(dst);
                    // MERGE_NEXT_SRC(src_reg, 1)
                    src_next_reg = _mm256_loadu_si256(src + 1);
                    MERGE_WITH_SRC(src_reg, src_next_reg, ref exp_src_lo, ref exp_src_hi);
                    FILTER_SRC(xfilter, pw8, ref exp_src_lo, ref exp_src_hi);
                    src_reg = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                    /* merge previous pack to current pack source */
                    MERGE_WITH_SRC(src_pack, src_reg, ref exp_src_lo, ref exp_src_hi);
                    /* filter the source */
                    FILTER_SRC(yfilter, pw8, ref exp_src_lo, ref exp_src_hi);
                    src_pack = _mm256_packus_epi16(exp_src_lo, exp_src_hi);
                    sec_reg = _mm256_loadu_si256(sec);
                    src_pack = _mm256_avg_epu8(src_pack, sec_reg);
                    MERGE_WITH_SRC(src_pack, zero_reg, ref exp_src_lo, ref exp_src_hi);
                    src_pack = src_reg;
                    sec += sec_stride;
                    CALC_SUM_SSE_INSIDE_LOOP(dst_reg, zero_reg, ref exp_src_lo, ref exp_src_hi, ref sum_reg, ref sse_reg);
                    dst += dst_stride;
                }
            }
        }
        sum = CALC_SUM_AND_SSE(sse_reg, sum_reg, zero_reg, ref sse);
        _mm256_zeroupper();
        return sum;
    }

    // MAKE_SUB_PIXEL_AVG_VAR_32XH(64, 6), (32, 5), (16, 4)
    internal static uint aom_sub_pixel_avg_variance32x64_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                              Ptr<byte> dst, int dst_stride, ref uint sse, Ptr<byte> sec_ptr)
    {
        int sum = sub_pixel_avg_variance32xh_imp_avx2(src, src_stride, x_offset, y_offset, dst, dst_stride, sec_ptr, 32, ref sse, 64);
        return sse - (uint)(((long)sum * sum) >> (5 + 6));
    }

    internal static uint aom_sub_pixel_avg_variance32x32_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                              Ptr<byte> dst, int dst_stride, ref uint sse, Ptr<byte> sec_ptr)
    {
        int sum = sub_pixel_avg_variance32xh_imp_avx2(src, src_stride, x_offset, y_offset, dst, dst_stride, sec_ptr, 32, ref sse, 32);
        return sse - (uint)(((long)sum * sum) >> (5 + 5));
    }

    internal static uint aom_sub_pixel_avg_variance32x16_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                              Ptr<byte> dst, int dst_stride, ref uint sse, Ptr<byte> sec_ptr)
    {
        int sum = sub_pixel_avg_variance32xh_imp_avx2(src, src_stride, x_offset, y_offset, dst, dst_stride, sec_ptr, 32, ref sse, 16);
        return sse - (uint)(((long)sum * sum) >> (5 + 4));
    }

    // AOM_SUB_PIXEL_AVG_VAR_AVX2(w, h, wf, hf, wlog2, hlog2) with wf = 32.
    [MethodImpl(AI)]
    private static uint AOM_SUB_PIXEL_AVG_VAR_AVX2(int w, int h, int hf, int wlog2, int hlog2,
                                                   Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                   Ptr<byte> dst, int dst_stride, ref uint sse_ptr,
                                                   Ptr<byte> sec)
    {
        const int wf = 32;
        uint sse = 0;
        int se = 0;
        for (int i = 0; i < (w / wf); ++i)
        {
            Ptr<byte> src_ptr = src;
            Ptr<byte> dst_ptr = dst;
            Ptr<byte> sec_ptr = sec;
            for (int j = 0; j < (h / hf); ++j)
            {
                uint sse2 = 0;
                int se2 = sub_pixel_avg_variance32xh_imp_avx2(
                    src_ptr, src_stride, x_offset, y_offset, dst_ptr, dst_stride,
                    sec_ptr, w, ref sse2, hf);
                dst_ptr += hf * dst_stride;
                src_ptr += hf * src_stride;
                sec_ptr += hf * w;
                se += se2;
                sse += sse2;
            }
            src += wf;
            dst += wf;
            sec += wf;
        }
        sse_ptr = sse;
        return sse - (uint)(((long)se * se) >> (wlog2 + hlog2));
    }

    // Note: hf = AOMMIN(h, 64) to avoid overflow in helper by capping height.
    internal static uint aom_sub_pixel_avg_variance128x128_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                                Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec) =>
        AOM_SUB_PIXEL_AVG_VAR_AVX2(128, 128, 64, 7, 7, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);

    internal static uint aom_sub_pixel_avg_variance128x64_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                               Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec) =>
        AOM_SUB_PIXEL_AVG_VAR_AVX2(128, 64, 64, 7, 6, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);

    internal static uint aom_sub_pixel_avg_variance64x128_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                               Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec) =>
        AOM_SUB_PIXEL_AVG_VAR_AVX2(64, 128, 64, 6, 7, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);

    internal static uint aom_sub_pixel_avg_variance64x64_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                              Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec) =>
        AOM_SUB_PIXEL_AVG_VAR_AVX2(64, 64, 64, 6, 6, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);

    internal static uint aom_sub_pixel_avg_variance64x32_avx2(Ptr<byte> src, int src_stride, int x_offset, int y_offset,
                                                              Ptr<byte> dst, int dst_stride, ref uint sse_ptr, Ptr<byte> sec) =>
        AOM_SUB_PIXEL_AVG_VAR_AVX2(64, 32, 32, 6, 5, src, src_stride, x_offset, y_offset, dst, dst_stride, ref sse_ptr, sec);
}
