// Port of libaom aom_scale/yv12config.h

global using static Av1rt.Aom.yv12config;
global using yv12_buffer_config = Av1rt.Aom.YV12_BUFFER_CONFIG;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

/*!\cond */
// [port] A class (PORTING 3.3): large and used through pointers; embedders allocate it.
// - The C anonymous unions give each pair/triple two names (y_width/uv_width and widths[2],
//   y_buffer/u_buffer/v_buffer and buffers[3]). The arrays are the storage (ArrN fields) and the
//   individual names are ref-returning accessors, so both spellings read and write the same
//   value and `ref ybf.y_buffer` works.
// - The plane pointers are Ptr<byte> into buffer_alloc, the single allocation.
// - CONFIG_REALTIME_ONLY: y_pyramid / corners are compiled out.
// - Clear() is `memset(ybf, 0, sizeof(*ybf))`; CopyFrom is the C struct assignment (shallow:
//   the copy shares the pixel buffer, as in C).
internal sealed partial class YV12_BUFFER_CONFIG
{
    public Arr2<int> widths;
    public ref int y_width => ref widths[0];
    public ref int uv_width => ref widths[1];

    public Arr2<int> heights;
    public ref int y_height => ref heights[0];
    public ref int uv_height => ref heights[1];

    public Arr2<int> crop_widths;
    public ref int y_crop_width => ref crop_widths[0];
    public ref int uv_crop_width => ref crop_widths[1];

    public Arr2<int> crop_heights;
    public ref int y_crop_height => ref crop_heights[0];
    public ref int uv_crop_height => ref crop_heights[1];

    public Arr2<int> strides;
    public ref int y_stride => ref strides[0];
    public ref int uv_stride => ref strides[1];

    public Arr3<Ptr<byte>> buffers;
    public ref Ptr<byte> y_buffer => ref buffers[0];
    public ref Ptr<byte> u_buffer => ref buffers[1];
    public ref Ptr<byte> v_buffer => ref buffers[2];

    // Indicate whether y_buffer, u_buffer, and v_buffer points to the internally
    // allocated memory or external buffers.
    public int use_external_reference_buffers;
    // This is needed to store y_buffer, u_buffer, and v_buffer when set reference
    // uses an external refernece, and restore those buffer pointers after the
    // external reference frame is no longer used.
    public Arr3<Ptr<byte>> store_buf_adr;

    // If the frame is stored in a 16-bit buffer, this stores an 8-bit version
    // for use in global motion detection. It is allocated on-demand.
    // [port] y_pyramid / corners: CONFIG_REALTIME_ONLY, compiled out.

    // [port] uint8_t *buffer_alloc: the allocation itself, so a plain array.
    public byte[] buffer_alloc;
    public int buffer_alloc_sz;
    public int border;
    public int frame_size;
    public int subsampling_x;
    public int subsampling_y;
    public uint bit_depth;
    public aom_color_primaries_t color_primaries;
    public aom_transfer_characteristics_t transfer_characteristics;
    public aom_matrix_coefficients_t matrix_coefficients;
    public byte monochrome;
    public aom_chroma_sample_position_t chroma_sample_position;
    public aom_color_range_t color_range;
    public int render_width;
    public int render_height;

    public int corrupted;
    public int flags;
    public aom_metadata_array_t metadata;

    // memset(ybf, 0, sizeof(YV12_BUFFER_CONFIG))
    public void Clear()
    {
        widths = default;
        heights = default;
        crop_widths = default;
        crop_heights = default;
        strides = default;
        buffers = default;
        use_external_reference_buffers = 0;
        store_buf_adr = default;
        buffer_alloc = null;
        buffer_alloc_sz = 0;
        border = 0;
        frame_size = 0;
        subsampling_x = 0;
        subsampling_y = 0;
        bit_depth = 0;
        color_primaries = 0;
        transfer_characteristics = 0;
        matrix_coefficients = 0;
        monochrome = 0;
        chroma_sample_position = 0;
        color_range = 0;
        render_width = 0;
        render_height = 0;
        corrupted = 0;
        flags = 0;
        metadata = null;
    }

    // C struct assignment `*this = *other` (shallow).
    public void CopyFrom(YV12_BUFFER_CONFIG other)
    {
        widths = other.widths;
        heights = other.heights;
        crop_widths = other.crop_widths;
        crop_heights = other.crop_heights;
        strides = other.strides;
        buffers = other.buffers;
        use_external_reference_buffers = other.use_external_reference_buffers;
        store_buf_adr = other.store_buf_adr;
        buffer_alloc = other.buffer_alloc;
        buffer_alloc_sz = other.buffer_alloc_sz;
        border = other.border;
        frame_size = other.frame_size;
        subsampling_x = other.subsampling_x;
        subsampling_y = other.subsampling_y;
        bit_depth = other.bit_depth;
        color_primaries = other.color_primaries;
        transfer_characteristics = other.transfer_characteristics;
        matrix_coefficients = other.matrix_coefficients;
        monochrome = other.monochrome;
        chroma_sample_position = other.chroma_sample_position;
        color_range = other.color_range;
        render_width = other.render_width;
        render_height = other.render_height;
        corrupted = other.corrupted;
        flags = other.flags;
        metadata = other.metadata;
    }
}
/*!\endcond */

internal static partial class yv12config
{
    /*!\cond */

    internal const int AOMINNERBORDERINPIXELS = 160;
    internal const int AOM_INTERP_EXTEND = 4;
    internal const int AOM_BORDER_IN_PIXELS = 288;
    internal const int AOM_ENC_NO_SCALE_BORDER = 160;
    internal const int AOM_ENC_ALLINTRA_BORDER = 64;
    internal const int AOM_DEC_BORDER_IN_PIXELS = 64;

    internal const int YV12_FLAG_HIGHBITDEPTH = 8;

    /*!\endcond */

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int aom_calc_y_stride(int aligned_width, int border)
    {
        return ((aligned_width + 2 * border) + 31) & ~31;
    }
}
