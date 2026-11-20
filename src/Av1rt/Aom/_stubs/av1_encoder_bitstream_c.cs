// Stubs for av1/encoder/bitstream.c: delete this file when av1/encoder/bitstream.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_bitstream_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_bitstream_c
{
    // av1/encoder/bitstream.c (f1)
    internal static void av1_pack_tile_info(AV1_COMP cpi, ThreadData td,
                                            PackBSParams pack_bs_params) =>
        throw new NotImplementedException();

    internal static void av1_write_last_tile_info(
        AV1_COMP cpi, in FrameHeaderInfo fh_info, Ptr<aom_write_bit_buffer> saved_wb,
        ref int curr_tg_data_size, Ptr<byte> curr_tg_start, ref uint total_size,
        ref Ptr<byte> tile_data_start, ref int largest_tile_id, ref int is_first_tg,
        uint obu_header_size, byte obu_extn_header) =>
        throw new NotImplementedException();

    internal static void av1_reset_pack_bs_thread_data(ThreadData td) =>
        throw new NotImplementedException();

    internal static void av1_accumulate_pack_bs_thread_data(AV1_COMP cpi, ThreadData td) =>
        throw new NotImplementedException();

    internal static void av1_write_obu_tg_tile_headers(AV1_COMP cpi, MACROBLOCKD xd,
                                                       PackBSParams pack_bs_params,
                                                       int tile_idx) =>
        throw new NotImplementedException();

    internal static int av1_neg_interleave(int x, int @ref, int max) =>
        throw new NotImplementedException();
}
