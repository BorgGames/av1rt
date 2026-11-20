using System;
using System.Collections.Generic;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aom_encoder;
using static Av1rt.Aom.av1_common_int;
using static Av1rt.Aom.context_tree;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.speed_features;

namespace Av1rt.Tests.F3;

// Differential tests of context_tree.c against libaom (shim_f3.c).
[Trait("Category", "Native")]
public class ContextTreeTests
{
    // av1_setup_sms_tree(): node block sizes, child links and the root.
    [Theory]
    [InlineData(0, 9999)]
    [InlineData(1, 9999)]
    [InlineData(0, 0)]
    public void SmsTreeMatchesLibaom(int sb128, int keyFreqMax)
    {
        const int cap = 2048;
        var cBlockSize = new int[cap];
        var cSplit = new int[4 * cap];
        int n = F3Shim.aomshim_f3_sms_tree(sb128, keyFreqMax, cBlockSize, cSplit, cap, out int cRoot);
        Assert.True(n >= 0);

        var cpi = new AV1_COMP();
        cpi.ppi = new AV1_PRIMARY();
        cpi.common.seq_params = cpi.ppi.seq_params;
        cpi.common.seq_params.sb_size = sb128 != 0 ? BLOCK_128X128 : BLOCK_64X64;
        cpi.oxcf.kf_cfg.key_freq_max = keyFreqMax;
        cpi.oxcf.pass = AOM_RC_ONE_PASS;
        ThreadData td = cpi.td;
        Assert.Equal(0, av1_setup_sms_tree(cpi, td));
        if (n == 0)
        {
            Assert.Null(td.sms_tree);
            return;
        }
        Assert.Equal(n, td.sms_tree.Length);
        var index = new Dictionary<SIMPLE_MOTION_DATA_TREE, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < n; ++i) index[td.sms_tree[i]] = i;
        for (int i = 0; i < n; ++i)
        {
            Assert.True(cBlockSize[i] == td.sms_tree[i].block_size, $"node {i} block_size");
            for (int j = 0; j < 4; ++j)
            {
                SIMPLE_MOTION_DATA_TREE child = td.sms_tree[i].split[j];
                int actual = child == null ? -1 : index[child];
                Assert.True(cSplit[4 * i + j] == actual, $"node {i} split[{j}]: C {cSplit[4 * i + j]} C# {actual}");
            }
        }
        Assert.Equal(cRoot, index[td.sms_root]);
        av1_free_sms_tree(td);
        Assert.Null(td.sms_tree);
    }

    // av1_alloc_pmc() / av1_reset_pmc() / av1_alloc_pc_tree_node() for every block size.
    [Fact]
    public void PickModeContextAndPcTreeMatchLibaom()
    {
        for (int monochrome = 0; monochrome < 2; ++monochrome)
        for (int sb128 = 0; sb128 < 2; ++sb128)
        for (int sct = 0; sct < 2; ++sct)
        for (int bsize = 0; bsize < BLOCK_SIZES_ALL; ++bsize)
        {
            var expected = new int[19];
            F3Shim.aomshim_f3_alloc_pmc(bsize, monochrome, sb128, sct, expected);

            var cpi = new AV1_COMP();
            cpi.ppi = new AV1_PRIMARY();
            AV1_COMMON cm = cpi.common;
            cm.seq_params = cpi.ppi.seq_params;
            cm.seq_params.monochrome = (byte)monochrome;
            cm.seq_params.subsampling_x = 1;
            cm.seq_params.subsampling_y = 1;
            cm.seq_params.sb_size = sb128 != 0 ? BLOCK_128X128 : BLOCK_64X64;
            cm.features.allow_screen_content_tools = sct != 0;
            var shared = new PC_TREE_SHARED_BUFFERS();
            av1_setup_shared_coeff_buffer(cm.seq_params, shared, new aom_internal_error_info());

            PICK_MODE_CONTEXT ctx = av1_alloc_pmc(cpi, bsize, shared);
            int numPlanes = av1_num_planes(cm);
            var actual = new int[19];
            actual[0] = ctx.num_4x4_blk;
            actual[1] = ctx.rd_mode_is_ready;
            actual[2] = ctx.rd_stats.rate;
            actual[3] = ctx.rd_stats.zero_rate;
            actual[4] = ctx.rd_stats.skip_txfm;
            actual[5] = ctx.rd_stats.dist == long.MaxValue ? 1 : 0;
            actual[6] = ctx.rd_stats.rdcost == long.MaxValue ? 1 : 0;
            actual[7] = ctx.rd_stats.sse == long.MaxValue ? 1 : 0;
            for (int i = 0; i < numPlanes; ++i)
            {
                actual[8] |= (ctx.coeff[i] == shared.coeff_buf[i] ? 1 : 0) << i;
                actual[9] |= (ctx.qcoeff[i] == shared.qcoeff_buf[i] ? 1 : 0) << i;
                actual[10] |= (ctx.dqcoeff[i] == shared.dqcoeff_buf[i] ? 1 : 0) << i;
                // Port-specific: the per-plane arrays hold at least what C's 32-byte rounded slices do.
                Assert.True(ctx.eobs[i].Array.Length >= ctx.num_4x4_blk);
                Assert.True(ctx.txb_entropy_ctx[i].Array.Length >= ctx.num_4x4_blk);
                int maxPix = 1 << common_data.num_pels_log2_lookup[cm.seq_params.sb_size];
                Assert.Equal(i == 0 ? maxPix : maxPix >> 2, shared.coeff_buf[i].Array.Length);
            }
            actual[11] = ctx.color_index_map[0].IsNull ? 0 : 1;
            actual[12] = ctx.color_index_map[1].IsNull ? 0 : 1;
            ctx.tx_type_map.AsSpan(ctx.num_4x4_blk).Fill(7);
            ctx.rd_stats.rate = 5;
            av1_reset_pmc(ctx);
            actual[13] = ctx.tx_type_map.AsSpan(ctx.num_4x4_blk).IndexOfAnyExcept((byte)0) < 0 ? 1 : 0;
            actual[14] = ctx.rd_stats.rate;
            av1_free_pmc(ctx, numPlanes);

            PC_TREE tree = av1_alloc_pc_tree_node(bsize);
            actual[15] = tree.partitioning;
            actual[16] = tree.block_size;
            actual[17] = tree.index;
            actual[18] = numPlanes;
            av1_free_pc_tree_recursive(tree, numPlanes, 0, 0, VAR_BASED_PARTITION);
            av1_free_shared_coeff_buffer(shared);

            Assert.True(expected.AsSpan().SequenceEqual(actual),
                        $"bsize={bsize} mono={monochrome} sb128={sb128} sct={sct}: C [{string.Join(",", expected)}] " +
                        $"C# [{string.Join(",", actual)}]");
        }
    }

    // av1_free_pc_tree_recursive(): both the VAR_BASED_PARTITION path and the general path with
    // keep_best / keep_none.
    [Fact]
    public void FreePcTreeRecursiveReleasesNodes()
    {
        static PC_TREE Build(int bsize, int depth)
        {
            PC_TREE t = av1_alloc_pc_tree_node(bsize);
            t.none = new PICK_MODE_CONTEXT();
            for (int i = 0; i < 2; ++i)
            {
                t.horizontal[i] = new PICK_MODE_CONTEXT();
                t.vertical[i] = new PICK_MODE_CONTEXT();
            }
            if (depth > 0)
                for (int i = 0; i < 4; ++i) t.split[i] = Build(bsize - 3, depth - 1);
            return t;
        }

        PC_TREE a = Build(BLOCK_64X64, 2);
        av1_free_pc_tree_recursive(a, 3, 0, 0, VAR_BASED_PARTITION);
        Assert.Null(a.none);
        Assert.Null(a.horizontal[1]);
        Assert.Null(a.vertical[0]);
        Assert.All(a.split, Assert.Null);

        PC_TREE b = Build(BLOCK_64X64, 1);
        b.partitioning = PARTITION_HORZ;
        av1_free_pc_tree_recursive(b, 3, 1, 0, FIXED_PARTITION);
        Assert.Null(b.none);
        Assert.NotNull(b.horizontal[0]);
        Assert.NotNull(b.horizontal[1]);
        Assert.Null(b.vertical[0]);
        Assert.All(b.split, Assert.Null);

        PC_TREE c = Build(BLOCK_64X64, 1);
        c.partitioning = PARTITION_SPLIT;
        av1_free_pc_tree_recursive(c, 3, 0, 1, FIXED_PARTITION);
        Assert.NotNull(c.none);
        Assert.Null(c.horizontal[0]);
        Assert.All(c.split, Assert.Null);

        PC_TREE d = Build(BLOCK_64X64, 1);
        d.partitioning = PARTITION_SPLIT;
        av1_free_pc_tree_recursive(d, 3, 1, 0, FIXED_PARTITION);
        Assert.Null(d.none);
        Assert.All(d.split, Assert.NotNull);
    }
}
