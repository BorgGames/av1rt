#nullable enable
using System;
using System.Buffers.Binary;
using System.IO;

namespace Av1rt;

/// <summary>
/// Writes AV1 temporal units into an IVF file, the simple container that aomenc, ffmpeg and
/// dav1d read.
/// </summary>
public sealed class IvfWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly int _width;
    private readonly int _height;
    private readonly int _timebaseDen;
    private readonly int _timebaseNum;
    private readonly long _headerPosition;
    private int _frameCount;

    /// <summary>Starts an IVF stream; the header is rewritten with the frame count on dispose if the stream is seekable.</summary>
    /// <param name="stream">Destination stream.</param>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <param name="timebaseDen">Time base denominator, e.g. the frame rate for one tick per frame.</param>
    /// <param name="timebaseNum">Time base numerator.</param>
    /// <param name="leaveOpen">Leave <paramref name="stream"/> open on dispose.</param>
    public IvfWriter(Stream stream, int width, int height, int timebaseDen, int timebaseNum = 1, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
        _width = width;
        _height = height;
        _timebaseDen = timebaseDen;
        _timebaseNum = timebaseNum;
        _headerPosition = stream.CanSeek ? stream.Position : -1;
        WriteHeader();
    }

    /// <summary>Number of frames written so far.</summary>
    public int FrameCount => _frameCount;

    /// <summary>Appends one temporal unit with the given presentation timestamp (in time base units).</summary>
    public void WriteFrame(ReadOnlySpan<byte> temporalUnit, long pts)
    {
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)temporalUnit.Length);
        BinaryPrimitives.WriteInt64LittleEndian(header[4..], pts);
        _stream.Write(header);
        _stream.Write(temporalUnit);
        _frameCount++;
    }

    private void WriteHeader()
    {
        Span<byte> header = stackalloc byte[32];
        "DKIF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(header[6..], 32);
        "AV01"u8.CopyTo(header[8..]);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], (ushort)_width);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], (ushort)_height);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], (uint)_timebaseDen);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], (uint)_timebaseNum);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)_frameCount);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], 0);
        _stream.Write(header);
    }

    /// <summary>Patches the frame count into the header (when seekable) and closes the stream unless asked not to.</summary>
    public void Dispose()
    {
        if (_headerPosition >= 0)
        {
            long end = _stream.Position;
            _stream.Position = _headerPosition;
            WriteHeader();
            _stream.Position = end;
        }
        _stream.Flush();
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }
}
