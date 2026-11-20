// Port of libaom av1/common/obmc.h
// [subset] OBMC is disabled: foreach_overlappable_nb_above/left never run and are not ported.

namespace Av1rt.Aom;

// `void *fun_ctxt` is an opaque context object.
internal delegate void overlappable_nb_visitor_t(MACROBLOCKD xd, int rel_mi_row,
                                                 int rel_mi_col, byte op_mi_size,
                                                 int dir, MB_MODE_INFO nb_mi,
                                                 object fun_ctxt, int num_planes);
