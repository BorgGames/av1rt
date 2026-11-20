// Stubs for av1/encoder/encoder_utils.c: delete this file when av1/encoder/encoder_utils.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_encoder_utils_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_encoder_utils_c
{
    // av1/encoder/encoder_utils.c (f4): extern const int
    // default_warped_probs[FRAME_UPDATE_TYPES].
    internal static int[] default_warped_probs => throw new NotImplementedException();

    // av1/encoder/encoder_utils.c (f4), called from encoder.c (g1)
    internal static void av1_scale_references(AV1_COMP cpi, InterpFilter filter, int phase,
                                              int use_optimized_scaler) =>
        throw new NotImplementedException();

    internal static void av1_setup_frame(AV1_COMP cpi) =>
        throw new NotImplementedException();

    internal static BLOCK_SIZE av1_select_sb_size(AV1EncoderConfig oxcf, int width, int height,
                                                  int number_spatial_layers) =>
        throw new NotImplementedException();

    internal static void av1_apply_active_map(AV1_COMP cpi) =>
        throw new NotImplementedException();

    internal static void av1_set_size_dependent_vars(AV1_COMP cpi, ref int q,
                                                     ref int bottom_index, ref int top_index) =>
        throw new NotImplementedException();

    internal static void av1_finalize_encoded_frame(AV1_COMP cpi) =>
        throw new NotImplementedException();

    internal static void av1_save_all_coding_context(AV1_COMP cpi) =>
        throw new NotImplementedException();
}
