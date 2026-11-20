#nullable enable
using System;

namespace Av1rt;

/// <summary>
/// The encoder's own buffer for the next picture, from <see cref="Av1Encoder.GetInputFrame"/>:
/// three writable 8-bit 4:2:0 planes. Writing the picture here instead of passing one to
/// <see cref="Av1Encoder.Encode(byte[], System.Buffers.IBufferWriter{byte}, long)"/> saves the
/// encoder a copy of the whole frame.
/// </summary>
/// <remarks>
/// Row <c>r</c> of a plane starts at <c>r * stride</c>; only the first <see cref="Width"/> (luma)
/// or <see cref="ChromaWidth"/> (chroma) bytes of a row are picture, the rest belongs to the
/// encoder. The planes are <see cref="Memory{T}"/> so that several threads can fill row bands.
/// They stay valid until the next encode call.
/// </remarks>
public readonly struct Av1InputFrame
{
    /// <summary>Luma plane.</summary>
    public Memory<byte> Y { get; }
    /// <summary>Cb plane.</summary>
    public Memory<byte> U { get; }
    /// <summary>Cr plane.</summary>
    public Memory<byte> V { get; }
    /// <summary>Bytes between luma rows.</summary>
    public int StrideY { get; }
    /// <summary>Bytes between Cb rows and between Cr rows.</summary>
    public int StrideUV { get; }
    /// <summary>Picture width.</summary>
    public int Width { get; }
    /// <summary>Picture height.</summary>
    public int Height { get; }
    /// <summary>Chroma plane width, ceil(<see cref="Width"/> / 2).</summary>
    public int ChromaWidth => (Width + 1) >> 1;
    /// <summary>Chroma plane height, ceil(<see cref="Height"/> / 2).</summary>
    public int ChromaHeight => (Height + 1) >> 1;

    internal Av1InputFrame(Memory<byte> y, int strideY, Memory<byte> u, Memory<byte> v, int strideUV, int width, int height)
    {
        Y = y; U = u; V = v;
        StrideY = strideY; StrideUV = strideUV;
        Width = width; Height = height;
    }
}
