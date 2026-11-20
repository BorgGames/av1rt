// Port of libaom av1/encoder/av1_quantize.h
global using static Av1rt.Aom.av1_quantize;

namespace Av1rt.Aom;

internal static partial class av1_quantize
{
    // All functions of this header are implemented in av1_quantize.c.
}

// Struct: small, passed as const QUANT_PARAM * and filled in place by the caller.
internal partial struct QUANT_PARAM
{
    internal int log_scale;
    internal TX_SIZE tx_size;
    internal Ptr<qm_val_t> qmatrix;
    internal Ptr<qm_val_t> iqmatrix;
    internal int use_quant_b_adapt;
    internal int use_optimize_b;
    internal int xform_quant_idx;
}

// [subset] AV1_QUANT_FACADE (function pointer typedef): never used in the subset.

// The QUANTS structure is used only for internal quantizer setup in
// av1_quantize.c.
// All of its fields use the same coefficient shift/scaling at TX.
// Class: large tables, embedded in EncQuantDequantParams; rows are handed out as pointers
// (x->plane[].quant_QTX = quants->y_quant[qindex]).
internal sealed partial class QUANTS
{
    // 0: dc 1: ac 2-8: ac repeated to SIMD width
    internal readonly short[][] y_quant = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] y_quant_shift = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] y_zbin = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] y_round = EncArrays.New2<short>(QINDEX_RANGE, 8);

    // TODO(jingning): in progress of re-working the quantization. will decide
    // if we want to deprecate the current use of y_quant.
    internal readonly short[][] y_quant_fp = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] u_quant_fp = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] v_quant_fp = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] y_round_fp = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] u_round_fp = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] v_round_fp = EncArrays.New2<short>(QINDEX_RANGE, 8);

    internal readonly short[][] u_quant = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] v_quant = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] u_quant_shift = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] v_quant_shift = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] u_zbin = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] v_zbin = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] u_round = EncArrays.New2<short>(QINDEX_RANGE, 8);
    internal readonly short[][] v_round = EncArrays.New2<short>(QINDEX_RANGE, 8);
}

// The Dequants structure is used only for internal quantizer setup in
// av1_quantize.c.
// Fields are suffixed according to whether or not they're expressed in
// the same coefficient shift/precision as TX or a fixed Q3 format.
internal sealed partial class Dequants
{
    internal readonly short[][] y_dequant_QTX = EncArrays.New2<short>(QINDEX_RANGE, 8); // 8: SIMD width
    internal readonly short[][] u_dequant_QTX = EncArrays.New2<short>(QINDEX_RANGE, 8); // 8: SIMD width
    internal readonly short[][] v_dequant_QTX = EncArrays.New2<short>(QINDEX_RANGE, 8); // 8: SIMD width
}

// The DeltaQuantParams structure holds the dc/ac deltaq parameters.
internal partial struct DeltaQuantParams
{
    internal int y_dc_delta_q;
    internal int u_dc_delta_q;
    internal int u_ac_delta_q;
    internal int v_dc_delta_q;
    internal int v_ac_delta_q;
    internal int sharpness;
}

// Class: embedded in AV1_COMP, used through pointers.
internal sealed partial class EncQuantDequantParams
{
    // Quantization parameters for internal quantizer setup.
    internal readonly QUANTS quants = new();
    // Dequantization parameters for internal quantizer setup.
    internal readonly Dequants dequants = new();
    // Deltaq parameters to track the state of the dc/ac deltaq parameters in
    // cm->quant_params. It is used to decide whether the quantizer tables need
    // to be re-initialized.
    internal DeltaQuantParams prev_deltaq_params;
}
