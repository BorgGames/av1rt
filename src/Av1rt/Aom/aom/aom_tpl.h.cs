// Port of libaom aom/aom_tpl.h

global using static Av1rt.Aom.aom_tpl;

using System.Runtime.InteropServices;

namespace Av1rt.Aom;

/*!\file
 * \brief Describes the TPL stats descriptor and associated operations
 *
 */

// [port] These types only reach the encoder through AV1_COMP.extrc_tpl_gop_stats and the
// external rate control interface, neither of which carries data in the subset. They are value
// structs (small, copied by value or held in arrays); the C pointers to their arrays are Ptr.

/*!\brief Full-pel motion vector, in units of full pixels */
internal partial struct AOM_FULLPEL_MV
{
    public short row; /*< motion vector row */
    public short col; /*< motion vector col */
}

/*!\brief Motion vector in units of 1/8-pel */
internal partial struct AOM_MV
{
    public short row; /*< motion vector row */
    public short col; /*< motion vector col */
}

/*!\brief Motion vector union */
// [port] A C union read under different members: explicit layout (PORTING 3.3).
[StructLayout(LayoutKind.Explicit)]
internal partial struct aom_int_mv
{
    [FieldOffset(0)] public uint as_int;                 /*< int representation */
    [FieldOffset(0)] public AOM_MV as_mv;                /*< 1/8-pel representation */
    [FieldOffset(0)] public AOM_FULLPEL_MV as_fullmv;    /*< full-pel representation */
} /* facilitates faster equality tests and copies */

/*!\brief Temporal dependency model stats for each block before propagation */
internal partial struct AomTplBlockStats
{
    public short row;                 /*< Pixel row of the top left corner */
    public short col;                 /*< Pixel col of the top left corner */
    public long srcrf_sse;            /*< Source sse */
    public long srcrf_dist;           /*< Source distortion */
    public long recrf_sse;            /*< Reconstructed sse */
    public long recrf_dist;           /*< Reconstructed distortion */
    public long intra_sse;            /*< Intra sse */
    public long intra_dist;           /*< Intra distortion */
    public Arr2<long> cmp_recrf_dist; /*< Compound distortion */
    public long mc_dep_rate;          /*< Motion compensated dependency rate */
    public long mc_dep_dist;          /*< Motion compensated dependency distortion */
    public Arr7<long> pred_error;     /*< Prediction error */
    public int intra_cost;            /*< Intra cost */
    public int inter_cost;            /*< Inter cost */
    public int srcrf_rate;            /*< Source rate */
    public int recrf_rate;            /*< Reconstructed rate */
    public int intra_rate;            /*< Intra rate */
    public Arr2<int> cmp_recrf_rate;  /*< Compound rate */
    public Arr7<aom_int_mv> mv;       /*< MV */
    public Arr2<sbyte> ref_frame_index; /*< Ref frame index in the ref frame buffer */
}

/*!\brief Temporal dependency model stats for each frame before propagation */
internal partial struct AomTplFrameStats
{
    public int frame_width;  /*< Frame width */
    public int frame_height; /*< Frame height */
    public int num_blocks;   /*< Number of blocks. Size of block_stats_list */
    public Ptr<AomTplBlockStats> block_stats_list; /*< List of tpl stats for each block */
}

/*!\brief Temporal dependency model stats for each GOP before propagation */
internal partial struct AomTplGopStats
{
    public int size; /*< GOP size, also the size of frame_stats_list. */
    public Ptr<AomTplFrameStats> frame_stats_list; /*< List of tpl stats for each frame */
}

internal static partial class aom_tpl
{
    /*!\brief Current ABI version number
     *
     * \internal
     * If this file is altered in any way that changes the ABI, this value
     * must be bumped. Examples include, but are not limited to, changing
     * types, removing or reassigning enums, adding/removing/rearranging
     * fields to structures.
     */
    internal const int AOM_TPL_ABI_VERSION = 1; /*<\hideinitializer*/

    /*!\brief Number of inter reference frames per frame */
    internal const int AOM_RC_INTER_REFS_PER_FRAME = 7;
}
