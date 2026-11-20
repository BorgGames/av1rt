using System;
using Av1rt.Aom;
using Xunit;
using Xunit.Abstractions;
using static Av1rt.Aom.aomcx;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.enc_enums;
using static Av1rt.Aom.enums;
using static Av1rt.Aom.quant_common;
using static Av1rt.Aom.ratectrl;

namespace Av1rt.Tests.E8;

// Differential tests of the leaf functions of rd.c and rdopt.c against libaom.
public class RdTests
{
    private readonly ITestOutputHelper _output;

    public RdTests(ITestOutputHelper output) => _output = output;

    private const int AOM_BITS_8 = 8;

    [Fact]
    public void BlockErrorLpMatchesLibaom()
    {
        var rng = new Random(0xE8);
        int[] sizes = { 0, 1, 15, 16, 64, 256, 1024, 4096 };
        foreach (int n in sizes)
        {
            for (int iter = 0; iter < 20; ++iter)
            {
                var coeff = new short[Math.Max(n, 1)];
                var dqcoeff = new short[Math.Max(n, 1)];
                for (int i = 0; i < n; ++i)
                {
                    switch (iter % 4)
                    {
                        case 0: // small residuals
                            coeff[i] = (short)rng.Next(-256, 257);
                            dqcoeff[i] = (short)(coeff[i] + rng.Next(-16, 17));
                            break;
                        case 1: // full range
                            coeff[i] = (short)rng.Next(short.MinValue, short.MaxValue + 1);
                            dqcoeff[i] = (short)rng.Next(short.MinValue, short.MaxValue + 1);
                            break;
                        case 2: // extremes (diff * diff overflows int in C as well)
                            coeff[i] = rng.Next(2) == 0 ? short.MinValue : short.MaxValue;
                            dqcoeff[i] = rng.Next(2) == 0 ? short.MinValue : short.MaxValue;
                            break;
                        default:
                            coeff[i] = (short)rng.Next(-4096, 4097);
                            dqcoeff[i] = 0;
                            break;
                    }
                }
                long expected = E8Shim.aomshim_e8_block_error_lp(coeff, dqcoeff, n);
                Assert.Equal(expected, rdopt.av1_block_error_lp_c(coeff, dqcoeff, n));
                Assert.Equal(expected, rdopt.av1_block_error_lp(coeff, dqcoeff, n));
            }
        }
        // Non-zero offsets into the arrays.
        var a = new short[300];
        var b = new short[300];
        for (int i = 0; i < a.Length; ++i)
        {
            a[i] = (short)rng.Next(-2000, 2000);
            b[i] = (short)rng.Next(-2000, 2000);
        }
        long full = E8Shim.aomshim_e8_block_error_lp(a[44..], b[44..], 256);
        Assert.Equal(full, rdopt.av1_block_error_lp_c(new Ptr<short>(a, 44), new Ptr<short>(b, 44), 256));
    }

    [Fact]
    public void ModelRdFromVarLapndzMatchesLibaom()
    {
        var rng = new Random(0x1A9);
        void Check(long var, uint nLog2, uint qstep)
        {
            E8Shim.aomshim_e8_model_rd_from_var_lapndz(var, nLog2, qstep, out int er, out long ed);
            int rate = -7;
            long dist = -7;
            rd.av1_model_rd_from_var_lapndz(var, nLog2, qstep, ref rate, ref dist);
            Assert.True(er == rate && ed == dist, $"var={var} n_log2={nLog2} qstep={qstep}: C ({er},{ed}) C# ({rate},{dist})");
        }
        for (uint nLog2 = 4; nLog2 <= 12; ++nLog2)
        {
            foreach (uint qstep in new uint[] { 1, 4, 8, 17, 64, 255, 1024, 1828, 5247 })
            {
                foreach (long var in new long[] { 0, 1, 2, 3, 100, 4096, 65535, 1L << 20, 1L << 31, (1L << 40) + 12345 })
                    Check(var, nLog2, qstep);
                for (int i = 0; i < 200; ++i)
                {
                    long var = rng.NextInt64(0, 1L << rng.Next(1, 44));
                    Check(var, nLog2, qstep);
                }
            }
        }
    }

    [Fact]
    public void InterModeDataInitResetsModels()
    {
        var tile = new TileDataEnc();
        for (int i = 0; i < tile.inter_mode_rd_models.Length; ++i)
        {
            ref InterModeRdModel m = ref tile.inter_mode_rd_models[i];
            m.ready = 1; m.num = 7; m.dist_sum = 1; m.ld_sum = 2; m.sse_sum = 3; m.sse_sse_sum = 4; m.sse_ld_sum = 5;
            m.a = 9; m.b = 10; m.dist_mean = 11;
        }
        rdopt.av1_inter_mode_data_init(tile);
        Assert.Equal(BLOCK_SIZES_ALL, tile.inter_mode_rd_models.Length);
        foreach (InterModeRdModel m in tile.inter_mode_rd_models)
        {
            Assert.Equal(0, m.ready);
            Assert.Equal(0, m.num);
            Assert.Equal(0.0, m.dist_sum);
            Assert.Equal(0.0, m.ld_sum);
            Assert.Equal(0.0, m.sse_sum);
            Assert.Equal(0.0, m.sse_sse_sum);
            Assert.Equal(0.0, m.sse_ld_sum);
            // Fields C does not touch keep their values.
            Assert.Equal(9.0, m.a);
            Assert.Equal(10.0, m.b);
            Assert.Equal(11.0, m.dist_mean);
        }
    }

    // C: (scaled_buf != ref_buf && scaled_buf != NULL) ? &scaled_buf->buf : NULL.
    [Fact]
    public void GetScaledRefFrameFollowsC()
    {
        var cpi = new AV1_COMP();
        AV1_COMMON cm = cpi.common;
        var refBuf = new RefCntBuffer();
        var scaledBuf = new RefCntBuffer();
        for (int rf = LAST_FRAME; rf <= ALTREF_FRAME; ++rf)
        {
            cm.remapped_ref_idx[rf - LAST_FRAME] = rf - LAST_FRAME;
            cm.ref_frame_map[rf - LAST_FRAME] = refBuf;
        }
        Assert.Null(rd.av1_get_scaled_ref_frame(cpi, GOLDEN_FRAME)); // scaled_buf == NULL
        cpi.scaled_ref_buf[GOLDEN_FRAME - 1] = refBuf;
        Assert.Null(rd.av1_get_scaled_ref_frame(cpi, GOLDEN_FRAME)); // scaled_buf == ref_buf
        cpi.scaled_ref_buf[GOLDEN_FRAME - 1] = scaledBuf;
        Assert.Same(scaledBuf.buf, rd.av1_get_scaled_ref_frame(cpi, GOLDEN_FRAME));
        Assert.Null(rd.av1_get_scaled_ref_frame(cpi, LAST_FRAME));
        cm.ref_frame_map[ALTREF_FRAME - LAST_FRAME] = null;
        cpi.scaled_ref_buf[ALTREF_FRAME - 1] = scaledBuf;
        Assert.Same(scaledBuf.buf, rd.av1_get_scaled_ref_frame(cpi, ALTREF_FRAME));
    }

    [Fact]
    public void ComputeRdMultMatchesLibaom() => Deferred.Run(_output, () =>
    {
        int[] tunings = { AOM_TUNE_PSNR, AOM_TUNE_SSIM, AOM_TUNE_IQ, AOM_TUNE_SSIMULACRA2 };
        int[] modes = { GOOD, REALTIME, ALLINTRA };
        for (int qindex = 0; qindex < QINDEX_RANGE; ++qindex)
        {
            for (int update_type = 0; update_type < FRAME_UPDATE_TYPES; ++update_type)
            {
                foreach (int tuning in tunings)
                    foreach (int mode in modes)
                        Assert.Equal(
                            E8Shim.aomshim_e8_compute_rd_mult_based_on_qindex(AOM_BITS_8, update_type, qindex, tuning, mode),
                            rd.av1_compute_rd_mult_based_on_qindex(AOM_BITS_8, update_type, qindex, tuning, mode));

                for (int layer_depth = 0; layer_depth <= 6; layer_depth += 3)
                    foreach (int boost_index in new[] { 0, 4, 15 })
                        for (int frame_type = 0; frame_type < FRAME_TYPES; ++frame_type)
                            for (int fixedQp = 0; fixedQp <= 1; ++fixedQp)
                                for (int stat = 0; stat <= 1; ++stat)
                                    foreach (int tuning in new[] { AOM_TUNE_PSNR, AOM_TUNE_IQ })
                                        Assert.Equal(
                                            E8Shim.aomshim_e8_compute_rd_mult(qindex, AOM_BITS_8, update_type, layer_depth,
                                                                              boost_index, frame_type, fixedQp, stat, tuning, REALTIME),
                                            rd.av1_compute_rd_mult(qindex, AOM_BITS_8, update_type, layer_depth,
                                                                   boost_index, frame_type, fixedQp, stat, tuning, REALTIME));
            }
        }
    });

    [Fact]
    public void IntraCostPenaltyMatchesLibaom() => Deferred.Run(_output, () =>
    {
        for (int qindex = 0; qindex < QINDEX_RANGE; ++qindex)
            foreach (int qdelta in new[] { -64, -15, 0, 7, 63 })
                Assert.Equal(E8Shim.aomshim_e8_intra_cost_penalty(qindex, qdelta, AOM_BITS_8),
                             rd.av1_get_intra_cost_penalty(qindex, qdelta, AOM_BITS_8));
    });

    [Fact]
    public void SadPerBitLutMatchesLibaom() => Deferred.Run(_output, () =>
    {
        var expected = new int[QINDEX_RANGE];
        E8Shim.aomshim_e8_sad_per_bit_lut8(expected);
        rd.av1_init_me_luts();
        var cpi = new AV1_COMP();
        cpi.common.seq_params = new SequenceHeader { bit_depth = AOM_BITS_8 };
        for (int q = 0; q < QINDEX_RANGE; ++q)
        {
            int sadperbit = -1;
            rd.av1_set_sad_per_bit(cpi, ref sadperbit, q);
            Assert.True(expected[q] == sadperbit, $"qindex {q}: C {expected[q]} C# {sadperbit}");
        }
    });
}
