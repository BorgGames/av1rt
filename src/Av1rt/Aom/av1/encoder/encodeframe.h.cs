// Port of libaom av1/encoder/encodeframe.h
global using static Av1rt.Aom.encodeframe;

namespace Av1rt.Aom;

internal static partial class encodeframe
{
    // C: #define DELTA_Q_PERCEPTUAL_MODULATION 1 (0: variance based, 1: wavelet
    // AC energy based).
    internal const int DELTA_Q_PERCEPTUAL_MODULATION = 1;

    // av1_encode_frame / av1_alloc_tile_data / av1_init_tile_data /
    // av1_encode_tile / av1_encode_sb_row / av1_enc_set_offsets /
    // av1_set_offsets_without_segment_id are defined in encodeframe.c.
}
