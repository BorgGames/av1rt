// Port of libaom av1/encoder/av1_ext_ratectrl.c

namespace Av1rt.Aom;

// [subset] Only av1_extrc_init / av1_extrc_delete run: no external rate controller is ever
// attached (ext_ratectrl.ready stays 0), so av1_extrc_create and the per-frame
// av1_extrc_* query/update functions are not ported.
internal static partial class av1_ext_ratectrl
{
    // [port] AOM_EXT_RATECTRL is embedded by value in AV1_COMP; the pointer becomes `ref`
    // (the NULL check cannot fail).
    internal static aom_codec_err_t av1_extrc_init(ref AOM_EXT_RATECTRL ext_ratectrl)
    {
        ext_ratectrl = default;
        return AOM_CODEC_OK;
    }

    internal static aom_codec_err_t av1_extrc_delete(ref AOM_EXT_RATECTRL ext_ratectrl)
    {
        if (ext_ratectrl.ready != 0)
        {
            // [subset] funcs.delete_model(model) and freeing rc_firstpass_stats /
            // sb_params_list: ready is never set in the subset.
            throw new System.NotSupportedException("external rate control is not supported");
        }
        return av1_extrc_init(ref ext_ratectrl);
    }
}
