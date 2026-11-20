// Port of libaom aom/aom_frame_buffer.h

global using static Av1rt.Aom.aom_frame_buffer;

namespace Av1rt.Aom;

/*!\brief External frame buffer
 *
 * This structure holds allocated frame buffers used by the decoder.
 */
// [port] A class: it is only handled through pointers, and those may be NULL
// (aom_realloc_frame_buffer(..., fb = NULL, cb = NULL, ...)).
internal sealed partial class aom_codec_frame_buffer_t
{
    public Ptr<byte> data; /*< Pointer to the data buffer */
    public int size;       /*< Size of data in bytes */
    public object priv;    /*< Frame's private data */
}

/*!\brief get frame buffer callback prototype
 *
 * This callback is invoked by the decoder to retrieve data for the frame
 * buffer in order for the decode call to complete. The callback must
 * allocate at least min_size in bytes and assign it to fb->data. The callback
 * must zero out all the data allocated. Then the callback must set fb->size
 * to the allocated size. The application does not need to align the
 * allocated data. The callback is triggered when the decoder needs a frame
 * buffer to decode a compressed image into. This function may be called
 * more than once for every call to aom_codec_decode. The application may set
 * fb->priv to some data which will be passed back in the aom_image_t and the
 * release function call. |fb| is guaranteed to not be NULL. On success the
 * callback must return 0. Any failure the callback must return a value less
 * than 0.
 */
internal delegate int aom_get_frame_buffer_cb_fn_t(object priv, int min_size, aom_codec_frame_buffer_t fb);

/*!\brief release frame buffer callback prototype
 *
 * This callback is invoked by the decoder when the frame buffer is not
 * referenced by any other buffers. |fb| is guaranteed to not be NULL. On
 * success the callback must return 0. Any failure the callback must return
 * a value less than 0.
 */
internal delegate int aom_release_frame_buffer_cb_fn_t(object priv, aom_codec_frame_buffer_t fb);

internal static partial class aom_frame_buffer
{
    /*!\brief The maximum number of work buffers used by libaom.
     *  Support maximum 4 threads to decode video in parallel.
     *  Each thread will use one work buffer.
     * TODO(hkuang): Add support to set number of worker threads dynamically.
     */
    internal const int AOM_MAXIMUM_WORK_BUFFERS = 8;

    /*!\brief The maximum number of reference buffers that a AV1 encoder may use.
     */
    internal const int AOM_MAXIMUM_REF_BUFFERS = 8;
}
