// Port of libaom aom/internal/aom_image_internal.h

global using static Av1rt.Aom.aom_image_internal;

namespace Av1rt.Aom;

/*!\brief Array of aom_metadata structs for an image. */
// [port] A class: only handled through pointers (aom_metadata_array_t *).
internal sealed partial class aom_metadata_array_t
{
    public int sz;                          /* Number of metadata structs in the list */
    public aom_metadata_t[] metadata_array; /* Array of metadata structs */
}

/*!\brief Allocate memory for aom_image.
 *
 * Callback that allocates memory for an aom_image's frame buffer.
 */
// [port] Returns the allocation (void * in C) as a byte array, or null on failure.
internal delegate byte[] aom_alloc_img_data_cb_fn_t(object priv, int size);

internal static partial class aom_image_internal
{
    /*!\brief Bit flag for layer-specific metadata insertion. */
    internal const int AOM_MIF_LAYER_SPECIFIC = 0x10;

    /*!\brief Mask for metadata insertion location. */
    internal const int AOM_MIF_INSERT_LOCATION_MASK = 0x0f;

    // [subset] aom_img_metadata_array_alloc, aom_img_metadata_array_free and
    // aom_img_alloc_with_cb never run in the subset (no metadata, no allocation callback) and
    // are not ported.
}
