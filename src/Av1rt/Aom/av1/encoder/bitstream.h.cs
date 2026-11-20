// Port of libaom av1/encoder/bitstream.h
global using static Av1rt.Aom.bitstream;

namespace Av1rt.Aom;

internal static partial class bitstream
{
    // All functions of this header are implemented in bitstream.c.
}

// Stores the location and size of a tile's data in the bitstream.  Used for
// later identifying identical tiles
// Struct: small, embedded by value in PackBSParams and stored in arrays.
internal partial struct TileBufferEnc
{
    internal Ptr<byte> data;
    internal int size;
}

// Struct: small local of av1_pack_bitstream() (`FrameHeaderInfo fh_info = { NULL, 0, 0 }`)
// passed by pointer.
internal partial struct FrameHeaderInfo
{
    internal Ptr<byte> frame_header;
    internal int obu_header_byte_offset;
    internal int total_length;
}

// Class: lives in arrays (`PackBSParams pack_bs_params[MAX_TILES]` in ethread.c) whose
// elements are handed out by pointer and through `void *` job arguments, and is mutated
// through those pointers; never copied as a whole.
// PORT-NOTE: `struct aom_write_bit_buffer *saved_wb` points to a caller local of
// av1_pack_bitstream() that several functions read and update; aom_write_bit_buffer is a
// struct (p1c), so the pointer is a Ptr<aom_write_bit_buffer> over a 1-element array kept by
// the caller. `uint32_t *total_size` likewise points to a caller local (Ptr<uint>).
internal sealed partial class PackBSParams
{
    internal Ptr<aom_write_bit_buffer> saved_wb; // Bit stream buffer writer structure
    internal TileBufferEnc buf;     // Structure to hold bitstream buffer and size
    internal Ptr<uint> total_size;  // Size of the bitstream buffer for the tile in bytes
    internal Ptr<byte> dst;         // Base address of tile bitstream buffer
    internal Ptr<byte> tile_data_curr;   // Base address of tile-group bitstream buffer
    internal int tile_buf_size;      // Available bitstream buffer for the tile in bytes
    internal byte obu_extn_header;   // Presence of OBU extension header
    internal uint obu_header_size;  // Size of the OBU header
    internal int curr_tg_hdr_size;      // Size of the obu, tg, frame headers
    internal int tile_size_mi;          // Tile size in mi units
    internal int tile_row;              // Number of tile rows
    internal int tile_col;              // Number of tile columns
    internal int is_last_tile_in_tg;    // Flag to indicate last tile in a tile-group
    internal int new_tg;                // Flag to indicate starting of a new tile-group
}

// Struct: array elements sorted with qsort().
internal partial struct PackBSTileOrder
{
    internal ulong abs_sum_level;
    internal ushort tile_idx;
}

// Pack bitstream data for pack bitstream multi-threading.
// Class: embedded in the multi-thread info and used through pointers.
// CONFIG_MULTITHREAD=1: `pthread_mutex_t *mutex_` is a reference to p1c's pthread_mutex_t class.
internal sealed partial class AV1EncPackBSSync
{
    // Mutex lock used while dispatching jobs.
    internal pthread_mutex_t mutex_;
    // Tile order structure of pack bitstream multithreading.
    internal readonly PackBSTileOrder[] pack_bs_tile_order = new PackBSTileOrder[MAX_TILES];
    // Index of next job to be processed.
    internal int next_job_idx;
    // Initialized to false, set to true by the worker thread that encounters an
    // error in order to abort the processing of other worker threads.
    internal bool pack_bs_mt_exit;
}
