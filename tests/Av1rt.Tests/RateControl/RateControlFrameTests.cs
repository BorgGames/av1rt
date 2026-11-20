using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aq_cyclicrefresh;
using static Av1rt.Aom.ratectrl;
using static Av1rt.Tests.RateControl.E7Harness;

namespace Av1rt.Tests.RateControl;

/// <summary>Differential tests that need source frames or block data: scene detection,
/// av1_get_one_pass_rt_params, cyclic refresh segment updates, and a multi-frame rate
/// control simulation.</summary>
[Collection("e7")]
public class RateControlFrameTests
{
    private const uint FRAMEFLAGS_KEY = 1;

    /// <summary>A source / last-source pair: static, noisy, moving or a scene cut.</summary>
    private static (byte[] src, byte[] last, int stride) MakeFrames(Random r, int w, int h)
    {
        (byte[] last, int stride) = NewPlane(w, h);
        byte[] src = new byte[last.Length];
        int rows = last.Length / stride;
        int kind = r.Next(5);
        int bg = r.Next(256);
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < stride; x++)
                last[y * stride + x] = (byte)Math.Clamp(bg + ((x * 3 + y * 5) & 31) + r.Next(-2, 3), 0, 255);
        for (int i = 0; i < src.Length; i++)
        {
            src[i] = kind switch
            {
                0 => last[i],                                                // static
                1 => (byte)Math.Clamp(last[i] + r.Next(-3, 4), 0, 255),      // noise
                2 => last[(i + 7) % last.Length],                            // motion
                3 => (byte)r.Next(256),                                      // scene cut
                _ => (byte)Math.Clamp(last[i] + (i / stride > rows / 2 ? 60 : 0), 0, 255),
            };
        }
        return (src, last, stride);
    }

    private static YV12_BUFFER_CONFIG Plane(byte[] buf, int stride, int w, int h)
    {
        var b = new YV12_BUFFER_CONFIG();
        b.y_buffer = buf;
        b.y_stride = stride;
        b.y_width = w;
        b.y_height = h;
        return b;
    }

    private static void AssertBlkSad(int expectedSize, ulong[] expected, AV1_COMP cpi, string context)
    {
        if (expectedSize < 0)
        {
            Assert.True(cpi.src_sad_blk_64x64.IsNull, context);
            return;
        }
        Assert.False(cpi.src_sad_blk_64x64.IsNull, context);
        Assert.Equal(expectedSize, cpi.src_sad_blk_alloc_size);
        for (int i = 0; i < expectedSize; i++)
            Assert.True(expected[i] == cpi.src_sad_blk_64x64[i], $"{context}: blk {i}");
    }

    [Fact]
    public void SceneDetectionOnepassRt()
    {
        var r = new Random(7201);
        for (int iter = 0; iter < 400; iter++)
        {
            E7State c = RandomState(r);
            int w = (int)c.common_width, h = (int)c.common_height;
            (byte[] src, byte[] last, int stride) = MakeFrames(r, w, h);
            int last_w = w, last_h = h;
            if (r.Next(20) == 0) last_w = w - 8;   // size mismatch: early return
            if (r.Next(20) == 0) c.common_render_width = w + 16;
            if (r.Next(4) == 0) c.rc_frames_since_key = r.Next(0, 4);
            var blk = new ulong[4096];
            E7State p = c;
            int blkSize = E7Shim.e7_scene_detection(ref c, src, last, stride, w, h, last_w, last_h, blk, blk.Length);
            AV1_COMP cpi = NewCpi(p);
            var fi = new EncodeFrameInput { source = Plane(src, stride, w, h), last_source = Plane(last, stride, last_w, last_h) };
            av1_rc_scene_detection_onepass_rt(cpi, fi);
            p.Store(cpi);
            AssertSame(c, p, $"iter {iter}");
            AssertBlkSad(blkSize, blk, cpi, $"iter {iter}");
        }
    }

    [Fact]
    public void GetOnePassRtParams()
    {
        var r = new Random(7202);
        for (int iter = 0; iter < 600; iter++)
        {
            E7State c = RandomState(r);
            int w = (int)c.common_width, h = (int)c.common_height;
            uint frame_flags = r.Next(8) == 0 ? FRAMEFLAGS_KEY : 0;
            if (r.Next(4) == 0) c.common_current_frame_frame_number = 0;
            bool withSources = r.Next(6) != 0;
            byte[] src = null, last = null;
            int stride = 0;
            if (withSources) (src, last, stride) = MakeFrames(r, w, h);
            var blk = new ulong[4096];
            E7State p = c;
            int expectedType = E7Shim.e7_get_one_pass_rt_params(ref c, frame_flags, src, last, stride, w, h,
                                                                blk, blk.Length, out int blkSize);
            AV1_COMP cpi = NewCpi(p);
            var fi = new EncodeFrameInput();
            if (withSources)
            {
                fi.source = Plane(src, stride, w, h);
                fi.last_source = Plane(last, stride, w, h);
            }
            int frame_type = INTER_FRAME;
            av1_get_one_pass_rt_params(cpi, ref frame_type, fi, frame_flags);
            p.Store(cpi);
            Assert.Equal(expectedType, frame_type);
            AssertSame(c, p, $"iter {iter}");
            AssertBlkSad(blkSize, blk, cpi, $"iter {iter}");
        }
    }

    [Fact]
    public void CyclicRefreshUpdateSegment()
    {
        var r = new Random(7203);
        int[] bsizes = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 15, 16, 17, 18, 19, 20, 21 };
        for (int iter = 0; iter < 3000; iter++)
        {
            E7State c = RandomState(r);
            int mi_rows = (int)c.common_mi_params_mi_rows, mi_cols = (int)c.common_mi_params_mi_cols;
            int n = mi_rows * mi_cols;
            int bsize = bsizes[r.Next(bsizes.Length)];
            int mi_row = r.Next(mi_rows), mi_col = r.Next(mi_cols);
            long rate = (long)(Math.Exp(r.NextDouble() * 25));
            long dist = (long)(Math.Exp(r.NextDouble() * 25));
            if (r.Next(4) == 0) c.cyclic_refresh_thresh_rate_sb = long.MaxValue;
            if (r.Next(4) == 0) c.cyclic_refresh_thresh_dist_sb = 0;
            c.cyclic_refresh_time_for_refresh = r.Next(3);
            int skip = r.Next(2), dry_run = r.Next(3);
            int mv_row = r.Next(4) == 0 ? 0 : r.Next(-80, 81);
            int mv_col = r.Next(4) == 0 ? 0 : r.Next(-80, 81);
            int ref0 = r.Next(4) == 0 ? 0 : r.Next(1, 8);           // INTRA_FRAME or inter
            int ref1 = r.Next(4) == 0 ? r.Next(1, 8) : -1;          // compound or NONE_FRAME
            int segment_id = r.Next(8);
            var encC = new byte[n]; var segC = new byte[n]; var crC = new sbyte[n];
            for (int i = 0; i < n; i++) crC[i] = (sbyte)r.Next(-2, 2);
            var encP = (byte[])encC.Clone(); var segP = (byte[])segC.Clone(); var crP = (sbyte[])crC.Clone();
            var countsC = new[] { r.Next(0, 1000), r.Next(0, 1000) };
            var countsP = (int[])countsC.Clone();
            E7State p = c;

            int expected = E7Shim.e7_cr_update_segment(ref c, mi_row, mi_col, bsize, rate, dist, skip, dry_run,
                                                       mv_row, mv_col, ref0, ref1, segment_id,
                                                       encC, segC, crC, countsC);

            AV1_COMP cpi = NewCpi(p);
            var x = new MACROBLOCK();
            var mbmi = new MB_MODE_INFO();
            mbmi.mv[0].as_mv.row = (short)mv_row;
            mbmi.mv[0].as_mv.col = (short)mv_col;
            mbmi.ref_frame[0] = ref0;
            mbmi.ref_frame[1] = ref1;
            mbmi.segment_id = (byte)segment_id;
            x.e_mbd.mi = new[] { mbmi };
            x.actual_num_seg1_blocks = countsP[0];
            x.actual_num_seg2_blocks = countsP[1];
            cpi.common.cur_frame = new RefCntBuffer { seg_map = segP };
            cpi.enc_seg.map = encP;
            cpi.cyclic_refresh.map = crP;
            av1_cyclic_refresh_update_segment(cpi, x, mi_row, mi_col, bsize, rate, dist, skip, dry_run);
            p.Store(cpi);

            Assert.Equal(expected, mbmi.segment_id);
            Assert.Equal(countsC[0], x.actual_num_seg1_blocks);
            Assert.Equal(countsC[1], x.actual_num_seg2_blocks);
            Assert.Equal(encC, encP);
            Assert.Equal(segC, segP);
            Assert.Equal(crC, crP);
            AssertSame(c, p, $"iter {iter}");
        }
    }

    [Fact]
    public void CyclicResetSegmentSkip()
    {
        var r = new Random(7204);
        for (int iter = 0; iter < 2000; iter++)
        {
            E7State c = RandomState(r);
            c.common_seg_enabled = 1;
            int mi_rows = (int)c.common_mi_params_mi_rows, mi_cols = (int)c.common_mi_params_mi_cols;
            int bsize = r.Next(0, 22);
            int mi_row = r.Next(mi_rows), mi_col = r.Next(mi_cols);
            int dry_run = r.Next(3), segment_id = r.Next(8);
            var countsC = new[] { r.Next(0, 1000), r.Next(0, 1000) };
            var countsP = (int[])countsC.Clone();
            E7State p = c;
            int expected = E7Shim.e7_cr_reset_segment_skip(ref c, mi_row, mi_col, bsize, dry_run, segment_id, countsC);
            AV1_COMP cpi = NewCpi(p);
            var x = new MACROBLOCK();
            var mbmi = new MB_MODE_INFO { segment_id = (byte)segment_id };
            x.e_mbd.mi = new[] { mbmi };
            x.actual_num_seg1_blocks = countsP[0];
            x.actual_num_seg2_blocks = countsP[1];
            av1_cyclic_reset_segment_skip(cpi, x, mi_row, mi_col, bsize, dry_run);
            p.Store(cpi);
            Assert.Equal(expected, mbmi.segment_id);
            Assert.Equal(countsC[0], x.actual_num_seg1_blocks);
            Assert.Equal(countsC[1], x.actual_num_seg2_blocks);
            AssertSame(c, p, $"iter {iter}");
        }
    }

    /// <summary>Runs both rate controls through whole sequences the way encoder.c drives them
    /// (frame params, drop check, q selection, overshoot handling, post-encode drop and
    /// update), with random encoded sizes and scene cuts, comparing the state after every
    /// call.</summary>
    [Fact]
    public void MultiFrameSimulation()
    {
        var r = new Random(7205);
        for (int seq = 0; seq < 12; seq++)
        {
            E7State c = RandomState(r);
            int w = Math.Min((int)c.common_width, 640), h = Math.Min((int)c.common_height, 480);
            SetSize(ref c, w, h);
            c.x_has_prev_frame = 0;
            c.common_current_frame_frame_number = 0;
            c.rc_frame_number_encoded = 0;
            c.common_show_frame = 1;
            c.refresh_frame_alt_ref_frame = 0;
            c.rc_is_src_frame_alt_ref = 0;
            c.oxcf_rc_cfg_worst_allowed_q = Math.Max(c.oxcf_rc_cfg_worst_allowed_q, c.oxcf_rc_cfg_best_allowed_q);
            E7State p = c;
            E7Shim.e7_rc_init(ref c);
            Run(ref p, cpi => { av1_primary_rc_init(cpi.oxcf, cpi.ppi.p_rc); av1_rc_init(cpi.oxcf, cpi.rc); });
            AssertSame(c, p, $"seq {seq} init");
            c.rc_worst_quality = p.rc_worst_quality = c.oxcf_rc_cfg_worst_allowed_q;
            c.rc_best_quality = p.rc_best_quality = c.oxcf_rc_cfg_best_allowed_q;
            c.ppi_p_rc_buffer_level = p.ppi_p_rc_buffer_level = c.ppi_p_rc_starting_buffer_level;
            c.ppi_p_rc_bits_off_target = p.ppi_p_rc_bits_off_target = c.ppi_p_rc_starting_buffer_level;
            c.rc_frames_to_key = p.rc_frames_to_key = 0;
            c.cyclic_refresh_percent_refresh_adjustment = p.cyclic_refresh_percent_refresh_adjustment = 5;
            c.cyclic_refresh_rate_ratio_qdelta_adjustment = p.cyclic_refresh_rate_ratio_qdelta_adjustment = 0.25;
            E7Shim.e7_rc_update_framerate(ref c);
            Run(ref p, cpi => av1_rc_update_framerate(cpi, cpi.common.width, cpi.common.height));
            AssertSame(c, p, $"seq {seq} framerate");

            (byte[] lastFrame, int stride) = NewPlane(w, h);
            r.NextBytes(lastFrame);
            double complexity = LogUniform(r);
            for (int frame = 0; frame < 120; frame++)
            {
                string ctx = $"seq {seq} frame {frame}";
                // New source: mostly a small change, sometimes a scene cut.
                byte[] src = (byte[])lastFrame.Clone();
                bool cut = r.Next(25) == 0;
                if (cut) { r.NextBytes(src); complexity = LogUniform(r); }
                else for (int i = 0; i < src.Length; i += r.Next(1, 64)) src[i] = (byte)(src[i] + r.Next(-4, 5));

                uint flags = r.Next(60) == 0 ? FRAMEFLAGS_KEY : 0;
                var blk = new ulong[4096];
                int type = E7Shim.e7_get_one_pass_rt_params(ref c, flags, src, lastFrame, stride, w, h, blk, blk.Length, out _);
                int ptype = INTER_FRAME;
                Run(ref p, cpi =>
                {
                    var fi = new EncodeFrameInput { source = Plane(src, stride, w, h), last_source = Plane(lastFrame, stride, w, h) };
                    av1_get_one_pass_rt_params(cpi, ref ptype, fi, flags);
                });
                Assert.Equal(type, ptype);
                AssertSame(c, p, ctx + " params");
                // av1_encode(): key frames restart the frame numbering.
                if (type == KEY_FRAME) c.common_current_frame_frame_number = p.common_current_frame_frame_number = 0;
                c.refresh_frame_golden_frame = p.refresh_frame_golden_frame =
                    type == KEY_FRAME || c.ppi_gf_group_update_type_0 == GF_UPDATE ? 1 : 0;

                int drop = E7Shim.e7_rc_drop_frame(ref c);
                Assert.Equal(drop, Run(ref p, av1_rc_drop_frame));
                AssertSame(c, p, ctx + " drop");
                if (drop != 0)
                {
                    E7Shim.e7_postencode_update_drop_frame(ref c);
                    Run(ref p, cpi => av1_rc_postencode_update_drop_frame(cpi));
                    AssertSame(c, p, ctx + " drop update");
                    continue;
                }

                E7Shim.e7_cr_update_parameters(ref c);
                Run(ref p, av1_cyclic_refresh_update_parameters);
                AssertSame(c, p, ctx + " cr params");

                int q = E7Shim.e7_pick_q_and_bounds(ref c, out _, out _);
                int b0 = 0, t0 = 0;
                Assert.Equal(q, Run(ref p, cpi => av1_rc_pick_q_and_bounds(cpi, w, h, 0, ref b0, ref t0)));
                AssertSame(c, p, ctx + " pick q");

                if (c.rc_high_source_sad != 0 && c.sf_rt_sf_overshoot_detection_cbr == 1 && type != KEY_FRAME)
                {
                    int qc = q, qp = q;
                    E7Shim.e7_encodedframe_overshoot_cbr(ref c, ref qc);
                    Run(ref p, cpi => av1_encodedframe_overshoot_cbr(cpi, ref qp));
                    Assert.Equal(qc, qp);
                    AssertSame(c, p, ctx + " overshoot");
                    q = qc;
                }
                c.common_quant_params_base_qindex = p.common_quant_params_base_qindex = q;
                c.common_seg_enabled = p.common_seg_enabled = c.cyclic_refresh_apply_cyclic_refresh;
                c.cyclic_refresh_actual_num_seg1_blocks = p.cyclic_refresh_actual_num_seg1_blocks =
                    c.cyclic_refresh_apply_cyclic_refresh * c.common_mi_params_mi_rows * c.common_mi_params_mi_cols * c.cyclic_refresh_percent_refresh / 100;

                // A frame size loosely following the model, with random over/undershoot.
                double qv = av1_convert_qindex_to_q(q, AOM_BITS_8);
                double bits = complexity * c.common_mi_params_MBs * 1500000.0 / Math.Max(qv, 1) / 512;
                if (type == KEY_FRAME) bits *= 4;
                if (cut) bits *= 3;
                bits *= Math.Exp(r.NextDouble() - 0.5);
                int size = (int)Math.Max(10, Math.Min(bits / 8, int.MaxValue / 16));

                int sc = size, sp = size;
                int pdrop = E7Shim.e7_postencode_drop_cbr(ref c, ref sc);
                Assert.Equal(pdrop, Run(ref p, cpi => av1_postencode_drop_cbr(cpi, ref sp)));
                Assert.Equal(sc, sp);
                AssertSame(c, p, ctx + " postencode drop");
                if (pdrop != 0) continue;

                E7Shim.e7_postencode_update(ref c, (ulong)size);
                Run(ref p, cpi => av1_rc_postencode_update(cpi, (ulong)size));
                AssertSame(c, p, ctx + " postencode");

                // encoder.c bookkeeping between frames (identical on both sides).
                lastFrame = src;
                Advance(ref c);
                Advance(ref p);
            }
        }
    }

    private static void Advance(ref E7State st)
    {
        st.common_current_frame_frame_number++;
        st.rc_frames_since_key++;
        st.rc_frames_since_scene_change++;
        if (st.rc_frames_to_key > 0) st.rc_frames_to_key--;
        if (st.rc_frames_till_gf_update_due > 0) st.rc_frames_till_gf_update_due--;
        st.x_has_prev_frame = 1;
    }

    private static double LogUniform(Random r) => Math.Exp(r.NextDouble() * 3 - 1.5);
}
