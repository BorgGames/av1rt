// Port of libaom av1/encoder/av1_ext_ratectrl.h
global using static Av1rt.Aom.av1_ext_ratectrl;

namespace Av1rt.Aom;

// The external rate control interface is out of the subset (ready stays 0).
internal static partial class av1_ext_ratectrl
{
}

// Struct: embedded by value in AV1_COMP and only read as flags.
internal partial struct AOM_EXT_RATECTRL
{
    public int ready;
    public int ext_rdmult;
    // Whether per-superblock delta Q should be used. This is set on frame basis.
    // Key, golden and regular ARF frames use delta Q. All other frames do not.
    // This flag is added so the sb_params_list array does not have to be freed
    // and allocated repeatedly.
    public int use_delta_q;
    // [subset] model, funcs, ratectrl_config, rc_firstpass_stats,
    // sb_params_list: only used once an external rate controller is attached.
}
