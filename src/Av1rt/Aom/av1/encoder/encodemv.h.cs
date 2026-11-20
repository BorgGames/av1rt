// Port of libaom av1/encoder/encodemv.h
global using static Av1rt.Aom.encodemv;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class encodemv
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MV_JOINT_TYPE av1_get_mv_joint(in MV mv)
    {
        // row:  Z  col:  Z  | MV_JOINT_ZERO   (0)
        // row:  Z  col: NZ  | MV_JOINT_HNZVZ  (1)
        // row: NZ  col:  Z  | MV_JOINT_HZVNZ  (2)
        // row: NZ  col: NZ  | MV_JOINT_HNZVNZ (3)
        return B(mv.col != 0) | (B(mv.row != 0) << 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_mv_class_base(MV_CLASS_TYPE c)
    {
        return c != 0 ? CLASS0_SIZE << (c + 2) : 0;
    }

    // If n != 0, returns the floor of log base 2 of n. If n == 0, returns 0.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte av1_log_in_base_2(uint n)
    {
        // get_msb() is only valid when n != 0.
        return n == 0 ? (byte)0 : (byte)get_msb(n);
    }

    // PORT-NOTE: C takes `int *offset` and skips the store when it is NULL; every in-scope
    // caller passes a valid pointer, so it is a `ref`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MV_CLASS_TYPE av1_get_mv_class(int z, ref int offset)
    {
        Debug.Assert(z >= 0);
        MV_CLASS_TYPE c = (MV_CLASS_TYPE)av1_log_in_base_2((uint)(z >> 3));
        Debug.Assert(c <= MV_CLASS_10);
        offset = z - av1_mv_class_base(c);
        return c;
    }

    // [subset] av1_check_newmv_joint_nonzero: never executed in the subset (compound NEWMV
    // checks of the rd path).
}
