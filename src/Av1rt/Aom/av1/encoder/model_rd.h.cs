// Port of libaom av1/encoder/model_rd.h
global using ModelRdType = int;
global using static Av1rt.Aom.model_rd;

namespace Av1rt.Aom;

internal static partial class model_rd
{
    // 0: Legacy model
    // 1: Curve fit model
    // 2: Surface fit model
    // 3: DNN regression model
    // 4: Full rd model
    internal const int MODELRD_TYPE_INTERP_FILTER = 1;
    internal const int MODELRD_TYPE_TX_SEARCH_PRUNE = 1;
    internal const int MODELRD_TYPE_MASKED_COMPOUND = 1;
    internal const int MODELRD_TYPE_INTERINTRA = 1;
    internal const int MODELRD_TYPE_INTRA = 1;
    internal const int MODELRD_TYPE_MOTION_MODE_RD = 1;

    // enum ModelRdType
    internal const int MODELRD_LEGACY = 0;
    internal const int MODELRD_CURVFIT = 1;
    internal const int MODELRD_TYPES = 2;

    // [subset] model_rd_for_sb_type / model_rd_from_sse_type, calculate_sse,
    // compute_sse_plane, model_rd_from_sse, model_rd_with_curvfit, model_rd_for_sb,
    // model_rd_for_sb_with_curvfit and the model_rd_sb_fn / model_rd_sse_fn tables: only used
    // by the rd mode search (none of them runs in the subset).
}
