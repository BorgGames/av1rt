using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.intrapred;
using static Av1rt.Aom.intrapred_asm_sse2;
using static Av1rt.Aom.intrapred_avx2;
using static Av1rt.Aom.intrapred_sse2;
using static Av1rt.Aom.intrapred_ssse3;

namespace Av1rt.Tests.Simd.S6;

/// <summary>
/// aom_dsp/x86/intrapred_{asm_sse2,sse2,ssse3,avx2}: every dispatched intra predictor (dc, dc_top,
/// dc_left, dc_128, v, h, smooth x 19 sizes), the libaom best x86 variant against the C port.
/// </summary>
public class IntraPredSimdTests
{
    private sealed record Kernel(string Name, int Bw, int Bh, intra_pred_fn Vec, intra_pred_fn C,
                                 intra_pred_fn Dispatch);

    // The libaom best x86 variant per rtcd_best.txt ("-" = asm sse2, ported as intrinsics).
    private static readonly Kernel[] Kernels =
    {
        new("aom_dc_predictor_4x4_sse2", 4, 4, aom_dc_predictor_4x4_sse2, aom_dc_predictor_4x4_c, aom_dc_predictor_4x4),
        new("aom_dc_predictor_8x8_sse2", 8, 8, aom_dc_predictor_8x8_sse2, aom_dc_predictor_8x8_c, aom_dc_predictor_8x8),
        new("aom_dc_predictor_16x16_sse2", 16, 16, aom_dc_predictor_16x16_sse2, aom_dc_predictor_16x16_c, aom_dc_predictor_16x16),
        new("aom_dc_predictor_32x32_avx2", 32, 32, aom_dc_predictor_32x32_avx2, aom_dc_predictor_32x32_c, aom_dc_predictor_32x32),
        new("aom_dc_predictor_64x64_avx2", 64, 64, aom_dc_predictor_64x64_avx2, aom_dc_predictor_64x64_c, aom_dc_predictor_64x64),
        new("aom_dc_predictor_4x8_sse2", 4, 8, aom_dc_predictor_4x8_sse2, aom_dc_predictor_4x8_c, aom_dc_predictor_4x8),
        new("aom_dc_predictor_8x4_sse2", 8, 4, aom_dc_predictor_8x4_sse2, aom_dc_predictor_8x4_c, aom_dc_predictor_8x4),
        new("aom_dc_predictor_8x16_sse2", 8, 16, aom_dc_predictor_8x16_sse2, aom_dc_predictor_8x16_c, aom_dc_predictor_8x16),
        new("aom_dc_predictor_16x8_sse2", 16, 8, aom_dc_predictor_16x8_sse2, aom_dc_predictor_16x8_c, aom_dc_predictor_16x8),
        new("aom_dc_predictor_16x32_sse2", 16, 32, aom_dc_predictor_16x32_sse2, aom_dc_predictor_16x32_c, aom_dc_predictor_16x32),
        new("aom_dc_predictor_32x16_avx2", 32, 16, aom_dc_predictor_32x16_avx2, aom_dc_predictor_32x16_c, aom_dc_predictor_32x16),
        new("aom_dc_predictor_32x64_avx2", 32, 64, aom_dc_predictor_32x64_avx2, aom_dc_predictor_32x64_c, aom_dc_predictor_32x64),
        new("aom_dc_predictor_64x32_avx2", 64, 32, aom_dc_predictor_64x32_avx2, aom_dc_predictor_64x32_c, aom_dc_predictor_64x32),
        new("aom_dc_predictor_4x16_sse2", 4, 16, aom_dc_predictor_4x16_sse2, aom_dc_predictor_4x16_c, aom_dc_predictor_4x16),
        new("aom_dc_predictor_16x4_sse2", 16, 4, aom_dc_predictor_16x4_sse2, aom_dc_predictor_16x4_c, aom_dc_predictor_16x4),
        new("aom_dc_predictor_8x32_sse2", 8, 32, aom_dc_predictor_8x32_sse2, aom_dc_predictor_8x32_c, aom_dc_predictor_8x32),
        new("aom_dc_predictor_32x8_sse2", 32, 8, aom_dc_predictor_32x8_sse2, aom_dc_predictor_32x8_c, aom_dc_predictor_32x8),
        new("aom_dc_predictor_16x64_sse2", 16, 64, aom_dc_predictor_16x64_sse2, aom_dc_predictor_16x64_c, aom_dc_predictor_16x64),
        new("aom_dc_predictor_64x16_avx2", 64, 16, aom_dc_predictor_64x16_avx2, aom_dc_predictor_64x16_c, aom_dc_predictor_64x16),
        new("aom_dc_top_predictor_4x4_sse2", 4, 4, aom_dc_top_predictor_4x4_sse2, aom_dc_top_predictor_4x4_c, aom_dc_top_predictor_4x4),
        new("aom_dc_top_predictor_8x8_sse2", 8, 8, aom_dc_top_predictor_8x8_sse2, aom_dc_top_predictor_8x8_c, aom_dc_top_predictor_8x8),
        new("aom_dc_top_predictor_16x16_sse2", 16, 16, aom_dc_top_predictor_16x16_sse2, aom_dc_top_predictor_16x16_c, aom_dc_top_predictor_16x16),
        new("aom_dc_top_predictor_32x32_avx2", 32, 32, aom_dc_top_predictor_32x32_avx2, aom_dc_top_predictor_32x32_c, aom_dc_top_predictor_32x32),
        new("aom_dc_top_predictor_64x64_avx2", 64, 64, aom_dc_top_predictor_64x64_avx2, aom_dc_top_predictor_64x64_c, aom_dc_top_predictor_64x64),
        new("aom_dc_top_predictor_4x8_sse2", 4, 8, aom_dc_top_predictor_4x8_sse2, aom_dc_top_predictor_4x8_c, aom_dc_top_predictor_4x8),
        new("aom_dc_top_predictor_8x4_sse2", 8, 4, aom_dc_top_predictor_8x4_sse2, aom_dc_top_predictor_8x4_c, aom_dc_top_predictor_8x4),
        new("aom_dc_top_predictor_8x16_sse2", 8, 16, aom_dc_top_predictor_8x16_sse2, aom_dc_top_predictor_8x16_c, aom_dc_top_predictor_8x16),
        new("aom_dc_top_predictor_16x8_sse2", 16, 8, aom_dc_top_predictor_16x8_sse2, aom_dc_top_predictor_16x8_c, aom_dc_top_predictor_16x8),
        new("aom_dc_top_predictor_16x32_sse2", 16, 32, aom_dc_top_predictor_16x32_sse2, aom_dc_top_predictor_16x32_c, aom_dc_top_predictor_16x32),
        new("aom_dc_top_predictor_32x16_avx2", 32, 16, aom_dc_top_predictor_32x16_avx2, aom_dc_top_predictor_32x16_c, aom_dc_top_predictor_32x16),
        new("aom_dc_top_predictor_32x64_avx2", 32, 64, aom_dc_top_predictor_32x64_avx2, aom_dc_top_predictor_32x64_c, aom_dc_top_predictor_32x64),
        new("aom_dc_top_predictor_64x32_avx2", 64, 32, aom_dc_top_predictor_64x32_avx2, aom_dc_top_predictor_64x32_c, aom_dc_top_predictor_64x32),
        new("aom_dc_top_predictor_4x16_sse2", 4, 16, aom_dc_top_predictor_4x16_sse2, aom_dc_top_predictor_4x16_c, aom_dc_top_predictor_4x16),
        new("aom_dc_top_predictor_16x4_sse2", 16, 4, aom_dc_top_predictor_16x4_sse2, aom_dc_top_predictor_16x4_c, aom_dc_top_predictor_16x4),
        new("aom_dc_top_predictor_8x32_sse2", 8, 32, aom_dc_top_predictor_8x32_sse2, aom_dc_top_predictor_8x32_c, aom_dc_top_predictor_8x32),
        new("aom_dc_top_predictor_32x8_sse2", 32, 8, aom_dc_top_predictor_32x8_sse2, aom_dc_top_predictor_32x8_c, aom_dc_top_predictor_32x8),
        new("aom_dc_top_predictor_16x64_sse2", 16, 64, aom_dc_top_predictor_16x64_sse2, aom_dc_top_predictor_16x64_c, aom_dc_top_predictor_16x64),
        new("aom_dc_top_predictor_64x16_avx2", 64, 16, aom_dc_top_predictor_64x16_avx2, aom_dc_top_predictor_64x16_c, aom_dc_top_predictor_64x16),
        new("aom_dc_left_predictor_4x4_sse2", 4, 4, aom_dc_left_predictor_4x4_sse2, aom_dc_left_predictor_4x4_c, aom_dc_left_predictor_4x4),
        new("aom_dc_left_predictor_8x8_sse2", 8, 8, aom_dc_left_predictor_8x8_sse2, aom_dc_left_predictor_8x8_c, aom_dc_left_predictor_8x8),
        new("aom_dc_left_predictor_16x16_sse2", 16, 16, aom_dc_left_predictor_16x16_sse2, aom_dc_left_predictor_16x16_c, aom_dc_left_predictor_16x16),
        new("aom_dc_left_predictor_32x32_avx2", 32, 32, aom_dc_left_predictor_32x32_avx2, aom_dc_left_predictor_32x32_c, aom_dc_left_predictor_32x32),
        new("aom_dc_left_predictor_64x64_avx2", 64, 64, aom_dc_left_predictor_64x64_avx2, aom_dc_left_predictor_64x64_c, aom_dc_left_predictor_64x64),
        new("aom_dc_left_predictor_4x8_sse2", 4, 8, aom_dc_left_predictor_4x8_sse2, aom_dc_left_predictor_4x8_c, aom_dc_left_predictor_4x8),
        new("aom_dc_left_predictor_8x4_sse2", 8, 4, aom_dc_left_predictor_8x4_sse2, aom_dc_left_predictor_8x4_c, aom_dc_left_predictor_8x4),
        new("aom_dc_left_predictor_8x16_sse2", 8, 16, aom_dc_left_predictor_8x16_sse2, aom_dc_left_predictor_8x16_c, aom_dc_left_predictor_8x16),
        new("aom_dc_left_predictor_16x8_sse2", 16, 8, aom_dc_left_predictor_16x8_sse2, aom_dc_left_predictor_16x8_c, aom_dc_left_predictor_16x8),
        new("aom_dc_left_predictor_16x32_sse2", 16, 32, aom_dc_left_predictor_16x32_sse2, aom_dc_left_predictor_16x32_c, aom_dc_left_predictor_16x32),
        new("aom_dc_left_predictor_32x16_avx2", 32, 16, aom_dc_left_predictor_32x16_avx2, aom_dc_left_predictor_32x16_c, aom_dc_left_predictor_32x16),
        new("aom_dc_left_predictor_32x64_avx2", 32, 64, aom_dc_left_predictor_32x64_avx2, aom_dc_left_predictor_32x64_c, aom_dc_left_predictor_32x64),
        new("aom_dc_left_predictor_64x32_avx2", 64, 32, aom_dc_left_predictor_64x32_avx2, aom_dc_left_predictor_64x32_c, aom_dc_left_predictor_64x32),
        new("aom_dc_left_predictor_4x16_sse2", 4, 16, aom_dc_left_predictor_4x16_sse2, aom_dc_left_predictor_4x16_c, aom_dc_left_predictor_4x16),
        new("aom_dc_left_predictor_16x4_sse2", 16, 4, aom_dc_left_predictor_16x4_sse2, aom_dc_left_predictor_16x4_c, aom_dc_left_predictor_16x4),
        new("aom_dc_left_predictor_8x32_sse2", 8, 32, aom_dc_left_predictor_8x32_sse2, aom_dc_left_predictor_8x32_c, aom_dc_left_predictor_8x32),
        new("aom_dc_left_predictor_32x8_sse2", 32, 8, aom_dc_left_predictor_32x8_sse2, aom_dc_left_predictor_32x8_c, aom_dc_left_predictor_32x8),
        new("aom_dc_left_predictor_16x64_sse2", 16, 64, aom_dc_left_predictor_16x64_sse2, aom_dc_left_predictor_16x64_c, aom_dc_left_predictor_16x64),
        new("aom_dc_left_predictor_64x16_avx2", 64, 16, aom_dc_left_predictor_64x16_avx2, aom_dc_left_predictor_64x16_c, aom_dc_left_predictor_64x16),
        new("aom_dc_128_predictor_4x4_sse2", 4, 4, aom_dc_128_predictor_4x4_sse2, aom_dc_128_predictor_4x4_c, aom_dc_128_predictor_4x4),
        new("aom_dc_128_predictor_8x8_sse2", 8, 8, aom_dc_128_predictor_8x8_sse2, aom_dc_128_predictor_8x8_c, aom_dc_128_predictor_8x8),
        new("aom_dc_128_predictor_16x16_sse2", 16, 16, aom_dc_128_predictor_16x16_sse2, aom_dc_128_predictor_16x16_c, aom_dc_128_predictor_16x16),
        new("aom_dc_128_predictor_32x32_avx2", 32, 32, aom_dc_128_predictor_32x32_avx2, aom_dc_128_predictor_32x32_c, aom_dc_128_predictor_32x32),
        new("aom_dc_128_predictor_64x64_avx2", 64, 64, aom_dc_128_predictor_64x64_avx2, aom_dc_128_predictor_64x64_c, aom_dc_128_predictor_64x64),
        new("aom_dc_128_predictor_4x8_sse2", 4, 8, aom_dc_128_predictor_4x8_sse2, aom_dc_128_predictor_4x8_c, aom_dc_128_predictor_4x8),
        new("aom_dc_128_predictor_8x4_sse2", 8, 4, aom_dc_128_predictor_8x4_sse2, aom_dc_128_predictor_8x4_c, aom_dc_128_predictor_8x4),
        new("aom_dc_128_predictor_8x16_sse2", 8, 16, aom_dc_128_predictor_8x16_sse2, aom_dc_128_predictor_8x16_c, aom_dc_128_predictor_8x16),
        new("aom_dc_128_predictor_16x8_sse2", 16, 8, aom_dc_128_predictor_16x8_sse2, aom_dc_128_predictor_16x8_c, aom_dc_128_predictor_16x8),
        new("aom_dc_128_predictor_16x32_sse2", 16, 32, aom_dc_128_predictor_16x32_sse2, aom_dc_128_predictor_16x32_c, aom_dc_128_predictor_16x32),
        new("aom_dc_128_predictor_32x16_avx2", 32, 16, aom_dc_128_predictor_32x16_avx2, aom_dc_128_predictor_32x16_c, aom_dc_128_predictor_32x16),
        new("aom_dc_128_predictor_32x64_avx2", 32, 64, aom_dc_128_predictor_32x64_avx2, aom_dc_128_predictor_32x64_c, aom_dc_128_predictor_32x64),
        new("aom_dc_128_predictor_64x32_avx2", 64, 32, aom_dc_128_predictor_64x32_avx2, aom_dc_128_predictor_64x32_c, aom_dc_128_predictor_64x32),
        new("aom_dc_128_predictor_4x16_sse2", 4, 16, aom_dc_128_predictor_4x16_sse2, aom_dc_128_predictor_4x16_c, aom_dc_128_predictor_4x16),
        new("aom_dc_128_predictor_16x4_sse2", 16, 4, aom_dc_128_predictor_16x4_sse2, aom_dc_128_predictor_16x4_c, aom_dc_128_predictor_16x4),
        new("aom_dc_128_predictor_8x32_sse2", 8, 32, aom_dc_128_predictor_8x32_sse2, aom_dc_128_predictor_8x32_c, aom_dc_128_predictor_8x32),
        new("aom_dc_128_predictor_32x8_sse2", 32, 8, aom_dc_128_predictor_32x8_sse2, aom_dc_128_predictor_32x8_c, aom_dc_128_predictor_32x8),
        new("aom_dc_128_predictor_16x64_sse2", 16, 64, aom_dc_128_predictor_16x64_sse2, aom_dc_128_predictor_16x64_c, aom_dc_128_predictor_16x64),
        new("aom_dc_128_predictor_64x16_avx2", 64, 16, aom_dc_128_predictor_64x16_avx2, aom_dc_128_predictor_64x16_c, aom_dc_128_predictor_64x16),
        new("aom_v_predictor_4x4_sse2", 4, 4, aom_v_predictor_4x4_sse2, aom_v_predictor_4x4_c, aom_v_predictor_4x4),
        new("aom_v_predictor_8x8_sse2", 8, 8, aom_v_predictor_8x8_sse2, aom_v_predictor_8x8_c, aom_v_predictor_8x8),
        new("aom_v_predictor_16x16_sse2", 16, 16, aom_v_predictor_16x16_sse2, aom_v_predictor_16x16_c, aom_v_predictor_16x16),
        new("aom_v_predictor_32x32_avx2", 32, 32, aom_v_predictor_32x32_avx2, aom_v_predictor_32x32_c, aom_v_predictor_32x32),
        new("aom_v_predictor_64x64_avx2", 64, 64, aom_v_predictor_64x64_avx2, aom_v_predictor_64x64_c, aom_v_predictor_64x64),
        new("aom_v_predictor_4x8_sse2", 4, 8, aom_v_predictor_4x8_sse2, aom_v_predictor_4x8_c, aom_v_predictor_4x8),
        new("aom_v_predictor_8x4_sse2", 8, 4, aom_v_predictor_8x4_sse2, aom_v_predictor_8x4_c, aom_v_predictor_8x4),
        new("aom_v_predictor_8x16_sse2", 8, 16, aom_v_predictor_8x16_sse2, aom_v_predictor_8x16_c, aom_v_predictor_8x16),
        new("aom_v_predictor_16x8_sse2", 16, 8, aom_v_predictor_16x8_sse2, aom_v_predictor_16x8_c, aom_v_predictor_16x8),
        new("aom_v_predictor_16x32_sse2", 16, 32, aom_v_predictor_16x32_sse2, aom_v_predictor_16x32_c, aom_v_predictor_16x32),
        new("aom_v_predictor_32x16_avx2", 32, 16, aom_v_predictor_32x16_avx2, aom_v_predictor_32x16_c, aom_v_predictor_32x16),
        new("aom_v_predictor_32x64_avx2", 32, 64, aom_v_predictor_32x64_avx2, aom_v_predictor_32x64_c, aom_v_predictor_32x64),
        new("aom_v_predictor_64x32_avx2", 64, 32, aom_v_predictor_64x32_avx2, aom_v_predictor_64x32_c, aom_v_predictor_64x32),
        new("aom_v_predictor_4x16_sse2", 4, 16, aom_v_predictor_4x16_sse2, aom_v_predictor_4x16_c, aom_v_predictor_4x16),
        new("aom_v_predictor_16x4_sse2", 16, 4, aom_v_predictor_16x4_sse2, aom_v_predictor_16x4_c, aom_v_predictor_16x4),
        new("aom_v_predictor_8x32_sse2", 8, 32, aom_v_predictor_8x32_sse2, aom_v_predictor_8x32_c, aom_v_predictor_8x32),
        new("aom_v_predictor_32x8_sse2", 32, 8, aom_v_predictor_32x8_sse2, aom_v_predictor_32x8_c, aom_v_predictor_32x8),
        new("aom_v_predictor_16x64_sse2", 16, 64, aom_v_predictor_16x64_sse2, aom_v_predictor_16x64_c, aom_v_predictor_16x64),
        new("aom_v_predictor_64x16_avx2", 64, 16, aom_v_predictor_64x16_avx2, aom_v_predictor_64x16_c, aom_v_predictor_64x16),
        new("aom_h_predictor_4x4_sse2", 4, 4, aom_h_predictor_4x4_sse2, aom_h_predictor_4x4_c, aom_h_predictor_4x4),
        new("aom_h_predictor_8x8_sse2", 8, 8, aom_h_predictor_8x8_sse2, aom_h_predictor_8x8_c, aom_h_predictor_8x8),
        new("aom_h_predictor_16x16_sse2", 16, 16, aom_h_predictor_16x16_sse2, aom_h_predictor_16x16_c, aom_h_predictor_16x16),
        new("aom_h_predictor_32x32_avx2", 32, 32, aom_h_predictor_32x32_avx2, aom_h_predictor_32x32_c, aom_h_predictor_32x32),
        new("aom_h_predictor_64x64_sse2", 64, 64, aom_h_predictor_64x64_sse2, aom_h_predictor_64x64_c, aom_h_predictor_64x64),
        new("aom_h_predictor_4x8_sse2", 4, 8, aom_h_predictor_4x8_sse2, aom_h_predictor_4x8_c, aom_h_predictor_4x8),
        new("aom_h_predictor_8x4_sse2", 8, 4, aom_h_predictor_8x4_sse2, aom_h_predictor_8x4_c, aom_h_predictor_8x4),
        new("aom_h_predictor_8x16_sse2", 8, 16, aom_h_predictor_8x16_sse2, aom_h_predictor_8x16_c, aom_h_predictor_8x16),
        new("aom_h_predictor_16x8_sse2", 16, 8, aom_h_predictor_16x8_sse2, aom_h_predictor_16x8_c, aom_h_predictor_16x8),
        new("aom_h_predictor_16x32_sse2", 16, 32, aom_h_predictor_16x32_sse2, aom_h_predictor_16x32_c, aom_h_predictor_16x32),
        new("aom_h_predictor_32x16_sse2", 32, 16, aom_h_predictor_32x16_sse2, aom_h_predictor_32x16_c, aom_h_predictor_32x16),
        new("aom_h_predictor_32x64_sse2", 32, 64, aom_h_predictor_32x64_sse2, aom_h_predictor_32x64_c, aom_h_predictor_32x64),
        new("aom_h_predictor_64x32_sse2", 64, 32, aom_h_predictor_64x32_sse2, aom_h_predictor_64x32_c, aom_h_predictor_64x32),
        new("aom_h_predictor_4x16_sse2", 4, 16, aom_h_predictor_4x16_sse2, aom_h_predictor_4x16_c, aom_h_predictor_4x16),
        new("aom_h_predictor_16x4_sse2", 16, 4, aom_h_predictor_16x4_sse2, aom_h_predictor_16x4_c, aom_h_predictor_16x4),
        new("aom_h_predictor_8x32_sse2", 8, 32, aom_h_predictor_8x32_sse2, aom_h_predictor_8x32_c, aom_h_predictor_8x32),
        new("aom_h_predictor_32x8_sse2", 32, 8, aom_h_predictor_32x8_sse2, aom_h_predictor_32x8_c, aom_h_predictor_32x8),
        new("aom_h_predictor_16x64_sse2", 16, 64, aom_h_predictor_16x64_sse2, aom_h_predictor_16x64_c, aom_h_predictor_16x64),
        new("aom_h_predictor_64x16_sse2", 64, 16, aom_h_predictor_64x16_sse2, aom_h_predictor_64x16_c, aom_h_predictor_64x16),
        new("aom_smooth_predictor_4x4_ssse3", 4, 4, aom_smooth_predictor_4x4_ssse3, aom_smooth_predictor_4x4_c, aom_smooth_predictor_4x4),
        new("aom_smooth_predictor_8x8_ssse3", 8, 8, aom_smooth_predictor_8x8_ssse3, aom_smooth_predictor_8x8_c, aom_smooth_predictor_8x8),
        new("aom_smooth_predictor_16x16_ssse3", 16, 16, aom_smooth_predictor_16x16_ssse3, aom_smooth_predictor_16x16_c, aom_smooth_predictor_16x16),
        new("aom_smooth_predictor_32x32_ssse3", 32, 32, aom_smooth_predictor_32x32_ssse3, aom_smooth_predictor_32x32_c, aom_smooth_predictor_32x32),
        new("aom_smooth_predictor_64x64_ssse3", 64, 64, aom_smooth_predictor_64x64_ssse3, aom_smooth_predictor_64x64_c, aom_smooth_predictor_64x64),
        new("aom_smooth_predictor_4x8_ssse3", 4, 8, aom_smooth_predictor_4x8_ssse3, aom_smooth_predictor_4x8_c, aom_smooth_predictor_4x8),
        new("aom_smooth_predictor_8x4_ssse3", 8, 4, aom_smooth_predictor_8x4_ssse3, aom_smooth_predictor_8x4_c, aom_smooth_predictor_8x4),
        new("aom_smooth_predictor_8x16_ssse3", 8, 16, aom_smooth_predictor_8x16_ssse3, aom_smooth_predictor_8x16_c, aom_smooth_predictor_8x16),
        new("aom_smooth_predictor_16x8_ssse3", 16, 8, aom_smooth_predictor_16x8_ssse3, aom_smooth_predictor_16x8_c, aom_smooth_predictor_16x8),
        new("aom_smooth_predictor_16x32_ssse3", 16, 32, aom_smooth_predictor_16x32_ssse3, aom_smooth_predictor_16x32_c, aom_smooth_predictor_16x32),
        new("aom_smooth_predictor_32x16_ssse3", 32, 16, aom_smooth_predictor_32x16_ssse3, aom_smooth_predictor_32x16_c, aom_smooth_predictor_32x16),
        new("aom_smooth_predictor_32x64_ssse3", 32, 64, aom_smooth_predictor_32x64_ssse3, aom_smooth_predictor_32x64_c, aom_smooth_predictor_32x64),
        new("aom_smooth_predictor_64x32_ssse3", 64, 32, aom_smooth_predictor_64x32_ssse3, aom_smooth_predictor_64x32_c, aom_smooth_predictor_64x32),
        new("aom_smooth_predictor_4x16_ssse3", 4, 16, aom_smooth_predictor_4x16_ssse3, aom_smooth_predictor_4x16_c, aom_smooth_predictor_4x16),
        new("aom_smooth_predictor_16x4_ssse3", 16, 4, aom_smooth_predictor_16x4_ssse3, aom_smooth_predictor_16x4_c, aom_smooth_predictor_16x4),
        new("aom_smooth_predictor_8x32_ssse3", 8, 32, aom_smooth_predictor_8x32_ssse3, aom_smooth_predictor_8x32_c, aom_smooth_predictor_8x32),
        new("aom_smooth_predictor_32x8_ssse3", 32, 8, aom_smooth_predictor_32x8_ssse3, aom_smooth_predictor_32x8_c, aom_smooth_predictor_32x8),
        new("aom_smooth_predictor_16x64_ssse3", 16, 64, aom_smooth_predictor_16x64_ssse3, aom_smooth_predictor_16x64_c, aom_smooth_predictor_16x64),
        new("aom_smooth_predictor_64x16_ssse3", 64, 16, aom_smooth_predictor_64x16_ssse3, aom_smooth_predictor_64x16_c, aom_smooth_predictor_64x16),
    };

    public static IEnumerable<object[]> Names => Kernels.Select(k => new object[] { k.Name });

    // reconintra.c: `uint8_t left_data[NUM_INTRA_NEIGHBOUR_PIXELS]` (MAX_TX_SIZE * 2 + 32), with
    // above_row = above_data + 16 and left_col = left_data + 16.
    private const int NumIntraNeighbourPixels = 64 * 2 + 32;
    private const int EdgeOrigin = 16;
    private const int Iterations = 400;
    private const int TightIterations = 64;

    [Theory]
    [MemberData(nameof(Names))]
    public void Simd_matches_c(string name)
    {
        if (!Avx2.IsSupported) return;
        int index = Array.FindIndex(Kernels, k => k.Name == name);
        var rng = new Random(0x5E6 + index);
        var aboveData = new byte[NumIntraNeighbourPixels];
        var leftData = new byte[NumIntraNeighbourPixels];
        for (int it = 0; it < Iterations; it++)
        {
            FillEdges(rng, it, aboveData, leftData);
            Run(Kernels[index], rng, it, aboveData, leftData);
        }
    }

    // Edge buffers cut right after max(n, 8) pixels (n = bw above, bh left; 16 left pixels for
    // h_32x8, whose sse2 kernel loads 16): a load past that throws. Every kernel stays well inside
    // the 144 pixels reconintra provides after the pointer.
    [Theory]
    [MemberData(nameof(Names))]
    public void Simd_reads_stay_within_edges(string name)
    {
        if (!Avx2.IsSupported) return;
        int index = Array.FindIndex(Kernels, k => k.Name == name);
        Kernel k = Kernels[index];
        var rng = new Random(0x7A1 + index);
        int leftMin = name.StartsWith("aom_h_predictor_32x8_", StringComparison.Ordinal) ? 16 : 8;
        var aboveData = new byte[EdgeOrigin + Math.Max(k.Bw, 8)];
        var leftData = new byte[EdgeOrigin + Math.Max(k.Bh, leftMin)];
        for (int it = 0; it < TightIterations; it++)
        {
            FillEdges(rng, it, aboveData, leftData);
            Run(k, rng, it, aboveData, leftData);
        }
    }

    // Patterns 0..7 (first 64 iterations): all 0, all 255, all equal, above 0 / left 255,
    // above 255 / left 0, random 0/255, random {0, 255, v}, constant above / random left. Then
    // uniform random. The whole buffer is filled, so pixels past the edge are noise the kernels
    // must ignore.
    private static void FillEdges(Random rng, int it, byte[] above, byte[] left)
    {
        int pattern = it < 64 ? it % 8 : 8;
        byte v = (byte)rng.Next(256);
        for (int i = 0; i < above.Length; i++) above[i] = Pick(rng, pattern, v, true);
        for (int i = 0; i < left.Length; i++) left[i] = Pick(rng, pattern, v, false);
        // above_row[-1] = <top-left>; left_col[-1] = above_row[-1];
        left[EdgeOrigin - 1] = above[EdgeOrigin - 1];
    }

    private static byte Pick(Random rng, int pattern, byte v, bool isAbove) => pattern switch
    {
        0 => 0,
        1 => 255,
        2 => v,
        3 => isAbove ? (byte)0 : (byte)255,
        4 => isAbove ? (byte)255 : (byte)0,
        5 => rng.Next(2) == 0 ? (byte)0 : (byte)255,
        6 => rng.Next(3) switch { 0 => 0, 1 => 255, _ => v },
        7 => isAbove ? v : (byte)rng.Next(256),
        _ => (byte)rng.Next(256),
    };

    // dst: random stride != bw, random offset, and on even iterations the buffer ends exactly at the
    // last pixel of the block (a store past it throws). Checks the SIMD kernel and the dispatching
    // forwarder against C, that only bw bytes per row are written, and that the edges are not
    // modified.
    private static void Run(Kernel k, Random rng, int it, byte[] aboveData, byte[] leftData)
    {
        var above = new Ptr<byte>(aboveData, EdgeOrigin);
        var left = new Ptr<byte>(leftData, EdgeOrigin);
        int stride = k.Bw + rng.Next(1, 65);
        int offset = rng.Next(0, 64);
        int tail = (it & 1) == 0 ? 0 : rng.Next(1, 33);
        int size = offset + (k.Bh - 1) * stride + k.Bw + tail;
        var init = new byte[size];
        rng.NextBytes(init);
        var expected = (byte[])init.Clone();
        var actual = (byte[])init.Clone();
        var dispatched = (byte[])init.Clone();
        var aboveSnap = (byte[])aboveData.Clone();
        var leftSnap = (byte[])leftData.Clone();

        k.C(new Ptr<byte>(expected, offset), stride, above, left);
        k.Vec(new Ptr<byte>(actual, offset), stride, above, left);
        k.Dispatch(new Ptr<byte>(dispatched, offset), stride, above, left);

        for (int i = 0; i < size; i++)
        {
            int rel = i - offset;
            bool inBlock = rel >= 0 && rel / stride < k.Bh && rel % stride < k.Bw;
            if (!inBlock && actual[i] != init[i])
                Assert.Fail($"{k.Name} it={it}: wrote outside the block at byte {i} (stride {stride}, offset {offset})");
            if (actual[i] != expected[i])
                Assert.Fail($"{k.Name} it={it}: row {rel / stride} col {rel % stride} expected {expected[i]} got {actual[i]} (stride {stride}, offset {offset})");
            if (dispatched[i] != expected[i])
                Assert.Fail($"{k.Name} it={it}: forwarder differs from C at row {rel / stride} col {rel % stride}");
        }
        Assert.Equal(aboveSnap, aboveData);
        Assert.Equal(leftSnap, leftData);
    }
}
