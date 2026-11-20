// Port of libaom av1/encoder/rdopt.c
// [subset] Only av1_inter_mode_data_init and av1_block_error_lp_c (+ its RTCD forwarder) are
// ported: the rest of rdopt.c is the rd mode search, which the nonrd realtime path never runs.

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class rdopt
{
    internal static void av1_inter_mode_data_init(TileDataEnc tile_data)
    {
        for (int i = 0; i < BLOCK_SIZES_ALL; ++i)
        {
            ref InterModeRdModel md = ref tile_data.inter_mode_rd_models[i];
            md.ready = 0;
            md.num = 0;
            md.dist_sum = 0;
            md.ld_sum = 0;
            md.sse_sum = 0;
            md.sse_sse_sum = 0;
            md.sse_ld_sum = 0;
        }
    }

    internal static long av1_block_error_lp_c(Ptr<short> coeff, Ptr<short> dqcoeff,
                                              int block_size)
    {
        long error = 0;

        for (int i = 0; i < block_size; i++)
        {
            int diff = coeff[i] - dqcoeff[i];
            error += diff * diff;
        }

        return error;
    }

    // RTCD: av1_rtcd.h maps av1_block_error_lp to av1_block_error_lp_c in this configuration.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long av1_block_error_lp(Ptr<short> coeff, Ptr<short> dqcoeff,
                                            int block_size) =>
        av1_block_error_lp_c(coeff, dqcoeff, block_size);
}
