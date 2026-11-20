using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.quant_common;
using static Av1rt.Aom.seg_common;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.E1;

/// <summary>quant_common.c and seg_common.c against libaom.</summary>
public class QuantSegTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void DcAcQuantMatchLibaom(int bit_depth)
    {
        for (int qindex = 0; qindex <= MAXQ; qindex++)
        {
            for (int delta = -300; delta <= 300; delta++)
            {
                Assert.Equal(E1Shim.aomshim_e1_dc_quant_QTX(qindex, delta, bit_depth),
                             av1_dc_quant_QTX(qindex, delta, bit_depth));
                Assert.Equal(E1Shim.aomshim_e1_ac_quant_QTX(qindex, delta, bit_depth),
                             av1_ac_quant_QTX(qindex, delta, bit_depth));
            }
        }
    }

    [Fact]
    public void GetQindexMatchesLibaom()
    {
        var rng = new Random(7);
        for (int iter = 0; iter < 2000; iter++)
        {
            var seg = new segmentation { enabled = (byte)rng.Next(2) };
            var mask = new uint[MAX_SEGMENTS];
            var data = new short[MAX_SEGMENTS * SEG_LVL_MAX];
            for (int i = 0; i < MAX_SEGMENTS; i++)
            {
                mask[i] = (uint)rng.Next(256);
                seg.feature_mask[i] = mask[i];
                for (int j = 0; j < SEG_LVL_MAX; j++)
                {
                    data[i * SEG_LVL_MAX + j] = (short)rng.Next(-300, 300);
                    seg.feature_data[i][j] = data[i * SEG_LVL_MAX + j];
                }
            }
            int segment_id = rng.Next(MAX_SEGMENTS);
            int base_qindex = rng.Next(256);
            Assert.Equal(E1Shim.aomshim_e1_get_qindex(seg.enabled, mask, data, segment_id, base_qindex),
                         av1_get_qindex(seg, segment_id, base_qindex));
        }
    }

    [Fact]
    public void UseQmatrixMatchesLibaom()
    {
        var rng = new Random(3);
        for (int iter = 0; iter < 200; iter++)
        {
            var qp = new CommonQuantParams { using_qmatrix = rng.Next(2) == 1 };
            var xd = new MACROBLOCKD();
            var lossless = new int[MAX_SEGMENTS];
            for (int i = 0; i < MAX_SEGMENTS; i++) xd.lossless[i] = lossless[i] = rng.Next(2);
            int segment_id = rng.Next(MAX_SEGMENTS);
            Assert.Equal(E1Shim.aomshim_e1_use_qmatrix(qp.using_qmatrix ? 1 : 0, lossless, segment_id) != 0,
                         av1_use_qmatrix(qp, xd, segment_id));
        }
    }

    [Fact]
    public void QmInitLeavesNoMatrices()
    {
        // The QM tables are not ported (quantization matrices are off in the subset): every
        // pointer av1_qm_init sets stays NULL, in particular level NUM_QM_LEVELS - 1, the only
        // one read when using_qmatrix == 0, which is NULL in libaom too.
        var qp = new CommonQuantParams();
        av1_qm_init(qp, 3);
        for (int q = 0; q < NUM_QM_LEVELS; q++)
            for (int c = 0; c < 3; c++)
                for (int t = 0; t < TX_SIZES_ALL; t++)
                {
                    Assert.True(qp.gqmatrix[q][c][t].IsNull);
                    Assert.True(qp.giqmatrix[q][c][t].IsNull);
                }
    }

    [Fact]
    public void SegFeatureLimitsMatchLibaom()
    {
        for (int f = 0; f < SEG_LVL_MAX; f++)
        {
            Assert.Equal(E1Shim.aomshim_e1_seg_feature_data_max(f), av1_seg_feature_data_max(f));
            Assert.Equal(E1Shim.aomshim_e1_is_segfeature_signed(f), av1_is_segfeature_signed(f));
        }
    }

    [Fact]
    public void CalculateSegdataMatchesLibaom()
    {
        var rng = new Random(11);
        for (int iter = 0; iter < 2000; iter++)
        {
            var mask = new uint[MAX_SEGMENTS];
            var seg = new segmentation { segid_preskip = 77, last_active_segid = 77 };
            for (int i = 0; i < MAX_SEGMENTS; i++)
            {
                // Sparse masks so that "no feature" and "only low features" both occur.
                mask[i] = rng.Next(3) == 0 ? (uint)rng.Next(256) : (rng.Next(4) == 0 ? 1u << rng.Next(8) : 0);
                seg.feature_mask[i] = mask[i];
            }
            E1Shim.aomshim_e1_calculate_segdata(mask, out int preskip, out int last);
            av1_calculate_segdata(seg);
            Assert.Equal(preskip, seg.segid_preskip);
            Assert.Equal(last, seg.last_active_segid);
        }
    }

    [Fact]
    public void SegFeatureSettersBehaveLikeC()
    {
        var seg = new segmentation();
        av1_enable_segfeature(seg, 3, SEG_LVL_ALT_Q);
        av1_enable_segfeature(seg, 3, SEG_LVL_SKIP);
        av1_set_segdata(seg, 3, SEG_LVL_ALT_Q, -42);
        Assert.Equal((1u << SEG_LVL_ALT_Q) | (1u << SEG_LVL_SKIP), seg.feature_mask[3]);
        Assert.Equal(-42, seg.feature_data[3][SEG_LVL_ALT_Q]);
        av1_clearall_segfeatures(seg);
        Assert.All(seg.feature_mask, m => Assert.Equal(0u, m));
        Assert.All(seg.feature_data, row => Assert.All(row, v => Assert.Equal(0, v)));
    }
}
