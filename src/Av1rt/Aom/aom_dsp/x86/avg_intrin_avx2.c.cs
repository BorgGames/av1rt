// Port of libaom aom_dsp/x86/avg_intrin_avx2.c

global using static Av1rt.Aom.avg_intrin_avx2;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] Only the functions the port dispatches to (RTCD picks avx2 for them) and their helpers.
// Not ported: sign_extend_16bit_to_32bit_avx2, hadamard_16x16_avx2, aom_hadamard_16x16_avx2,
// aom_hadamard_32x32_avx2, aom_satd_avx2 (their tran_low_t C versions are not in the port: never
// executed in the subset, see avg.c.cs) and everything under CONFIG_AV1_HIGHBITDEPTH.
internal static partial class avg_intrin_avx2
{
    // [port] C passes __m256i in[8]; the rows are by-ref locals here, so once inlined they stay in
    // registers (a span of vectors is a stack buffer every step stores to and reloads from).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void hadamard_col8x2_avx2(ref __m256i in0, ref __m256i in1, ref __m256i in2,
                                             ref __m256i in3, ref __m256i in4, ref __m256i in5,
                                             ref __m256i in6, ref __m256i in7, int iter)
    {
        __m256i a0 = in0;
        __m256i a1 = in1;
        __m256i a2 = in2;
        __m256i a3 = in3;
        __m256i a4 = in4;
        __m256i a5 = in5;
        __m256i a6 = in6;
        __m256i a7 = in7;

        __m256i b0 = _mm256_add_epi16(a0, a1);
        __m256i b1 = _mm256_sub_epi16(a0, a1);
        __m256i b2 = _mm256_add_epi16(a2, a3);
        __m256i b3 = _mm256_sub_epi16(a2, a3);
        __m256i b4 = _mm256_add_epi16(a4, a5);
        __m256i b5 = _mm256_sub_epi16(a4, a5);
        __m256i b6 = _mm256_add_epi16(a6, a7);
        __m256i b7 = _mm256_sub_epi16(a6, a7);

        a0 = _mm256_add_epi16(b0, b2);
        a1 = _mm256_add_epi16(b1, b3);
        a2 = _mm256_sub_epi16(b0, b2);
        a3 = _mm256_sub_epi16(b1, b3);
        a4 = _mm256_add_epi16(b4, b6);
        a5 = _mm256_add_epi16(b5, b7);
        a6 = _mm256_sub_epi16(b4, b6);
        a7 = _mm256_sub_epi16(b5, b7);

        if (iter == 0)
        {
            b0 = _mm256_add_epi16(a0, a4);
            b7 = _mm256_add_epi16(a1, a5);
            b3 = _mm256_add_epi16(a2, a6);
            b4 = _mm256_add_epi16(a3, a7);
            b2 = _mm256_sub_epi16(a0, a4);
            b6 = _mm256_sub_epi16(a1, a5);
            b1 = _mm256_sub_epi16(a2, a6);
            b5 = _mm256_sub_epi16(a3, a7);

            a0 = _mm256_unpacklo_epi16(b0, b1);
            a1 = _mm256_unpacklo_epi16(b2, b3);
            a2 = _mm256_unpackhi_epi16(b0, b1);
            a3 = _mm256_unpackhi_epi16(b2, b3);
            a4 = _mm256_unpacklo_epi16(b4, b5);
            a5 = _mm256_unpacklo_epi16(b6, b7);
            a6 = _mm256_unpackhi_epi16(b4, b5);
            a7 = _mm256_unpackhi_epi16(b6, b7);

            b0 = _mm256_unpacklo_epi32(a0, a1);
            b1 = _mm256_unpacklo_epi32(a4, a5);
            b2 = _mm256_unpackhi_epi32(a0, a1);
            b3 = _mm256_unpackhi_epi32(a4, a5);
            b4 = _mm256_unpacklo_epi32(a2, a3);
            b5 = _mm256_unpacklo_epi32(a6, a7);
            b6 = _mm256_unpackhi_epi32(a2, a3);
            b7 = _mm256_unpackhi_epi32(a6, a7);

            in0 = _mm256_unpacklo_epi64(b0, b1);
            in1 = _mm256_unpackhi_epi64(b0, b1);
            in2 = _mm256_unpacklo_epi64(b2, b3);
            in3 = _mm256_unpackhi_epi64(b2, b3);
            in4 = _mm256_unpacklo_epi64(b4, b5);
            in5 = _mm256_unpackhi_epi64(b4, b5);
            in6 = _mm256_unpacklo_epi64(b6, b7);
            in7 = _mm256_unpackhi_epi64(b6, b7);
        }
        else
        {
            in0 = _mm256_add_epi16(a0, a4);
            in7 = _mm256_add_epi16(a1, a5);
            in3 = _mm256_add_epi16(a2, a6);
            in4 = _mm256_add_epi16(a3, a7);
            in2 = _mm256_sub_epi16(a0, a4);
            in6 = _mm256_sub_epi16(a1, a5);
            in1 = _mm256_sub_epi16(a2, a6);
            in5 = _mm256_sub_epi16(a3, a7);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_hadamard_lp_8x8_dual_avx2(Ptr<short> src_diff,
                                                       int src_stride, Ptr<short> coeff)
    {
        // [port] The eight rows in locals (see hadamard_col8x2_avx2), read and written through
        // spans sliced once.
        ReadOnlySpan<short> sd = src_diff.AsSpan(0, 7 * src_stride + 16);
        __m256i src0 = _mm256_loadu_si256(sd);
        __m256i src1 = _mm256_loadu_si256(sd.Slice(src_stride, 16));
        __m256i src2 = _mm256_loadu_si256(sd.Slice(2 * src_stride, 16));
        __m256i src3 = _mm256_loadu_si256(sd.Slice(3 * src_stride, 16));
        __m256i src4 = _mm256_loadu_si256(sd.Slice(4 * src_stride, 16));
        __m256i src5 = _mm256_loadu_si256(sd.Slice(5 * src_stride, 16));
        __m256i src6 = _mm256_loadu_si256(sd.Slice(6 * src_stride, 16));
        __m256i src7 = _mm256_loadu_si256(sd.Slice(7 * src_stride, 16));

        hadamard_col8x2_avx2(ref src0, ref src1, ref src2, ref src3, ref src4, ref src5,
                             ref src6, ref src7, 0);
        hadamard_col8x2_avx2(ref src0, ref src1, ref src2, ref src3, ref src4, ref src5,
                             ref src6, ref src7, 1);

        Span<short> c = coeff.AsSpan(0, 128);
        _mm256_storeu_si256(c, _mm256_permute2x128_si256(src0, src1, 0x20));
        _mm256_storeu_si256(c.Slice(16), _mm256_permute2x128_si256(src2, src3, 0x20));
        _mm256_storeu_si256(c.Slice(32), _mm256_permute2x128_si256(src4, src5, 0x20));
        _mm256_storeu_si256(c.Slice(48), _mm256_permute2x128_si256(src6, src7, 0x20));
        _mm256_storeu_si256(c.Slice(64), _mm256_permute2x128_si256(src0, src1, 0x31));
        _mm256_storeu_si256(c.Slice(80), _mm256_permute2x128_si256(src2, src3, 0x31));
        _mm256_storeu_si256(c.Slice(96), _mm256_permute2x128_si256(src4, src5, 0x31));
        _mm256_storeu_si256(c.Slice(112), _mm256_permute2x128_si256(src6, src7, 0x31));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_hadamard_lp_16x16_avx2(Ptr<short> src_diff, int src_stride,
                                                    Ptr<short> coeff)
    {
        Ptr<short> t_coeff = coeff;
        for (int idx = 0; idx < 2; ++idx)
        {
            Ptr<short> src_ptr = src_diff + idx * 8 * src_stride;
            aom_hadamard_lp_8x8_dual_avx2(src_ptr, src_stride,
                                          t_coeff + (idx * 64 * 2));
        }

        // [port] In place over the 256 coefficients through one span (t_coeff == coeff here).
        Span<short> tc = t_coeff.AsSpan(0, 256);
        for (int idx = 0; idx < 64; idx += 16)
        {
            __m256i coeff0 = _mm256_loadu_si256(tc.Slice(idx, 16));
            __m256i coeff1 = _mm256_loadu_si256(tc.Slice(idx + 64, 16));
            __m256i coeff2 = _mm256_loadu_si256(tc.Slice(idx + 128, 16));
            __m256i coeff3 = _mm256_loadu_si256(tc.Slice(idx + 192, 16));

            __m256i b0 = _mm256_add_epi16(coeff0, coeff1);
            __m256i b1 = _mm256_sub_epi16(coeff0, coeff1);
            __m256i b2 = _mm256_add_epi16(coeff2, coeff3);
            __m256i b3 = _mm256_sub_epi16(coeff2, coeff3);

            b0 = _mm256_srai_epi16(b0, 1);
            b1 = _mm256_srai_epi16(b1, 1);
            b2 = _mm256_srai_epi16(b2, 1);
            b3 = _mm256_srai_epi16(b3, 1);
            _mm256_storeu_si256(tc.Slice(idx, 16), _mm256_add_epi16(b0, b2));
            _mm256_storeu_si256(tc.Slice(idx + 64, 16), _mm256_add_epi16(b1, b3));
            _mm256_storeu_si256(tc.Slice(idx + 128, 16), _mm256_sub_epi16(b0, b2));
            _mm256_storeu_si256(tc.Slice(idx + 192, 16), _mm256_sub_epi16(b1, b3));
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static int aom_satd_lp_avx2(Ptr<short> coeff, int length)
    {
        __m256i one = _mm256_set1_epi16(1);
        __m256i accum = _mm256_setzero_si256();

        for (int i = 0; i < length; i += 16)
        {
            __m256i src_line = _mm256_loadu_si256(coeff);
            __m256i abs = _mm256_abs_epi16(src_line);
            __m256i sum = _mm256_madd_epi16(abs, one);
            accum = _mm256_add_epi32(accum, sum);
            coeff += 16;
        }

        {  // 32 bit horizontal add
            __m256i a = _mm256_srli_si256(accum, 8);
            __m256i b = _mm256_add_epi32(accum, a);
            __m256i c = _mm256_srli_epi64(b, 32);
            __m256i d = _mm256_add_epi32(b, c);
            __m128i accum_128 = _mm_add_epi32(_mm256_castsi256_si128(d),
                                              _mm256_extractf128_si256(d, 1));
            return _mm_cvtsi128_si32(accum_128);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_avg_8x8_quad_avx2(Ptr<byte> s, int p, int x16_idx, int y16_idx,
                                               Ptr<int> avg)
    {
        Ptr<byte> s_y0 = s + y16_idx * p + x16_idx;
        Ptr<byte> s_y1 = s_y0 + 8 * p;
        __m256i sum0, sum1, s0, s1, s2, s3, u0;
        u0 = _mm256_setzero_si256();
        s0 = _mm256_sad_epu8(yy_loadu2_128(s_y1, s_y0), u0);
        s1 = _mm256_sad_epu8(yy_loadu2_128(s_y1 + p, s_y0 + p), u0);
        s2 = _mm256_sad_epu8(yy_loadu2_128(s_y1 + 2 * p, s_y0 + 2 * p), u0);
        s3 = _mm256_sad_epu8(yy_loadu2_128(s_y1 + 3 * p, s_y0 + 3 * p), u0);
        sum0 = _mm256_add_epi16(s0, s1);
        sum1 = _mm256_add_epi16(s2, s3);
        s0 = _mm256_sad_epu8(yy_loadu2_128(s_y1 + 4 * p, s_y0 + 4 * p), u0);
        s1 = _mm256_sad_epu8(yy_loadu2_128(s_y1 + 5 * p, s_y0 + 5 * p), u0);
        s2 = _mm256_sad_epu8(yy_loadu2_128(s_y1 + 6 * p, s_y0 + 6 * p), u0);
        s3 = _mm256_sad_epu8(yy_loadu2_128(s_y1 + 7 * p, s_y0 + 7 * p), u0);
        sum0 = _mm256_add_epi16(sum0, _mm256_add_epi16(s0, s1));
        sum1 = _mm256_add_epi16(sum1, _mm256_add_epi16(s2, s3));
        sum0 = _mm256_add_epi16(sum0, sum1);

        // (avg + 32) >> 6
        __m256i rounding = _mm256_set1_epi32(32);
        sum0 = _mm256_add_epi32(sum0, rounding);
        sum0 = _mm256_srli_epi32(sum0, 6);
        __m128i lo = _mm256_castsi256_si128(sum0);
        __m128i hi = _mm256_extracti128_si256(sum0, 1);
        avg[0] = _mm_cvtsi128_si32(lo);
        avg[1] = _mm_extract_epi32(lo, 2);
        avg[2] = _mm_cvtsi128_si32(hi);
        avg[3] = _mm_extract_epi32(hi, 2);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_int_pro_row_avx2(Ptr<short> hbuf, Ptr<byte> @ref,
                                              int ref_stride, int width,
                                              int height, int norm_factor)
    {
        // SIMD implementation assumes width and height to be multiple of 16 and 2
        // respectively. For any odd width or height, SIMD support needs to be added.
        Debug.Assert(width % 16 == 0 && height % 2 == 0);

        if (width % 32 == 0)
        {
            __m256i zero = _mm256_setzero_si256();
            for (int wd = 0; wd < width; wd += 32)
            {
                Ptr<byte> ref_tmp = @ref + wd;
                Ptr<short> hbuf_tmp = hbuf + wd;
                __m256i s0 = zero;
                __m256i s1 = zero;
                int idx = 0;
                do
                {
                    __m256i src_line = _mm256_loadu_si256(ref_tmp);
                    __m256i t0 = _mm256_unpacklo_epi8(src_line, zero);
                    __m256i t1 = _mm256_unpackhi_epi8(src_line, zero);
                    s0 = _mm256_add_epi16(s0, t0);
                    s1 = _mm256_add_epi16(s1, t1);
                    ref_tmp += ref_stride;

                    src_line = _mm256_loadu_si256(ref_tmp);
                    t0 = _mm256_unpacklo_epi8(src_line, zero);
                    t1 = _mm256_unpackhi_epi8(src_line, zero);
                    s0 = _mm256_add_epi16(s0, t0);
                    s1 = _mm256_add_epi16(s1, t1);
                    ref_tmp += ref_stride;
                    idx += 2;
                } while (idx < height);
                s0 = _mm256_srai_epi16(s0, norm_factor);
                s1 = _mm256_srai_epi16(s1, norm_factor);
                _mm_storeu_si128(hbuf_tmp, _mm256_castsi256_si128(s0));
                _mm_storeu_si128(hbuf_tmp + 8, _mm256_castsi256_si128(s1));
                _mm_storeu_si128(hbuf_tmp + 16,
                                 _mm256_extractf128_si256(s0, 1));
                _mm_storeu_si128(hbuf_tmp + 24,
                                 _mm256_extractf128_si256(s1, 1));
            }
        }
        else if (width % 16 == 0)
        {
            aom_int_pro_row_sse2(hbuf, @ref, ref_stride, width, height, norm_factor);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void load_from_src_buf(Ptr<byte> ref1, Span<__m256i> src,
                                          int stride)
    {
        src[0] = _mm256_loadu_si256(ref1);
        src[1] = _mm256_loadu_si256(ref1 + stride);
        src[2] = _mm256_loadu_si256(ref1 + (2 * stride));
        src[3] = _mm256_loadu_si256(ref1 + (3 * stride));
    }

    // [port] The CALC_TOT_SAD_AND_STORE macro, with the locals it uses as parameters.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CALC_TOT_SAD_AND_STORE(__m256i r0, __m256i r1, __m256i r2, __m256i r3,
                                               Ptr<short> vbuf, int norm_factor)
    {
        /* r00 r10 x x r01 r11 x x | r02 r12 x x r03 r13 x x */
        __m256i r01 = _mm256_add_epi16(_mm256_slli_si256(r1, 2), r0);
        /* r00 r10 r20 x r01 r11 r21 x | r02 r12 r22 x r03 r13 r23 x */
        __m256i r012 = _mm256_add_epi16(_mm256_slli_si256(r2, 4), r01);
        /* r00 r10 r20 r30 r01 r11 r21 r31 | r02 r12 r22 r32 r03 r13 r23 r33 */
        __m256i result0 = _mm256_add_epi16(_mm256_slli_si256(r3, 6), r012);

        __m128i results0 = _mm_add_epi16(
            _mm256_castsi256_si128(result0), _mm256_extractf128_si256(result0, 1));
        __m128i results1 =
            _mm_add_epi16(results0, _mm_srli_si128(results0, 8));
        _mm_storel_epi64(vbuf, _mm_srli_epi16(results1, norm_factor));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void aom_int_pro_col_16wd_avx2(Ptr<short> vbuf, Ptr<byte> @ref,
                                                  int ref_stride,
                                                  int height,
                                                  int norm_factor)
    {
        __m256i zero = _mm256_setzero_si256();
        int ht = 0;
        // Post sad operation, the data is present in lower 16-bit of each 64-bit lane
        // and higher 16-bits are Zero. Here, we are processing 8 rows at a time to
        // utilize the higher 16-bits efficiently.
        do
        {
            __m256i src_00 =
                _mm256_castsi128_si256(_mm_loadu_si128(@ref));
            src_00 = _mm256_inserti128_si256(
                src_00, _mm_loadu_si128(@ref + ref_stride * 4), 1);
            __m256i src_01 = _mm256_castsi128_si256(
                _mm_loadu_si128(@ref + ref_stride * 1));
            src_01 = _mm256_inserti128_si256(
                src_01, _mm_loadu_si128(@ref + ref_stride * 5), 1);
            __m256i src_10 = _mm256_castsi128_si256(
                _mm_loadu_si128(@ref + ref_stride * 2));
            src_10 = _mm256_inserti128_si256(
                src_10, _mm_loadu_si128(@ref + ref_stride * 6), 1);
            __m256i src_11 = _mm256_castsi128_si256(
                _mm_loadu_si128(@ref + ref_stride * 3));
            src_11 = _mm256_inserti128_si256(
                src_11, _mm_loadu_si128(@ref + ref_stride * 7), 1);

            // s00 x x x s01 x x x | s40 x x x s41 x x x
            __m256i s0 = _mm256_sad_epu8(src_00, zero);
            // s10 x x x s11 x x x | s50 x x x s51 x x x
            __m256i s1 = _mm256_sad_epu8(src_01, zero);
            // s20 x x x s21 x x x | s60 x x x s61 x x x
            __m256i s2 = _mm256_sad_epu8(src_10, zero);
            // s30 x x x s31 x x x | s70 x x x s71 x x x
            __m256i s3 = _mm256_sad_epu8(src_11, zero);

            // s00 s10 x x x x x x | s40 s50 x x x x x x
            __m256i s0_lo = _mm256_unpacklo_epi16(s0, s1);
            // s01 s11 x x x x x x | s41 s51 x x x x x x
            __m256i s0_hi = _mm256_unpackhi_epi16(s0, s1);
            // s20 s30 x x x x x x | s60 s70 x x x x x x
            __m256i s1_lo = _mm256_unpacklo_epi16(s2, s3);
            // s21 s31 x x x x x x | s61 s71 x x x x x x
            __m256i s1_hi = _mm256_unpackhi_epi16(s2, s3);

            // s0 s1 x x x x x x | s4 s5 x x x x x x
            __m256i s0_add = _mm256_add_epi16(s0_lo, s0_hi);
            // s2 s3 x x x x x x | s6 s7 x x x x x x
            __m256i s1_add = _mm256_add_epi16(s1_lo, s1_hi);

            // s1 s1 s2 s3 s4 s5 s6 s7
            __m128i results = _mm256_castsi256_si128(
                _mm256_permute4x64_epi64(_mm256_unpacklo_epi32(s0_add, s1_add), 0x08));
            _mm_storeu_si128(vbuf, _mm_srli_epi16(results, norm_factor));
            vbuf += 8;
            @ref += (ref_stride << 3);
            ht += 8;
        } while (ht < height);
    }

    // [port] The `__m256i src[16]` (8, 4) arrays declared inside the row loops are hoisted out of
    // the loops: a stackalloc in a C# loop would allocate on every iteration.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void aom_int_pro_col_avx2(Ptr<short> vbuf, Ptr<byte> @ref,
                                              int ref_stride, int width,
                                              int height, int norm_factor)
    {
        Debug.Assert(width % 16 == 0);
        if (width == 128)
        {
            __m256i zero = _mm256_setzero_si256();
            Span<__m256i> src = stackalloc __m256i[16];
            for (int ht = 0; ht < height; ht += 4)
            {
                // Load source data.
                load_from_src_buf(@ref, src.Slice(0), ref_stride);
                load_from_src_buf(@ref + 32, src.Slice(4), ref_stride);
                load_from_src_buf(@ref + 64, src.Slice(8), ref_stride);
                load_from_src_buf(@ref + 96, src.Slice(12), ref_stride);

                // Row0 output: r00 x x x r01 x x x | r02 x x x r03 x x x
                __m256i s0 = _mm256_add_epi16(_mm256_sad_epu8(src[0], zero),
                                              _mm256_sad_epu8(src[4], zero));
                __m256i s1 = _mm256_add_epi16(_mm256_sad_epu8(src[8], zero),
                                              _mm256_sad_epu8(src[12], zero));
                __m256i r0 = _mm256_add_epi16(s0, s1);
                // Row1 output: r10 x x x r11 x x x | r12 x x x r13 x x x
                __m256i s2 = _mm256_add_epi16(_mm256_sad_epu8(src[1], zero),
                                              _mm256_sad_epu8(src[5], zero));
                __m256i s3 = _mm256_add_epi16(_mm256_sad_epu8(src[9], zero),
                                              _mm256_sad_epu8(src[13], zero));
                __m256i r1 = _mm256_add_epi16(s2, s3);
                // Row2 output: r20 x x x r21 x x x | r22 x x x r23 x x x
                __m256i s4 = _mm256_add_epi16(_mm256_sad_epu8(src[2], zero),
                                              _mm256_sad_epu8(src[6], zero));
                __m256i s5 = _mm256_add_epi16(_mm256_sad_epu8(src[10], zero),
                                              _mm256_sad_epu8(src[14], zero));
                __m256i r2 = _mm256_add_epi16(s4, s5);
                // Row3 output: r30 x x x r31 x x x | r32 x x x r33 x x x
                __m256i s6 = _mm256_add_epi16(_mm256_sad_epu8(src[3], zero),
                                              _mm256_sad_epu8(src[7], zero));
                __m256i s7 = _mm256_add_epi16(_mm256_sad_epu8(src[11], zero),
                                              _mm256_sad_epu8(src[15], zero));
                __m256i r3 = _mm256_add_epi16(s6, s7);

                CALC_TOT_SAD_AND_STORE(r0, r1, r2, r3, vbuf, norm_factor);
                vbuf += 4;
                @ref += ref_stride << 2;
            }
        }
        else if (width == 64)
        {
            __m256i zero = _mm256_setzero_si256();
            Span<__m256i> src = stackalloc __m256i[8];
            for (int ht = 0; ht < height; ht += 4)
            {
                // Load source data.
                load_from_src_buf(@ref, src.Slice(0), ref_stride);
                load_from_src_buf(@ref + 32, src.Slice(4), ref_stride);

                // Row0 output: r00 x x x r01 x x x | r02 x x x r03 x x x
                __m256i s0 = _mm256_sad_epu8(src[0], zero);
                __m256i s1 = _mm256_sad_epu8(src[4], zero);
                __m256i r0 = _mm256_add_epi16(s0, s1);
                // Row1 output: r10 x x x r11 x x x | r12 x x x r13 x x x
                __m256i s2 = _mm256_sad_epu8(src[1], zero);
                __m256i s3 = _mm256_sad_epu8(src[5], zero);
                __m256i r1 = _mm256_add_epi16(s2, s3);
                // Row2 output: r20 x x x r21 x x x | r22 x x x r23 x x x
                __m256i s4 = _mm256_sad_epu8(src[2], zero);
                __m256i s5 = _mm256_sad_epu8(src[6], zero);
                __m256i r2 = _mm256_add_epi16(s4, s5);
                // Row3 output: r30 x x x r31 x x x | r32 x x x r33 x x x
                __m256i s6 = _mm256_sad_epu8(src[3], zero);
                __m256i s7 = _mm256_sad_epu8(src[7], zero);
                __m256i r3 = _mm256_add_epi16(s6, s7);

                CALC_TOT_SAD_AND_STORE(r0, r1, r2, r3, vbuf, norm_factor);
                vbuf += 4;
                @ref += ref_stride << 2;
            }
        }
        else if (width == 32)
        {
            Debug.Assert(height % 2 == 0);
            __m256i zero = _mm256_setzero_si256();
            Span<__m256i> src = stackalloc __m256i[4];
            for (int ht = 0; ht < height; ht += 4)
            {
                // Load source data.
                load_from_src_buf(@ref, src.Slice(0), ref_stride);

                // s00 x x x s01 x x x s02 x x x s03 x x x
                __m256i r0 = _mm256_sad_epu8(src[0], zero);
                // s10 x x x s11 x x x s12 x x x s13 x x x
                __m256i r1 = _mm256_sad_epu8(src[1], zero);
                // s20 x x x s21 x x x s22 x x x s23 x x x
                __m256i r2 = _mm256_sad_epu8(src[2], zero);
                // s30 x x x s31 x x x s32 x x x s33 x x x
                __m256i r3 = _mm256_sad_epu8(src[3], zero);

                CALC_TOT_SAD_AND_STORE(r0, r1, r2, r3, vbuf, norm_factor);
                vbuf += 4;
                @ref += ref_stride << 2;
            }
        }
        else if (width == 16)
        {
            aom_int_pro_col_16wd_avx2(vbuf, @ref, ref_stride, height, norm_factor);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void calc_vector_mean_sse_64wd(Ptr<short> @ref,
                                                  Ptr<short> src, ref __m256i mean,
                                                  ref __m256i sse)
    {
        __m256i src_line0 = _mm256_loadu_si256(src);
        __m256i src_line1 = _mm256_loadu_si256(src + 16);
        __m256i src_line2 = _mm256_loadu_si256(src + 32);
        __m256i src_line3 = _mm256_loadu_si256(src + 48);
        __m256i ref_line0 = _mm256_loadu_si256(@ref);
        __m256i ref_line1 = _mm256_loadu_si256(@ref + 16);
        __m256i ref_line2 = _mm256_loadu_si256(@ref + 32);
        __m256i ref_line3 = _mm256_loadu_si256(@ref + 48);

        __m256i diff0 = _mm256_sub_epi16(ref_line0, src_line0);
        __m256i diff1 = _mm256_sub_epi16(ref_line1, src_line1);
        __m256i diff2 = _mm256_sub_epi16(ref_line2, src_line2);
        __m256i diff3 = _mm256_sub_epi16(ref_line3, src_line3);
        __m256i diff_sqr0 = _mm256_madd_epi16(diff0, diff0);
        __m256i diff_sqr1 = _mm256_madd_epi16(diff1, diff1);
        __m256i diff_sqr2 = _mm256_madd_epi16(diff2, diff2);
        __m256i diff_sqr3 = _mm256_madd_epi16(diff3, diff3);

        mean = _mm256_add_epi16(mean, _mm256_add_epi16(diff0, diff1));
        mean = _mm256_add_epi16(mean, diff2);
        mean = _mm256_add_epi16(mean, diff3);
        sse = _mm256_add_epi32(sse, _mm256_add_epi32(diff_sqr0, diff_sqr1));
        sse = _mm256_add_epi32(sse, diff_sqr2);
        sse = _mm256_add_epi32(sse, diff_sqr3);
    }

    // [port] The CALC_VAR_FROM_MEAN_SSE(mean, sse) macro; returns the value it assigns to var.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CALC_VAR_FROM_MEAN_SSE(__m256i mean, __m256i sse, int bwl)
    {
        mean = _mm256_madd_epi16(mean, _mm256_set1_epi16(1));
        mean = _mm256_hadd_epi32(mean, sse);
        mean = _mm256_add_epi32(mean, _mm256_bsrli_epi128(mean, 4));
        __m128i result = _mm_add_epi32(_mm256_castsi256_si128(mean),
                                       _mm256_extractf128_si256(mean, 1));
        /*(mean * mean): dynamic range 31 bits.*/
        int mean_int = _mm_extract_epi32(result, 0);
        int sse_int = _mm_extract_epi32(result, 2);
        uint mean_abs = (uint)Math.Abs(mean_int);
        return (int)((uint)sse_int - ((mean_abs * mean_abs) >> (bwl + 2)));
    }

    // ref: [0 - 510]
    // src: [0 - 510]
    // bwl: {2, 3, 4, 5}
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static int aom_vector_var_avx2(Ptr<short> @ref, Ptr<short> src, int bwl)
    {
        int width = 4 << bwl;
        Debug.Assert(width % 16 == 0 && width <= 128);
        int var = 0;

        // Instead of having a loop over width 16, considered loop unrolling to avoid
        // some addition operations.
        if (width == 128)
        {
            __m256i mean = _mm256_setzero_si256();
            __m256i sse = _mm256_setzero_si256();

            calc_vector_mean_sse_64wd(src, @ref, ref mean, ref sse);
            calc_vector_mean_sse_64wd(src + 64, @ref + 64, ref mean, ref sse);
            var = CALC_VAR_FROM_MEAN_SSE(mean, sse, bwl);
        }
        else if (width == 64)
        {
            __m256i mean = _mm256_setzero_si256();
            __m256i sse = _mm256_setzero_si256();

            calc_vector_mean_sse_64wd(src, @ref, ref mean, ref sse);
            var = CALC_VAR_FROM_MEAN_SSE(mean, sse, bwl);
        }
        else if (width == 32)
        {
            __m256i src_line0 = _mm256_loadu_si256(src);
            __m256i ref_line0 = _mm256_loadu_si256(@ref);
            __m256i src_line1 = _mm256_loadu_si256(src + 16);
            __m256i ref_line1 = _mm256_loadu_si256(@ref + 16);

            __m256i diff0 = _mm256_sub_epi16(ref_line0, src_line0);
            __m256i diff1 = _mm256_sub_epi16(ref_line1, src_line1);
            __m256i diff_sqr0 = _mm256_madd_epi16(diff0, diff0);
            __m256i diff_sqr1 = _mm256_madd_epi16(diff1, diff1);
            __m256i sse = _mm256_add_epi32(diff_sqr0, diff_sqr1);
            __m256i mean = _mm256_add_epi16(diff0, diff1);

            var = CALC_VAR_FROM_MEAN_SSE(mean, sse, bwl);
        }
        else if (width == 16)
        {
            __m256i src_line = _mm256_loadu_si256(src);
            __m256i ref_line = _mm256_loadu_si256(@ref);
            __m256i mean = _mm256_sub_epi16(ref_line, src_line);
            __m256i sse = _mm256_madd_epi16(mean, mean);

            var = CALC_VAR_FROM_MEAN_SSE(mean, sse, bwl);
        }
        return var;
    }
}
