// Port of libaom av1/encoder/tokenize.h
global using RUN_TYPE = int;
global using static Av1rt.Aom.tokenize;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class tokenize
{
    // enum RUN_TYPE
    internal const int OUTPUT_ENABLED = 0;
    internal const int DRY_RUN_NORMAL = 1;
    internal const int DRY_RUN_COSTCOEFFS = 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int av1_get_tx_eob(segmentation seg, int segment_id,
                                       TX_SIZE tx_size)
    {
        int eob_max = av1_get_max_eob(tx_size);
        // C: the int segment_id converts to segfeature_active's uint8_t parameter.
        return segfeature_active(seg, (byte)segment_id, SEG_LVL_SKIP) != 0 ? 0 : eob_max;
    }

    // [subset] get_token_alloc / alloc_token_info: the token buffer is only used for palette
    // tokens (palette is off in the subset).

    // Check if memory allocation has been done for token related info.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool is_token_info_allocated(TokenInfo token_info)
    {
        return (!token_info.tile_tok[0][0].IsNull) &&
               (!token_info.tplist[0][0].IsNull);
    }

    // Free memory from token related variables.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void free_token_info(TokenInfo token_info)
    {
        token_info.tile_tok[0][0] = Ptr<TokenExtra>.Null;

        token_info.tplist[0][0] = Ptr<TokenList>.Null;

        token_info.tokens_allocated = 0;
    }
}

// The token and color_ctx members of the TokenExtra structure are used
// to store the indices of color and color context of each pixel in
// case of palette mode.
// 1) token can take values in the range of [0, 7] as maximum number of possible
// colors is 8 (PALETTE_COLORS). Hence token requires 3 bits (unsigned).
// 2) The reserved field (1-bit) is positioned such that color_ctx occupies the
// most significant bits and token occupies the least significant bits of the
// byte. Thus accesses to token and color_ctx are optimal.
// 3) color_ctx can take 5 (PALETTE_COLOR_INDEX_CONTEXTS) valid values, i.e.,
// from 0 to 4. As per the current implementation it can take values in the
// range of [-1, 4]. Here -1 corresponds to invalid color index context and is
// used for default initialization. Hence color_ctx requires 4 bits (signed).
// C bitfields (token : 3, reserved : 1, color_ctx : 4) become plain fields; writers must
// keep values in range as C truncation would.
internal partial struct TokenExtra
{
    internal byte token;
    internal byte reserved;
    internal sbyte color_ctx;
}

internal partial struct TokenList
{
    internal Ptr<TokenExtra> start;
    internal uint count;
}

// Class: embedded in AV1_COMP (cpi->token_info), used through pointers.
internal sealed partial class TokenInfo
{
    // Number of tile tokens for which memory is allocated.
    internal uint tokens_allocated;
    // tile_tok[i][j] is a pointer to the buffer storing palette tokens of the ith
    // tile row, jth tile column.
    internal readonly Ptr<TokenExtra>[][] tile_tok = EncArrays.New2<Ptr<TokenExtra>>(MAX_TILE_ROWS, MAX_TILE_COLS);
    // tplist[i][j][k] holds the start pointer of tile_tok[i][j] and the count of
    // palette tokens for the kth superblock row of the ith tile row, jth tile
    // column.
    internal readonly Ptr<TokenList>[][] tplist = EncArrays.New2<Ptr<TokenList>>(MAX_TILE_ROWS, MAX_TILE_COLS);
}

// Class: passed to the transform-block visitors as `void *arg`.
internal sealed partial class tokenize_b_args
{
    internal AV1_COMP cpi;
    internal ThreadData td;
    internal int this_rate;
    internal byte allow_update_cdf;
    internal RUN_TYPE dry_run;
}
