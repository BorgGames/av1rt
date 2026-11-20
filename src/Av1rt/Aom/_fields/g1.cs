// Field additions of package g1 (encoder.c, encode_strategy.c, av1_cx_iface.c, aom_encoder.c,
// aom_codec.c) to shared types.

namespace Av1rt.Aom;

// [port] Not in libaom. Exception.Message never returns null, so aom_internal_error records
// whether C had a detail string (aom_internal_error(info, code, NULL) leaves has_detail 0) and
// aom_set_error_from_exception restores it.
public sealed partial class AomException
{
    internal int has_detail = 1;
}
