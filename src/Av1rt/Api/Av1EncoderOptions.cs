#nullable enable
using System;

namespace Av1rt;

/// <summary>Superblock size choice for <see cref="Av1EncoderOptions.Superblock"/>.</summary>
public enum Av1SuperblockSize
{
    /// <summary>
    /// 64x64 from 960x540 with 4 or more threads (WebRTC's rule, which stops below 1920x1080,
    /// extended to all larger sizes), otherwise libaom's dynamic choice (128x128 when the shorter
    /// side exceeds 720 pixels).
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
    /// Nominal frame rate: the frame time when <see cref="Av1Encoder.Encode(in I420Frame, System.Buffers.IBufferWriter{byte}, long)"/> is not given
    /// timestamps, and of the first frame when it is. With timestamps the rate control follows
    /// their actual spacing instead.
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
    /// <see cref="Environment.ProcessorCount"/> (at most 8 threads), except that frames of about
    /// 4K and up use every logical processor (at most 16): 4K at 60 fps needs all of a 6-core
    /// desktop.
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

    /// <summary>
    /// Colour primaries signalled in the sequence header. Like the transfer characteristics, matrix
    /// coefficients and range, it tells the decoder how to interpret the samples and does not change
    /// the encoding. The header carries a colour description only when at least one of the three is
    /// specified.
    /// </summary>
    public Av1ColorPrimaries ColorPrimaries { get; set; } = Av1ColorPrimaries.Unspecified;

    /// <summary>Transfer characteristics signalled in the sequence header.</summary>
    public Av1TransferCharacteristics TransferCharacteristics { get; set; } = Av1TransferCharacteristics.Unspecified;

    /// <summary>
    /// Matrix coefficients signalled in the sequence header: the RGB to YCbCr conversion the input
    /// was made with, such as <see cref="Av1MatrixCoefficients.Bt709"/>.
    /// </summary>
    public Av1MatrixCoefficients MatrixCoefficients { get; set; } = Av1MatrixCoefficients.Unspecified;

    /// <summary>
    /// The input uses the full 0 to 255 range instead of the limited one (16 to 235 luma, 16 to 240
    /// chroma); signalled in the sequence header.
    /// </summary>
    public bool FullRange { get; set; }

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
        if (!Enum.IsDefined(ColorPrimaries))
            throw new ArgumentOutOfRangeException(nameof(ColorPrimaries));
        if (!Enum.IsDefined(TransferCharacteristics))
            throw new ArgumentOutOfRangeException(nameof(TransferCharacteristics));
        if (!Enum.IsDefined(MatrixCoefficients))
            throw new ArgumentOutOfRangeException(nameof(MatrixCoefficients));
    }

    /// <summary>
    /// libwebrtc's thread count rule (LibaomAv1Encoder::NumberOfThreads), plus all logical
    /// processors (up to 16) from about 4K.
    /// </summary>
    internal static int WebRtcThreads(int width, int height, int cores)
    {
        long area = (long)width * height;
        if (area >= 3840L * 2160 * 3 / 4 && cores > 8) return Math.Min(cores, 16);
        if (area > 1280 * 720 && cores > 8) return 8;
        if (area >= 640 * 360 && cores > 4) return 4;
        if (area >= 320 * 180 && cores > 2) return 2;
        return 1;
    }
}
