// Port of libaom av1/encoder/segmentation.h
global using static Av1rt.Aom.segmentation_h;

namespace Av1rt.Aom;

// Class name has the _h suffix (PORTING 2): the stem clashes with p1a's
// `segmentation` type (struct segmentation, seg_common.h).
// Nothing to port: encoder segmentation helpers are prototypes of segmentation.c.
internal static partial class segmentation_h
{
}
