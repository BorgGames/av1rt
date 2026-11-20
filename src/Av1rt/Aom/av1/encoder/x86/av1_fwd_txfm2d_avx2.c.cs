// Port of libaom av1/encoder/x86/av1_fwd_txfm2d_avx2.c

global using static Av1rt.Aom.av1_fwd_txfm2d_avx2;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [subset] TX64 (fdct16x64_new_avx2, fdct64_new_avx2, the 64x64/32x64/64x32/16x64/64x16
// functions), fdct32_avx2 (only used by the 64-point sizes), store_output_32bit_w16 and
// the 4:1 SSE2 entries of fwd_txfm2d_func_ls are not ported: those transform sizes are not
// used by the realtime-only subset (their table entries are null).
internal static partial class av1_fwd_txfm2d_avx2
{
    // [port] C stack array, uninitialised: per-thread scratch instead of a zeroed stackalloc.
    [ThreadStatic] private static __m256i[] t_fdct16x16_new_avx2_x1;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fdct16x16_new_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        Ptr<int> cospi = cospi_arr(cos_bit);
        __m256i _r = _mm256_set1_epi32(1 << (cos_bit - 1));

        __m256i cospi_m32_p32 = pair_set_w16_epi16(-cospi[32], cospi[32]);
        __m256i cospi_p32_p32 = pair_set_w16_epi16(cospi[32], cospi[32]);
        __m256i cospi_p32_m32 = pair_set_w16_epi16(cospi[32], -cospi[32]);
        __m256i cospi_p48_p16 = pair_set_w16_epi16(cospi[48], cospi[16]);
        __m256i cospi_m16_p48 = pair_set_w16_epi16(-cospi[16], cospi[48]);
        __m256i cospi_m48_m16 = pair_set_w16_epi16(-cospi[48], -cospi[16]);
        __m256i cospi_p56_p08 = pair_set_w16_epi16(cospi[56], cospi[8]);
        __m256i cospi_m08_p56 = pair_set_w16_epi16(-cospi[8], cospi[56]);
        __m256i cospi_p24_p40 = pair_set_w16_epi16(cospi[24], cospi[40]);
        __m256i cospi_m40_p24 = pair_set_w16_epi16(-cospi[40], cospi[24]);
        __m256i cospi_p60_p04 = pair_set_w16_epi16(cospi[60], cospi[4]);
        __m256i cospi_m04_p60 = pair_set_w16_epi16(-cospi[4], cospi[60]);
        __m256i cospi_p28_p36 = pair_set_w16_epi16(cospi[28], cospi[36]);
        __m256i cospi_m36_p28 = pair_set_w16_epi16(-cospi[36], cospi[28]);
        __m256i cospi_p44_p20 = pair_set_w16_epi16(cospi[44], cospi[20]);
        __m256i cospi_m20_p44 = pair_set_w16_epi16(-cospi[20], cospi[44]);
        __m256i cospi_p12_p52 = pair_set_w16_epi16(cospi[12], cospi[52]);
        __m256i cospi_m52_p12 = pair_set_w16_epi16(-cospi[52], cospi[12]);

        // stage 1
        Span<__m256i> x1 = t_fdct16x16_new_avx2_x1 ??= new __m256i[16];
        btf_16_adds_subs_out_avx2(ref x1[0], ref x1[15], input[0], input[15]);
        btf_16_adds_subs_out_avx2(ref x1[1], ref x1[14], input[1], input[14]);
        btf_16_adds_subs_out_avx2(ref x1[2], ref x1[13], input[2], input[13]);
        btf_16_adds_subs_out_avx2(ref x1[3], ref x1[12], input[3], input[12]);
        btf_16_adds_subs_out_avx2(ref x1[4], ref x1[11], input[4], input[11]);
        btf_16_adds_subs_out_avx2(ref x1[5], ref x1[10], input[5], input[10]);
        btf_16_adds_subs_out_avx2(ref x1[6], ref x1[9], input[6], input[9]);
        btf_16_adds_subs_out_avx2(ref x1[7], ref x1[8], input[7], input[8]);

        // stage 2
        btf_16_adds_subs_avx2(ref x1[0], ref x1[7]);
        btf_16_adds_subs_avx2(ref x1[1], ref x1[6]);
        btf_16_adds_subs_avx2(ref x1[2], ref x1[5]);
        btf_16_adds_subs_avx2(ref x1[3], ref x1[4]);
        btf_16_w16_avx2(cospi_m32_p32, cospi_p32_p32, ref x1[10], ref x1[13], _r, cos_bit);
        btf_16_w16_avx2(cospi_m32_p32, cospi_p32_p32, ref x1[11], ref x1[12], _r, cos_bit);

        // stage 3
        btf_16_adds_subs_avx2(ref x1[0], ref x1[3]);
        btf_16_adds_subs_avx2(ref x1[1], ref x1[2]);
        btf_16_w16_avx2(cospi_m32_p32, cospi_p32_p32, ref x1[5], ref x1[6], _r, cos_bit);
        btf_16_adds_subs_avx2(ref x1[8], ref x1[11]);
        btf_16_adds_subs_avx2(ref x1[9], ref x1[10]);
        btf_16_adds_subs_avx2(ref x1[15], ref x1[12]);
        btf_16_adds_subs_avx2(ref x1[14], ref x1[13]);

        // stage 4
        btf_16_w16_avx2(cospi_p32_p32, cospi_p32_m32, ref x1[0], ref x1[1], _r, cos_bit);
        btf_16_w16_avx2(cospi_p48_p16, cospi_m16_p48, ref x1[2], ref x1[3], _r, cos_bit);
        btf_16_adds_subs_avx2(ref x1[4], ref x1[5]);
        btf_16_adds_subs_avx2(ref x1[7], ref x1[6]);
        btf_16_w16_avx2(cospi_m16_p48, cospi_p48_p16, ref x1[9], ref x1[14], _r, cos_bit);
        btf_16_w16_avx2(cospi_m48_m16, cospi_m16_p48, ref x1[10], ref x1[13], _r, cos_bit);

        // stage 5
        btf_16_w16_avx2(cospi_p56_p08, cospi_m08_p56, ref x1[4], ref x1[7], _r, cos_bit);
        btf_16_w16_avx2(cospi_p24_p40, cospi_m40_p24, ref x1[5], ref x1[6], _r, cos_bit);
        btf_16_adds_subs_avx2(ref x1[8], ref x1[9]);
        btf_16_adds_subs_avx2(ref x1[11], ref x1[10]);
        btf_16_adds_subs_avx2(ref x1[12], ref x1[13]);
        btf_16_adds_subs_avx2(ref x1[15], ref x1[14]);

        // stage 6
        btf_16_w16_avx2(cospi_p60_p04, cospi_m04_p60, ref x1[8], ref x1[15], _r, cos_bit);
        btf_16_w16_avx2(cospi_p28_p36, cospi_m36_p28, ref x1[9], ref x1[14], _r, cos_bit);
        btf_16_w16_avx2(cospi_p44_p20, cospi_m20_p44, ref x1[10], ref x1[13], _r, cos_bit);
        btf_16_w16_avx2(cospi_p12_p52, cospi_m52_p12, ref x1[11], ref x1[12], _r, cos_bit);

        // stage 7
        output[0] = x1[0];
        output[1] = x1[8];
        output[2] = x1[4];
        output[3] = x1[12];
        output[4] = x1[2];
        output[5] = x1[10];
        output[6] = x1[6];
        output[7] = x1[14];
        output[8] = x1[1];
        output[9] = x1[9];
        output[10] = x1[5];
        output[11] = x1[13];
        output[12] = x1[3];
        output[13] = x1[11];
        output[14] = x1[7];
        output[15] = x1[15];
    }

    // [port] NoInlining: the kernel inlined into a 2D driver twice exhausts the JIT's inline
    // budget there, so each constant-cos_bit instance is its own root.
    [MethodImpl(MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void fdct16x16_cb13_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        _ = cos_bit;
        fdct16x16_new_avx2(input, output, 13);
    }

    // [port] NoInlining: the kernel inlined into a 2D driver twice exhausts the JIT's inline
    // budget there, so each constant-cos_bit instance is its own root.
    [MethodImpl(MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void fdct16x16_cb12_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        _ = cos_bit;
        fdct16x16_new_avx2(input, output, 12);
    }

    // [port] C stack array, uninitialised: per-thread scratch instead of a zeroed stackalloc.
    [ThreadStatic] private static __m256i[] t_fdct16x32_avx2_x1;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fdct16x32_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        Ptr<int> cospi = cospi_arr(cos_bit);
        __m256i _r = _mm256_set1_epi32(1 << (cos_bit - 1));

        __m256i cospi_m32_p32 = pair_set_w16_epi16(-cospi[32], cospi[32]);
        __m256i cospi_p32_p32 = pair_set_w16_epi16(cospi[32], cospi[32]);
        __m256i cospi_m16_p48 = pair_set_w16_epi16(-cospi[16], cospi[48]);
        __m256i cospi_p48_p16 = pair_set_w16_epi16(cospi[48], cospi[16]);
        __m256i cospi_m48_m16 = pair_set_w16_epi16(-cospi[48], -cospi[16]);
        __m256i cospi_p32_m32 = pair_set_w16_epi16(cospi[32], -cospi[32]);
        __m256i cospi_p56_p08 = pair_set_w16_epi16(cospi[56], cospi[8]);
        __m256i cospi_m08_p56 = pair_set_w16_epi16(-cospi[8], cospi[56]);
        __m256i cospi_p24_p40 = pair_set_w16_epi16(cospi[24], cospi[40]);
        __m256i cospi_m40_p24 = pair_set_w16_epi16(-cospi[40], cospi[24]);
        __m256i cospi_m56_m08 = pair_set_w16_epi16(-cospi[56], -cospi[8]);
        __m256i cospi_m24_m40 = pair_set_w16_epi16(-cospi[24], -cospi[40]);
        __m256i cospi_p60_p04 = pair_set_w16_epi16(cospi[60], cospi[4]);
        __m256i cospi_m04_p60 = pair_set_w16_epi16(-cospi[4], cospi[60]);
        __m256i cospi_p28_p36 = pair_set_w16_epi16(cospi[28], cospi[36]);
        __m256i cospi_m36_p28 = pair_set_w16_epi16(-cospi[36], cospi[28]);
        __m256i cospi_p44_p20 = pair_set_w16_epi16(cospi[44], cospi[20]);
        __m256i cospi_m20_p44 = pair_set_w16_epi16(-cospi[20], cospi[44]);
        __m256i cospi_p12_p52 = pair_set_w16_epi16(cospi[12], cospi[52]);
        __m256i cospi_m52_p12 = pair_set_w16_epi16(-cospi[52], cospi[12]);
        __m256i cospi_p62_p02 = pair_set_w16_epi16(cospi[62], cospi[2]);
        __m256i cospi_m02_p62 = pair_set_w16_epi16(-cospi[2], cospi[62]);
        __m256i cospi_p30_p34 = pair_set_w16_epi16(cospi[30], cospi[34]);
        __m256i cospi_m34_p30 = pair_set_w16_epi16(-cospi[34], cospi[30]);
        __m256i cospi_p46_p18 = pair_set_w16_epi16(cospi[46], cospi[18]);
        __m256i cospi_m18_p46 = pair_set_w16_epi16(-cospi[18], cospi[46]);
        __m256i cospi_p14_p50 = pair_set_w16_epi16(cospi[14], cospi[50]);
        __m256i cospi_m50_p14 = pair_set_w16_epi16(-cospi[50], cospi[14]);
        __m256i cospi_p54_p10 = pair_set_w16_epi16(cospi[54], cospi[10]);
        __m256i cospi_m10_p54 = pair_set_w16_epi16(-cospi[10], cospi[54]);
        __m256i cospi_p22_p42 = pair_set_w16_epi16(cospi[22], cospi[42]);
        __m256i cospi_m42_p22 = pair_set_w16_epi16(-cospi[42], cospi[22]);
        __m256i cospi_p38_p26 = pair_set_w16_epi16(cospi[38], cospi[26]);
        __m256i cospi_m26_p38 = pair_set_w16_epi16(-cospi[26], cospi[38]);
        __m256i cospi_p06_p58 = pair_set_w16_epi16(cospi[6], cospi[58]);
        __m256i cospi_m58_p06 = pair_set_w16_epi16(-cospi[58], cospi[6]);

        // stage 1
        Span<__m256i> x1 = t_fdct16x32_avx2_x1 ??= new __m256i[32];
        btf_16_adds_subs_out_avx2(ref x1[0], ref x1[31], input[0], input[31]);
        btf_16_adds_subs_out_avx2(ref x1[1], ref x1[30], input[1], input[30]);
        btf_16_adds_subs_out_avx2(ref x1[2], ref x1[29], input[2], input[29]);
        btf_16_adds_subs_out_avx2(ref x1[3], ref x1[28], input[3], input[28]);
        btf_16_adds_subs_out_avx2(ref x1[4], ref x1[27], input[4], input[27]);
        btf_16_adds_subs_out_avx2(ref x1[5], ref x1[26], input[5], input[26]);
        btf_16_adds_subs_out_avx2(ref x1[6], ref x1[25], input[6], input[25]);
        btf_16_adds_subs_out_avx2(ref x1[7], ref x1[24], input[7], input[24]);
        btf_16_adds_subs_out_avx2(ref x1[8], ref x1[23], input[8], input[23]);
        btf_16_adds_subs_out_avx2(ref x1[9], ref x1[22], input[9], input[22]);
        btf_16_adds_subs_out_avx2(ref x1[10], ref x1[21], input[10], input[21]);
        btf_16_adds_subs_out_avx2(ref x1[11], ref x1[20], input[11], input[20]);
        btf_16_adds_subs_out_avx2(ref x1[12], ref x1[19], input[12], input[19]);
        btf_16_adds_subs_out_avx2(ref x1[13], ref x1[18], input[13], input[18]);
        btf_16_adds_subs_out_avx2(ref x1[14], ref x1[17], input[14], input[17]);
        btf_16_adds_subs_out_avx2(ref x1[15], ref x1[16], input[15], input[16]);

        // stage 2
        btf_16_adds_subs_avx2(ref x1[0], ref x1[15]);
        btf_16_adds_subs_avx2(ref x1[1], ref x1[14]);
        btf_16_adds_subs_avx2(ref x1[2], ref x1[13]);
        btf_16_adds_subs_avx2(ref x1[3], ref x1[12]);
        btf_16_adds_subs_avx2(ref x1[4], ref x1[11]);
        btf_16_adds_subs_avx2(ref x1[5], ref x1[10]);
        btf_16_adds_subs_avx2(ref x1[6], ref x1[9]);
        btf_16_adds_subs_avx2(ref x1[7], ref x1[8]);
        btf_16_w16_avx2(cospi_m32_p32, cospi_p32_p32, ref x1[20], ref x1[27], _r, cos_bit);
        btf_16_w16_avx2(cospi_m32_p32, cospi_p32_p32, ref x1[21], ref x1[26], _r, cos_bit);
        btf_16_w16_avx2(cospi_m32_p32, cospi_p32_p32, ref x1[22], ref x1[25], _r, cos_bit);
        btf_16_w16_avx2(cospi_m32_p32, cospi_p32_p32, ref x1[23], ref x1[24], _r, cos_bit);

        // stage 3
        btf_16_adds_subs_avx2(ref x1[0], ref x1[7]);
        btf_16_adds_subs_avx2(ref x1[1], ref x1[6]);
        btf_16_adds_subs_avx2(ref x1[2], ref x1[5]);
        btf_16_adds_subs_avx2(ref x1[3], ref x1[4]);
        btf_16_w16_avx2(cospi_m32_p32, cospi_p32_p32, ref x1[10], ref x1[13], _r, cos_bit);
        btf_16_w16_avx2(cospi_m32_p32, cospi_p32_p32, ref x1[11], ref x1[12], _r, cos_bit);
        btf_16_adds_subs_avx2(ref x1[16], ref x1[23]);
        btf_16_adds_subs_avx2(ref x1[17], ref x1[22]);
        btf_16_adds_subs_avx2(ref x1[18], ref x1[21]);
        btf_16_adds_subs_avx2(ref x1[19], ref x1[20]);
        btf_16_adds_subs_avx2(ref x1[31], ref x1[24]);
        btf_16_adds_subs_avx2(ref x1[30], ref x1[25]);
        btf_16_adds_subs_avx2(ref x1[29], ref x1[26]);
        btf_16_adds_subs_avx2(ref x1[28], ref x1[27]);

        // stage 4
        btf_16_adds_subs_avx2(ref x1[0], ref x1[3]);
        btf_16_adds_subs_avx2(ref x1[1], ref x1[2]);
        btf_16_w16_avx2(cospi_m32_p32, cospi_p32_p32, ref x1[5], ref x1[6], _r, cos_bit);
        btf_16_adds_subs_avx2(ref x1[8], ref x1[11]);
        btf_16_adds_subs_avx2(ref x1[9], ref x1[10]);
        btf_16_adds_subs_avx2(ref x1[15], ref x1[12]);
        btf_16_adds_subs_avx2(ref x1[14], ref x1[13]);
        btf_16_w16_avx2(cospi_m16_p48, cospi_p48_p16, ref x1[18], ref x1[29], _r, cos_bit);
        btf_16_w16_avx2(cospi_m16_p48, cospi_p48_p16, ref x1[19], ref x1[28], _r, cos_bit);
        btf_16_w16_avx2(cospi_m48_m16, cospi_m16_p48, ref x1[20], ref x1[27], _r, cos_bit);
        btf_16_w16_avx2(cospi_m48_m16, cospi_m16_p48, ref x1[21], ref x1[26], _r, cos_bit);

        // stage 5
        btf_16_w16_avx2(cospi_p32_p32, cospi_p32_m32, ref x1[0], ref x1[1], _r, cos_bit);
        btf_16_w16_avx2(cospi_p48_p16, cospi_m16_p48, ref x1[2], ref x1[3], _r, cos_bit);
        btf_16_adds_subs_avx2(ref x1[4], ref x1[5]);
        btf_16_adds_subs_avx2(ref x1[7], ref x1[6]);
        btf_16_w16_avx2(cospi_m16_p48, cospi_p48_p16, ref x1[9], ref x1[14], _r, cos_bit);
        btf_16_w16_avx2(cospi_m48_m16, cospi_m16_p48, ref x1[10], ref x1[13], _r, cos_bit);
        btf_16_adds_subs_avx2(ref x1[16], ref x1[19]);
        btf_16_adds_subs_avx2(ref x1[17], ref x1[18]);
        btf_16_adds_subs_avx2(ref x1[23], ref x1[20]);
        btf_16_adds_subs_avx2(ref x1[22], ref x1[21]);
        btf_16_adds_subs_avx2(ref x1[24], ref x1[27]);
        btf_16_adds_subs_avx2(ref x1[25], ref x1[26]);
        btf_16_adds_subs_avx2(ref x1[31], ref x1[28]);
        btf_16_adds_subs_avx2(ref x1[30], ref x1[29]);

        // stage 6
        btf_16_w16_avx2(cospi_p56_p08, cospi_m08_p56, ref x1[4], ref x1[7], _r, cos_bit);
        btf_16_w16_avx2(cospi_p24_p40, cospi_m40_p24, ref x1[5], ref x1[6], _r, cos_bit);
        btf_16_adds_subs_avx2(ref x1[8], ref x1[9]);
        btf_16_adds_subs_avx2(ref x1[11], ref x1[10]);
        btf_16_adds_subs_avx2(ref x1[12], ref x1[13]);
        btf_16_adds_subs_avx2(ref x1[15], ref x1[14]);
        btf_16_w16_avx2(cospi_m08_p56, cospi_p56_p08, ref x1[17], ref x1[30], _r, cos_bit);
        btf_16_w16_avx2(cospi_m56_m08, cospi_m08_p56, ref x1[18], ref x1[29], _r, cos_bit);
        btf_16_w16_avx2(cospi_m40_p24, cospi_p24_p40, ref x1[21], ref x1[26], _r, cos_bit);
        btf_16_w16_avx2(cospi_m24_m40, cospi_m40_p24, ref x1[22], ref x1[25], _r, cos_bit);

        // stage 7
        btf_16_w16_avx2(cospi_p60_p04, cospi_m04_p60, ref x1[8], ref x1[15], _r, cos_bit);
        btf_16_w16_avx2(cospi_p28_p36, cospi_m36_p28, ref x1[9], ref x1[14], _r, cos_bit);
        btf_16_w16_avx2(cospi_p44_p20, cospi_m20_p44, ref x1[10], ref x1[13], _r, cos_bit);
        btf_16_w16_avx2(cospi_p12_p52, cospi_m52_p12, ref x1[11], ref x1[12], _r, cos_bit);
        btf_16_adds_subs_avx2(ref x1[16], ref x1[17]);
        btf_16_adds_subs_avx2(ref x1[19], ref x1[18]);
        btf_16_adds_subs_avx2(ref x1[20], ref x1[21]);
        btf_16_adds_subs_avx2(ref x1[23], ref x1[22]);
        btf_16_adds_subs_avx2(ref x1[24], ref x1[25]);
        btf_16_adds_subs_avx2(ref x1[27], ref x1[26]);
        btf_16_adds_subs_avx2(ref x1[28], ref x1[29]);
        btf_16_adds_subs_avx2(ref x1[31], ref x1[30]);

        // stage 8
        btf_16_w16_avx2(cospi_p62_p02, cospi_m02_p62, ref x1[16], ref x1[31], _r, cos_bit);
        btf_16_w16_avx2(cospi_p30_p34, cospi_m34_p30, ref x1[17], ref x1[30], _r, cos_bit);
        btf_16_w16_avx2(cospi_p46_p18, cospi_m18_p46, ref x1[18], ref x1[29], _r, cos_bit);
        btf_16_w16_avx2(cospi_p14_p50, cospi_m50_p14, ref x1[19], ref x1[28], _r, cos_bit);
        btf_16_w16_avx2(cospi_p54_p10, cospi_m10_p54, ref x1[20], ref x1[27], _r, cos_bit);
        btf_16_w16_avx2(cospi_p22_p42, cospi_m42_p22, ref x1[21], ref x1[26], _r, cos_bit);
        btf_16_w16_avx2(cospi_p38_p26, cospi_m26_p38, ref x1[22], ref x1[25], _r, cos_bit);
        btf_16_w16_avx2(cospi_p06_p58, cospi_m58_p06, ref x1[23], ref x1[24], _r, cos_bit);

        // stage 9
        output[0] = x1[0];
        output[1] = x1[16];
        output[2] = x1[8];
        output[3] = x1[24];
        output[4] = x1[4];
        output[5] = x1[20];
        output[6] = x1[12];
        output[7] = x1[28];
        output[8] = x1[2];
        output[9] = x1[18];
        output[10] = x1[10];
        output[11] = x1[26];
        output[12] = x1[6];
        output[13] = x1[22];
        output[14] = x1[14];
        output[15] = x1[30];
        output[16] = x1[1];
        output[17] = x1[17];
        output[18] = x1[9];
        output[19] = x1[25];
        output[20] = x1[5];
        output[21] = x1[21];
        output[22] = x1[13];
        output[23] = x1[29];
        output[24] = x1[3];
        output[25] = x1[19];
        output[26] = x1[11];
        output[27] = x1[27];
        output[28] = x1[7];
        output[29] = x1[23];
        output[30] = x1[15];
        output[31] = x1[31];
    }

    // [port] NoInlining: the kernel inlined into a 2D driver twice exhausts the JIT's inline
    // budget there, so each constant-cos_bit instance is its own root.
    [MethodImpl(MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void fdct16x32_cb12_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        _ = cos_bit;
        fdct16x32_avx2(input, output, 12);
    }

    // [port] NoInlining: the kernel inlined into a 2D driver twice exhausts the JIT's inline
    // budget there, so each constant-cos_bit instance is its own root.
    [MethodImpl(MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void fdct16x32_cb13_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        _ = cos_bit;
        fdct16x32_avx2(input, output, 13);
    }

    // [port] C stack array, uninitialised: per-thread scratch instead of a zeroed stackalloc.
    [ThreadStatic] private static __m256i[] t_fadst16x16_new_avx2_x1;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fadst16x16_new_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        Ptr<int> cospi = cospi_arr(cos_bit);
        __m256i __zero = _mm256_setzero_si256();
        __m256i _r = _mm256_set1_epi32(1 << (cos_bit - 1));

        __m256i cospi_p32_p32 = pair_set_w16_epi16(cospi[32], cospi[32]);
        __m256i cospi_p32_m32 = pair_set_w16_epi16(cospi[32], -cospi[32]);
        __m256i cospi_p16_p48 = pair_set_w16_epi16(cospi[16], cospi[48]);
        __m256i cospi_p48_m16 = pair_set_w16_epi16(cospi[48], -cospi[16]);
        __m256i cospi_m48_p16 = pair_set_w16_epi16(-cospi[48], cospi[16]);
        __m256i cospi_p08_p56 = pair_set_w16_epi16(cospi[8], cospi[56]);
        __m256i cospi_p56_m08 = pair_set_w16_epi16(cospi[56], -cospi[8]);
        __m256i cospi_p40_p24 = pair_set_w16_epi16(cospi[40], cospi[24]);
        __m256i cospi_p24_m40 = pair_set_w16_epi16(cospi[24], -cospi[40]);
        __m256i cospi_m56_p08 = pair_set_w16_epi16(-cospi[56], cospi[8]);
        __m256i cospi_m24_p40 = pair_set_w16_epi16(-cospi[24], cospi[40]);
        __m256i cospi_p02_p62 = pair_set_w16_epi16(cospi[2], cospi[62]);
        __m256i cospi_p62_m02 = pair_set_w16_epi16(cospi[62], -cospi[2]);
        __m256i cospi_p10_p54 = pair_set_w16_epi16(cospi[10], cospi[54]);
        __m256i cospi_p54_m10 = pair_set_w16_epi16(cospi[54], -cospi[10]);
        __m256i cospi_p18_p46 = pair_set_w16_epi16(cospi[18], cospi[46]);
        __m256i cospi_p46_m18 = pair_set_w16_epi16(cospi[46], -cospi[18]);
        __m256i cospi_p26_p38 = pair_set_w16_epi16(cospi[26], cospi[38]);
        __m256i cospi_p38_m26 = pair_set_w16_epi16(cospi[38], -cospi[26]);
        __m256i cospi_p34_p30 = pair_set_w16_epi16(cospi[34], cospi[30]);
        __m256i cospi_p30_m34 = pair_set_w16_epi16(cospi[30], -cospi[34]);
        __m256i cospi_p42_p22 = pair_set_w16_epi16(cospi[42], cospi[22]);
        __m256i cospi_p22_m42 = pair_set_w16_epi16(cospi[22], -cospi[42]);
        __m256i cospi_p50_p14 = pair_set_w16_epi16(cospi[50], cospi[14]);
        __m256i cospi_p14_m50 = pair_set_w16_epi16(cospi[14], -cospi[50]);
        __m256i cospi_p58_p06 = pair_set_w16_epi16(cospi[58], cospi[6]);
        __m256i cospi_p06_m58 = pair_set_w16_epi16(cospi[6], -cospi[58]);

        // stage 1
        Span<__m256i> x1 = t_fadst16x16_new_avx2_x1 ??= new __m256i[16];
        x1[0] = input[0];
        x1[1] = _mm256_subs_epi16(__zero, input[15]);
        x1[2] = _mm256_subs_epi16(__zero, input[7]);
        x1[3] = input[8];
        x1[4] = _mm256_subs_epi16(__zero, input[3]);
        x1[5] = input[12];
        x1[6] = input[4];
        x1[7] = _mm256_subs_epi16(__zero, input[11]);
        x1[8] = _mm256_subs_epi16(__zero, input[1]);
        x1[9] = input[14];
        x1[10] = input[6];
        x1[11] = _mm256_subs_epi16(__zero, input[9]);
        x1[12] = input[2];
        x1[13] = _mm256_subs_epi16(__zero, input[13]);
        x1[14] = _mm256_subs_epi16(__zero, input[5]);
        x1[15] = input[10];

        // stage 2
        btf_16_w16_avx2(cospi_p32_p32, cospi_p32_m32, ref x1[2], ref x1[3], _r, cos_bit);
        btf_16_w16_avx2(cospi_p32_p32, cospi_p32_m32, ref x1[6], ref x1[7], _r, cos_bit);
        btf_16_w16_avx2(cospi_p32_p32, cospi_p32_m32, ref x1[10], ref x1[11], _r, cos_bit);
        btf_16_w16_avx2(cospi_p32_p32, cospi_p32_m32, ref x1[14], ref x1[15], _r, cos_bit);

        // stage 3
        btf_16_adds_subs_avx2(ref x1[0], ref x1[2]);
        btf_16_adds_subs_avx2(ref x1[1], ref x1[3]);
        btf_16_adds_subs_avx2(ref x1[4], ref x1[6]);
        btf_16_adds_subs_avx2(ref x1[5], ref x1[7]);
        btf_16_adds_subs_avx2(ref x1[8], ref x1[10]);
        btf_16_adds_subs_avx2(ref x1[9], ref x1[11]);
        btf_16_adds_subs_avx2(ref x1[12], ref x1[14]);
        btf_16_adds_subs_avx2(ref x1[13], ref x1[15]);

        // stage 4
        btf_16_w16_avx2(cospi_p16_p48, cospi_p48_m16, ref x1[4], ref x1[5], _r, cos_bit);
        btf_16_w16_avx2(cospi_m48_p16, cospi_p16_p48, ref x1[6], ref x1[7], _r, cos_bit);
        btf_16_w16_avx2(cospi_p16_p48, cospi_p48_m16, ref x1[12], ref x1[13], _r, cos_bit);
        btf_16_w16_avx2(cospi_m48_p16, cospi_p16_p48, ref x1[14], ref x1[15], _r, cos_bit);

        // stage 5
        btf_16_adds_subs_avx2(ref x1[0], ref x1[4]);
        btf_16_adds_subs_avx2(ref x1[1], ref x1[5]);
        btf_16_adds_subs_avx2(ref x1[2], ref x1[6]);
        btf_16_adds_subs_avx2(ref x1[3], ref x1[7]);
        btf_16_adds_subs_avx2(ref x1[8], ref x1[12]);
        btf_16_adds_subs_avx2(ref x1[9], ref x1[13]);
        btf_16_adds_subs_avx2(ref x1[10], ref x1[14]);
        btf_16_adds_subs_avx2(ref x1[11], ref x1[15]);

        // stage 6
        btf_16_w16_avx2(cospi_p08_p56, cospi_p56_m08, ref x1[8], ref x1[9], _r, cos_bit);
        btf_16_w16_avx2(cospi_p40_p24, cospi_p24_m40, ref x1[10], ref x1[11], _r, cos_bit);
        btf_16_w16_avx2(cospi_m56_p08, cospi_p08_p56, ref x1[12], ref x1[13], _r, cos_bit);
        btf_16_w16_avx2(cospi_m24_p40, cospi_p40_p24, ref x1[14], ref x1[15], _r, cos_bit);

        // stage 7
        btf_16_adds_subs_avx2(ref x1[0], ref x1[8]);
        btf_16_adds_subs_avx2(ref x1[1], ref x1[9]);
        btf_16_adds_subs_avx2(ref x1[2], ref x1[10]);
        btf_16_adds_subs_avx2(ref x1[3], ref x1[11]);
        btf_16_adds_subs_avx2(ref x1[4], ref x1[12]);
        btf_16_adds_subs_avx2(ref x1[5], ref x1[13]);
        btf_16_adds_subs_avx2(ref x1[6], ref x1[14]);
        btf_16_adds_subs_avx2(ref x1[7], ref x1[15]);

        // stage 8
        btf_16_w16_avx2(cospi_p02_p62, cospi_p62_m02, ref x1[0], ref x1[1], _r, cos_bit);
        btf_16_w16_avx2(cospi_p10_p54, cospi_p54_m10, ref x1[2], ref x1[3], _r, cos_bit);
        btf_16_w16_avx2(cospi_p18_p46, cospi_p46_m18, ref x1[4], ref x1[5], _r, cos_bit);
        btf_16_w16_avx2(cospi_p26_p38, cospi_p38_m26, ref x1[6], ref x1[7], _r, cos_bit);
        btf_16_w16_avx2(cospi_p34_p30, cospi_p30_m34, ref x1[8], ref x1[9], _r, cos_bit);
        btf_16_w16_avx2(cospi_p42_p22, cospi_p22_m42, ref x1[10], ref x1[11], _r, cos_bit);
        btf_16_w16_avx2(cospi_p50_p14, cospi_p14_m50, ref x1[12], ref x1[13], _r, cos_bit);
        btf_16_w16_avx2(cospi_p58_p06, cospi_p06_m58, ref x1[14], ref x1[15], _r, cos_bit);

        // stage 9
        output[0] = x1[1];
        output[1] = x1[14];
        output[2] = x1[3];
        output[3] = x1[12];
        output[4] = x1[5];
        output[5] = x1[10];
        output[6] = x1[7];
        output[7] = x1[8];
        output[8] = x1[9];
        output[9] = x1[6];
        output[10] = x1[11];
        output[11] = x1[4];
        output[12] = x1[13];
        output[13] = x1[2];
        output[14] = x1[15];
        output[15] = x1[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fadst16x16_cb13_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        _ = cos_bit;
        fadst16x16_new_avx2(input, output, 13);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fadst16x16_cb12_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        _ = cos_bit;
        fadst16x16_new_avx2(input, output, 12);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fidentity16x16_new_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        _ = cos_bit;
        __m256i one = _mm256_set1_epi16(1);

        for (int i = 0; i < 16; ++i)
        {
            __m256i a_lo = _mm256_unpacklo_epi16(input[i], one);
            __m256i a_hi = _mm256_unpackhi_epi16(input[i], one);
            __m256i b_lo = scale_round_avx2(a_lo, 2 * NewSqrt2);
            __m256i b_hi = scale_round_avx2(a_hi, 2 * NewSqrt2);
            output[i] = _mm256_packs_epi32(b_lo, b_hi);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fidentity16x32_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        _ = cos_bit;
        for (int i = 0; i < 32; ++i)
        {
            output[i] = _mm256_slli_epi16(input[i], 2);
        }
    }

    // Store 8 16 bit values. Sign extend the values.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void store_buffer_16bit_to_32bit_w16_avx2(Span<__m256i> @in, Ptr<int> @out, int stride, int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            _mm256_store_si256((@out),
                                                  _mm256_cvtepi16_epi32(_mm256_castsi256_si128(@in[i])));
            _mm256_store_si256(
                    (@out + 8),
                    _mm256_cvtepi16_epi32(_mm256_extracti128_si256(@in[i], 1)));
            @out += stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void store_rect_16bit_to_32bit_avx2(__m256i a, Ptr<int> b)
    {
        __m256i one = _mm256_set1_epi16(1);
        __m256i a_reoder = _mm256_permute4x64_epi64(a, 0xd8);
        __m256i a_lo = _mm256_unpacklo_epi16(a_reoder, one);
        __m256i a_hi = _mm256_unpackhi_epi16(a_reoder, one);
        __m256i b_lo = scale_round_avx2(a_lo, NewSqrt2);
        __m256i b_hi = scale_round_avx2(a_hi, NewSqrt2);
        _mm256_store_si256(b, b_lo);
        _mm256_store_si256((b + 8), b_hi);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void store_rect_buffer_16bit_to_32bit_w16_avx2(Span<__m256i> @in, Ptr<int> @out, int stride, int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            store_rect_16bit_to_32bit_avx2(@in[i], @out + i * stride);
        }
    }

    private delegate void transform_1d_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit);

    private static readonly transform_1d_avx2[] col_txfm16x32_arr = {
        fdct16x32_cb12_avx2,  // DCT_DCT
        null,                 // ADST_DCT
        null,                 // DCT_ADST
        null,                 // ADST_ADST
        null,                 // FLIPADST_DCT
        null,                 // DCT_FLIPADST
        null,                 // FLIPADST_FLIPADST
        null,                 // ADST_FLIPADST
        null,                 // FLIPADST_ADST
        fidentity16x32_avx2,  // IDTX
        fdct16x32_cb12_avx2,  // V_DCT
        fidentity16x32_avx2,  // H_DCT
        null,                 // V_ADST
        null,                 // H_ADST
        null,                 // V_FLIPADST
        null                  // H_FLIPADST
    };

    private static readonly transform_1d_avx2[] row_txfm16x32_cb12_arr = {
        fdct16x32_cb12_avx2,  // DCT_DCT
        null,                 // ADST_DCT
        null,                 // DCT_ADST
        null,                 // ADST_ADST
        null,                 // FLIPADST_DCT
        null,                 // DCT_FLIPADST
        null,                 // FLIPADST_FLIPADST
        null,                 // ADST_FLIPADST
        null,                 // FLIPADST_ADST
        fidentity16x32_avx2,  // IDTX
        fidentity16x32_avx2,  // V_DCT
        fdct16x32_cb12_avx2,  // H_DCT
        null,                 // V_ADST
        null,                 // H_ADST
        null,                 // V_FLIPADST
        null                  // H_FLIPADST
    };

    private static readonly transform_1d_avx2[] row_txfm16x32_cb13_arr = {
        fdct16x32_cb13_avx2,  // DCT_DCT
        null,                 // ADST_DCT
        null,                 // DCT_ADST
        null,                 // ADST_ADST
        null,                 // FLIPADST_DCT
        null,                 // DCT_FLIPADST
        null,                 // FLIPADST_FLIPADST
        null,                 // ADST_FLIPADST
        null,                 // FLIPADST_ADST
        fidentity16x32_avx2,  // IDTX
        fidentity16x32_avx2,  // V_DCT
        fdct16x32_cb13_avx2,  // H_DCT
        null,                 // V_ADST
        null,                 // H_ADST
        null,                 // V_FLIPADST
        null                  // H_FLIPADST
    };

    private static readonly transform_1d_avx2[] col_txfm16x16_arr = {
        fdct16x16_cb13_avx2,      // DCT_DCT
        fadst16x16_cb13_avx2,     // ADST_DCT
        fdct16x16_cb13_avx2,      // DCT_ADST
        fadst16x16_cb13_avx2,     // ADST_ADST
        fadst16x16_cb13_avx2,     // FLIPADST_DCT
        fdct16x16_cb13_avx2,      // DCT_FLIPADST
        fadst16x16_cb13_avx2,     // FLIPADST_FLIPADST
        fadst16x16_cb13_avx2,     // ADST_FLIPADST
        fadst16x16_cb13_avx2,     // FLIPADST_ADST
        fidentity16x16_new_avx2,  // IDTX
        fdct16x16_cb13_avx2,      // V_DCT
        fidentity16x16_new_avx2,  // H_DCT
        fadst16x16_cb13_avx2,     // V_ADST
        fidentity16x16_new_avx2,  // H_ADST
        fadst16x16_cb13_avx2,     // V_FLIPADST
        fidentity16x16_new_avx2   // H_FLIPADST
    };

    private static readonly transform_1d_avx2[] row_txfm16x16_cb12_arr = {
        fdct16x16_cb12_avx2,      // DCT_DCT
        fdct16x16_cb12_avx2,      // ADST_DCT
        fadst16x16_cb12_avx2,     // DCT_ADST
        fadst16x16_cb12_avx2,     // ADST_ADST
        fdct16x16_cb12_avx2,      // FLIPADST_DCT
        fadst16x16_cb12_avx2,     // DCT_FLIPADST
        fadst16x16_cb12_avx2,     // FLIPADST_FLIPADST
        fadst16x16_cb12_avx2,     // ADST_FLIPADST
        fadst16x16_cb12_avx2,     // FLIPADST_ADST
        fidentity16x16_new_avx2,  // IDTX
        fidentity16x16_new_avx2,  // V_DCT
        fdct16x16_cb12_avx2,      // H_DCT
        fidentity16x16_new_avx2,  // V_ADST
        fadst16x16_cb12_avx2,     // H_ADST
        fidentity16x16_new_avx2,  // V_FLIPADST
        fadst16x16_cb12_avx2      // H_FLIPADST
    };

    private static readonly transform_1d_avx2[] row_txfm16x16_cb13_arr = {
        fdct16x16_cb13_avx2,      // DCT_DCT
        fdct16x16_cb13_avx2,      // ADST_DCT
        fadst16x16_cb13_avx2,     // DCT_ADST
        fadst16x16_cb13_avx2,     // ADST_ADST
        fdct16x16_cb13_avx2,      // FLIPADST_DCT
        fadst16x16_cb13_avx2,     // DCT_FLIPADST
        fadst16x16_cb13_avx2,     // FLIPADST_FLIPADST
        fadst16x16_cb13_avx2,     // ADST_FLIPADST
        fadst16x16_cb13_avx2,     // FLIPADST_ADST
        fidentity16x16_new_avx2,  // IDTX
        fidentity16x16_new_avx2,  // V_DCT
        fdct16x16_cb13_avx2,      // H_DCT
        fidentity16x16_new_avx2,  // V_ADST
        fadst16x16_cb13_avx2,     // H_ADST
        fidentity16x16_new_avx2,  // V_FLIPADST
        fadst16x16_cb13_avx2      // H_FLIPADST
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void load_buffer_and_round_shift(Ptr<short> @in, int stride, Span<__m128i> @out, int bit)
    {
        @out[0] = _mm_load_si128((@in + 0 * stride));
        @out[1] = _mm_load_si128((@in + 1 * stride));
        @out[2] = _mm_load_si128((@in + 2 * stride));
        @out[3] = _mm_load_si128((@in + 3 * stride));
        @out[4] = _mm_load_si128((@in + 4 * stride));
        @out[5] = _mm_load_si128((@in + 5 * stride));
        @out[6] = _mm_load_si128((@in + 6 * stride));
        @out[7] = _mm_load_si128((@in + 7 * stride));
        @out[0] = _mm_slli_epi16(@out[0], bit);
        @out[1] = _mm_slli_epi16(@out[1], bit);
        @out[2] = _mm_slli_epi16(@out[2], bit);
        @out[3] = _mm_slli_epi16(@out[3], bit);
        @out[4] = _mm_slli_epi16(@out[4], bit);
        @out[5] = _mm_slli_epi16(@out[5], bit);
        @out[6] = _mm_slli_epi16(@out[6], bit);
        @out[7] = _mm_slli_epi16(@out[7], bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void load_buffer_and_flip_round_shift(Ptr<short> @in, int stride, Span<__m128i> @out, int bit)
    {
        @out[7] = load_16bit_to_16bit(@in + 0 * stride);
        @out[6] = load_16bit_to_16bit(@in + 1 * stride);
        @out[5] = load_16bit_to_16bit(@in + 2 * stride);
        @out[4] = load_16bit_to_16bit(@in + 3 * stride);
        @out[3] = load_16bit_to_16bit(@in + 4 * stride);
        @out[2] = load_16bit_to_16bit(@in + 5 * stride);
        @out[1] = load_16bit_to_16bit(@in + 6 * stride);
        @out[0] = load_16bit_to_16bit(@in + 7 * stride);
        @out[7] = _mm_slli_epi16(@out[7], bit);
        @out[6] = _mm_slli_epi16(@out[6], bit);
        @out[5] = _mm_slli_epi16(@out[5], bit);
        @out[4] = _mm_slli_epi16(@out[4], bit);
        @out[3] = _mm_slli_epi16(@out[3], bit);
        @out[2] = _mm_slli_epi16(@out[2], bit);
        @out[1] = _mm_slli_epi16(@out[1], bit);
        @out[0] = _mm_slli_epi16(@out[0], bit);
    }

    // [port] TRANSPOSE_8X8_AVX2() is a statement macro reading b0..b3 and writing c0..c3 in the
    // caller's scope: a function taking b0..b3 and returning c0..c3 through out parameters.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TRANSPOSE_8X8_AVX2(__m256i b0, __m256i b1, __m256i b2, __m256i b3,
                                           out __m256i c0, out __m256i c1, out __m256i c2, out __m256i c3)
    {
        // aa0:    00 10 01 11  02 12 03 13 | 40 50 41 51  42 52 43 53
        // aa1:    04 14 05 15  06 16 07 17 | 44 54 45 55  46 56 47 57
        // aa2:    20 30 21 31  22 32 23 33 | 60 70 61 71  62 72 63 73
        // aa3:    24 34 25 35  26 36 27 37 | 64 74 65 75  66 76 67 77
        __m256i aa0 = _mm256_unpacklo_epi16(b0, b1);
        __m256i aa1 = _mm256_unpackhi_epi16(b0, b1);
        __m256i aa2 = _mm256_unpacklo_epi16(b2, b3);
        __m256i aa3 = _mm256_unpackhi_epi16(b2, b3);
        // Unpack 32 bit elements resulting in:
        // bb0: 00 10 20 30  01 11 21 31 | 40 50 60 70  41 51 61 71
        // bb1: 02 12 22 32  03 13 23 33 | 42 52 62 72  43 53 63 73
        // bb2: 04 14 24 34  05 15 25 35 | 44 54 64 74  45 55 65 75
        // bb2: 06 16 26 36  07 17 27 37 | 46 56 66 76  47 57 67 77
        __m256i bb0 = _mm256_unpacklo_epi32(aa0, aa2);
        __m256i bb1 = _mm256_unpackhi_epi32(aa0, aa2);
        __m256i bb2 = _mm256_unpacklo_epi32(aa1, aa3);
        __m256i bb3 = _mm256_unpackhi_epi32(aa1, aa3);
        // bb0: 00 10 20 30 40 50 60 70| 01 11 21 31 41 51 61 71
        // bb1: 02 12 22 32 42 52 62 72| 03 13 23 33 43 53 63 73
        // bb2: 04 14 24 34 44 54 64 74| 05 15 25 35 45 55 65 75
        // bb2: 06 16 26 36 46 56 66 76| 07 17 27 37 47 57 67 77
        c0 = _mm256_permute4x64_epi64(bb0, 0xd8);
        c1 = _mm256_permute4x64_epi64(bb1, 0xd8);
        c2 = _mm256_permute4x64_epi64(bb2, 0xd8);
        c3 = _mm256_permute4x64_epi64(bb3, 0xd8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void transpose_round_shift_flip_8x8(Span<__m128i> @in, Span<__m128i> @out, int bit)
    {
        __m256i c0, c1, c2, c3;
        bit = -bit;
        __m256i rounding = _mm256_set1_epi16(1 << (bit - 1));
        __m256i s04 =
                _mm256_insertf128_si256(_mm256_castsi128_si256(@in[0]), @in[4], 0x1);
        __m256i s15 =
                _mm256_insertf128_si256(_mm256_castsi128_si256(@in[1]), @in[5], 0x1);
        __m256i s26 =
                _mm256_insertf128_si256(_mm256_castsi128_si256(@in[2]), @in[6], 0x1);
        __m256i s37 =
                _mm256_insertf128_si256(_mm256_castsi128_si256(@in[3]), @in[7], 0x1);

        __m256i a0 = _mm256_adds_epi16(s04, rounding);
        __m256i a1 = _mm256_adds_epi16(s15, rounding);
        __m256i a2 = _mm256_adds_epi16(s26, rounding);
        __m256i a3 = _mm256_adds_epi16(s37, rounding);

        // b0: 00 01 02 03  04 05 06 07 | 40 41 42 43  44 45 46 47
        // b1: 10 11 12 13  14 15 16 17 | 50 51 52 53  54 55 56 57
        // b2: 20 21 22 23  24 25 26 27 | 60 61 62 63  64 65 66 67
        // b3: 30 31 32 33  34 35 36 37 | 70 71 72 73  74 75 76 77
        __m256i b0 = _mm256_srai_epi16(a0, bit);
        __m256i b1 = _mm256_srai_epi16(a1, bit);
        __m256i b2 = _mm256_srai_epi16(a2, bit);
        __m256i b3 = _mm256_srai_epi16(a3, bit);

        TRANSPOSE_8X8_AVX2(b0, b1, b2, b3, out c0, out c1, out c2, out c3);

        // Unpack 64 bit elements resulting in:
        // out[7]: 00 10 20 30  40 50 60 70
        // out[6]: 01 11 21 31  41 51 61 71
        // out[5]: 02 12 22 32  42 52 62 72
        // out[4]: 03 13 23 33  43 53 63 73
        // out[3]: 04 14 24 34  44 54 64 74
        // out[2]: 05 15 25 35  45 55 65 75
        // out[1]: 06 16 26 36  46 56 66 76
        // out[0]: 07 17 27 37  47 57 67 77
        @out[7] = _mm256_castsi256_si128(c0);
        @out[6] = _mm256_extractf128_si256(c0, 1);
        @out[5] = _mm256_castsi256_si128(c1);
        @out[4] = _mm256_extractf128_si256(c1, 1);
        @out[3] = _mm256_castsi256_si128(c2);
        @out[2] = _mm256_extractf128_si256(c2, 1);
        @out[1] = _mm256_castsi256_si128(c3);
        @out[0] = _mm256_extractf128_si256(c3, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void transpose_round_shift_8x8(Span<__m128i> @in, Span<__m128i> @out, int bit)
    {
        __m256i c0, c1, c2, c3;
        bit = -bit;
        __m256i rounding = _mm256_set1_epi16(1 << (bit - 1));
        __m256i s04 =
                _mm256_insertf128_si256(_mm256_castsi128_si256(@in[0]), @in[4], 0x1);
        __m256i s15 =
                _mm256_insertf128_si256(_mm256_castsi128_si256(@in[1]), @in[5], 0x1);
        __m256i s26 =
                _mm256_insertf128_si256(_mm256_castsi128_si256(@in[2]), @in[6], 0x1);
        __m256i s37 =
                _mm256_insertf128_si256(_mm256_castsi128_si256(@in[3]), @in[7], 0x1);

        __m256i a0 = _mm256_adds_epi16(s04, rounding);
        __m256i a1 = _mm256_adds_epi16(s15, rounding);
        __m256i a2 = _mm256_adds_epi16(s26, rounding);
        __m256i a3 = _mm256_adds_epi16(s37, rounding);

        // b0: 00 01 02 03  04 05 06 07 | 40 41 42 43  44 45 46 47
        // b1: 10 11 12 13  14 15 16 17 | 50 51 52 53  54 55 56 57
        // b2: 20 21 22 23  24 25 26 27 | 60 61 62 63  64 65 66 67
        // b3: 30 31 32 33  34 35 36 37 | 70 71 72 73  74 75 76 77
        __m256i b0 = _mm256_srai_epi16(a0, bit);
        __m256i b1 = _mm256_srai_epi16(a1, bit);
        __m256i b2 = _mm256_srai_epi16(a2, bit);
        __m256i b3 = _mm256_srai_epi16(a3, bit);

        TRANSPOSE_8X8_AVX2(b0, b1, b2, b3, out c0, out c1, out c2, out c3);
        // Unpack 64 bit elements resulting in:
        // out[7]: 00 10 20 30  40 50 60 70
        // out[6]: 01 11 21 31  41 51 61 71
        // out[5]: 02 12 22 32  42 52 62 72
        // out[4]: 03 13 23 33  43 53 63 73
        // out[3]: 04 14 24 34  44 54 64 74
        // out[2]: 05 15 25 35  45 55 65 75
        // out[1]: 06 16 26 36  46 56 66 76
        // out[0]: 07 17 27 37  47 57 67 77
        @out[0] = _mm256_castsi256_si128(c0);
        @out[1] = _mm256_extractf128_si256(c0, 1);
        @out[2] = _mm256_castsi256_si128(c1);
        @out[3] = _mm256_extractf128_si256(c1, 1);
        @out[4] = _mm256_castsi256_si128(c2);
        @out[5] = _mm256_extractf128_si256(c2, 1);
        @out[6] = _mm256_castsi256_si128(c3);
        @out[7] = _mm256_extractf128_si256(c3, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void store_buffer_16bit_to_32bit_w8_avx2(Span<__m128i> @in, Ptr<int> @out, int stride, int out_size)
    {
        for (int i = 0; i < out_size; ++i)
        {
            _mm256_store_si256((@out + i * stride),
                                                  _mm256_cvtepi16_epi32(@in[i]));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void lowbd_fwd_txfm2d_8x8_non_dct_avx2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        _ = bd;
        Span<__m128i> buf0 = stackalloc __m128i[8];
        Span<__m128i> buf1 = stackalloc __m128i[8];
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[TX_8X8];

        int ud_flip = 0, lr_flip = 0;
        get_flip_cfg(tx_type, ref ud_flip, ref lr_flip);
        Debug.Assert(shift[0] > 0);
        if (ud_flip != 0)
            load_buffer_and_flip_round_shift(input, stride, buf0, shift[0]);
        else
            load_buffer_and_round_shift(input, stride, buf0, shift[0]);

        switch (vtx_tab[tx_type])
        {
            case DCT_1D: fdct8x8_new_sse2(buf0, buf0, 13); break;
            case ADST_1D:
            case FLIPADST_1D: fadst8x8_new_sse2(buf0, buf0, 13); break;
            default: fidentity8x8_new_sse2(buf0, buf0, 13); break;
        }
        Debug.Assert(shift[1] < 0);

        if (lr_flip != 0)
        {
            transpose_round_shift_flip_8x8(buf0, buf1, shift[1]);
        }
        else
        {
            transpose_round_shift_8x8(buf0, buf1, shift[1]);
        }

        switch (htx_tab[tx_type])
        {
            case DCT_1D: fdct8x8_new_sse2(buf1, buf1, 13); break;
            case ADST_1D:
            case FLIPADST_1D: fadst8x8_new_sse2(buf1, buf1, 13); break;
            default: fidentity8x8_new_sse2(buf1, buf1, 13); break;
        }
        Debug.Assert(shift[2] == 0);
        store_buffer_16bit_to_32bit_w8_avx2(buf1, output, 8, 8);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void av1_lowbd_fwd_txfm2d_8x8_avx2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        if ((tx_type != DCT_DCT))
        {
            lowbd_fwd_txfm2d_8x8_non_dct_avx2(input, output, stride, tx_type, bd);
            return;
        }
        _ = bd;
        Span<__m128i> buf0 = stackalloc __m128i[8];
        Span<__m128i> buf1 = stackalloc __m128i[8];
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[TX_8X8];
        load_buffer_and_round_shift(input, stride, buf0, shift[0]);
        fdct8x8_new_sse2(buf0, buf0, 13);
        transpose_round_shift_8x8(buf0, buf1, shift[1]);
        fdct8x8_new_sse2(buf1, buf1, 13);
        store_buffer_16bit_to_32bit_w8_avx2(buf1, output, 8, 8);
    }

    // [port] C stack array, uninitialised: per-thread scratch instead of a zeroed stackalloc.
    [ThreadStatic] private static __m256i[] t_lowbd_fwd_txfm2d_16x16_non_dct_avx2_buf0;
    [ThreadStatic] private static __m256i[] t_lowbd_fwd_txfm2d_16x16_non_dct_avx2_buf1;
    [MethodImpl(MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void lowbd_fwd_txfm2d_16x16_non_dct_avx2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        _ = bd;
        TX_SIZE tx_size = TX_16X16;
        Span<__m256i> buf0 = t_lowbd_fwd_txfm2d_16x16_non_dct_avx2_buf0 ??= new __m256i[16];
        Span<__m256i> buf1 = t_lowbd_fwd_txfm2d_16x16_non_dct_avx2_buf1 ??= new __m256i[16];
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[tx_size];
        int width = tx_size_wide[tx_size];
        int height = tx_size_high[tx_size];
        int txw_idx = get_txw_idx(tx_size);
        int txh_idx = get_txh_idx(tx_size);
        int cos_bit_col = av1_fwd_cos_bit_col[txw_idx][txh_idx];
        int cos_bit_row = av1_fwd_cos_bit_row[txw_idx][txh_idx];
        transform_1d_avx2 col_txfm = col_txfm16x16_arr[tx_type];
        transform_1d_avx2 row_txfm = row_txfm16x16_cb12_arr[tx_type];
        int ud_flip = 0, lr_flip = 0;

        get_flip_cfg(tx_type, ref ud_flip, ref lr_flip);
        int i = 0;
        if (ud_flip != 0)
        {
            load_buffer_16bit_to_16bit_flip_avx2(input + 16 * i, stride, buf0, height);
        }
        else
        {
            load_buffer_16bit_to_16bit_avx2(input + 16 * i, stride, buf0, height);
        }
        round_shift_16bit_w16_avx2(buf0, height, shift[0]);
        col_txfm(buf0, buf0, (sbyte)cos_bit_col);
        round_shift_16bit_w16_avx2(buf0, height, shift[1]);
        transpose_16bit_16x16_avx2(buf0, buf1.Slice(0 * width + 16 * i));

        scoped Span<__m256i> buf;
        if (lr_flip != 0)
        {
            buf = buf0;
            flip_buf_avx2(buf1.Slice(width * i), buf, width);
        }
        else
        {
            buf = buf1.Slice(width * i);
        }
        row_txfm(buf, buf, (sbyte)cos_bit_row);
        round_shift_16bit_w16_avx2(buf, width, shift[2]);
        store_buffer_16bit_to_32bit_w16_avx2(buf, output + i * 16, height, width);
    }

    // [port] C stack array, uninitialised: per-thread scratch instead of a zeroed stackalloc.
    [ThreadStatic] private static __m256i[] t_lowbd_fwd_txfm2d_16x16_avx2_buf0;
    [ThreadStatic] private static __m256i[] t_lowbd_fwd_txfm2d_16x16_avx2_buf1;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void lowbd_fwd_txfm2d_16x16_avx2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        if ((tx_type != DCT_DCT))
        {
            lowbd_fwd_txfm2d_16x16_non_dct_avx2(input, output, stride, tx_type, bd);
            return;
        }
        _ = bd;
        TX_SIZE tx_size = TX_16X16;
        Span<__m256i> buf0 = t_lowbd_fwd_txfm2d_16x16_avx2_buf0 ??= new __m256i[16];
        Span<__m256i> buf1 = t_lowbd_fwd_txfm2d_16x16_avx2_buf1 ??= new __m256i[16];
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[tx_size];
        int width = tx_size_wide[tx_size];
        int height = tx_size_high[tx_size];

        load_buffer_16bit_to_16bit_avx2(input, stride, buf0, height);
        round_shift_16bit_w16_avx2(buf0, height, shift[0]);
        fdct16x16_cb13_avx2(buf0, buf0, 13);
        round_shift_16bit_w16_avx2(buf0, height, shift[1]);
        transpose_16bit_16x16_avx2(buf0, buf1);
        fdct16x16_cb12_avx2(buf1, buf1, 12);
        store_buffer_16bit_to_32bit_w16_avx2(buf1, output, height, width);
    }

    // [port] C stack array, uninitialised: per-thread scratch instead of a zeroed stackalloc.
    [ThreadStatic] private static __m256i[] t_lowbd_fwd_txfm2d_32x32_avx2_buf0;
    [ThreadStatic] private static __m256i[] t_lowbd_fwd_txfm2d_32x32_avx2_buf1;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void lowbd_fwd_txfm2d_32x32_avx2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        _ = bd;
        TX_SIZE tx_size = TX_32X32;
        Span<__m256i> buf0 = t_lowbd_fwd_txfm2d_32x32_avx2_buf0 ??= new __m256i[32];
        Span<__m256i> buf1 = t_lowbd_fwd_txfm2d_32x32_avx2_buf1 ??= new __m256i[64];
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[tx_size];
        int width = tx_size_wide[tx_size];
        int height = tx_size_high[tx_size];

        // Fast path for DCT_DCT: inline 32x32 DCT with constant cos_bit (12).
        if (tx_type == DCT_DCT)
        {
            for (int i = 0; i < 2; i++)
            {
                load_buffer_16bit_to_16bit_avx2(input + 16 * i, stride, buf0, height);
                round_shift_16bit_w16_avx2(buf0, height, shift[0]);
                fdct16x32_cb12_avx2(buf0, buf0, 12);
                round_shift_16bit_w16_avx2(buf0, height, shift[1]);
                transpose_16bit_16x16_avx2(buf0.Slice(0 * 16), buf1.Slice(0 * width + 16 * i));
                transpose_16bit_16x16_avx2(buf0.Slice(1 * 16), buf1.Slice(1 * width + 16 * i));
            }
            for (int i = 0; i < 2; i++)
            {
                Span<__m256i> buf = buf1.Slice(width * i);
                fdct16x32_cb12_avx2(buf, buf, 12);
                store_buffer_16bit_to_32bit_w16_avx2(buf, output + i * 16, height, width);
            }
            return;
        }

        int txw_idx = get_txw_idx(tx_size);
        int txh_idx = get_txh_idx(tx_size);
        int cos_bit_col = av1_fwd_cos_bit_col[txw_idx][txh_idx];
        int cos_bit_row = av1_fwd_cos_bit_row[txw_idx][txh_idx];
        transform_1d_avx2 col_txfm = col_txfm16x32_arr[tx_type];
        transform_1d_avx2 row_txfm = row_txfm16x32_cb12_arr[tx_type];

        int ud_flip = 0, lr_flip = 0;
        get_flip_cfg(tx_type, ref ud_flip, ref lr_flip);

        for (int i = 0; i < 2; i++)
        {
            if (ud_flip != 0)
            {
                load_buffer_16bit_to_16bit_flip_avx2(input + 16 * i, stride, buf0,
                                                                                          height);
            }
            else
            {
                load_buffer_16bit_to_16bit_avx2(input + 16 * i, stride, buf0, height);
            }
            round_shift_16bit_w16_avx2(buf0, height, shift[0]);
            col_txfm(buf0, buf0, (sbyte)cos_bit_col);
            round_shift_16bit_w16_avx2(buf0, height, shift[1]);
            transpose_16bit_16x16_avx2(buf0.Slice(0 * 16), buf1.Slice(0 * width + 16 * i));
            transpose_16bit_16x16_avx2(buf0.Slice(1 * 16), buf1.Slice(1 * width + 16 * i));
        }

        for (int i = 0; i < 2; i++)
        {
            scoped Span<__m256i> buf;
            if (lr_flip != 0)
            {
                buf = buf0;
                flip_buf_avx2(buf1.Slice(width * i), buf, width);
            }
            else
            {
                buf = buf1.Slice(width * i);
            }
            row_txfm(buf, buf, (sbyte)cos_bit_row);
            round_shift_16bit_w16_avx2(buf, width, shift[2]);
            store_buffer_16bit_to_32bit_w16_avx2(buf, output + i * 16, height, width);
        }
    }

    // [port] C stack array, uninitialised: per-thread scratch instead of a zeroed stackalloc.
    [ThreadStatic] private static __m256i[] t_lowbd_fwd_txfm2d_16x32_avx2_buf0;
    [ThreadStatic] private static __m256i[] t_lowbd_fwd_txfm2d_16x32_avx2_buf1;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void lowbd_fwd_txfm2d_16x32_avx2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        _ = bd;
        TX_SIZE tx_size = TX_16X32;
        Span<__m256i> buf0 = t_lowbd_fwd_txfm2d_16x32_avx2_buf0 ??= new __m256i[32];
        Span<__m256i> buf1 = t_lowbd_fwd_txfm2d_16x32_avx2_buf1 ??= new __m256i[32];
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[tx_size];
        int txw_idx = get_txw_idx(tx_size);
        int txh_idx = get_txh_idx(tx_size);
        int cos_bit_col = av1_fwd_cos_bit_col[txw_idx][txh_idx];
        int cos_bit_row = av1_fwd_cos_bit_row[txw_idx][txh_idx];
        int width = tx_size_wide[tx_size];
        int height = tx_size_high[tx_size];
        transform_1d_avx2 col_txfm = col_txfm16x32_arr[tx_type];
        transform_1d_avx2 row_txfm = row_txfm16x16_cb13_arr[tx_type];

        int ud_flip = 0, lr_flip = 0;
        get_flip_cfg(tx_type, ref ud_flip, ref lr_flip);

        if (ud_flip != 0)
        {
            load_buffer_16bit_to_16bit_flip_avx2(input, stride, buf0, height);
        }
        else
        {
            load_buffer_16bit_to_16bit_avx2(input, stride, buf0, height);
        }
        round_shift_16bit_w16_avx2(buf0, height, shift[0]);
        col_txfm(buf0, buf0, (sbyte)cos_bit_col);
        round_shift_16bit_w16_avx2(buf0, height, shift[1]);
        transpose_16bit_16x16_avx2(buf0, buf1);
        transpose_16bit_16x16_avx2(buf0.Slice(16), buf1.Slice(16));

        for (int i = 0; i < 2; i++)
        {
            scoped Span<__m256i> buf;
            if (lr_flip != 0)
            {
                buf = buf0;
                flip_buf_avx2(buf1.Slice(width * i), buf, width);
            }
            else
            {
                buf = buf1.Slice(width * i);
            }
            row_txfm(buf, buf, (sbyte)cos_bit_row);
            round_shift_16bit_w16_avx2(buf, width, shift[2]);
            store_rect_buffer_16bit_to_32bit_w16_avx2(buf, output + i * 16, height,
                                                                                                width);
        }
    }

    // [port] C stack array, uninitialised: per-thread scratch instead of a zeroed stackalloc.
    [ThreadStatic] private static __m256i[] t_lowbd_fwd_txfm2d_32x16_avx2_buf0;
    [ThreadStatic] private static __m256i[] t_lowbd_fwd_txfm2d_32x16_avx2_buf1;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void lowbd_fwd_txfm2d_32x16_avx2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        _ = bd;
        Span<__m256i> buf0 = t_lowbd_fwd_txfm2d_32x16_avx2_buf0 ??= new __m256i[32];
        Span<__m256i> buf1 = t_lowbd_fwd_txfm2d_32x16_avx2_buf1 ??= new __m256i[64];
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[TX_32X16];
        int txw_idx = get_txw_idx(TX_32X16);
        int txh_idx = get_txh_idx(TX_32X16);
        int cos_bit_col = av1_fwd_cos_bit_col[txw_idx][txh_idx];
        int cos_bit_row = av1_fwd_cos_bit_row[txw_idx][txh_idx];
        int width = 32;
        int height = 16;
        transform_1d_avx2 col_txfm = col_txfm16x16_arr[tx_type];
        transform_1d_avx2 row_txfm = row_txfm16x32_cb13_arr[tx_type];

        int ud_flip = 0, lr_flip = 0;
        get_flip_cfg(tx_type, ref ud_flip, ref lr_flip);

        for (int i = 0; i < 2; i++)
        {
            if (ud_flip != 0)
            {
                load_buffer_16bit_to_16bit_flip_avx2(input + 16 * i, stride, buf0,
                                                                                          height);
            }
            else
            {
                load_buffer_16bit_to_16bit_avx2(input + 16 * i, stride, buf0, height);
            }
            round_shift_16bit_w16_avx2(buf0, height, shift[0]);
            col_txfm(buf0, buf0, (sbyte)cos_bit_col);
            round_shift_16bit_w16_avx2(buf0, height, shift[1]);
            transpose_16bit_16x16_avx2(buf0, buf1.Slice(0 * width + 16 * i));
        }

        scoped Span<__m256i> buf;
        if (lr_flip != 0)
        {
            buf = buf0;
            flip_buf_avx2(buf1, buf, width);
        }
        else
        {
            buf = buf1;
        }
        row_txfm(buf, buf, (sbyte)cos_bit_row);
        round_shift_16bit_w16_avx2(buf, width, shift[2]);
        store_rect_buffer_16bit_to_32bit_w16_avx2(buf, output, height, width);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void btf_16_avx2(ref __m256i w0, ref __m256i w1, ref __m256i in0, ref __m256i in1, out __m128i out0, out __m128i out1, out __m128i out2, out __m128i out3, in __m256i __rounding, ref sbyte cos_bit)
    {
        __m256i t0 = _mm256_unpacklo_epi16(in0, in1);
        __m256i t1 = _mm256_unpackhi_epi16(in0, in1);
        __m256i u0 = _mm256_madd_epi16(t0, w0);
        __m256i u1 = _mm256_madd_epi16(t1, w0);
        __m256i v0 = _mm256_madd_epi16(t0, w1);
        __m256i v1 = _mm256_madd_epi16(t1, w1);

        __m256i a0 = _mm256_add_epi32(u0, __rounding);
        __m256i a1 = _mm256_add_epi32(u1, __rounding);
        __m256i b0 = _mm256_add_epi32(v0, __rounding);
        __m256i b1 = _mm256_add_epi32(v1, __rounding);

        __m256i c0 = _mm256_srai_epi32(a0, cos_bit);
        __m256i c1 = _mm256_srai_epi32(a1, cos_bit);
        __m256i d0 = _mm256_srai_epi32(b0, cos_bit);
        __m256i d1 = _mm256_srai_epi32(b1, cos_bit);

        __m256i temp0 = _mm256_packs_epi32(c0, c1);
        __m256i temp1 = _mm256_packs_epi32(d0, d1);

        out0 = _mm256_castsi256_si128(temp0);
        out1 = _mm256_castsi256_si128(temp1);
        out2 = _mm256_extracti128_si256(temp0, 0x01);
        out3 = _mm256_extracti128_si256(temp1, 0x01);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fdct8x8_new_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit_arg)
    {
        _ = cos_bit_arg;
        sbyte cos_bit = 13;
        Ptr<int> cospi = cospi_arr(cos_bit);
        __m256i __rounding = _mm256_set1_epi32(1 << (cos_bit - 1));

        __m256i cospi_m32_p32 = pair_set_w16_epi16(-cospi[32], cospi[32]);
        __m256i cospi_p32_p32 = pair_set_w16_epi16(cospi[32], cospi[32]);
        __m256i cospi_p32_m32 = pair_set_w16_epi16(cospi[32], -cospi[32]);
        __m256i cospi_p48_p16 = pair_set_w16_epi16(cospi[48], cospi[16]);
        __m256i cospi_m16_p48 = pair_set_w16_epi16(-cospi[16], cospi[48]);
        __m256i cospi_p56_p08 = pair_set_w16_epi16(cospi[56], cospi[8]);
        __m256i cospi_m08_p56 = pair_set_w16_epi16(-cospi[8], cospi[56]);
        __m256i cospi_p24_p40 = pair_set_w16_epi16(cospi[24], cospi[40]);
        __m256i cospi_m40_p24 = pair_set_w16_epi16(-cospi[40], cospi[24]);

        // stage 1
        Span<__m256i> x1 = stackalloc __m256i[8];
        x1[0] = _mm256_adds_epi16(input[0], input[7]);
        x1[7] = _mm256_subs_epi16(input[0], input[7]);
        x1[1] = _mm256_adds_epi16(input[1], input[6]);
        x1[6] = _mm256_subs_epi16(input[1], input[6]);
        x1[2] = _mm256_adds_epi16(input[2], input[5]);
        x1[5] = _mm256_subs_epi16(input[2], input[5]);
        x1[3] = _mm256_adds_epi16(input[3], input[4]);
        x1[4] = _mm256_subs_epi16(input[3], input[4]);

        // stage 2
        Span<__m256i> x2 = stackalloc __m256i[8];
        x2[0] = _mm256_adds_epi16(x1[0], x1[3]);
        x2[3] = _mm256_subs_epi16(x1[0], x1[3]);
        x2[1] = _mm256_adds_epi16(x1[1], x1[2]);
        x2[2] = _mm256_subs_epi16(x1[1], x1[2]);
        x2[4] = x1[4];
        btf_16_w16_avx2(cospi_m32_p32, cospi_p32_p32, ref x1[5], ref x1[6], __rounding,
                                        cos_bit);
        x2[5] = x1[5];
        x2[6] = x1[6];
        x2[7] = x1[7];

        // stage 3
        Span<__m256i> x3 = stackalloc __m256i[8];
        btf_16_w16_avx2(cospi_p32_p32, cospi_p32_m32, ref x2[0], ref x2[1], __rounding,
                                        cos_bit);
        x3[0] = x2[0];
        x3[1] = x2[1];
        btf_16_w16_avx2(cospi_p48_p16, cospi_m16_p48, ref x2[2], ref x2[3], __rounding,
                                        cos_bit);
        x3[2] = x2[2];
        x3[3] = x2[3];
        x3[4] = _mm256_adds_epi16(x2[4], x2[5]);
        x3[5] = _mm256_subs_epi16(x2[4], x2[5]);
        x3[6] = _mm256_subs_epi16(x2[7], x2[6]);
        x3[7] = _mm256_adds_epi16(x2[7], x2[6]);

        // stage 4
        Span<__m256i> x4 = stackalloc __m256i[8];
        x4[0] = x3[0];
        x4[1] = x3[1];
        x4[2] = x3[2];
        x4[3] = x3[3];
        btf_16_w16_avx2(cospi_p56_p08, cospi_m08_p56, ref x3[4], ref x3[7], __rounding,
                                        cos_bit);
        x4[4] = x3[4];
        x4[7] = x3[7];
        btf_16_w16_avx2(cospi_p24_p40, cospi_m40_p24, ref x3[5], ref x3[6], __rounding,
                                        cos_bit);
        x4[5] = x3[5];
        x4[6] = x3[6];
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fadst8x8_new_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit_arg)
    {
        _ = cos_bit_arg;
        sbyte cos_bit = 13;
        Ptr<int> cospi = cospi_arr(cos_bit);
        __m256i __zero = _mm256_setzero_si256();
        __m256i __rounding = _mm256_set1_epi32(1 << (cos_bit - 1));

        __m256i cospi_p32_p32 = pair_set_w16_epi16(cospi[32], cospi[32]);
        __m256i cospi_p32_m32 = pair_set_w16_epi16(cospi[32], -cospi[32]);
        __m256i cospi_p16_p48 = pair_set_w16_epi16(cospi[16], cospi[48]);
        __m256i cospi_p48_m16 = pair_set_w16_epi16(cospi[48], -cospi[16]);
        __m256i cospi_m48_p16 = pair_set_w16_epi16(-cospi[48], cospi[16]);
        __m256i cospi_p04_p60 = pair_set_w16_epi16(cospi[4], cospi[60]);
        __m256i cospi_p60_m04 = pair_set_w16_epi16(cospi[60], -cospi[4]);
        __m256i cospi_p20_p44 = pair_set_w16_epi16(cospi[20], cospi[44]);
        __m256i cospi_p44_m20 = pair_set_w16_epi16(cospi[44], -cospi[20]);
        __m256i cospi_p36_p28 = pair_set_w16_epi16(cospi[36], cospi[28]);
        __m256i cospi_p28_m36 = pair_set_w16_epi16(cospi[28], -cospi[36]);
        __m256i cospi_p52_p12 = pair_set_w16_epi16(cospi[52], cospi[12]);
        __m256i cospi_p12_m52 = pair_set_w16_epi16(cospi[12], -cospi[52]);

        // stage 1
        Span<__m256i> x1 = stackalloc __m256i[8];
        x1[0] = input[0];
        x1[1] = _mm256_subs_epi16(__zero, input[7]);
        x1[2] = _mm256_subs_epi16(__zero, input[3]);
        x1[3] = input[4];
        x1[4] = _mm256_subs_epi16(__zero, input[1]);
        x1[5] = input[6];
        x1[6] = input[2];
        x1[7] = _mm256_subs_epi16(__zero, input[5]);

        // stage 2
        Span<__m256i> x2 = stackalloc __m256i[8];
        x2[0] = x1[0];
        x2[1] = x1[1];
        btf_16_w16_avx2(cospi_p32_p32, cospi_p32_m32, ref x1[2], ref x1[3], __rounding,
                                        cos_bit);
        x2[2] = x1[2];
        x2[3] = x1[3];
        x2[4] = x1[4];
        x2[5] = x1[5];
        btf_16_w16_avx2(cospi_p32_p32, cospi_p32_m32, ref x1[6], ref x1[7], __rounding,
                                        cos_bit);
        x2[6] = x1[6];
        x2[7] = x1[7];

        // stage 3
        Span<__m256i> x3 = stackalloc __m256i[8];
        x3[0] = _mm256_adds_epi16(x2[0], x2[2]);
        x3[2] = _mm256_subs_epi16(x2[0], x2[2]);
        x3[1] = _mm256_adds_epi16(x2[1], x2[3]);
        x3[3] = _mm256_subs_epi16(x2[1], x2[3]);
        x3[4] = _mm256_adds_epi16(x2[4], x2[6]);
        x3[6] = _mm256_subs_epi16(x2[4], x2[6]);
        x3[5] = _mm256_adds_epi16(x2[5], x2[7]);
        x3[7] = _mm256_subs_epi16(x2[5], x2[7]);

        // stage 4
        Span<__m256i> x4 = stackalloc __m256i[8];
        x4[0] = x3[0];
        x4[1] = x3[1];
        x4[2] = x3[2];
        x4[3] = x3[3];
        btf_16_w16_avx2(cospi_p16_p48, cospi_p48_m16, ref x3[4], ref x3[5], __rounding,
                                        cos_bit);
        x4[4] = x3[4];
        x4[5] = x3[5];
        btf_16_w16_avx2(cospi_m48_p16, cospi_p16_p48, ref x3[6], ref x3[7], __rounding,
                                        cos_bit);
        x4[6] = x3[6];
        x4[7] = x3[7];

        // stage 5
        Span<__m256i> x5 = stackalloc __m256i[8];
        x5[0] = _mm256_adds_epi16(x4[0], x4[4]);
        x5[4] = _mm256_subs_epi16(x4[0], x4[4]);
        x5[1] = _mm256_adds_epi16(x4[1], x4[5]);
        x5[5] = _mm256_subs_epi16(x4[1], x4[5]);
        x5[2] = _mm256_adds_epi16(x4[2], x4[6]);
        x5[6] = _mm256_subs_epi16(x4[2], x4[6]);
        x5[3] = _mm256_adds_epi16(x4[3], x4[7]);
        x5[7] = _mm256_subs_epi16(x4[3], x4[7]);

        // stage 6
        Span<__m256i> x6 = stackalloc __m256i[8];
        btf_16_w16_avx2(cospi_p04_p60, cospi_p60_m04, ref x5[0], ref x5[1], __rounding,
                                        cos_bit);
        x6[0] = x5[0];
        x6[1] = x5[1];
        btf_16_w16_avx2(cospi_p20_p44, cospi_p44_m20, ref x5[2], ref x5[3], __rounding,
                                        cos_bit);
        x6[2] = x5[2];
        x6[3] = x5[3];
        btf_16_w16_avx2(cospi_p36_p28, cospi_p28_m36, ref x5[4], ref x5[5], __rounding,
                                        cos_bit);
        x6[4] = x5[4];
        x6[5] = x5[5];
        btf_16_w16_avx2(cospi_p52_p12, cospi_p12_m52, ref x5[6], ref x5[7], __rounding,
                                        cos_bit);
        x6[6] = x5[6];
        x6[7] = x5[7];

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fidentity8x8_new_avx2(Span<__m256i> input, Span<__m256i> output, sbyte cos_bit)
    {
        _ = cos_bit;

        output[0] = _mm256_adds_epi16(input[0], input[0]);
        output[1] = _mm256_adds_epi16(input[1], input[1]);
        output[2] = _mm256_adds_epi16(input[2], input[2]);
        output[3] = _mm256_adds_epi16(input[3], input[3]);
        output[4] = _mm256_adds_epi16(input[4], input[4]);
        output[5] = _mm256_adds_epi16(input[5], input[5]);
        output[6] = _mm256_adds_epi16(input[6], input[6]);
        output[7] = _mm256_adds_epi16(input[7], input[7]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fdct8x16_new_avx2(Span<__m128i> input, Span<__m128i> output, sbyte cos_bit_arg)
    {
        _ = cos_bit_arg;
        sbyte cos_bit = 13;
        Ptr<int> cospi = av1_txfm.cospi_arr(cos_bit);  // [port] qualified: the local cospi_arr below hides the function
        __m256i __rounding_256 = _mm256_set1_epi32(1 << (cos_bit - 1));
        __m128i __rounding = _mm_set1_epi32(1 << (cos_bit - 1));
        __m128i temp0, temp1, temp2, temp3;
        __m256i in0, in1;
        __m128i cospi_m32_p32 = pair_set_epi16(-cospi[32], cospi[32]);
        __m128i cospi_p32_p32 = pair_set_epi16(cospi[32], cospi[32]);
        __m128i cospi_p32_m32 = pair_set_epi16(cospi[32], -cospi[32]);
        __m128i cospi_p48_p16 = pair_set_epi16(cospi[48], cospi[16]);
        __m128i cospi_m16_p48 = pair_set_epi16(-cospi[16], cospi[48]);
        __m128i cospi_m48_m16 = pair_set_epi16(-cospi[48], -cospi[16]);
        __m128i cospi_p56_p08 = pair_set_epi16(cospi[56], cospi[8]);
        __m128i cospi_m08_p56 = pair_set_epi16(-cospi[8], cospi[56]);
        __m128i cospi_p24_p40 = pair_set_epi16(cospi[24], cospi[40]);
        __m128i cospi_m40_p24 = pair_set_epi16(-cospi[40], cospi[24]);
        __m128i cospi_p60_p04 = pair_set_epi16(cospi[60], cospi[4]);
        __m128i cospi_m04_p60 = pair_set_epi16(-cospi[4], cospi[60]);
        __m128i cospi_p28_p36 = pair_set_epi16(cospi[28], cospi[36]);
        __m128i cospi_m36_p28 = pair_set_epi16(-cospi[36], cospi[28]);
        __m128i cospi_p44_p20 = pair_set_epi16(cospi[44], cospi[20]);
        __m128i cospi_m20_p44 = pair_set_epi16(-cospi[20], cospi[44]);
        __m128i cospi_p12_p52 = pair_set_epi16(cospi[12], cospi[52]);
        __m128i cospi_m52_p12 = pair_set_epi16(-cospi[52], cospi[12]);

        Span<__m256i> cospi_arr = stackalloc __m256i[12];

        cospi_arr[0] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_m32_p32),
                                                                                      cospi_m32_p32, 0x1);
        cospi_arr[1] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p32_p32),
                                                                                      cospi_p32_p32, 0x1);
        cospi_arr[2] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p32_p32),
                                                                                      cospi_p48_p16, 0x1);
        cospi_arr[3] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p32_m32),
                                                                                      cospi_m16_p48, 0x1);
        cospi_arr[4] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_m16_p48),
                                                                                      cospi_m48_m16, 0x1);
        cospi_arr[5] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p48_p16),
                                                                                      cospi_m16_p48, 0x1);
        cospi_arr[6] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p56_p08),
                                                                                      cospi_p24_p40, 0x1);
        cospi_arr[7] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_m08_p56),
                                                                                      cospi_m40_p24, 0x1);
        cospi_arr[8] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p60_p04),
                                                                                      cospi_p28_p36, 0x1);
        cospi_arr[9] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_m04_p60),
                                                                                      cospi_m36_p28, 0x1);
        cospi_arr[10] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p44_p20),
                                                                                        cospi_p12_p52, 0x1);
        cospi_arr[11] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_m20_p44),
                                                                                        cospi_m52_p12, 0x1);

        Span<__m256i> x = stackalloc __m256i[8];
        x[0] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[0]), input[1], 0x1);
        x[1] = _mm256_insertf128_si256(_mm256_castsi128_si256(input[15]), input[14],
                                                                      0x1);
        x[2] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[2]), input[3], 0x1);
        x[3] = _mm256_insertf128_si256(_mm256_castsi128_si256(input[13]), input[12],
                                                                      0x1);
        x[4] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[5]), input[4], 0x1);
        x[5] = _mm256_insertf128_si256(_mm256_castsi128_si256(input[10]), input[11],
                                                                      0x1);
        x[6] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[7]), input[6], 0x1);
        x[7] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[8]), input[9], 0x1);

        // stage 1
        Span<__m256i> x1 = stackalloc __m256i[8];
        x1[0] = _mm256_adds_epi16(x[0], x[1]);
        x1[7] = _mm256_subs_epi16(x[0], x[1]);
        x1[1] = _mm256_adds_epi16(x[2], x[3]);
        x1[6] = _mm256_subs_epi16(x[2], x[3]);
        x1[2] = _mm256_adds_epi16(x[4], x[5]);
        x1[5] = _mm256_subs_epi16(x[4], x[5]);
        x1[3] = _mm256_adds_epi16(x[6], x[7]);
        x1[4] = _mm256_subs_epi16(x[6], x[7]);

        // stage 2
        Span<__m256i> x2 = stackalloc __m256i[8];
        x2[0] = _mm256_adds_epi16(x1[0], x1[3]);
        x2[7] = _mm256_subs_epi16(x1[0], x1[3]);
        x2[1] = _mm256_adds_epi16(x1[1], x1[2]);
        x2[6] = _mm256_subs_epi16(x1[1], x1[2]);
        x2[2] = x1[4];
        x2[3] = x1[7];
        btf_16_avx2(ref cospi_arr[0], ref cospi_arr[1], ref x1[5], ref x1[6], out temp0, out temp1,
                                out temp2, out temp3, in __rounding_256, ref cos_bit);
        x2[4] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp2), temp0, 0x1);
        x2[5] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp3), temp1, 0x1);

        // stage 3
        Span<__m256i> x3 = stackalloc __m256i[8];
        x2[1] = _mm256_permute4x64_epi64(x2[1], 0x4e);
        x3[0] = _mm256_adds_epi16(x2[0], x2[1]);
        x3[1] = _mm256_subs_epi16(x2[0], x2[1]);
        x3[2] = _mm256_blend_epi32(x2[7], x2[6], 0xf0);
        btf_16_sse2(cospi_m32_p32, cospi_p32_p32, _mm256_castsi256_si128(x2[6]),
                                _mm256_extractf128_si256(x2[7], 0x01), ref temp0, ref temp1, __rounding, cos_bit);
        x3[7] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp1), temp0, 0x1);
        x3[3] = _mm256_adds_epi16(x2[2], x2[4]);
        x3[4] = _mm256_subs_epi16(x2[2], x2[4]);
        x3[5] = _mm256_adds_epi16(x2[3], x2[5]);
        x3[6] = _mm256_subs_epi16(x2[3], x2[5]);

        // stage 4
        Span<__m256i> x4 = stackalloc __m256i[8];
        x4[0] = _mm256_blend_epi32(x3[0], x3[1], 0xf0);
        x4[1] = _mm256_permute2f128_si256(x3[0], x3[1], 0x21);
        btf_16_avx2(ref cospi_arr[2], ref cospi_arr[3], ref x4[0], ref x4[1], out output[0],
                                out output[8], out output[4], out output[12], in __rounding_256, ref cos_bit);
        x4[2] = _mm256_adds_epi16(x3[2], x3[7]);
        x4[3] = _mm256_subs_epi16(x3[2], x3[7]);
        x4[4] = _mm256_permute2f128_si256(x3[3], x3[4], 0x20);
        x4[5] = _mm256_permute2f128_si256(x3[6], x3[5], 0x20);
        in0 = _mm256_permute2f128_si256(x3[3], x3[4], 0x31);
        in1 = _mm256_permute2f128_si256(x3[5], x3[6], 0x31);
        btf_16_avx2(ref cospi_arr[4], ref cospi_arr[5], ref in0, ref in1, out temp0, out temp1, out temp2,
                                out temp3, in __rounding_256, ref cos_bit);

        x4[6] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp0), temp2, 0x1);
        x4[7] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp3), temp1, 0x1);

        // stage 5
        Span<__m256i> x5 = stackalloc __m256i[4];
        in0 = _mm256_permute2f128_si256(x4[2], x4[3], 0x31);
        in1 = _mm256_permute2f128_si256(x4[2], x4[3], 0x20);
        btf_16_avx2(ref cospi_arr[6], ref cospi_arr[7], ref in0, ref in1, out output[2], out output[14],
                                out output[10], out output[6], in __rounding_256, ref cos_bit);
        x5[0] = _mm256_adds_epi16(x4[4], x4[6]);
        x5[1] = _mm256_subs_epi16(x4[4], x4[6]);
        x5[2] = _mm256_adds_epi16(x4[5], x4[7]);
        x5[3] = _mm256_subs_epi16(x4[5], x4[7]);

        // stage 6
        in0 = _mm256_permute2f128_si256(x5[0], x5[1], 0x20);
        in1 = _mm256_permute2f128_si256(x5[2], x5[3], 0x31);
        btf_16_avx2(ref cospi_arr[8], ref cospi_arr[9], ref in0, ref in1, out output[1], out output[15],
                                out output[9], out output[7], in __rounding_256, ref cos_bit);
        in0 = _mm256_permute2f128_si256(x5[1], x5[0], 0x31);
        in1 = _mm256_permute2f128_si256(x5[3], x5[2], 0x20);
        btf_16_avx2(ref cospi_arr[10], ref cospi_arr[11], ref in0, ref in1, out output[5],
                                out output[11], out output[13], out output[3], in __rounding_256, ref cos_bit);
    }

    // [port] C stack array, uninitialised: per-thread scratch instead of a zeroed stackalloc.
    [ThreadStatic] private static __m256i[] t_fadst8x16_new_avx2_cospi_arr;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fadst8x16_new_avx2(Span<__m128i> input, Span<__m128i> output, sbyte cos_bit_arg)
    {
        _ = cos_bit_arg;
        sbyte cos_bit = 13;
        Ptr<int> cospi = av1_txfm.cospi_arr(cos_bit);  // [port] qualified: the local cospi_arr below hides the function
        __m256i __zero = _mm256_setzero_si256();
        __m256i __rounding_256 = _mm256_set1_epi32(1 << (cos_bit - 1));
        __m256i in0, in1;
        __m128i temp0, temp1, temp2, temp3;

        __m128i cospi_p32_p32 = pair_set_epi16(cospi[32], cospi[32]);
        __m128i cospi_p32_m32 = pair_set_epi16(cospi[32], -cospi[32]);
        __m128i cospi_p16_p48 = pair_set_epi16(cospi[16], cospi[48]);
        __m128i cospi_p48_m16 = pair_set_epi16(cospi[48], -cospi[16]);
        __m128i cospi_m48_p16 = pair_set_epi16(-cospi[48], cospi[16]);
        __m128i cospi_p08_p56 = pair_set_epi16(cospi[8], cospi[56]);
        __m128i cospi_p56_m08 = pair_set_epi16(cospi[56], -cospi[8]);
        __m128i cospi_p40_p24 = pair_set_epi16(cospi[40], cospi[24]);
        __m128i cospi_p24_m40 = pair_set_epi16(cospi[24], -cospi[40]);
        __m128i cospi_m56_p08 = pair_set_epi16(-cospi[56], cospi[8]);
        __m128i cospi_m24_p40 = pair_set_epi16(-cospi[24], cospi[40]);
        __m128i cospi_p02_p62 = pair_set_epi16(cospi[2], cospi[62]);
        __m128i cospi_p62_m02 = pair_set_epi16(cospi[62], -cospi[2]);
        __m128i cospi_p10_p54 = pair_set_epi16(cospi[10], cospi[54]);
        __m128i cospi_p54_m10 = pair_set_epi16(cospi[54], -cospi[10]);
        __m128i cospi_p18_p46 = pair_set_epi16(cospi[18], cospi[46]);
        __m128i cospi_p46_m18 = pair_set_epi16(cospi[46], -cospi[18]);
        __m128i cospi_p26_p38 = pair_set_epi16(cospi[26], cospi[38]);
        __m128i cospi_p38_m26 = pair_set_epi16(cospi[38], -cospi[26]);
        __m128i cospi_p34_p30 = pair_set_epi16(cospi[34], cospi[30]);
        __m128i cospi_p30_m34 = pair_set_epi16(cospi[30], -cospi[34]);
        __m128i cospi_p42_p22 = pair_set_epi16(cospi[42], cospi[22]);
        __m128i cospi_p22_m42 = pair_set_epi16(cospi[22], -cospi[42]);
        __m128i cospi_p50_p14 = pair_set_epi16(cospi[50], cospi[14]);
        __m128i cospi_p14_m50 = pair_set_epi16(cospi[14], -cospi[50]);
        __m128i cospi_p58_p06 = pair_set_epi16(cospi[58], cospi[6]);
        __m128i cospi_p06_m58 = pair_set_epi16(cospi[6], -cospi[58]);

        Span<__m256i> cospi_arr = t_fadst8x16_new_avx2_cospi_arr ??= new __m256i[20];

        cospi_arr[0] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p32_p32),
                                                                                      cospi_p32_p32, 0x1);
        cospi_arr[1] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p32_m32),
                                                                                      cospi_p32_m32, 0x1);
        cospi_arr[2] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p32_p32),
                                                                                      cospi_p32_p32, 0x1);
        cospi_arr[3] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p32_m32),
                                                                                      cospi_p32_m32, 0x1);
        cospi_arr[4] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p16_p48),
                                                                                      cospi_m48_p16, 0x1);
        cospi_arr[5] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p48_m16),
                                                                                      cospi_p16_p48, 0x1);
        cospi_arr[6] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p16_p48),
                                                                                      cospi_m48_p16, 0x1);
        cospi_arr[7] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p48_m16),
                                                                                      cospi_p16_p48, 0x1);
        cospi_arr[8] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p08_p56),
                                                                                      cospi_p40_p24, 0x1);
        cospi_arr[9] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p56_m08),
                                                                                      cospi_p24_m40, 0x1);
        cospi_arr[10] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_m56_p08),
                                                                                        cospi_m24_p40, 0x1);
        cospi_arr[11] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p08_p56),
                                                                                        cospi_p40_p24, 0x1);
        cospi_arr[12] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p02_p62),
                                                                                        cospi_p10_p54, 0x1);
        cospi_arr[13] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p62_m02),
                                                                                        cospi_p54_m10, 0x1);
        cospi_arr[14] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p18_p46),
                                                                                        cospi_p26_p38, 0x1);
        cospi_arr[15] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p46_m18),
                                                                                        cospi_p38_m26, 0x1);
        cospi_arr[16] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p34_p30),
                                                                                        cospi_p42_p22, 0x1);
        cospi_arr[17] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p30_m34),
                                                                                        cospi_p22_m42, 0x1);
        cospi_arr[18] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p50_p14),
                                                                                        cospi_p58_p06, 0x1);
        cospi_arr[19] = _mm256_insertf128_si256(_mm256_castsi128_si256(cospi_p14_m50),
                                                                                        cospi_p06_m58, 0x1);

        Span<__m256i> x = stackalloc __m256i[8];
        x[0] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[0]), input[4], 0x1);
        x[1] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[2]), input[6], 0x1);
        x[2] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[8]), input[12], 0x1);
        x[3] = _mm256_insertf128_si256(_mm256_castsi128_si256(input[10]), input[14],
                                                                      0x1);
        x[4] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[1]), input[9], 0x1);
        x[5] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[3]), input[11], 0x1);
        x[6] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[5]), input[13], 0x1);
        x[7] =
                _mm256_insertf128_si256(_mm256_castsi128_si256(input[7]), input[15], 0x1);

        // stage 1
        Span<__m256i> x1 = stackalloc __m256i[8];
        x1[0] = x[0];
        x1[1] = _mm256_subs_epi16(__zero, x[7]);
        x1[2] = x[2];
        x1[3] = _mm256_subs_epi16(__zero, x[5]);
        x1[4] = _mm256_subs_epi16(__zero, x[4]);
        x1[5] = x[3];
        x1[6] = _mm256_subs_epi16(__zero, x[6]);
        x1[7] = x[1];

        // stage 2
        Span<__m256i> x2 = stackalloc __m256i[8];
        x2[0] = _mm256_blend_epi32(x1[0], x1[1], 0xf0);
        x2[3] = _mm256_blend_epi32(x1[3], x1[2], 0xf0);
        x2[4] = _mm256_blend_epi32(x1[4], x1[5], 0xf0);
        x2[7] = _mm256_blend_epi32(x1[7], x1[6], 0xf0);
        in0 = _mm256_blend_epi32(x1[1], x1[0], 0xf0);
        in1 = _mm256_blend_epi32(x1[2], x1[3], 0xf0);
        btf_16_avx2(ref cospi_arr[0], ref cospi_arr[1], ref in0, ref in1, out temp0, out temp1, out temp2,
                                out temp3, in __rounding_256, ref cos_bit);
        x2[1] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp0), temp1, 0x1);
        x2[2] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp2), temp3, 0x1);
        in0 = _mm256_permute2f128_si256(x1[7], x1[6], 0x21);
        in1 = _mm256_permute2f128_si256(x1[4], x1[5], 0x21);
        btf_16_avx2(ref cospi_arr[2], ref cospi_arr[3], ref in0, ref in1, out temp0, out temp1, out temp2,
                                out temp3, in __rounding_256, ref cos_bit);
        x2[5] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp0), temp1, 0x1);
        x2[6] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp2), temp3, 0x1);

        // stage 3
        Span<__m256i> x3 = stackalloc __m256i[8];
        x3[0] = _mm256_adds_epi16(x2[0], x2[1]);
        x3[1] = _mm256_subs_epi16(x2[0], x2[1]);
        x3[2] = _mm256_adds_epi16(x2[3], x2[2]);
        x3[3] = _mm256_subs_epi16(x2[3], x2[2]);
        x3[4] = _mm256_adds_epi16(x2[4], x2[5]);
        x3[5] = _mm256_subs_epi16(x2[4], x2[5]);
        x3[6] = _mm256_adds_epi16(x2[7], x2[6]);
        x3[7] = _mm256_subs_epi16(x2[7], x2[6]);

        // stage 4
        Span<__m256i> x4 = stackalloc __m256i[8];
        x4[0] = x3[0];
        x4[1] = x3[1];
        x4[4] = x3[4];
        x4[5] = x3[5];
        in0 = _mm256_permute2f128_si256(x3[2], x3[3], 0x20);
        in1 = _mm256_permute2f128_si256(x3[2], x3[3], 0x31);
        btf_16_avx2(ref cospi_arr[4], ref cospi_arr[5], ref in0, ref in1, out temp0, out temp1, out temp2,
                                out temp3, in __rounding_256, ref cos_bit);
        x4[2] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp0), temp1, 0x1);
        x4[3] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp2), temp3, 0x1);
        in0 = _mm256_permute2f128_si256(x3[6], x3[7], 0x20);
        in1 = _mm256_permute2f128_si256(x3[6], x3[7], 0x31);
        btf_16_avx2(ref cospi_arr[6], ref cospi_arr[7], ref in0, ref in1, out temp0, out temp1, out temp2,
                                out temp3, in __rounding_256, ref cos_bit);
        x4[6] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp0), temp1, 0x1);
        x4[7] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp2), temp3, 0x1);

        // stage 5
        Span<__m256i> x5 = stackalloc __m256i[8];
        x5[0] = _mm256_adds_epi16(x4[0], x4[2]);
        x5[1] = _mm256_subs_epi16(x4[0], x4[2]);
        x5[2] = _mm256_adds_epi16(x4[1], x4[3]);
        x5[3] = _mm256_subs_epi16(x4[1], x4[3]);
        x5[4] = _mm256_adds_epi16(x4[4], x4[6]);
        x5[5] = _mm256_subs_epi16(x4[4], x4[6]);
        x5[6] = _mm256_adds_epi16(x4[5], x4[7]);
        x5[7] = _mm256_subs_epi16(x4[5], x4[7]);

        // stage 6
        Span<__m256i> x6 = stackalloc __m256i[8];
        x6[0] = x5[0];
        x6[1] = x5[2];
        x6[2] = x5[1];
        x6[3] = x5[3];
        in0 = _mm256_permute2f128_si256(x5[4], x5[6], 0x20);
        in1 = _mm256_permute2f128_si256(x5[4], x5[6], 0x31);
        btf_16_avx2(ref cospi_arr[8], ref cospi_arr[9], ref in0, ref in1, out temp0, out temp1, out temp2,
                                out temp3, in __rounding_256, ref cos_bit);
        x6[4] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp0), temp1, 0x1);
        x6[5] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp2), temp3, 0x1);
        in0 = _mm256_permute2f128_si256(x5[5], x5[7], 0x20);
        in1 = _mm256_permute2f128_si256(x5[5], x5[7], 0x31);
        btf_16_avx2(ref cospi_arr[10], ref cospi_arr[11], ref in0, ref in1, out temp0, out temp1,
                                out temp2, out temp3, in __rounding_256, ref cos_bit);
        x6[6] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp0), temp1, 0x1);
        x6[7] = _mm256_insertf128_si256(_mm256_castsi128_si256(temp2), temp3, 0x1);

        // stage 7
        Span<__m256i> x7 = stackalloc __m256i[8];
        x7[0] = _mm256_adds_epi16(x6[0], x6[4]);
        x7[1] = _mm256_subs_epi16(x6[0], x6[4]);
        x7[2] = _mm256_adds_epi16(x6[1], x6[5]);
        x7[3] = _mm256_subs_epi16(x6[1], x6[5]);
        x7[4] = _mm256_adds_epi16(x6[2], x6[6]);
        x7[5] = _mm256_subs_epi16(x6[2], x6[6]);
        x7[6] = _mm256_adds_epi16(x6[3], x6[7]);
        x7[7] = _mm256_subs_epi16(x6[3], x6[7]);

        // stage 8
        in0 = _mm256_permute2f128_si256(x7[0], x7[2], 0x20);
        in1 = _mm256_permute2f128_si256(x7[0], x7[2], 0x31);
        btf_16_avx2(ref cospi_arr[12], ref cospi_arr[13], ref in0, ref in1, out output[15],
                                out output[0], out output[13], out output[2], in __rounding_256, ref cos_bit);
        in0 = _mm256_permute2f128_si256(x7[4], x7[6], 0x20);
        in1 = _mm256_permute2f128_si256(x7[4], x7[6], 0x31);
        btf_16_avx2(ref cospi_arr[14], ref cospi_arr[15], ref in0, ref in1, out output[11],
                                out output[4], out output[9], out output[6], in __rounding_256, ref cos_bit);
        in0 = _mm256_permute2f128_si256(x7[1], x7[3], 0x20);
        in1 = _mm256_permute2f128_si256(x7[1], x7[3], 0x31);
        btf_16_avx2(ref cospi_arr[16], ref cospi_arr[17], ref in0, ref in1, out output[7],
                                out output[8], out output[5], out output[10], in __rounding_256, ref cos_bit);
        in0 = _mm256_permute2f128_si256(x7[5], x7[7], 0x20);
        in1 = _mm256_permute2f128_si256(x7[5], x7[7], 0x31);
        btf_16_avx2(ref cospi_arr[18], ref cospi_arr[19], ref in0, ref in1, out output[3],
                                out output[12], out output[1], out output[14], in __rounding_256, ref cos_bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void fidentity8x16_new_avx2(Span<__m128i> input, Span<__m128i> output, sbyte cos_bit)
    {
        _ = cos_bit;
        __m256i one = _mm256_set1_epi16(1);
        __m256i temp;
        for (int i = 0; i < 16; i += 2)
        {
            temp = _mm256_insertf128_si256(_mm256_castsi128_si256(input[i]),
                                                                          input[i + 1], 0x1);
            __m256i a_lo = _mm256_unpacklo_epi16(temp, one);
            __m256i a_hi = _mm256_unpackhi_epi16(temp, one);
            __m256i b_lo = scale_round_avx2(a_lo, 2 * NewSqrt2);
            __m256i b_hi = scale_round_avx2(a_hi, 2 * NewSqrt2);
            temp = _mm256_packs_epi32(b_lo, b_hi);
            output[i] = _mm256_castsi256_si128(temp);
            output[i + 1] = _mm256_extractf128_si256(temp, 0x1);
        }
    }

    private static readonly transform_1d_avx2[] row_txfm8x16_arr = {
        fdct8x8_new_avx2,       // DCT_DCT
        fdct8x8_new_avx2,       // ADST_DCT
        fadst8x8_new_avx2,      // DCT_ADST
        fadst8x8_new_avx2,      // ADST_ADST
        fdct8x8_new_avx2,       // FLIPADST_DCT
        fadst8x8_new_avx2,      // DCT_FLIPADST
        fadst8x8_new_avx2,      // FLIPADST_FLIPADST
        fadst8x8_new_avx2,      // ADST_FLIPADST
        fadst8x8_new_avx2,      // FLIPADST_ADST
        fidentity8x8_new_avx2,  // IDTX
        fidentity8x8_new_avx2,  // V_DCT
        fdct8x8_new_avx2,       // H_DCT
        fidentity8x8_new_avx2,  // V_ADST
        fadst8x8_new_avx2,      // H_ADST
        fidentity8x8_new_avx2,  // V_FLIPADST
        fadst8x8_new_avx2       // H_FLIPADST
    };

    private static readonly transform_1d_sse2[] col_txfm8x16_arr = {
        fdct8x16_new_avx2,       // DCT_DCT
        fadst8x16_new_avx2,      // ADST_DCT
        fdct8x16_new_avx2,       // DCT_ADST
        fadst8x16_new_avx2,      // ADST_ADST
        fadst8x16_new_avx2,      // FLIPADST_DCT
        fdct8x16_new_avx2,       // DCT_FLIPADST
        fadst8x16_new_avx2,      // FLIPADST_FLIPADST
        fadst8x16_new_avx2,      // ADST_FLIPADST
        fadst8x16_new_avx2,      // FLIPADST_ADST
        fidentity8x16_new_avx2,  // IDTX
        fdct8x16_new_avx2,       // V_DCT
        fidentity8x16_new_avx2,  // H_DCT
        fadst8x16_new_avx2,      // V_ADST
        fidentity8x16_new_avx2,  // H_ADST
        fadst8x16_new_avx2,      // V_FLIPADST
        fidentity8x16_new_avx2   // H_FLIPADST
    };

    private static readonly transform_1d_avx2[] col_txfm16x8_arr = {
        fdct8x8_new_avx2,       // DCT_DCT
        fadst8x8_new_avx2,      // ADST_DCT
        fdct8x8_new_avx2,       // DCT_ADST
        fadst8x8_new_avx2,      // ADST_ADST
        fadst8x8_new_avx2,      // FLIPADST_DCT
        fdct8x8_new_avx2,       // DCT_FLIPADST
        fadst8x8_new_avx2,      // FLIPADST_FLIPADST
        fadst8x8_new_avx2,      // ADST_FLIPADST
        fadst8x8_new_avx2,      // FLIPADST_ADST
        fidentity8x8_new_avx2,  // IDTX
        fdct8x8_new_avx2,       // V_DCT
        fidentity8x8_new_avx2,  // H_DCT
        fadst8x8_new_avx2,      // V_ADST
        fidentity8x8_new_avx2,  // H_ADST
        fadst8x8_new_avx2,      // V_FLIPADST
        fidentity8x8_new_avx2,  // H_FLIPADST
    };

    private static readonly transform_1d_sse2[] row_txfm16x8_arr = {
        fdct8x16_new_avx2,       // DCT_DCT
        fdct8x16_new_avx2,       // ADST_DCT
        fadst8x16_new_avx2,      // DCT_ADST
        fadst8x16_new_avx2,      // ADST_ADST
        fdct8x16_new_avx2,       // FLIPADST_DCT
        fadst8x16_new_avx2,      // DCT_FLIPADST
        fadst8x16_new_avx2,      // FLIPADST_FLIPADST
        fadst8x16_new_avx2,      // ADST_FLIPADST
        fadst8x16_new_avx2,      // FLIPADST_ADST
        fidentity8x16_new_avx2,  // IDTX
        fidentity8x16_new_avx2,  // V_DCT
        fdct8x16_new_avx2,       // H_DCT
        fidentity8x16_new_avx2,  // V_ADST
        fadst8x16_new_avx2,      // H_ADST
        fidentity8x16_new_avx2,  // V_FLIPADST
        fadst8x16_new_avx2       // H_FLIPADST
    };

    [MethodImpl(MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void lowbd_fwd_txfm2d_8x16_non_dct_avx2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        _ = bd;
        Span<__m128i> buf0 = stackalloc __m128i[16];
        Span<__m128i> buf1 = stackalloc __m128i[16];
        Span<__m256i> buf2 = stackalloc __m256i[8];
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[TX_8X16];
        int txw_idx = get_txw_idx(TX_8X16);
        int txh_idx = get_txh_idx(TX_8X16);
        int cos_bit_col = av1_fwd_cos_bit_col[txw_idx][txh_idx];
        int cos_bit_row = av1_fwd_cos_bit_row[txw_idx][txh_idx];
        int width = 8;
        int height = 16;
        transform_1d_sse2 col_txfm = col_txfm8x16_arr[tx_type];
        transform_1d_avx2 row_txfm = row_txfm8x16_arr[tx_type];
        int ud_flip = 0, lr_flip = 0;

        get_flip_cfg(tx_type, ref ud_flip, ref lr_flip);
        if (ud_flip != 0)
        {
            load_buffer_16bit_to_16bit_flip(input, stride, buf0, height);
        }
        else
        {
            load_buffer_16bit_to_16bit(input, stride, buf0, height);
        }
        round_shift_16bit(buf0, height, shift[0]);
        col_txfm(buf0, buf0, (sbyte)cos_bit_col);
        round_shift_16bit(buf0, height, shift[1]);
        transpose_16bit_8x8(buf0, buf1);
        transpose_16bit_8x8(buf0.Slice(8), buf1.Slice(8));

        scoped Span<__m128i> bufl, bufu;
        if (lr_flip != 0)
        {
            bufl = buf0;
            bufu = buf0.Slice(8);
            flip_buf_sse2(buf1.Slice(width * 0), bufl, width);
            flip_buf_sse2(buf1.Slice(width * 1), bufu, width);
        }
        else
        {
            bufl = buf1.Slice(width * 0);
            bufu = buf1.Slice(width * 1);
        }
        pack_reg(bufl, bufu, buf2);
        row_txfm(buf2, buf2, (sbyte)cos_bit_row);
        round_shift_16bit_w16_avx2(buf2, width, shift[2]);
        store_rect_buffer_16bit_to_32bit_w16_avx2(buf2, output, height, width);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void lowbd_fwd_txfm2d_8x16_avx2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        if ((tx_type != DCT_DCT))
        {
            lowbd_fwd_txfm2d_8x16_non_dct_avx2(input, output, stride, tx_type, bd);
            return;
        }
        _ = bd;
        Span<__m128i> buf0 = stackalloc __m128i[16];
        Span<__m128i> buf1 = stackalloc __m128i[16];
        Span<__m256i> buf2 = stackalloc __m256i[8];
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[TX_8X16];
        load_buffer_16bit_to_16bit(input, stride, buf0, 16);
        round_shift_16bit(buf0, 16, shift[0]);
        fdct8x16_new_avx2(buf0, buf0, 13);
        round_shift_16bit(buf0, 16, shift[1]);
        transpose_16bit_8x8(buf0, buf1);
        transpose_16bit_8x8(buf0.Slice(8), buf1.Slice(8));
        pack_reg(buf1, buf1.Slice(8), buf2);
        fdct8x8_new_avx2(buf2, buf2, 13);
        store_rect_buffer_16bit_to_32bit_w16_avx2(buf2, output, 16, 8);
    }

    [MethodImpl(MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void lowbd_fwd_txfm2d_16x8_non_dct_avx2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        _ = bd;
        Span<__m128i> buf0 = stackalloc __m128i[16];
        Span<__m128i> buf1 = stackalloc __m128i[16];
        Span<__m256i> buf2 = stackalloc __m256i[8];
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[TX_16X8];
        int txw_idx = get_txw_idx(TX_16X8);
        int txh_idx = get_txh_idx(TX_16X8);
        int cos_bit_col = av1_fwd_cos_bit_col[txw_idx][txh_idx];
        int cos_bit_row = av1_fwd_cos_bit_row[txw_idx][txh_idx];
        int width = 16;
        int height = 8;
        transform_1d_avx2 col_txfm = col_txfm16x8_arr[tx_type];
        transform_1d_sse2 row_txfm = row_txfm16x8_arr[tx_type];
        scoped Span<__m128i> buf;
        int ud_flip = 0, lr_flip = 0;

        get_flip_cfg(tx_type, ref ud_flip, ref lr_flip);

        if (ud_flip != 0)
        {
            load_buffer_16bit_to_16bit_flip(input + 8 * 0, stride, buf0, height);
            load_buffer_16bit_to_16bit_flip(input + 8 * 1, stride, buf0.Slice(8), height);
        }
        else
        {
            load_buffer_16bit_to_16bit(input + 8 * 0, stride, buf0, height);
            load_buffer_16bit_to_16bit(input + 8 * 1, stride, buf0.Slice(8), height);
        }
        pack_reg(buf0, buf0.Slice(8), buf2);
        round_shift_16bit_w16_avx2(buf2, height, shift[0]);
        col_txfm(buf2, buf2, (sbyte)cos_bit_col);
        round_shift_16bit_w16_avx2(buf2, height, shift[1]);
        transpose_16bit_16x8_avx2(buf2, buf2);
        extract_reg(buf2, buf1);

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

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void lowbd_fwd_txfm2d_16x8_avx2(Ptr<short> input, Ptr<int> output, int stride, TX_TYPE tx_type, int bd)
    {
        if ((tx_type != DCT_DCT))
        {
            lowbd_fwd_txfm2d_16x8_non_dct_avx2(input, output, stride, tx_type, bd);
            return;
        }
        _ = bd;
        Span<__m128i> buf0 = stackalloc __m128i[16];
        Span<__m128i> buf1 = stackalloc __m128i[16];
        Span<__m256i> buf2 = stackalloc __m256i[8];
        Ptr<sbyte> shift = av1_fwd_txfm_shift_ls[TX_16X8];
        load_buffer_16bit_to_16bit(input + 8 * 0, stride, buf0, 8);
        load_buffer_16bit_to_16bit(input + 8 * 1, stride, buf0.Slice(8), 8);
        pack_reg(buf0, buf0.Slice(8), buf2);
        round_shift_16bit_w16_avx2(buf2, 8, shift[0]);
        fdct8x8_new_avx2(buf2, buf2, 13);
        round_shift_16bit_w16_avx2(buf2, 8, shift[1]);
        transpose_16bit_16x8_avx2(buf2, buf2);
        extract_reg(buf2, buf1);
        fdct8x16_new_avx2(buf1, buf1, 13);
        store_rect_buffer_16bit_to_32bit_w8(buf1, output, 8, 16);
    }

    private static readonly FwdTxfm2dFunc[] fwd_txfm2d_func_ls = {
        av1_lowbd_fwd_txfm2d_4x4_sse2,   // 4x4 transform
        av1_lowbd_fwd_txfm2d_8x8_avx2,   // 8x8 transform
        lowbd_fwd_txfm2d_16x16_avx2,     // 16x16 transform
        lowbd_fwd_txfm2d_32x32_avx2,     // 32x32 transform
        null,  // [subset] lowbd_fwd_txfm2d_64x64_avx2, // 64x64 transform
        av1_lowbd_fwd_txfm2d_4x8_sse2,   // 4x8 transform
        av1_lowbd_fwd_txfm2d_8x4_sse2,   // 8x4 transform
        lowbd_fwd_txfm2d_8x16_avx2,      // 8x16 transform
        lowbd_fwd_txfm2d_16x8_avx2,      // 16x8 transform
        lowbd_fwd_txfm2d_16x32_avx2,     // 16x32 transform
        lowbd_fwd_txfm2d_32x16_avx2,     // 32x16 transform
        null,  // [subset] lowbd_fwd_txfm2d_32x64_avx2, // 32x64 transform
        null,  // [subset] lowbd_fwd_txfm2d_64x32_avx2, // 64x32 transform
        null,  // [subset] av1_lowbd_fwd_txfm2d_4x16_sse2, // 4x16 transform
        null,  // [subset] av1_lowbd_fwd_txfm2d_16x4_sse2, // 16x4 transform
        null,  // [subset] av1_lowbd_fwd_txfm2d_8x32_sse2, // 8x32 transform
        null,  // [subset] av1_lowbd_fwd_txfm2d_32x8_sse2, // 32x8 transform
        null,  // [subset] lowbd_fwd_txfm2d_16x64_avx2, // 16x64 transform
        null,  // [subset] lowbd_fwd_txfm2d_64x16_avx2, // 64x16 transform
    };

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static void av1_lowbd_fwd_txfm_avx2(Ptr<short> src_diff, Ptr<tran_low_t> coeff, int diff_stride, ref TxfmParam txfm_param)
    {
        FwdTxfm2dFunc fwd_txfm2d_func = fwd_txfm2d_func_ls[txfm_param.tx_size];
        if (txfm_param.lossless != 0 && txfm_param.tx_size == TX_4X4)
        {
            av1_lowbd_fwd_txfm_c(src_diff, coeff, diff_stride, ref txfm_param);
        }
        else
        {
            fwd_txfm2d_func(src_diff, coeff, diff_stride, txfm_param.tx_type,
                                            txfm_param.bd);
        }
    }
}
