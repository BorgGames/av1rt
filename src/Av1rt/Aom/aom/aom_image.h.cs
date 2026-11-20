// Port of libaom aom/aom_image.h

global using static Av1rt.Aom.aom_image;
global using aom_img_fmt_t = int;
global using aom_color_primaries_t = int;
global using aom_transfer_characteristics_t = int;
global using aom_matrix_coefficients_t = int;
global using aom_color_range_t = int;
global using aom_chroma_sample_position_t = int;
global using aom_metadata_insert_flags_t = int;

namespace Av1rt.Aom;

/*!\brief Describes the image metadata */
// [port] A class: it is only handled through pointers (aom_metadata_t *).
internal sealed partial class aom_metadata_t
{
    public uint type;                                 /*< Metadata type */
    public Ptr<byte> payload;                         /*< Metadata payload data */
    public int sz;                                    /*< Metadata payload size */
    public aom_metadata_insert_flags_t insert_flag;   /*< Metadata insertion flag */
}

/*\brief Image Descriptor */
// [port] A class: the encoder only sees it through pointers, and aom_img_alloc either fills a
// caller's image (memset to 0 first, see Clear) or allocates one.
internal sealed partial class aom_image_t
{
    public aom_img_fmt_t fmt;                 /*< Image Format */
    public aom_color_primaries_t cp;          /*< CICP Color Primaries */
    public aom_transfer_characteristics_t tc; /*< CICP Transfer Characteristics */
    public aom_matrix_coefficients_t mc;      /*< CICP Matrix Coefficients */
    public int monochrome;                    /*< Whether image is monochrome */
    public aom_chroma_sample_position_t csp;  /*< chroma sample position */
    public aom_color_range_t range;           /*< Color Range */

    /* Image storage dimensions */
    public uint w;         /*< Stored image width */
    public uint h;         /*< Stored image height */
    public uint bit_depth; /*< Stored image bit-depth */

    /* Image display dimensions */
    public uint d_w; /*< Displayed image width */
    public uint d_h; /*< Displayed image height */

    /* Image intended rendering dimensions */
    public uint r_w; /*< Intended rendering image width */
    public uint r_h; /*< Intended rendering image height */

    /* Chroma subsampling info */
    public uint x_chroma_shift; /*< subsampling order, X */
    public uint y_chroma_shift; /*< subsampling order, Y */

    /* Image data pointers. */
    public Arr3<Ptr<byte>> planes; /*< pointer to the top left pixel for each plane */
    public Arr3<int> stride;       /*< stride between rows for each plane */
    public int sz;                 /*< data size */

    public int bps; /*< bits per sample (for packed formats) */

    public int temporal_id; /*< Temporal layer Id of image */
    public int spatial_id;  /*< Spatial layer Id of image */

    /*!\brief The following member may be set by the application to associate
     * data with this image.
     */
    public object user_priv;

    /* The following members should be treated as private. */
    public Ptr<byte> img_data; /*< private */
    public int img_data_owner; /*< private */
    public int self_allocd;    /*< private */

    public aom_metadata_array_t metadata; /*< Metadata payloads associated with the image. */

    public object fb_priv; /*< Frame buffer data associated with the image. */

    // memset(img, 0, sizeof(aom_image_t))
    public void Clear()
    {
        fmt = 0; cp = 0; tc = 0; mc = 0; monochrome = 0; csp = 0; range = 0;
        w = 0; h = 0; bit_depth = 0; d_w = 0; d_h = 0; r_w = 0; r_h = 0;
        x_chroma_shift = 0; y_chroma_shift = 0;
        planes = default; stride = default; sz = 0; bps = 0;
        temporal_id = 0; spatial_id = 0; user_priv = null;
        img_data = default; img_data_owner = 0; self_allocd = 0;
        metadata = null; fb_priv = null;
    }
}

internal static partial class aom_image
{
    /*!\brief Current ABI version number
     *
     * \internal
     * If this file is altered in any way that changes the ABI, this value
     * must be bumped.  Examples include, but are not limited to, changing
     * types, removing or reassigning enums, adding/removing/rearranging
     * fields to structures
     */
    internal const int AOM_IMAGE_ABI_VERSION = (9); /*<\hideinitializer*/

    internal const int AOM_IMG_FMT_PLANAR = 0x100;  /*< Image is a planar format. */
    internal const int AOM_IMG_FMT_UV_FLIP = 0x200; /*< V plane precedes U in memory. */
    /* 0x400 used to signal alpha channel, skipping for backwards compatibility. */
    internal const int AOM_IMG_FMT_HIGHBITDEPTH = 0x800; /*< Image uses 16bit framebuffer. */

    /*!\brief List of supported image formats */
    // aom_img_fmt_t
    internal const int AOM_IMG_FMT_NONE = 0;
    internal const int AOM_IMG_FMT_YV12 = AOM_IMG_FMT_PLANAR | AOM_IMG_FMT_UV_FLIP | 1; /*< planar YVU */
    internal const int AOM_IMG_FMT_I420 = AOM_IMG_FMT_PLANAR | 2;
    internal const int AOM_IMG_FMT_AOMYV12 = AOM_IMG_FMT_PLANAR | AOM_IMG_FMT_UV_FLIP | 3;
    internal const int AOM_IMG_FMT_AOMI420 = AOM_IMG_FMT_PLANAR | 4;
    internal const int AOM_IMG_FMT_I422 = AOM_IMG_FMT_PLANAR | 5;
    internal const int AOM_IMG_FMT_I444 = AOM_IMG_FMT_PLANAR | 6;
    internal const int AOM_HAVE_IMG_FMT_NV12 = 1;
    internal const int AOM_IMG_FMT_NV12 = AOM_IMG_FMT_PLANAR | 7; /*< 4:2:0 with U and V interleaved */
    internal const int AOM_IMG_FMT_I42016 = AOM_IMG_FMT_I420 | AOM_IMG_FMT_HIGHBITDEPTH;
    internal const int AOM_IMG_FMT_YV1216 = AOM_IMG_FMT_YV12 | AOM_IMG_FMT_HIGHBITDEPTH;
    internal const int AOM_IMG_FMT_I42216 = AOM_IMG_FMT_I422 | AOM_IMG_FMT_HIGHBITDEPTH;
    internal const int AOM_IMG_FMT_I44416 = AOM_IMG_FMT_I444 | AOM_IMG_FMT_HIGHBITDEPTH;

    /*!\brief List of supported color primaries */
    // aom_color_primaries_t
    internal const int AOM_CICP_CP_RESERVED_0 = 0;  /*< For future use */
    internal const int AOM_CICP_CP_BT_709 = 1;      /*< BT.709 */
    internal const int AOM_CICP_CP_UNSPECIFIED = 2; /*< Unspecified */
    internal const int AOM_CICP_CP_RESERVED_3 = 3;  /*< For future use */
    internal const int AOM_CICP_CP_BT_470_M = 4;    /*< BT.470 System M (historical) */
    internal const int AOM_CICP_CP_BT_470_B_G = 5;  /*< BT.470 System B, G (historical) */
    internal const int AOM_CICP_CP_BT_601 = 6;      /*< BT.601 */
    internal const int AOM_CICP_CP_SMPTE_240 = 7;   /*< SMPTE 240 */
    internal const int AOM_CICP_CP_GENERIC_FILM = 8; /*< Generic film (color filters using illuminant C) */
    internal const int AOM_CICP_CP_BT_2020 = 9;      /*< BT.2020, BT.2100 */
    internal const int AOM_CICP_CP_XYZ = 10;         /*< SMPTE 428 (CIE 1921 XYZ) */
    internal const int AOM_CICP_CP_SMPTE_431 = 11;   /*< SMPTE RP 431-2 */
    internal const int AOM_CICP_CP_SMPTE_432 = 12;   /*< SMPTE EG 432-1  */
    internal const int AOM_CICP_CP_RESERVED_13 = 13; /*< For future use (values 13 - 21)  */
    internal const int AOM_CICP_CP_EBU_3213 = 22;    /*< EBU Tech. 3213-E  */
    internal const int AOM_CICP_CP_RESERVED_23 = 23; /*< For future use (values 23 - 255)  */

    /*!\brief List of supported transfer functions */
    // aom_transfer_characteristics_t
    internal const int AOM_CICP_TC_RESERVED_0 = 0;  /*< For future use */
    internal const int AOM_CICP_TC_BT_709 = 1;      /*< BT.709 */
    internal const int AOM_CICP_TC_UNSPECIFIED = 2; /*< Unspecified */
    internal const int AOM_CICP_TC_RESERVED_3 = 3;  /*< For future use */
    internal const int AOM_CICP_TC_BT_470_M = 4;    /*< BT.470 System M (historical)  */
    internal const int AOM_CICP_TC_BT_470_B_G = 5;  /*< BT.470 System B, G (historical) */
    internal const int AOM_CICP_TC_BT_601 = 6;      /*< BT.601 */
    internal const int AOM_CICP_TC_SMPTE_240 = 7;   /*< SMPTE 240 M */
    internal const int AOM_CICP_TC_LINEAR = 8;      /*< Linear */
    internal const int AOM_CICP_TC_LOG_100 = 9;     /*< Logarithmic (100 : 1 range) */
    internal const int AOM_CICP_TC_LOG_100_SQRT10 = 10; /*< Logarithmic (100 * Sqrt(10) : 1 range) */
    internal const int AOM_CICP_TC_IEC_61966 = 11; /*< IEC 61966-2-4 */
    internal const int AOM_CICP_TC_BT_1361 = 12;   /*< BT.1361 */
    internal const int AOM_CICP_TC_SRGB = 13;      /*< sRGB or sYCC*/
    internal const int AOM_CICP_TC_BT_2020_10_BIT = 14; /*< BT.2020 10-bit systems */
    internal const int AOM_CICP_TC_BT_2020_12_BIT = 15; /*< BT.2020 12-bit systems */
    internal const int AOM_CICP_TC_SMPTE_2084 = 16;     /*< SMPTE ST 2084, ITU BT.2100 PQ */
    internal const int AOM_CICP_TC_SMPTE_428 = 17;      /*< SMPTE ST 428 */
    internal const int AOM_CICP_TC_HLG = 18;            /*< BT.2100 HLG, ARIB STD-B67 */
    internal const int AOM_CICP_TC_RESERVED_19 = 19;    /*< For future use (values 19-255) */

    /*!\brief List of supported matrix coefficients */
    // aom_matrix_coefficients_t
    internal const int AOM_CICP_MC_IDENTITY = 0;    /*< Identity matrix */
    internal const int AOM_CICP_MC_BT_709 = 1;      /*< BT.709 */
    internal const int AOM_CICP_MC_UNSPECIFIED = 2; /*< Unspecified */
    internal const int AOM_CICP_MC_RESERVED_3 = 3;  /*< For future use */
    internal const int AOM_CICP_MC_FCC = 4;         /*< US FCC 73.628 */
    internal const int AOM_CICP_MC_BT_470_B_G = 5;  /*< BT.470 System B, G (historical) */
    internal const int AOM_CICP_MC_BT_601 = 6;      /*< BT.601 */
    internal const int AOM_CICP_MC_SMPTE_240 = 7;   /*< SMPTE 240 M */
    internal const int AOM_CICP_MC_SMPTE_YCGCO = 8; /*< YCgCo */
    internal const int AOM_CICP_MC_BT_2020_NCL = 9; /*< BT.2020 non-constant luminance, BT.2100 YCbCr  */
    internal const int AOM_CICP_MC_BT_2020_CL = 10; /*< BT.2020 constant luminance */
    internal const int AOM_CICP_MC_SMPTE_2085 = 11; /*< SMPTE ST 2085 YDzDx */
    internal const int AOM_CICP_MC_CHROMAT_NCL = 12; /*< Chromaticity-derived non-constant luminance */
    internal const int AOM_CICP_MC_CHROMAT_CL = 13;  /*< Chromaticity-derived constant luminance */
    internal const int AOM_CICP_MC_ICTCP = 14;       /*< BT.2100 ICtCp */
    internal const int AOM_CICP_MC_RESERVED_15 = 15; /*< For backward compatibility */
    internal const int AOM_CICP_MC_IPT_C2 = 15;      /*< IPT-C2 */
    internal const int AOM_CICP_MC_YCGCO_RE = 16;    /*< YCgCo-Re */
    internal const int AOM_CICP_MC_YCGCO_RO = 17;    /*< YCgCo-Ro */

    /*!\brief List of supported color range */
    // aom_color_range_t
    internal const int AOM_CR_STUDIO_RANGE = 0; /*<- Y  [16..235],  UV  [16..240]  (bit depth 8) */
    internal const int AOM_CR_FULL_RANGE = 1;   /*<- YUV/RGB [0..255]  (bit depth 8) */

    /*!\brief List of chroma sample positions */
    // aom_chroma_sample_position_t
    internal const int AOM_CSP_UNKNOWN = 0;   /*< Unknown */
    internal const int AOM_CSP_VERTICAL = 1;  /*< Horizontally co-located with luma(0, 0)*/
    /*< sample, between two vertical samples */
    internal const int AOM_CSP_COLOCATED = 2; /*< Co-located with luma(0, 0) sample */
    internal const int AOM_CSP_RESERVED = 3;  /*< Reserved value */

    /*!\brief List of insert flags for Metadata
     *
     * These flags control how the library treats metadata during encode.
     *
     * While encoding, when metadata is added to an aom_image via
     * aom_img_add_metadata(), the flag passed along with the metadata will
     * determine where the metadata OBU will be placed in the encoded OBU stream.
     * Metadata will be emitted into the output stream within the next temporal
     * unit if it satisfies the specified insertion flag.
     *
     * During decoding, when the library encounters a metadata OBU, it is always
     * flagged as AOM_MIF_ANY_FRAME and emitted with the next output aom_image.
     */
    // aom_metadata_insert_flags_t
    internal const int AOM_MIF_NON_KEY_FRAME = 0; /*< Adds metadata if it's not a keyframe */
    internal const int AOM_MIF_KEY_FRAME = 1;     /*< Adds metadata only if it's a keyframe */
    internal const int AOM_MIF_ANY_FRAME = 2;     /*< Adds metadata to any type of frame */
    internal const int AOM_MIF_NON_KEY_FRAME_LAYER_SPECIFIC = 16;
    internal const int AOM_MIF_KEY_FRAME_LAYER_SPECIFIC = 17;
    internal const int AOM_MIF_ANY_FRAME_LAYER_SPECIFIC = 18;

    internal const int AOM_PLANE_PACKED = 0; /*< To be used for all packed formats */
    internal const int AOM_PLANE_Y = 0;      /*< Y (Luminance) plane */
    internal const int AOM_PLANE_U = 1;      /*< U (Chroma) plane */
    internal const int AOM_PLANE_V = 2;      /*< V (Chroma) plane */
}
