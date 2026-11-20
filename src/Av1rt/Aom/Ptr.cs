using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

/// <summary>
/// A safe stand-in for a C pointer into an array: the array plus an element offset.
/// </summary>
/// <remarks>
/// <para>
/// The port keeps libaom's pointer arithmetic as written, including negative indexing such as
/// <c>p[-1]</c> or <c>p[-stride]</c>, which a <see cref="Span{T}"/> cannot express. Every access
/// is bounds checked against the whole underlying array, so a stray index fails loudly
/// instead of corrupting memory.
/// </para>
/// <para>
/// It is a small value type (an object reference and an int), cheap to pass by value and safe
/// to store in fields, unlike a span. Use <see cref="AsSpan()"/> to hand a range to span based
/// or SIMD code.
/// </para>
/// </remarks>
[DebuggerDisplay("{Offset} of {Array == null ? 0 : Array.Length}")]
internal readonly partial struct Ptr<T> : IEquatable<Ptr<T>>
{
    public readonly T[] Array;
    public readonly int Offset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ptr(T[] array, int offset = 0)
    {
        Array = array;
        Offset = offset;
    }

    /// <summary>A null pointer.</summary>
    public static Ptr<T> Null => default;

    /// <summary>True for a null pointer (no underlying array).</summary>
    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Array is null;
    }

    /// <summary>C's <c>p[index]</c>.</summary>
    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Array[Offset + index];
    }

    /// <summary>C's <c>*p</c>.</summary>
    public ref T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Array[Offset];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ptr<T> operator +(Ptr<T> p, int n) => new(p.Array, p.Offset + n);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ptr<T> operator -(Ptr<T> p, int n) => new(p.Array, p.Offset - n);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ptr<T> operator ++(Ptr<T> p) => new(p.Array, p.Offset + 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ptr<T> operator --(Ptr<T> p) => new(p.Array, p.Offset - 1);

    /// <summary>C's pointer difference. Both pointers must point into the same array.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int operator -(Ptr<T> a, Ptr<T> b)
    {
        Debug.Assert(ReferenceEquals(a.Array, b.Array));
        return a.Offset - b.Offset;
    }

    public static bool operator <(Ptr<T> a, Ptr<T> b) => a.Offset < b.Offset;
    public static bool operator >(Ptr<T> a, Ptr<T> b) => a.Offset > b.Offset;
    public static bool operator <=(Ptr<T> a, Ptr<T> b) => a.Offset <= b.Offset;
    public static bool operator >=(Ptr<T> a, Ptr<T> b) => a.Offset >= b.Offset;
    public static bool operator ==(Ptr<T> a, Ptr<T> b) => ReferenceEquals(a.Array, b.Array) && a.Offset == b.Offset;
    public static bool operator !=(Ptr<T> a, Ptr<T> b) => !(a == b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Ptr<T>(T[] array) => new(array, 0);

    /// <summary>The elements from this pointer to the end of the array.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan() => Array.AsSpan(Offset);

    /// <summary><paramref name="length"/> elements starting at this pointer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan(int length) => Array.AsSpan(Offset, length);

    /// <summary><paramref name="length"/> elements starting at <c>p + start</c>; <paramref name="start"/> may be negative.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan(int start, int length) => Array.AsSpan(Offset + start, length);

    public bool Equals(Ptr<T> other) => this == other;
    public override bool Equals(object obj) => obj is Ptr<T> p && this == p;
    public override int GetHashCode() => HashCode.Combine(Array, Offset);
}
