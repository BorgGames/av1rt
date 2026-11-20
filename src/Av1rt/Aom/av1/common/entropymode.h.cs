// Port of libaom av1/common/entropymode.h

global using static Av1rt.Aom.entropymode;

using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// [port] Class: entries of av1_scan_orders (scan.c) are only ever referenced through
// `const SCAN_ORDER *` and never copied.
internal sealed partial class SCAN_ORDER
{
    public readonly Ptr<short> scan;
    public readonly Ptr<short> iscan;

    public SCAN_ORDER(Ptr<short> scan, Ptr<short> iscan)
    {
        this.scan = scan;
        this.iscan = iscan;
    }
}

// <gen:classes>
internal sealed partial class FRAME_CONTEXT
{
    public readonly ushort[][][] txb_skip_cdf = new_jagged<ushort>(TX_SIZES, TXB_SKIP_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][][][] eob_extra_cdf = new_jagged<ushort>(TX_SIZES, PLANE_TYPES, EOB_COEF_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][][] dc_sign_cdf = new_jagged<ushort>(PLANE_TYPES, DC_SIGN_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][][] eob_flag_cdf16 = new_jagged<ushort>(PLANE_TYPES, 2, CDF_SIZE(5));
    public readonly ushort[][][] eob_flag_cdf32 = new_jagged<ushort>(PLANE_TYPES, 2, CDF_SIZE(6));
    public readonly ushort[][][] eob_flag_cdf64 = new_jagged<ushort>(PLANE_TYPES, 2, CDF_SIZE(7));
    public readonly ushort[][][] eob_flag_cdf128 = new_jagged<ushort>(PLANE_TYPES, 2, CDF_SIZE(8));
    public readonly ushort[][][] eob_flag_cdf256 = new_jagged<ushort>(PLANE_TYPES, 2, CDF_SIZE(9));
    public readonly ushort[][][] eob_flag_cdf512 = new_jagged<ushort>(PLANE_TYPES, 2, CDF_SIZE(10));
    public readonly ushort[][][] eob_flag_cdf1024 = new_jagged<ushort>(PLANE_TYPES, 2, CDF_SIZE(11));
    public readonly ushort[][][][] coeff_base_eob_cdf = new_jagged<ushort>(TX_SIZES, PLANE_TYPES, SIG_COEF_CONTEXTS_EOB, CDF_SIZE(3));
    public readonly ushort[][][][] coeff_base_cdf = new_jagged<ushort>(TX_SIZES, PLANE_TYPES, SIG_COEF_CONTEXTS, CDF_SIZE(4));
    public readonly ushort[][][][] coeff_br_cdf = new_jagged<ushort>(TX_SIZES, PLANE_TYPES, LEVEL_CONTEXTS, CDF_SIZE(BR_CDF_SIZE));
    public readonly ushort[][] newmv_cdf = new_jagged<ushort>(NEWMV_MODE_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][] zeromv_cdf = new_jagged<ushort>(GLOBALMV_MODE_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][] refmv_cdf = new_jagged<ushort>(REFMV_MODE_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][] drl_cdf = new_jagged<ushort>(DRL_MODE_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][] inter_compound_mode_cdf = new_jagged<ushort>(INTER_MODE_CONTEXTS, CDF_SIZE(INTER_COMPOUND_MODES));
    public readonly ushort[][] compound_type_cdf = new_jagged<ushort>(BLOCK_SIZES_ALL, CDF_SIZE(MASKED_COMPOUND_TYPES));
    public readonly ushort[][] wedge_idx_cdf = new_jagged<ushort>(BLOCK_SIZES_ALL, CDF_SIZE(16));
    public readonly ushort[][] interintra_cdf = new_jagged<ushort>(BLOCK_SIZE_GROUPS, CDF_SIZE(2));
    public readonly ushort[][] wedge_interintra_cdf = new_jagged<ushort>(BLOCK_SIZES_ALL, CDF_SIZE(2));
    public readonly ushort[][] interintra_mode_cdf = new_jagged<ushort>(BLOCK_SIZE_GROUPS, CDF_SIZE(INTERINTRA_MODES));
    public readonly ushort[][] motion_mode_cdf = new_jagged<ushort>(BLOCK_SIZES_ALL, CDF_SIZE(MOTION_MODES));
    public readonly ushort[][] obmc_cdf = new_jagged<ushort>(BLOCK_SIZES_ALL, CDF_SIZE(2));
    public readonly ushort[][] palette_y_size_cdf = new_jagged<ushort>(PALATTE_BSIZE_CTXS, CDF_SIZE(PALETTE_SIZES));
    public readonly ushort[][] palette_uv_size_cdf = new_jagged<ushort>(PALATTE_BSIZE_CTXS, CDF_SIZE(PALETTE_SIZES));
    public readonly ushort[][][] palette_y_color_index_cdf = new_jagged<ushort>(PALETTE_SIZES, PALETTE_COLOR_INDEX_CONTEXTS, CDF_SIZE(PALETTE_COLORS));
    public readonly ushort[][][] palette_uv_color_index_cdf = new_jagged<ushort>(PALETTE_SIZES, PALETTE_COLOR_INDEX_CONTEXTS, CDF_SIZE(PALETTE_COLORS));
    public readonly ushort[][][] palette_y_mode_cdf = new_jagged<ushort>(PALATTE_BSIZE_CTXS, PALETTE_Y_MODE_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][] palette_uv_mode_cdf = new_jagged<ushort>(PALETTE_UV_MODE_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][] comp_inter_cdf = new_jagged<ushort>(COMP_INTER_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][][] single_ref_cdf = new_jagged<ushort>(REF_CONTEXTS, SINGLE_REFS - 1, CDF_SIZE(2));
    public readonly ushort[][] comp_ref_type_cdf = new_jagged<ushort>(COMP_REF_TYPE_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][][] uni_comp_ref_cdf = new_jagged<ushort>(UNI_COMP_REF_CONTEXTS, UNIDIR_COMP_REFS - 1, CDF_SIZE(2));
    public readonly ushort[][][] comp_ref_cdf = new_jagged<ushort>(REF_CONTEXTS, FWD_REFS - 1, CDF_SIZE(2));
    public readonly ushort[][][] comp_bwdref_cdf = new_jagged<ushort>(REF_CONTEXTS, BWD_REFS - 1, CDF_SIZE(2));
    public readonly ushort[][] txfm_partition_cdf = new_jagged<ushort>(TXFM_PARTITION_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][] compound_index_cdf = new_jagged<ushort>(COMP_INDEX_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][] comp_group_idx_cdf = new_jagged<ushort>(COMP_GROUP_IDX_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][] skip_mode_cdfs = new_jagged<ushort>(SKIP_MODE_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][] skip_txfm_cdfs = new_jagged<ushort>(SKIP_CONTEXTS, CDF_SIZE(2));
    public readonly ushort[][] intra_inter_cdf = new_jagged<ushort>(INTRA_INTER_CONTEXTS, CDF_SIZE(2));
    public readonly nmv_context nmvc = new();
    public readonly nmv_context ndvc = new();
    public readonly ushort[] intrabc_cdf = new ushort[CDF_SIZE(2)];
    public readonly segmentation_probs seg = new();
    public readonly ushort[][] filter_intra_cdfs = new_jagged<ushort>(BLOCK_SIZES_ALL, CDF_SIZE(2));
    public readonly ushort[] filter_intra_mode_cdf = new ushort[CDF_SIZE(FILTER_INTRA_MODES)];
    public readonly ushort[] switchable_restore_cdf = new ushort[CDF_SIZE(RESTORE_SWITCHABLE_TYPES)];
    public readonly ushort[] wiener_restore_cdf = new ushort[CDF_SIZE(2)];
    public readonly ushort[] sgrproj_restore_cdf = new ushort[CDF_SIZE(2)];
    public readonly ushort[][] y_mode_cdf = new_jagged<ushort>(BLOCK_SIZE_GROUPS, CDF_SIZE(INTRA_MODES));
    public readonly ushort[][][] uv_mode_cdf = new_jagged<ushort>(CFL_ALLOWED_TYPES, INTRA_MODES, CDF_SIZE(UV_INTRA_MODES));
    public readonly ushort[][] partition_cdf = new_jagged<ushort>(PARTITION_CONTEXTS, CDF_SIZE(EXT_PARTITION_TYPES));
    public readonly ushort[][] switchable_interp_cdf = new_jagged<ushort>(SWITCHABLE_FILTER_CONTEXTS, CDF_SIZE(SWITCHABLE_FILTERS));
    public readonly ushort[][][] kf_y_cdf = new_jagged<ushort>(KF_MODE_CONTEXTS, KF_MODE_CONTEXTS, CDF_SIZE(INTRA_MODES));
    public readonly ushort[][] angle_delta_cdf = new_jagged<ushort>(DIRECTIONAL_MODES, CDF_SIZE(2 * MAX_ANGLE_DELTA + 1));
    public readonly ushort[][][] tx_size_cdf = new_jagged<ushort>(MAX_TX_CATS, TX_SIZE_CONTEXTS, CDF_SIZE(MAX_TX_DEPTH + 1));
    public readonly ushort[] delta_q_cdf = new ushort[CDF_SIZE(DELTA_Q_PROBS + 1)];
    public readonly ushort[][] delta_lf_multi_cdf = new_jagged<ushort>(FRAME_LF_COUNT, CDF_SIZE(DELTA_LF_PROBS + 1));
    public readonly ushort[] delta_lf_cdf = new ushort[CDF_SIZE(DELTA_LF_PROBS + 1)];
    public readonly ushort[][][][] intra_ext_tx_cdf = new_jagged<ushort>(EXT_TX_SETS_INTRA, EXT_TX_SIZES, INTRA_MODES, CDF_SIZE(TX_TYPES));
    public readonly ushort[][][] inter_ext_tx_cdf = new_jagged<ushort>(EXT_TX_SETS_INTER, EXT_TX_SIZES, CDF_SIZE(TX_TYPES));
    public readonly ushort[] cfl_sign_cdf = new ushort[CDF_SIZE(CFL_JOINT_SIGNS)];
    public readonly ushort[][] cfl_alpha_cdf = new_jagged<ushort>(CFL_ALPHA_CONTEXTS, CDF_SIZE(CFL_ALPHABET_SIZE));
    public int initialized;

    /// <summary>C struct assignment (<c>*this = *other</c>): deep copy of every field.</summary>
    public void CopyFrom(FRAME_CONTEXT other)
    {
        av1_copy(txb_skip_cdf, other.txb_skip_cdf);
        av1_copy(eob_extra_cdf, other.eob_extra_cdf);
        av1_copy(dc_sign_cdf, other.dc_sign_cdf);
        av1_copy(eob_flag_cdf16, other.eob_flag_cdf16);
        av1_copy(eob_flag_cdf32, other.eob_flag_cdf32);
        av1_copy(eob_flag_cdf64, other.eob_flag_cdf64);
        av1_copy(eob_flag_cdf128, other.eob_flag_cdf128);
        av1_copy(eob_flag_cdf256, other.eob_flag_cdf256);
        av1_copy(eob_flag_cdf512, other.eob_flag_cdf512);
        av1_copy(eob_flag_cdf1024, other.eob_flag_cdf1024);
        av1_copy(coeff_base_eob_cdf, other.coeff_base_eob_cdf);
        av1_copy(coeff_base_cdf, other.coeff_base_cdf);
        av1_copy(coeff_br_cdf, other.coeff_br_cdf);
        av1_copy(newmv_cdf, other.newmv_cdf);
        av1_copy(zeromv_cdf, other.zeromv_cdf);
        av1_copy(refmv_cdf, other.refmv_cdf);
        av1_copy(drl_cdf, other.drl_cdf);
        av1_copy(inter_compound_mode_cdf, other.inter_compound_mode_cdf);
        av1_copy(compound_type_cdf, other.compound_type_cdf);
        av1_copy(wedge_idx_cdf, other.wedge_idx_cdf);
        av1_copy(interintra_cdf, other.interintra_cdf);
        av1_copy(wedge_interintra_cdf, other.wedge_interintra_cdf);
        av1_copy(interintra_mode_cdf, other.interintra_mode_cdf);
        av1_copy(motion_mode_cdf, other.motion_mode_cdf);
        av1_copy(obmc_cdf, other.obmc_cdf);
        av1_copy(palette_y_size_cdf, other.palette_y_size_cdf);
        av1_copy(palette_uv_size_cdf, other.palette_uv_size_cdf);
        av1_copy(palette_y_color_index_cdf, other.palette_y_color_index_cdf);
        av1_copy(palette_uv_color_index_cdf, other.palette_uv_color_index_cdf);
        av1_copy(palette_y_mode_cdf, other.palette_y_mode_cdf);
        av1_copy(palette_uv_mode_cdf, other.palette_uv_mode_cdf);
        av1_copy(comp_inter_cdf, other.comp_inter_cdf);
        av1_copy(single_ref_cdf, other.single_ref_cdf);
        av1_copy(comp_ref_type_cdf, other.comp_ref_type_cdf);
        av1_copy(uni_comp_ref_cdf, other.uni_comp_ref_cdf);
        av1_copy(comp_ref_cdf, other.comp_ref_cdf);
        av1_copy(comp_bwdref_cdf, other.comp_bwdref_cdf);
        av1_copy(txfm_partition_cdf, other.txfm_partition_cdf);
        av1_copy(compound_index_cdf, other.compound_index_cdf);
        av1_copy(comp_group_idx_cdf, other.comp_group_idx_cdf);
        av1_copy(skip_mode_cdfs, other.skip_mode_cdfs);
        av1_copy(skip_txfm_cdfs, other.skip_txfm_cdfs);
        av1_copy(intra_inter_cdf, other.intra_inter_cdf);
        nmvc.CopyFrom(other.nmvc);
        ndvc.CopyFrom(other.ndvc);
        av1_copy(intrabc_cdf, other.intrabc_cdf);
        seg.CopyFrom(other.seg);
        av1_copy(filter_intra_cdfs, other.filter_intra_cdfs);
        av1_copy(filter_intra_mode_cdf, other.filter_intra_mode_cdf);
        av1_copy(switchable_restore_cdf, other.switchable_restore_cdf);
        av1_copy(wiener_restore_cdf, other.wiener_restore_cdf);
        av1_copy(sgrproj_restore_cdf, other.sgrproj_restore_cdf);
        av1_copy(y_mode_cdf, other.y_mode_cdf);
        av1_copy(uv_mode_cdf, other.uv_mode_cdf);
        av1_copy(partition_cdf, other.partition_cdf);
        av1_copy(switchable_interp_cdf, other.switchable_interp_cdf);
        av1_copy(kf_y_cdf, other.kf_y_cdf);
        av1_copy(angle_delta_cdf, other.angle_delta_cdf);
        av1_copy(tx_size_cdf, other.tx_size_cdf);
        av1_copy(delta_q_cdf, other.delta_q_cdf);
        av1_copy(delta_lf_multi_cdf, other.delta_lf_multi_cdf);
        av1_copy(delta_lf_cdf, other.delta_lf_cdf);
        av1_copy(intra_ext_tx_cdf, other.intra_ext_tx_cdf);
        av1_copy(inter_ext_tx_cdf, other.inter_ext_tx_cdf);
        av1_copy(cfl_sign_cdf, other.cfl_sign_cdf);
        av1_copy(cfl_alpha_cdf, other.cfl_alpha_cdf);
        initialized = other.initialized;
    }
}
// </gen:classes>

internal static partial class entropymode
{
    // <gen:consts>
    internal const int BLOCK_SIZE_GROUPS = 4;
    internal const int TX_SIZE_CONTEXTS = 3;
    internal const int PALETTE_COLOR_INDEX_CONTEXTS = 5;
    internal const int PALETTE_Y_MODE_CONTEXTS = 3;
    internal const int PALETTE_UV_MODE_CONTEXTS = 2;
    internal const int PALATTE_BSIZE_CTXS = 7;
    internal const int MAX_COLOR_CONTEXT_HASH = 8;
    internal const int NUM_PALETTE_NEIGHBORS = 3;
    internal const int KF_MODE_CONTEXTS = 5;

    // int av1_ext_tx_ind[EXT_TX_SET_TYPES][TX_TYPES]
    internal static readonly int[][] av1_ext_tx_ind = {
        new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        new int[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        new int[] { 1, 3, 4, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        new int[] { 1, 5, 6, 4, 0, 0, 0, 0, 0, 0, 2, 3, 0, 0, 0, 0 },
        new int[] { 3, 4, 5, 8, 6, 7, 9, 10, 11, 0, 1, 2, 0, 0, 0, 0 },
        new int[] { 7, 8, 9, 12, 10, 11, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6 }
    };

    // int av1_ext_tx_inv[EXT_TX_SET_TYPES][TX_TYPES]
    internal static readonly int[][] av1_ext_tx_inv = {
        new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        new int[] { 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        new int[] { 9, 0, 3, 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        new int[] { 9, 0, 10, 11, 3, 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        new int[] { 9, 10, 11, 0, 1, 2, 4, 5, 3, 6, 7, 8, 0, 0, 0, 0 },
        new int[] { 9, 10, 11, 12, 13, 14, 15, 0, 1, 2, 4, 5, 3, 6, 7, 8 }
    };
    // </gen:consts>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int INTER_OFFSET(int mode) => (mode) - NEARESTMV;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int INTER_COMPOUND_OFFSET(int mode) => (byte)((mode) - NEAREST_NEARESTMV);

    // av1_palette_color_index_context_lookup[MAX_COLOR_CONTEXT_HASH + 1] is defined in
    // entropymode.c (not part of p1a).
}
