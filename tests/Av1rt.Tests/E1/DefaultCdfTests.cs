using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.entropy;
using static Av1rt.Aom.entropymode;
using static Av1rt.Aom.entropymv;
using static Av1rt.Aom.seg_common;

namespace Av1rt.Tests.E1;

/// <summary>
/// Every default CDF table (token_cdfs.h, entropymode.c, entropymv.c) as it lands in a
/// FRAME_CONTEXT, compared with libaom's FRAME_CONTEXT memory, plus the counter reset and
/// av1_setup_past_independence.
/// </summary>
[Trait("Category", "Native")]
public class DefaultCdfTests
{
    [Fact]
    public void InitModeProbsMatchesLibaom()
    {
        var fc = new FRAME_CONTEXT();
        var image = new FrameContextImage(fc);
        av1_init_mode_probs(fc);
        byte[] c = image.NewCImage();
        E1Shim.aomshim_e1_init_mode_probs(c);
        image.AssertEqualTo(c);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(60)]
    [InlineData(61)]
    [InlineData(120)]
    [InlineData(121)]
    [InlineData(255)]
    public void DefaultCoefProbsMatchLibaom(int base_qindex)
    {
        var cm = new AV1_COMMON { fc = new FRAME_CONTEXT() };
        cm.quant_params.base_qindex = base_qindex;
        var image = new FrameContextImage(cm.fc);
        av1_default_coef_probs(cm);
        byte[] c = image.NewCImage();
        E1Shim.aomshim_e1_default_coef_probs(base_qindex, c);
        image.AssertEqualTo(c);
    }

    [Fact]
    public void InitMvProbsMatchesLibaom()
    {
        var cm = new AV1_COMMON { fc = new FRAME_CONTEXT() };
        var image = new FrameContextImage(cm.fc);
        av1_init_mv_probs(cm);
        byte[] c = image.NewCImage();
        E1Shim.aomshim_e1_init_mv_probs(c);
        image.AssertEqualTo(c);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ResetCdfSymbolCountersMatchesLibaom(int seed)
    {
        var rng = new Random(seed);
        var fc = new FRAME_CONTEXT();
        var image = new FrameContextImage(fc);
        byte[] c = image.NewCImage();
        rng.NextBytes(c);
        image.FromBytes(c);

        av1_reset_cdf_symbol_counters(fc);
        E1Shim.aomshim_e1_reset_cdf_symbol_counters(c);
        image.AssertEqualTo(c);
    }

    [Theory]
    [InlineData(5, 3, 7, true)]
    [InlineData(80, 9, 4, false)]
    [InlineData(200, 1, 1, true)]
    public void SetupPastIndependenceMatchesLibaom(int base_qindex, int mi_rows, int mi_cols, bool withSegMap)
    {
        var rng = new Random(base_qindex);
        var segData = new short[MAX_SEGMENTS * SEG_LVL_MAX];
        var segMask = new uint[MAX_SEGMENTS];
        for (int i = 0; i < segData.Length; i++) segData[i] = (short)rng.Next(-255, 256);
        for (int i = 0; i < segMask.Length; i++) segMask[i] = (uint)rng.Next(256);
        byte[] segMap = withSegMap ? new byte[mi_rows * mi_cols] : null;
        if (segMap != null) rng.NextBytes(segMap);

        var cm = new AV1_COMMON
        {
            fc = new FRAME_CONTEXT(),
            default_frame_context = new FRAME_CONTEXT(),
            cur_frame = new RefCntBuffer(),
        };
        cm.quant_params.base_qindex = base_qindex;
        cm.cur_frame.mi_rows = mi_rows;
        cm.cur_frame.mi_cols = mi_cols;
        byte[] csSegMap = segMap == null ? null : (byte[])segMap.Clone();
        if (csSegMap != null) cm.cur_frame.seg_map = csSegMap;
        for (int i = 0; i < MAX_SEGMENTS; i++)
        {
            cm.seg.feature_mask[i] = segMask[i];
            for (int j = 0; j < SEG_LVL_MAX; j++) cm.seg.feature_data[i][j] = segData[i * SEG_LVL_MAX + j];
        }
        var fcImage = new FrameContextImage(cm.fc);
        var dfcImage = new FrameContextImage(cm.default_frame_context);

        byte[] cfc = fcImage.NewCImage(), cdfc = dfcImage.NewCImage();
        var deltas = new sbyte[20];
        var lfFlags = new int[2];
        E1Shim.aomshim_e1_setup_past_independence(base_qindex, mi_rows, mi_cols, segMap, segData,
                                                   segMask, cfc, cdfc, deltas, lfFlags);
        av1_setup_past_independence(cm);

        fcImage.AssertEqualTo(cfc);
        dfcImage.AssertEqualTo(cdfc);
        Assert.Equal(1, cm.fc.initialized);
        if (segMap != null) Assert.Equal(segMap, csSegMap);
        for (int i = 0; i < MAX_SEGMENTS; i++)
        {
            Assert.Equal(segMask[i], cm.seg.feature_mask[i]);
            for (int j = 0; j < SEG_LVL_MAX; j++) Assert.Equal(segData[i * SEG_LVL_MAX + j], cm.seg.feature_data[i][j]);
        }
        Assert.Equal(deltas[..8], cm.cur_frame.ref_deltas);
        Assert.Equal(deltas[8..10], cm.cur_frame.mode_deltas);
        Assert.Equal(deltas[10..18], cm.lf.ref_deltas);
        Assert.Equal(deltas[18..20], cm.lf.mode_deltas);
        Assert.Equal(lfFlags[0], cm.lf.mode_ref_delta_enabled);
        Assert.Equal(lfFlags[1], cm.lf.mode_ref_delta_update);
    }
}
