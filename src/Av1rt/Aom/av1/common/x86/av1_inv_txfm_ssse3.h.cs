// Port of libaom av1/common/x86/av1_inv_txfm_ssse3.h

global using ITX_TYPE_1D = int;

global using static Av1rt.Aom.av1_inv_txfm_ssse3;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal delegate void transform_1d_ssse3(Span<__m128i> input, Span<__m128i> output);

internal static partial class av1_inv_txfm_ssse3
{
    // [port] Macros -> methods. Each reads its inputs before writing its outputs, as the
    // macros' `_in` / `_in0` / `_in1` temporaries do, so in-place calls behave the same.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void btf_16_ssse3(int w0, int w1, __m128i @in, ref __m128i out0, ref __m128i out1)
    {
        __m128i _w0 = _mm_set1_epi16(w0 * 8);
        __m128i _w1 = _mm_set1_epi16(w1 * 8);
        __m128i _in = @in;
        out0 = _mm_mulhrs_epi16(_in, _w0);
        out1 = _mm_mulhrs_epi16(_in, _w1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void btf_16_adds_subs_sse2(ref __m128i in0, ref __m128i in1)
    {
        __m128i _in0 = in0;
        __m128i _in1 = in1;
        in0 = _mm_adds_epi16(_in0, _in1);
        in1 = _mm_subs_epi16(_in0, _in1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void btf_16_subs_adds_sse2(ref __m128i in0, ref __m128i in1)
    {
        __m128i _in0 = in0;
        __m128i _in1 = in1;
        in1 = _mm_subs_epi16(_in0, _in1);
        in0 = _mm_adds_epi16(_in0, _in1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void btf_16_adds_subs_out_sse2(ref __m128i out0, ref __m128i out1, __m128i in0, __m128i in1)
    {
        __m128i _in0 = in0;
        __m128i _in1 = in1;
        out0 = _mm_adds_epi16(_in0, _in1);
        out1 = _mm_subs_epi16(_in0, _in1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void round_shift_16bit_ssse3(Span<__m128i> @in, int size, int bit)
    {
        if (bit < 0)
        {
            __m128i scale = _mm_set1_epi16(1 << (15 + bit));
            for (int i = 0; i < size; ++i)
            {
                @in[i] = _mm_mulhrs_epi16(@in[i], scale);
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

    // 1D itx types
    internal const int IDCT_1D = 0;
    internal const int IADST_1D = 1;
    internal const int IFLIPADST_1D = IADST_1D;
    internal const int IIDENTITY_1D = 2;
    internal const int ITX_TYPES_1D = 3;

    internal static readonly ITX_TYPE_1D[] vitx_1d_tab = {
        IDCT_1D,      IADST_1D,     IDCT_1D,      IADST_1D,
        IFLIPADST_1D, IDCT_1D,      IFLIPADST_1D, IADST_1D,
        IFLIPADST_1D, IIDENTITY_1D, IDCT_1D,      IIDENTITY_1D,
        IADST_1D,     IIDENTITY_1D, IFLIPADST_1D, IIDENTITY_1D,
    };

    internal static readonly ITX_TYPE_1D[] hitx_1d_tab = {
        IDCT_1D,      IDCT_1D,      IADST_1D,     IADST_1D,
        IDCT_1D,      IFLIPADST_1D, IFLIPADST_1D, IFLIPADST_1D,
        IADST_1D,     IIDENTITY_1D, IIDENTITY_1D, IDCT_1D,
        IIDENTITY_1D, IADST_1D,     IIDENTITY_1D, IFLIPADST_1D,
    };

    internal static readonly short[] av1_eob_to_eobxy_8x8_default = {
        0x0707, 0x0707, 0x0707, 0x0707, 0x0707, 0x0707, 0x0707, 0x0707,
    };

    internal static readonly short[] av1_eob_to_eobxy_16x16_default = {
        0x0707, 0x0707, 0x0f0f, 0x0f0f, 0x0f0f, 0x0f0f, 0x0f0f, 0x0f0f,
        0x0f0f, 0x0f0f, 0x0f0f, 0x0f0f, 0x0f0f, 0x0f0f, 0x0f0f, 0x0f0f,
    };

    internal static readonly short[] av1_eob_to_eobxy_32x32_default = {
        0x0707, 0x0f0f, 0x0f0f, 0x0f0f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f,
        0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f,
        0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f,
        0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f, 0x1f1f,
    };

    internal static readonly short[] av1_eob_to_eobxy_8x16_default = {
        0x0707, 0x0707, 0x0707, 0x0707, 0x0707, 0x0f07, 0x0f07, 0x0f07,
        0x0f07, 0x0f07, 0x0f07, 0x0f07, 0x0f07, 0x0f07, 0x0f07, 0x0f07,
    };

    internal static readonly short[] av1_eob_to_eobxy_16x8_default = {
        0x0707, 0x0707, 0x070f, 0x070f, 0x070f, 0x070f, 0x070f, 0x070f,
    };

    internal static readonly short[] av1_eob_to_eobxy_16x32_default = {
        0x0707, 0x0707, 0x0f0f, 0x0f0f, 0x0f0f, 0x0f0f, 0x0f0f, 0x0f0f,
        0x0f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f,
        0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f,
        0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f, 0x1f0f,
    };

    internal static readonly short[] av1_eob_to_eobxy_32x16_default = {
        0x0707, 0x0f0f, 0x0f0f, 0x0f0f, 0x0f1f, 0x0f1f, 0x0f1f, 0x0f1f,
        0x0f1f, 0x0f1f, 0x0f1f, 0x0f1f, 0x0f1f, 0x0f1f, 0x0f1f, 0x0f1f,
    };

    internal static readonly short[] av1_eob_to_eobxy_8x32_default = {
        0x0707, 0x0707, 0x0707, 0x0707, 0x0707, 0x0f07, 0x0f07, 0x0f07,
        0x0f07, 0x0f07, 0x0f07, 0x0f07, 0x0f07, 0x1f07, 0x1f07, 0x1f07,
        0x1f07, 0x1f07, 0x1f07, 0x1f07, 0x1f07, 0x1f07, 0x1f07, 0x1f07,
        0x1f07, 0x1f07, 0x1f07, 0x1f07, 0x1f07, 0x1f07, 0x1f07, 0x1f07,
    };

    internal static readonly short[] av1_eob_to_eobxy_32x8_default = {
        0x0707, 0x070f, 0x070f, 0x071f, 0x071f, 0x071f, 0x071f, 0x071f,
    };

    internal static readonly short[][] av1_eob_to_eobxy_default = {
        null,
        av1_eob_to_eobxy_8x8_default,
        av1_eob_to_eobxy_16x16_default,
        av1_eob_to_eobxy_32x32_default,
        av1_eob_to_eobxy_32x32_default,
        null,
        null,
        av1_eob_to_eobxy_8x16_default,
        av1_eob_to_eobxy_16x8_default,
        av1_eob_to_eobxy_16x32_default,
        av1_eob_to_eobxy_32x16_default,
        av1_eob_to_eobxy_32x32_default,
        av1_eob_to_eobxy_32x32_default,
        null,
        null,
        av1_eob_to_eobxy_8x32_default,
        av1_eob_to_eobxy_32x8_default,
        av1_eob_to_eobxy_16x32_default,
        av1_eob_to_eobxy_32x16_default,
    };

    internal static readonly int[] lowbd_txfm_all_1d_zeros_idx = {
        0, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
    };

    // Transform block width in log2 for eob (size of 64 map to 32)
    internal static readonly int[] tx_size_wide_log2_eob = {
        2, 3, 4, 5, 5, 2, 3, 3, 4, 4, 5, 5, 5, 2, 4, 3, 5, 4, 5,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void get_eobx_eoby_scan_default(ref int eobx, ref int eoby,
                                                    TX_SIZE tx_size, int eob)
    {
        if (eob == 1)
        {
            eobx = 0;
            eoby = 0;
            return;
        }

        int tx_w_log2 = tx_size_wide_log2_eob[tx_size];
        int eob_row = (eob - 1) >> tx_w_log2;
        int eobxy = av1_eob_to_eobxy_default[tx_size][eob_row];
        eobx = eobxy & 0xFF;
        eoby = eobxy >> 8;
    }

    internal static readonly int[] eob_fill = {
        0,  7,  7,  7,  7,  7,  7,  7,  15, 15, 15, 15, 15, 15, 15, 15,
        31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 31,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void get_eobx_eoby_scan_h_identity(ref int eobx, ref int eoby,
                                                       TX_SIZE tx_size, int eob)
    {
        eob -= 1;
        int txfm_size_col = tx_size_wide[tx_size];
        int eobx_max = AOMMIN(32, txfm_size_col) - 1;
        eobx = (eob >= eobx_max) ? eobx_max : eob_fill[eob];
        int temp_eoby = eob / (eobx_max + 1);
        Debug.Assert(temp_eoby < 32);
        eoby = eob_fill[temp_eoby];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void get_eobx_eoby_scan_v_identity(ref int eobx, ref int eoby,
                                                       TX_SIZE tx_size, int eob)
    {
        eob -= 1;
        int txfm_size_row = tx_size_high[tx_size];
        int eoby_max = AOMMIN(32, txfm_size_row) - 1;
        eobx = eob_fill[eob / (eoby_max + 1)];
        eoby = (eob >= eoby_max) ? eoby_max : eob_fill[eob];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void idct8_low1_ssse3_inline(Span<__m128i> input,
                                                 Span<__m128i> output)
    {
        Ptr<int> cospi = cospi_arr(INV_COS_BIT);

        Span<__m128i> x = stackalloc __m128i[2];
        x[0] = input[0];

        btf_16_ssse3(cospi[32], cospi[32], x[0], ref x[0], ref x[1]);

        output[0] = x[0];
        output[7] = x[0];
        output[1] = x[1];
        output[6] = x[1];
        output[2] = x[1];
        output[5] = x[1];
        output[3] = x[0];
        output[4] = x[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void idct8_sse2_inline(Span<__m128i> input, Span<__m128i> output)
    {
        sbyte cos_bit = INV_COS_BIT;
        Ptr<int> cospi = cospi_arr(INV_COS_BIT);
        __m128i __rounding = _mm_set1_epi32(1 << (INV_COS_BIT - 1));

        __m128i cospi_p56_m08 = pair_set_epi16(cospi[56], -cospi[8]);
        __m128i cospi_p08_p56 = pair_set_epi16(cospi[8], cospi[56]);
        __m128i cospi_p24_m40 = pair_set_epi16(cospi[24], -cospi[40]);
        __m128i cospi_p40_p24 = pair_set_epi16(cospi[40], cospi[24]);
        __m128i cospi_p32_p32 = pair_set_epi16(cospi[32], cospi[32]);
        __m128i cospi_p32_m32 = pair_set_epi16(cospi[32], -cospi[32]);
        __m128i cospi_p48_m16 = pair_set_epi16(cospi[48], -cospi[16]);
        __m128i cospi_p16_p48 = pair_set_epi16(cospi[16], cospi[48]);
        __m128i cospi_m32_p32 = pair_set_epi16(-cospi[32], cospi[32]);

        // stage 1
        Span<__m128i> x = stackalloc __m128i[8];
        x[0] = input[0];
        x[1] = input[4];
        x[2] = input[2];
        x[3] = input[6];
        x[4] = input[1];
        x[5] = input[5];
        x[6] = input[3];
        x[7] = input[7];

        // stage 2
        btf_16_sse2(cospi_p56_m08, cospi_p08_p56, x[4], x[7], ref x[4], ref x[7], __rounding, cos_bit);
        btf_16_sse2(cospi_p24_m40, cospi_p40_p24, x[5], x[6], ref x[5], ref x[6], __rounding, cos_bit);

        // stage 3
        btf_16_sse2(cospi_p32_p32, cospi_p32_m32, x[0], x[1], ref x[0], ref x[1], __rounding, cos_bit);
        btf_16_sse2(cospi_p48_m16, cospi_p16_p48, x[2], x[3], ref x[2], ref x[3], __rounding, cos_bit);
        btf_16_adds_subs_sse2(ref x[4], ref x[5]);
        btf_16_subs_adds_sse2(ref x[7], ref x[6]);

        // stage 4
        btf_16_adds_subs_sse2(ref x[0], ref x[3]);
        btf_16_adds_subs_sse2(ref x[1], ref x[2]);
        btf_16_sse2(cospi_m32_p32, cospi_p32_p32, x[5], x[6], ref x[5], ref x[6], __rounding, cos_bit);

        // stage 5
        btf_16_adds_subs_out_sse2(ref output[0], ref output[7], x[0], x[7]);
        btf_16_adds_subs_out_sse2(ref output[1], ref output[6], x[1], x[6]);
        btf_16_adds_subs_out_sse2(ref output[2], ref output[5], x[2], x[5]);
        btf_16_adds_subs_out_sse2(ref output[3], ref output[4], x[3], x[4]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void iadst8_low1_ssse3_inline(Span<__m128i> input,
                                                  Span<__m128i> output)
    {
        sbyte cos_bit = INV_COS_BIT;
        Ptr<int> cospi = cospi_arr(INV_COS_BIT);
        __m128i __zero = _mm_setzero_si128();
        __m128i __rounding = _mm_set1_epi32(1 << (INV_COS_BIT - 1));

        __m128i cospi_p16_p48 = pair_set_epi16(cospi[16], cospi[48]);
        __m128i cospi_p48_m16 = pair_set_epi16(cospi[48], -cospi[16]);
        __m128i cospi_p32_p32 = pair_set_epi16(cospi[32], cospi[32]);
        __m128i cospi_p32_m32 = pair_set_epi16(cospi[32], -cospi[32]);

        Span<__m128i> x = stackalloc __m128i[8];
        x[1] = input[0];

        btf_16_ssse3(cospi[60], -cospi[4], x[1], ref x[0], ref x[1]);

        x[4] = x[0];
        x[5] = x[1];

        btf_16_sse2(cospi_p16_p48, cospi_p48_m16, x[4], x[5], ref x[4], ref x[5], __rounding, cos_bit);

        x[2] = x[0];
        x[3] = x[1];
        x[6] = x[4];
        x[7] = x[5];

        btf_16_sse2(cospi_p32_p32, cospi_p32_m32, x[2], x[3], ref x[2], ref x[3], __rounding, cos_bit);
        btf_16_sse2(cospi_p32_p32, cospi_p32_m32, x[6], x[7], ref x[6], ref x[7], __rounding, cos_bit);

        output[0] = x[0];
        output[1] = _mm_subs_epi16(__zero, x[4]);
        output[2] = x[6];
        output[3] = _mm_subs_epi16(__zero, x[2]);
        output[4] = x[3];
        output[5] = _mm_subs_epi16(__zero, x[7]);
        output[6] = x[5];
        output[7] = _mm_subs_epi16(__zero, x[1]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void iadst8_sse2_inline(Span<__m128i> input, Span<__m128i> output)
    {
        sbyte cos_bit = INV_COS_BIT;
        Ptr<int> cospi = cospi_arr(INV_COS_BIT);
        __m128i __zero = _mm_setzero_si128();
        __m128i __rounding = _mm_set1_epi32(1 << (INV_COS_BIT - 1));

        __m128i cospi_p04_p60 = pair_set_epi16(cospi[4], cospi[60]);
        __m128i cospi_p60_m04 = pair_set_epi16(cospi[60], -cospi[4]);
        __m128i cospi_p20_p44 = pair_set_epi16(cospi[20], cospi[44]);
        __m128i cospi_p44_m20 = pair_set_epi16(cospi[44], -cospi[20]);
        __m128i cospi_p36_p28 = pair_set_epi16(cospi[36], cospi[28]);
        __m128i cospi_p28_m36 = pair_set_epi16(cospi[28], -cospi[36]);
        __m128i cospi_p52_p12 = pair_set_epi16(cospi[52], cospi[12]);
        __m128i cospi_p12_m52 = pair_set_epi16(cospi[12], -cospi[52]);
        __m128i cospi_p16_p48 = pair_set_epi16(cospi[16], cospi[48]);
        __m128i cospi_p48_m16 = pair_set_epi16(cospi[48], -cospi[16]);
        __m128i cospi_m48_p16 = pair_set_epi16(-cospi[48], cospi[16]);
        __m128i cospi_p32_p32 = pair_set_epi16(cospi[32], cospi[32]);
        __m128i cospi_p32_m32 = pair_set_epi16(cospi[32], -cospi[32]);

        // stage 1
        Span<__m128i> x = stackalloc __m128i[8];
        x[0] = input[7];
        x[1] = input[0];
        x[2] = input[5];
        x[3] = input[2];
        x[4] = input[3];
        x[5] = input[4];
        x[6] = input[1];
        x[7] = input[6];

        // stage 2
        btf_16_sse2(cospi_p04_p60, cospi_p60_m04, x[0], x[1], ref x[0], ref x[1], __rounding, cos_bit);
        btf_16_sse2(cospi_p20_p44, cospi_p44_m20, x[2], x[3], ref x[2], ref x[3], __rounding, cos_bit);
        btf_16_sse2(cospi_p36_p28, cospi_p28_m36, x[4], x[5], ref x[4], ref x[5], __rounding, cos_bit);
        btf_16_sse2(cospi_p52_p12, cospi_p12_m52, x[6], x[7], ref x[6], ref x[7], __rounding, cos_bit);

        // stage 3
        btf_16_adds_subs_sse2(ref x[0], ref x[4]);
        btf_16_adds_subs_sse2(ref x[1], ref x[5]);
        btf_16_adds_subs_sse2(ref x[2], ref x[6]);
        btf_16_adds_subs_sse2(ref x[3], ref x[7]);

        // stage 4
        btf_16_sse2(cospi_p16_p48, cospi_p48_m16, x[4], x[5], ref x[4], ref x[5], __rounding, cos_bit);
        btf_16_sse2(cospi_m48_p16, cospi_p16_p48, x[6], x[7], ref x[6], ref x[7], __rounding, cos_bit);

        // stage 5
        btf_16_adds_subs_sse2(ref x[0], ref x[2]);
        btf_16_adds_subs_sse2(ref x[1], ref x[3]);
        btf_16_adds_subs_sse2(ref x[4], ref x[6]);
        btf_16_adds_subs_sse2(ref x[5], ref x[7]);

        // stage 6
        btf_16_sse2(cospi_p32_p32, cospi_p32_m32, x[2], x[3], ref x[2], ref x[3], __rounding, cos_bit);
        btf_16_sse2(cospi_p32_p32, cospi_p32_m32, x[6], x[7], ref x[6], ref x[7], __rounding, cos_bit);

        // stage 7
        output[0] = x[0];
        output[1] = _mm_subs_epi16(__zero, x[4]);
        output[2] = x[6];
        output[3] = _mm_subs_epi16(__zero, x[2]);
        output[4] = x[3];
        output[5] = _mm_subs_epi16(__zero, x[7]);
        output[6] = x[5];
        output[7] = _mm_subs_epi16(__zero, x[1]);
    }

    // av1_lowbd_inv_txfm2d_add_idtx_ssse3, av1_lowbd_inv_txfm2d_add_h_identity_ssse3,
    // av1_lowbd_inv_txfm2d_add_v_identity_ssse3, av1_iadst8_low1_ssse3 and av1_idct8_low1_ssse3
    // are defined in av1_inv_txfm_ssse3.c.cs.
}
