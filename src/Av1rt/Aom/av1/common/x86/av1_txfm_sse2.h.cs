// Port of libaom av1/common/x86/av1_txfm_sse2.h

global using static Av1rt.Aom.av1_txfm_sse2;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal delegate void transform_1d_sse2(Span<__m128i> input, Span<__m128i> output,
                                         sbyte cos_bit);

internal struct transform_2d_sse2
{
    public transform_1d_sse2 col, row;  // vertical and horizontal
}

// [port] `__m128i *` array parameters are Span<__m128i>; pointers to a single register are
// `in` / `ref __m128i`. The btf_16_*_sse2 macros use the caller's `__rounding` and `cos_bit`
// locals: as methods they take them as two trailing parameters.
internal static class av1_txfm_sse2
{
    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    [MethodImpl(AI)]
    internal static void btf_16_w4_sse2(
        in __m128i w0, in __m128i w1, __m128i __rounding,
        sbyte cos_bit, in __m128i in0, in __m128i in1,
        ref __m128i out0, ref __m128i out1)
    {
        __m128i t0 = _mm_unpacklo_epi16(in0, in1);
        __m128i u0 = _mm_madd_epi16(t0, w0);
        __m128i v0 = _mm_madd_epi16(t0, w1);
        __m128i a0 = _mm_add_epi32(u0, __rounding);
        __m128i b0 = _mm_add_epi32(v0, __rounding);
        __m128i c0 = _mm_srai_epi32(a0, cos_bit);
        __m128i d0 = _mm_srai_epi32(b0, cos_bit);

        out0 = _mm_packs_epi32(c0, c0);
        out1 = _mm_packs_epi32(d0, c0);
    }

    // [port] Macro -> method; in0/in1 are read before out0/out1 are written, as in the macro.
    [MethodImpl(AI)]
    internal static void btf_16_4p_sse2(__m128i w0, __m128i w1, __m128i in0, __m128i in1,
                                        ref __m128i out0, ref __m128i out1,
                                        __m128i __rounding, int cos_bit)
    {
        __m128i t0 = _mm_unpacklo_epi16(in0, in1);
        __m128i u0 = _mm_madd_epi16(t0, w0);
        __m128i v0 = _mm_madd_epi16(t0, w1);

        __m128i a0 = _mm_add_epi32(u0, __rounding);
        __m128i b0 = _mm_add_epi32(v0, __rounding);

        __m128i c0 = _mm_srai_epi32(a0, cos_bit);
        __m128i d0 = _mm_srai_epi32(b0, cos_bit);

        out0 = _mm_packs_epi32(c0, c0);
        out1 = _mm_packs_epi32(d0, d0);
    }

    // [port] Macro -> method; in0/in1 are read before out0/out1 are written, as in the macro.
    [MethodImpl(AI)]
    internal static void btf_16_sse2(__m128i w0, __m128i w1, __m128i in0, __m128i in1,
                                     ref __m128i out0, ref __m128i out1,
                                     __m128i __rounding, int cos_bit)
    {
        __m128i t0 = _mm_unpacklo_epi16(in0, in1);
        __m128i t1 = _mm_unpackhi_epi16(in0, in1);
        __m128i u0 = _mm_madd_epi16(t0, w0);
        __m128i u1 = _mm_madd_epi16(t1, w0);
        __m128i v0 = _mm_madd_epi16(t0, w1);
        __m128i v1 = _mm_madd_epi16(t1, w1);

        __m128i a0 = _mm_add_epi32(u0, __rounding);
        __m128i a1 = _mm_add_epi32(u1, __rounding);
        __m128i b0 = _mm_add_epi32(v0, __rounding);
        __m128i b1 = _mm_add_epi32(v1, __rounding);

        __m128i c0 = _mm_srai_epi32(a0, cos_bit);
        __m128i c1 = _mm_srai_epi32(a1, cos_bit);
        __m128i d0 = _mm_srai_epi32(b0, cos_bit);
        __m128i d1 = _mm_srai_epi32(b1, cos_bit);

        out0 = _mm_packs_epi32(c0, c1);
        out1 = _mm_packs_epi32(d0, d1);
    }

    [MethodImpl(AI)]
    internal static __m128i load_16bit_to_16bit(Ptr<short> a)
    {
        return _mm_load_si128(a);
    }

    [MethodImpl(AI)]
    internal static __m128i load_32bit_to_16bit(Ptr<int> a)
    {
        __m128i a_low = _mm_load_si128(a);
        return _mm_packs_epi32(a_low, _mm_load_si128(a + 4));
    }

    [MethodImpl(AI)]
    internal static __m128i load_32bit_to_16bit_w4(Ptr<int> a)
    {
        __m128i a_low = _mm_load_si128(a);
        return _mm_packs_epi32(a_low, a_low);
    }

    // Store 4 16 bit values. Sign extend the values.
    [MethodImpl(AI)]
    internal static void store_16bit_to_32bit_w4(__m128i a, Ptr<int> b)
    {
        __m128i a_lo = _mm_unpacklo_epi16(a, a);
        __m128i a_1 = _mm_srai_epi32(a_lo, 16);
        _mm_store_si128(b, a_1);
    }

    // Store 8 16 bit values. Sign extend the values.
    [MethodImpl(AI)]
    internal static void store_16bit_to_32bit(__m128i a, Ptr<int> b)
    {
        __m128i a_lo = _mm_unpacklo_epi16(a, a);
        __m128i a_hi = _mm_unpackhi_epi16(a, a);
        __m128i a_1 = _mm_srai_epi32(a_lo, 16);
        __m128i a_2 = _mm_srai_epi32(a_hi, 16);
        _mm_store_si128(b, a_1);
        _mm_store_si128(b + 4, a_2);
    }

    [MethodImpl(AI)]
    internal static __m128i scale_round_sse2(__m128i a, int scale)
    {
        __m128i scale_rounding = pair_set_epi16(scale, 1 << (NewSqrt2Bits - 1));
        __m128i b = _mm_madd_epi16(a, scale_rounding);
        return _mm_srai_epi32(b, NewSqrt2Bits);
    }

    [MethodImpl(AI)]
    internal static void store_rect_16bit_to_32bit_w4(__m128i a,
                                                      Ptr<int> b)
    {
        __m128i one = _mm_set1_epi16(1);
        __m128i a_lo = _mm_unpacklo_epi16(a, one);
        __m128i b_lo = scale_round_sse2(a_lo, NewSqrt2);
        _mm_store_si128(b, b_lo);
    }

    [MethodImpl(AI)]
    internal static void store_rect_16bit_to_32bit(__m128i a,
                                                   Ptr<int> b)
    {
        __m128i one = _mm_set1_epi16(1);
        __m128i a_lo = _mm_unpacklo_epi16(a, one);
        __m128i a_hi = _mm_unpackhi_epi16(a, one);
        __m128i b_lo = scale_round_sse2(a_lo, NewSqrt2);
        __m128i b_hi = scale_round_sse2(a_hi, NewSqrt2);
        _mm_store_si128(b, b_lo);
        _mm_store_si128(b + 4, b_hi);
    }

    [MethodImpl(AI)]
    internal static void load_buffer_16bit_to_16bit_w4(Ptr<short> @in,
                                                       int stride,
                                                       Span<__m128i> @out,
                                                       int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            @out[i] = _mm_loadl_epi64(@in + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void load_buffer_16bit_to_16bit_w4_flip(Ptr<short> @in,
                                                            int stride,
                                                            Span<__m128i> @out,
                                                            int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            @out[out_size - i - 1] = _mm_loadl_epi64(@in + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void load_buffer_16bit_to_16bit(Ptr<short> @in, int stride,
                                                    Span<__m128i> @out, int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            @out[i] = load_16bit_to_16bit(@in + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void load_buffer_16bit_to_16bit_flip(Ptr<short> @in,
                                                         int stride, Span<__m128i> @out,
                                                         int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            @out[out_size - i - 1] = load_16bit_to_16bit(@in + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void load_buffer_32bit_to_16bit(Ptr<int> @in, int stride,
                                                    Span<__m128i> @out, int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            @out[i] = load_32bit_to_16bit(@in + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void load_buffer_32bit_to_16bit_w4(Ptr<int> @in, int stride,
                                                       Span<__m128i> @out, int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            @out[i] = load_32bit_to_16bit_w4(@in + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void load_buffer_32bit_to_16bit_flip(Ptr<int> @in,
                                                         int stride, Span<__m128i> @out,
                                                         int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            @out[out_size - i - 1] = load_32bit_to_16bit(@in + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void store_buffer_16bit_to_32bit_w4(Span<__m128i> @in,
                                                        Ptr<int> @out,
                                                        int stride,
                                                        int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            store_16bit_to_32bit_w4(@in[i], @out + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void store_buffer_16bit_to_32bit_w8(Span<__m128i> @in,
                                                        Ptr<int> @out,
                                                        int stride,
                                                        int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            store_16bit_to_32bit(@in[i], @out + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void store_rect_buffer_16bit_to_32bit_w4(Span<__m128i> @in,
                                                             Ptr<int> @out,
                                                             int stride,
                                                             int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            store_rect_16bit_to_32bit_w4(@in[i], @out + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void store_rect_buffer_16bit_to_32bit_w8(Span<__m128i> @in,
                                                             Ptr<int> @out,
                                                             int stride,
                                                             int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            store_rect_16bit_to_32bit(@in[i], @out + i * stride);
        }
    }

    [MethodImpl(AI)]
    internal static void store_buffer_16bit_to_16bit_8x8(Span<__m128i> @in,
                                                         Ptr<ushort> @out,
                                                         int stride)
    {
        for (int i = 0; i < 8; ++i)
        {
            _mm_store_si128(@out + i * stride, @in[i]);
        }
    }

    [MethodImpl(AI)]
    internal static void round_shift_16bit(Span<__m128i> @in, int size, int bit)
    {
        if (bit < 0)
        {
            bit = -bit;
            __m128i rounding = _mm_set1_epi16(1 << (bit - 1));
            for (int i = 0; i < size; ++i)
            {
                @in[i] = _mm_adds_epi16(@in[i], rounding);
                @in[i] = _mm_srai_epi16(@in[i], bit);
            }
        }
        else if (bit > 0)
        {
            for (int i = 0; i < size; ++i)
            {
                @in[i] = _mm_slli_epi16(@in[i], bit);
            }
        }
    }

    [MethodImpl(AI)]
    internal static void flip_buf_sse2(Span<__m128i> @in, Span<__m128i> @out, int size)
    {
        for (int i = 0; i < size; ++i)
        {
            @out[size - i - 1] = @in[i];
        }
    }

    // [port] The av1_lowbd_fwd_txfm2d_*_sse2 prototypes belong to the forward transforms
    // (av1/encoder/x86/av1_fwd_txfm_sse2.c); av1_iadst8_sse2 / av1_idct8_sse2 are defined in
    // av1_inv_txfm_ssse3.c.cs.
}
