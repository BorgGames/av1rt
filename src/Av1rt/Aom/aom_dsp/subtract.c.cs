// Port of libaom aom_dsp/subtract.c

global using static Av1rt.Aom.subtract;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [subset] aom_highbd_subtract_block_c: CONFIG_AV1_HIGHBITDEPTH == 0.
internal static partial class subtract
{
    internal static void aom_subtract_block_c(int rows, int cols, Ptr<short> diff,
                                              int diff_stride, Ptr<byte> src,
                                              int src_stride, Ptr<byte> pred,
                                              int pred_stride)
    {
        int r, c;

        for (r = 0; r < rows; r++)
        {
            for (c = 0; c < cols; c++) diff[c] = (short)(src[c] - pred[c]);

            diff += diff_stride;
            pred += pred_stride;
            src += src_stride;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void aom_subtract_block(int rows, int cols, Ptr<short> diff_ptr,
                                            int diff_stride, Ptr<byte> src_ptr,
                                            int src_stride, Ptr<byte> pred_ptr,
                                            int pred_stride) =>
        aom_subtract_block_c(rows, cols, diff_ptr, diff_stride, src_ptr, src_stride, pred_ptr,
                             pred_stride);
}
