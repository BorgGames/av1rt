// Port of libaom av1/encoder/lookahead.h
global using static Av1rt.Aom.lookahead;
global using COMPRESSOR_STAGE = int;

namespace Av1rt.Aom;

internal static partial class lookahead
{
    internal const int MAX_LAG_BUFFERS = 48;
    internal const int MAX_LAP_BUFFERS = 48;
    internal const int MAX_TOTAL_BUFFERS = (MAX_LAG_BUFFERS + MAX_LAP_BUFFERS);
    internal const int LAP_LAG_IN_FRAMES = 17;

    // The max of past frames we want to keep in the queue.
    internal const int MAX_PRE_FRAMES = 1;

    // COMPRESSOR_STAGE
    internal const int ENCODE_STAGE = 0;
    internal const int LAP_STAGE = 1;
    internal const int MAX_STAGES = 2;
}

internal sealed partial class lookahead_entry
{
    public YV12_BUFFER_CONFIG img = new();
    public long ts_start;
    public long ts_end;
    public int display_idx;
    public aom_enc_frame_flags_t flags;
}

internal partial struct read_ctx
{
    public int sz;       /* Number of buffers currently in the queue */
    public int read_idx; /* Read index */
    public int pop_sz;   /* Size to check for pop condition */
    public int valid;    /* Is this ctx valid? */
}

internal sealed partial class lookahead_ctx
{
    public int max_sz;                                       /* Absolute size of the queue */
    public int write_idx;                                    /* Write index */
    public readonly read_ctx[] read_ctxs = new read_ctx[MAX_STAGES]; /* Read context */
    public lookahead_entry[] buf;                            /* Buffer list */
    public int push_frame_count; /* Number of frames that have been pushed in the queue*/
    public byte max_pre_frames;  /* Maximum number of past frames allowed in the queue */
    // [port] Not in C: threads av1_lookahead_push may use for the source copy (set by the
    // encoder from max_threads; the copy is memory bound and on the critical path).
    public int copy_threads = 1;
}
