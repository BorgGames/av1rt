// Port of libaom aom_dsp/x86/variance_avx2.c

global using static Av1rt.Aom.variance_avx2;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] The !CONFIG_HIGHWAY, CONFIG_AV1_HIGHBITDEPTH == 0 code. The AOM_VAR_NO_LOOP_AVX2 /
// AOM_VAR_LOOP_AVX2 instantiations are written out for the CONFIG_REALTIME_ONLY sizes.
// Not ported (no dispatched C counterpart in the port, see variance.c.cs):
// aom_comp_mask_pred_avx2 (masked compound is off in the subset), aom_mse_wxh_16bit_avx2 /
// aom_mse_16xh_16bit_avx2 and their helpers (never executed in the subset),
// aom_calc_variance_stat_avx2 (not in the RT path).
internal static class variance_avx2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i mm256_add_hi_lo_epi16(__m256i val)
    {
        return _mm_add_epi16(_mm256_castsi256_si128(val),
                             _mm256_extractf128_si256(val, 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m128i mm256_add_hi_lo_epi32(__m256i val)
    {
        return _mm_add_epi32(_mm256_castsi256_si128(val),
                             _mm256_extractf128_si256(val, 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance_kernel_avx2(__m256i src, __m256i @ref,
                                             ref __m256i sse,
                                             ref __m256i sum)
    {
        __m256i adj_sub = _mm256_set1_epi16(unchecked((short)0xff01));  // (1,-1)

        // unpack into pairs of source and reference values
        __m256i src_ref0 = _mm256_unpacklo_epi8(src, @ref);
        __m256i src_ref1 = _mm256_unpackhi_epi8(src, @ref);

        // subtract adjacent elements using src*1 + ref*-1
        __m256i diff0 = _mm256_maddubs_epi16(src_ref0, adj_sub);
        __m256i diff1 = _mm256_maddubs_epi16(src_ref1, adj_sub);
        __m256i madd0 = _mm256_madd_epi16(diff0, diff0);
        __m256i madd1 = _mm256_madd_epi16(diff1, diff1);

        // add to the running totals
        sum = _mm256_add_epi16(sum, _mm256_add_epi16(diff0, diff1));
        sse = _mm256_add_epi32(sse, _mm256_add_epi32(madd0, madd1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int variance_final_from_32bit_sum_avx2(__m256i vsse, __m128i vsum,
                                                          ref uint sse)
    {
        // extract the low lane and add it to the high lane
        __m128i sse_reg_128 = mm256_add_hi_lo_epi32(vsse);

        // unpack sse and sum registers and add
        __m128i sse_sum_lo = _mm_unpacklo_epi32(sse_reg_128, vsum);
        __m128i sse_sum_hi = _mm_unpackhi_epi32(sse_reg_128, vsum);
        __m128i sse_sum = _mm_add_epi32(sse_sum_lo, sse_sum_hi);

        // perform the final summation and extract the results
        __m128i res = _mm_add_epi32(sse_sum, _mm_srli_si128(sse_sum, 8));
        sse = (uint)_mm_cvtsi128_si32(res);  // *((int *)sse) = _mm_cvtsi128_si32(res)
        return _mm_extract_epi32(res, 1);
    }

    // handle pixels (<= 512)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int variance_final_512_avx2(__m256i vsse, __m256i vsum,
                                               ref uint sse)
    {
        // extract the low lane and add it to the high lane
        __m128i vsum_128 = mm256_add_hi_lo_epi16(vsum);
        __m128i vsum_64 = _mm_add_epi16(vsum_128, _mm_srli_si128(vsum_128, 8));
        __m128i sum_int32 = _mm_cvtepi16_epi32(vsum_64);
        return variance_final_from_32bit_sum_avx2(vsse, sum_int32, ref sse);
    }

    // handle 1024 pixels (32x32, 16x64, 64x16)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int variance_final_1024_avx2(__m256i vsse, __m256i vsum,
                                                ref uint sse)
    {
        // extract the low lane and add it to the high lane
        __m128i vsum_128 = mm256_add_hi_lo_epi16(vsum);
        __m128i vsum_64 =
            _mm_add_epi32(_mm_cvtepi16_epi32(vsum_128),
                          _mm_cvtepi16_epi32(_mm_srli_si128(vsum_128, 8)));
        return variance_final_from_32bit_sum_avx2(vsse, vsum_64, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i sum_to_32bit_avx2(__m256i sum)
    {
        __m256i sum_lo = _mm256_cvtepi16_epi32(_mm256_castsi256_si128(sum));
        __m256i sum_hi =
            _mm256_cvtepi16_epi32(_mm256_extractf128_si256(sum, 1));
        return _mm256_add_epi32(sum_lo, sum_hi);
    }

    // handle 2048 pixels (32x64, 64x32)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int variance_final_2048_avx2(__m256i vsse, __m256i vsum,
                                                ref uint sse)
    {
        vsum = sum_to_32bit_avx2(vsum);
        __m128i vsum_128 = mm256_add_hi_lo_epi32(vsum);
        return variance_final_from_32bit_sum_avx2(vsse, vsum_128, ref sse);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance16_kernel_avx2(
        Ptr<byte> src, int src_stride, Ptr<byte> @ref,
        int ref_stride, ref __m256i sse, ref __m256i sum)
    {
        __m128i s0 = _mm_loadu_si128(src + 0 * src_stride);
        __m128i s1 = _mm_loadu_si128(src + 1 * src_stride);
        __m128i r0 = _mm_loadu_si128(@ref + 0 * ref_stride);
        __m128i r1 = _mm_loadu_si128(@ref + 1 * ref_stride);
        __m256i s = _mm256_inserti128_si256(_mm256_castsi128_si256(s0), s1, 1);
        __m256i r = _mm256_inserti128_si256(_mm256_castsi128_si256(r0), r1, 1);
        variance_kernel_avx2(s, r, ref sse, ref sum);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance32_kernel_avx2(Ptr<byte> src,
                                               Ptr<byte> @ref,
                                               ref __m256i sse,
                                               ref __m256i sum)
    {
        __m256i s = _mm256_loadu_si256(src);
        __m256i r = _mm256_loadu_si256(@ref);
        variance_kernel_avx2(s, r, ref sse, ref sum);
    }

    // [port] The row loops below slice each row once per buffer, so the loads inside need no
    // further bounds checks (loading through Ptr checks every load).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance16_avx2(Ptr<byte> src, int src_stride,
                                        Ptr<byte> @ref, int ref_stride,
                                        int h, ref __m256i vsse,
                                        ref __m256i vsum)
    {
        vsum = _mm256_setzero_si256();
        ReadOnlySpan<byte> sbuf = src.Array, rbuf = @ref.Array;
        int so = src.Offset, ro = @ref.Offset;

        for (int i = 0; i < h; i += 2)
        {
            // variance16_kernel_avx2
            __m128i s0 = _mm_loadu_si128(sbuf.Slice(so, 16));
            __m128i s1 = _mm_loadu_si128(sbuf.Slice(so + src_stride, 16));
            __m128i r0 = _mm_loadu_si128(rbuf.Slice(ro, 16));
            __m128i r1 = _mm_loadu_si128(rbuf.Slice(ro + ref_stride, 16));
            __m256i s = _mm256_inserti128_si256(_mm256_castsi128_si256(s0), s1, 1);
            __m256i r = _mm256_inserti128_si256(_mm256_castsi128_si256(r0), r1, 1);
            variance_kernel_avx2(s, r, ref vsse, ref vsum);
            so += 2 * src_stride;
            ro += 2 * ref_stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance32_avx2(Ptr<byte> src, int src_stride,
                                        Ptr<byte> @ref, int ref_stride,
                                        int h, ref __m256i vsse,
                                        ref __m256i vsum)
    {
        vsum = _mm256_setzero_si256();
        ReadOnlySpan<byte> sbuf = src.Array, rbuf = @ref.Array;
        int so = src.Offset, ro = @ref.Offset;

        for (int i = 0; i < h; i++)
        {
            variance_kernel_avx2(_mm256_loadu_si256(sbuf.Slice(so, 32)),
                                 _mm256_loadu_si256(rbuf.Slice(ro, 32)), ref vsse, ref vsum);
            so += src_stride;
            ro += ref_stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance64_avx2(Ptr<byte> src, int src_stride,
                                        Ptr<byte> @ref, int ref_stride,
                                        int h, ref __m256i vsse,
                                        ref __m256i vsum)
    {
        vsum = _mm256_setzero_si256();
        ReadOnlySpan<byte> sbuf = src.Array, rbuf = @ref.Array;
        int so = src.Offset, ro = @ref.Offset;

        for (int i = 0; i < h; i++)
        {
            ReadOnlySpan<byte> s = sbuf.Slice(so, 64), r = rbuf.Slice(ro, 64);
            variance_kernel_avx2(_mm256_loadu_si256(s), _mm256_loadu_si256(r), ref vsse, ref vsum);
            variance_kernel_avx2(_mm256_loadu_si256(s.Slice(32)), _mm256_loadu_si256(r.Slice(32)),
                                 ref vsse, ref vsum);
            so += src_stride;
            ro += ref_stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void variance128_avx2(Ptr<byte> src, int src_stride,
                                         Ptr<byte> @ref, int ref_stride,
                                         int h, ref __m256i vsse,
                                         ref __m256i vsum)
    {
        vsum = _mm256_setzero_si256();
        ReadOnlySpan<byte> sbuf = src.Array, rbuf = @ref.Array;
        int so = src.Offset, ro = @ref.Offset;

        for (int i = 0; i < h; i++)
        {
            ReadOnlySpan<byte> s = sbuf.Slice(so, 128), r = rbuf.Slice(ro, 128);
            variance_kernel_avx2(_mm256_loadu_si256(s), _mm256_loadu_si256(r), ref vsse, ref vsum);
            variance_kernel_avx2(_mm256_loadu_si256(s.Slice(32)), _mm256_loadu_si256(r.Slice(32)),
                                 ref vsse, ref vsum);
            variance_kernel_avx2(_mm256_loadu_si256(s.Slice(64)), _mm256_loadu_si256(r.Slice(64)),
                                 ref vsse, ref vsum);
            variance_kernel_avx2(_mm256_loadu_si256(s.Slice(96)), _mm256_loadu_si256(r.Slice(96)),
                                 ref vsse, ref vsum);
            so += src_stride;
            ro += ref_stride;
        }
    }

    // AOM_VAR_NO_LOOP_AVX2(bw, bh, bits, max_pixel)
    internal static uint aom_variance16x8_avx2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse)
    {
        __m256i vsse = _mm256_setzero_si256();
        __m256i vsum = default;
        variance16_avx2(src, src_stride, @ref, ref_stride, 8, ref vsse, ref vsum);
        int sum = variance_final_512_avx2(vsse, vsum, ref sse);
        return sse - (uint)(((long)sum * sum) >> 7);
    }

    internal static uint aom_variance16x16_avx2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse)
    {
        __m256i vsse = _mm256_setzero_si256();
        __m256i vsum = default;
        variance16_avx2(src, src_stride, @ref, ref_stride, 16, ref vsse, ref vsum);
        int sum = variance_final_512_avx2(vsse, vsum, ref sse);
        return sse - (uint)(((long)sum * sum) >> 8);
    }

    internal static uint aom_variance16x32_avx2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse)
    {
        __m256i vsse = _mm256_setzero_si256();
        __m256i vsum = default;
        variance16_avx2(src, src_stride, @ref, ref_stride, 32, ref vsse, ref vsum);
        int sum = variance_final_512_avx2(vsse, vsum, ref sse);
        return sse - (uint)(((long)sum * sum) >> 9);
    }

    internal static uint aom_variance32x16_avx2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse)
    {
        __m256i vsse = _mm256_setzero_si256();
        __m256i vsum = default;
        variance32_avx2(src, src_stride, @ref, ref_stride, 16, ref vsse, ref vsum);
        int sum = variance_final_512_avx2(vsse, vsum, ref sse);
        return sse - (uint)(((long)sum * sum) >> 9);
    }

    internal static uint aom_variance32x32_avx2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse)
    {
        __m256i vsse = _mm256_setzero_si256();
        __m256i vsum = default;
        variance32_avx2(src, src_stride, @ref, ref_stride, 32, ref vsse, ref vsum);
        int sum = variance_final_1024_avx2(vsse, vsum, ref sse);
        return sse - (uint)(((long)sum * sum) >> 10);
    }

    internal static uint aom_variance32x64_avx2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse)
    {
        __m256i vsse = _mm256_setzero_si256();
        __m256i vsum = default;
        variance32_avx2(src, src_stride, @ref, ref_stride, 64, ref vsse, ref vsum);
        int sum = variance_final_2048_avx2(vsse, vsum, ref sse);
        return sse - (uint)(((long)sum * sum) >> 11);
    }

    internal static uint aom_variance64x32_avx2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse)
    {
        __m256i vsse = _mm256_setzero_si256();
        __m256i vsum = default;
        variance64_avx2(src, src_stride, @ref, ref_stride, 32, ref vsse, ref vsum);
        int sum = variance_final_2048_avx2(vsse, vsum, ref sse);
        return sse - (uint)(((long)sum * sum) >> 11);
    }

    // AOM_VAR_LOOP_AVX2(bw, bh, bits, uh)
    internal static uint aom_variance64x64_avx2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse)
    {
        __m256i vsse = _mm256_setzero_si256();
        __m256i vsum = _mm256_setzero_si256();
        for (int i = 0; i < (64 / 32); i++)  // 64x32 * ( 64/32)
        {
            __m256i vsum16 = default;
            variance64_avx2(src, src_stride, @ref, ref_stride, 32, ref vsse, ref vsum16);
            vsum = _mm256_add_epi32(vsum, sum_to_32bit_avx2(vsum16));
            src += 32 * src_stride;
            @ref += 32 * ref_stride;
        }
        __m128i vsum_128 = mm256_add_hi_lo_epi32(vsum);
        int sum = variance_final_from_32bit_sum_avx2(vsse, vsum_128, ref sse);
        return sse - (uint)(((long)sum * sum) >> 12);
    }

    internal static uint aom_variance64x128_avx2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse)
    {
        __m256i vsse = _mm256_setzero_si256();
        __m256i vsum = _mm256_setzero_si256();
        for (int i = 0; i < (128 / 32); i++)  // 64x32 * (128/32)
        {
            __m256i vsum16 = default;
            variance64_avx2(src, src_stride, @ref, ref_stride, 32, ref vsse, ref vsum16);
            vsum = _mm256_add_epi32(vsum, sum_to_32bit_avx2(vsum16));
            src += 32 * src_stride;
            @ref += 32 * ref_stride;
        }
        __m128i vsum_128 = mm256_add_hi_lo_epi32(vsum);
        int sum = variance_final_from_32bit_sum_avx2(vsse, vsum_128, ref sse);
        return sse - (uint)(((long)sum * sum) >> 13);
    }

    internal static uint aom_variance128x64_avx2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse)
    {
        __m256i vsse = _mm256_setzero_si256();
        __m256i vsum = _mm256_setzero_si256();
        for (int i = 0; i < (64 / 16); i++)  // 128x16 * ( 64/16)
        {
            __m256i vsum16 = default;
            variance128_avx2(src, src_stride, @ref, ref_stride, 16, ref vsse, ref vsum16);
            vsum = _mm256_add_epi32(vsum, sum_to_32bit_avx2(vsum16));
            src += 16 * src_stride;
            @ref += 16 * ref_stride;
        }
        __m128i vsum_128 = mm256_add_hi_lo_epi32(vsum);
        int sum = variance_final_from_32bit_sum_avx2(vsse, vsum_128, ref sse);
        return sse - (uint)(((long)sum * sum) >> 13);
    }

    internal static uint aom_variance128x128_avx2(Ptr<byte> src, int src_stride, Ptr<byte> @ref, int ref_stride, ref uint sse)
    {
        __m256i vsse = _mm256_setzero_si256();
        __m256i vsum = _mm256_setzero_si256();
        for (int i = 0; i < (128 / 16); i++)  // 128x16 * (128/16)
        {
            __m256i vsum16 = default;
            variance128_avx2(src, src_stride, @ref, ref_stride, 16, ref vsse, ref vsum16);
            vsum = _mm256_add_epi32(vsum, sum_to_32bit_avx2(vsum16));
            src += 16 * src_stride;
            @ref += 16 * ref_stride;
        }
        __m128i vsum_128 = mm256_add_hi_lo_epi32(vsum);
        int sum = variance_final_from_32bit_sum_avx2(vsse, vsum_128, ref sse);
        return sse - (uint)(((long)sum * sum) >> 14);
    }

    internal static uint aom_mse16x16_avx2(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           ref uint sse)
    {
        aom_variance16x16_avx2(src, src_stride, @ref, ref_stride, ref sse);
        return sse;
    }

    internal static void aom_comp_avg_pred_avx2(Ptr<byte> comp_pred, Ptr<byte> pred, int width,
                                                int height, Ptr<byte> @ref, int ref_stride)
    {
        int row = 0;
        if (width == 8)
        {
            do
            {
                __m256i pred_0123 = _mm256_loadu_si256(pred);
                __m128i ref_0 = _mm_loadl_epi64(@ref);
                __m128i ref_1 = _mm_loadl_epi64(@ref + ref_stride);
                __m128i ref_2 = _mm_loadl_epi64(@ref + 2 * ref_stride);
                __m128i ref_3 = _mm_loadl_epi64(@ref + 3 * ref_stride);
                __m128i ref_01 = _mm_unpacklo_epi64(ref_0, ref_1);
                __m128i ref_23 = _mm_unpacklo_epi64(ref_2, ref_3);

                __m256i ref_0123 =
                    _mm256_inserti128_si256(_mm256_castsi128_si256(ref_01), ref_23, 1);
                __m256i average = _mm256_avg_epu8(pred_0123, ref_0123);
                _mm256_storeu_si256(comp_pred, average);

                row += 4;
                pred += 32;
                comp_pred += 32;
                @ref += 4 * ref_stride;
            } while (row < height);
        }
        else if (width == 16)
        {
            do
            {
                __m256i pred_0 = _mm256_loadu_si256(pred);
                __m256i pred_1 = _mm256_loadu_si256(pred + 32);
                __m256i tmp0 =
                    _mm256_castsi128_si256(_mm_loadu_si128(@ref));
                __m256i ref_0 = _mm256_inserti128_si256(
                    tmp0, _mm_loadu_si128(@ref + ref_stride), 1);
                __m256i tmp1 = _mm256_castsi128_si256(
                    _mm_loadu_si128(@ref + 2 * ref_stride));
                __m256i ref_1 = _mm256_inserti128_si256(
                    tmp1, _mm_loadu_si128(@ref + 3 * ref_stride), 1);
                __m256i average_0 = _mm256_avg_epu8(pred_0, ref_0);
                __m256i average_1 = _mm256_avg_epu8(pred_1, ref_1);
                _mm256_storeu_si256(comp_pred, average_0);
                _mm256_storeu_si256(comp_pred + 32, average_1);

                row += 4;
                pred += 64;
                comp_pred += 64;
                @ref += 4 * ref_stride;
            } while (row < height);
        }
        else if (width == 32)
        {
            do
            {
                __m256i pred_0 = _mm256_loadu_si256(pred);
                __m256i pred_1 = _mm256_loadu_si256(pred + 32);
                __m256i ref_0 = _mm256_loadu_si256(@ref);
                __m256i ref_1 =
                    _mm256_loadu_si256(@ref + ref_stride);
                __m256i average_0 = _mm256_avg_epu8(pred_0, ref_0);
                __m256i average_1 = _mm256_avg_epu8(pred_1, ref_1);
                _mm256_storeu_si256(comp_pred, average_0);
                _mm256_storeu_si256(comp_pred + 32, average_1);

                row += 2;
                pred += 64;
                comp_pred += 64;
                @ref += 2 * ref_stride;
            } while (row < height);
        }
        else if (width % 64 == 0)
        {
            do
            {
                for (int x = 0; x < width; x += 64)
                {
                    __m256i pred_0 = _mm256_loadu_si256(pred + x);
                    __m256i pred_1 =
                        _mm256_loadu_si256(pred + x + 32);
                    __m256i ref_0 = _mm256_loadu_si256(@ref + x);
                    __m256i ref_1 =
                        _mm256_loadu_si256(@ref + x + 32);
                    __m256i average_0 = _mm256_avg_epu8(pred_0, ref_0);
                    __m256i average_1 = _mm256_avg_epu8(pred_1, ref_1);
                    _mm256_storeu_si256(comp_pred + x, average_0);
                    _mm256_storeu_si256(comp_pred + x + 32, average_1);
                }
                row++;
                pred += width;
                comp_pred += width;
                @ref += ref_stride;
            } while (row < height);
        }
        else
        {
            aom_comp_avg_pred_c(comp_pred, pred, width, height, @ref, ref_stride);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void calc_sum_sse_wd32_avx2(Ptr<byte> src,
                                               Ptr<byte> @ref,
                                               __m256i set_one_minusone,
                                               Span<__m256i> sse_8x16,
                                               Span<__m256i> sum_8x16)
    {
        __m256i s00_256 = _mm256_loadu_si256(src);
        __m256i r00_256 = _mm256_loadu_si256(@ref);

        __m256i u_low_256 = _mm256_unpacklo_epi8(s00_256, r00_256);
        __m256i u_high_256 = _mm256_unpackhi_epi8(s00_256, r00_256);

        __m256i diff0 = _mm256_maddubs_epi16(u_low_256, set_one_minusone);
        __m256i diff1 = _mm256_maddubs_epi16(u_high_256, set_one_minusone);

        sse_8x16[0] = _mm256_add_epi32(sse_8x16[0], _mm256_madd_epi16(diff0, diff0));
        sse_8x16[1] = _mm256_add_epi32(sse_8x16[1], _mm256_madd_epi16(diff1, diff1));
        sum_8x16[0] = _mm256_add_epi16(sum_8x16[0], diff0);
        sum_8x16[1] = _mm256_add_epi16(sum_8x16[1], diff1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static __m256i calc_sum_sse_order(Span<__m256i> sse_hx16, Span<__m256i> sum_hx16,
                                              ref uint tot_sse, ref int tot_sum)
    {
        // s00 s01 s10 s11 s20 s21 s30 s31
        __m256i sse_results = _mm256_hadd_epi32(sse_hx16[0], sse_hx16[1]);
        // d00 d01 d02 d03 | d10 d11 d12 d13 | d20 d21 d22 d23 | d30 d31 d32 d33
        __m256i sum_result_r0 = _mm256_hadd_epi16(sum_hx16[0], sum_hx16[1]);
        // d00 d01 d10 d11 | d00 d02 d10 d11 | d20 d21 d30 d31 | d20 d21 d30 d31
        __m256i sum_result_1 = _mm256_hadd_epi16(sum_result_r0, sum_result_r0);
        // d00 d01 d10 d11 d20 d21 d30 d31 | X
        __m256i sum_result_3 = _mm256_permute4x64_epi64(sum_result_1, 0x08);
        // d00 d01 d10 d11 d20 d21 d30 d31
        __m256i sum_results =
            _mm256_cvtepi16_epi32(_mm256_castsi256_si128(sum_result_3));

        // Add sum & sse registers appropriately to get total sum & sse separately.
        // s0 s1 d0 d1 s2 s3 d2 d3
        __m256i sum_sse_add = _mm256_hadd_epi32(sse_results, sum_results);
        // s0 s1 s2 s3 d0 d1 d2 d3
        __m256i sum_sse_order_add = _mm256_permute4x64_epi64(sum_sse_add, 0xd8);
        // s0+s1 s2+s3 s0+s1 s2+s3 d0+d1 d2+d3 d0+d1 d2+d3
        __m256i sum_sse_order_add_1 =
            _mm256_hadd_epi32(sum_sse_order_add, sum_sse_order_add);
        // s0 x x x | d0 x x x
        __m256i sum_sse_order_add_final =
            _mm256_hadd_epi32(sum_sse_order_add_1, sum_sse_order_add_1);
        // s0
        uint first_value =
            (uint)_mm256_extract_epi32(sum_sse_order_add_final, 0);
        tot_sse += first_value;
        // d0
        int second_value = _mm256_extract_epi32(sum_sse_order_add_final, 4);
        tot_sum += second_value;
        return sum_sse_order_add;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void get_var_sse_sum_8x8_quad_avx2(
        Ptr<byte> src, int src_stride, Ptr<byte> @ref,
        int ref_stride, int h, Ptr<uint> sse8x8, Ptr<int> sum8x8,
        ref uint tot_sse, ref int tot_sum, Ptr<uint> var8x8)
    {
        System.Diagnostics.Debug.Assert(h <= 128);  // May overflow for larger height.
        Span<__m256i> sse_8x16 = stackalloc __m256i[2], sum_8x16 = stackalloc __m256i[2];
        sum_8x16[0] = _mm256_setzero_si256();
        sse_8x16[0] = _mm256_setzero_si256();
        sum_8x16[1] = sum_8x16[0];
        sse_8x16[1] = sse_8x16[0];
        __m256i set_one_minusone = _mm256_set1_epi16(unchecked((short)0xff01));

        for (int i = 0; i < h; i++)
        {
            // Process 8x32 block of one row.
            calc_sum_sse_wd32_avx2(src, @ref, set_one_minusone, sse_8x16, sum_8x16);
            src += src_stride;
            @ref += ref_stride;
        }

        __m256i sum_sse_order_add =
            calc_sum_sse_order(sse_8x16, sum_8x16, ref tot_sse, ref tot_sum);

        // s0 s1 s2 s3
        _mm_storeu_si128(sse8x8, _mm256_castsi256_si128(sum_sse_order_add));
        // d0 d1 d2 d3
        __m128i sum_temp8x8 = _mm256_extractf128_si256(sum_sse_order_add, 1);
        _mm_storeu_si128(sum8x8, sum_temp8x8);

        // (d0xd0 >> 6)=f0 (d1xd1 >> 6)=f1 (d2xd2 >> 6)=f2 (d3xd3 >> 6)=f3
        __m128i mull_results =
            _mm_srli_epi32(_mm_mullo_epi32(sum_temp8x8, sum_temp8x8), 6);
        // s0-f0=v0 s1-f1=v1 s2-f2=v2 s3-f3=v3
        __m128i variance_8x8 =
            _mm_sub_epi32(_mm256_castsi256_si128(sum_sse_order_add), mull_results);
        // v0 v1 v2 v3
        _mm_storeu_si128(var8x8, variance_8x8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void get_var_sse_sum_16x16_dual_avx2(
        Ptr<byte> src, int src_stride, Ptr<byte> @ref,
        int ref_stride, int h, Ptr<uint> sse16x16,
        ref uint tot_sse, ref int tot_sum, Ptr<uint> var16x16)
    {
        System.Diagnostics.Debug.Assert(h <= 128);  // May overflow for larger height.
        Span<__m256i> sse_16x16 = stackalloc __m256i[2], sum_16x16 = stackalloc __m256i[2];
        sum_16x16[0] = _mm256_setzero_si256();
        sse_16x16[0] = _mm256_setzero_si256();
        sum_16x16[1] = sum_16x16[0];
        sse_16x16[1] = sse_16x16[0];
        __m256i set_one_minusone = _mm256_set1_epi16(unchecked((short)0xff01));

        for (int i = 0; i < h; i++)
        {
            // Process 16x32 block of one row.
            calc_sum_sse_wd32_avx2(src, @ref, set_one_minusone, sse_16x16, sum_16x16);
            src += src_stride;
            @ref += ref_stride;
        }

        __m256i sum_sse_order_add =
            calc_sum_sse_order(sse_16x16, sum_16x16, ref tot_sse, ref tot_sum);

        __m256i sum_sse_order_add_1 =
            _mm256_hadd_epi32(sum_sse_order_add, sum_sse_order_add);

        // s0+s1 s2+s3 x x
        _mm_storel_epi64(sse16x16, _mm256_castsi256_si128(sum_sse_order_add_1));

        // d0+d1 d2+d3 x x
        __m128i sum_temp16x16 =
            _mm256_extractf128_si256(sum_sse_order_add_1, 1);

        // (d0xd0 >> 6)=f0 (d1xd1 >> 6)=f1 (d2xd2 >> 6)=f2 (d3xd3 >> 6)=f3
        __m128i mull_results =
            _mm_srli_epi32(_mm_mullo_epi32(sum_temp16x16, sum_temp16x16), 8);

        // s0-f0=v0 s1-f1=v1 s2-f2=v2 s3-f3=v3
        __m128i variance_16x16 =
            _mm_sub_epi32(_mm256_castsi256_si128(sum_sse_order_add_1), mull_results);

        // v0 v1 v2 v3
        _mm_storel_epi64(var16x16, variance_16x16);
    }

    internal static void aom_get_var_sse_sum_8x8_quad_avx2(Ptr<byte> src_ptr,
                                                           int source_stride,
                                                           Ptr<byte> ref_ptr, int ref_stride,
                                                           Ptr<uint> sse8x8, Ptr<int> sum8x8,
                                                           ref uint tot_sse, ref int tot_sum,
                                                           Ptr<uint> var8x8)
    {
        get_var_sse_sum_8x8_quad_avx2(src_ptr, source_stride, ref_ptr, ref_stride, 8,
                                      sse8x8, sum8x8, ref tot_sse, ref tot_sum, var8x8);
    }

    internal static void aom_get_var_sse_sum_16x16_dual_avx2(Ptr<byte> src_ptr,
                                                             int source_stride,
                                                             Ptr<byte> ref_ptr, int ref_stride,
                                                             Ptr<uint> sse16x16,
                                                             ref uint tot_sse, ref int tot_sum,
                                                             Ptr<uint> var16x16)
    {
        get_var_sse_sum_16x16_dual_avx2(src_ptr, source_stride, ref_ptr, ref_stride,
                                        16, sse16x16, ref tot_sse, ref tot_sum, var16x16);
    }
}
