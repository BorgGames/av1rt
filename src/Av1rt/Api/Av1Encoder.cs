using System;
using System.Buffers;
using Av1rt.Aom;

namespace Av1rt;

/// <summary>What <see cref="Av1Encoder.Encode"/> produced for one input picture.</summary>
public readonly struct Av1EncodeResult
{
    /// <summary>Bytes written to the output: one AV1 temporal unit (low overhead OBU format), or 0 if the frame was dropped.</summary>
    public int Length { get; init; }

    /// <summary>The frame is a key frame (starts with a sequence header).</summary>
    public bool IsKeyFrame { get; init; }

    /// <summary>The rate control dropped the frame; nothing was written.</summary>
    public bool Dropped => Length == 0;

    /// <summary>Presentation timestamp of the frame, in 90 kHz units.</summary>
    public long Timestamp { get; init; }

    /// <summary>Base quantizer index the frame was coded with (0 to 255), or -1 if dropped.</summary>
    public int QIndex { get; init; }
}

/// <summary>
/// A realtime AV1 encoder: one pass, CBR, no lookahead, 8-bit 4:2:0. Every call to
/// <see cref="Encode"/> returns at most one temporal unit for the picture just passed in.
/// </summary>
/// <remarks>Not thread safe: use one instance from one thread at a time.</remarks>
public sealed class Av1Encoder : IDisposable
{
    /// <summary>The RTP clock rate timestamps are expressed in.</summary>
    public const int TimestampRate = 90_000;

    private readonly Av1EncoderOptions _options;
    private readonly aom_codec_ctx_t _codec = new();
    private readonly aom_codec_enc_cfg_t _cfg = new();
    private readonly aom_image_t _image;
    private readonly int[] _qOut = new int[1];
    private long _nextTimestamp;
    private long _framesIn;
    private bool _forceKeyFrame;
    private bool _disposed;

    /// <summary>Creates an encoder. The options are copied; later changes to them have no effect.</summary>
    public Av1Encoder(Av1EncoderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = new Av1EncoderOptions
        {
            Width = options.Width, Height = options.Height, FrameRate = options.FrameRate,
            TargetBitrateKbps = options.TargetBitrateKbps, Speed = options.Speed, Threads = options.Threads,
            TileColumnsLog2 = options.TileColumnsLog2, TileRowsLog2 = options.TileRowsLog2,
            Superblock = options.Superblock, MinQuantizer = options.MinQuantizer, MaxQuantizer = options.MaxQuantizer,
            FrameDropThreshold = options.FrameDropThreshold, KeyFrameInterval = options.KeyFrameInterval,
            EnableCdef = options.EnableCdef, EnableCyclicRefresh = options.EnableCyclicRefresh,
        };
        var o = _options;
        int threads = o.Threads > 0 ? o.Threads : Av1EncoderOptions.WebRtcThreads(o.Width, o.Height, Environment.ProcessorCount);

        aom_codec_iface_t iface = aom_codec_av1_cx();
        Check(aom_codec_enc_config_default(iface, _cfg, AOM_USAGE_REALTIME), "aom_codec_enc_config_default");
        _cfg.g_w = (uint)o.Width;
        _cfg.g_h = (uint)o.Height;
        _cfg.g_threads = (uint)threads;
        _cfg.g_timebase.num = 1;
        _cfg.g_timebase.den = TimestampRate;
        _cfg.rc_target_bitrate = (uint)o.TargetBitrateKbps;
        _cfg.rc_dropframe_thresh = (uint)o.FrameDropThreshold;
        _cfg.g_input_bit_depth = 8;
        _cfg.kf_mode = AOM_KF_DISABLED;
        _cfg.rc_min_quantizer = (uint)o.MinQuantizer;
        _cfg.rc_max_quantizer = (uint)o.MaxQuantizer;
        _cfg.rc_undershoot_pct = 50;
        _cfg.rc_overshoot_pct = 50;
        _cfg.rc_buf_initial_sz = 600;
        _cfg.rc_buf_optimal_sz = 600;
        _cfg.rc_buf_sz = 1000;
        _cfg.g_usage = AOM_USAGE_REALTIME;
        _cfg.g_error_resilient = 0;
        _cfg.rc_end_usage = AOM_CBR;
        _cfg.g_pass = AOM_RC_ONE_PASS;
        _cfg.g_lag_in_frames = 0;
        Check(aom_codec_enc_init_ver(_codec, iface, _cfg, 0, AOM_ENCODER_ABI_VERSION), "aom_codec_enc_init");

        try
        {
            // The same controls, in the same order, as libwebrtc and tools/oracle/av1rt_oracle.c.
            Control(AOME_SET_CPUUSED, o.Speed);
            Control(AV1E_SET_ENABLE_CDEF, o.EnableCdef ? 1 : 0);
            Control(AV1E_SET_ENABLE_TPL_MODEL, 0);
            Control(AV1E_SET_DELTAQ_MODE, 0);
            Control(AV1E_SET_ENABLE_ORDER_HINT, 0);
            Control(AV1E_SET_AQ_MODE, o.EnableCyclicRefresh ? 3 : 0);
            Control(AOME_SET_MAX_INTRA_BITRATE_PCT, 300);
            Control(AV1E_SET_COEFF_COST_UPD_FREQ, 3);
            Control(AV1E_SET_MODE_COST_UPD_FREQ, 3);
            Control(AV1E_SET_MV_COST_UPD_FREQ, 3);
            Control(AV1E_SET_ENABLE_PALETTE, 0);
            if (o.TileColumnsLog2 >= 0 || o.TileRowsLog2 >= 0)
            {
                Control(AV1E_SET_TILE_COLUMNS, Math.Max(o.TileColumnsLog2, 0));
                Control(AV1E_SET_TILE_ROWS, Math.Max(o.TileRowsLog2, 0));
            }
            else
            {
                Control(AV1E_SET_AUTO_TILES, 1);
            }
            Control(AV1E_SET_ROW_MT, 1);
            Control(AV1E_SET_ENABLE_OBMC, 0);
            Control(AV1E_SET_NOISE_SENSITIVITY, 0);
            Control(AV1E_SET_ENABLE_WARPED_MOTION, 0);
            Control(AV1E_SET_ENABLE_GLOBAL_MOTION, 0);
            Control(AV1E_SET_ENABLE_REF_FRAME_MVS, 0);
            Control(AV1E_SET_SUPERBLOCK_SIZE, SuperblockControl(o, threads));
            Control(AV1E_SET_ENABLE_CFL_INTRA, 0);
            Control(AV1E_SET_ENABLE_SMOOTH_INTRA, 0);
            Control(AV1E_SET_ENABLE_ANGLE_DELTA, 0);
            Control(AV1E_SET_ENABLE_FILTER_INTRA, 0);
            Control(AV1E_SET_INTRA_DEFAULT_TX_ONLY, 1);
            Control(AV1E_SET_DISABLE_TRELLIS_QUANT, 1);
            Control(AV1E_SET_ENABLE_DIST_WTD_COMP, 0);
            Control(AV1E_SET_ENABLE_DIFF_WTD_COMP, 0);
            Control(AV1E_SET_ENABLE_DUAL_FILTER, 0);
            Control(AV1E_SET_ENABLE_INTERINTRA_COMP, 0);
            Control(AV1E_SET_ENABLE_INTERINTRA_WEDGE, 0);
            Control(AV1E_SET_ENABLE_INTRA_EDGE_FILTER, 0);
            Control(AV1E_SET_ENABLE_INTRABC, 0);
            Control(AV1E_SET_ENABLE_MASKED_COMP, 0);
            Control(AV1E_SET_ENABLE_PAETH_INTRA, 0);
            Control(AV1E_SET_ENABLE_QM, 0);
            Control(AV1E_SET_ENABLE_RECT_PARTITIONS, 0);
            Control(AV1E_SET_ENABLE_RESTORATION, 0);
            Control(AV1E_SET_ENABLE_SMOOTH_INTERINTRA, 0);
            Control(AV1E_SET_ENABLE_TX64, 0);
            Control(AV1E_SET_MAX_REFERENCE_FRAMES, 3);
            Control(AV1E_SET_MAX_CONSEC_FRAME_DROP_MS_CBR, 250);
            Control(AV1E_SET_POSTENCODE_DROP_RTC, 1);

            _image = aom_img_alloc(null, AOM_IMG_FMT_I420, (uint)o.Width, (uint)o.Height, 32)
                     ?? throw new OutOfMemoryException("aom_img_alloc failed");
        }
        catch
        {
            aom_codec_destroy(_codec);
            throw;
        }
    }

    /// <summary>Frame width.</summary>
    public int Width => _options.Width;

    /// <summary>Frame height.</summary>
    public int Height => _options.Height;

    /// <summary>Current target bitrate in kilobits per second.</summary>
    public int TargetBitrateKbps => (int)_cfg.rc_target_bitrate;

    /// <summary>Makes the next encoded frame a key frame.</summary>
    public void ForceKeyFrame() => _forceKeyFrame = true;

    /// <summary>Changes the target bitrate, effective from the next frame.</summary>
    public void SetTargetBitrate(int kbps)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(kbps);
        if (_cfg.rc_target_bitrate == (uint)kbps) return;
        _cfg.rc_target_bitrate = (uint)kbps;
        Check(aom_codec_enc_config_set(_codec, _cfg), "aom_codec_enc_config_set");
    }

    /// <summary>
    /// Encodes one picture and appends the resulting temporal unit to <paramref name="output"/>.
    /// </summary>
    /// <param name="frame">The picture, <see cref="Width"/> by <see cref="Height"/>.</param>
    /// <param name="output">Receives the temporal unit (temporal delimiter, sequence header on key frames, frame OBU).</param>
    /// <param name="timestamp">
    /// Presentation time in 90 kHz units, or -1 to continue from the previous frame at the nominal
    /// frame rate. Real capture times let the rate control follow the actual frame rate.
    /// </param>
    public Av1EncodeResult Encode(in I420Frame frame, IBufferWriter<byte> output, long timestamp = -1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(output);
        CopyIn(frame);

        long duration = TimestampRate / _options.FrameRate;
        long pts = timestamp >= 0 ? timestamp : _nextTimestamp;
        _nextTimestamp = pts + duration;

        aom_enc_frame_flags_t flags = 0;
        bool keyRequested = _forceKeyFrame ||
            (_options.KeyFrameInterval > 0 && _framesIn > 0 && _framesIn % _options.KeyFrameInterval == 0);
        if (keyRequested)
            flags |= AOM_EFLAG_FORCE_KF;
        _forceKeyFrame = false;
        _framesIn++;

        Check(aom_codec_encode(_codec, _image, pts, (uint)duration, flags), "aom_codec_encode");

        int length = 0;
        bool key = false;
        long outPts = pts;
        aom_codec_iter_t iter = default;
        aom_codec_cx_pkt_t pkt;
        while ((pkt = aom_codec_get_cx_data(_codec, ref iter)) != null)
        {
            if (pkt.kind != AOM_CODEC_CX_FRAME_PKT) continue;
            int sz = pkt.data.frame.sz;
            pkt.data.frame.buf.AsSpan(sz).CopyTo(output.GetSpan(sz));
            output.Advance(sz);
            length += sz;
            key |= (pkt.data.frame.flags & AOM_FRAME_IS_KEY) != 0;
            outPts = pkt.data.frame.pts;
        }

        int q = -1;
        if (length > 0)
        {
            _qOut[0] = -1;
            Check(aom_codec_control(_codec, AOME_GET_LAST_QUANTIZER_64, new Ptr<int>(_qOut)), "AOME_GET_LAST_QUANTIZER_64");
            q = _qOut[0];
        }
        else if (keyRequested)
        {
            _forceKeyFrame = true; // a dropped key frame request carries over to the next picture
        }

        return new Av1EncodeResult { Length = length, IsKeyFrame = key, Timestamp = outPts, QIndex = q };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        aom_img_free(_image);
        aom_codec_destroy(_codec);
    }

    private void CopyIn(in I420Frame frame)
    {
        int w = _options.Width, h = _options.Height, cw = (w + 1) >> 1, ch = (h + 1) >> 1;
        CopyPlane(frame.Y, frame.StrideY, _image.planes[AOM_PLANE_Y], _image.stride[AOM_PLANE_Y], w, h, "Y");
        CopyPlane(frame.U, frame.StrideU, _image.planes[AOM_PLANE_U], _image.stride[AOM_PLANE_U], cw, ch, "U");
        CopyPlane(frame.V, frame.StrideV, _image.planes[AOM_PLANE_V], _image.stride[AOM_PLANE_V], cw, ch, "V");
    }

    private static void CopyPlane(ReadOnlySpan<byte> src, int srcStride, Ptr<byte> dst, int dstStride, int w, int h, string plane)
    {
        if (srcStride < w || src.Length < (long)srcStride * (h - 1) + w)
            throw new ArgumentException($"Plane {plane} is too small for {w}x{h} with stride {srcStride}.", nameof(src));
        Span<byte> d = dst.AsSpan();
        for (int y = 0; y < h; y++)
            src.Slice(y * srcStride, w).CopyTo(d.Slice(y * dstStride, w));
    }

    private static int SuperblockControl(Av1EncoderOptions o, int threads)
    {
        switch (o.Superblock)
        {
            case Av1SuperblockSize.Size64: return AOM_SUPERBLOCK_SIZE_64X64;
            case Av1SuperblockSize.Size128: return AOM_SUPERBLOCK_SIZE_128X128;
            case Av1SuperblockSize.Dynamic: return AOM_SUPERBLOCK_SIZE_DYNAMIC;
        }
        long area = (long)o.Width * o.Height;
        return threads >= 4 && area >= 960 * 540 && area < 1920 * 1080
            ? AOM_SUPERBLOCK_SIZE_64X64
            : AOM_SUPERBLOCK_SIZE_DYNAMIC;
    }

    private void Control(int id, int value) => Check(aom_codec_control(_codec, id, value), $"aom_codec_control({id})");

    private void Check(aom_codec_err_t err, string what)
    {
        if (err == AOM_CODEC_OK) return;
        string detail = _codec.err_detail;
        throw new AomException(err, detail is null ? $"{what} failed: error {err}" : $"{what} failed: {detail}");
    }
}
