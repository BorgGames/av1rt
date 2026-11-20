// Port of libaom aom_dsp/flow_estimation/flow_estimation.h

global using static Av1rt.Aom.flow_estimation;
global using TransformationType = int;
global using GlobalMotionMethod = int;

namespace Av1rt.Aom;

// [port] A class: GlobalMotionData embeds an array of them (motion_models[RANSAC_NUM_MOTIONS])
// and the code works through pointers to the elements; it is never copied by value. The owner
// of the array must allocate each element. params has the C length MAX_PARAMDIM.
internal sealed partial class MotionModel
{
    public readonly double[] @params = new double[MAX_PARAMDIM];
    public Ptr<int> inliers;
    public int num_inliers;
}

// Data structure to store a single correspondence point during global
// motion search.
//
// A correspondence (x, y) -> (rx, ry) means that point (x, y) in the
// source frame corresponds to point (rx, ry) in the ref frame.
internal partial struct Correspondence
{
    public double x, y;
    public double rx, ry;
}

// [subset] The flow estimation functions (aom_compute_global_motion and friends, kIdentityParams
// in flow_estimation.c) belong to the global motion search, which the realtime subset does not
// run; only the types and constants that in-scope headers name are ported.
internal static partial class flow_estimation
{
    internal const int MAX_PARAMDIM = 6;
    internal const double MIN_INLIER_PROB = 0.1;

    /* clang-format off */
    // TransformationType
    internal const int IDENTITY = 0;      // identity transformation, 0-parameter
    internal const int TRANSLATION = 1;   // translational motion 2-parameter
    internal const int ROTZOOM = 2;       // simplified affine with rotation + zoom only, 4-parameter
    internal const int AFFINE = 3;        // affine, 6-parameter
    internal const int TRANS_TYPES = 4;
    /* clang-format on */

    // number of parameters used by each transformation in TransformationTypes
    internal static readonly int[] trans_model_params = { 0, 2, 4, 6 };

    // Available methods which can be used for global motion estimation
    // GlobalMotionMethod
    internal const int GLOBAL_MOTION_METHOD_FEATURE_MATCH = 0;
    internal const int GLOBAL_MOTION_METHOD_DISFLOW = 1;
    internal const int GLOBAL_MOTION_METHOD_LAST = GLOBAL_MOTION_METHOD_DISFLOW;
    internal const int GLOBAL_MOTION_METHODS = 2;

    // Which global motion method should we use in practice?
    // Disflow is both faster and gives better results than feature matching in
    // practically all cases, so we use disflow by default
    internal const GlobalMotionMethod default_global_motion_method = GLOBAL_MOTION_METHOD_DISFLOW;
}
