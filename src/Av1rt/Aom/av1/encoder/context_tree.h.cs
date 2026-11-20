// Port of libaom av1/encoder/context_tree.h
global using static Av1rt.Aom.context_tree;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class context_tree
{
    internal static readonly BLOCK_SIZE[] square = { BLOCK_4X4, BLOCK_8X8, BLOCK_16X16, BLOCK_32X32, BLOCK_64X64, BLOCK_128X128 };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_get_pc_tree_nodes(int is_sb_size_128,
                                              int stat_generation_stage)
    {
        int tree_nodes_inc = is_sb_size_128 != 0 ? 1024 : 0;
        int tree_nodes =
            stat_generation_stage != 0 ? 1 : (tree_nodes_inc + 256 + 64 + 16 + 4 + 1);
        return tree_nodes;
    }
}

// Class: embedded in ThreadData, used through pointers.
internal sealed partial class PC_TREE_SHARED_BUFFERS
{
    internal readonly Ptr<int>[] coeff_buf = new Ptr<int>[MAX_MB_PLANE];
    internal readonly Ptr<int>[] qcoeff_buf = new Ptr<int>[MAX_MB_PLANE];
    internal readonly Ptr<int>[] dqcoeff_buf = new Ptr<int>[MAX_MB_PLANE];

    internal void CopyFrom(PC_TREE_SHARED_BUFFERS o)
    {
        EncArrays.Copy1(coeff_buf, o.coeff_buf);
        EncArrays.Copy1(qcoeff_buf, o.qcoeff_buf);
        EncArrays.Copy1(dqcoeff_buf, o.dqcoeff_buf);
    }
}

// Structure to hold snapshot of coding context during the mode picking process
// Class: allocated per node (av1_alloc_pmc) and used through pointers; never copied as a
// whole (av1_copy_tree_context copies selected fields). `mic` is assigned by value in C
// (ctx->mic = *xd->mi[0]), which maps to mic.CopyFrom(...) (MB_MODE_INFO.CopyFrom, p1a).
// [subset] best_mode_index (CONFIG_INTERNAL_STATS) and the denoiser fields
// (CONFIG_AV1_TEMPORAL_DENOISING) are compiled out.
internal sealed partial class PICK_MODE_CONTEXT
{
    internal readonly MB_MODE_INFO mic = new();
    internal MB_MODE_INFO_EXT_FRAME mbmi_ext_best;
    internal readonly Ptr<byte>[] color_index_map = new Ptr<byte>[2];

    internal readonly Ptr<int>[] coeff = new Ptr<int>[MAX_MB_PLANE];
    internal readonly Ptr<int>[] qcoeff = new Ptr<int>[MAX_MB_PLANE];
    internal readonly Ptr<int>[] dqcoeff = new Ptr<int>[MAX_MB_PLANE];
    internal readonly Ptr<ushort>[] eobs = new Ptr<ushort>[MAX_MB_PLANE];
    internal readonly Ptr<byte>[] txb_entropy_ctx = new Ptr<byte>[MAX_MB_PLANE];
    internal Ptr<byte> tx_type_map;

    internal int num_4x4_blk;
    // For current partition, only if all Y, U, and V transform blocks'
    // coefficients are quantized to 0, skippable is set to 1.
    internal int skippable;
    // [subset] #if CONFIG_INTERNAL_STATS is false here.
    internal RD_STATS rd_stats;

    internal int rd_mode_is_ready; // Flag to indicate whether rd pick mode decision has
    // been made.
    // [subset] #if CONFIG_AV1_TEMPORAL_DENOISING is false here.
}

// Class: tree nodes linked by pointers.
// [subset] horizontala/b, verticala/b, horizontal4, vertical4: !CONFIG_REALTIME_ONLY.
internal sealed partial class PC_TREE
{
    internal PARTITION_TYPE partitioning;
    internal BLOCK_SIZE block_size;
    internal PICK_MODE_CONTEXT none;
    internal readonly PICK_MODE_CONTEXT[] horizontal = new PICK_MODE_CONTEXT[2];
    internal readonly PICK_MODE_CONTEXT[] vertical = new PICK_MODE_CONTEXT[2];
    // [subset] #if !CONFIG_REALTIME_ONLY is false here.
    internal readonly PC_TREE[] split = new PC_TREE[4];
    internal int index;
}

// Class: elements of td->sms_tree[] linked by pointers.
internal sealed partial class SIMPLE_MOTION_DATA_TREE
{
    internal BLOCK_SIZE block_size;
    internal PARTITION_TYPE partitioning;
    internal readonly SIMPLE_MOTION_DATA_TREE[] split = new SIMPLE_MOTION_DATA_TREE[4];

    // Simple motion search_features
    internal readonly FULLPEL_MV[] start_mvs = new FULLPEL_MV[REF_FRAMES];
    internal readonly uint[] sms_none_feat = new uint[2];
    internal readonly uint[] sms_rect_feat = new uint[8];
    internal int sms_none_valid;
    internal int sms_rect_valid;
}
