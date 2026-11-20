using System.Runtime.InteropServices;

namespace Av1rt.Tests.E5;

// Imports of tools/oracle/shim/shim_e5.c (aomshim_e5.dll): libaom's C implementations of the
// work package e5 functions (dsp kernels, frame border extension, tiles, buffers).
internal static class E5Shim
{
    private const string Dll = "aomshim_e5";

    [DllImport(Dll)] public static extern uint aomshim_e5_sad(int w, int h, byte[] src, int src_stride, byte[] @ref, int ref_stride);
    [DllImport(Dll)] public static extern uint aomshim_e5_sad_avg(int w, int h, byte[] src, int src_stride, byte[] @ref, int ref_stride, byte[] second_pred);
    [DllImport(Dll)] public static extern uint aomshim_e5_sad_skip(int w, int h, byte[] src, int src_stride, byte[] @ref, int ref_stride);
    [DllImport(Dll)] public static extern int aomshim_e5_sad_multi(int kind, int w, int h, byte[] src, int src_stride, byte[] @ref, int[] offsets, int ref_stride, uint[] sad_array);

    [DllImport(Dll)] public static extern uint aomshim_e5_variance(int w, int h, byte[] a, int a_stride, byte[] b, int b_stride, out uint sse);
    [DllImport(Dll)] public static extern uint aomshim_e5_sub_pixel_variance(int w, int h, byte[] a, int a_stride, int xoffset, int yoffset, byte[] b, int b_stride, out uint sse);
    [DllImport(Dll)] public static extern uint aomshim_e5_sub_pixel_avg_variance(int w, int h, byte[] a, int a_stride, int xoffset, int yoffset, byte[] b, int b_stride, out uint sse, byte[] second_pred);
    [DllImport(Dll)] public static extern uint aomshim_e5_mse(int w, int h, byte[] a, int a_stride, byte[] b, int b_stride, out uint sse);

    [DllImport(Dll)] public static extern void aomshim_e5_comp_avg_pred(byte[] comp_pred, byte[] pred, int width, int height, byte[] @ref, int ref_stride);
    [DllImport(Dll)] public static extern void aomshim_e5_get_var_sse_sum_8x8_quad(byte[] a, int a_stride, byte[] b, int b_stride, uint[] sse8x8, int[] sum8x8, ref uint tot_sse, ref int tot_sum, uint[] var8x8);
    [DllImport(Dll)] public static extern void aomshim_e5_get_var_sse_sum_16x16_dual(byte[] a, int a_stride, byte[] b, int b_stride, uint[] sse16x16, ref uint tot_sse, ref int tot_sum, uint[] var16x16);

    [DllImport(Dll)] public static extern uint aomshim_e5_avg_4x4(byte[] s, int p);
    [DllImport(Dll)] public static extern uint aomshim_e5_avg_8x8(byte[] s, int p);
    [DllImport(Dll)] public static extern void aomshim_e5_avg_8x8_quad(byte[] s, int p, int x16_idx, int y16_idx, int[] avg);
    [DllImport(Dll)] public static extern void aomshim_e5_hadamard_lp(int kind, short[] src_diff, int src_stride, short[] coeff);
    [DllImport(Dll)] public static extern int aomshim_e5_satd_lp(short[] coeff, int length);
    [DllImport(Dll)] public static extern void aomshim_e5_int_pro_row(short[] hbuf, byte[] @ref, int ref_stride, int width, int height, int norm_factor);
    [DllImport(Dll)] public static extern void aomshim_e5_int_pro_col(short[] vbuf, byte[] @ref, int ref_stride, int width, int height, int norm_factor);
    [DllImport(Dll)] public static extern int aomshim_e5_vector_var(short[] @ref, short[] src, int bwl);
    [DllImport(Dll)] public static extern long aomshim_e5_sse(byte[] a, int a_stride, byte[] b, int b_stride, int width, int height);
    [DllImport(Dll)] public static extern void aomshim_e5_subtract_block(int rows, int cols, short[] diff, int diff_stride, byte[] src, int src_stride, byte[] pred, int pred_stride);

    [DllImport(Dll)] public static extern int aomshim_e5_extend(int width, int height, int ss_x, int ss_y, int border, int op, int a0, int a1, int a2, byte[] buf, int buf_size, int[] layout);
    [DllImport(Dll)] public static extern int aomshim_e5_copy(int op, int sw, int sh, int sborder, int dw, int dh, int dborder, int ss_x, int ss_y, int use_crop, byte[] sbuf, int sbuf_size, byte[] dbuf, int dbuf_size);

    [DllImport(Dll)] public static extern int aomshim_e5_tiles(int mi_rows, int mi_cols, int mib_size_log2, int uniform, int log2_cols, int log2_rows, int ncols, int nrows, int[] col_starts, int[] row_starts, int[] @params, int[] tiles, int tiles_cap);

    [DllImport(Dll)] public static extern int aomshim_e5_get_MBs(int width, int height);
    [DllImport(Dll)] public static extern long aomshim_e5_max_level_bitrate(int profile, int level, int tier);
    [DllImport(Dll)] public static extern int aomshim_e5_cdef_alloc(int steps, int[] cfg, int[] @out);
    [DllImport(Dll)] public static extern int aomshim_e5_lookahead(int width, int height, int depth, int nops, int[] ops, int[] results);
}
