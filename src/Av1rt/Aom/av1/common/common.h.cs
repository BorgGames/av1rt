// Port of libaom av1/common/common.h

global using static Av1rt.Aom.common;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class common
{
    // <gen:consts>
    internal const int AOM_FRAME_MARKER = 0x2;
    internal const int AV1_MIN_TILE_SIZE_BYTES = 1;
    // </gen:consts>

    // av1_copy: "Only need this for fixed-size arrays, for structs just assign."
    // [port] C copies the whole (multi-dimensional) array with one memcpy; the C# arrays are
    // jagged, so the overloads copy element-wise at the innermost level. C# overload resolution
    // picks the deepest matching overload, so av1_copy(a, b) never copies row references.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_copy<T>(T[] dest, T[] src)
    {
        Debug.Assert(dest.Length == src.Length);
        src.AsSpan().CopyTo(dest);
    }

    internal static void av1_copy<T>(T[][] dest, T[][] src)
    {
        Debug.Assert(dest.Length == src.Length);
        for (int i = 0; i < src.Length; i++) av1_copy(dest[i], src[i]);
    }

    internal static void av1_copy<T>(T[][][] dest, T[][][] src)
    {
        Debug.Assert(dest.Length == src.Length);
        for (int i = 0; i < src.Length; i++) av1_copy(dest[i], src[i]);
    }

    internal static void av1_copy<T>(T[][][][] dest, T[][][][] src)
    {
        Debug.Assert(dest.Length == src.Length);
        for (int i = 0; i < src.Length; i++) av1_copy(dest[i], src[i]);
    }

    // Use this for variably-sized arrays.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_copy_array<T>(Ptr<T> dest, Ptr<T> src, int n) => memcpy(dest, src, n);

    // av1_zero(dest): memset(&dest, 0, sizeof(dest)).
    // [port] Arrays (any rank) are cleared in place; value structs are reset to default.
    // Class-mapped structs need their own reset at the call site.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_zero<T>(T[] dest) => dest.AsSpan().Clear();

    internal static void av1_zero<T>(T[][] dest)
    {
        for (int i = 0; i < dest.Length; i++) av1_zero(dest[i]);
    }

    internal static void av1_zero<T>(T[][][] dest)
    {
        for (int i = 0; i < dest.Length; i++) av1_zero(dest[i]);
    }

    internal static void av1_zero<T>(T[][][][] dest)
    {
        for (int i = 0; i < dest.Length; i++) av1_zero(dest[i]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_zero<T>(ref T dest) where T : struct => dest = default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void av1_zero_array<T>(Ptr<T> dest, int n) => dest.AsSpan(n).Clear();

    // [port] Allocation of C fixed-size multi-dimensional arrays held by classes (jagged).
    internal static T[][] new_jagged<T>(int d0, int d1)
    {
        var a = new T[d0][];
        for (int i = 0; i < d0; i++) a[i] = new T[d1];
        return a;
    }

    internal static T[][][] new_jagged<T>(int d0, int d1, int d2)
    {
        var a = new T[d0][][];
        for (int i = 0; i < d0; i++) a[i] = new_jagged<T>(d1, d2);
        return a;
    }

    internal static T[][][][] new_jagged<T>(int d0, int d1, int d2, int d3)
    {
        var a = new T[d0][][][];
        for (int i = 0; i < d0; i++) a[i] = new_jagged<T>(d1, d2, d3);
        return a;
    }

    internal static T[][][][][] new_jagged<T>(int d0, int d1, int d2, int d3, int d4)
    {
        var a = new T[d0][][][][];
        for (int i = 0; i < d0; i++) a[i] = new_jagged<T>(d1, d2, d3, d4);
        return a;
    }

    // [port] Allocates n instances of a class-mapped struct (C fixed array of structs).
    internal static T[] new_array<T>(int n) where T : class, new()
    {
        var a = new T[n];
        for (int i = 0; i < n; i++) a[i] = new T();
        return a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int get_unsigned_bits(uint num_values)
    {
        return num_values > 0 ? get_msb(num_values) + 1 : 0;
    }

    // CHECK_MEM_ERROR(cm, lval, expr): [port] allocations cannot return NULL in C#; callers
    // just assign `lval = expr`.
}
