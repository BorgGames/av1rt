// Port of libaom av1/common/pred_common.c

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

internal static partial class pred_common
{
    // Returns a context number for the given MB prediction signal
    private static InterpFilter get_ref_filter_type(MB_MODE_INFO ref_mbmi,
                                                    MACROBLOCKD xd, int dir,
                                                    MV_REFERENCE_FRAME ref_frame)
    {
        return ((ref_mbmi.ref_frame[0] == ref_frame ||
                 ref_mbmi.ref_frame[1] == ref_frame)
                    ? av1_extract_interp_filter(ref_mbmi.interp_filters, dir & 0x01)
                    : SWITCHABLE_FILTERS);
    }

    internal static int av1_get_pred_context_switchable_interp(MACROBLOCKD xd, int dir)
    {
        MB_MODE_INFO mbmi = xd.mi[0];
        int ctx_offset =
            B(mbmi.ref_frame[1] > INTRA_FRAME) * INTER_FILTER_COMP_OFFSET;
        Debug.Assert(dir == 0 || dir == 1);
        MV_REFERENCE_FRAME ref_frame = mbmi.ref_frame[0];
        // Note:
        // The mode info data structure has a one element border above and to the
        // left of the entries corresponding to real macroblocks.
        // The prediction flags in these dummy entries are initialized to 0.
        int filter_type_ctx = ctx_offset + (dir & 0x01) * INTER_FILTER_DIR_OFFSET;
        int left_type = SWITCHABLE_FILTERS;
        int above_type = SWITCHABLE_FILTERS;

        if (xd.left_available)
            left_type = get_ref_filter_type(xd.mi[-1], xd, dir, ref_frame);

        if (xd.up_available)
            above_type =
                get_ref_filter_type(xd.mi[-xd.mi_stride], xd, dir, ref_frame);

        if (left_type == above_type)
        {
            filter_type_ctx += left_type;
        }
        else if (left_type == SWITCHABLE_FILTERS)
        {
            Debug.Assert(above_type != SWITCHABLE_FILTERS);
            filter_type_ctx += above_type;
        }
        else if (above_type == SWITCHABLE_FILTERS)
        {
            Debug.Assert(left_type != SWITCHABLE_FILTERS);
            filter_type_ctx += left_type;
        }
        else
        {
            filter_type_ctx += SWITCHABLE_FILTERS;
        }

        return filter_type_ctx;
    }

    // [subset] palette_add_to_cache, av1_get_palette_cache: palette is disabled.

    // The mode info data structure has a one element border above and to the
    // left of the entries corresponding to real macroblocks.
    // The prediction flags in these dummy entries are initialized to 0.
    // 0 - inter/inter, inter/--, --/inter, --/--
    // 1 - intra/inter, inter/intra
    // 2 - intra/--, --/intra
    // 3 - intra/intra
    internal static int av1_get_intra_inter_context(MACROBLOCKD xd)
    {
        MB_MODE_INFO above_mbmi = xd.above_mbmi;
        MB_MODE_INFO left_mbmi = xd.left_mbmi;
        int has_above = B(xd.up_available);
        int has_left = B(xd.left_available);

        if (has_above != 0 && has_left != 0)
        {  // both edges available
            int above_intra = B(is_inter_block(above_mbmi) == 0);
            int left_intra = B(is_inter_block(left_mbmi) == 0);
            return left_intra != 0 && above_intra != 0 ? 3
                                                       : B(left_intra != 0 || above_intra != 0);
        }
        else if (has_above != 0 || has_left != 0)
        {  // one edge available
            return 2 * B(is_inter_block(has_above != 0 ? above_mbmi : left_mbmi) == 0);
        }
        else
        {
            return 0;
        }
    }

    // [port] CHECK_BACKWARD_REFS / IS_BACKWARD_REF_FRAME macros.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CHECK_BACKWARD_REFS(MV_REFERENCE_FRAME ref_frame) =>
        B(((ref_frame) >= BWDREF_FRAME) && ((ref_frame) <= ALTREF_FRAME));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IS_BACKWARD_REF_FRAME(MV_REFERENCE_FRAME ref_frame) =>
        CHECK_BACKWARD_REFS(ref_frame);

    // [subset] av1_get_reference_mode_context: never executed (reference_mode is always
    // SINGLE_REFERENCE when coding blocks in the subset).

    internal static int av1_get_comp_reference_type_context(MACROBLOCKD xd)
    {
        int pred_context;
        MB_MODE_INFO above_mbmi = xd.above_mbmi;
        MB_MODE_INFO left_mbmi = xd.left_mbmi;
        bool above_in_image = xd.up_available;
        bool left_in_image = xd.left_available;

        if (above_in_image && left_in_image)
        {  // both edges available
            int above_intra = B(is_inter_block(above_mbmi) == 0);
            int left_intra = B(is_inter_block(left_mbmi) == 0);

            if (above_intra != 0 && left_intra != 0)
            {  // intra/intra
                pred_context = 2;
            }
            else if (above_intra != 0 || left_intra != 0)
            {  // intra/inter
                MB_MODE_INFO inter_mbmi = above_intra != 0 ? left_mbmi : above_mbmi;

                if (has_second_ref(inter_mbmi) == 0)  // single pred
                    pred_context = 2;
                else  // comp pred
                    pred_context = 1 + 2 * has_uni_comp_refs(inter_mbmi);
            }
            else
            {  // inter/inter
                int a_sg = B(has_second_ref(above_mbmi) == 0);
                int l_sg = B(has_second_ref(left_mbmi) == 0);
                MV_REFERENCE_FRAME frfa = above_mbmi.ref_frame[0];
                MV_REFERENCE_FRAME frfl = left_mbmi.ref_frame[0];

                if (a_sg != 0 && l_sg != 0)
                {  // single/single
                    pred_context = 1 + 2 * B((IS_BACKWARD_REF_FRAME(frfa) ^
                                              IS_BACKWARD_REF_FRAME(frfl)) == 0);
                }
                else if (l_sg != 0 || a_sg != 0)
                {  // single/comp
                    int uni_rfc =
                        a_sg != 0 ? has_uni_comp_refs(left_mbmi) : has_uni_comp_refs(above_mbmi);

                    if (uni_rfc == 0)  // comp bidir
                        pred_context = 1;
                    else  // comp unidir
                        pred_context = 3 + B((IS_BACKWARD_REF_FRAME(frfa) ^
                                              IS_BACKWARD_REF_FRAME(frfl)) == 0);
                }
                else
                {  // comp/comp
                    int a_uni_rfc = has_uni_comp_refs(above_mbmi);
                    int l_uni_rfc = has_uni_comp_refs(left_mbmi);

                    if (a_uni_rfc == 0 && l_uni_rfc == 0)  // bidir/bidir
                        pred_context = 0;
                    else if (a_uni_rfc == 0 || l_uni_rfc == 0)  // unidir/bidir
                        pred_context = 2;
                    else  // unidir/unidir
                        pred_context =
                            3 + B((B(frfa == BWDREF_FRAME) ^ B(frfl == BWDREF_FRAME)) == 0);
                }
            }
        }
        else if (above_in_image || left_in_image)
        {  // one edge available
            MB_MODE_INFO edge_mbmi = above_in_image ? above_mbmi : left_mbmi;

            if (is_inter_block(edge_mbmi) == 0)
            {  // intra
                pred_context = 2;
            }
            else
            {  // inter
                if (has_second_ref(edge_mbmi) == 0)  // single pred
                    pred_context = 2;
                else  // comp pred
                    pred_context = 4 * has_uni_comp_refs(edge_mbmi);
            }
        }
        else
        {  // no edges available
            pred_context = 2;
        }

        Debug.Assert(pred_context >= 0 && pred_context < COMP_REF_TYPE_CONTEXTS);
        return pred_context;
    }

    // [subset] av1_get_pred_context_uni_comp_ref_p{,1,2}: never executed (compound
    // reference is never coded in the subset).

    // == Common context functions for both comp and single ref ==
    //
    // Obtain contexts to signal a reference frame to be either LAST/LAST2 or
    // LAST3/GOLDEN.
    private static int get_pred_context_ll2_or_l3gld(MACROBLOCKD xd)
    {
        byte[] ref_counts = xd.neighbors_ref_counts;

        // Count of LAST + LAST2
        int last_last2_count = ref_counts[LAST_FRAME] + ref_counts[LAST2_FRAME];
        // Count of LAST3 + GOLDEN
        int last3_gld_count =
            ref_counts[LAST3_FRAME] + ref_counts[GOLDEN_FRAME];

        int pred_context = (last_last2_count == last3_gld_count)
                               ? 1
                               : ((last_last2_count < last3_gld_count) ? 0 : 2);

        Debug.Assert(pred_context >= 0 && pred_context < REF_CONTEXTS);
        return pred_context;
    }

    // Obtain contexts to signal a reference frame to be either LAST or LAST2.
    private static int get_pred_context_last_or_last2(MACROBLOCKD xd)
    {
        byte[] ref_counts = xd.neighbors_ref_counts;

        // Count of LAST
        int last_count = ref_counts[LAST_FRAME];
        // Count of LAST2
        int last2_count = ref_counts[LAST2_FRAME];

        int pred_context =
            (last_count == last2_count) ? 1 : ((last_count < last2_count) ? 0 : 2);

        Debug.Assert(pred_context >= 0 && pred_context < REF_CONTEXTS);
        return pred_context;
    }

    // Obtain contexts to signal a reference frame to be either LAST3 or GOLDEN.
    // [port] Never executed in the coverage runs, but reached as soon as a block codes
    // GOLDEN_FRAME (a data-dependent mode decision), so it is ported.
    private static int get_pred_context_last3_or_gld(MACROBLOCKD xd)
    {
        byte[] ref_counts = xd.neighbors_ref_counts;

        // Count of LAST3
        int last3_count = ref_counts[LAST3_FRAME];
        // Count of GOLDEN
        int gld_count = ref_counts[GOLDEN_FRAME];

        int pred_context =
            (last3_count == gld_count) ? 1 : ((last3_count < gld_count) ? 0 : 2);

        Debug.Assert(pred_context >= 0 && pred_context < REF_CONTEXTS);
        return pred_context;
    }

    // [subset] get_pred_context_brfarf2_or_arf, get_pred_context_brf_or_arf2 and the comp ref
    // wrappers av1_get_pred_context_comp_ref_p{,1,2}, av1_get_pred_context_comp_bwdref_p{,1}:
    // never executed (backward references and compound references are never coded).

    // == Context functions for single ref ==
    //
    // For the bit to signal whether the single reference is a forward reference
    // frame or a backward reference frame.
    internal static int av1_get_pred_context_single_ref_p1(MACROBLOCKD xd)
    {
        byte[] ref_counts = xd.neighbors_ref_counts;

        // Count of forward reference frames
        int fwd_count = ref_counts[LAST_FRAME] + ref_counts[LAST2_FRAME] +
                        ref_counts[LAST3_FRAME] + ref_counts[GOLDEN_FRAME];
        // Count of backward reference frames
        int bwd_count = ref_counts[BWDREF_FRAME] + ref_counts[ALTREF2_FRAME] +
                        ref_counts[ALTREF_FRAME];

        int pred_context =
            (fwd_count == bwd_count) ? 1 : ((fwd_count < bwd_count) ? 0 : 2);

        Debug.Assert(pred_context >= 0 && pred_context < REF_CONTEXTS);
        return pred_context;
    }

    // [subset] av1_get_pred_context_single_ref_p2: never executed (no backward reference is
    // ever coded).

    // For the bit to signal whether the single reference is LAST3/GOLDEN or
    // LAST2/LAST, knowing that it shall be either of these 2 choices.
    internal static int av1_get_pred_context_single_ref_p3(MACROBLOCKD xd)
    {
        return get_pred_context_ll2_or_l3gld(xd);
    }

    // For the bit to signal whether the single reference is LAST2_FRAME or
    // LAST_FRAME, knowing that it shall be either of these 2 choices.
    internal static int av1_get_pred_context_single_ref_p4(MACROBLOCKD xd)
    {
        return get_pred_context_last_or_last2(xd);
    }

    // For the bit to signal whether the single reference is GOLDEN_FRAME or
    // LAST3_FRAME, knowing that it shall be either of these 2 choices.
    // [port] Not executed in the coverage runs (no block ended up coding GOLDEN_FRAME); ported
    // because that is a data-dependent mode decision (see get_pred_context_last3_or_gld).
    internal static int av1_get_pred_context_single_ref_p5(MACROBLOCKD xd)
    {
        return get_pred_context_last3_or_gld(xd);
    }

    // [subset] av1_get_pred_context_single_ref_p6: never executed (ALTREF2/BWDREF are never
    // coded).
}
