// Port of libaom aom_dsp/intrapred_common.h

global using static Av1rt.Aom.intrapred_common;

namespace Av1rt.Aom;

// [subset] smooth_weights_u16 is only used by the SIMD kernels and is not ported.
internal static partial class intrapred_common
{
    // Weights are quadratic from '1' to '1 / block_size', scaled by
    // 2^SMOOTH_WEIGHT_LOG2_SCALE.
    internal const int SMOOTH_WEIGHT_LOG2_SCALE = 8;

    // Note these arrays are aligned to ensure NEON loads using a cast to uint32_t*
    // have sufficient alignment. Using 8 preserves the potential for an alignment
    // hint in load_weight_w8(). For that case, this could be increased to 16 to
    // allow an aligned load in x86.
    internal static readonly byte[] smooth_weights = {
        // bs = 4
        255, 149, 85, 64,
        // bs = 8
        255, 197, 146, 105, 73, 50, 37, 32,
        // bs = 16
        255, 225, 196, 170, 145, 123, 102, 84, 68, 54, 43, 33, 26, 20, 17, 16,
        // bs = 32
        255, 240, 225, 210, 196, 182, 169, 157, 145, 133, 122, 111, 101, 92, 83, 74,
        66, 59, 52, 45, 39, 34, 29, 25, 21, 17, 14, 12, 10, 9, 8, 8,
        // bs = 64
        255, 248, 240, 233, 225, 218, 210, 203, 196, 189, 182, 176, 169, 163, 156,
        150, 144, 138, 133, 127, 121, 116, 111, 106, 101, 96, 91, 86, 82, 77, 73, 69,
        65, 61, 57, 54, 50, 47, 44, 41, 38, 35, 32, 29, 27, 25, 22, 20, 18, 16, 15,
        13, 12, 10, 9, 8, 7, 6, 6, 5, 5, 4, 4, 4
    };
}
