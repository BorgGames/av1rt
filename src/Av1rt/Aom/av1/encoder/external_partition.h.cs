// Port of libaom av1/encoder/external_partition.h
global using static Av1rt.Aom.external_partition;

namespace Av1rt.Aom;

// The external partition model interface is out of the subset (ready stays 0).
internal static partial class external_partition
{
}

// Struct: embedded by value in AV1_COMP and only read as flags.
internal partial struct ExtPartController
{
    public int ready;
    public int test_mode;
    // [subset] config, model, funcs: only used once an external partition model
    // is attached.
}
