// Port of libaom av1/encoder/gop_structure.h
global using static Av1rt.Aom.gop_structure;

namespace Av1rt.Aom;

internal static partial class gop_structure
{
    internal const int MIN_ARF_GF_BOOST = 240;
    internal const int NORMAL_BOOST = 100;

    // av1_gop_setup_structure / av1_gop_check_forward_keyframe /
    // av1_get_arf_q_index* are defined in gop_structure.c (not ported here).
}
