// Port of libaom av1/encoder/x86/av1_fwd_txfm_sse2.c


using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] Only the transforms the AVX2 dispatcher (av1_lowbd_fwd_txfm_avx2) takes from this file
// for the realtime subset: 4x4, 4x8 and 8x4 (and the 1-D kernels and tables they use).
// [subset] fdct8x16_new_sse2, av1_fdct8x32_new_sse2, av1_fdct8x64_new_sse2, fadst8x16_new_sse2,
// the 8x8+ tables, the other av1_lowbd_fwd_txfm2d_*_sse2 and av1_lowbd_fwd_txfm_sse2 are
// not reached: the AVX2 dispatcher has its own 8x8..32x32 versions, and 4x16, 16x4, 8x32,
// 32x8, 16x64, 64x16 and TX64 sizes are not used in the realtime-only subset.
internal static partial class av1_fwd_txfm_sse2
{
    private static void fdct4x4_new_sse2(Span<__m128i> input, Span<__m128i> output, sbyte cos_bit)
    {
        _ = cos_bit;
        sbyte cb = 13;
        Ptr<int> cospi = cospi_arr(cb);
        __m128i cospi_p32_p32 = pair_set_epi16(cospi[32], cospi[32]);
        __m128i cospi_p32_m32 = pair_set_epi16(cospi[32], -cospi[32]);
        __m128i cospi_p16_p48 = pair_set_epi16(cospi[16], cospi[48]);
        __m128i cospi_p48_m16 = pair_set_epi16(cospi[48], -cospi[16]);
        __m128i __rounding = _mm_set1_epi32(1 << (cb - 1));
        Span<__m128i> u = stackalloc __m128i[4];
        Span<__m128i> v = stackalloc __m128i[4];

        u[0] = _mm_unpacklo_epi16(input[0], input[1]);
        u[1] = _mm_unpacklo_epi16(input[3], input[2]);

        v[0] = _mm_add_epi16(u[0], u[1]);
        v[1] = _mm_sub_epi16(u[0], u[1]);

        u[0] = _mm_madd_epi16(v[0], cospi_p32_p32);  // 0
        u[1] = _mm_madd_epi16(v[0], cospi_p32_m32);  // 2
        u[2] = _mm_madd_epi16(v[1], cospi_p16_p48);  // 1
        u[3] = _mm_madd_epi16(v[1], cospi_p48_m16);  // 3

        v[0] = _mm_add_epi32(u[0], __rounding);
        v[1] = _mm_add_epi32(u[1], __rounding);
        v[2] = _mm_add_epi32(u[2], __rounding);
        v[3] = _mm_add_epi32(u[3], __rounding);
        u[0] = _mm_srai_epi32(v[0], cb);
        u[1] = _mm_srai_epi32(v[1], cb);
        u[2] = _mm_srai_epi32(v[2], cb);
        u[3] = _mm_srai_epi32(v[3], cb);

        output[0] = _mm_packs_epi32(u[0], u[1]);
        output[1] = _mm_packs_epi32(u[2], u[3]);
        output[2] = _mm_srli_si128(output[0], 8);
        output[3] = _mm_srli_si128(output[1], 8);
    }

    private static void fdct8x4_new_sse2(Span<__m128i> input, Span<__m128i> output, sbyte cos_bit)
    {
        Ptr<int> cospi = cospi_arr(cos_bit);
        __m128i __rounding = _mm_set1_epi32(1 << (cos_bit - 1));

        __m128i cospi_p32_p32 = pair_set_epi16(cospi[32], cospi[32]);
        __m128i cospi_p32_m32 = pair_set_epi16(cospi[32], -cospi[32]);
        __m128i cospi_p48_p16 = pair_set_epi16(cospi[48], cospi[16]);
        __m128i cospi_m16_p48 = pair_set_epi16(-cospi[16], cospi[48]);

        // stage 1
        Span<__m128i> x1 = stackalloc __m128i[4];
        x1[0] = _mm_adds_epi16(input[0], input[3]);
        x1[3] = _mm_subs_epi16(input[0], input[3]);
        x1[1] = _mm_adds_epi16(input[1], input[2]);
        x1[2] = _mm_subs_epi16(input[1], input[2]);

        // stage 2
        Span<__m128i> x2 = stackalloc __m128i[4];
        btf_16_sse2(cospi_p32_p32, cospi_p32_m32, x1[0], x1[1], ref x2[0], ref x2[1], __rounding, cos_bit);
        btf_16_sse2(cospi_p48_p16, cospi_m16_p48, x1[2], x1[3], ref x2[2], ref x2[3], __rounding, cos_bit);

        // stage 3
        output[0] = x2[0];
        output[1] = x2[2];
        output[2] = x2[1];
        output[3] = x2[3];
    }

    private static void fdct4x8_new_sse2(Span<__m128i> input, Span<__m128i> output, sbyte cos_bit)
    {
        _ = cos_bit;
        sbyte cb = 13;
        Ptr<int> cospi = cospi_arr(cb);
        __m128i __rounding = _mm_set1_epi32(1 << (cb - 1));

        __m128i cospi_m32_p32 = pair_set_epi16(-cospi[32], cospi[32]);
        __m128i cospi_p32_p32 = pair_set_epi16(cospi[32], cospi[32]);
        __m128i cospi_p32_m32 = pair_set_epi16(cospi[32], -cospi[32]);
        __m128i cospi_p48_p16 = pair_set_epi16(cospi[48], cospi[16]);
        __m128i cospi_m16_p48 = pair_set_epi16(-cospi[16], cospi[48]);
        __m128i cospi_p56_p08 = pair_set_epi16(cospi[56], cospi[8]);
        __m128i cospi_m08_p56 = pair_set_epi16(-cospi[8], cospi[56]);
        __m128i cospi_p24_p40 = pair_set_epi16(cospi[24], cospi[40]);
        __m128i cospi_m40_p24 = pair_set_epi16(-cospi[40], cospi[24]);

        // stage 1
        Span<__m128i> x1 = stackalloc __m128i[8];
        x1[0] = _mm_adds_epi16(input[0], input[7]);
        x1[7] = _mm_subs_epi16(input[0], input[7]);
        x1[1] = _mm_adds_epi16(input[1], input[6]);
        x1[6] = _mm_subs_epi16(input[1], input[6]);
        x1[2] = _mm_adds_epi16(input[2], input[5]);
        x1[5] = _mm_subs_epi16(input[2], input[5]);
        x1[3] = _mm_adds_epi16(input[3], input[4]);
        x1[4] = _mm_subs_epi16(input[3], input[4]);

        // stage 2
        Span<__m128i> x2 = stackalloc __m128i[8];
        x2[0] = _mm_adds_epi16(x1[0], x1[3]);
        x2[3] = _mm_subs_epi16(x1[0], x1[3]);
        x2[1] = _mm_adds_epi16(x1[1], x1[2]);
        x2[2] = _mm_subs_epi16(x1[1], x1[2]);
        x2[4] = x1[4];
        btf_16_w4_sse2(in cospi_m32_p32, in cospi_p32_p32, __rounding, cb, in x1[5], in x1[6],
                                      ref x2[5], ref x2[6]);
        x2[7] = x1[7];

        // stage 3
        Span<__m128i> x3 = stackalloc __m128i[8];
        btf_16_w4_sse2(in cospi_p32_p32, in cospi_p32_m32, __rounding, cb, in x2[0], in x2[1],
                                      ref x3[0], ref x3[1]);
        btf_16_w4_sse2(in cospi_p48_p16, in cospi_m16_p48, __rounding, cb, in x2[2], in x2[3],
                                      ref x3[2], ref x3[3]);
        x3[4] = _mm_adds_epi16(x2[4], x2[5]);
        x3[5] = _mm_subs_epi16(x2[4], x2[5]);
        x3[6] = _mm_subs_epi16(x2[7], x2[6]);
        x3[7] = _mm_adds_epi16(x2[7], x2[6]);

        // stage 4
        Span<__m128i> x4 = stackalloc __m128i[8];
        x4[0] = x3[0];
        x4[1] = x3[1];
        x4[2] = x3[2];
        x4[3] = x3[3];
        btf_16_w4_sse2(in cospi_p56_p08, in cospi_m08_p56, __rounding, cb, in x3[4], in x3[7],
                                      ref x4[4], ref x4[7]);
        btf_16_w4_sse2(in cospi_p24_p40, in cospi_m40_p24, __rounding, cb, in x3[5], in x3[6],
                                      ref x4[5], ref x4[6]);

        // stage 5
        output[0] = x4[0];
        output[1] = x4[4];
        output[2] = x4[2];
        output[3] = x4[6];
        output[4] = x4[1];
        output[5] = x4[5];
        output[6] = x4[3];
        output[7] = x4[7];
    }

    private static void fadst4x4_new_sse2(Span<__m128i> input, Span<__m128i> output, sbyte cos_bit_arg)
    {
        _ = cos_bit_arg;
        sbyte cos_bit = 13;
        Ptr<int> sinpi = sinpi_arr(cos_bit);
        __m128i sinpi_p01_p02 = pair_set_epi16(sinpi[1], sinpi[2]);
        __m128i sinpi_p04_m01 = pair_set_epi16(sinpi[4], -sinpi[1]);
        __m128i sinpi_p03_p04 = pair_set_epi16(sinpi[3], sinpi[4]);
        __m128i sinpi_m03_p02 = pair_set_epi16(-sinpi[3], sinpi[2]);
        __m128i sinpi_p03_p03 = _mm_set1_epi16((short)sinpi[3]);
        __m128i __zero = _mm_setzero_si128();
        __m128i __rounding = _mm_set1_epi32(1 << (cos_bit - 1));
        __m128i in7 = _mm_add_epi16(input[0], input[1]);
        Span<__m128i> u = stackalloc __m128i[8];
        Span<__m128i> v = stackalloc __m128i[8];

        u[0] = _mm_unpacklo_epi16(input[0], input[1]);
        u[1] = _mm_unpacklo_epi16(input[2], input[3]);
        u[2] = _mm_unpacklo_epi16(in7, __zero);
        u[3] = _mm_unpacklo_epi16(input[2], __zero);
        u[4] = _mm_unpacklo_epi16(input[3], __zero);

        v[0] = _mm_madd_epi16(u[0], sinpi_p01_p02);  // s0 + s2
        v[1] = _mm_madd_epi16(u[1], sinpi_p03_p04);  // s4 + s5
        v[2] = _mm_madd_epi16(u[2], sinpi_p03_p03);  // x1
        v[3] = _mm_madd_epi16(u[0], sinpi_p04_m01);  // s1 - s3
        v[4] = _mm_madd_epi16(u[1], sinpi_m03_p02);  // -s4 + s6
        v[5] = _mm_madd_epi16(u[3], sinpi_p03_p03);  // s4
        v[6] = _mm_madd_epi16(u[4], sinpi_p03_p03);

        u[0] = _mm_add_epi32(v[0], v[1]);
        u[1] = _mm_sub_epi32(v[2], v[6]);
        u[2] = _mm_add_epi32(v[3], v[4]);
        u[3] = _mm_sub_epi32(u[2], u[0]);
        u[4] = _mm_slli_epi32(v[5], 2);
        u[5] = _mm_sub_epi32(u[4], v[5]);
        u[6] = _mm_add_epi32(u[3], u[5]);

        v[0] = _mm_add_epi32(u[0], __rounding);
        v[1] = _mm_add_epi32(u[1], __rounding);
        v[2] = _mm_add_epi32(u[2], __rounding);
        v[3] = _mm_add_epi32(u[6], __rounding);

        u[0] = _mm_srai_epi32(v[0], cos_bit);
        u[1] = _mm_srai_epi32(v[1], cos_bit);
        u[2] = _mm_srai_epi32(v[2], cos_bit);
        u[3] = _mm_srai_epi32(v[3], cos_bit);

        output[0] = _mm_packs_epi32(u[0], u[2]);
        output[1] = _mm_packs_epi32(u[1], u[3]);
        output[2] = _mm_srli_si128(output[0], 8);
        output[3] = _mm_srli_si128(output[1], 8);
    }

    private static void fadst4x8_new_sse2(Span<__m128i> input, Span<__m128i> output, sbyte cos_bit_arg)
    {
        _ = cos_bit_arg;
        sbyte cos_bit = 13;
        Ptr<int> cospi = cospi_arr(cos_bit);
        __m128i __zero = _mm_setzero_si128();
        __m128i __rounding = _mm_set1_epi32(1 << (cos_bit - 1));

        __m128i cospi_p32_p32 = pair_set_epi16(cospi[32], cospi[32]);
        __m128i cospi_p32_m32 = pair_set_epi16(cospi[32], -cospi[32]);
        __m128i cospi_p16_p48 = pair_set_epi16(cospi[16], cospi[48]);
        __m128i cospi_p48_m16 = pair_set_epi16(cospi[48], -cospi[16]);
        __m128i cospi_m48_p16 = pair_set_epi16(-cospi[48], cospi[16]);
        __m128i cospi_p04_p60 = pair_set_epi16(cospi[4], cospi[60]);
        __m128i cospi_p60_m04 = pair_set_epi16(cospi[60], -cospi[4]);
        __m128i cospi_p20_p44 = pair_set_epi16(cospi[20], cospi[44]);
        __m128i cospi_p44_m20 = pair_set_epi16(cospi[44], -cospi[20]);
        __m128i cospi_p36_p28 = pair_set_epi16(cospi[36], cospi[28]);
        __m128i cospi_p28_m36 = pair_set_epi16(cospi[28], -cospi[36]);
        __m128i cospi_p52_p12 = pair_set_epi16(cospi[52], cospi[12]);
        __m128i cospi_p12_m52 = pair_set_epi16(cospi[12], -cospi[52]);

        // stage 1
        Span<__m128i> x1 = stackalloc __m128i[8];
        x1[0] = input[0];
        x1[1] = _mm_subs_epi16(__zero, input[7]);
        x1[2] = _mm_subs_epi16(__zero, input[3]);
        x1[3] = input[4];
        x1[4] = _mm_subs_epi16(__zero, input[1]);
        x1[5] = input[6];
        x1[6] = input[2];
        x1[7] = _mm_subs_epi16(__zero, input[5]);

        // stage 2
        Span<__m128i> x2 = stackalloc __m128i[8];
        x2[0] = x1[0];
        x2[1] = x1[1];
        btf_16_w4_sse2(in cospi_p32_p32, in cospi_p32_m32, __rounding, cos_bit, in x1[2],
                                      in x1[3], ref x2[2], ref x2[3]);
        x2[4] = x1[4];
        x2[5] = x1[5];
        btf_16_w4_sse2(in cospi_p32_p32, in cospi_p32_m32, __rounding, cos_bit, in x1[6],
                                      in x1[7], ref x2[6], ref x2[7]);

        // stage 3
        Span<__m128i> x3 = stackalloc __m128i[8];
        x3[0] = _mm_adds_epi16(x2[0], x2[2]);
        x3[2] = _mm_subs_epi16(x2[0], x2[2]);
        x3[1] = _mm_adds_epi16(x2[1], x2[3]);
        x3[3] = _mm_subs_epi16(x2[1], x2[3]);
        x3[4] = _mm_adds_epi16(x2[4], x2[6]);
        x3[6] = _mm_subs_epi16(x2[4], x2[6]);
        x3[5] = _mm_adds_epi16(x2[5], x2[7]);
        x3[7] = _mm_subs_epi16(x2[5], x2[7]);

        // stage 4
        Span<__m128i> x4 = stackalloc __m128i[8];
        x4[0] = x3[0];
        x4[1] = x3[1];
        x4[2] = x3[2];
        x4[3] = x3[3];
        btf_16_w4_sse2(in cospi_p16_p48, in cospi_p48_m16, __rounding, cos_bit, in x3[4],
                                      in x3[5], ref x4[4], ref x4[5]);
        btf_16_w4_sse2(in cospi_m48_p16, in cospi_p16_p48, __rounding, cos_bit, in x3[6],
                                      in x3[7], ref x4[6], ref x4[7]);

        // stage 5
        Span<__m128i> x5 = stackalloc __m128i[8];
        x5[0] = _mm_adds_epi16(x4[0], x4[4]);
        x5[4] = _mm_subs_epi16(x4[0], x4[4]);
        x5[1] = _mm_adds_epi16(x4[1], x4[5]);
        x5[5] = _mm_subs_epi16(x4[1], x4[5]);
        x5[2] = _mm_adds_epi16(x4[2], x4[6]);
        x5[6] = _mm_subs_epi16(x4[2], x4[6]);
        x5[3] = _mm_adds_epi16(x4[3], x4[7]);
        x5[7] = _mm_subs_epi16(x4[3], x4[7]);

        // stage 6
        Span<__m128i> x6 = stackalloc __m128i[8];
        btf_16_w4_sse2(in cospi_p04_p60, in cospi_p60_m04, __rounding, cos_bit, in x5[0],
                                      in x5[1], ref x6[0], ref x6[1]);
        btf_16_w4_sse2(in cospi_p20_p44, in cospi_p44_m20, __rounding, cos_bit, in x5[2],
                                      in x5[3], ref x6[2], ref x6[3]);
        btf_16_w4_sse2(in cospi_p36_p28, in cospi_p28_m36, __rounding, cos_bit, in x5[4],
                                      in x5[5], ref x6[4], ref x6[5]);
        btf_16_w4_sse2(in cospi_p52_p12, in cospi_p12_m52, __rounding, cos_bit, in x5[6],
                                      in x5[7], ref x6[6], ref x6[7]);

        // stage 7
        output[0] = x6[1];
        output[1] = x6[6];
        output[2] = x6[3];
        output[3] = x6[4];
        output[4] = x6[5];
        output[5] = x6[2];
        output[6] = x6[7];
        output[7] = x6[0];
    }

    private static void fadst8x4_new_sse2(Span<__m128i> input, Span<__m128i> output, sbyte cos_bit)
    {
        Ptr<int> sinpi = sinpi_arr(cos_bit);
        __m128i sinpi_p01_p02 = pair_set_epi16(sinpi[1], sinpi[2]);
        __m128i sinpi_p04_m01 = pair_set_epi16(sinpi[4], -sinpi[1]);
        __m128i sinpi_p03_p04 = pair_set_epi16(sinpi[3], sinpi[4]);
        __m128i sinpi_m03_p02 = pair_set_epi16(-sinpi[3], sinpi[2]);
        __m128i sinpi_p03_p03 = _mm_set1_epi16((short)sinpi[3]);
        __m128i __zero = _mm_setzero_si128();
        __m128i __rounding = _mm_set1_epi32(1 << (cos_bit - 1));
        __m128i in7 = _mm_add_epi16(input[0], input[1]);
        Span<__m128i> u_lo = stackalloc __m128i[8];
        Span<__m128i> u_hi = stackalloc __m128i[8];
        Span<__m128i> v_lo = stackalloc __m128i[8];
        Span<__m128i> v_hi = stackalloc __m128i[8];

        u_lo[0] = _mm_unpacklo_epi16(input[0], input[1]);
        u_hi[0] = _mm_unpackhi_epi16(input[0], input[1]);
        u_lo[1] = _mm_unpacklo_epi16(input[2], input[3]);
        u_hi[1] = _mm_unpackhi_epi16(input[2], input[3]);
        u_lo[2] = _mm_unpacklo_epi16(in7, __zero);
        u_hi[2] = _mm_unpackhi_epi16(in7, __zero);
        u_lo[3] = _mm_unpacklo_epi16(input[2], __zero);
        u_hi[3] = _mm_unpackhi_epi16(input[2], __zero);
        u_lo[4] = _mm_unpacklo_epi16(input[3], __zero);
        u_hi[4] = _mm_unpackhi_epi16(input[3], __zero);

        v_lo[0] = _mm_madd_epi16(u_lo[0], sinpi_p01_p02);  // s0 + s2
        v_hi[0] = _mm_madd_epi16(u_hi[0], sinpi_p01_p02);  // s0 + s2
        v_lo[1] = _mm_madd_epi16(u_lo[1], sinpi_p03_p04);  // s4 + s5
        v_hi[1] = _mm_madd_epi16(u_hi[1], sinpi_p03_p04);  // s4 + s5
        v_lo[2] = _mm_madd_epi16(u_lo[2], sinpi_p03_p03);  // x1
        v_hi[2] = _mm_madd_epi16(u_hi[2], sinpi_p03_p03);  // x1
        v_lo[3] = _mm_madd_epi16(u_lo[0], sinpi_p04_m01);  // s1 - s3
        v_hi[3] = _mm_madd_epi16(u_hi[0], sinpi_p04_m01);  // s1 - s3
        v_lo[4] = _mm_madd_epi16(u_lo[1], sinpi_m03_p02);  // -s4 + s6
        v_hi[4] = _mm_madd_epi16(u_hi[1], sinpi_m03_p02);  // -s4 + s6
        v_lo[5] = _mm_madd_epi16(u_lo[3], sinpi_p03_p03);  // s4
        v_hi[5] = _mm_madd_epi16(u_hi[3], sinpi_p03_p03);  // s4
        v_lo[6] = _mm_madd_epi16(u_lo[4], sinpi_p03_p03);
        v_hi[6] = _mm_madd_epi16(u_hi[4], sinpi_p03_p03);

        u_lo[0] = _mm_add_epi32(v_lo[0], v_lo[1]);
        u_hi[0] = _mm_add_epi32(v_hi[0], v_hi[1]);
        u_lo[1] = _mm_sub_epi32(v_lo[2], v_lo[6]);
        u_hi[1] = _mm_sub_epi32(v_hi[2], v_hi[6]);
        u_lo[2] = _mm_add_epi32(v_lo[3], v_lo[4]);
        u_hi[2] = _mm_add_epi32(v_hi[3], v_hi[4]);
        u_lo[3] = _mm_sub_epi32(u_lo[2], u_lo[0]);
        u_hi[3] = _mm_sub_epi32(u_hi[2], u_hi[0]);
        u_lo[4] = _mm_slli_epi32(v_lo[5], 2);
        u_hi[4] = _mm_slli_epi32(v_hi[5], 2);
        u_lo[5] = _mm_sub_epi32(u_lo[4], v_lo[5]);
        u_hi[5] = _mm_sub_epi32(u_hi[4], v_hi[5]);
        u_lo[6] = _mm_add_epi32(u_lo[3], u_lo[5]);
        u_hi[6] = _mm_add_epi32(u_hi[3], u_hi[5]);

        v_lo[0] = _mm_add_epi32(u_lo[0], __rounding);
        v_hi[0] = _mm_add_epi32(u_hi[0], __rounding);
        v_lo[1] = _mm_add_epi32(u_lo[1], __rounding);
        v_hi[1] = _mm_add_epi32(u_hi[1], __rounding);
        v_lo[2] = _mm_add_epi32(u_lo[2], __rounding);
        v_hi[2] = _mm_add_epi32(u_hi[2], __rounding);
        v_lo[3] = _mm_add_epi32(u_lo[6], __rounding);
        v_hi[3] = _mm_add_epi32(u_hi[6], __rounding);

        u_lo[0] = _mm_srai_epi32(v_lo[0], cos_bit);
        u_hi[0] = _mm_srai_epi32(v_hi[0], cos_bit);
        u_lo[1] = _mm_srai_epi32(v_lo[1], cos_bit);
        u_hi[1] = _mm_srai_epi32(v_hi[1], cos_bit);
        u_lo[2] = _mm_srai_epi32(v_lo[2], cos_bit);
        u_hi[2] = _mm_srai_epi32(v_hi[2], cos_bit);
        u_lo[3] = _mm_srai_epi32(v_lo[3], cos_bit);
        u_hi[3] = _mm_srai_epi32(v_hi[3], cos_bit);

        output[0] = _mm_packs_epi32(u_lo[0], u_hi[0]);
        output[1] = _mm_packs_epi32(u_lo[1], u_hi[1]);
        output[2] = _mm_packs_epi32(u_lo[2], u_hi[2]);
        output[3] = _mm_packs_epi32(u_lo[3], u_hi[3]);
    }

    private static readonly transform_1d_sse2[] col_txfm4x4_arr = {
        fdct4x4_new_sse2,       // DCT_DCT
        fadst4x4_new_sse2,      // ADST_DCT
        fdct4x4_new_sse2,       // DCT_ADST
        fadst4x4_new_sse2,      // ADST_ADST
        fadst4x4_new_sse2,      // FLIPADST_DCT
        fdct4x4_new_sse2,       // DCT_FLIPADST
        fadst4x4_new_sse2,      // FLIPADST_FLIPADST
        fadst4x4_new_sse2,      // ADST_FLIPADST
        fadst4x4_new_sse2,      // FLIPADST_ADST
        fidentity4x4_new_sse2,  // IDTX
        fdct4x4_new_sse2,       // V_DCT
        fidentity4x4_new_sse2,  // H_DCT
        fadst4x4_new_sse2,      // V_ADST
        fidentity4x4_new_sse2,  // H_ADST
        fadst4x4_new_sse2,      // V_FLIPADST
        fidentity4x4_new_sse2   // H_FLIPADST
    };

    private static readonly transform_1d_sse2[] row_txfm4x4_arr = {
        fdct4x4_new_sse2,       // DCT_DCT
        fdct4x4_new_sse2,       // ADST_DCT
        fadst4x4_new_sse2,      // DCT_ADST
        fadst4x4_new_sse2,      // ADST_ADST
        fdct4x4_new_sse2,       // FLIPADST_DCT
        fadst4x4_new_sse2,      // DCT_FLIPADST
        fadst4x4_new_sse2,      // FLIPADST_FLIPADST
        fadst4x4_new_sse2,      // ADST_FLIPADST
        fadst4x4_new_sse2,      // FLIPADST_ADST
        fidentity4x4_new_sse2,  // IDTX
        fidentity4x4_new_sse2,  // V_DCT
        fdct4x4_new_sse2,       // H_DCT
        fidentity4x4_new_sse2,  // V_ADST
        fadst4x4_new_sse2,      // H_ADST
        fidentity4x4_new_sse2,  // V_FLIPADST
        fadst4x4_new_sse2       // H_FLIPADST
    };

    private static readonly transform_1d_sse2[] col_txfm4x8_arr = {
        fdct4x8_new_sse2,       // DCT_DCT
        fadst4x8_new_sse2,      // ADST_DCT
        fdct4x8_new_sse2,       // DCT_ADST
        fadst4x8_new_sse2,      // ADST_ADST
        fadst4x8_new_sse2,      // FLIPADST_DCT
        fdct4x8_new_sse2,       // DCT_FLIPADST
        fadst4x8_new_sse2,      // FLIPADST_FLIPADST
        fadst4x8_new_sse2,      // ADST_FLIPADST
        fadst4x8_new_sse2,      // FLIPADST_ADST
        fidentity8x8_new_sse2,  // IDTX
        fdct4x8_new_sse2,       // V_DCT
        fidentity8x8_new_sse2,  // H_DCT
        fadst4x8_new_sse2,      // V_ADST
        fidentity8x8_new_sse2,  // H_ADST
        fadst4x8_new_sse2,      // V_FLIPADST
        fidentity8x8_new_sse2   // H_FLIPADST
    };

    private static readonly transform_1d_sse2[] row_txfm8x4_arr = {
        fdct8x4_new_sse2,       // DCT_DCT
        fdct8x4_new_sse2,       // ADST_DCT
        fadst8x4_new_sse2,      // DCT_ADST
        fadst8x4_new_sse2,      // ADST_ADST
        fdct8x4_new_sse2,       // FLIPADST_DCT
        fadst8x4_new_sse2,      // DCT_FLIPADST
        fadst8x4_new_sse2,      // FLIPADST_FLIPADST
        fadst8x4_new_sse2,      // ADST_FLIPADST
        fadst8x4_new_sse2,      // FLIPADST_ADST
        fidentity8x4_new_sse2,  // IDTX
        fidentity8x4_new_sse2,  // V_DCT
        fdct8x4_new_sse2,       // H_DCT
        fidentity8x4_new_sse2,  // V_ADST
        fadst8x4_new_sse2,      // H_ADST
        fidentity8x4_new_sse2,  // V_FLIPADST
        fadst8x4_new_sse2       // H_FLIPADST
    };

    private static readonly transform_1d_sse2[] col_txfm8x4_arr = {
        fdct8x4_new_sse2,       // DCT_DCT
        fadst8x4_new_sse2,      // ADST_DCT
        fdct8x4_new_sse2,       // DCT_ADST
        fadst8x4_new_sse2,      // ADST_ADST
        fadst8x4_new_sse2,      // FLIPADST_DCT
        fdct8x4_new_sse2,       // DCT_FLIPADST
        fadst8x4_new_sse2,      // FLIPADST_FLIPADST
        fadst8x4_new_sse2,      // ADST_FLIPADST
        fadst8x4_new_sse2,      // FLIPADST_ADST
        fidentity8x4_new_sse2,  // IDTX
        fdct8x4_new_sse2,       // V_DCT
        fidentity8x4_new_sse2,  // H_DCT
        fadst8x4_new_sse2,      // V_ADST
        fidentity8x4_new_sse2,  // H_ADST
        fadst8x4_new_sse2,      // V_FLIPADST
        fidentity8x4_new_sse2   // H_FLIPADST
    };

    private static readonly transform_1d_sse2[] row_txfm4x8_arr = {
        fdct4x8_new_sse2,       // DCT_DCT
        fdct4x8_new_sse2,       // ADST_DCT
        fadst4x8_new_sse2,      // DCT_ADST
        fadst4x8_new_sse2,      // ADST_ADST
        fdct4x8_new_sse2,       // FLIPADST_DCT
        fadst4x8_new_sse2,      // DCT_FLIPADST
        fadst4x8_new_sse2,      // FLIPADST_FLIPADST
        fadst4x8_new_sse2,      // ADST_FLIPADST
        fadst4x8_new_sse2,      // FLIPADST_ADST
        fidentity8x8_new_sse2,  // IDTX
        fidentity8x8_new_sse2,  // V_DCT
        fdct4x8_new_sse2,       // H_DCT
        fidentity8x8_new_sse2,  // V_ADST
        fadst4x8_new_sse2,      // H_ADST
        fidentity8x8_new_sse2,  // V_FLIPADST
        fadst4x8_new_sse2       // H_FLIPADST
    };

    internal static void av1_lowbd_fwd_txfm2d_4x4_sse2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        _ = bd;
        Span<__m128i> buf0 = stackalloc __m128i[4];
        Span<__m128i> buf1 = stackalloc __m128i[4];
        scoped Span<__m128i> buf;
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[TX_4X4];
        int width = 4;
        int height = 4;

        // Fast path for DCT_DCT: bypass function pointer table and flip checks.
        if (tx_type == DCT_DCT)
        {
            load_buffer_16bit_to_16bit_w4(input, stride, buf0, height);
            round_shift_16bit(buf0, height, shift[0]);
            fdct4x4_new_sse2(buf0, buf0, 13);
            transpose_16bit_4x4(buf0, buf1);
            fdct4x4_new_sse2(buf1, buf1, 13);
            store_buffer_16bit_to_32bit_w4(buf1, output, height, width);
            return;
        }

        int txw_idx = get_txw_idx(TX_4X4);
        int txh_idx = get_txh_idx(TX_4X4);
        int cos_bit_col = av1_fwd_cos_bit_col[txw_idx][txh_idx];
        int cos_bit_row = av1_fwd_cos_bit_row[txw_idx][txh_idx];
        transform_1d_sse2 col_txfm = col_txfm4x4_arr[tx_type];
        transform_1d_sse2 row_txfm = row_txfm4x4_arr[tx_type];
        int ud_flip = 0, lr_flip = 0;

        get_flip_cfg(tx_type, ref ud_flip, ref lr_flip);
        if (ud_flip != 0)
        {
            load_buffer_16bit_to_16bit_w4_flip(input, stride, buf0, height);
        }
        else
        {
            load_buffer_16bit_to_16bit_w4(input, stride, buf0, height);
        }
        round_shift_16bit(buf0, height, shift[0]);
        col_txfm(buf0, buf0, (sbyte)cos_bit_col);
        round_shift_16bit(buf0, height, shift[1]);
        transpose_16bit_4x4(buf0, buf1);

        if (lr_flip != 0)
        {
            buf = buf0;
            flip_buf_sse2(buf1, buf, width);
        }
        else
        {
            buf = buf1;
        }
        row_txfm(buf, buf, (sbyte)cos_bit_row);
        round_shift_16bit(buf, width, shift[2]);
        store_buffer_16bit_to_32bit_w4(buf, output, height, width);
    }

    internal static void av1_lowbd_fwd_txfm2d_4x8_sse2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        _ = stride;
        _ = bd;
        Span<__m128i> buf0 = stackalloc __m128i[8];
        Span<__m128i> buf1 = stackalloc __m128i[8];
        scoped Span<__m128i> buf;
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[TX_4X8];
        int txw_idx = get_txw_idx(TX_4X8);
        int txh_idx = get_txh_idx(TX_4X8);
        int cos_bit_col = av1_fwd_cos_bit_col[txw_idx][txh_idx];
        int cos_bit_row = av1_fwd_cos_bit_row[txw_idx][txh_idx];
        int width = 4;
        int height = 8;
        transform_1d_sse2 col_txfm = col_txfm4x8_arr[tx_type];
        transform_1d_sse2 row_txfm = row_txfm8x4_arr[tx_type];
        int ud_flip = 0, lr_flip = 0;

        get_flip_cfg(tx_type, ref ud_flip, ref lr_flip);
        if (ud_flip != 0)
        {
            load_buffer_16bit_to_16bit_w4_flip(input, stride, buf0, height);
        }
        else
        {
            load_buffer_16bit_to_16bit_w4(input, stride, buf0, height);
        }
        round_shift_16bit(buf0, height, shift[0]);
        col_txfm(buf0, buf0, (sbyte)cos_bit_col);
        round_shift_16bit(buf0, height, shift[1]);
        transpose_16bit_4x8(buf0, buf1);

        if (lr_flip != 0)
        {
            buf = buf0;
            flip_buf_sse2(buf1, buf, width);
        }
        else
        {
            buf = buf1;
        }
        row_txfm(buf, buf, (sbyte)cos_bit_row);
        round_shift_16bit(buf, width, shift[2]);
        store_rect_buffer_16bit_to_32bit_w8(buf, output, height, width);
    }

    internal static void av1_lowbd_fwd_txfm2d_8x4_sse2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        _ = bd;
        Span<__m128i> buf0 = stackalloc __m128i[8];
        Span<__m128i> buf1 = stackalloc __m128i[8];
        scoped Span<__m128i> buf;
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[TX_8X4];
        int txw_idx = get_txw_idx(TX_8X4);
        int txh_idx = get_txh_idx(TX_8X4);
        int cos_bit_col = av1_fwd_cos_bit_col[txw_idx][txh_idx];
        int cos_bit_row = av1_fwd_cos_bit_row[txw_idx][txh_idx];
        int width = 8;
        int height = 4;
        transform_1d_sse2 col_txfm = col_txfm8x4_arr[tx_type];
        transform_1d_sse2 row_txfm = row_txfm4x8_arr[tx_type];
        int ud_flip = 0, lr_flip = 0;

        get_flip_cfg(tx_type, ref ud_flip, ref lr_flip);
        if (ud_flip != 0)
            load_buffer_16bit_to_16bit_flip(input, stride, buf0, height);
        else
            load_buffer_16bit_to_16bit(input, stride, buf0, height);
        round_shift_16bit(buf0, height, shift[0]);
        col_txfm(buf0, buf0, (sbyte)cos_bit_col);
        round_shift_16bit(buf0, height, shift[1]);
        transpose_16bit_8x8(buf0, buf1);

        if (lr_flip != 0)
        {
            buf = buf0;
            flip_buf_sse2(buf1, buf, width);
        }
        else
        {
            buf = buf1;
        }
        row_txfm(buf, buf, (sbyte)cos_bit_row);
        round_shift_16bit(buf, width, shift[2]);
        store_rect_buffer_16bit_to_32bit_w4(buf, output, height, width);
    }
}
