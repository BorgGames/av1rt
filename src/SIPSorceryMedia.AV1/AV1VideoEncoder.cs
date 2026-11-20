using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Av1rt;
using SIPSorceryMedia.Abstractions;

namespace SIPSorceryMedia.AV1;

/// <summary>
/// A realtime AV1 encoder for SIPSorcery, built on av1rt. Each encoded frame is one AV1 temporal
/// unit in the low overhead bitstream format, ready for SIPSorcery's AV1 RTP packetiser.
/// </summary>
/// <remarks>
/// <para>Encode only: the decode methods throw <see cref="NotSupportedException"/>.</para>
/// <para>
/// The encoder is created on the first frame and re-created, starting with a key frame, whenever the
/// frame size changes. Calls are serialised internally.
/// </para>
/// </remarks>
public sealed class AV1VideoEncoder : IVideoEncoder
{
    /// <summary>The RTP payload type SIPSorcery suggests for AV1.</summary>
    public const int DefaultFormatId = 101;

    private readonly List<VideoFormat> _supportedFormats = new() { new VideoFormat(VideoCodecsEnum.AV1, DefaultFormatId) };
    private readonly Av1EncoderOptions _template;
    private readonly object _lock = new();
    private readonly ArrayBufferWriter<byte> _output = new(1 << 20);
    private Av1Encoder _encoder;
    private byte[] _i420 = Array.Empty<byte>();
    private byte[] _staging = Array.Empty<byte>();
    private bool _forceKeyFrame;
    private int _targetBitrateKbps;
    private bool _disposed;

    /// <summary>
    /// Creates an encoder. <paramref name="options"/> supplies everything except the frame size,
    /// which is taken from each frame; null means libwebrtc's defaults (speed 11, 35 Mbps CBR).
    /// </summary>
    public AV1VideoEncoder(Av1EncoderOptions options = null)
    {
        _template = options ?? new Av1EncoderOptions();
        _targetBitrateKbps = _template.TargetBitrateKbps;
    }

    /// <inheritdoc/>
    public List<VideoFormat> SupportedFormats => _supportedFormats;

    /// <summary>Target bitrate in kilobits per second. Changes apply from the next frame.</summary>
    public int TargetBitrateKbps
    {
        get => _targetBitrateKbps;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            lock (_lock)
            {
                _targetBitrateKbps = value;
                _encoder?.SetTargetBitrate(value);
            }
        }
    }

    /// <summary>Raised after every encoded (not dropped) frame, from the encoding thread.</summary>
    public event Action<Av1EncodeResult> FrameEncoded;

    /// <inheritdoc/>
    public void ForceKeyFrame() => _forceKeyFrame = true;

    /// <inheritdoc/>
    /// <returns>The temporal unit, or null if the rate control dropped the frame.</returns>
    public byte[] EncodeVideo(int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat, VideoCodecsEnum codec)
    {
        ArgumentNullException.ThrowIfNull(sample);
        return EncodeToArray(width, height, sample, I420Converter.PackedStride(pixelFormat, width), pixelFormat, codec);
    }

    /// <inheritdoc/>
    /// <returns>The temporal unit, or null if the rate control dropped the frame.</returns>
    public byte[] EncodeVideoFaster(RawImage rawImage, VideoCodecsEnum codec)
    {
        ArgumentNullException.ThrowIfNull(rawImage);
        if (rawImage.Sample == IntPtr.Zero) throw new ArgumentException("The image has no pixel data.", nameof(rawImage));
        int stride = rawImage.Stride > 0 ? rawImage.Stride : I420Converter.PackedStride(rawImage.PixelFormat, rawImage.Width);
        int size = I420Converter.SourceSize(rawImage.PixelFormat, stride, rawImage.Width, rawImage.Height);
        lock (_lock)
        {
            if (_staging.Length < size) _staging = new byte[size];
            Marshal.Copy(rawImage.Sample, _staging, 0, size);
            return EncodeToArray(rawImage.Width, rawImage.Height, _staging.AsSpan(0, size), stride, rawImage.PixelFormat, codec);
        }
    }

    /// <summary>
    /// Encodes a frame into a caller-supplied writer without allocating a result array.
    /// </summary>
    /// <returns>What was encoded; <see cref="Av1EncodeResult.Dropped"/> if nothing was written.</returns>
    public Av1EncodeResult EncodeVideo(int width, int height, ReadOnlySpan<byte> sample, int stride,
                                       VideoPixelFormatsEnum pixelFormat, IBufferWriter<byte> output, long timestamp = -1)
    {
        ArgumentNullException.ThrowIfNull(output);
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (stride <= 0) stride = I420Converter.PackedStride(pixelFormat, width);
            if (sample.Length < I420Converter.SourceSize(pixelFormat, stride, width, height))
                throw new ArgumentException($"The sample is too small for a {width}x{height} {pixelFormat} frame.", nameof(sample));

            Av1Encoder encoder = EnsureEncoder(width, height);
            int size = I420Frame.PackedSize(width, height);
            if (_i420.Length < size) _i420 = new byte[size];
            I420Converter.Convert(sample, stride, pixelFormat, width, height, _i420);

            if (_forceKeyFrame)
            {
                _forceKeyFrame = false;
                encoder.ForceKeyFrame();
            }
            Av1EncodeResult result = encoder.Encode(I420Frame.FromPacked(_i420, width, height), output, timestamp);
            if (!result.Dropped) FrameEncoded?.Invoke(result);
            return result;
        }
    }

    /// <summary>Not supported: this is an encoder only.</summary>
    public IEnumerable<VideoSample> DecodeVideo(byte[] encodedSample, VideoPixelFormatsEnum pixelFormat, VideoCodecsEnum codec) =>
        throw new NotSupportedException("SIPSorceryMedia.AV1 only encodes; use another component to decode AV1.");

    /// <summary>Not supported: this is an encoder only.</summary>
    public IEnumerable<RawImage> DecodeVideoFaster(byte[] encodedSample, VideoPixelFormatsEnum pixelFormat, VideoCodecsEnum codec) =>
        throw new NotSupportedException("SIPSorceryMedia.AV1 only encodes; use another component to decode AV1.");

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _encoder?.Dispose();
            _encoder = null;
        }
    }

    private byte[] EncodeToArray(int width, int height, ReadOnlySpan<byte> sample, int stride, VideoPixelFormatsEnum pixelFormat, VideoCodecsEnum codec)
    {
        if (codec != VideoCodecsEnum.AV1) throw new NotSupportedException($"This encoder only produces AV1, not {codec}.");
        lock (_lock)
        {
            _output.ResetWrittenCount();
            Av1EncodeResult result = EncodeVideo(width, height, sample, stride, pixelFormat, _output);
            return result.Dropped ? null : _output.WrittenSpan.ToArray();
        }
    }

    private Av1Encoder EnsureEncoder(int width, int height)
    {
        if (_encoder != null && _encoder.Width == width && _encoder.Height == height)
            return _encoder;

        _encoder?.Dispose();
        _encoder = null;
        var o = _template;
        _encoder = new Av1Encoder(new Av1EncoderOptions
        {
            Width = width, Height = height, FrameRate = o.FrameRate, TargetBitrateKbps = _targetBitrateKbps,
            Speed = o.Speed, Threads = o.Threads, TileColumnsLog2 = o.TileColumnsLog2, TileRowsLog2 = o.TileRowsLog2,
            Superblock = o.Superblock, MinQuantizer = o.MinQuantizer, MaxQuantizer = o.MaxQuantizer,
            FrameDropThreshold = o.FrameDropThreshold, KeyFrameInterval = o.KeyFrameInterval,
            EnableCdef = o.EnableCdef, EnableCyclicRefresh = o.EnableCyclicRefresh,
        });
        return _encoder;
    }
}
