using System;

namespace Av1rt;

/// <summary>Superblock size choice for <see cref="Av1EncoderOptions.Superblock"/>.</summary>
public enum Av1SuperblockSize
{
    /// <summary>
    /// WebRTC's rule: 64x64 for 960x540 up to (excluding) 1920x1080 with 4 or more threads, otherwise
    /// libaom's dynamic choice (128x128 when the shorter side exceeds 720 pixels).
    /// </summary>
    Auto = 0,
    /// <summary>libaom's dynamic choice.</summary>
    Dynamic,
    /// <summary>Always 64x64.</summary>
    Size64,
    /// <summary>Always 128x128.</summary>
    Size128,
}

/// <summary>
/// Settings for <see cref="Av1Encoder"/>. The defaults reproduce libwebrtc's libaom configuration for
/// camera content (one pass, realtime, CBR, no lookahead), which is also what the bit-exactness
/// oracle uses.
/// </summary>
public sealed class Av1EncoderOptions
{
    /// <summary>Frame width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Frame height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>
    /// Nominal frame rate. Used to derive timestamps when <see cref="Av1Encoder.Encode"/> is not given
    /// one, and as the encoder's initial rate estimate.
    /// </summary>
    public int FrameRate { get; set; } = 60;

    /// <summary>Target bitrate in kilobits per second.</summary>
    public int TargetBitrateKbps { get; set; } = 35_000;

    /// <summary>
    /// libaom realtime speed (cpu-used), 5 to 12. Higher is faster. 11 is libwebrtc's choice for
    /// high resolutions and produces the same bitstream as libaom. 12 is an av1rt extension for
    /// 4K: speed 11 plus quantizer-derived CDEF strengths, half-pel motion vectors and no
    /// temporal source filter, about a third faster for a few percent more bitrate.
    /// </summary>
    public int Speed { get; set; } = 11;

    /// <summary>
    /// Worker threads. 0 picks libwebrtc's rule from the frame size and
    /// <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    public int Threads { get; set; }

    /// <summary>log2 of the tile column count, or -1 to let the encoder choose (one tile per thread).</summary>
    public int TileColumnsLog2 { get; set; } = -1;

    /// <summary>log2 of the tile row count, or -1 to let the encoder choose.</summary>
    public int TileRowsLog2 { get; set; } = -1;

    /// <summary>Superblock size.</summary>
    public Av1SuperblockSize Superblock { get; set; } = Av1SuperblockSize.Auto;

    /// <summary>Lowest quantizer the rate control may use (0 to 63).</summary>
    public int MinQuantizer { get; set; } = 10;

    /// <summary>Highest quantizer the rate control may use (0 to 63).</summary>
    public int MaxQuantizer { get; set; } = 56;

    /// <summary>
    /// Buffer fullness percentage below which the encoder drops frames to hold the bitrate, or 0 to
    /// never drop for buffer underflow.
    /// </summary>
    public int FrameDropThreshold { get; set; }

    /// <summary>Insert a key frame every this many frames; 0 means only on request.</summary>
    public int KeyFrameInterval { get; set; }

    /// <summary>Enable the CDEF in-loop filter.</summary>
    public bool EnableCdef { get; set; } = true;

    /// <summary>Use cyclic-refresh adaptive quantization (libwebrtc's aq-mode 3).</summary>
    public bool EnableCyclicRefresh { get; set; } = true;

    internal void Validate()
    {
        if (Width <= 0 || Height <= 0 || Width > 65536 || Height > 65536)
            throw new ArgumentOutOfRangeException(nameof(Width), $"Invalid frame size {Width}x{Height}.");
        if (FrameRate <= 0 || FrameRate > 1000)
            throw new ArgumentOutOfRangeException(nameof(FrameRate));
        if (TargetBitrateKbps <= 0)
            throw new ArgumentOutOfRangeException(nameof(TargetBitrateKbps));
        if (Speed < 5 || Speed > 12)
            throw new ArgumentOutOfRangeException(nameof(Speed), "Realtime speed must be 5 to 12.");
        if (Threads < 0 || Threads > 64)
            throw new ArgumentOutOfRangeException(nameof(Threads));
        if (TileColumnsLog2 < -1 || TileColumnsLog2 > 6)
            throw new ArgumentOutOfRangeException(nameof(TileColumnsLog2));
        if (TileRowsLog2 < -1 || TileRowsLog2 > 6)
            throw new ArgumentOutOfRangeException(nameof(TileRowsLog2));
        if (MinQuantizer < 0 || MaxQuantizer > 63 || MinQuantizer > MaxQuantizer)
            throw new ArgumentOutOfRangeException(nameof(MinQuantizer));
        if (FrameDropThreshold < 0 || FrameDropThreshold > 100)
            throw new ArgumentOutOfRangeException(nameof(FrameDropThreshold));
        if (KeyFrameInterval < 0)
            throw new ArgumentOutOfRangeException(nameof(KeyFrameInterval));
    }

    /// <summary>libwebrtc's thread count rule (LibaomAv1Encoder::NumberOfThreads).</summary>
    internal static int WebRtcThreads(int width, int height, int cores)
    {
        long area = (long)width * height;
        if (area > 1280 * 720 && cores > 8) return 8;
        if (area >= 640 * 360 && cores > 4) return 4;
        if (area >= 320 * 180 && cores > 2) return 2;
        return 1;
    }
}
