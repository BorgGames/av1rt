// Port of libaom av1/encoder/hash.h
global using static Av1rt.Aom.hash;

namespace Av1rt.Aom;

internal static partial class hash
{
    // [subset] CRC32C (crc32c calculator of the intraBC hash): intraBC is off in the subset.

    // Maximum number of subblocks per block
    // The biggest intraBC block size supported by AV1 is 128x128, and the smallest
    // subblock size is 2x2, therefore there can be a maximum of (128/2) * (128/2)
    // subblocks per block: 64 * 64 = 4096
    internal const int AOM_BUFFER_SIZE_FOR_BLOCK_HASH = (4096);
}
