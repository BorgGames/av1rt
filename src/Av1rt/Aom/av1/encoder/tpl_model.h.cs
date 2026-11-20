// Port of libaom av1/encoder/tpl_model.h
global using static Av1rt.Aom.tpl_model;

namespace Av1rt.Aom;

// TPL is out of the subset (enable_tpl_model is off for realtime / lag 0).
// Only the multi-threading info embedded in MultiThreadInfo is ported.
internal static partial class tpl_model
{
    // [subset] MAX_TPL_FRAME_IDX, TPL_* constants, TplTxfmStats, TplBuffers,
    // TplDepStats, TplDepFrame, TplParams, VBR_RATECTRL_INFO and the tpl_model.c
    // prototypes.
}

internal sealed partial class AV1TplRowMultiThreadInfo
{
    // Initialized to false, set to true by the worker thread that encounters an
    // error in order to abort the processing of other worker threads.
    public bool tpl_mt_exit;
    // CONFIG_MULTITHREAD == 1:
    // Mutex lock object used for error handling (C: pthread_mutex_t *, allocated).
    public pthread_mutex_t mutex_;
    // [subset] sync_read_ptr / sync_write_ptr (AV1TplRowMultiThreadSync): TPL
    // row-mt only.
}
