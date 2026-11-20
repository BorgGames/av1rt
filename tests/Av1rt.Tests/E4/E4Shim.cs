using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Av1rt.Aom;
using Xunit;

#nullable enable

namespace Av1rt.Tests.E4;

/// <summary>Imports of tools/oracle/shim/shim_e4.c (libaom's C implementation).</summary>
internal static class E4Shim
{
    private const string Dll = "aomshim_e4";

    // kind = dir * 12 + size * 3 + variant (dir 0 horizontal / 1 vertical; size 0..3 = 4/6/8/14;
    // variant 0 single / 1 dual / 2 quad). s is passed as (buffer, offset).
    [DllImport(Dll, EntryPoint = "aomshim_e4_lpf")]
    private static extern void lpf(int kind, ref byte s, int pitch, byte[] b0, byte[] l0, byte[] t0,
                                          byte[] b1, byte[] l1, byte[] t1);

    public static void Lpf(int kind, byte[] buf, int offset, int pitch, byte[] b0, byte[] l0,
                                  byte[] t0, byte[] b1, byte[] l1, byte[] t1)
    {
        lpf(kind, ref buf[offset], pitch, b0, l0, t0, b1, l1, t1);
    }

    [DllImport(Dll)] public static extern void aomshim_e4_lf_init(int sharpness, byte[] output);

    [DllImport(Dll)]
    public static extern void aomshim_e4_lf_frame_init(int[] levels, int mode_ref_delta_enabled,
                                                       sbyte[] ref_deltas, sbyte[] mode_deltas,
                                                       int seg_enabled, short[] seg_data,
                                                       uint[] seg_mask, int plane_start,
                                                       int plane_end, byte[] lvl, byte[] thr);

    [DllImport(Dll)]
    public static extern void aomshim_e4_lf_frame(int mi_rows, int mi_cols, int sb128, int[] mi_block,
                                                  int nblocks, byte[] bsize, byte[] tx_size,
                                                  byte[] skip_txfm, sbyte[] ref_frame0, byte[] mode,
                                                  byte[] segment_id, int[] levels, sbyte[] ref_deltas,
                                                  sbyte[] mode_deltas, byte[] y, int ys, byte[] u,
                                                  byte[] v, int uvs, int plane_start, int plane_end);

    [DllImport(Dll, EntryPoint = "aomshim_e4_cdef_find_dir")]
    private static extern int cdef_find_dir(ref ushort img, int stride, out int var, int coeff_shift);

    public static int CdefFindDir(ushort[] buf, int offset, int stride, out int var, int coeff_shift)
    {
        return cdef_find_dir(ref buf[offset], stride, out var, coeff_shift);
    }

    [DllImport(Dll, EntryPoint = "aomshim_e4_cdef_find_dir_dual")]
    private static extern void cdef_find_dir_dual(ref ushort img1, ref ushort img2, int stride,
                                                         out int var1, out int var2, int coeff_shift,
                                                         out int out1, out int out2);

    public static void CdefFindDirDual(ushort[] buf, int offset1, int offset2, int stride,
                                              out int var1, out int var2, int coeff_shift,
                                              out int out1, out int out2)
    {
        cdef_find_dir_dual(ref buf[offset1], ref buf[offset2], stride, out var1, out var2, coeff_shift,
                               out out1, out out2);
    }

    [DllImport(Dll, EntryPoint = "aomshim_e4_cdef_filter_8")]
    private static extern void cdef_filter_8(int variant, byte[] dst, int dstride, ref ushort @in,
                                                    int pri_strength, int sec_strength, int dir,
                                                    int pri_damping, int sec_damping, int coeff_shift,
                                                    int block_width, int block_height);

    public static void CdefFilter8(int variant, byte[] dst, int dstride, ushort[] inbuf, int inOffset,
                                          int pri_strength, int sec_strength, int dir, int pri_damping,
                                          int sec_damping, int coeff_shift, int bw, int bh)
    {
        cdef_filter_8(variant, dst, dstride, ref inbuf[inOffset], pri_strength, sec_strength, dir,
                          pri_damping, sec_damping, coeff_shift, bw, bh);
    }

    [DllImport(Dll)]
    public static extern void aomshim_e4_cdef_copy_rect8(ushort[] dst, int dstride, byte[] src, int sstride,
                                                         int width, int height);

    [DllImport(Dll, EntryPoint = "aomshim_e4_cdef_filter_fb")]
    private static extern void cdef_filter_fb(byte[] dst8, int dstride, ref ushort @in, int xdec, int ydec,
                                                     int[] dir, int[]? dirinit, int[] var, int pli,
                                                     byte[] dlist, int cdef_count, int level,
                                                     int sec_strength, int damping, int coeff_shift);

    public static void CdefFilterFb(byte[] dst8, int dstride, ushort[] inbuf, int inOffset, int xdec,
                                           int ydec, int[] dir, int[]? dirinit, int[] var, int pli,
                                           byte[] dlist, int cdef_count, int level, int sec_strength,
                                           int damping, int coeff_shift)
    {
        cdef_filter_fb(dst8, dstride, ref inbuf[inOffset], xdec, ydec, dir, dirinit, var, pli, dlist,
                           cdef_count, level, sec_strength, damping, coeff_shift);
    }

    [DllImport(Dll)]
    public static extern int aomshim_e4_cdef_compute_sb_list(int mi_rows, int mi_cols, int mi_stride,
                                                             byte[] skip, int mi_row, int mi_col, int bs,
                                                             byte[] dlist_out);

    [DllImport(Dll)]
    public static extern void aomshim_e4_cdef_frame(int mi_rows, int mi_cols, byte[] skip, sbyte[] strength,
                                                    int[] info, int[] strengths, int[] uv_strengths,
                                                    byte[] y, int ys, byte[] u, byte[] v, int uvs);

    [DllImport(Dll, EntryPoint = "aomshim_e4_cdef_search")]
    private static extern void cdef_search(int mi_rows, int mi_cols, int sb128, byte[] skip, int qindex,
                                           int pick_method, int rdmult, ref byte src_y, ref byte src_u,
                                           ref byte src_v, ref byte rec_y, ref byte rec_u, ref byte rec_v, int ys,
                                           int uvs, int[] output, sbyte[] strength_out);

    // Planes are (buffer, origin offset) so that the frames can have borders.
    public static void CdefSearch(int mi_rows, int mi_cols, int sb128, byte[] skip, int qindex, int pick_method,
                                  int rdmult, byte[][] src, byte[][] rec, int[] origin, int ys, int uvs,
                                  int[] output, sbyte[] strength_out) =>
        cdef_search(mi_rows, mi_cols, sb128, skip, qindex, pick_method, rdmult, ref src[0][origin[0]],
                    ref src[1][origin[1]], ref src[2][origin[2]], ref rec[0][origin[0]], ref rec[1][origin[1]],
                    ref rec[2][origin[2]], ys, uvs, output, strength_out);

    [DllImport(Dll)]
    public static extern void aomshim_e4_pick_cdef_from_qp(int qindex, int frame_type, int skip_cdef,
                                                           int avoid_uv_cdef, int[] output);

    [DllImport(Dll)] public static extern void aomshim_e4_pick_filter_level(int[] p, int[] output);

    [DllImport(Dll)] public static extern int aomshim_e4_ac_quant_QTX(int qindex, int delta, int bit_depth);
}

/// <summary>
/// Detects functions of other work packages that are still <c>NotImplementedException</c> stubs
/// in this branch, so tests that need them are skipped until the real port is merged. The
/// function is looked up by name (stubs and real ports live in different classes).
/// </summary>
internal static class StubProbe
{
    private const BindingFlags Statics =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    // True if the method (called with args) or the table (a stub property) named `name` throws
    // NotImplementedException; false once a real port provides it.
    private static bool IsStub(string name, params object?[] args)
    {
        foreach (Type t in typeof(Av1rt.Aom.av1_loopfilter).Assembly.GetTypes())
        {
            if (t.GetField(name, Statics) != null) return false;
            MethodInfo? m = t.GetProperty(name, Statics)?.GetMethod;
            if (m == null)
            {
                m = Array.Find(t.GetMethods(Statics), x => x.Name == name && x.GetParameters().Length == args.Length);
                if (m == null) continue;
            }
            else
            {
                args = Array.Empty<object>();
            }
            try { m.Invoke(null, args); }
            catch (TargetInvocationException e) when (e.InnerException is NotImplementedException) { return true; }
            catch (Exception) { return false; }
            return false;
        }
        return true;
    }

    /// <summary>Calls the static function `name` of another package (stub or real port).</summary>
    public static object? Call(string name, params object?[] args)
    {
        foreach (Type t in typeof(Av1rt.Aom.av1_loopfilter).Assembly.GetTypes())
        {
            MethodInfo? m = Array.Find(t.GetMethods(Statics), x => x.Name == name && x.GetParameters().Length == args.Length);
            if (m != null) return m.Invoke(null, args);
        }
        throw new MissingMethodException(name);
    }

    public static bool Check(string dependency) => dependency switch
    {
        // av1/common/quant_common.c
        "av1_ac_quant_QTX" => IsStub("av1_ac_quant_QTX", 0, 0, 8),
        // av1/common/reconinter.c
        "av1_setup_dst_planes" => IsStub("av1_setup_dst_planes", null, 0, null, 0, 0, 0, 0),
        // aom_dsp/sse.c
        "aom_sse" => IsStub("aom_sse", null, 0, null, 0, 0, 0),
        // aom_util/aom_thread.c
        "aom_get_worker_interface" => IsStub("aom_get_worker_interface"),
        // av1/common/common_data.c (read by get_plane_block_size for the chroma planes)
        "av1_ss_size_lookup" => IsStub("av1_ss_size_lookup"),
        _ => throw new ArgumentException(dependency),
    };
}

/// <summary>A [Fact] that is skipped while any of the named dependencies is still a stub.</summary>
public sealed class FactUnlessStubAttribute : FactAttribute
{
    public FactUnlessStubAttribute(params string[] dependencies)
    {
        string[] stubs = Array.FindAll(dependencies, StubProbe.Check);
        if (stubs.Length > 0)
            Skip = $"{string.Join(", ", stubs)}: still a stub in this branch (ported by another package)";
    }
}
