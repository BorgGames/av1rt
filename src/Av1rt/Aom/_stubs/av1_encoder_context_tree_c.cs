// Stubs for av1/encoder/context_tree.c: delete this file when av1/encoder/context_tree.c is ported.
global using static Av1rt.Aom.stubs_av1_encoder_context_tree_c;

using System;

namespace Av1rt.Aom;

internal static partial class stubs_av1_encoder_context_tree_c
{
    // av1/encoder/context_tree.c (f3)
    internal static void av1_setup_shared_coeff_buffer(SequenceHeader seq_params,
                                                       PC_TREE_SHARED_BUFFERS shared_bufs,
                                                       aom_internal_error_info error) =>
        throw new NotImplementedException();

    internal static void av1_free_shared_coeff_buffer(PC_TREE_SHARED_BUFFERS shared_bufs) =>
        throw new NotImplementedException();

    internal static int av1_setup_sms_tree(AV1_COMP cpi, ThreadData td) =>
        throw new NotImplementedException();

    internal static void av1_free_sms_tree(ThreadData td) =>
        throw new NotImplementedException();

    internal static void av1_free_pmc(PICK_MODE_CONTEXT ctx, int num_planes) =>
        throw new NotImplementedException();

    internal static void av1_free_pc_tree_recursive(PC_TREE tree, int num_planes, int keep_best,
                                                    int keep_none,
                                                    PARTITION_SEARCH_TYPE partition_search_type) =>
        throw new NotImplementedException();

    internal static PC_TREE av1_alloc_pc_tree_node(BLOCK_SIZE bsize) =>
        throw new NotImplementedException();
}
