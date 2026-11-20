using System.Runtime.InteropServices;

namespace Av1rt.Tests.Prediction;

// Imports from tools/oracle/shim/shim_e3.c (aomshim_e3.dll).
internal static class E3Shim
{
    private const string Dll = "aomshim_e3";

    // Families: 0 dc, 1 dc_top, 2 dc_left, 3 dc_128, 4 v, 5 h, 6 smooth.
    [DllImport(Dll)]
    public static extern void aomshim_e3_intra_kernel(int family, int tx_size, ref byte dst, int stride,
                                                      ref byte above, ref byte left);

    [DllImport(Dll)]
    public static extern void aomshim_e3_predict_intra_block(
        int plane, int ss_x, int ss_y, int up_available, int left_available,
        int chroma_up_available, int chroma_left_available, int mb_to_right_edge,
        int mb_to_bottom_edge, int enable_intra_edge_filter, int wpx, int hpx,
        int tx_size, int mode, ref byte buf, int stride, int col_off, int row_off);

    // kind: 0 2d_sr, 1 x_sr, 2 y_sr, 3 facade, 4 copy.
    [DllImport(Dll)]
    public static extern void aomshim_e3_convolve(int kind, ref byte src, int src_stride, ref byte dst,
                                                  int dst_stride, int w, int h, int filter_x, int filter_y,
                                                  int subpel_x_qn, int subpel_y_qn);

    [DllImport(Dll)]
    public static extern void aomshim_e3_setup_scale_factors(int other_w, int other_h, int this_w, int this_h,
                                                             int[] out4);

    [DllImport(Dll)]
    public static extern int aomshim_e3_single_ref_ctx(byte[] counts, int which);

    [DllImport(Dll)]
    public static extern int aomshim_e3_neighbour_ctx(int[] cells, int up_available, int left_available,
                                                      int which);

    [DllImport(Dll)]
    public static extern void aomshim_e3_find_mv_refs(
        int mi_rows, int mi_cols, int grid_rows, int mi_stride, int[] cells,
        int mi_row, int mi_col, int partition, int is_last_vertical_rect,
        int is_first_horizontal_rect, int sb_size, int allow_hp,
        int force_integer_mv, int[] sign_bias, int ref_frame,
        int use_mv_ref_list, byte[] ref_mv_count, int[] stack, ushort[] weight,
        int[] mv_ref_list, int[] global_mvs, short[] mode_context);

    [DllImport(Dll)]
    public static extern void aomshim_e3_enc_build_inter(byte[] refbase, int[] rg, byte[] dstbase, int[] dg,
                                                         int ss_x, int ss_y, int mi_row, int mi_col,
                                                         int[] cells, int plane_from, int plane_to,
                                                         int is_chroma_ref, int y_nonrd);

    [DllImport(Dll)]
    public static extern void aomshim_e3_set_entropy_contexts(
        int plane, int ss_x, int ss_y, int mb_to_right_edge, int mb_to_bottom_edge,
        int plane_bsize, int tx_size, int has_eob, int aoff, int loff,
        sbyte[] above, sbyte[] left);
}
