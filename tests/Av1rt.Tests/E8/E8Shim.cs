using System;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace Av1rt.Tests.E8;

// libaom wrappers from tools/oracle/shim/shim_e8.c.
internal static class E8Shim
{
    private const string Dll = "aomshim_e8";

    [DllImport(Dll)]
    public static extern long aomshim_e8_block_error_lp(short[] coeff, short[] dqcoeff, int blockSize);

    [DllImport(Dll)]
    public static extern int aomshim_e8_compute_rd_mult_based_on_qindex(int bitDepth, int updateType, int qindex, int tuning, int mode);

    [DllImport(Dll)]
    public static extern int aomshim_e8_compute_rd_mult(int qindex, int bitDepth, int updateType, int layerDepth, int boostIndex,
                                                         int frameType, int useFixedQpOffsets, int isStatConsumptionStage,
                                                         int tuning, int mode);

    [DllImport(Dll)]
    public static extern int aomshim_e8_intra_cost_penalty(int qindex, int qdelta, int bitDepth);

    [DllImport(Dll)]
    public static extern void aomshim_e8_model_rd_from_var_lapndz(long var, uint nLog2, uint qstep, out int rate, out long dist);

    [DllImport(Dll)]
    public static extern void aomshim_e8_sad_per_bit_lut8(int[] output);

    [DllImport(Dll)]
    public static extern int aomshim_e8_speed_features(int[] p, long[] output, int capacity);

    [DllImport(Dll)]
    public static extern int aomshim_e8_rd_consts(int[] p, ushort[] cdfOut, int cdfCapacity, out int cdfCount,
                                                  int[] output, int capacity);
}

// Originally let tests pass while dependencies were stubs. Every dependency is ported now, so
// a stub hit is a real failure.
internal static class Deferred
{
    public static void Run(ITestOutputHelper output, Action test) => test();
}
