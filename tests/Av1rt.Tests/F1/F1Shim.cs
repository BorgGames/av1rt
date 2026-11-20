using System.Runtime.InteropServices;

namespace Av1rt.Tests.F1;

/// <summary>Imports of tools/oracle/shim/shim_f1.c (libaom's C implementation).</summary>
internal static class F1Shim
{
    private const string Dll = "aomshim_f1";

    [DllImport(Dll)] public static extern int f1_seq_count();
    [DllImport(Dll)] public static extern int f1_fh_count();

    [DllImport(Dll)]
    public static extern uint f1_write_obu_header(int keep_level_stats, int obu_type,
                                                  int has_nonzero_operating_point_idc,
                                                  int is_layer_specific_obu, int obu_extension,
                                                  byte[] dst, int[] output);

    [DllImport(Dll)] public static extern int f1_write_uleb_obu_size(ulong obu_payload_size, byte[] dest, int dest_size);
    [DllImport(Dll)] public static extern uint f1_write_sequence_header_obu(int[] seq, byte[] dst, int dst_size);
    [DllImport(Dll)] public static extern int f1_neg_interleave(int x, int @ref, int max);

    [DllImport(Dll)] public static extern void f1_write_obu_tg_tile_headers(int[] seq, int[] fh, byte[] dst, int[] output);

    [DllImport(Dll)]
    public static extern int f1_write_coeffs_txb(byte[] fc, int base_qindex, int reduced_tx_set,
                                                 int allow_update_cdf, int count, int[] parameters,
                                                 int[] coeffs, byte[] output, int cap);

    [DllImport(Dll)] public static extern void f1_txb_init_levels(int[] coeff, int width, int height, byte[] levels_buf);

    [DllImport(Dll)]
    public static extern void f1_get_nz_map_contexts(byte[] levels_buf, int tx_size, int tx_type, int eob,
                                                     sbyte[] coeff_contexts);

    [DllImport(Dll)] public static extern int f1_get_eob_pos_token(int eob, out int extra);
}
