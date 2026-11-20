// Port of libaom av1/common/entropymv.h

// <gen:aliases>
global using MV_JOINT_TYPE = int;
global using MV_CLASS_TYPE = int;
global using MvSubpelPrecision = int;
// </gen:aliases>

global using static Av1rt.Aom.entropymv;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// <gen:classes>
internal sealed partial class nmv_component
{
    public readonly ushort[] classes_cdf = new ushort[CDF_SIZE(MV_CLASSES)];
    public readonly ushort[][] class0_fp_cdf = new_jagged<ushort>(CLASS0_SIZE, CDF_SIZE(MV_FP_SIZE));
    public readonly ushort[] fp_cdf = new ushort[CDF_SIZE(MV_FP_SIZE)];
    public readonly ushort[] sign_cdf = new ushort[CDF_SIZE(2)];
    public readonly ushort[] class0_hp_cdf = new ushort[CDF_SIZE(2)];
    public readonly ushort[] hp_cdf = new ushort[CDF_SIZE(2)];
    public readonly ushort[] class0_cdf = new ushort[CDF_SIZE(CLASS0_SIZE)];
    public readonly ushort[][] bits_cdf = new_jagged<ushort>(MV_OFFSET_BITS, CDF_SIZE(2));

    /// <summary>C struct assignment (<c>*this = *other</c>): deep copy of every field.</summary>
    public void CopyFrom(nmv_component other)
    {
        av1_copy(classes_cdf, other.classes_cdf);
        av1_copy(class0_fp_cdf, other.class0_fp_cdf);
        av1_copy(fp_cdf, other.fp_cdf);
        av1_copy(sign_cdf, other.sign_cdf);
        av1_copy(class0_hp_cdf, other.class0_hp_cdf);
        av1_copy(hp_cdf, other.hp_cdf);
        av1_copy(class0_cdf, other.class0_cdf);
        av1_copy(bits_cdf, other.bits_cdf);
    }
}

internal sealed partial class nmv_context
{
    public readonly ushort[] joints_cdf = new ushort[CDF_SIZE(MV_JOINTS)];
    public readonly nmv_component[] comps = { new(), new() };

    /// <summary>C struct assignment (<c>*this = *other</c>): deep copy of every field.</summary>
    public void CopyFrom(nmv_context other)
    {
        av1_copy(joints_cdf, other.joints_cdf);
        for (int i = 0; i < comps.Length; i++) comps[i].CopyFrom(other.comps[i]);
    }
}
// </gen:classes>

internal static partial class entropymv
{
    // <gen:consts>
    internal const int MV_UPDATE_PROB = 252;
    internal const int MV_JOINTS = 4;
    internal const int MV_CLASSES = 11;
    internal const int CLASS0_BITS = 1;
    internal const int CLASS0_SIZE = (1 << CLASS0_BITS);
    internal const int MV_OFFSET_BITS = (MV_CLASSES + CLASS0_BITS - 2);
    internal const int MV_BITS_CONTEXTS = 6;
    internal const int MV_FP_SIZE = 4;
    internal const int MV_MAX_BITS = (MV_CLASSES + CLASS0_BITS + 2);
    internal const int MV_MAX = ((1 << MV_MAX_BITS) - 1);
    internal const int MV_VALS = ((MV_MAX << 1) + 1);
    internal const int MV_IN_USE_BITS = 14;
    internal const int MV_UPP = (1 << MV_IN_USE_BITS);
    internal const int MV_LOW = (-(1 << MV_IN_USE_BITS));

    // MV_JOINT_TYPE
    internal const int MV_JOINT_ZERO = 0;
    internal const int MV_JOINT_HNZVZ = 1;
    internal const int MV_JOINT_HZVNZ = 2;
    internal const int MV_JOINT_HNZVNZ = 3;

    // MV_CLASS_TYPE
    internal const int MV_CLASS_0 = 0;
    internal const int MV_CLASS_1 = 1;
    internal const int MV_CLASS_2 = 2;
    internal const int MV_CLASS_3 = 3;
    internal const int MV_CLASS_4 = 4;
    internal const int MV_CLASS_5 = 5;
    internal const int MV_CLASS_6 = 6;
    internal const int MV_CLASS_7 = 7;
    internal const int MV_CLASS_8 = 8;
    internal const int MV_CLASS_9 = 9;
    internal const int MV_CLASS_10 = 10;

    // MvSubpelPrecision
    internal const int MV_SUBPEL_NONE = -1;
    internal const int MV_SUBPEL_LOW_PRECISION = 0;
    internal const int MV_SUBPEL_HIGH_PRECISION = 1;
    // </gen:consts>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int mv_joint_vertical(MV_JOINT_TYPE type)
    {
        return B(type == MV_JOINT_HZVNZ || type == MV_JOINT_HNZVNZ);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int mv_joint_horizontal(MV_JOINT_TYPE type)
    {
        return B(type == MV_JOINT_HNZVZ || type == MV_JOINT_HNZVNZ);
    }
}
