using System.Runtime.InteropServices;

namespace Av1rt.Tests.F3;

// libaom wrappers from tools/oracle/shim/shim_f3.c.
internal static class F3Shim
{
    private const string Dll = "aomshim_f3";

    [DllImport(Dll)]
    public static extern void aomshim_f3_vbp_thresholds(int[] p, long[] output);

    [DllImport(Dll)]
    public static extern int aomshim_f3_force_skip_low_temp_var(byte[] varianceLow, int miRow, int miCol, int bsize,
                                                                int smallSb);

    [DllImport(Dll)]
    public static extern int aomshim_f3_sms_tree(int sb128, int keyFreqMax, int[] blockSize, int[] split, int cap,
                                                 out int root);

    [DllImport(Dll)]
    public static extern void aomshim_f3_alloc_pmc(int bsize, int monochrome, int sb128, int allowScreenContentTools,
                                                   int[] output);

    [DllImport(Dll)]
    public static extern int aomshim_f3_choose_vbp(int[] p, byte[] frames, uint[] threshExitPart, ulong[] srcSadBlk,
                                                   int[] sbInfo, long[] thresholds, int[] sbOut, int[] vtOut,
                                                   int[] gridOut);
}
