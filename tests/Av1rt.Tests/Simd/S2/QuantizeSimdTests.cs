using System;
using System.Runtime.Intrinsics.X86;
using Av1rt.Aom;
using Xunit;
using static Av1rt.Aom.aom_codec;
using static Av1rt.Aom.av1_quantize;
using static Av1rt.Aom.av1_quantize_avx2;
using static Av1rt.Aom.avg;
using static Av1rt.Aom.blockd;
using static Av1rt.Aom.common_data;
using static Av1rt.Aom.hybrid_fwd_txfm;
using static Av1rt.Aom.idct;
using static Av1rt.Aom.nonrd_opt;
using static Av1rt.Aom.quantize;
using static Av1rt.Aom.quantize_avx2;
using static Av1rt.Aom.scan;
using static Av1rt.Aom.txfm_common;

namespace Av1rt.Tests.Simd.S2;

// Quantizer tables from the port's own av1_build_quantizer (8-bit), for a few realistic
// configurations.
internal sealed class S2QuantTables
{
    public readonly QUANTS Quants = new();
    public readonly Dequants Dequants = new();
    public readonly string Name;

    public S2QuantTables(string name, int yDc, int uDc, int uAc, int vDc, int vAc, int sharpness)
    {
        Name = name;
        av1_build_quantizer(AOM_BITS_8, yDc, uDc, uAc, vDc, vAc, Quants, Dequants, sharpness);
    }

    public static readonly S2QuantTables[] All =
    {
        new("default", 0, 0, 0, 0, 0, 0),
        new("sharp", 0, 0, 0, 0, 0, 5),
        new("deltaq", -8, 6, 4, -4, 10, 0),
    };

    public (short[] zbin, short[] round, short[] quant, short[] quantShift, short[] dequant,
            short[] roundFp, short[] quantFp) Plane(int plane, int q) => plane switch
    {
        0 => (Quants.y_zbin[q], Quants.y_round[q], Quants.y_quant[q], Quants.y_quant_shift[q],
              Dequants.y_dequant_QTX[q], Quants.y_round_fp[q], Quants.y_quant_fp[q]),
        1 => (Quants.u_zbin[q], Quants.u_round[q], Quants.u_quant[q], Quants.u_quant_shift[q],
              Dequants.u_dequant_QTX[q], Quants.u_round_fp[q], Quants.u_quant_fp[q]),
        _ => (Quants.v_zbin[q], Quants.v_round[q], Quants.v_quant[q], Quants.v_quant_shift[q],
              Dequants.v_dequant_QTX[q], Quants.v_round_fp[q], Quants.v_quant_fp[q]),
    };
}

// Differential tests of the AVX2 quantizer ports (aom_dsp/x86/quantize_avx2.c,
// av1/encoder/x86/av1_quantize_avx2.c) against the C ports, with quantizer tables from
// av1_build_quantizer over qindex 0..255 and 16-bit coefficients (the lowbd forward transform
// and the Hadamard produce int16; INT16_MIN is excluded like in libaom's tests, _mm_abs_epi16
// cannot represent its magnitude).
public class QuantizeSimdTests
{
    public static readonly TheoryData<int> TxSizes = FwdTxfmSimdTests.TxSizes;

    private static int RandomCoeff(Random rng, int kase, int zbin, int dq)
    {
        switch (kase)
        {
            case 0: return 0;
            case 1: return rng.Next(2) == 0 ? short.MaxValue : -short.MaxValue;
            case 2: return rng.Next(-2 * zbin, 2 * zbin + 1); // around the zero bin
            case 3: return rng.Next(-short.MaxValue, short.MaxValue + 1);
            default:
                int sel = rng.Next(10);
                if (sel < 4) return 0;
                if (sel < 8) return rng.Next(-3 * dq, 3 * dq + 1);
                return rng.Next(-short.MaxValue, short.MaxValue + 1);
        }
    }

    private const int RandomCases = 6; // kase 0..5 above
    private const int TxfmCases = 2;   // forward transforms of random residuals

    [Theory]
    [MemberData(nameof(TxSizes))]
    public void QuantizeB_Avx2MatchesC(int txSize)
    {
        if (!Avx2.IsSupported) return; // skipped: no AVX2 on this machine
        int n = av1_get_max_eob(txSize);
        int w = tx_size_wide[txSize], h = tx_size_high[txSize];
        int logScale = av1_get_tx_scale(txSize);
        var rng = new Random(0x52B0 + txSize);
        var txTypes = FwdTxfmSimdTests.SimdTxTypes(txSize);
        var residual = new short[w * h];
        int iter = 0;
        foreach (S2QuantTables t in S2QuantTables.All)
        {
            for (int q = 0; q < 256; q++)
            {
                int plane = (q + iter) % 3;
                var (zbin, round, quant, quantShift, dequant, _, _) = t.Plane(plane, q);
                int txType = txTypes[(q + iter) % txTypes.Count];
                SCAN_ORDER sc = av1_scan_orders[txSize][txType];
                for (int kase = 0; kase < RandomCases + TxfmCases; kase++, iter++)
                {
                    var coeff = new int[n];
                    if (kase < RandomCases)
                    {
                        for (int i = 0; i < n; i++) coeff[i] = RandomCoeff(rng, kase, zbin[1], dequant[1]);
                    }
                    else
                    {
                        int amp = kase == RandomCases ? 255 : 24;
                        for (int i = 0; i < residual.Length; i++) residual[i] = (short)rng.Next(-amp, amp + 1);
                        TxfmParam p = new()
                        {
                            tx_type = txType, tx_size = txSize, bd = 8, tx_set_type = EXT_TX_SET_ALL16,
                        };
                        av1_lowbd_fwd_txfm_c(residual, coeff, w, ref p);
                    }

                    var qC = new int[n];
                    var dqC = new int[n];
                    var eobC = new ushort[1];
                    var qS = new int[n];
                    var dqS = new int[n];
                    var eobS = new ushort[1];
                    Array.Fill(qC, 0x1234);
                    Array.Fill(qS, 0x1234);
                    Array.Fill(dqC, 0x4321);
                    Array.Fill(dqS, 0x4321);

                    if (logScale == 0)
                    {
                        aom_quantize_b_c(coeff, n, zbin, round, quant, quantShift, qC, dqC, dequant,
                                         eobC, sc.scan, sc.iscan);
                        aom_quantize_b_avx2(coeff, n, zbin, round, quant, quantShift, qS, dqS, dequant,
                                            eobS, sc.scan, sc.iscan);
                    }
                    else
                    {
                        Assert.Equal(1, logScale);
                        aom_quantize_b_32x32_c(coeff, n, zbin, round, quant, quantShift, qC, dqC,
                                               dequant, eobC, sc.scan, sc.iscan);
                        aom_quantize_b_32x32_avx2(coeff, n, zbin, round, quant, quantShift, qS, dqS,
                                                  dequant, eobS, sc.scan, sc.iscan);
                    }

                    string ctx = $"quantize_b tx_size={txSize} tx_type={txType} tables={t.Name} q={q} plane={plane} case={kase}";
                    SimdAssert.Same(eobC, eobS, ctx + " eob");
                    SimdAssert.Same(qC, qS, ctx + " qcoeff");
                    SimdAssert.Same(dqC, dqS, ctx + " dqcoeff");
                }
            }
        }
    }

    // av1_quantize_lp: the nonrd RD estimate quantizes 8x8 / 16x16 Hadamard coefficients with
    // the *_fp tables and the transposed scans; 4x4 with the default scan.
    [Theory]
    [InlineData(TX_4X4)]
    [InlineData(TX_8X8)]
    [InlineData(TX_16X16)]
    public void QuantizeLp_Avx2MatchesC(int txSize)
    {
        if (!Avx2.IsSupported) return; // skipped: no AVX2 on this machine
        int n = av1_get_max_eob(txSize);
        int w = tx_size_wide[txSize];
        var rng = new Random(0x52C0 + txSize);
        short[] scanTab, iscanTab;
        switch (txSize)
        {
            case TX_16X16:
                scanTab = default_scan_lp_16x16_transpose;
                iscanTab = av1_default_iscan_lp_16x16_transpose;
                break;
            case TX_8X8:
                scanTab = default_scan_8x8_transpose;
                iscanTab = av1_default_iscan_8x8_transpose;
                break;
            default:
                SCAN_ORDER sc = av1_scan_orders[txSize][DCT_DCT];
                scanTab = sc.scan.AsSpan(n).ToArray();
                iscanTab = sc.iscan.AsSpan(n).ToArray();
                break;
        }
        var residual = new short[w * w];
        int iter = 0;
        foreach (S2QuantTables t in S2QuantTables.All)
        {
            for (int q = 0; q < 256; q++)
            {
                int plane = (q + iter) % 3;
                var (_, _, _, _, dequant, roundFp, quantFp) = t.Plane(plane, q);
                for (int kase = 0; kase < RandomCases + TxfmCases; kase++, iter++)
                {
                    var coeff = new short[n];
                    if (kase < RandomCases)
                    {
                        for (int i = 0; i < n; i++) coeff[i] = (short)RandomCoeff(rng, kase, roundFp[1], dequant[1]);
                    }
                    else
                    {
                        int amp = kase == RandomCases ? 255 : 24;
                        for (int i = 0; i < residual.Length; i++) residual[i] = (short)rng.Next(-amp, amp + 1);
                        if (txSize == TX_16X16) aom_hadamard_lp_16x16_c(residual, w, coeff);
                        else if (txSize == TX_8X8) aom_hadamard_lp_8x8_c(residual, w, coeff);
                        else for (int i = 0; i < n; i++) coeff[i] = (short)(residual[i] * 8);
                    }

                    var qC = new short[n];
                    var dqC = new short[n];
                    var eobC = new ushort[1];
                    var qS = new short[n];
                    var dqS = new short[n];
                    var eobS = new ushort[1];
                    Array.Fill(qC, (short)0x1234);
                    Array.Fill(qS, (short)0x1234);
                    Array.Fill(dqC, (short)0x4321);
                    Array.Fill(dqS, (short)0x4321);

                    av1_quantize_lp_c(coeff, n, roundFp, quantFp, qC, dqC, dequant, eobC, scanTab, iscanTab);
                    av1_quantize_lp_avx2(coeff, n, roundFp, quantFp, qS, dqS, dequant, eobS, scanTab, iscanTab);

                    string ctx = $"quantize_lp tx_size={txSize} tables={t.Name} q={q} plane={plane} case={kase}";
                    SimdAssert.Same(eobC, eobS, ctx + " eob");
                    SimdAssert.Same(qC, qS, ctx + " qcoeff");
                    SimdAssert.Same(dqC, dqS, ctx + " dqcoeff");
                }
            }
        }
    }
}
