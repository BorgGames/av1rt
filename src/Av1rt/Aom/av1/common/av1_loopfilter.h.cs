// Port of libaom av1/common/av1_loopfilter.h

// <gen:aliases>
global using lf_path = int;
global using EDGE_DIR = int;
// </gen:aliases>
global using LoopFilterWorkerData = Av1rt.Aom.LFWorkerData;

global using static Av1rt.Aom.av1_loopfilter;

namespace Av1rt.Aom;

internal partial struct FilterMask
{
    public Arr4<ulong> bits;
}

// [port] Class with CopyFrom: embedded in AV1_COMMON (cm->lf), copied whole by the encoder
// (`cc->lf = cm->lf` in the coding-context save/restore) and its arrays are passed as
// pointers (av1_set_default_ref_deltas(cm->lf.ref_deltas)).
internal sealed partial class loopfilter
{
    public readonly int[] filter_level = new int[2];
    public int filter_level_u;
    public int filter_level_v;

    public readonly int[] backup_filter_level = new int[2];
    public int backup_filter_level_u;
    public int backup_filter_level_v;

    public int sharpness_level;

    public byte mode_ref_delta_enabled;
    public byte mode_ref_delta_update;

    // 0 = Intra, Last, Last2+Last3,
    // GF, BRF, ARF2, ARF
    public readonly sbyte[] ref_deltas = new sbyte[REF_FRAMES];

    // 0 = ZERO_MV, MV
    public readonly sbyte[] mode_deltas = new sbyte[MAX_MODE_LF_DELTAS];

    /// <summary>C struct assignment: deep copy of every field.</summary>
    public void CopyFrom(loopfilter other)
    {
        av1_copy(filter_level, other.filter_level);
        filter_level_u = other.filter_level_u;
        filter_level_v = other.filter_level_v;
        av1_copy(backup_filter_level, other.backup_filter_level);
        backup_filter_level_u = other.backup_filter_level_u;
        backup_filter_level_v = other.backup_filter_level_v;
        sharpness_level = other.sharpness_level;
        mode_ref_delta_enabled = other.mode_ref_delta_enabled;
        mode_ref_delta_update = other.mode_ref_delta_update;
        av1_copy(ref_deltas, other.ref_deltas);
        av1_copy(mode_deltas, other.mode_deltas);
    }
}

// Need to align this structure so when it is declared and
// passed it can be loaded into vector registers.
// [port] Class: the arrays are passed to the aom_lpf_* functions as `const uint8_t *`, which
// needs Ptr<byte>; instances live in loop_filter_info_n.lfthr and are referenced by
// AV1_DEBLOCKING_PARAMETERS.lfthr.
internal sealed partial class loop_filter_thresh
{
    public readonly byte[] mblim = new byte[SIMD_WIDTH];
    public readonly byte[] lim = new byte[SIMD_WIDTH];
    public readonly byte[] hev_thr = new byte[SIMD_WIDTH];
}

internal sealed partial class loop_filter_info_n
{
    public readonly loop_filter_thresh[] lfthr = new_array<loop_filter_thresh>(MAX_LOOP_FILTER + 1);
    public readonly byte[][][][][] lvl =
        new_jagged<byte>(MAX_MB_PLANE, MAX_SEGMENTS, 2, REF_FRAMES, MAX_MODE_LF_DELTAS);
}

internal partial struct AV1_DEBLOCKING_PARAMETERS
{
    // length of the filter applied to the outer edge
    public byte filter_length;
    // deblocking limits
    public loop_filter_thresh lfthr;
}

internal sealed partial class LFWorkerData
{
    public YV12_BUFFER_CONFIG frame_buffer;
    public AV1_COMMON cm;
    public readonly macroblockd_plane[] planes = new_array<macroblockd_plane>(MAX_MB_PLANE);
    // TODO(Ranjit): When the filter functions are modified to use xd->lossless
    // add lossless as a member here.
    public MACROBLOCKD xd;

    public readonly AV1_DEBLOCKING_PARAMETERS[] params_buf = new AV1_DEBLOCKING_PARAMETERS[MAX_MIB_SIZE];
    public readonly TX_SIZE[] tx_buf = new TX_SIZE[MAX_MIB_SIZE];
    public aom_internal_error_info error_info = new();
}

internal static partial class av1_loopfilter
{
    // <gen:consts>
    internal const int MAX_LOOP_FILTER = 63;
    internal const int MAX_SHARPNESS = 7;
    internal const int SIMD_WIDTH = 16;

    // lf_path
    internal const int LF_PATH_420 = 0;
    internal const int LF_PATH_444 = 1;
    internal const int LF_PATH_SLOW = 2;

    // EDGE_DIR
    internal const int VERT_EDGE = 0;
    internal const int HORZ_EDGE = 1;
    internal const int NUM_EDGE_DIRS = 2;
    // </gen:consts>
}
