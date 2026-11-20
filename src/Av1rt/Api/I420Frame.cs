#nullable enable
using System;

namespace Av1rt;

/// <summary>A borrowed 8-bit 4:2:0 planar picture: three planes with their row strides.</summary>
public readonly ref struct I420Frame
{
    /// <summary>Luma plane.</summary>
    public readonly ReadOnlySpan<byte> Y;
    /// <summary>Cb plane, ceil(width/2) by ceil(height/2).</summary>
    public readonly ReadOnlySpan<byte> U;
    /// <summary>Cr plane, ceil(width/2) by ceil(height/2).</summary>
    public readonly ReadOnlySpan<byte> V;
    /// <summary>Bytes between luma rows.</summary>
    public readonly int StrideY;
    /// <summary>Bytes between Cb rows.</summary>
    public readonly int StrideU;
    /// <summary>Bytes between Cr rows.</summary>
    public readonly int StrideV;

    /// <summary>Wraps three planes.</summary>
    public I420Frame(ReadOnlySpan<byte> y, int strideY, ReadOnlySpan<byte> u, int strideU, ReadOnlySpan<byte> v, int strideV)
    {
        Y = y; U = u; V = v;
        StrideY = strideY; StrideU = strideU; StrideV = strideV;
    }

    /// <summary>
    /// Wraps a tightly packed I420 buffer (Y, then U, then V, no row padding) of the given size.
    /// </summary>
    public static I420Frame FromPacked(ReadOnlySpan<byte> i420, int width, int height)
    {
        int cw = (width + 1) >> 1, ch = (height + 1) >> 1;
        int ySize = width * height, cSize = cw * ch;
        if (i420.Length < ySize + 2 * cSize)
            throw new ArgumentException($"An I420 frame of {width}x{height} needs {ySize + 2 * cSize} bytes, got {i420.Length}.", nameof(i420));
        return new I420Frame(i420.Slice(0, ySize), width,
                             i420.Slice(ySize, cSize), cw,
                             i420.Slice(ySize + cSize, cSize), cw);
    }

    /// <summary>Size in bytes of a tightly packed I420 frame.</summary>
    public static int PackedSize(int width, int height) =>
        width * height + 2 * (((width + 1) >> 1) * ((height + 1) >> 1));
}
