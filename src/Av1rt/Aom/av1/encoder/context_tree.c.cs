// Port of libaom av1/encoder/context_tree.c
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class context_tree
{
    // [subset] av1_copy_tree_context: only called by the rd partition search
    // (partition_search.c rd paths, !CONFIG_REALTIME_ONLY); never executed in the subset.

    internal static void av1_setup_shared_coeff_buffer(SequenceHeader seq_params,
                                                       PC_TREE_SHARED_BUFFERS shared_bufs,
                                                       aom_internal_error_info error)
    {
        int num_planes = seq_params.monochrome != 0 ? 1 : MAX_MB_PLANE;
        int max_sb_square_y = 1 << num_pels_log2_lookup[seq_params.sb_size];
        int max_sb_square_uv = max_sb_square_y >> (seq_params.subsampling_x +
                                                   seq_params.subsampling_y);
        for (int i = 0; i < num_planes; i++)
        {
            int max_num_pix =
                (i == AOM_PLANE_Y) ? max_sb_square_y : max_sb_square_uv;
            // PORT-NOTE: AOM_CHECK_MEM_ERROR(aom_memalign(32, ...)): a managed allocation
            // never returns null.
            shared_bufs.coeff_buf[i] = new int[max_num_pix];
            shared_bufs.qcoeff_buf[i] = new int[max_num_pix];
            shared_bufs.dqcoeff_buf[i] = new int[max_num_pix];
        }
    }

    internal static void av1_free_shared_coeff_buffer(PC_TREE_SHARED_BUFFERS shared_bufs)
    {
        for (int i = 0; i < 3; i++)
        {
            shared_bufs.coeff_buf[i] = Ptr<int>.Null;
            shared_bufs.qcoeff_buf[i] = Ptr<int>.Null;
            shared_bufs.dqcoeff_buf[i] = Ptr<int>.Null;
        }
    }

    internal static PICK_MODE_CONTEXT av1_alloc_pmc(AV1_COMP cpi,
                                                    BLOCK_SIZE bsize,
                                                    PC_TREE_SHARED_BUFFERS shared_bufs)
    {
        AV1_COMMON cm = cpi.common;
        int num_planes = av1_num_planes(cm);
        int num_pix = block_size_wide[bsize] * block_size_high[bsize];
        int num_blk = num_pix / 16;

        // Allocate PICK_MODE_CONTEXT and all sub-arrays (eobs, txb_entropy_ctx,
        // color_index_map, tx_type_map) in a single 32-byte aligned memory block to
        // avoid multiple aom_memalign/aom_free calls per partition context.
        // PORT-NOTE: the sub-arrays are separate managed arrays here. Each keeps the
        // 32-byte rounded size of its C slice (in elements), so an access within C's
        // padding stays in bounds as it does in C.
        int eob_sz = (num_blk * sizeof(ushort) + 31) & ~31;
        int txb_ctx_sz = (num_blk * sizeof(byte) + 31) & ~31;
        bool use_palette = (num_pix <= MAX_PALETTE_SQUARE) &&
                           cm.features.allow_screen_content_tools;
        int pal_sz =
            use_palette ? ((num_pix * sizeof(byte) + 31) & ~31) : 0;
        int tx_type_sz = num_blk * sizeof(byte);

        // [av1rt] Reuse a context of this size that this thread freed, reset to the state of a
        // new one (the managed arrays of a new context are zeroed).
        if (!use_palette && shared_bufs.pmc_pool[bsize] is { Count: > 0 } free)
        {
            PICK_MODE_CONTEXT reused = free.Pop();
            if (reused.pool_num_planes == num_planes)
            {
                reset_pooled_pmc(reused, shared_bufs, num_planes, num_blk);
                return reused;
            }
        }

        PICK_MODE_CONTEXT ctx = new PICK_MODE_CONTEXT();
        if (!use_palette)
        {
            ctx.pool = shared_bufs;
            ctx.pool_bsize = bsize;
            ctx.pool_num_planes = num_planes;
        }
        ctx.rd_mode_is_ready = 0;
        ctx.num_4x4_blk = num_blk;

        for (int i = 0; i < num_planes; ++i)
        {
            ctx.coeff[i] = shared_bufs.coeff_buf[i];
            ctx.qcoeff[i] = shared_bufs.qcoeff_buf[i];
            ctx.dqcoeff[i] = shared_bufs.dqcoeff_buf[i];
            ctx.eobs[i] = new ushort[eob_sz / sizeof(ushort)];
            ctx.txb_entropy_ctx[i] = new byte[txb_ctx_sz];
        }

        if (use_palette)
        {
            ctx.color_index_map[0] = new byte[pal_sz];
            ctx.color_index_map[1] = new byte[pal_sz];
        }
        else
        {
            ctx.color_index_map[0] = Ptr<byte>.Null;
            ctx.color_index_map[1] = Ptr<byte>.Null;
        }

        ctx.tx_type_map = new byte[tx_type_sz];

        av1_invalid_rd_stats(ref ctx.rd_stats);

        return ctx;
    }

    // [av1rt] A context from the free list as av1_alloc_pmc makes a new one (without palette).
    private static void reset_pooled_pmc(PICK_MODE_CONTEXT ctx, PC_TREE_SHARED_BUFFERS shared_bufs,
                                         int num_planes, int num_blk)
    {
        ctx.pool = shared_bufs;
        ctx.mic.Clear();
        ctx.mbmi_ext_best = default;
        for (int i = 0; i < num_planes; ++i)
        {
            ctx.coeff[i] = shared_bufs.coeff_buf[i];
            ctx.qcoeff[i] = shared_bufs.qcoeff_buf[i];
            ctx.dqcoeff[i] = shared_bufs.dqcoeff_buf[i];
            ctx.eobs[i].AsSpan().Clear();
            ctx.txb_entropy_ctx[i].AsSpan().Clear();
        }
        ctx.tx_type_map.AsSpan().Clear();
        ctx.num_4x4_blk = num_blk;
        ctx.skippable = 0;
        ctx.rd_stats = default;
        ctx.rd_mode_is_ready = 0;
        av1_invalid_rd_stats(ref ctx.rd_stats);
    }

    internal static void av1_reset_pmc(PICK_MODE_CONTEXT ctx)
    {
        av1_zero_array(ctx.tx_type_map, ctx.num_4x4_blk);
        av1_invalid_rd_stats(ref ctx.rd_stats);
    }

    internal static void av1_free_pmc(PICK_MODE_CONTEXT ctx, int num_planes)
    {
        _ = num_planes;
        if (ctx == null) return;
        // aom_free(ctx): [av1rt] back to the free list of the thread that allocated it.
        PC_TREE_SHARED_BUFFERS pool = ctx.pool;
        if (pool == null) return;
        ctx.pool = null;
        (pool.pmc_pool[ctx.pool_bsize] ??= new()).Push(ctx);
    }

    internal static PC_TREE av1_alloc_pc_tree_node(BLOCK_SIZE bsize)
    {
        PC_TREE pc_tree = new PC_TREE();

        pc_tree.partitioning = PARTITION_NONE;
        pc_tree.block_size = bsize;

        return pc_tree;
    }

    // [av1rt] av1_alloc_pc_tree_node from the free list of the thread whose buffers these are.
    internal static PC_TREE av1_alloc_pc_tree_node(BLOCK_SIZE bsize, PC_TREE_SHARED_BUFFERS pool)
    {
        PC_TREE pc_tree;
        if (pool.pc_tree_pool.Count > 0)
        {
            // Freed nodes have no children left; reset the rest as new.
            pc_tree = pool.pc_tree_pool.Pop();
            pc_tree.none = null;
            pc_tree.horizontal[0] = pc_tree.horizontal[1] = null;
            pc_tree.vertical[0] = pc_tree.vertical[1] = null;
            pc_tree.split[0] = pc_tree.split[1] = pc_tree.split[2] = pc_tree.split[3] = null;
            pc_tree.index = 0;
        }
        else
        {
            pc_tree = new PC_TREE();
        }
        pc_tree.pool = pool;

        pc_tree.partitioning = PARTITION_NONE;
        pc_tree.block_size = bsize;

        return pc_tree;
    }

    // aom_free(pc_tree): [av1rt] back to its free list, if it came from one.
    private static void free_pc_tree_node(PC_TREE pc_tree)
    {
        PC_TREE_SHARED_BUFFERS pool = pc_tree.pool;
        if (pool == null) return;
        pc_tree.pool = null;
        pool.pc_tree_pool.Push(pc_tree);
    }

    // C: #define FREE_PMC_NODE(CTX) do { av1_free_pmc(CTX, num_planes); CTX = NULL; } while (0)
    private static void FREE_PMC_NODE(ref PICK_MODE_CONTEXT CTX, int num_planes)
    {
        av1_free_pmc(CTX, num_planes);
        CTX = null;
    }

    internal static void av1_free_pc_tree_recursive(PC_TREE pc_tree, int num_planes, int keep_best,
                                                    int keep_none,
                                                    PARTITION_SEARCH_TYPE partition_search_type)
    {
        if (pc_tree == null) return;

        // Avoid freeing of extended partitions as they are not supported when
        // partition_search_type is VAR_BASED_PARTITION.
        if (partition_search_type == VAR_BASED_PARTITION && keep_best == 0 &&
            keep_none == 0)
        {
            FREE_PMC_NODE(ref pc_tree.none, num_planes);

            for (int i = 0; i < 2; ++i)
            {
                FREE_PMC_NODE(ref pc_tree.horizontal[i], num_planes);
                FREE_PMC_NODE(ref pc_tree.vertical[i], num_planes);
            }

            // [subset] the extended-partition asserts are #if !CONFIG_REALTIME_ONLY.

            for (int i = 0; i < 4; ++i)
            {
                if (pc_tree.split[i] != null)
                {
                    av1_free_pc_tree_recursive(pc_tree.split[i], num_planes, 0, 0,
                                               partition_search_type);
                    pc_tree.split[i] = null;
                }
            }
            // aom_free(pc_tree)
            free_pc_tree_node(pc_tree);
            return;
        }

        PARTITION_TYPE partition = pc_tree.partitioning;

        if (keep_none == 0 && (keep_best == 0 || (partition != PARTITION_NONE)))
            FREE_PMC_NODE(ref pc_tree.none, num_planes);

        for (int i = 0; i < 2; ++i)
        {
            if (keep_best == 0 || (partition != PARTITION_HORZ))
                FREE_PMC_NODE(ref pc_tree.horizontal[i], num_planes);
            if (keep_best == 0 || (partition != PARTITION_VERT))
                FREE_PMC_NODE(ref pc_tree.vertical[i], num_planes);
        }
        // [subset] horizontala/b, verticala/b, horizontal4, vertical4: !CONFIG_REALTIME_ONLY.
        if (keep_best == 0 || (partition != PARTITION_SPLIT))
        {
            for (int i = 0; i < 4; ++i)
            {
                if (pc_tree.split[i] != null)
                {
                    av1_free_pc_tree_recursive(pc_tree.split[i], num_planes, 0, 0,
                                               partition_search_type);
                    pc_tree.split[i] = null;
                }
            }
        }

        if (keep_best == 0 && keep_none == 0) free_pc_tree_node(pc_tree);
    }

    internal static int av1_setup_sms_tree(AV1_COMP cpi, ThreadData td)
    {
        // The structure 'sms_tree' is used to store the simple motion search data for
        // partition pruning in inter frames. Hence, the memory allocations and
        // initializations related to it are avoided for allintra encoding mode.
        if (cpi.oxcf.kf_cfg.key_freq_max == 0) return 0;

        AV1_COMMON cm = cpi.common;
        int stat_generation_stage = is_stat_generation_stage(cpi);
        int is_sb_size_128 = B(cm.seq_params.sb_size == BLOCK_128X128);
        int tree_nodes =
            av1_get_pc_tree_nodes(is_sb_size_128, stat_generation_stage);
        int sms_tree_index = 0;
        int this_sms; // C: SIMPLE_MOTION_DATA_TREE *this_sms (index into td->sms_tree)
        int square_index = 1;
        int nodes;

        td.sms_tree = alloc_array<SIMPLE_MOTION_DATA_TREE>(tree_nodes);
        this_sms = 0;

        if (stat_generation_stage == 0)
        {
            int leaf_factor = is_sb_size_128 != 0 ? 4 : 1;
            int leaf_nodes = 256 * leaf_factor;

            // Sets up all the leaf nodes in the tree.
            for (sms_tree_index = 0; sms_tree_index < leaf_nodes; ++sms_tree_index)
            {
                SIMPLE_MOTION_DATA_TREE tree = td.sms_tree[sms_tree_index];
                tree.block_size = square[0];
            }

            // Each node has 4 leaf nodes, fill each block_size level of the tree
            // from leafs to the root.
            for (nodes = leaf_nodes >> 2; nodes > 0; nodes >>= 2)
            {
                for (int i = 0; i < nodes; ++i)
                {
                    SIMPLE_MOTION_DATA_TREE tree = td.sms_tree[sms_tree_index];
                    tree.block_size = square[square_index];
                    for (int j = 0; j < 4; j++) tree.split[j] = td.sms_tree[this_sms++];
                    ++sms_tree_index;
                }
                ++square_index;
            }
        }
        else
        {
            // Allocation for firstpass/LAP stage
            // TODO(Mufaddal): refactor square_index to use a common block_size macro
            // from firstpass.c
            SIMPLE_MOTION_DATA_TREE tree = td.sms_tree[sms_tree_index];
            square_index = 2;
            tree.block_size = square[square_index];
        }

        // Set up the root node for the largest superblock size
        td.sms_root = td.sms_tree[tree_nodes - 1];
        Debug.Assert(td.sms_root != null);
        return 0;
    }

    internal static void av1_free_sms_tree(ThreadData td)
    {
        td.sms_tree = null;
    }
}
