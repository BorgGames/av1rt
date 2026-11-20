// Port of libaom av1/encoder/av1_fwd_txfm1d.c
//
// [port] The stage buffers bf0/bf1 are spans over `output` or the stack `step` array
// (C: int32_t pointers); all arithmetic stays in int32 exactly as in C (half_btf).

using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class av1_fwd_txfm1d
{
    internal static void av1_fdct4(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
        Ptr<sbyte> stage_range)
    {
        const int size = 4;
        ReadOnlySpan<int> cospi;

        int stage = 0;
        scoped Span<int> bf0, bf1;
        Span<int> step = stackalloc int[4];

        // stage 0;
        av1_range_check_buf(stage, input, input, size, stage_range[stage]);

        // stage 1;
        stage++;
        bf1 = output.AsSpan(size);
        bf1[0] = input[0] + input[3];
        bf1[1] = input[1] + input[2];
        bf1[2] = -input[2] + input[1];
        bf1[3] = -input[3] + input[0];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 2
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = half_btf(cospi[32], bf0[0], cospi[32], bf0[1], cos_bit);
        bf1[1] = half_btf(-cospi[32], bf0[1], cospi[32], bf0[0], cos_bit);
        bf1[2] = half_btf(cospi[48], bf0[2], cospi[16], bf0[3], cos_bit);
        bf1[3] = half_btf(cospi[48], bf0[3], -cospi[16], bf0[2], cos_bit);
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 3
        stage++;
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0];
        bf1[1] = bf0[2];
        bf1[2] = bf0[1];
        bf1[3] = bf0[3];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);
    }

    internal static void av1_fdct8(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
        Ptr<sbyte> stage_range)
    {
        const int size = 8;
        ReadOnlySpan<int> cospi;

        int stage = 0;
        scoped Span<int> bf0, bf1;
        Span<int> step = stackalloc int[8];

        // stage 0;
        av1_range_check_buf(stage, input, input, size, stage_range[stage]);

        // stage 1;
        stage++;
        bf1 = output.AsSpan(size);
        bf1[0] = input[0] + input[7];
        bf1[1] = input[1] + input[6];
        bf1[2] = input[2] + input[5];
        bf1[3] = input[3] + input[4];
        bf1[4] = -input[4] + input[3];
        bf1[5] = -input[5] + input[2];
        bf1[6] = -input[6] + input[1];
        bf1[7] = -input[7] + input[0];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 2
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0] + bf0[3];
        bf1[1] = bf0[1] + bf0[2];
        bf1[2] = -bf0[2] + bf0[1];
        bf1[3] = -bf0[3] + bf0[0];
        bf1[4] = bf0[4];
        bf1[5] = half_btf(-cospi[32], bf0[5], cospi[32], bf0[6], cos_bit);
        bf1[6] = half_btf(cospi[32], bf0[6], cospi[32], bf0[5], cos_bit);
        bf1[7] = bf0[7];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 3
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = half_btf(cospi[32], bf0[0], cospi[32], bf0[1], cos_bit);
        bf1[1] = half_btf(-cospi[32], bf0[1], cospi[32], bf0[0], cos_bit);
        bf1[2] = half_btf(cospi[48], bf0[2], cospi[16], bf0[3], cos_bit);
        bf1[3] = half_btf(cospi[48], bf0[3], -cospi[16], bf0[2], cos_bit);
        bf1[4] = bf0[4] + bf0[5];
        bf1[5] = -bf0[5] + bf0[4];
        bf1[6] = -bf0[6] + bf0[7];
        bf1[7] = bf0[7] + bf0[6];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 4
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = bf0[2];
        bf1[3] = bf0[3];
        bf1[4] = half_btf(cospi[56], bf0[4], cospi[8], bf0[7], cos_bit);
        bf1[5] = half_btf(cospi[24], bf0[5], cospi[40], bf0[6], cos_bit);
        bf1[6] = half_btf(cospi[24], bf0[6], -cospi[40], bf0[5], cos_bit);
        bf1[7] = half_btf(cospi[56], bf0[7], -cospi[8], bf0[4], cos_bit);
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 5
        stage++;
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0];
        bf1[1] = bf0[4];
        bf1[2] = bf0[2];
        bf1[3] = bf0[6];
        bf1[4] = bf0[1];
        bf1[5] = bf0[5];
        bf1[6] = bf0[3];
        bf1[7] = bf0[7];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);
    }

    internal static void av1_fdct16(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
        Ptr<sbyte> stage_range)
    {
        const int size = 16;
        ReadOnlySpan<int> cospi;

        int stage = 0;
        scoped Span<int> bf0, bf1;
        Span<int> step = stackalloc int[16];

        // stage 0;
        av1_range_check_buf(stage, input, input, size, stage_range[stage]);

        // stage 1;
        stage++;
        bf1 = output.AsSpan(size);
        bf1[0] = input[0] + input[15];
        bf1[1] = input[1] + input[14];
        bf1[2] = input[2] + input[13];
        bf1[3] = input[3] + input[12];
        bf1[4] = input[4] + input[11];
        bf1[5] = input[5] + input[10];
        bf1[6] = input[6] + input[9];
        bf1[7] = input[7] + input[8];
        bf1[8] = -input[8] + input[7];
        bf1[9] = -input[9] + input[6];
        bf1[10] = -input[10] + input[5];
        bf1[11] = -input[11] + input[4];
        bf1[12] = -input[12] + input[3];
        bf1[13] = -input[13] + input[2];
        bf1[14] = -input[14] + input[1];
        bf1[15] = -input[15] + input[0];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 2
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0] + bf0[7];
        bf1[1] = bf0[1] + bf0[6];
        bf1[2] = bf0[2] + bf0[5];
        bf1[3] = bf0[3] + bf0[4];
        bf1[4] = -bf0[4] + bf0[3];
        bf1[5] = -bf0[5] + bf0[2];
        bf1[6] = -bf0[6] + bf0[1];
        bf1[7] = -bf0[7] + bf0[0];
        bf1[8] = bf0[8];
        bf1[9] = bf0[9];
        bf1[10] = half_btf(-cospi[32], bf0[10], cospi[32], bf0[13], cos_bit);
        bf1[11] = half_btf(-cospi[32], bf0[11], cospi[32], bf0[12], cos_bit);
        bf1[12] = half_btf(cospi[32], bf0[12], cospi[32], bf0[11], cos_bit);
        bf1[13] = half_btf(cospi[32], bf0[13], cospi[32], bf0[10], cos_bit);
        bf1[14] = bf0[14];
        bf1[15] = bf0[15];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 3
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0] + bf0[3];
        bf1[1] = bf0[1] + bf0[2];
        bf1[2] = -bf0[2] + bf0[1];
        bf1[3] = -bf0[3] + bf0[0];
        bf1[4] = bf0[4];
        bf1[5] = half_btf(-cospi[32], bf0[5], cospi[32], bf0[6], cos_bit);
        bf1[6] = half_btf(cospi[32], bf0[6], cospi[32], bf0[5], cos_bit);
        bf1[7] = bf0[7];
        bf1[8] = bf0[8] + bf0[11];
        bf1[9] = bf0[9] + bf0[10];
        bf1[10] = -bf0[10] + bf0[9];
        bf1[11] = -bf0[11] + bf0[8];
        bf1[12] = -bf0[12] + bf0[15];
        bf1[13] = -bf0[13] + bf0[14];
        bf1[14] = bf0[14] + bf0[13];
        bf1[15] = bf0[15] + bf0[12];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 4
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = half_btf(cospi[32], bf0[0], cospi[32], bf0[1], cos_bit);
        bf1[1] = half_btf(-cospi[32], bf0[1], cospi[32], bf0[0], cos_bit);
        bf1[2] = half_btf(cospi[48], bf0[2], cospi[16], bf0[3], cos_bit);
        bf1[3] = half_btf(cospi[48], bf0[3], -cospi[16], bf0[2], cos_bit);
        bf1[4] = bf0[4] + bf0[5];
        bf1[5] = -bf0[5] + bf0[4];
        bf1[6] = -bf0[6] + bf0[7];
        bf1[7] = bf0[7] + bf0[6];
        bf1[8] = bf0[8];
        bf1[9] = half_btf(-cospi[16], bf0[9], cospi[48], bf0[14], cos_bit);
        bf1[10] = half_btf(-cospi[48], bf0[10], -cospi[16], bf0[13], cos_bit);
        bf1[11] = bf0[11];
        bf1[12] = bf0[12];
        bf1[13] = half_btf(cospi[48], bf0[13], -cospi[16], bf0[10], cos_bit);
        bf1[14] = half_btf(cospi[16], bf0[14], cospi[48], bf0[9], cos_bit);
        bf1[15] = bf0[15];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 5
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = bf0[2];
        bf1[3] = bf0[3];
        bf1[4] = half_btf(cospi[56], bf0[4], cospi[8], bf0[7], cos_bit);
        bf1[5] = half_btf(cospi[24], bf0[5], cospi[40], bf0[6], cos_bit);
        bf1[6] = half_btf(cospi[24], bf0[6], -cospi[40], bf0[5], cos_bit);
        bf1[7] = half_btf(cospi[56], bf0[7], -cospi[8], bf0[4], cos_bit);
        bf1[8] = bf0[8] + bf0[9];
        bf1[9] = -bf0[9] + bf0[8];
        bf1[10] = -bf0[10] + bf0[11];
        bf1[11] = bf0[11] + bf0[10];
        bf1[12] = bf0[12] + bf0[13];
        bf1[13] = -bf0[13] + bf0[12];
        bf1[14] = -bf0[14] + bf0[15];
        bf1[15] = bf0[15] + bf0[14];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 6
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = bf0[2];
        bf1[3] = bf0[3];
        bf1[4] = bf0[4];
        bf1[5] = bf0[5];
        bf1[6] = bf0[6];
        bf1[7] = bf0[7];
        bf1[8] = half_btf(cospi[60], bf0[8], cospi[4], bf0[15], cos_bit);
        bf1[9] = half_btf(cospi[28], bf0[9], cospi[36], bf0[14], cos_bit);
        bf1[10] = half_btf(cospi[44], bf0[10], cospi[20], bf0[13], cos_bit);
        bf1[11] = half_btf(cospi[12], bf0[11], cospi[52], bf0[12], cos_bit);
        bf1[12] = half_btf(cospi[12], bf0[12], -cospi[52], bf0[11], cos_bit);
        bf1[13] = half_btf(cospi[44], bf0[13], -cospi[20], bf0[10], cos_bit);
        bf1[14] = half_btf(cospi[28], bf0[14], -cospi[36], bf0[9], cos_bit);
        bf1[15] = half_btf(cospi[60], bf0[15], -cospi[4], bf0[8], cos_bit);
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 7
        stage++;
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0];
        bf1[1] = bf0[8];
        bf1[2] = bf0[4];
        bf1[3] = bf0[12];
        bf1[4] = bf0[2];
        bf1[5] = bf0[10];
        bf1[6] = bf0[6];
        bf1[7] = bf0[14];
        bf1[8] = bf0[1];
        bf1[9] = bf0[9];
        bf1[10] = bf0[5];
        bf1[11] = bf0[13];
        bf1[12] = bf0[3];
        bf1[13] = bf0[11];
        bf1[14] = bf0[7];
        bf1[15] = bf0[15];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);
    }

    internal static void av1_fdct32(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
        Ptr<sbyte> stage_range)
    {
        const int size = 32;
        ReadOnlySpan<int> cospi;

        int stage = 0;
        scoped Span<int> bf0, bf1;
        Span<int> step = stackalloc int[32];

        // stage 0;
        av1_range_check_buf(stage, input, input, size, stage_range[stage]);

        // stage 1;
        stage++;
        bf1 = output.AsSpan(size);
        bf1[0] = input[0] + input[31];
        bf1[1] = input[1] + input[30];
        bf1[2] = input[2] + input[29];
        bf1[3] = input[3] + input[28];
        bf1[4] = input[4] + input[27];
        bf1[5] = input[5] + input[26];
        bf1[6] = input[6] + input[25];
        bf1[7] = input[7] + input[24];
        bf1[8] = input[8] + input[23];
        bf1[9] = input[9] + input[22];
        bf1[10] = input[10] + input[21];
        bf1[11] = input[11] + input[20];
        bf1[12] = input[12] + input[19];
        bf1[13] = input[13] + input[18];
        bf1[14] = input[14] + input[17];
        bf1[15] = input[15] + input[16];
        bf1[16] = -input[16] + input[15];
        bf1[17] = -input[17] + input[14];
        bf1[18] = -input[18] + input[13];
        bf1[19] = -input[19] + input[12];
        bf1[20] = -input[20] + input[11];
        bf1[21] = -input[21] + input[10];
        bf1[22] = -input[22] + input[9];
        bf1[23] = -input[23] + input[8];
        bf1[24] = -input[24] + input[7];
        bf1[25] = -input[25] + input[6];
        bf1[26] = -input[26] + input[5];
        bf1[27] = -input[27] + input[4];
        bf1[28] = -input[28] + input[3];
        bf1[29] = -input[29] + input[2];
        bf1[30] = -input[30] + input[1];
        bf1[31] = -input[31] + input[0];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 2
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0] + bf0[15];
        bf1[1] = bf0[1] + bf0[14];
        bf1[2] = bf0[2] + bf0[13];
        bf1[3] = bf0[3] + bf0[12];
        bf1[4] = bf0[4] + bf0[11];
        bf1[5] = bf0[5] + bf0[10];
        bf1[6] = bf0[6] + bf0[9];
        bf1[7] = bf0[7] + bf0[8];
        bf1[8] = -bf0[8] + bf0[7];
        bf1[9] = -bf0[9] + bf0[6];
        bf1[10] = -bf0[10] + bf0[5];
        bf1[11] = -bf0[11] + bf0[4];
        bf1[12] = -bf0[12] + bf0[3];
        bf1[13] = -bf0[13] + bf0[2];
        bf1[14] = -bf0[14] + bf0[1];
        bf1[15] = -bf0[15] + bf0[0];
        bf1[16] = bf0[16];
        bf1[17] = bf0[17];
        bf1[18] = bf0[18];
        bf1[19] = bf0[19];
        bf1[20] = half_btf(-cospi[32], bf0[20], cospi[32], bf0[27], cos_bit);
        bf1[21] = half_btf(-cospi[32], bf0[21], cospi[32], bf0[26], cos_bit);
        bf1[22] = half_btf(-cospi[32], bf0[22], cospi[32], bf0[25], cos_bit);
        bf1[23] = half_btf(-cospi[32], bf0[23], cospi[32], bf0[24], cos_bit);
        bf1[24] = half_btf(cospi[32], bf0[24], cospi[32], bf0[23], cos_bit);
        bf1[25] = half_btf(cospi[32], bf0[25], cospi[32], bf0[22], cos_bit);
        bf1[26] = half_btf(cospi[32], bf0[26], cospi[32], bf0[21], cos_bit);
        bf1[27] = half_btf(cospi[32], bf0[27], cospi[32], bf0[20], cos_bit);
        bf1[28] = bf0[28];
        bf1[29] = bf0[29];
        bf1[30] = bf0[30];
        bf1[31] = bf0[31];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 3
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0] + bf0[7];
        bf1[1] = bf0[1] + bf0[6];
        bf1[2] = bf0[2] + bf0[5];
        bf1[3] = bf0[3] + bf0[4];
        bf1[4] = -bf0[4] + bf0[3];
        bf1[5] = -bf0[5] + bf0[2];
        bf1[6] = -bf0[6] + bf0[1];
        bf1[7] = -bf0[7] + bf0[0];
        bf1[8] = bf0[8];
        bf1[9] = bf0[9];
        bf1[10] = half_btf(-cospi[32], bf0[10], cospi[32], bf0[13], cos_bit);
        bf1[11] = half_btf(-cospi[32], bf0[11], cospi[32], bf0[12], cos_bit);
        bf1[12] = half_btf(cospi[32], bf0[12], cospi[32], bf0[11], cos_bit);
        bf1[13] = half_btf(cospi[32], bf0[13], cospi[32], bf0[10], cos_bit);
        bf1[14] = bf0[14];
        bf1[15] = bf0[15];
        bf1[16] = bf0[16] + bf0[23];
        bf1[17] = bf0[17] + bf0[22];
        bf1[18] = bf0[18] + bf0[21];
        bf1[19] = bf0[19] + bf0[20];
        bf1[20] = -bf0[20] + bf0[19];
        bf1[21] = -bf0[21] + bf0[18];
        bf1[22] = -bf0[22] + bf0[17];
        bf1[23] = -bf0[23] + bf0[16];
        bf1[24] = -bf0[24] + bf0[31];
        bf1[25] = -bf0[25] + bf0[30];
        bf1[26] = -bf0[26] + bf0[29];
        bf1[27] = -bf0[27] + bf0[28];
        bf1[28] = bf0[28] + bf0[27];
        bf1[29] = bf0[29] + bf0[26];
        bf1[30] = bf0[30] + bf0[25];
        bf1[31] = bf0[31] + bf0[24];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 4
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0] + bf0[3];
        bf1[1] = bf0[1] + bf0[2];
        bf1[2] = -bf0[2] + bf0[1];
        bf1[3] = -bf0[3] + bf0[0];
        bf1[4] = bf0[4];
        bf1[5] = half_btf(-cospi[32], bf0[5], cospi[32], bf0[6], cos_bit);
        bf1[6] = half_btf(cospi[32], bf0[6], cospi[32], bf0[5], cos_bit);
        bf1[7] = bf0[7];
        bf1[8] = bf0[8] + bf0[11];
        bf1[9] = bf0[9] + bf0[10];
        bf1[10] = -bf0[10] + bf0[9];
        bf1[11] = -bf0[11] + bf0[8];
        bf1[12] = -bf0[12] + bf0[15];
        bf1[13] = -bf0[13] + bf0[14];
        bf1[14] = bf0[14] + bf0[13];
        bf1[15] = bf0[15] + bf0[12];
        bf1[16] = bf0[16];
        bf1[17] = bf0[17];
        bf1[18] = half_btf(-cospi[16], bf0[18], cospi[48], bf0[29], cos_bit);
        bf1[19] = half_btf(-cospi[16], bf0[19], cospi[48], bf0[28], cos_bit);
        bf1[20] = half_btf(-cospi[48], bf0[20], -cospi[16], bf0[27], cos_bit);
        bf1[21] = half_btf(-cospi[48], bf0[21], -cospi[16], bf0[26], cos_bit);
        bf1[22] = bf0[22];
        bf1[23] = bf0[23];
        bf1[24] = bf0[24];
        bf1[25] = bf0[25];
        bf1[26] = half_btf(cospi[48], bf0[26], -cospi[16], bf0[21], cos_bit);
        bf1[27] = half_btf(cospi[48], bf0[27], -cospi[16], bf0[20], cos_bit);
        bf1[28] = half_btf(cospi[16], bf0[28], cospi[48], bf0[19], cos_bit);
        bf1[29] = half_btf(cospi[16], bf0[29], cospi[48], bf0[18], cos_bit);
        bf1[30] = bf0[30];
        bf1[31] = bf0[31];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 5
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = half_btf(cospi[32], bf0[0], cospi[32], bf0[1], cos_bit);
        bf1[1] = half_btf(-cospi[32], bf0[1], cospi[32], bf0[0], cos_bit);
        bf1[2] = half_btf(cospi[48], bf0[2], cospi[16], bf0[3], cos_bit);
        bf1[3] = half_btf(cospi[48], bf0[3], -cospi[16], bf0[2], cos_bit);
        bf1[4] = bf0[4] + bf0[5];
        bf1[5] = -bf0[5] + bf0[4];
        bf1[6] = -bf0[6] + bf0[7];
        bf1[7] = bf0[7] + bf0[6];
        bf1[8] = bf0[8];
        bf1[9] = half_btf(-cospi[16], bf0[9], cospi[48], bf0[14], cos_bit);
        bf1[10] = half_btf(-cospi[48], bf0[10], -cospi[16], bf0[13], cos_bit);
        bf1[11] = bf0[11];
        bf1[12] = bf0[12];
        bf1[13] = half_btf(cospi[48], bf0[13], -cospi[16], bf0[10], cos_bit);
        bf1[14] = half_btf(cospi[16], bf0[14], cospi[48], bf0[9], cos_bit);
        bf1[15] = bf0[15];
        bf1[16] = bf0[16] + bf0[19];
        bf1[17] = bf0[17] + bf0[18];
        bf1[18] = -bf0[18] + bf0[17];
        bf1[19] = -bf0[19] + bf0[16];
        bf1[20] = -bf0[20] + bf0[23];
        bf1[21] = -bf0[21] + bf0[22];
        bf1[22] = bf0[22] + bf0[21];
        bf1[23] = bf0[23] + bf0[20];
        bf1[24] = bf0[24] + bf0[27];
        bf1[25] = bf0[25] + bf0[26];
        bf1[26] = -bf0[26] + bf0[25];
        bf1[27] = -bf0[27] + bf0[24];
        bf1[28] = -bf0[28] + bf0[31];
        bf1[29] = -bf0[29] + bf0[30];
        bf1[30] = bf0[30] + bf0[29];
        bf1[31] = bf0[31] + bf0[28];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 6
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = bf0[2];
        bf1[3] = bf0[3];
        bf1[4] = half_btf(cospi[56], bf0[4], cospi[8], bf0[7], cos_bit);
        bf1[5] = half_btf(cospi[24], bf0[5], cospi[40], bf0[6], cos_bit);
        bf1[6] = half_btf(cospi[24], bf0[6], -cospi[40], bf0[5], cos_bit);
        bf1[7] = half_btf(cospi[56], bf0[7], -cospi[8], bf0[4], cos_bit);
        bf1[8] = bf0[8] + bf0[9];
        bf1[9] = -bf0[9] + bf0[8];
        bf1[10] = -bf0[10] + bf0[11];
        bf1[11] = bf0[11] + bf0[10];
        bf1[12] = bf0[12] + bf0[13];
        bf1[13] = -bf0[13] + bf0[12];
        bf1[14] = -bf0[14] + bf0[15];
        bf1[15] = bf0[15] + bf0[14];
        bf1[16] = bf0[16];
        bf1[17] = half_btf(-cospi[8], bf0[17], cospi[56], bf0[30], cos_bit);
        bf1[18] = half_btf(-cospi[56], bf0[18], -cospi[8], bf0[29], cos_bit);
        bf1[19] = bf0[19];
        bf1[20] = bf0[20];
        bf1[21] = half_btf(-cospi[40], bf0[21], cospi[24], bf0[26], cos_bit);
        bf1[22] = half_btf(-cospi[24], bf0[22], -cospi[40], bf0[25], cos_bit);
        bf1[23] = bf0[23];
        bf1[24] = bf0[24];
        bf1[25] = half_btf(cospi[24], bf0[25], -cospi[40], bf0[22], cos_bit);
        bf1[26] = half_btf(cospi[40], bf0[26], cospi[24], bf0[21], cos_bit);
        bf1[27] = bf0[27];
        bf1[28] = bf0[28];
        bf1[29] = half_btf(cospi[56], bf0[29], -cospi[8], bf0[18], cos_bit);
        bf1[30] = half_btf(cospi[8], bf0[30], cospi[56], bf0[17], cos_bit);
        bf1[31] = bf0[31];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 7
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = bf0[2];
        bf1[3] = bf0[3];
        bf1[4] = bf0[4];
        bf1[5] = bf0[5];
        bf1[6] = bf0[6];
        bf1[7] = bf0[7];
        bf1[8] = half_btf(cospi[60], bf0[8], cospi[4], bf0[15], cos_bit);
        bf1[9] = half_btf(cospi[28], bf0[9], cospi[36], bf0[14], cos_bit);
        bf1[10] = half_btf(cospi[44], bf0[10], cospi[20], bf0[13], cos_bit);
        bf1[11] = half_btf(cospi[12], bf0[11], cospi[52], bf0[12], cos_bit);
        bf1[12] = half_btf(cospi[12], bf0[12], -cospi[52], bf0[11], cos_bit);
        bf1[13] = half_btf(cospi[44], bf0[13], -cospi[20], bf0[10], cos_bit);
        bf1[14] = half_btf(cospi[28], bf0[14], -cospi[36], bf0[9], cos_bit);
        bf1[15] = half_btf(cospi[60], bf0[15], -cospi[4], bf0[8], cos_bit);
        bf1[16] = bf0[16] + bf0[17];
        bf1[17] = -bf0[17] + bf0[16];
        bf1[18] = -bf0[18] + bf0[19];
        bf1[19] = bf0[19] + bf0[18];
        bf1[20] = bf0[20] + bf0[21];
        bf1[21] = -bf0[21] + bf0[20];
        bf1[22] = -bf0[22] + bf0[23];
        bf1[23] = bf0[23] + bf0[22];
        bf1[24] = bf0[24] + bf0[25];
        bf1[25] = -bf0[25] + bf0[24];
        bf1[26] = -bf0[26] + bf0[27];
        bf1[27] = bf0[27] + bf0[26];
        bf1[28] = bf0[28] + bf0[29];
        bf1[29] = -bf0[29] + bf0[28];
        bf1[30] = -bf0[30] + bf0[31];
        bf1[31] = bf0[31] + bf0[30];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 8
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = bf0[2];
        bf1[3] = bf0[3];
        bf1[4] = bf0[4];
        bf1[5] = bf0[5];
        bf1[6] = bf0[6];
        bf1[7] = bf0[7];
        bf1[8] = bf0[8];
        bf1[9] = bf0[9];
        bf1[10] = bf0[10];
        bf1[11] = bf0[11];
        bf1[12] = bf0[12];
        bf1[13] = bf0[13];
        bf1[14] = bf0[14];
        bf1[15] = bf0[15];
        bf1[16] = half_btf(cospi[62], bf0[16], cospi[2], bf0[31], cos_bit);
        bf1[17] = half_btf(cospi[30], bf0[17], cospi[34], bf0[30], cos_bit);
        bf1[18] = half_btf(cospi[46], bf0[18], cospi[18], bf0[29], cos_bit);
        bf1[19] = half_btf(cospi[14], bf0[19], cospi[50], bf0[28], cos_bit);
        bf1[20] = half_btf(cospi[54], bf0[20], cospi[10], bf0[27], cos_bit);
        bf1[21] = half_btf(cospi[22], bf0[21], cospi[42], bf0[26], cos_bit);
        bf1[22] = half_btf(cospi[38], bf0[22], cospi[26], bf0[25], cos_bit);
        bf1[23] = half_btf(cospi[6], bf0[23], cospi[58], bf0[24], cos_bit);
        bf1[24] = half_btf(cospi[6], bf0[24], -cospi[58], bf0[23], cos_bit);
        bf1[25] = half_btf(cospi[38], bf0[25], -cospi[26], bf0[22], cos_bit);
        bf1[26] = half_btf(cospi[22], bf0[26], -cospi[42], bf0[21], cos_bit);
        bf1[27] = half_btf(cospi[54], bf0[27], -cospi[10], bf0[20], cos_bit);
        bf1[28] = half_btf(cospi[14], bf0[28], -cospi[50], bf0[19], cos_bit);
        bf1[29] = half_btf(cospi[46], bf0[29], -cospi[18], bf0[18], cos_bit);
        bf1[30] = half_btf(cospi[30], bf0[30], -cospi[34], bf0[17], cos_bit);
        bf1[31] = half_btf(cospi[62], bf0[31], -cospi[2], bf0[16], cos_bit);
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 9
        stage++;
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0];
        bf1[1] = bf0[16];
        bf1[2] = bf0[8];
        bf1[3] = bf0[24];
        bf1[4] = bf0[4];
        bf1[5] = bf0[20];
        bf1[6] = bf0[12];
        bf1[7] = bf0[28];
        bf1[8] = bf0[2];
        bf1[9] = bf0[18];
        bf1[10] = bf0[10];
        bf1[11] = bf0[26];
        bf1[12] = bf0[6];
        bf1[13] = bf0[22];
        bf1[14] = bf0[14];
        bf1[15] = bf0[30];
        bf1[16] = bf0[1];
        bf1[17] = bf0[17];
        bf1[18] = bf0[9];
        bf1[19] = bf0[25];
        bf1[20] = bf0[5];
        bf1[21] = bf0[21];
        bf1[22] = bf0[13];
        bf1[23] = bf0[29];
        bf1[24] = bf0[3];
        bf1[25] = bf0[19];
        bf1[26] = bf0[11];
        bf1[27] = bf0[27];
        bf1[28] = bf0[7];
        bf1[29] = bf0[23];
        bf1[30] = bf0[15];
        bf1[31] = bf0[31];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);
    }

    internal static void av1_fadst4(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
        Ptr<sbyte> stage_range)
    {
        int bit = cos_bit;
        ReadOnlySpan<int> sinpi = sinpi_arr(bit).AsSpan();
        int x0, x1, x2, x3;
        int s0, s1, s2, s3, s4, s5, s6, s7;

        // stage 0
        av1_range_check_buf(0, input, input, 4, stage_range[0]);
        x0 = input[0];
        x1 = input[1];
        x2 = input[2];
        x3 = input[3];

        if ((x0 | x1 | x2 | x3) == 0)
        {
            output[0] = output[1] = output[2] = output[3] = 0;
            return;
        }

        // stage 1
        s0 = range_check_value(sinpi[1] * x0, (sbyte)(bit + stage_range[1]));
        s1 = range_check_value(sinpi[4] * x0, (sbyte)(bit + stage_range[1]));
        s2 = range_check_value(sinpi[2] * x1, (sbyte)(bit + stage_range[1]));
        s3 = range_check_value(sinpi[1] * x1, (sbyte)(bit + stage_range[1]));
        s4 = range_check_value(sinpi[3] * x2, (sbyte)(bit + stage_range[1]));
        s5 = range_check_value(sinpi[4] * x3, (sbyte)(bit + stage_range[1]));
        s6 = range_check_value(sinpi[2] * x3, (sbyte)(bit + stage_range[1]));
        s7 = range_check_value(x0 + x1, stage_range[1]);

        // stage 2
        s7 = range_check_value(s7 - x3, stage_range[2]);

        // stage 3
        x0 = range_check_value(s0 + s2, (sbyte)(bit + stage_range[3]));
        x1 = range_check_value(sinpi[3] * s7, (sbyte)(bit + stage_range[3]));
        x2 = range_check_value(s1 - s3, (sbyte)(bit + stage_range[3]));
        x3 = range_check_value(s4, (sbyte)(bit + stage_range[3]));

        // stage 4
        x0 = range_check_value(x0 + s5, (sbyte)(bit + stage_range[4]));
        x2 = range_check_value(x2 + s6, (sbyte)(bit + stage_range[4]));

        // stage 5
        s0 = range_check_value(x0 + x3, (sbyte)(bit + stage_range[5]));
        s1 = range_check_value(x1, (sbyte)(bit + stage_range[5]));
        s2 = range_check_value(x2 - x3, (sbyte)(bit + stage_range[5]));
        s3 = range_check_value(x2 - x0, (sbyte)(bit + stage_range[5]));

        // stage 6
        s3 = range_check_value(s3 + x3, (sbyte)(bit + stage_range[6]));

        // 1-D transform scaling factor is sqrt(2).
        output[0] = round_shift(s0, bit);
        output[1] = round_shift(s1, bit);
        output[2] = round_shift(s2, bit);
        output[3] = round_shift(s3, bit);
        av1_range_check_buf(6, input, output, 4, stage_range[6]);
    }

    internal static void av1_fadst8(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
        Ptr<sbyte> stage_range)
    {
        const int size = 8;
        ReadOnlySpan<int> cospi;

        int stage = 0;
        scoped Span<int> bf0, bf1;
        Span<int> step = stackalloc int[8];

        // stage 0;
        av1_range_check_buf(stage, input, input, size, stage_range[stage]);

        // stage 1;
        stage++;
        Debug.Assert(output != input);
        bf1 = output.AsSpan(size);
        bf1[0] = input[0];
        bf1[1] = -input[7];
        bf1[2] = -input[3];
        bf1[3] = input[4];
        bf1[4] = -input[1];
        bf1[5] = input[6];
        bf1[6] = input[2];
        bf1[7] = -input[5];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 2
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = half_btf(cospi[32], bf0[2], cospi[32], bf0[3], cos_bit);
        bf1[3] = half_btf(cospi[32], bf0[2], -cospi[32], bf0[3], cos_bit);
        bf1[4] = bf0[4];
        bf1[5] = bf0[5];
        bf1[6] = half_btf(cospi[32], bf0[6], cospi[32], bf0[7], cos_bit);
        bf1[7] = half_btf(cospi[32], bf0[6], -cospi[32], bf0[7], cos_bit);
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 3
        stage++;
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0] + bf0[2];
        bf1[1] = bf0[1] + bf0[3];
        bf1[2] = bf0[0] - bf0[2];
        bf1[3] = bf0[1] - bf0[3];
        bf1[4] = bf0[4] + bf0[6];
        bf1[5] = bf0[5] + bf0[7];
        bf1[6] = bf0[4] - bf0[6];
        bf1[7] = bf0[5] - bf0[7];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 4
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = bf0[2];
        bf1[3] = bf0[3];
        bf1[4] = half_btf(cospi[16], bf0[4], cospi[48], bf0[5], cos_bit);
        bf1[5] = half_btf(cospi[48], bf0[4], -cospi[16], bf0[5], cos_bit);
        bf1[6] = half_btf(-cospi[48], bf0[6], cospi[16], bf0[7], cos_bit);
        bf1[7] = half_btf(cospi[16], bf0[6], cospi[48], bf0[7], cos_bit);
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 5
        stage++;
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0] + bf0[4];
        bf1[1] = bf0[1] + bf0[5];
        bf1[2] = bf0[2] + bf0[6];
        bf1[3] = bf0[3] + bf0[7];
        bf1[4] = bf0[0] - bf0[4];
        bf1[5] = bf0[1] - bf0[5];
        bf1[6] = bf0[2] - bf0[6];
        bf1[7] = bf0[3] - bf0[7];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 6
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = half_btf(cospi[4], bf0[0], cospi[60], bf0[1], cos_bit);
        bf1[1] = half_btf(cospi[60], bf0[0], -cospi[4], bf0[1], cos_bit);
        bf1[2] = half_btf(cospi[20], bf0[2], cospi[44], bf0[3], cos_bit);
        bf1[3] = half_btf(cospi[44], bf0[2], -cospi[20], bf0[3], cos_bit);
        bf1[4] = half_btf(cospi[36], bf0[4], cospi[28], bf0[5], cos_bit);
        bf1[5] = half_btf(cospi[28], bf0[4], -cospi[36], bf0[5], cos_bit);
        bf1[6] = half_btf(cospi[52], bf0[6], cospi[12], bf0[7], cos_bit);
        bf1[7] = half_btf(cospi[12], bf0[6], -cospi[52], bf0[7], cos_bit);
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 7
        stage++;
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[1];
        bf1[1] = bf0[6];
        bf1[2] = bf0[3];
        bf1[3] = bf0[4];
        bf1[4] = bf0[5];
        bf1[5] = bf0[2];
        bf1[6] = bf0[7];
        bf1[7] = bf0[0];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);
    }

    internal static void av1_fadst16(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
        Ptr<sbyte> stage_range)
    {
        const int size = 16;
        ReadOnlySpan<int> cospi;

        int stage = 0;
        scoped Span<int> bf0, bf1;
        Span<int> step = stackalloc int[16];

        // stage 0;
        av1_range_check_buf(stage, input, input, size, stage_range[stage]);

        // stage 1;
        stage++;
        Debug.Assert(output != input);
        bf1 = output.AsSpan(size);
        bf1[0] = input[0];
        bf1[1] = -input[15];
        bf1[2] = -input[7];
        bf1[3] = input[8];
        bf1[4] = -input[3];
        bf1[5] = input[12];
        bf1[6] = input[4];
        bf1[7] = -input[11];
        bf1[8] = -input[1];
        bf1[9] = input[14];
        bf1[10] = input[6];
        bf1[11] = -input[9];
        bf1[12] = input[2];
        bf1[13] = -input[13];
        bf1[14] = -input[5];
        bf1[15] = input[10];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 2
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = half_btf(cospi[32], bf0[2], cospi[32], bf0[3], cos_bit);
        bf1[3] = half_btf(cospi[32], bf0[2], -cospi[32], bf0[3], cos_bit);
        bf1[4] = bf0[4];
        bf1[5] = bf0[5];
        bf1[6] = half_btf(cospi[32], bf0[6], cospi[32], bf0[7], cos_bit);
        bf1[7] = half_btf(cospi[32], bf0[6], -cospi[32], bf0[7], cos_bit);
        bf1[8] = bf0[8];
        bf1[9] = bf0[9];
        bf1[10] = half_btf(cospi[32], bf0[10], cospi[32], bf0[11], cos_bit);
        bf1[11] = half_btf(cospi[32], bf0[10], -cospi[32], bf0[11], cos_bit);
        bf1[12] = bf0[12];
        bf1[13] = bf0[13];
        bf1[14] = half_btf(cospi[32], bf0[14], cospi[32], bf0[15], cos_bit);
        bf1[15] = half_btf(cospi[32], bf0[14], -cospi[32], bf0[15], cos_bit);
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 3
        stage++;
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0] + bf0[2];
        bf1[1] = bf0[1] + bf0[3];
        bf1[2] = bf0[0] - bf0[2];
        bf1[3] = bf0[1] - bf0[3];
        bf1[4] = bf0[4] + bf0[6];
        bf1[5] = bf0[5] + bf0[7];
        bf1[6] = bf0[4] - bf0[6];
        bf1[7] = bf0[5] - bf0[7];
        bf1[8] = bf0[8] + bf0[10];
        bf1[9] = bf0[9] + bf0[11];
        bf1[10] = bf0[8] - bf0[10];
        bf1[11] = bf0[9] - bf0[11];
        bf1[12] = bf0[12] + bf0[14];
        bf1[13] = bf0[13] + bf0[15];
        bf1[14] = bf0[12] - bf0[14];
        bf1[15] = bf0[13] - bf0[15];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 4
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = bf0[2];
        bf1[3] = bf0[3];
        bf1[4] = half_btf(cospi[16], bf0[4], cospi[48], bf0[5], cos_bit);
        bf1[5] = half_btf(cospi[48], bf0[4], -cospi[16], bf0[5], cos_bit);
        bf1[6] = half_btf(-cospi[48], bf0[6], cospi[16], bf0[7], cos_bit);
        bf1[7] = half_btf(cospi[16], bf0[6], cospi[48], bf0[7], cos_bit);
        bf1[8] = bf0[8];
        bf1[9] = bf0[9];
        bf1[10] = bf0[10];
        bf1[11] = bf0[11];
        bf1[12] = half_btf(cospi[16], bf0[12], cospi[48], bf0[13], cos_bit);
        bf1[13] = half_btf(cospi[48], bf0[12], -cospi[16], bf0[13], cos_bit);
        bf1[14] = half_btf(-cospi[48], bf0[14], cospi[16], bf0[15], cos_bit);
        bf1[15] = half_btf(cospi[16], bf0[14], cospi[48], bf0[15], cos_bit);
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 5
        stage++;
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0] + bf0[4];
        bf1[1] = bf0[1] + bf0[5];
        bf1[2] = bf0[2] + bf0[6];
        bf1[3] = bf0[3] + bf0[7];
        bf1[4] = bf0[0] - bf0[4];
        bf1[5] = bf0[1] - bf0[5];
        bf1[6] = bf0[2] - bf0[6];
        bf1[7] = bf0[3] - bf0[7];
        bf1[8] = bf0[8] + bf0[12];
        bf1[9] = bf0[9] + bf0[13];
        bf1[10] = bf0[10] + bf0[14];
        bf1[11] = bf0[11] + bf0[15];
        bf1[12] = bf0[8] - bf0[12];
        bf1[13] = bf0[9] - bf0[13];
        bf1[14] = bf0[10] - bf0[14];
        bf1[15] = bf0[11] - bf0[15];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 6
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = bf0[0];
        bf1[1] = bf0[1];
        bf1[2] = bf0[2];
        bf1[3] = bf0[3];
        bf1[4] = bf0[4];
        bf1[5] = bf0[5];
        bf1[6] = bf0[6];
        bf1[7] = bf0[7];
        bf1[8] = half_btf(cospi[8], bf0[8], cospi[56], bf0[9], cos_bit);
        bf1[9] = half_btf(cospi[56], bf0[8], -cospi[8], bf0[9], cos_bit);
        bf1[10] = half_btf(cospi[40], bf0[10], cospi[24], bf0[11], cos_bit);
        bf1[11] = half_btf(cospi[24], bf0[10], -cospi[40], bf0[11], cos_bit);
        bf1[12] = half_btf(-cospi[56], bf0[12], cospi[8], bf0[13], cos_bit);
        bf1[13] = half_btf(cospi[8], bf0[12], cospi[56], bf0[13], cos_bit);
        bf1[14] = half_btf(-cospi[24], bf0[14], cospi[40], bf0[15], cos_bit);
        bf1[15] = half_btf(cospi[40], bf0[14], cospi[24], bf0[15], cos_bit);
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 7
        stage++;
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[0] + bf0[8];
        bf1[1] = bf0[1] + bf0[9];
        bf1[2] = bf0[2] + bf0[10];
        bf1[3] = bf0[3] + bf0[11];
        bf1[4] = bf0[4] + bf0[12];
        bf1[5] = bf0[5] + bf0[13];
        bf1[6] = bf0[6] + bf0[14];
        bf1[7] = bf0[7] + bf0[15];
        bf1[8] = bf0[0] - bf0[8];
        bf1[9] = bf0[1] - bf0[9];
        bf1[10] = bf0[2] - bf0[10];
        bf1[11] = bf0[3] - bf0[11];
        bf1[12] = bf0[4] - bf0[12];
        bf1[13] = bf0[5] - bf0[13];
        bf1[14] = bf0[6] - bf0[14];
        bf1[15] = bf0[7] - bf0[15];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 8
        stage++;
        cospi = cospi_arr(cos_bit).AsSpan();
        bf0 = output.AsSpan(size);
        bf1 = step;
        bf1[0] = half_btf(cospi[2], bf0[0], cospi[62], bf0[1], cos_bit);
        bf1[1] = half_btf(cospi[62], bf0[0], -cospi[2], bf0[1], cos_bit);
        bf1[2] = half_btf(cospi[10], bf0[2], cospi[54], bf0[3], cos_bit);
        bf1[3] = half_btf(cospi[54], bf0[2], -cospi[10], bf0[3], cos_bit);
        bf1[4] = half_btf(cospi[18], bf0[4], cospi[46], bf0[5], cos_bit);
        bf1[5] = half_btf(cospi[46], bf0[4], -cospi[18], bf0[5], cos_bit);
        bf1[6] = half_btf(cospi[26], bf0[6], cospi[38], bf0[7], cos_bit);
        bf1[7] = half_btf(cospi[38], bf0[6], -cospi[26], bf0[7], cos_bit);
        bf1[8] = half_btf(cospi[34], bf0[8], cospi[30], bf0[9], cos_bit);
        bf1[9] = half_btf(cospi[30], bf0[8], -cospi[34], bf0[9], cos_bit);
        bf1[10] = half_btf(cospi[42], bf0[10], cospi[22], bf0[11], cos_bit);
        bf1[11] = half_btf(cospi[22], bf0[10], -cospi[42], bf0[11], cos_bit);
        bf1[12] = half_btf(cospi[50], bf0[12], cospi[14], bf0[13], cos_bit);
        bf1[13] = half_btf(cospi[14], bf0[12], -cospi[50], bf0[13], cos_bit);
        bf1[14] = half_btf(cospi[58], bf0[14], cospi[6], bf0[15], cos_bit);
        bf1[15] = half_btf(cospi[6], bf0[14], -cospi[58], bf0[15], cos_bit);
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);

        // stage 9
        stage++;
        bf0 = step;
        bf1 = output.AsSpan(size);
        bf1[0] = bf0[1];
        bf1[1] = bf0[14];
        bf1[2] = bf0[3];
        bf1[3] = bf0[12];
        bf1[4] = bf0[5];
        bf1[5] = bf0[10];
        bf1[6] = bf0[7];
        bf1[7] = bf0[8];
        bf1[8] = bf0[9];
        bf1[9] = bf0[6];
        bf1[10] = bf0[11];
        bf1[11] = bf0[4];
        bf1[12] = bf0[13];
        bf1[13] = bf0[2];
        bf1[14] = bf0[15];
        bf1[15] = bf0[0];
        av1_range_check_buf(stage, input, bf1, size, stage_range[stage]);
    }

    // [port] The identity transforms never ran in the subset (IDTX and the H_*/V_* types are
    // only used for screen content or by the RD tx-type search), but they are four tiny
    // functions that make the 2-D transform total over TX_TYPE, and are covered by the
    // differential tests.
    internal static void av1_fidentity4_c(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
        Ptr<sbyte> stage_range)
    {
        _ = cos_bit;
        for (int i = 0; i < 4; ++i)
            output[i] = round_shift((long)input[i] * NewSqrt2, NewSqrt2Bits);
        Debug.Assert(stage_range[0] + NewSqrt2Bits <= 32);
        av1_range_check_buf(0, input, output, 4, stage_range[0]);
    }

    internal static void av1_fidentity8_c(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
        Ptr<sbyte> stage_range)
    {
        _ = cos_bit;
        for (int i = 0; i < 8; ++i) output[i] = input[i] * 2;
        av1_range_check_buf(0, input, output, 8, stage_range[0]);
    }

    internal static void av1_fidentity16_c(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
        Ptr<sbyte> stage_range)
    {
        _ = cos_bit;
        for (int i = 0; i < 16; ++i)
            output[i] = round_shift((long)input[i] * 2 * NewSqrt2, NewSqrt2Bits);
        Debug.Assert(stage_range[0] + NewSqrt2Bits <= 32);
        av1_range_check_buf(0, input, output, 16, stage_range[0]);
    }

    internal static void av1_fidentity32_c(Ptr<int> input, Ptr<int> output, sbyte cos_bit,
        Ptr<sbyte> stage_range)
    {
        _ = cos_bit;
        for (int i = 0; i < 32; ++i) output[i] = input[i] * 4;
        av1_range_check_buf(0, input, output, 32, stage_range[0]);
    }

    // [subset] av1_fdct64: no 64-point transforms (TX64 is disabled in the subset).
}
