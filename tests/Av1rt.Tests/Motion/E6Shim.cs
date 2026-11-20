using System.Runtime.InteropServices;

namespace Av1rt.Tests.Motion;

/// <summary>P/Invoke surface of tools/oracle/shim/shim_e6.c (libaom mcomp.c / encodemv.c).</summary>
internal static class E6Shim
{
    private const string Dll = "aomshim_e6";

    // ---- MV entropy context / costs ----
    [DllImport(Dll)] public static extern int e6_nmvc_size();
    [DllImport(Dll)] public static extern int e6_default_nmvc([Out] ushort[] output, int n);
    [DllImport(Dll)]
    public static extern void e6_build_nmv_cost_table(ushort[] nmvc, int precision, [Out] int[] mvjoint,
                                                      [Out] int[] mvcost0, [Out] int[] mvcost1);
    [DllImport(Dll)]
    public static extern int e6_mv_bit_cost(int mvRow, int mvCol, int refRow, int refCol, int[] mvjcost,
                                            int[] mvcost0, int[] mvcost1, int weight);
    [DllImport(Dll)]
    public static extern int e6_encode_mvs(short[] mvs, short[] refs, int count, int usehp, int allowUpdateCdf,
                                           [In, Out] ushort[] nmvc, [Out] byte[] output, int capacity,
                                           out int maxMvMagnitude);

    // ---- ref MV stack ----
    [DllImport(Dll)]
    public static extern void e6_find_best_ref_mvs(int allowHp, int refFrame, int isInteger, uint[] stackThis,
                                                   int count, uint globalMv, out uint nearest, out uint near);
    [DllImport(Dll)]
    public static extern uint e6_get_ref_mv_from_stack(int refIdx, int rf0, int rf1, int refMvIdx,
                                                       uint[] stackThis, uint[] stackComp, byte[] counts,
                                                       uint[] globals);

    // ---- search sites ----
    [DllImport(Dll)] public static extern int e6_search_site_ints();
    [DllImport(Dll)] public static extern void e6_init_search_site(int method, int stride, int level, [Out] int[] output);
    [DllImport(Dll)] public static extern int e6_init_search_range(int size);

    // ---- libaom C kernels (used by the C# vfp delegates) ----
    [DllImport(Dll)]
    public static extern uint e6_sdf(int bsize, byte[] a, int aOff, int aStride, byte[] b, int bOff, int bStride);
    [DllImport(Dll)]
    public static extern uint e6_sdsf(int bsize, byte[] a, int aOff, int aStride, byte[] b, int bOff, int bStride);
    [DllImport(Dll)]
    public static extern void e6_sdxnd(int bsize, int n, byte[] a, int aOff, int aStride, byte[] b, int[] bOffs,
                                       int bStride, [Out] uint[] sads);
    [DllImport(Dll)]
    public static extern void e6_sdsx4d(int bsize, byte[] a, int aOff, int aStride, byte[] b, int[] bOffs,
                                        int bStride, [Out] uint[] sads);
    [DllImport(Dll)]
    public static extern uint e6_vf(int bsize, byte[] a, int aOff, int aStride, byte[] b, int bOff, int bStride,
                                    out uint sse);
    [DllImport(Dll)]
    public static extern uint e6_svf(int bsize, byte[] a, int aOff, int aStride, int xoff, int yoff, byte[] b,
                                     int bOff, int bStride, out uint sse);

    // ---- searches ----
    [DllImport(Dll)]
    public static extern void e6_full_pixel_search(int[] inInts, byte[] src, int srcOff, int srcStride, byte[] refBuf,
                                                   int refOff, int refStride, int[] mvjcost, int[] mvcost0,
                                                   int[] mvcost1, [Out] int[] output);
    [DllImport(Dll)]
    public static extern void e6_subpel_pruned_more(int[] inInts, byte[] src, int srcOff, int srcStride,
                                                    byte[] refBuf, int refOff, int refStride, int[] mvjcost,
                                                    int[] mvcost0, int[] mvcost1, [Out] int[] output);
}
