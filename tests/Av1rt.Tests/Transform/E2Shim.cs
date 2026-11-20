using System.Runtime.InteropServices;

namespace Av1rt.Tests.Transform;

// Wrappers exported by tools/oracle/shim/shim_e2.c (pure-C libaom).
internal static class E2Shim
{
    private const string Dll = "aomshim_e2";

    [DllImport(Dll)]
    public static extern void e2_fwd_txfm(short[] src_diff, int[] coeff, int diff_stride, int tx_size, int tx_type);

    [DllImport(Dll)]
    public static extern void e2_inv_txfm_add(int[] dqcoeff, byte[] dst, int stride, int tx_size, int tx_type, int eob);

    [DllImport(Dll)]
    public static extern int e2_get_scan(int tx_size, int tx_type, short[] scan, short[] iscan);

    [DllImport(Dll)]
    public static extern void e2_build_quantizer(int bit_depth, int y_dc_delta_q, int u_dc_delta_q, int u_ac_delta_q,
                                                 int v_dc_delta_q, int v_ac_delta_q, int sharpness,
                                                 short[] quants_out, short[] dequants_out);

    [DllImport(Dll)]
    public static extern int e2_quants_size();

    [DllImport(Dll)]
    public static extern int e2_dequants_size();

    [DllImport(Dll)]
    public static extern void e2_quantize_b_facade(int[] coeff, int n_coeffs, short[] zbin, short[] round, short[] quant,
                                                   short[] quant_shift, short[] dequant, int[] qcoeff, int[] dqcoeff,
                                                   ushort[] eob, int tx_size, int tx_type);

    [DllImport(Dll)]
    public static extern void e2_quantize_lp(short[] coeff, int n_coeffs, short[] round, short[] quant, short[] qcoeff,
                                             short[] dqcoeff, short[] dequant, ushort[] eob, short[] scan, short[] iscan);

    [DllImport(Dll)]
    public static extern int e2_quantizer_to_qindex(int quantizer);

    [DllImport(Dll)]
    public static extern int e2_qindex_to_quantizer(int qindex);
}
