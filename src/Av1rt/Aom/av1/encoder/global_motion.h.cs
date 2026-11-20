// Port of libaom av1/encoder/global_motion.h
global using static Av1rt.Aom.global_motion;

namespace Av1rt.Aom;

// Global motion search is out of the subset (enable_global_motion is off for
// realtime). Only the multi-threading sync object embedded in MultiThreadInfo
// is ported.
internal static partial class global_motion
{
    internal const int RANSAC_NUM_MOTIONS = 1;
    internal const int GM_MAX_REFINEMENT_STEPS = 5;
    internal const int MAX_DIRECTIONS = 2;

    // [subset] GlobalMotionJobInfo, GlobalMotionData, and the
    // av1_compute_global_motion / av1_refine_integerized_param / ... prototypes.
}

internal sealed partial class AV1GlobalMotionSync
{
    // [subset] job_info (GlobalMotionJobInfo): global motion search only.

    // CONFIG_MULTITHREAD == 1:
    // Mutex lock used while dispatching jobs (C: pthread_mutex_t *, allocated).
    public pthread_mutex_t mutex_;

    // Initialized to false, set to true by the worker thread that encounters an
    // error in order to abort the processing of other worker threads.
    public bool gm_mt_exit;
}
