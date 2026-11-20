// Port of libaom aom/aom.h

global using static Av1rt.Aom.aom;
global using aom_com_control_id = int;

namespace Av1rt.Aom;

/*!\brief AV1 specific reference frame data struct
 *
 * Define the data struct to access av1 reference frames.
 */
// [port] A class: it is a control payload read with va_arg(args, av1_ref_frame_t *) (see the
// note in aomcx.h.cs). The embedded image is allocated with it.
internal sealed partial class av1_ref_frame_t
{
    public int idx;              /*< frame index to get (input) */
    public int use_external_ref; /*< Directly use external ref buffer(decoder only) */
    public readonly aom_image_t img = new(); /*< img structure to populate (output) */
}

internal static partial class aom
{
    /*!\brief Control functions
     *
     * The set of macros define the control functions of AOM interface
     * The range for common control IDs is 230-255(max).
     */
    // aom_com_control_id
    /*!\brief Codec control function to get a pointer to a reference frame
     *
     * av1_ref_frame_t* parameter
     */
    internal const int AV1_GET_REFERENCE = 230;

    /*!\brief Codec control function to write a frame into a reference buffer
     *
     * av1_ref_frame_t* parameter
     */
    internal const int AV1_SET_REFERENCE = 231;

    /*!\brief Codec control function to get a copy of reference frame from the
     * decoder
     *
     * av1_ref_frame_t* parameter
     */
    internal const int AV1_COPY_REFERENCE = 232;

    /*!\brief Codec control function to get a pointer to the new frame
     *
     * aom_image_t* parameter
     */
    internal const int AV1_GET_NEW_FRAME_IMAGE = 233;

    /*!\brief Codec control function to copy the new frame to an external buffer
     *
     * aom_image_t* parameter
     */
    internal const int AV1_COPY_NEW_FRAME_IMAGE = 234;

    /*!\brief Start point of control IDs for aom_dec_control_id.
     * Any new common control IDs should be added above.
     */
    internal const int AOM_DECODER_CTRL_ID_START = 256;
    // No common control IDs should be added after AOM_DECODER_CTRL_ID_START.
}
