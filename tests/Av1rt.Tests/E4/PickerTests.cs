using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.pickcdef;
using static Av1rt.Aom.picklpf;

namespace Av1rt.Tests.E4;

/// <summary>
/// The Q-based level pickers (av1_pick_filter_level with LPF_PICK_FROM_Q, av1_pick_cdef_from_qp)
/// over every qindex against libaom. Both read av1_ac_quant_QTX (quant_common.c, package e1),
/// so they are skipped while that is still a stub in this branch.
/// </summary>
[Trait("Category", "Native")]
public class PickerTests
{
    [FactUnlessStub("av1_ac_quant_QTX")]
    public void PickCdefFromQpMatchesLibaom()
    {
        for (int qindex = 0; qindex < 256; qindex++)
            for (int frameType = 0; frameType < 4; frameType++)
                for (int skipCdef = 0; skipCdef < 2; skipCdef++)
                    for (int avoidUv = 0; avoidUv < 2; avoidUv++)
                    {
                        var expected = new int[7];
                        E4Shim.aomshim_e4_pick_cdef_from_qp(qindex, frameType, skipCdef, avoidUv, expected);

                        var cm = new AV1_COMMON { seq_params = new SequenceHeader { bit_depth = 8 } };
                        cm.quant_params.base_qindex = qindex;
                        cm.current_frame.frame_type = frameType;
                        av1_pick_cdef_from_qp(cm, skipCdef, 0, avoidUv != 0);
                        CdefInfo ci = cm.cdef_info;
                        int[] actual =
                        {
                            ci.cdef_bits, ci.nb_cdef_strengths, ci.cdef_damping, ci.cdef_strengths[0],
                            ci.cdef_strengths[1], ci.cdef_uv_strengths[0], ci.cdef_uv_strengths[1],
                        };
                        Assert.True(expected.AsSpan().SequenceEqual(actual),
                                    $"qindex {qindex} frame_type {frameType} skip {skipCdef} avoid_uv {avoidUv}: " +
                                    $"[{string.Join(",", actual)}] vs libaom [{string.Join(",", expected)}]");
                    }
    }

    [FactUnlessStub("av1_ac_quant_QTX")]
    public void PickFilterLevelFromQMatchesLibaom()
    {
        var rng = new Random(2024);
        for (int qindex = 0; qindex < 256; qindex++)
            for (int k = 0; k < 40; k++)
            {
                bool layered = rng.Next(3) == 0;
                int[] p =
                {
                    qindex,
                    rng.Next(4),                                   // frame_type
                    rng.Next(5) == 0 ? rng.Next(3) : 1,            // mode (GOOD / REALTIME / ALLINTRA)
                    new[] { 0, 0, 0, 10, 11 }[rng.Next(5)],        // tuning (PSNR / IQ / SSIMULACRA2)
                    rng.Next(8),                                   // sharpness
                    rng.Next(4) == 0 ? 1 : 0,                      // enable_adaptive_sharpness
                    rng.Next(6) == 0 ? rng.Next(3) : 1,            // loopfilter_control (NONE / ALL / REFERENCE)
                    rng.Next(2),                                   // use_nonrd_pick_mode
                    rng.Next(2),                                   // use_fast_fixed_part
                    layered ? 1 + rng.Next(3) : 1,                 // number_temporal_layers
                    layered ? rng.Next(2) : 0,                     // temporal_layer_id
                    new[] { 160, 320, 352, 640, 1280 }[rng.Next(5)], // width
                    new[] { 90, 240, 288, 480, 720 }[rng.Next(5)],   // height
                    new[] { 0, 40000, 60000, 150000 }[rng.Next(4)],  // frame_source_sad
                    rng.Next(5) == 0 ? 4 : 3,                      // LPF_PICK_MINIMAL_LPF / LPF_PICK_FROM_Q
                    rng.Next(5) == 0 ? 2 : 0,                      // pass
                    rng.Next(16),                                  // section_intra_rating
                    rng.Next(2),                                   // non_reference_frame
                };
                var expected = new int[5];
                E4Shim.aomshim_e4_pick_filter_level(p, expected);

                var cpi = new AV1_COMP { ppi = new AV1_PRIMARY() };
                AV1_COMMON cm = cpi.common;
                cm.seq_params = new SequenceHeader { bit_depth = 8 };
                cm.quant_params.base_qindex = p[0];
                cm.current_frame.frame_type = p[1];
                cpi.oxcf.mode = p[2];
                cpi.oxcf.tune_cfg.tuning = p[3];
                cpi.oxcf.algo_cfg.sharpness = p[4];
                cpi.oxcf.algo_cfg.enable_adaptive_sharpness = p[5] != 0;
                cpi.oxcf.algo_cfg.loopfilter_control = p[6];
                cpi.sf.rt_sf.use_nonrd_pick_mode = p[7];
                cpi.sf.rt_sf.use_fast_fixed_part = p[8];
                cpi.svc.number_temporal_layers = p[9];
                cpi.svc.temporal_layer_id = p[10];
                cm.width = p[11];
                cm.height = p[12];
                cpi.rc.frame_source_sad = (ulong)p[13];
                cpi.oxcf.pass = p[15];
                cpi.ppi.twopass.section_intra_rating = (uint)p[16];
                cpi.ppi.rtc_ref.non_reference_frame = p[17];
                cm.lf.filter_level[0] = cm.lf.filter_level[1] = -1;
                cm.lf.filter_level_u = cm.lf.filter_level_v = -1;
                av1_pick_filter_level(null!, cpi, p[14]);
                int[] actual =
                {
                    cm.lf.filter_level[0], cm.lf.filter_level[1], cm.lf.filter_level_u, cm.lf.filter_level_v,
                    cm.lf.sharpness_level,
                };
                Assert.True(expected.AsSpan().SequenceEqual(actual),
                            $"params [{string.Join(",", p)}]: [{string.Join(",", actual)}] vs libaom [{string.Join(",", expected)}]");
            }
    }
}
