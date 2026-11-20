using System;
using System.IO;
using System.Text;

namespace Av1rt;

/// <summary>Reads 8-bit 4:2:0 YUV4MPEG2 (.y4m) files frame by frame.</summary>
public sealed class Y4mReader : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    /// <summary>Frame width in pixels.</summary>
    public int Width { get; }

    /// <summary>Frame height in pixels.</summary>
    public int Height { get; }

    /// <summary>Frame rate numerator from the header (0 if absent).</summary>
    public int FrameRateNumerator { get; }

    /// <summary>Frame rate denominator from the header (0 if absent).</summary>
    public int FrameRateDenominator { get; }

    /// <summary>Bytes in one I420 frame: Y plus two half-resolution chroma planes.</summary>
    public int FrameSize => Width * Height + 2 * ((Width + 1) / 2) * ((Height + 1) / 2);

    /// <summary>Opens a Y4M stream and parses its header.</summary>
    public Y4mReader(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
        string header = ReadLine() ?? throw new InvalidDataException("Empty Y4M stream.");
        if (!header.StartsWith("YUV4MPEG2", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Not a YUV4MPEG2 stream.");
        }
        foreach (string token in header.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token[0])
            {
                case 'W': Width = int.Parse(token.AsSpan(1)); break;
                case 'H': Height = int.Parse(token.AsSpan(1)); break;
                case 'F':
                    int colon = token.IndexOf(':');
                    if (colon > 0)
                    {
                        FrameRateNumerator = int.Parse(token.AsSpan(1, colon - 1));
                        FrameRateDenominator = int.Parse(token.AsSpan(colon + 1));
                    }
                    break;
                case 'C':
                    if (token is not ("C420" or "C420jpeg" or "C420paldv" or "C420mpeg2"))
                    {
                        throw new InvalidDataException($"Only 8-bit 4:2:0 Y4M is supported, got {token}.");
                    }
                    break;
            }
        }
        if (Width <= 0 || Height <= 0)
        {
            throw new InvalidDataException("Y4M header has no frame size.");
        }
    }

    /// <summary>Opens a Y4M file.</summary>
    public static Y4mReader Open(string path) => new(File.OpenRead(path));

    /// <summary>Reads the next frame as packed I420 (Y, then U, then V, no padding).</summary>
    /// <returns>False at the end of the stream.</returns>
    public bool ReadFrame(Span<byte> i420)
    {
        if (i420.Length < FrameSize)
        {
            throw new ArgumentException($"Buffer must hold {FrameSize} bytes.", nameof(i420));
        }
        string marker = ReadLine();
        if (marker == null)
        {
            return false;
        }
        if (!marker.StartsWith("FRAME", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Bad Y4M frame marker.");
        }
        _stream.ReadExactly(i420[..FrameSize]);
        return true;
    }

    private string ReadLine()
    {
        var sb = new StringBuilder();
        while (true)
        {
            int b = _stream.ReadByte();
            if (b < 0)
            {
                return sb.Length == 0 ? null : sb.ToString();
            }
            if (b == '\n')
            {
                return sb.ToString();
            }
            sb.Append((char)b);
        }
    }

    /// <summary>Closes the stream unless asked not to.</summary>
    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }
}
