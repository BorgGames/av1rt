// Port of libaom aom/aom_codec.h

global using static Av1rt.Aom.aom_codec;
global using aom_codec_err_t = int;
// [port] C `long` is 32 bits on the (MSVC, Windows x64) oracle build.
global using aom_codec_caps_t = int;
global using aom_codec_flags_t = int;
global using aom_codec_pts_t = long;
global using aom_codec_frame_flags_t = uint;
// [port] `typedef const void *aom_codec_iter_t`: the only iterator the encoder hands out is a
// pointer into the packet list (aom_codec_pkt_list_get), so it is a Ptr into that array.
// `aom_codec_iter_t iter = NULL` becomes `aom_codec_iter_t iter = default`.
global using aom_codec_iter_t = Av1rt.Aom.Ptr<Av1rt.Aom.aom_codec_cx_pkt_t>;
global using aom_bit_depth_t = int;
global using aom_superblock_size_t = int;
global using OBU_TYPE = int;
global using OBU_METADATA_TYPE = int;
global using aom_codec_ctx = Av1rt.Aom.aom_codec_ctx_t;

namespace Av1rt.Aom;

/*!\brief Codec context structure
 *
 * All codecs \ref MUST support this context structure fully. In general,
 * this data should be considered private to the codec algorithm, and
 * not be manipulated or examined by the calling application. Applications
 * may reference the 'name' member to get a printable description of the
 * algorithm.
 */
// [port] A class: the application owns it and every API call takes its address.
internal sealed partial class aom_codec_ctx_t
{
    public string name;                /*< Printable interface name */
    public aom_codec_iface_t iface;    /*< Interface pointers */
    public aom_codec_err_t err;        /*< Last returned error */
    public string err_detail;          /*< Detailed info, if available */
    public aom_codec_flags_t init_flags; /*< Flags passed at init time */
    public aom_codec_ctx_config config; /*< Configuration pointer aliasing union */
    public aom_codec_priv_t priv;      /*< Algorithm private storage */
}

// [port] The `config` union of aom_codec_ctx. Only the encoder member exists in the port: the
// decoder configuration (dec) and the untyped alias (raw) are never read by the encoder.
internal partial struct aom_codec_ctx_config
{
    public aom_codec_enc_cfg_t enc;
}

internal static partial class aom_codec
{
    /*!\brief Current ABI version number
     *
     * \internal
     * If this file is altered in any way that changes the ABI, this value
     * must be bumped.  Examples include, but are not limited to, changing
     * types, removing or reassigning enums, adding/removing/rearranging
     * fields to structures
     */
    internal const int AOM_CODEC_ABI_VERSION = (7 + AOM_IMAGE_ABI_VERSION); /*<\hideinitializer*/

    /*!\brief Algorithm return codes */
    // aom_codec_err_t
    /*!\brief Operation completed without error */
    internal const int AOM_CODEC_OK = 0;
    /*!\brief Unspecified error */
    internal const int AOM_CODEC_ERROR = 1;
    /*!\brief Memory operation failed */
    internal const int AOM_CODEC_MEM_ERROR = 2;
    /*!\brief ABI version mismatch */
    internal const int AOM_CODEC_ABI_MISMATCH = 3;
    /*!\brief Algorithm does not have required capability */
    internal const int AOM_CODEC_INCAPABLE = 4;
    /*!\brief The given bitstream is not supported. */
    internal const int AOM_CODEC_UNSUP_BITSTREAM = 5;
    /*!\brief Encoded bitstream uses an unsupported feature */
    internal const int AOM_CODEC_UNSUP_FEATURE = 6;
    /*!\brief The coded data for this stream is corrupt or incomplete */
    internal const int AOM_CODEC_CORRUPT_FRAME = 7;
    /*!\brief An application-supplied parameter is not valid. */
    internal const int AOM_CODEC_INVALID_PARAM = 8;
    /*!\brief An iterator reached the end of list. */
    internal const int AOM_CODEC_LIST_END = 9;

    /*! \brief Codec capabilities bitfield */
    internal const int AOM_CODEC_CAP_DECODER = 0x1; /*< Is a decoder */
    internal const int AOM_CODEC_CAP_ENCODER = 0x2; /*< Is an encoder */

    /*! \brief Initialization-time Feature Enabling */
    internal const int AOM_CODEC_USE_EXPERIMENTAL = 0x1; /*< Enables experimental features */

    /*!\brief Compressed Frame Flags */
    internal const uint AOM_FRAME_IS_KEY = 0x1u; /*< frame is the start of a GOP */
    /*!\brief frame can be dropped without affecting the stream (no future frame
     * depends on this one) */
    internal const uint AOM_FRAME_IS_DROPPABLE = 0x2u;
    /*!\brief this is an INTRA_ONLY frame */
    internal const uint AOM_FRAME_IS_INTRAONLY = 0x10u;
    /*!\brief this is an S-frame */
    internal const uint AOM_FRAME_IS_SWITCH = 0x20u;
    /*!\brief this is an error-resilient frame */
    internal const uint AOM_FRAME_IS_ERROR_RESILIENT = 0x40u;
    /*!\brief this is a key-frame dependent recovery-point frame */
    internal const uint AOM_FRAME_IS_DELAYED_RANDOM_ACCESS_POINT = 0x80u;

    /*!\brief Bit depth for codec
     * *
     * This enumeration determines the bit depth of the codec.
     */
    // aom_bit_depth_t
    internal const int AOM_BITS_8 = 8;   /*<  8 bits */
    internal const int AOM_BITS_10 = 10; /*< 10 bits */
    internal const int AOM_BITS_12 = 12; /*< 12 bits */

    /*!\brief Superblock size selection.
     *
     * Defines the superblock size used for encoding. The superblock size can
     * either be fixed at 64x64 or 128x128 pixels, or it can be dynamically
     * selected by the encoder for each frame.
     */
    // aom_superblock_size_t
    internal const int AOM_SUPERBLOCK_SIZE_64X64 = 0;   /*< Always use 64x64 superblocks. */
    internal const int AOM_SUPERBLOCK_SIZE_128X128 = 1; /*< Always use 128x128 superblocks. */
    internal const int AOM_SUPERBLOCK_SIZE_DYNAMIC = 2; /*< Select superblock size dynamically. */

    /*!\brief OBU types. */
    // OBU_TYPE
    internal const int OBU_SEQUENCE_HEADER = 1;
    internal const int OBU_TEMPORAL_DELIMITER = 2;
    internal const int OBU_FRAME_HEADER = 3;
    internal const int OBU_TILE_GROUP = 4;
    internal const int OBU_METADATA = 5;
    internal const int OBU_FRAME = 6;
    internal const int OBU_REDUNDANT_FRAME_HEADER = 7;
    internal const int OBU_TILE_LIST = 8;
    internal const int OBU_PADDING = 15;

    /*!\brief OBU metadata types. */
    // OBU_METADATA_TYPE
    internal const int OBU_METADATA_TYPE_AOM_RESERVED_0 = 0;
    internal const int OBU_METADATA_TYPE_HDR_CLL = 1;
    internal const int OBU_METADATA_TYPE_HDR_MDCV = 2;
    internal const int OBU_METADATA_TYPE_SCALABILITY = 3;
    internal const int OBU_METADATA_TYPE_ITUT_T35 = 4;
    internal const int OBU_METADATA_TYPE_TIMECODE = 5;

    // [port] The functions declared in aom_codec.h (aom_codec_version, aom_codec_err_to_string,
    // aom_codec_destroy, aom_codec_control, ...) are defined in aom/src/aom_codec.c and belong to
    // its port. AOM_CODEC_CONTROL_TYPECHECKED / AOM_CTRL_USE_TYPE generate C type checks only;
    // a control call is aom_codec_control(ctx, id, args) with the argument as written in C.
}
