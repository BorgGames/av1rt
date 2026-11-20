// Port of libaom av1/common/timing.h

global using static Av1rt.Aom.timing;

namespace Av1rt.Aom;

internal partial struct aom_timing_info_t
{
    public uint num_units_in_display_tick;
    public uint time_scale;
    public int equal_picture_interval;
    public uint num_ticks_per_picture;
}

internal partial struct aom_dec_model_info_t
{
    public uint num_units_in_decoding_tick;
    public int encoder_decoder_buffer_delay_length;
    public int buffer_removal_time_length;
    public int frame_presentation_time_length;
}

internal partial struct aom_dec_model_op_parameters_t
{
    public int decoder_model_param_present_flag;
    public long bitrate;
    public long buffer_size;
    public uint decoder_buffer_delay;
    public uint encoder_buffer_delay;
    public int low_delay_mode_flag;
    public int display_model_param_present_flag;
    public int initial_display_delay;
}

internal static partial class timing
{
    // <gen:consts>
    internal const int MAX_NUM_OP_POINTS = 32;
    // </gen:consts>
}
