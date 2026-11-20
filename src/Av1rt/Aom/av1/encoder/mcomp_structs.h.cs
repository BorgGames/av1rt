// Port of libaom av1/encoder/mcomp_structs.h
global using SEARCH_METHODS = int;
global using WARP_SEARCH_METHOD = int;
global using static Av1rt.Aom.mcomp_structs;

namespace Av1rt.Aom;

internal static partial class mcomp_structs
{
    // The maximum number of steps in a step search given the largest
    // allowed initial step
    internal const int MAX_MVSEARCH_STEPS = 11;
    // Max full pel mv specified in the unit of full pixel
    // Enable the use of motion vector in range [-1023, 1023].
    internal const int MAX_FULL_PEL_VAL = (1 << (MAX_MVSEARCH_STEPS - 1)) - 1;
    // Maximum size of the first step in full pel units
    internal const int MAX_FIRST_STEP = 1 << (MAX_MVSEARCH_STEPS - 1);
    // Maximum number of neighbors to scan per iteration during
    // WARPED_CAUSAL refinement
    // Note: The elements of warp_search_config.neighbor_mask must be at least
    // MAX_WARP_SEARCH_NEIGHBORS many bits wide. So the type may need to be
    // widened if this value is increased.
    internal const int MAX_WARP_SEARCH_NEIGHBORS = 8;

    internal const int SEARCH_RANGE_8P = 3;
    internal const int SEARCH_GRID_STRIDE_8P = 2 * SEARCH_RANGE_8P + 1;
    internal const int SEARCH_GRID_CENTER_8P =
        SEARCH_RANGE_8P * SEARCH_GRID_STRIDE_8P + SEARCH_RANGE_8P;

    // enum SEARCH_METHODS
    // Search 8-points in the radius grid around center, up to 11 search stages.
    internal const int DIAMOND = 0;
    // Search 12-points in the radius/tan_radius grid around center,
    // up to 15 search stages.
    internal const int NSTEP = 1;
    // Search 8-points in the radius grid around center, up to 16 search stages.
    internal const int NSTEP_8PT = 2;
    // Search 8-points in the radius grid around center, upto 11 search stages
    // with clamping of search radius.
    internal const int CLAMPED_DIAMOND = 3;
    // Search maximum 8-points in the radius grid around center,
    // up to 11 search stages. First stage consists of 8 search points
    // and the rest with 6 search points each in hex shape.
    internal const int HEX = 4;
    // Search maximum 8-points in the radius grid around center,
    // up to 11 search stages. First stage consists of 4 search
    // points and the rest with 8 search points each.
    internal const int BIGDIA = 5;
    // BIGDIA search with up to 2 stages.
    internal const int FAST_DIAMOND = 6;
    // BIGDIA search with up to 3 stages.
    internal const int FAST_BIGDIA = 7;
    // BIGDIA search with up to 1 stage.
    internal const int VFAST_DIAMOND = 8;
    // Total number of search methods.
    internal const int NUM_SEARCH_METHODS = 9;
    // Number of distinct search methods.
    internal const int NUM_DISTINCT_SEARCH_METHODS = BIGDIA + 1;
    // enum WARP_SEARCH_METHOD
    // Search 4 adjacent points in a diamond shape at each iteration
    internal const int WARP_SEARCH_DIAMOND = 0;
    // Search 8 adjacent points in a square at each iteration
    internal const int WARP_SEARCH_SQUARE = 1;
    internal const int WARP_SEARCH_METHODS = 2;
}

internal partial struct search_neighbors
{
    internal FULLPEL_MV coord;
    internal int coord_offset;
}

// motion search site
internal partial struct search_site
{
    internal FULLPEL_MV mv;
    internal int offset;
}

// C copies whole configs by value (encoder.c: search_site_cfg[SS_CFG_FPF][i] = ...),
// hence CopyFrom.
internal sealed partial class search_site_config
{
    internal readonly search_site[][] site = EncArrays.New2<search_site>(MAX_MVSEARCH_STEPS * 2, 16 + 1);
    // Number of search steps.
    internal int num_search_steps;
    internal readonly int[] searches_per_step = new int[MAX_MVSEARCH_STEPS * 2];
    internal readonly int[] radius = new int[MAX_MVSEARCH_STEPS * 2];
    internal int stride;

    internal void CopyFrom(search_site_config o)
    {
        EncArrays.Copy2(site, o.site);
        num_search_steps = o.num_search_steps;
        EncArrays.Copy1(searches_per_step, o.searches_per_step);
        EncArrays.Copy1(radius, o.radius);
        stride = o.stride;
    }
}

internal partial struct warp_search_config
{
    internal int num_neighbors;
    internal Arr8<MV> neighbors;
    // Bitmask which is used to prune the search neighbors at one iteration
    // based on which direction we chose in the previous iteration.
    // See comments in av1_refine_warped_mv for details.
    internal Arr8<byte> neighbor_mask;
}
