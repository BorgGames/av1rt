// Port of libaom aom_dsp/noise_model.h

namespace Av1rt.Aom;

// [port] Only the opaque type the encoder holds a pointer to is declared:
// AV1_COMP.denoise_and_model (CONFIG_DENOISE). It is created by aom_denoise_and_model_alloc,
// which only runs when noise_level > 0 (denoise / film grain from the command line). That never
// happens in the subset, so the type stays an empty placeholder and the field stays null. The
// noise model structs and functions in noise_model.h/.c are not ported.
internal sealed partial class aom_denoise_and_model_t
{
}
