using System;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.av1_common_int;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.convolve;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.mv;
using static Av1rt.Aom.reconinter;
using static Av1rt.Aom.reconinter_enc;
using static Av1rt.Aom.scale;

namespace Av1rt.Tests.Prediction;

// Single-reference translational inter prediction end to end: av1_setup_dst_planes,
// av1_setup_pre_planes, av1_enc_build_inter_predictor (reconinter_template: 8x8-and-bigger and
// sub-8x8 chroma paths, av1_init_warp_params, av1_make_inter_predictor, inter_predictor,
// av1_convolve_2d_facade) and av1_enc_build_inter_predictor_y_nonrd.
public class InterPredTests
{
    private const int Border = 296;  // > AOM_BORDER_IN_PIXELS + filter taps
    private const int MaxW = 96, MaxH = 80;

    // Frame geometry (shim layout): y off, u off, v off, y w, y h, y stride, uv w, uv h, uv stride.
    private static int[] Geometry(int w, int h, int ssX, int ssY, out int total)
    {
        int uvw = (w + ssX) >> ssX, uvh = (h + ssY) >> ssY;
        int ys = w + 2 * Border + 7, uvs = uvw + 2 * Border + 3;
        int ySize = ys * (h + 2 * Border), uvSize = uvs * (uvh + 2 * Border);
        int yo = Border * ys + Border;
        int uo = ySize + Border * uvs + Border;
        int vo = ySize + uvSize + Border * uvs + Border;
        total = ySize + 2 * uvSize;
        return new[] { yo, uo, vo, w, h, ys, uvw, uvh, uvs };
    }

    private static YV12_BUFFER_CONFIG ToYv12(byte[] data, int[] g)
    {
        var b = new YV12_BUFFER_CONFIG();
        b.y_buffer = new Ptr<byte>(data, g[0]);
        b.u_buffer = new Ptr<byte>(data, g[1]);
        b.v_buffer = new Ptr<byte>(data, g[2]);
        b.y_crop_width = g[3];
        b.y_crop_height = g[4];
        b.y_stride = g[5];
        b.uv_crop_width = g[6];
        b.uv_crop_height = g[7];
        b.uv_stride = g[8];
        return b;
    }

    private static bool StubbedSsSizeLookup
    {
        get
        {
            try { _ = get_plane_block_size(BLOCK_8X8, 1, 1); return false; }
            catch (NotImplementedException) { return true; }
        }
    }

    private static int RandomMv(Random rng) =>
        rng.Next(8) switch
        {
            0 => rng.Next(-4000, 4001),  // far outside the frame: exercises the position clamp
            1 => rng.Next(-8, 9) * 8,    // full-pel
            _ => rng.Next(-300, 301),
        };

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(0, 0, true)]
    [InlineData(1, 1, true)]
    public void EncBuildInterPredictorMatchesLibaom(int ssX, int ssY, bool yNonrd)
    {
        var rng = new Random(600 + ssX * 4 + ssY * 2 + (yNonrd ? 1 : 0));
        var cells = new int[4 * 8];
        int stubSkips = 0;
        for (int trial = 0; trial < 400; trial++)
        {
            int w = rng.Next(8, MaxW + 1), h = rng.Next(8, MaxH + 1);
            int[] g = Geometry(w, h, ssX, ssY, out int total);
            var refData = new byte[total];
            rng.NextBytes(refData);
            if (rng.Next(6) == 0) Array.Fill(refData, (byte)255);
            var dstC = new byte[total];
            rng.NextBytes(dstC);
            var dstS = (byte[])dstC.Clone();

            int bsize;
            do bsize = rng.Next(BLOCK_SIZES_ALL);
            while (ContextTests.PlaneBsize(bsize, ssX, ssY) == BLOCK_INVALID);
            int miRow = rng.Next((h + 3) / 4), miCol = rng.Next((w + 3) / 4);

            for (int i = 0; i < 4; i++)
            {
                int o = i * 8;
                cells[o + 0] = i == 3 ? bsize : rng.Next(BLOCK_SIZES_ALL);
                cells[o + 1] = rng.Next(4) == 0 ? GLOBALMV : NEWMV;
                cells[o + 2] = rng.Next(5) == 0 && i != 3 ? INTRA_FRAME : rng.Next(LAST_FRAME, ALTREF_FRAME + 1);
                cells[o + 3] = NONE_FRAME;
                cells[o + 4] = (ushort)RandomMv(rng) | (RandomMv(rng) << 16);
                cells[o + 5] = 0;
                cells[o + 6] = 0;
                // x and y filters (dual filter), including BILINEAR.
                cells[o + 7] = rng.Next(4) | (rng.Next(4) << 16);
            }
            int planeFrom = rng.Next(3), planeTo = rng.Next(planeFrom, 3);
            if (rng.Next(2) == 0) { planeFrom = 0; planeTo = 2; }

            E3Shim.aomshim_e3_enc_build_inter(refData, g, dstC, g, ssX, ssY, miRow, miCol, cells, planeFrom,
                                              planeTo, 1, yNonrd ? 1 : 0);

            // C#: mirror the shim set-up.
            YV12_BUFFER_CONFIG refb = ToYv12(refData, g), dstb = ToYv12(dstS, g);
            var sf = new scale_factors();
            av1_setup_scale_factors_for_frame(sf, w, h, w, h);
            var grid = new MB_MODE_INFO[4];
            for (int i = 0; i < 4; i++) grid[i] = ContextTests.ToMbmi(cells, i);
            var gm = new WarpedMotionParams[REF_FRAMES];
            for (int i = 0; i < REF_FRAMES; i++) gm[i] = default_warp_params;

            var xd = new MACROBLOCKD();
            xd.mi = new Ptr<MB_MODE_INFO>(grid, 3);
            xd.mi_stride = 2;
            xd.mi_row = miRow;
            xd.mi_col = miCol;
            xd.is_chroma_ref = true;
            xd.bd = 8;
            xd.global_motion = gm;
            xd.block_ref_scale_factors[0] = sf;
            xd.tmp_conv_dst = new ushort[MAX_SB_SIZE * MAX_SB_SIZE];
            for (int p = 0; p < MAX_MB_PLANE; p++)
            {
                macroblockd_plane pd = xd.plane[p];
                pd.subsampling_x = p != 0 ? ssX : 0;
                pd.subsampling_y = p != 0 ? ssY : 0;
                int pw = (mi_size_wide[bsize] * MI_SIZE) >> pd.subsampling_x;
                int ph = (mi_size_high[bsize] * MI_SIZE) >> pd.subsampling_y;
                pd.width = (byte)Math.Max(pw, 4);
                pd.height = (byte)Math.Max(ph, 4);
            }
            av1_setup_dst_planes(xd.plane, bsize, dstb, miRow, miCol, 0, MAX_MB_PLANE);
            av1_setup_pre_planes(xd, 0, refb, miRow, miCol, sf, MAX_MB_PLANE);

            var cm = new AV1_COMMON();
            var rcb = new RefCntBuffer { buf = refb };
            for (int i = 0; i < REF_FRAMES; i++)
            {
                cm.remapped_ref_idx[i] = i;
                cm.ref_frame_map[i] = rcb;
                av1_setup_scale_factors_for_frame(cm.ref_scale_factors[i], w, h, w, h);
            }

            if (yNonrd)
            {
                InterPredParams ipp = default;
                macroblockd_plane pd0 = xd.plane[0];
                av1_init_inter_params(ref ipp, pd0.width, pd0.height, miRow * MI_SIZE, miCol * MI_SIZE, 0, 0, 8, 0,
                                      0, sf, in pd0.pre[0], grid[3].interp_filters);
                ipp.conv_params = get_conv_params_no_round(0, 0, Ptr<ushort>.Null, 0, 0, 8);
                SubpelParams sp = default;
                MV mv = grid[3].mv[0].as_mv;
                init_subpel_params(in mv, ref ipp, ref sp, pd0.pre[0].width, pd0.pre[0].height);
                av1_enc_build_inter_predictor_y_nonrd(xd, ref ipp, in sp);
            }
            else
            {
                // The sub-8x8 chroma path (build_inter_predictors_sub8x8) calls get_plane_block_size,
                // which needs av1_ss_size_lookup from common_data.c (another package, still a stub in
                // this tree). Those trials are skipped until it is merged; any other stub hit fails.
                bool sub4X = block_size_wide[bsize] == 4 && ssX != 0;
                bool sub4Y = block_size_high[bsize] == 4 && ssY != 0;
                bool sub8x8 = planeTo > 0 && (sub4X || sub4Y);
                for (int r = sub4Y ? -1 : 0; r <= 0; r++)
                    for (int c = sub4X ? -1 : 0; c <= 0; c++)
                        sub8x8 &= grid[3 + r * 2 + c].ref_frame[0] > INTRA_FRAME;
                try
                {
                    av1_enc_build_inter_predictor(cm, xd, miRow, miCol, Ptr<BUFFER_SET>.Null, bsize, planeFrom,
                                                  planeTo);
                }
                catch (NotImplementedException) when (sub8x8 && StubbedSsSizeLookup)
                {
                    stubSkips++;
                    continue;
                }
            }

            if (!dstC.AsSpan().SequenceEqual(dstS))
            {
                int first = dstC.AsSpan().CommonPrefixLength(dstS);
                Assert.Fail($"trial {trial}: bsize {bsize} pos {miRow},{miCol} frame {w}x{h} planes " +
                            $"{planeFrom}..{planeTo}: first difference at byte {first}");
            }
        }
        Assert.True(stubSkips < 100, $"{stubSkips} sub-8x8 trials skipped");
    }
}
