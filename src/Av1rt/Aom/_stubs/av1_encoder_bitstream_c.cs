// Stubs for av1/encoder/bitstream.c: delete this file when av1/encoder/bitstream.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_bitstream_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_bitstream_c
{
    // av1/encoder/bitstream.c (f1), called from encoder.c / av1_cx_iface.c (g1)
    internal static uint av1_write_obu_header(AV1LevelParams level_params,
                                              ref int frame_header_count, OBU_TYPE obu_type,
                                              bool has_nonzero_operating_point_idc,
                                              bool is_layer_specific_obu, int obu_extension,
                                              Ptr<byte> dst) =>
        throw new NotImplementedException();

    internal static int av1_write_uleb_obu_size(int obu_payload_size, Ptr<byte> dest,
                                                int dest_size) =>
        throw new NotImplementedException();

    internal static int av1_pack_bitstream(AV1_COMP cpi, Ptr<byte> dst, int dst_size,
                                           ref int size, ref int largest_tile_id) =>
        throw new NotImplementedException();
}
