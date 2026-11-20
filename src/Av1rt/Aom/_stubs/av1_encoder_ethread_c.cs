// Stubs for av1/encoder/ethread.c: delete this file when av1/encoder/ethread.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_ethread_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_ethread_c
{
    // av1/encoder/ethread.c (f4)
    internal static void av1_write_tile_obu_mt(
        AV1_COMP cpi, Ptr<byte> dst, int dst_size, ref uint total_size,
        Ptr<aom_write_bit_buffer> saved_wb, byte obu_extn_header,
        in FrameHeaderInfo fh_info, ref int largest_tile_id, ref uint max_tile_size,
        ref uint obu_header_size, ref Ptr<byte> tile_data_start, int num_workers) =>
        throw new NotImplementedException();
}
