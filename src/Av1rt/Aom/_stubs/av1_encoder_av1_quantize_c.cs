// Stubs for av1/encoder/av1_quantize.c: delete this file when av1/encoder/av1_quantize.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_av1_quantize_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_av1_quantize_c
{
    // ---- av1/encoder/av1_quantize.c -----------------------------------------------------
    internal static void av1_quantize_b_facade(Ptr<int> coeff_ptr, int n_coeffs,
                                               macroblock_plane p, Ptr<int> qcoeff_ptr,
                                               Ptr<int> dqcoeff_ptr, Ptr<ushort> eob_ptr,
                                               SCAN_ORDER sc, in QUANT_PARAM qparam) =>
        throw new NotImplementedException();
    internal static void av1_quantize_skip(int n_coeffs, Ptr<int> qcoeff_ptr,
                                           Ptr<int> dqcoeff_ptr, Ptr<ushort> eob_ptr) =>
        throw new NotImplementedException();
}
