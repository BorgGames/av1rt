// Port of libaom av1/encoder/allintra_vis.c

namespace Av1rt.Aom;

// [subset] All-intra perceptual analysis (wiener variance, user-rating based quantization) is an
// ALLINTRA-mode feature: only the teardown helper av1_dealloc_mb_wiener_var_pred_buf executes in
// the subset. Every other function of allintra_vis.c is not ported.
internal static partial class allintra_vis
{
    internal static void av1_dealloc_mb_wiener_var_pred_buf(ThreadData td)
    {
        // aom_free(td->wiener_tmp_pred_buf);
        td.wiener_tmp_pred_buf = Ptr<byte>.Null;
    }
}
