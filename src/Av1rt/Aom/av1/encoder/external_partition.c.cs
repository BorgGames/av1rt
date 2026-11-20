// Port of libaom av1/encoder/external_partition.c

namespace Av1rt.Aom;

// [subset] Only ext_part_init / av1_ext_part_delete run: no external partition model is ever
// attached (ext_part_controller.ready stays 0), so av1_ext_part_create and the query/send
// functions are not ported.
internal static partial class external_partition
{
    // [port] ExtPartController is embedded by value in AV1_COMP; the pointer becomes `ref`
    // (the NULL check cannot fail).
    private static aom_codec_err_t ext_part_init(ref ExtPartController ext_part_controller)
    {
        ext_part_controller = default;
        return AOM_CODEC_OK;
    }

    internal static aom_codec_err_t av1_ext_part_delete(ref ExtPartController ext_part_controller)
    {
        if (ext_part_controller.ready != 0)
        {
            // [subset] funcs.delete_model(model): ready is never set in the subset.
            throw new System.NotSupportedException("external partition models are not supported");
        }
        return ext_part_init(ref ext_part_controller);
    }
}
