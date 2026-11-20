using System.Runtime.InteropServices;

namespace Av1rt.Tests.E1;

/// <summary>Imports of tools/oracle/shim/shim_e1.c (libaom's C implementation).</summary>
internal static class E1Shim
{
    private const string Dll = "aomshim_e1";

    // FRAME_CONTEXT images are passed as byte[] of sizeof(FRAME_CONTEXT).
    [DllImport(Dll)] public static extern int aomshim_e1_frame_context_size();
    [DllImport(Dll)] public static extern void aomshim_e1_init_mode_probs(byte[] fc);
    [DllImport(Dll)] public static extern void aomshim_e1_default_coef_probs(int base_qindex, byte[] fc);
    [DllImport(Dll)] public static extern void aomshim_e1_init_mv_probs(byte[] fc);
    [DllImport(Dll)] public static extern void aomshim_e1_reset_cdf_symbol_counters(byte[] fc);

    [DllImport(Dll)]
    public static extern void aomshim_e1_setup_past_independence(
        int base_qindex, int mi_rows, int mi_cols, byte[] seg_map, short[] seg_feature_data,
        uint[] seg_feature_mask, byte[] fc, byte[] dfc, sbyte[] deltas, int[] lf_flags);

    [DllImport(Dll)] public static extern void aomshim_e1_scan_order(int tx_size, int tx_type, short[] scan, short[] iscan, int n);
    [DllImport(Dll)] public static extern void aomshim_e1_txb_padded_idx(int i, short[] output, int n);
    [DllImport(Dll)] public static extern void aomshim_e1_nz_map_ctx_offset(int tx_size, sbyte[] output, int n);
    [DllImport(Dll)] public static extern void aomshim_e1_eob_tables(short[] group_start, short[] offset_bits);
    [DllImport(Dll)] public static extern void aomshim_e1_prob_cost(ushort[] output);
    [DllImport(Dll)] public static extern void aomshim_e1_cost_lut_low(ushort[] output);

    [DllImport(Dll)] public static extern int aomshim_e1_dc_quant_QTX(int qindex, int delta, int bit_depth);
    [DllImport(Dll)] public static extern int aomshim_e1_ac_quant_QTX(int qindex, int delta, int bit_depth);
    [DllImport(Dll)] public static extern int aomshim_e1_get_qindex(int enabled, uint[] feature_mask, short[] feature_data, int segment_id, int base_qindex);
    [DllImport(Dll)] public static extern int aomshim_e1_use_qmatrix(int using_qmatrix, int[] lossless, int segment_id);

    [DllImport(Dll)] public static extern void aomshim_e1_calculate_segdata(uint[] feature_mask, out int segid_preskip, out int last_active_segid);
    [DllImport(Dll)] public static extern int aomshim_e1_seg_feature_data_max(int feature_id);
    [DllImport(Dll)] public static extern int aomshim_e1_is_segfeature_signed(int feature_id);

    [DllImport(Dll)] public static extern void aomshim_e1_cost_tokens_from_cdf(int[] costs, ushort[] cdf, int[] inv_map);
    [DllImport(Dll)] public static extern void aomshim_e1_cost_tokens_from_cdf_2_batch(int[] costs, int costs_stride, ushort[] cdf, int cdf_stride, int n);
    [DllImport(Dll)] public static extern void aomshim_e1_cost_tokens_from_cdf_4_batch(int[] costs, int costs_stride, ushort[] cdf, int cdf_stride, int n);

    [DllImport(Dll)] public static extern int aomshim_e1_uleb_size_in_bytes(ulong value);
    [DllImport(Dll)] public static extern int aomshim_e1_uleb_encode(ulong value, int available, byte[] coded_value, ref int coded_size);
    [DllImport(Dll)] public static extern int aomshim_e1_uleb_decode(byte[] buffer, int available, ref ulong value, ref int length);

    [DllImport(Dll)] public static extern int aomshim_e1_get_padded_idx(int idx, int bhl);
    [DllImport(Dll)] public static extern int aomshim_e1_get_nz_map_ctx_from_stats(int stats, int coeff_idx, int bhl, int tx_size, int tx_class);
    [DllImport(Dll)] public static extern int aomshim_e1_get_nz_mag(byte[] levels_buf, int offset, int bhl, int tx_class);
    [DllImport(Dll)] public static extern int aomshim_e1_get_br_ctx(byte[] levels_buf, int offset, int c, int bhl, int tx_class);
    [DllImport(Dll)] public static extern void aomshim_e1_get_txb_ctx(int plane_bsize, int tx_size, int plane, sbyte[] a, sbyte[] l, out int txb_skip_ctx, out int dc_sign_ctx);
    [DllImport(Dll)] public static extern int aomshim_e1_get_txb_entropy_context(int[] qcoeff, int tx_size, int tx_type, int eob);
}
