// Port of libaom aom_dsp/grain_params.h

namespace Av1rt.Aom;

/*!\file
 * \brief Describes film grain parameters
 *
 */

/*!\brief Structure containing film grain synthesis parameters for a frame
 *
 * This structure contains input parameters for film grain synthesis
 */
// [port] A struct: AV1_COMMON and RefCntBuffer embed it and the encoder copies it by value
// (cur_frame->film_grain_params = cm->film_grain_params). The 2-D arrays are nested Arr types.
internal partial struct aom_film_grain_t
{
    // This structure is compared element-by-element in the function
    // aom_check_grain_params_equiv: this function must be updated if any changes
    // are made to this structure.
    public int apply_grain;

    public int update_parameters;

    // 8 bit values
    public Arr14<Arr2<int>> scaling_points_y;
    public int num_y_points;  // value: 0..14

    // 8 bit values
    public Arr10<Arr2<int>> scaling_points_cb;
    public int num_cb_points;  // value: 0..10

    // 8 bit values
    public Arr10<Arr2<int>> scaling_points_cr;
    public int num_cr_points;  // value: 0..10

    public int scaling_shift;  // values : 8..11

    public int ar_coeff_lag;  // values:  0..3

    // 8 bit values
    public Arr24<int> ar_coeffs_y;
    public Arr25<int> ar_coeffs_cb;
    public Arr25<int> ar_coeffs_cr;

    // Shift value: AR coeffs range
    // 6: [-2, 2)
    // 7: [-1, 1)
    // 8: [-0.5, 0.5)
    // 9: [-0.25, 0.25)
    public int ar_coeff_shift;  // values : 6..9

    public int cb_mult;       // 8 bits
    public int cb_luma_mult;  // 8 bits
    public int cb_offset;     // 9 bits

    public int cr_mult;       // 8 bits
    public int cr_luma_mult;  // 8 bits
    public int cr_offset;     // 9 bits

    public int overlap_flag;

    public int clip_to_restricted_range;

    public uint bit_depth;  // video bit depth

    public int chroma_scaling_from_luma;

    public int grain_scale_shift;

    public ushort random_seed;
    // This structure is compared element-by-element in the function
    // aom_check_grain_params_equiv: this function must be updated if any changes
    // are made to this structure.
}

// [subset] aom_check_grain_params_equiv never runs in the subset (film grain is off) and is not
// ported, so grain_params.h has no static class.
