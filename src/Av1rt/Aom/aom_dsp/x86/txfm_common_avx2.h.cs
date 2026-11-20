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

    // [port] The C stack arrays t[] and u[] are locals: the shuffles stay in registers instead
    // of storing and reloading each vector. All inputs are read before any output is written.
    [MethodImpl(AI)]
    internal static void transpose2_8x8_avx2(Span<__m256i> @in, Span<__m256i> @out)
    {
        _ = @in[7];
        transpose2_8x8_avx2(@in[0], @in[1], @in[2], @in[3], @in[4], @in[5], @in[6], @in[7], @out);
    }

    [MethodImpl(AI)]
    private static void transpose2_8x8_avx2(__m256i in0, __m256i in1, __m256i in2, __m256i in3,
                                            __m256i in4, __m256i in5, __m256i in6, __m256i in7,
                                            Span<__m256i> @out)
    {
        // (1st, 2nd) ==> (lo, hi)
        //   (0, 1)   ==>  (0, 1)
        //   (2, 3)   ==>  (2, 3)
        //   (4, 5)   ==>  (4, 5)
        //   (6, 7)   ==>  (6, 7)
        __m256i t0 = _mm256_unpacklo_epi16(in0, in1), t1 = _mm256_unpackhi_epi16(in0, in1);
        __m256i t2 = _mm256_unpacklo_epi16(in2, in3), t3 = _mm256_unpackhi_epi16(in2, in3);
        __m256i t4 = _mm256_unpacklo_epi16(in4, in5), t5 = _mm256_unpackhi_epi16(in4, in5);
        __m256i t6 = _mm256_unpacklo_epi16(in6, in7), t7 = _mm256_unpackhi_epi16(in6, in7);

        // (1st, 2nd) ==> (lo, hi)
        //   (0, 2)   ==>  (0, 2)
        //   (1, 3)   ==>  (1, 3)
        //   (4, 6)   ==>  (4, 6)
        //   (5, 7)   ==>  (5, 7)
        __m256i u0 = _mm256_unpacklo_epi32(t0, t2), u2 = _mm256_unpackhi_epi32(t0, t2);
        __m256i u4 = _mm256_unpacklo_epi32(t4, t6), u6 = _mm256_unpackhi_epi32(t4, t6);
        __m256i u1 = _mm256_unpacklo_epi32(t1, t3), u3 = _mm256_unpackhi_epi32(t1, t3);
        __m256i u5 = _mm256_unpacklo_epi32(t5, t7), u7 = _mm256_unpackhi_epi32(t5, t7);

        // (1st, 2nd) ==> (lo, hi)
        //   (0, 4)   ==>  (0, 1)
        //   (1, 5)   ==>  (4, 5)
        //   (2, 6)   ==>  (2, 3)
        //   (3, 7)   ==>  (6, 7)
        @out[7] = _mm256_unpackhi_epi64(u3, u7);
        @out[0] = _mm256_unpacklo_epi64(u0, u4);
        @out[1] = _mm256_unpackhi_epi64(u0, u4);
        @out[4] = _mm256_unpacklo_epi64(u1, u5);
        @out[5] = _mm256_unpackhi_epi64(u1, u5);
        @out[2] = _mm256_unpacklo_epi64(u2, u6);
        @out[3] = _mm256_unpackhi_epi64(u2, u6);
        @out[6] = _mm256_unpacklo_epi64(u3, u7);
    }

    // [port] t[] is locals (see transpose2_8x8_avx2). LOADL(i) / LOADR(i), the low / high 128
    // bits of in[i], pair up as vperm2i128 of in[i] and in[i + 8].
    [MethodImpl(AI)]
    internal static void transpose_16bit_16x16_avx2(Span<__m256i> @in, Span<__m256i> @out)
    {
        _ = @in[15];
        _ = @out[15];
        __m256i a0 = @in[0], a1 = @in[1], a2 = @in[2], a3 = @in[3];
        __m256i a4 = @in[4], a5 = @in[5], a6 = @in[6], a7 = @in[7];
        __m256i b0 = @in[8], b1 = @in[9], b2 = @in[10], b3 = @in[11];
        __m256i b4 = @in[12], b5 = @in[13], b6 = @in[14], b7 = @in[15];

        // get the top 16x8 result: left 8x16 (LOADL(i), LOADL(i + 8))
        transpose2_8x8_avx2(
            _mm256_permute2x128_si256(a0, b0, 0x20), _mm256_permute2x128_si256(a1, b1, 0x20),
            _mm256_permute2x128_si256(a2, b2, 0x20), _mm256_permute2x128_si256(a3, b3, 0x20),
            _mm256_permute2x128_si256(a4, b4, 0x20), _mm256_permute2x128_si256(a5, b5, 0x20),
            _mm256_permute2x128_si256(a6, b6, 0x20), _mm256_permute2x128_si256(a7, b7, 0x20),
            @out);
        // get the bottom 16x8 result: right 8x16 (LOADR(i), LOADR(i + 8))
        transpose2_8x8_avx2(
            _mm256_permute2x128_si256(a0, b0, 0x31), _mm256_permute2x128_si256(a1, b1, 0x31),
            _mm256_permute2x128_si256(a2, b2, 0x31), _mm256_permute2x128_si256(a3, b3, 0x31),
            _mm256_permute2x128_si256(a4, b4, 0x31), _mm256_permute2x128_si256(a5, b5, 0x31),
            _mm256_permute2x128_si256(a6, b6, 0x31), _mm256_permute2x128_si256(a7, b7, 0x31),
            @out.Slice(8));
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
