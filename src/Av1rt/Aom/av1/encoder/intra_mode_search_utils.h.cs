// Port of libaom av1/encoder/intra_mode_search_utils.h
global using static Av1rt.Aom.intra_mode_search_utils;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class intra_mode_search_utils
{
    // Macro for computing the speed-preset dependent threshold which is used for
    // deciding whether to enable/disable variance calculations in
    // intra_rd_variance_factor().
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double INTRA_RD_VAR_THRESH(int X) => (1.0 - (0.25 * (X)));

    internal const int HOG_BINS = 32;
    internal const int HOG_FIX_PREC_BITS = 16;

    // [subset] av1_intra_hog_model_bias/weights, hog_bin_lookup_fn, get_hist_bin_idx,
    // normalize_hog, lowbd/highbd gradient helpers, generate_hog, compute_gradient_info_sb,
    // generate_hog_using_gradient_cache, collect_hog_data, prune_intra_mode_with_hog,
    // write_uniform_cost, intra_mode_info_cost_y/uv, model_intra_yrd_and_prune, ...:
    // HOG-based intra pruning and the rd intra mode search are outside the subset.

    // Checks whether pixel-level gradient info can be cached at superblock level.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool is_gradient_caching_for_hog_enabled(
        AV1_COMP cpi)
    {
        SPEED_FEATURES sf = cpi.sf;
        return frame_is_intra_only(cpi.common) != 0 && sf.rt_sf.use_nonrd_pick_mode == 0 &&
               (sf.part_sf.partition_search_type == SEARCH_PARTITION) &&
               (sf.intra_sf.intra_pruning_with_hog != 0 ||
                sf.intra_sf.chroma_intra_pruning_with_hog != 0);
    }

    // Function to generate pixel level gradient information for a given superblock.
    // Sets the flags 'is_sb_gradient_cached' for the specific plane-type if
    // gradient info is generated for the same.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void produce_gradients_for_sb(AV1_COMP cpi, MACROBLOCK x,
                                                  BLOCK_SIZE sb_size, int mi_row,
                                                  int mi_col)
    {
        // Initialise flags related to hog data caching.
        x.is_sb_gradient_cached[PLANE_TYPE_Y] = false;
        x.is_sb_gradient_cached[PLANE_TYPE_UV] = false;
        if (!is_gradient_caching_for_hog_enabled(cpi)) return;

        SPEED_FEATURES sf = cpi.sf;
        int num_planes = av1_num_planes(cpi.common);

        av1_setup_src_planes(x, cpi.source, mi_row, mi_col, num_planes, sb_size);

        if (sf.intra_sf.intra_pruning_with_hog != 0)
        {
            // [subset] compute_gradient_info_sb(x, sb_size, PLANE_TYPE_Y): HOG pruning.
            throw new NotSupportedException("produce_gradients_for_sb: intra_pruning_with_hog");
        }
        if (sf.intra_sf.chroma_intra_pruning_with_hog != 0 && num_planes > 1)
        {
            // [subset] compute_gradient_info_sb(x, sb_size, PLANE_TYPE_UV): HOG pruning.
            throw new NotSupportedException("produce_gradients_for_sb: chroma_intra_pruning_with_hog");
        }
    }

    // Returns whether caching of source variance for 4x4 sub-blocks is allowed.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool is_src_var_for_4x4_sub_blocks_caching_enabled(
        AV1_COMP cpi)
    {
        SPEED_FEATURES sf = cpi.sf;
        if (cpi.oxcf.mode != ALLINTRA) return false;

        if (sf.part_sf.partition_search_type == SEARCH_PARTITION) return true;

        if (INTRA_RD_VAR_THRESH(cpi.oxcf.speed) <= 0 ||
            (sf.rt_sf.use_nonrd_pick_mode != 0 && sf.rt_sf.hybrid_intra_pickmode == 0))
            return false;

        return true;
    }

    // Initialize the members of Block4x4VarInfo structure to -1 at the start
    // of every superblock.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void init_src_var_info_of_4x4_sub_blocks(
        AV1_COMP cpi, Ptr<Block4x4VarInfo> src_var_info_of_4x4_sub_blocks,
        BLOCK_SIZE sb_size)
    {
        if (!is_src_var_for_4x4_sub_blocks_caching_enabled(cpi)) return;

        int mi_count_in_sb = mi_size_wide[sb_size] * mi_size_high[sb_size];
        for (int i = 0; i < mi_count_in_sb; i++)
        {
            src_var_info_of_4x4_sub_blocks[i].var = -1;
            src_var_info_of_4x4_sub_blocks[i].log_var = -1.0;
        }
    }
}
