global using static Av1rt.Aom.CRuntime;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

/// <summary>Raised where libaom calls aom_internal_error() or otherwise fails hard.</summary>
public sealed partial class AomException : Exception
{
    /// <summary>The libaom error code (an aom_codec_err_t value).</summary>
    public int ErrorCode { get; }

    public AomException(int errorCode, string message) : base(message) => ErrorCode = errorCode;
}

/// <summary>
/// Replacements for the C runtime calls libaom makes. Counts are in elements, not bytes:
/// a C <c>memcpy(d, s, n * sizeof(T))</c> becomes <c>memcpy(d, s, n)</c>.
/// </summary>
internal static partial class CRuntime
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void memcpy<T>(Ptr<T> dst, Ptr<T> src, int count) =>
        src.AsSpan(count).CopyTo(dst.AsSpan(count));

    /// <summary>memmove: Span.CopyTo already handles overlap.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void memmove<T>(Ptr<T> dst, Ptr<T> src, int count) =>
        src.AsSpan(count).CopyTo(dst.AsSpan(count));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void memset<T>(Ptr<T> dst, T value, int count) => dst.AsSpan(count).Fill(value);

    /// <summary>C's truthiness of an int, for <c>if (x)</c> style code kept as int flags.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int B(bool value) => value ? 1 : 0;

    /// <summary>Copies a whole array; for C struct-array assignments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void copy_array<T>(T[] dst, T[] src) => System.Array.Copy(src, dst, src.Length);
}
