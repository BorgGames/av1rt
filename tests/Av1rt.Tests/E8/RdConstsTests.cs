using System;
using System.Collections.Generic;
using System.Reflection;
using Av1rt.Aom;
using Xunit;
using Xunit.Abstractions;
using static Av1rt.Aom.aomcx;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.encoder;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.ratectrl;

namespace Av1rt.Tests.E8;

// End-to-end check of av1_set_rd_speed_thresholds + av1_initialize_rd_consts (RDMULT,
// errorperbit, set_block_thresholds, compute_rd_thresh_factor, the cost update frequency
// logic, av1_fill_coeff_costs and av1_fill_mode_rates) against libaom, starting from libaom's
// default CDFs for the frame's base_qindex. The shim leaves x->mv_costs NULL (as the C# MACROBLOCK
// does), so av1_fill_mv_costs returns early on both sides.
public class RdConstsTests
{
    private readonly ITestOutputHelper _output;

    public RdConstsTests(ITestOutputHelper output)
    {
        _output = output;
        // libaom does this in av1_initialize_enc; the 2- and 4-symbol cost paths read the LUT.
        Av1rt.Aom.cost.av1_init_cost_luts();
    }

    // FRAME_CONTEXT members up to `initialized`, in the declaration order of entropymode.h.
    private static readonly string[] FrameContextFields =
    {
        "txb_skip_cdf", "eob_extra_cdf", "dc_sign_cdf", "eob_flag_cdf16", "eob_flag_cdf32", "eob_flag_cdf64",
        "eob_flag_cdf128", "eob_flag_cdf256", "eob_flag_cdf512", "eob_flag_cdf1024", "coeff_base_eob_cdf",
        "coeff_base_cdf", "coeff_br_cdf", "newmv_cdf", "zeromv_cdf", "refmv_cdf", "drl_cdf",
        "inter_compound_mode_cdf", "compound_type_cdf", "wedge_idx_cdf", "interintra_cdf", "wedge_interintra_cdf",
        "interintra_mode_cdf", "motion_mode_cdf", "obmc_cdf", "palette_y_size_cdf", "palette_uv_size_cdf",
        "palette_y_color_index_cdf", "palette_uv_color_index_cdf", "palette_y_mode_cdf", "palette_uv_mode_cdf",
        "comp_inter_cdf", "single_ref_cdf", "comp_ref_type_cdf", "uni_comp_ref_cdf", "comp_ref_cdf",
        "comp_bwdref_cdf", "txfm_partition_cdf", "compound_index_cdf", "comp_group_idx_cdf", "skip_mode_cdfs",
        "skip_txfm_cdfs", "intra_inter_cdf", "nmvc", "ndvc", "intrabc_cdf", "seg", "filter_intra_cdfs",
        "filter_intra_mode_cdf", "switchable_restore_cdf", "wiener_restore_cdf", "sgrproj_restore_cdf",
        "y_mode_cdf", "uv_mode_cdf", "partition_cdf", "switchable_interp_cdf", "kf_y_cdf", "angle_delta_cdf",
        "tx_size_cdf", "delta_q_cdf", "delta_lf_multi_cdf", "delta_lf_cdf", "intra_ext_tx_cdf",
        "inter_ext_tx_cdf", "cfl_sign_cdf", "cfl_alpha_cdf",
    };

    // ModeCosts members in the declaration order of block.h.
    private static readonly string[] ModeCostFields =
    {
        "partition_cost", "mbmode_cost", "y_mode_costs", "intra_uv_mode_cost", "filter_intra_cost",
        "filter_intra_mode_cost", "angle_delta_cost", "cfl_cost", "intrabc_cost", "palette_y_size_cost",
        "palette_uv_size_cost", "palette_y_color_cost", "palette_uv_color_cost", "palette_y_mode_cost",
        "palette_uv_mode_cost", "skip_mode_cost", "newmv_mode_cost", "zeromv_mode_cost", "refmv_mode_cost",
        "drl_mode_cost0", "single_ref_cost", "comp_inter_cost", "comp_ref_type_cost", "uni_comp_ref_cost",
        "comp_ref_cost", "comp_bwdref_cost", "intra_inter_cost", "inter_compound_mode_cost", "compound_type_cost",
        "wedge_idx_cost", "interintra_cost", "wedge_interintra_cost", "interintra_mode_cost", "comp_idx_cost",
        "comp_group_idx_cost", "motion_mode_cost", "motion_mode_cost1", "switchable_interp_costs",
        "skip_txfm_cost", "tx_size_cost", "txfm_partition_cost", "inter_tx_type_costs", "intra_tx_type_costs",
        "switchable_restore_cost", "wiener_restore_cost", "sgrproj_restore_cost", "tmp_pred_cost",
        "spatial_pred_cost",
    };

    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static void LoadCdf(object o, ushort[] src, ref int pos)
    {
        switch (o)
        {
            case ushort[] a:
                Array.Copy(src, pos, a, 0, a.Length);
                pos += a.Length;
                return;
            case nmv_context n:
                LoadCdf(n.joints_cdf, src, ref pos);
                foreach (nmv_component c in n.comps)
                {
                    LoadCdf(c.classes_cdf, src, ref pos);
                    LoadCdf(c.class0_fp_cdf, src, ref pos);
                    LoadCdf(c.fp_cdf, src, ref pos);
                    LoadCdf(c.sign_cdf, src, ref pos);
                    LoadCdf(c.class0_hp_cdf, src, ref pos);
                    LoadCdf(c.hp_cdf, src, ref pos);
                    LoadCdf(c.class0_cdf, src, ref pos);
                    LoadCdf(c.bits_cdf, src, ref pos);
                }
                return;
            case segmentation_probs s:
                LoadCdf(s.pred_cdf, src, ref pos);
                LoadCdf(s.spatial_pred_seg_cdf, src, ref pos);
                return;
            case Array arr:
                foreach (object e in arr) LoadCdf(e, src, ref pos);
                return;
            default:
                throw new InvalidOperationException(o.GetType().Name);
        }
    }

    private static void Flatten(object o, List<int> output)
    {
        switch (o)
        {
            case int[] a:
                output.AddRange(a);
                return;
            case Array arr:
                foreach (object e in arr) Flatten(e, output);
                return;
            default:
                throw new InvalidOperationException(o.GetType().Name);
        }
    }

    // Returns the values and, for diagnostics, the name of the member each value belongs to.
    private static (int[] Values, string[] Names) Serialize(AV1_COMP cpi)
    {
        var o = new List<int>(64 * 1024);
        var names = new List<string>(64 * 1024);
        void Add(object member, string name)
        {
            if (member is int v) o.Add(v);
            else Flatten(member, o);
            while (names.Count < o.Count) names.Add(name);
        }
        Add(cpi.rd.RDMULT, "rd.RDMULT");
        Add(cpi.td.mb.errorperbit, "errorperbit");
        Add(cpi.rd.thresh_mult, "rd.thresh_mult");
        Add(cpi.rd.threshes, "rd.threshes");
        ModeCosts mc = cpi.td.mb.mode_costs;
        foreach (string name in ModeCostFields)
            Add(typeof(ModeCosts).GetField(name, AnyInstance)!.GetValue(mc)!, "mode_costs." + name);
        CoeffCosts cc = cpi.td.mb.coeff_costs;
        for (int t = 0; t < cc.coeff_costs.Length; ++t)
            for (int pl = 0; pl < cc.coeff_costs[t].Length; ++pl)
            {
                LV_MAP_COEFF_COST c = cc.coeff_costs[t][pl];
                string at = $"coeff_costs[{t}][{pl}].";
                Add(c.txb_skip_cost, at + "txb_skip_cost");
                Add(c.base_eob_cost, at + "base_eob_cost");
                Add(c.base_cost, at + "base_cost");
                Add(c.eob_extra_cost, at + "eob_extra_cost");
                Add(c.dc_sign_cost, at + "dc_sign_cost");
                Add(c.lps_cost, at + "lps_cost");
            }
        for (int s = 0; s < cc.eob_costs.Length; ++s)
            for (int pl = 0; pl < cc.eob_costs[s].Length; ++pl)
                Add(cc.eob_costs[s][pl].eob_cost, $"eob_costs[{s}][{pl}].eob_cost");
        MvCosts mv = cpi.td.mb.mv_costs;
        if (mv != null)
        {
            Add(mv.nmv_joint_cost, "mv_costs.nmv_joint_cost");
            Add(mv.nmv_cost_alloc, "mv_costs.nmv_cost_alloc");
            Add(mv.nmv_cost_hp_alloc, "mv_costs.nmv_cost_hp_alloc");
            for (int k = 0; k < 2; ++k)
            {
                Add(PtrOffset(mv.nmv_cost[k], mv.nmv_cost_alloc[k]), $"mv_costs.nmv_cost[{k}] offset");
                Add(PtrOffset(mv.nmv_cost_hp[k], mv.nmv_cost_hp_alloc[k]), $"mv_costs.nmv_cost_hp[{k}] offset");
            }
            int stack = mv.mv_cost_stack == null ? -1
                : ReferenceEquals(mv.mv_cost_stack, mv.nmv_cost) ? 0
                : ReferenceEquals(mv.mv_cost_stack, mv.nmv_cost_hp) ? 1
                : 2;
            Add(stack, "mv_costs.mv_cost_stack");
        }
        return (o.ToArray(), names.ToArray());
    }

    private static int PtrOffset(Ptr<int> p, int[] array)
    {
        if (p.IsNull) return -1;
        Assert.Same(array, p.Array);
        return p.Offset;
    }

    private static void Check(int[] p)
    {
        var cdfs = new ushort[1 << 16];
        var expected = new int[1 << 18];
        int n = E8Shim.aomshim_e8_rd_consts(p, cdfs, cdfs.Length, out int cdfCount, expected, expected.Length);
        Assert.InRange(cdfCount, 1, cdfs.Length);
        Assert.InRange(n, 1, expected.Length);

        AV1_COMP cpi = E8Context.Create(p);
        int pos = 0;
        foreach (string name in FrameContextFields)
            LoadCdf(typeof(FRAME_CONTEXT).GetField(name, AnyInstance)!.GetValue(cpi.common.fc)!, cdfs, ref pos);
        Assert.Equal(cdfCount, pos);

        E8Context.SetSpeedFeatures(cpi);
        rd.av1_set_rd_speed_thresholds(cpi);
        rd.av1_initialize_rd_consts(cpi);
        var (actual, names) = Serialize(cpi);
        Assert.True(n == actual.Length, $"value count: C {n}, C# {actual.Length}");
        for (int i = 0; i < n; ++i)
            if (expected[i] != actual[i])
                Assert.Fail($"{P.Describe(p)}: value #{i} ({names[i]}, element {i - Array.IndexOf(names, names[i])}) " +
                            $"C {expected[i]} C# {actual[i]}");
    }

    [Fact]
    public void RdThresholdsAndCostsMatchLibaom() => Deferred.Run(_output, () =>
    {
        (int ft, int ut, int fsk)[] frames =
        {
            (KEY_FRAME, KF_UPDATE, 0), (INTER_FRAME, LF_UPDATE, 1), (INTER_FRAME, LF_UPDATE, 30),
            (INTER_FRAME, GF_UPDATE, 12),
        };
        for (int speed = 5; speed <= 11; ++speed)
            foreach (var (ft, ut, fsk) in frames)
                foreach (int q in new[] { 0, 1, 37, 60, 100, 121, 160, 200, 255 })
                {
                    int[] p = P.Default(speed, 640, 480);
                    p[P.FRAME_TYPE] = ft;
                    p[P.UPDATE_TYPE] = ut;
                    p[P.FRAMES_SINCE_KEY] = fsk;
                    p[P.FRAME_NUMBER] = fsk;
                    p[P.BASE_QINDEX] = q;
                    Check(p);
                }
    });

    [Fact]
    public void RdConfigurationVariantsMatchLibaom() => Deferred.Run(_output, () =>
    {
        var variants = new List<Action<int[]>>
        {
            p => p[P.Y_DC_DELTA_Q] = 5,
            p => { p[P.Y_DC_DELTA_Q] = -8; p[P.BASE_QINDEX] = 3; },
            p => p[P.SEG_ENABLED] = 1,
            p => { p[P.SEG_ENABLED] = 1; p[P.BASE_QINDEX] = 10; },
            p => { p[P.SEG_ENABLED] = 1; p[P.BASE_QINDEX] = 240; },
            p => p[P.SKIP_MODE_FLAG] = 1,
            p => p[P.DISABLE_CDF_UPDATE] = 1,
            p => { p[P.DISABLE_CDF_UPDATE] = 1; p[P.FRAMES_SINCE_KEY] = 40; },
            p => { p[P.COST_UPD_COEFF] = COST_UPD_OFF; p[P.COST_UPD_MODE] = COST_UPD_OFF; p[P.COST_UPD_MV] = COST_UPD_OFF; },
            p => { p[P.COST_UPD_COEFF] = COST_UPD_TILE; p[P.COST_UPD_MODE] = COST_UPD_SBROW; p[P.FRAMES_SINCE_KEY] = 40; },
            p => { p[P.COST_UPD_MODE] = COST_UPD_TILE; p[P.FRAMES_SINCE_KEY] = 40; },
            p => { p[P.FRAMES_SINCE_KEY] = 40; p[P.FRAME_NUMBER] = 41; },
            p => { p[P.FRAMES_SINCE_KEY] = 40; p[P.FRAME_NUMBER] = 48; },
            p => { p[P.FRAMES_SINCE_KEY] = 40; p[P.FRAME_NUMBER] = 43; p[P.HIGH_SOURCE_SAD] = 1; },
            p => { p[P.FRAMES_SINCE_KEY] = 40; p[P.FRAME_NUMBER] = 43; p[P.MAXQ_SCENE_CHANGE] = 3; },
            p => { p[P.FRAMES_SINCE_KEY] = 40; p[P.FRAME_NUMBER] = 43; p[P.CDF_UPDATE_MODE] = 2; },
            p => { p[P.FRAMES_SINCE_KEY] = 40; p[P.FRAME_NUMBER] = 43; p[P.CDF_UPDATE_MODE] = 2; p[P.FRAMES_SINCE_LAST_UPDATE] = 2; },
            p => { p[P.FRAMES_SINCE_KEY] = 40; p[P.FRAME_NUMBER] = 43; p[P.CDF_UPDATE_MODE] = 0; },
            p => { p[P.LAYER_DEPTH] = 3; p[P.GFU_BOOST] = 800; },
            p => { p[P.USE_FIXED_QP_OFFSETS] = 1; p[P.UPDATE_TYPE] = ARF_UPDATE; },
            p => p[P.TUNING] = AOM_TUNE_IQ,
            p => p[P.TUNING] = AOM_TUNE_SSIMULACRA2,
            p => { p[P.ALLOW_HP] = 1; p[P.FORCE_INTEGER_MV] = 1; },
            p => { p[P.WIDTH] = 1920; p[P.HEIGHT] = 1080; p[P.CFG_WIDTH] = 1920; p[P.CFG_HEIGHT] = 1080; },
            p => { p[P.NUM_WORKERS] = 4; p[P.ROW_MT] = 1; },
        };
        foreach (Action<int[]> variant in variants)
            for (int speed = 5; speed <= 11; ++speed)
                foreach (int q in new[] { 30, 140, 230 })
                {
                    int[] p = P.Default(speed, 640, 480);
                    p[P.BASE_QINDEX] = q;
                    variant(p);
                    Check(p);
                }
    });

    // With x->mv_costs allocated: av1_fill_mv_costs (both precisions, integer mv) through
    // av1_initialize_rd_consts. Needs av1_build_nmv_cost_table (encodemv.c).
    [Fact]
    public void MvCostsMatchLibaom() => Deferred.Run(_output, () =>
    {
        foreach (int speed in new[] { 5, 6, 7, 9, 11 })
            foreach (var (ft, ut, fsk) in new[] { (KEY_FRAME, KF_UPDATE, 0), (INTER_FRAME, LF_UPDATE, 1), (INTER_FRAME, LF_UPDATE, 9) })
                for (int hp = 0; hp <= 1; ++hp)
                    for (int integer = 0; integer <= 1; ++integer)
                    {
                        int[] p = P.Default(speed, 640, 480);
                        p[P.ALLOC_MV_COSTS] = 1;
                        p[P.FRAME_TYPE] = ft;
                        p[P.UPDATE_TYPE] = ut;
                        p[P.FRAMES_SINCE_KEY] = fsk;
                        p[P.ALLOW_HP] = hp;
                        p[P.FORCE_INTEGER_MV] = integer;
                        Check(p);
                    }
    });
}
