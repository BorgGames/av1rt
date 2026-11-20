// Port of libaom av1/av1_cx_iface.h

global using static Av1rt.Aom.av1_cx_iface;

namespace Av1rt.Aom;

// [port] av1_cx_iface.h only declares functions (av1_get_encoder_config,
// av1_create_context_and_bufferpool, av1_destroy_context_and_bufferpool); they are defined in
// av1/av1_cx_iface.c, whose port adds them to this class.
internal static partial class av1_cx_iface
{
}
