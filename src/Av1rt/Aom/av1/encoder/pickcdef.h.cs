// Port of libaom av1/encoder/pickcdef.h
global using static Av1rt.Aom.pickcdef;
global using CDEF_CONTROL = int;

using System;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class pickcdef
{
    // CDEF_CONTROL
    internal const int CDEF_NONE = 0;      /* Disable CDEF on all frames. */
    internal const int CDEF_ALL = 1;       /* Enable CDEF for all frames. */
    internal const int CDEF_REFERENCE = 2; /* Disable CDEF on non reference frames. */
    internal const int CDEF_ADAPTIVE = 3;  /* Enable CDEF adaptively based on frame qindex */

    internal const int REDUCED_PRI_STRENGTHS_LVL1 = 8;
    internal const int REDUCED_PRI_STRENGTHS_LVL2 = 5;
    internal const int REDUCED_SEC_STRENGTHS_LVL3 = 2;
    internal const int REDUCED_SEC_STRENGTHS_LVL5 = 1;
    internal const int REDUCED_PRI_STRENGTHS_LVL4 = 2;

    internal const int REDUCED_TOTAL_STRENGTHS_LVL1 =
        (REDUCED_PRI_STRENGTHS_LVL1 * CDEF_SEC_STRENGTHS);
    internal const int REDUCED_TOTAL_STRENGTHS_LVL2 =
        (REDUCED_PRI_STRENGTHS_LVL2 * CDEF_SEC_STRENGTHS);
    internal const int REDUCED_TOTAL_STRENGTHS_LVL3 =
        (REDUCED_PRI_STRENGTHS_LVL2 * REDUCED_SEC_STRENGTHS_LVL3);
    internal const int REDUCED_TOTAL_STRENGTHS_LVL4 =
        (REDUCED_PRI_STRENGTHS_LVL4 * REDUCED_SEC_STRENGTHS_LVL3);
    internal const int REDUCED_TOTAL_STRENGTHS_LVL5 =
        (REDUCED_PRI_STRENGTHS_LVL4 * REDUCED_SEC_STRENGTHS_LVL5);
    internal const int TOTAL_STRENGTHS = (CDEF_PRI_STRENGTHS * CDEF_SEC_STRENGTHS);

    internal static readonly int[] priconv_lvl1 = { 0, 1, 2, 3, 5, 7, 10, 13 };
    internal static readonly int[] priconv_lvl2 = { 0, 2, 4, 8, 14 };
    internal static readonly int[] priconv_lvl4 = { 0, 11 };
    internal static readonly int[] priconv_lvl5 = { 0, 5 };
    internal static readonly int[] secconv_lvl3 = { 0, 2 };
    internal static readonly int[] secconv_lvl5 = { 0 };
    internal static readonly int[] nb_cdef_strengths = {
        TOTAL_STRENGTHS,
        REDUCED_TOTAL_STRENGTHS_LVL1,
        REDUCED_TOTAL_STRENGTHS_LVL2,
        REDUCED_TOTAL_STRENGTHS_LVL3,
        REDUCED_TOTAL_STRENGTHS_LVL4,
        REDUCED_TOTAL_STRENGTHS_LVL5,
        TOTAL_STRENGTHS
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int sb_all_skip(CommonModeInfoParams mi_params,
                                    int mi_row, int mi_col)
    {
        int maxr = Math.Min(mi_params.mi_rows - mi_row, MI_SIZE_64X64);
        int maxc = Math.Min(mi_params.mi_cols - mi_col, MI_SIZE_64X64);
        int stride = mi_params.mi_stride;
        Ptr<MB_MODE_INFO> mbmi = mi_params.mi_grid_base;
        mbmi += mi_row * stride + mi_col;
        for (int r = 0; r < maxr; ++r, mbmi += stride)
        {
            for (int c = 0; c < maxc; ++c)
            {
                if (mbmi[c].skip_txfm == 0) return 0;
            }
        }
        return 1;
    }

    // Checks if cdef processing can be skipped for particular sb.
    // Inputs:
    //   cdef_search_ctx: Pointer to the structure containing parameters related to
    //   CDEF search context.
    //   fbr: Row index in units of 64x64 block
    //   fbc: Column index in units of 64x64 block
    // Returns:
    //   1/0 will be returned to indicate skip/don't skip cdef processing of sb
    //   respectively.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int cdef_sb_skip(CommonModeInfoParams mi_params,
                                     int fbr, int fbc)
    {
        MB_MODE_INFO mbmi =
            mi_params.mi_grid_base[MI_SIZE_64X64 * fbr * mi_params.mi_stride +
                                   MI_SIZE_64X64 * fbc];
        // No filtering if the entire filter block is skipped.
        if (sb_all_skip(mi_params, fbr * MI_SIZE_64X64, fbc * MI_SIZE_64X64) != 0)
            return 1;
        // Skip odd numbered 64x64 block rows(cols) when bsize is BLOCK_128X128,
        // BLOCK_64X128(BLOCK_128X128, BLOCK_128X64) as for such blocks CDEF filtering
        // is done at the corresponding block sizes.
        if (((fbc & 1) != 0 &&
             (mbmi.bsize == BLOCK_128X128 || mbmi.bsize == BLOCK_128X64)) ||
            ((fbr & 1) != 0 &&
             (mbmi.bsize == BLOCK_128X128 || mbmi.bsize == BLOCK_64X128)))
            return 1;
        return 0;
    }
}

internal delegate void copy_fn_t(Ptr<ushort> dst, int dstride, Ptr<byte> src,
                                 int src_voffset, int src_hoffset, int sstride,
                                 int vsize, int hsize);

// PORT-NOTE: C `void *dst` is uint8_t* (lowbd) or uint16_t* (highbd); highbd is
// off in the subset, so it is Ptr<byte>.
internal delegate ulong compute_cdef_dist_t(Ptr<byte> dst, int dstride, Ptr<ushort> src,
                                            Ptr<cdef_list> dlist, int cdef_count,
                                            BLOCK_SIZE bsize, int coeff_shift,
                                            int row, int col);

/// <summary>CDEF search context.</summary>
internal sealed partial class CdefSearchCtx
{
    /// Pointer to the frame buffer holding the source frame
    public YV12_BUFFER_CONFIG @ref;
    /// Pointer to params related to MB_MODE_INFO arrays and related info
    public CommonModeInfoParams mi_params;
    /// Info specific to each plane
    public readonly macroblockd_plane[] plane = alloc_array<macroblockd_plane>(MAX_MB_PLANE);
    /// Function pointer of copy_fn
    public copy_fn_t copy_fn;
    /// Function pointer of compute_cdef_dist_fn
    public compute_cdef_dist_t compute_cdef_dist_fn;
    ///  Number of strenghts evaluated in CDEF filter search
    public int total_strengths;
    /// Bit-depth dependent shift
    public int coeff_shift;
    /// CDEF damping factor
    public int damping;
    /// Search method used to select CDEF parameters
    public int pick_method;
    /// Number of planes
    public int num_planes;
    /// Log2 of width of the MI unit in pixels. mi_wide_l2[i]
    /// indicates the width of the MI unit in pixels for the ith plane
    public readonly int[] mi_wide_l2 = new int[MAX_MB_PLANE];
    /// Log2 of height of the MI unit in pixels. mi_high_l2[i]
    /// indicates the height of the MI unit in pixels for the ith plane
    public readonly int[] mi_high_l2 = new int[MAX_MB_PLANE];
    /// Subsampling in x direction. xdec[i] indicates the subsampling
    /// for the ith plane
    public readonly int[] xdec = new int[MAX_MB_PLANE];
    /// Subsampling in y direction. ydec[i] indicates the subsampling
    /// for the ith plane
    public readonly int[] ydec = new int[MAX_MB_PLANE];
    /// bsize[i] indicates the block size of ith plane
    public readonly int[] bsize = new int[MAX_MB_PLANE];
    /// Number of 64x64 blocks in vertical direction of a frame
    public int nvfb;
    /// Number of 64x64 blocks in horizontal direction of a frame
    public int nhfb;
    /// Pointer to the mean squared error between the CDEF filtered block and the
    /// source block. mse[i][j][k] stores the MSE of the ith plane (i=0 corresponds
    /// to Y-plane, i=1 corresponds to U and V planes), jth block and kth strength
    /// index
    /// PORT-NOTE: C `uint64_t (*mse[2])[TOTAL_STRENGTHS]` (two pointers to
    /// row arrays) is jagged: mse[pli] = new ulong[nvfb * nhfb][], each row
    /// new ulong[TOTAL_STRENGTHS]. pickcdef.c only ever indexes it as
    /// mse[pli][sb][strength] or passes mse[pli] whole.
    public readonly ulong[][][] mse = new ulong[2][][];
    /// Holds the position (in units of mi's) of the cdef filtered
    /// block in raster scan order
    public Ptr<int> sb_index;
    /// Holds the count of cdef filtered blocks
    public int sb_count;
    /// Indicates if 16bit frame buffers are to be used i.e., the content bit-depth
    /// is > 8-bit
    public bool use_highbitdepth;
}
