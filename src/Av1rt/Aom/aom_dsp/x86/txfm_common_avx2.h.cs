// Port of libaom aom_dsp/x86/txfm_common_avx2.h

global using static Av1rt.Aom.txfm_common_avx2;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] `__m256i *` array parameters are Span<__m256i>; pointers to a single register
// (`__m256i *in0` of the butterflies) are `ref __m256i`. In-place calls pass the same span
// for input and output: every function reads its inputs before writing, as in C.
internal static class txfm_common_avx2
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    // PORT-NOTE: C takes int16_t a, b. The callers pass int expressions (cospi[] entries and
    // their negations) that C converts implicitly; the int parameters keep those call sites
    // verbatim, and the (uint16_t) casts below keep the same low 16 bits.
    [MethodImpl(AI)]
    internal static __m256i pair_set_w16_epi16(int a, int b)
    {
        return _mm256_set1_epi32(
            (int)(((uint)(ushort)(a)) | (((uint)(ushort)(b)) << 16)));
    }

    [MethodImpl(AI)]
    internal static void btf_16_w16_avx2(__m256i w0, __m256i w1,
                                         ref __m256i in0, ref __m256i in1, __m256i _r,
                                         int cos_bit)
    {
        __m256i t0 = _mm256_unpacklo_epi16(in0, in1);
        __m256i t1 = _mm256_unpackhi_epi16(in0, in1);
        __m256i u0 = _mm256_madd_epi16(t0, w0);
        __m256i u1 = _mm256_madd_epi16(t1, w0);
        __m256i v0 = _mm256_madd_epi16(t0, w1);
        __m256i v1 = _mm256_madd_epi16(t1, w1);

        __m256i a0 = _mm256_add_epi32(u0, _r);
        __m256i a1 = _mm256_add_epi32(u1, _r);
        __m256i b0 = _mm256_add_epi32(v0, _r);
        __m256i b1 = _mm256_add_epi32(v1, _r);

        __m256i c0 = _mm256_srai_epi32(a0, cos_bit);
        __m256i c1 = _mm256_srai_epi32(a1, cos_bit);
        __m256i d0 = _mm256_srai_epi32(b0, cos_bit);
        __m256i d1 = _mm256_srai_epi32(b1, cos_bit);

        in0 = _mm256_packs_epi32(c0, c1);
        in1 = _mm256_packs_epi32(d0, d1);
    }

    [MethodImpl(AI)]
    internal static void btf_16_adds_subs_avx2(ref __m256i in0, ref __m256i in1)
    {
        __m256i _in0 = in0;
        __m256i _in1 = in1;
        in0 = _mm256_adds_epi16(_in0, _in1);
        in1 = _mm256_subs_epi16(_in0, _in1);
    }

    [MethodImpl(AI)]
    internal static void btf_32_add_sub_avx2(ref __m256i in0, ref __m256i in1)
    {
        __m256i _in0 = in0;
        __m256i _in1 = in1;
        in0 = _mm256_add_epi32(_in0, _in1);
        in1 = _mm256_sub_epi32(_in0, _in1);
    }

    [MethodImpl(AI)]
    internal static void btf_16_adds_subs_out_avx2(ref __m256i out0, ref __m256i out1,
                                                   __m256i in0, __m256i in1)
    {
        __m256i _in0 = in0;
        __m256i _in1 = in1;
        out0 = _mm256_adds_epi16(_in0, _in1);
        out1 = _mm256_subs_epi16(_in0, _in1);
    }

    [MethodImpl(AI)]
    internal static void btf_32_add_sub_out_avx2(ref __m256i out0, ref __m256i out1,
                                                 __m256i in0, __m256i in1)
    {
        __m256i _in0 = in0;
        __m256i _in1 = in1;
        out0 = _mm256_add_epi32(_in0, _in1);
        out1 = _mm256_sub_epi32(_in0, _in1);
    }

    [MethodImpl(AI)]
    internal static __m256i load_16bit_to_16bit_avx2(Ptr<short> a)
    {
        return _mm256_load_si256(a);
    }

    [MethodImpl(AI)]
    internal static void load_buffer_16bit_to_16bit_avx2(Ptr<short> @in,
                                                         int stride, Span<__m256i> @out,
                                                         int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            @out[i] = load_16bit_to_16bit_avx2(@in + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void load_buffer_16bit_to_16bit_flip_avx2(Ptr<short> @in,
                                                              int stride,
                                                              Span<__m256i> @out,
                                                              int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            @out[out_size - i - 1] = load_16bit_to_16bit_avx2(@in + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static __m256i load_32bit_to_16bit_w16_avx2(Ptr<int> a)
    {
        __m256i a_low = _mm256_lddqu_si256(a);
        __m256i b = _mm256_packs_epi32(a_low, _mm256_load_si256(a + 8));
        return _mm256_permute4x64_epi64(b, 0xD8);
    }

    [MethodImpl(AI)]
    internal static void load_buffer_32bit_to_16bit_w16_avx2(Ptr<int> @in,
                                                             int stride, Span<__m256i> @out,
                                                             int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            @out[i] = load_32bit_to_16bit_w16_avx2(@in + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void transpose2_8x8_avx2(Span<__m256i> @in, Span<__m256i> @out)
    {
        Span<__m256i> t = stackalloc __m256i[16];
        Span<__m256i> u = stackalloc __m256i[16];
        // (1st, 2nd) ==> (lo, hi)
        //   (0, 1)   ==>  (0, 1)
        //   (2, 3)   ==>  (2, 3)
        //   (4, 5)   ==>  (4, 5)
        //   (6, 7)   ==>  (6, 7)
        for (int i = 0; i < 4; i++)
        {
            t[2 * i] = _mm256_unpacklo_epi16(@in[2 * i], @in[2 * i + 1]);
            t[2 * i + 1] = _mm256_unpackhi_epi16(@in[2 * i], @in[2 * i + 1]);
        }

        // (1st, 2nd) ==> (lo, hi)
        //   (0, 2)   ==>  (0, 2)
        //   (1, 3)   ==>  (1, 3)
        //   (4, 6)   ==>  (4, 6)
        //   (5, 7)   ==>  (5, 7)
        for (int i = 0; i < 2; i++)
        {
            u[i] = _mm256_unpacklo_epi32(t[i], t[i + 2]);
            u[i + 2] = _mm256_unpackhi_epi32(t[i], t[i + 2]);

            u[i + 4] = _mm256_unpacklo_epi32(t[i + 4], t[i + 6]);
            u[i + 6] = _mm256_unpackhi_epi32(t[i + 4], t[i + 6]);
        }

        // (1st, 2nd) ==> (lo, hi)
        //   (0, 4)   ==>  (0, 1)
        //   (1, 5)   ==>  (4, 5)
        //   (2, 6)   ==>  (2, 3)
        //   (3, 7)   ==>  (6, 7)
        for (int i = 0; i < 2; i++)
        {
            @out[2 * i] = _mm256_unpacklo_epi64(u[2 * i], u[2 * i + 4]);
            @out[2 * i + 1] = _mm256_unpackhi_epi64(u[2 * i], u[2 * i + 4]);

            @out[2 * i + 4] = _mm256_unpacklo_epi64(u[2 * i + 1], u[2 * i + 5]);
            @out[2 * i + 5] = _mm256_unpackhi_epi64(u[2 * i + 1], u[2 * i + 5]);
        }
    }

    [MethodImpl(AI)]
    internal static void transpose_16bit_16x16_avx2(Span<__m256i> @in, Span<__m256i> @out)
    {
        Span<__m256i> t = stackalloc __m256i[16];

        // [port] LOADL(idx) / LOADR(idx) macros: `_mm_load_si128((__m128i const *)&in[idx])` is
        // the low 128 bits of in[idx], `... + 1` the high 128 bits.

        // load left 8x16
        t[0] = _mm256_castsi128_si256(_mm256_castsi256_si128(@in[0]));  // LOADL(0)
        t[0] = _mm256_inserti128_si256(t[0], _mm256_castsi256_si128(@in[8]), 1);
        t[1] = _mm256_castsi128_si256(_mm256_castsi256_si128(@in[1]));  // LOADL(1)
        t[1] = _mm256_inserti128_si256(t[1], _mm256_castsi256_si128(@in[9]), 1);
        t[2] = _mm256_castsi128_si256(_mm256_castsi256_si128(@in[2]));  // LOADL(2)
        t[2] = _mm256_inserti128_si256(t[2], _mm256_castsi256_si128(@in[10]), 1);
        t[3] = _mm256_castsi128_si256(_mm256_castsi256_si128(@in[3]));  // LOADL(3)
        t[3] = _mm256_inserti128_si256(t[3], _mm256_castsi256_si128(@in[11]), 1);
        t[4] = _mm256_castsi128_si256(_mm256_castsi256_si128(@in[4]));  // LOADL(4)
        t[4] = _mm256_inserti128_si256(t[4], _mm256_castsi256_si128(@in[12]), 1);
        t[5] = _mm256_castsi128_si256(_mm256_castsi256_si128(@in[5]));  // LOADL(5)
        t[5] = _mm256_inserti128_si256(t[5], _mm256_castsi256_si128(@in[13]), 1);
        t[6] = _mm256_castsi128_si256(_mm256_castsi256_si128(@in[6]));  // LOADL(6)
        t[6] = _mm256_inserti128_si256(t[6], _mm256_castsi256_si128(@in[14]), 1);
        t[7] = _mm256_castsi128_si256(_mm256_castsi256_si128(@in[7]));  // LOADL(7)
        t[7] = _mm256_inserti128_si256(t[7], _mm256_castsi256_si128(@in[15]), 1);

        // load right 8x16
        t[8] = _mm256_castsi128_si256(_mm256_extracti128_si256(@in[0], 1));  // LOADR(0)
        t[8] = _mm256_inserti128_si256(t[8], _mm256_extracti128_si256(@in[8], 1), 1);
        t[9] = _mm256_castsi128_si256(_mm256_extracti128_si256(@in[1], 1));  // LOADR(1)
        t[9] = _mm256_inserti128_si256(t[9], _mm256_extracti128_si256(@in[9], 1), 1);
        t[10] = _mm256_castsi128_si256(_mm256_extracti128_si256(@in[2], 1));  // LOADR(2)
        t[10] = _mm256_inserti128_si256(t[10], _mm256_extracti128_si256(@in[10], 1), 1);
        t[11] = _mm256_castsi128_si256(_mm256_extracti128_si256(@in[3], 1));  // LOADR(3)
        t[11] = _mm256_inserti128_si256(t[11], _mm256_extracti128_si256(@in[11], 1), 1);
        t[12] = _mm256_castsi128_si256(_mm256_extracti128_si256(@in[4], 1));  // LOADR(4)
        t[12] = _mm256_inserti128_si256(t[12], _mm256_extracti128_si256(@in[12], 1), 1);
        t[13] = _mm256_castsi128_si256(_mm256_extracti128_si256(@in[5], 1));  // LOADR(5)
        t[13] = _mm256_inserti128_si256(t[13], _mm256_extracti128_si256(@in[13], 1), 1);
        t[14] = _mm256_castsi128_si256(_mm256_extracti128_si256(@in[6], 1));  // LOADR(6)
        t[14] = _mm256_inserti128_si256(t[14], _mm256_extracti128_si256(@in[14], 1), 1);
        t[15] = _mm256_castsi128_si256(_mm256_extracti128_si256(@in[7], 1));  // LOADR(7)
        t[15] = _mm256_inserti128_si256(t[15], _mm256_extracti128_si256(@in[15], 1), 1);

        // get the top 16x8 result
        transpose2_8x8_avx2(t, @out);
        // get the bottom 16x8 result
        transpose2_8x8_avx2(t.Slice(8), @out.Slice(8));
    }

    [MethodImpl(AI)]
    internal static void transpose_16bit_16x8_avx2(Span<__m256i> @in, Span<__m256i> @out)
    {
        __m256i a0 = _mm256_unpacklo_epi16(@in[0], @in[1]);
        __m256i a1 = _mm256_unpacklo_epi16(@in[2], @in[3]);
        __m256i a2 = _mm256_unpacklo_epi16(@in[4], @in[5]);
        __m256i a3 = _mm256_unpacklo_epi16(@in[6], @in[7]);
        __m256i a4 = _mm256_unpackhi_epi16(@in[0], @in[1]);
        __m256i a5 = _mm256_unpackhi_epi16(@in[2], @in[3]);
        __m256i a6 = _mm256_unpackhi_epi16(@in[4], @in[5]);
        __m256i a7 = _mm256_unpackhi_epi16(@in[6], @in[7]);

        __m256i b0 = _mm256_unpacklo_epi32(a0, a1);
        __m256i b1 = _mm256_unpacklo_epi32(a2, a3);
        __m256i b2 = _mm256_unpacklo_epi32(a4, a5);
        __m256i b3 = _mm256_unpacklo_epi32(a6, a7);
        __m256i b4 = _mm256_unpackhi_epi32(a0, a1);
        __m256i b5 = _mm256_unpackhi_epi32(a2, a3);
        __m256i b6 = _mm256_unpackhi_epi32(a4, a5);
        __m256i b7 = _mm256_unpackhi_epi32(a6, a7);

        @out[0] = _mm256_unpacklo_epi64(b0, b1);
        @out[1] = _mm256_unpackhi_epi64(b0, b1);
        @out[2] = _mm256_unpacklo_epi64(b4, b5);
        @out[3] = _mm256_unpackhi_epi64(b4, b5);
        @out[4] = _mm256_unpacklo_epi64(b2, b3);
        @out[5] = _mm256_unpackhi_epi64(b2, b3);
        @out[6] = _mm256_unpacklo_epi64(b6, b7);
        @out[7] = _mm256_unpackhi_epi64(b6, b7);
    }

    [MethodImpl(AI)]
    internal static void flip_buf_avx2(Span<__m256i> @in, Span<__m256i> @out, int size)
    {
        for (int i = 0; i < size; ++i)
        {
            @out[size - i - 1] = @in[i];
        }
    }

    [MethodImpl(AI)]
    internal static void round_shift_16bit_w16_avx2(Span<__m256i> @in, int size, int bit)
    {
        if (bit < 0)
        {
            bit = -bit;
            __m256i round = _mm256_set1_epi16(1 << (bit - 1));
            for (int i = 0; i < size; ++i)
            {
                @in[i] = _mm256_adds_epi16(@in[i], round);
                @in[i] = _mm256_srai_epi16(@in[i], bit);
            }
        }
        else if (bit > 0)
        {
            for (int i = 0; i < size; ++i)
            {
                @in[i] = _mm256_slli_epi16(@in[i], bit);
            }
        }
    }

    [MethodImpl(AI)]
    internal static __m256i round_shift_32_avx2(__m256i vec, int bit)
    {
        __m256i tmp, round;
        round = _mm256_set1_epi32(1 << (bit - 1));
        tmp = _mm256_add_epi32(vec, round);
        return _mm256_srai_epi32(tmp, bit);
    }

    [MethodImpl(AI)]
    internal static void round_shift_array_32_avx2(Span<__m256i> input, Span<__m256i> output,
                                                   int size, int bit)
    {
        if (bit > 0)
        {
            int i;
            for (i = 0; i < size; i++)
            {
                output[i] = round_shift_32_avx2(input[i], bit);
            }
        }
        else
        {
            int i;
            for (i = 0; i < size; i++)
            {
                output[i] = _mm256_slli_epi32(input[i], -bit);
            }
        }
    }

    [MethodImpl(AI)]
    internal static void round_shift_rect_array_32_avx2(Span<__m256i> input,
                                                        Span<__m256i> output,
                                                        int size, int bit,
                                                        int val)
    {
        __m256i sqrt2 = _mm256_set1_epi32(val);
        if (bit > 0)
        {
            int i;
            for (i = 0; i < size; i++)
            {
                __m256i r0 = round_shift_32_avx2(input[i], bit);
                __m256i r1 = _mm256_mullo_epi32(sqrt2, r0);
                output[i] = round_shift_32_avx2(r1, NewSqrt2Bits);
            }
        }
        else
        {
            int i;
            for (i = 0; i < size; i++)
            {
                __m256i r0 = _mm256_slli_epi32(input[i], -bit);
                __m256i r1 = _mm256_mullo_epi32(sqrt2, r0);
                output[i] = round_shift_32_avx2(r1, NewSqrt2Bits);
            }
        }
    }

    [MethodImpl(AI)]
    internal static __m256i scale_round_avx2(__m256i a, int scale)
    {
        __m256i scale_rounding =
            pair_set_w16_epi16(scale, 1 << (NewSqrt2Bits - 1));
        __m256i b = _mm256_madd_epi16(a, scale_rounding);
        return _mm256_srai_epi32(b, NewSqrt2Bits);
    }

    [MethodImpl(AI)]
    internal static void store_rect_16bit_to_32bit_w8_avx2(__m256i a,
                                                           Ptr<int> b)
    {
        __m256i one = _mm256_set1_epi16(1);
        __m256i a_lo = _mm256_unpacklo_epi16(a, one);
        __m256i a_hi = _mm256_unpackhi_epi16(a, one);
        __m256i b_lo = scale_round_avx2(a_lo, NewSqrt2);
        __m256i b_hi = scale_round_avx2(a_hi, NewSqrt2);
        __m256i temp = _mm256_permute2f128_si256(b_lo, b_hi, 0x31);
        _mm_store_si128(b, _mm256_castsi256_si128(b_lo));
        _mm_store_si128(b + 4, _mm256_castsi256_si128(b_hi));
        _mm256_store_si256(b + 64, temp);
    }

    [MethodImpl(AI)]
    internal static void store_rect_buffer_16bit_to_32bit_w8_avx2(
        Span<__m256i> @in, Ptr<int> @out, int stride,
        int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            store_rect_16bit_to_32bit_w8_avx2(@in[i], @out + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void pack_reg(Span<__m128i> in1, Span<__m128i> in2,
                                  Span<__m256i> @out)
    {
        @out[0] = _mm256_insertf128_si256(_mm256_castsi128_si256(in1[0]), in2[0], 0x1);
        @out[1] = _mm256_insertf128_si256(_mm256_castsi128_si256(in1[1]), in2[1], 0x1);
        @out[2] = _mm256_insertf128_si256(_mm256_castsi128_si256(in1[2]), in2[2], 0x1);
        @out[3] = _mm256_insertf128_si256(_mm256_castsi128_si256(in1[3]), in2[3], 0x1);
        @out[4] = _mm256_insertf128_si256(_mm256_castsi128_si256(in1[4]), in2[4], 0x1);
        @out[5] = _mm256_insertf128_si256(_mm256_castsi128_si256(in1[5]), in2[5], 0x1);
        @out[6] = _mm256_insertf128_si256(_mm256_castsi128_si256(in1[6]), in2[6], 0x1);
        @out[7] = _mm256_insertf128_si256(_mm256_castsi128_si256(in1[7]), in2[7], 0x1);
    }

    [MethodImpl(AI)]
    internal static void extract_reg(Span<__m256i> @in, Span<__m128i> out1)
    {
        out1[0] = _mm256_castsi256_si128(@in[0]);
        out1[1] = _mm256_castsi256_si128(@in[1]);
        out1[2] = _mm256_castsi256_si128(@in[2]);
        out1[3] = _mm256_castsi256_si128(@in[3]);
        out1[4] = _mm256_castsi256_si128(@in[4]);
        out1[5] = _mm256_castsi256_si128(@in[5]);
        out1[6] = _mm256_castsi256_si128(@in[6]);
        out1[7] = _mm256_castsi256_si128(@in[7]);

        out1[8] = _mm256_extracti128_si256(@in[0], 0x01);
        out1[9] = _mm256_extracti128_si256(@in[1], 0x01);
        out1[10] = _mm256_extracti128_si256(@in[2], 0x01);
        out1[11] = _mm256_extracti128_si256(@in[3], 0x01);
        out1[12] = _mm256_extracti128_si256(@in[4], 0x01);
        out1[13] = _mm256_extracti128_si256(@in[5], 0x01);
        out1[14] = _mm256_extracti128_si256(@in[6], 0x01);
        out1[15] = _mm256_extracti128_si256(@in[7], 0x01);
    }
}
