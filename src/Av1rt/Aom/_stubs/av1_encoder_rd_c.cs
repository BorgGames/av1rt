// Stubs for av1/encoder/rd.c: delete this file when av1/encoder/rd.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_rd_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_rd_c
{
    // av1/encoder/rd.c (e8)
    internal static void av1_set_rd_speed_thresholds(AV1_COMP cpi) =>
        throw new NotImplementedException();

    // ---- av1/encoder/rd.c ---------------------------------------------------------------
    internal static YV12_BUFFER_CONFIG av1_get_scaled_ref_frame(AV1_COMP cpi,
                                                                int ref_frame) =>
        throw new NotImplementedException();
    // C: `struct buf_2d dst[MAX_MB_PLANE]` (decays to a pointer).
    internal static void av1_setup_pred_block(MACROBLOCKD xd, Ptr<buf_2d> dst,
                                              YV12_BUFFER_CONFIG src,
                                              scale_factors scale,
                                              scale_factors scale_uv,
                                              int num_planes) =>
        throw new NotImplementedException();
    internal static void av1_mv_pred(AV1_COMP cpi, MACROBLOCK x,
                                     Ptr<byte> ref_y_buffer, int ref_y_stride,
                                     int ref_frame, BLOCK_SIZE block_size) =>
        throw new NotImplementedException();
}
