// Stubs for av1/common/quant_common.c: delete this file when av1/common/quant_common.c is ported.
global using static Av1rt.Aom.stubs_av1_common_quant_common_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_common_quant_common_c
{
    // ---- av1/common/quant_common.c (e1; called by av1_quantize.c, e2) -------------------
    internal static short av1_dc_quant_QTX(int qindex, int delta, aom_bit_depth_t bit_depth) =>
        throw new NotImplementedException();
    internal static short av1_ac_quant_QTX(int qindex, int delta, aom_bit_depth_t bit_depth) =>
        throw new NotImplementedException();
    internal static int av1_get_qindex(segmentation seg, int segment_id, int base_qindex) =>
        throw new NotImplementedException();
    // Returns true if we are using quantization matrix.
    internal static bool av1_use_qmatrix(CommonQuantParams quant_params, MACROBLOCKD xd,
                                         int segment_id) =>
        throw new NotImplementedException();
}
