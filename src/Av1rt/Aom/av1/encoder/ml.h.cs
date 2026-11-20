// Port of libaom av1/encoder/ml.h
global using static Av1rt.Aom.ml;

namespace Av1rt.Aom;

internal static partial class ml
{
    internal const int NN_MAX_HIDDEN_LAYERS = 10;
    internal const int NN_MAX_NODES_PER_LAYER = 128;

    // [subset] NN_CONFIG (and the CONFIG_NN_V2 types): the neural-net partition / tx pruning
    // models are not used in the subset.
}
