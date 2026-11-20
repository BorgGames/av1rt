// Port of libaom av1/encoder/x86/av1_fwd_txfm_avx2.h

global using static Av1rt.Aom.av1_fwd_txfm_avx2;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class av1_fwd_txfm_avx2
{

    // out0 = in0*w0 + in1*w1
    // out1 = -in1*w0 + in0*w1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void btf_32_avx2_type0(int w0, int w1, ref __m256i in0, ref __m256i in1, __m256i _r, int cos_bit)
    {
        __m256i _in0 = in0;
        __m256i _in1 = in1;
        __m256i ww0 = _mm256_set1_epi32(w0);
        __m256i ww1 = _mm256_set1_epi32(w1);
        __m256i in0_w0 = _mm256_mullo_epi32(_in0, ww0);
        __m256i in1_w1 = _mm256_mullo_epi32(_in1, ww1);
        __m256i temp0 = _mm256_add_epi32(in0_w0, in1_w1);
        temp0 = _mm256_add_epi32(temp0, _r);
        in0 = _mm256_srai_epi32(temp0, cos_bit);
        __m256i in0_w1 = _mm256_mullo_epi32(_in0, ww1);
        __m256i in1_w0 = _mm256_mullo_epi32(_in1, ww0);
        __m256i temp1 = _mm256_sub_epi32(in0_w1, in1_w0);
        temp1 = _mm256_add_epi32(temp1, _r);
        in1 = _mm256_srai_epi32(temp1, cos_bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void btf_32_avx2_type1(int w0, int w1, ref __m256i in0, ref __m256i in1, __m256i _r, int cos_bit)
    {
        __m256i _in0 = in0;
        __m256i _in1 = in1;
        __m256i ww0 = _mm256_set1_epi32(w0);
        __m256i ww1 = _mm256_set1_epi32(w1);
        __m256i in0_w0 = _mm256_mullo_epi32(_in0, ww0);
        __m256i in1_w1 = _mm256_mullo_epi32(_in1, ww1);
        __m256i temp0 = _mm256_add_epi32(in0_w0, in1_w1);
        temp0 = _mm256_add_epi32(temp0, _r);
        in0 = _mm256_srai_epi32(temp0, cos_bit);
        __m256i in0_w1 = _mm256_mullo_epi32(_in0, ww1);
        __m256i in1_w0 = _mm256_mullo_epi32(_in1, ww0);
        __m256i temp1 = _mm256_sub_epi32(in1_w0, in0_w1);
        temp1 = _mm256_add_epi32(temp1, _r);
        in1 = _mm256_srai_epi32(temp1, cos_bit);
    }

    // out0 = in0*w0 + in1*w1
    // out1 = -in1*w0 + in0*w1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void btf_32_avx2_type0_new(__m256i ww0, __m256i ww1, ref __m256i in0, ref __m256i in1, __m256i _r, int cos_bit)
    {
        __m256i _in0 = in0;
        __m256i _in1 = in1;
        __m256i in0_w0 = _mm256_mullo_epi32(_in0, ww0);
        __m256i in1_w1 = _mm256_mullo_epi32(_in1, ww1);
        __m256i temp0 = _mm256_add_epi32(in0_w0, in1_w1);
        temp0 = _mm256_add_epi32(temp0, _r);
        in0 = _mm256_srai_epi32(temp0, cos_bit);
        __m256i in0_w1 = _mm256_mullo_epi32(_in0, ww1);
        __m256i in1_w0 = _mm256_mullo_epi32(_in1, ww0);
        __m256i temp1 = _mm256_sub_epi32(in0_w1, in1_w0);
        temp1 = _mm256_add_epi32(temp1, _r);
        in1 = _mm256_srai_epi32(temp1, cos_bit);
    }

    // out0 = in0*w0 + in1*w1
    // out1 = in1*w0 - in0*w1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void btf_32_avx2_type1_new(__m256i ww0, __m256i ww1, ref __m256i in0, ref __m256i in1, __m256i _r, int cos_bit)
    {
        __m256i _in0 = in0;
        __m256i _in1 = in1;
        __m256i in0_w0 = _mm256_mullo_epi32(_in0, ww0);
        __m256i in1_w1 = _mm256_mullo_epi32(_in1, ww1);
        __m256i temp0 = _mm256_add_epi32(in0_w0, in1_w1);
        temp0 = _mm256_add_epi32(temp0, _r);
        in0 = _mm256_srai_epi32(temp0, cos_bit);
        __m256i in0_w1 = _mm256_mullo_epi32(_in0, ww1);
        __m256i in1_w0 = _mm256_mullo_epi32(_in1, ww0);
        __m256i temp1 = _mm256_sub_epi32(in1_w0, in0_w1);
        temp1 = _mm256_add_epi32(temp1, _r);
        in1 = _mm256_srai_epi32(temp1, cos_bit);
    }
}
