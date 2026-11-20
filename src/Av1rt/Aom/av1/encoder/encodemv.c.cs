// Port of libaom av1/encoder/encodemv.c
using System;
using System.Diagnostics;

namespace Av1rt.Aom;

internal static partial class encodemv
{
    // [subset] update_mv_component_stats / av1_update_mv_stats: no caller in the realtime-only
    // build (never executed).

    private static void encode_mv_component(aom_writer w, int comp, nmv_component mvcomp,
                                            MvSubpelPrecision precision)
    {
        Debug.Assert(comp != 0);
        int offset = 0;
        int sign = B(comp < 0);
        int mag = sign != 0 ? -comp : comp;
        int mv_class = av1_get_mv_class(mag - 1, ref offset);
        int d = offset >> 3;         // int mv data
        int fr = (offset >> 1) & 3;  // fractional mv data
        int hp = offset & 1;         // high precision mv data

        // Sign
        aom_write_symbol(w, sign, mvcomp.sign_cdf, 2);

        // Class
        aom_write_symbol(w, mv_class, mvcomp.classes_cdf, MV_CLASSES);

        // Integer bits
        if (mv_class == MV_CLASS_0)
        {
            aom_write_symbol(w, d, mvcomp.class0_cdf, CLASS0_SIZE);
        }
        else
        {
            int i;
            int n = mv_class + CLASS0_BITS - 1;  // number of bits
            for (i = 0; i < n; ++i)
                aom_write_symbol(w, (d >> i) & 1, mvcomp.bits_cdf[i], 2);
        }
        // Fractional bits
        if (precision > MV_SUBPEL_NONE)
        {
            aom_write_symbol(
                w, fr,
                mv_class == MV_CLASS_0 ? mvcomp.class0_fp_cdf[d] : mvcomp.fp_cdf,
                MV_FP_SIZE);
        }

        // High precision bit
        if (precision > MV_SUBPEL_LOW_PRECISION)
            aom_write_symbol(
                w, hp, mv_class == MV_CLASS_0 ? mvcomp.class0_hp_cdf : mvcomp.hp_cdf,
                2);
    }

    /* TODO(siekyleb@amazon.com): This function writes MV_VALS ints or 128 KiB. This
     *   is more than most L1D caches and is a significant chunk of L2. Write
     *   SIMD that uses streaming writes to avoid loading all of that into L1, or
     *   just don't update the larger component costs every time this called
     *   (or both).
     */
    // `mvcost` points to the middle of a MV_VALS array (index range [-MV_MAX, MV_MAX]).
    internal static void av1_build_nmv_component_cost_table(Ptr<int> mvcost,
                                                            nmv_component mvcomp,
                                                            MvSubpelPrecision precision)
    {
        int i, j, v, o, mantissa;
        // C: stack arrays. Called a few times per frame, so the allocations are harmless.
        int[] sign_cost = new int[2], class_cost = new int[MV_CLASSES],
              class0_cost = new int[CLASS0_SIZE];
        int[][] bits_cost = EncArrays.New2<int>(MV_OFFSET_BITS, 2);
        int[][] class0_fp_cost = EncArrays.New2<int>(CLASS0_SIZE, MV_FP_SIZE);
        int[] fp_cost = new int[MV_FP_SIZE];
        int[] class0_hp_cost = new int[2], hp_cost = new int[2];

        av1_cost_tokens_from_cdf(sign_cost, mvcomp.sign_cdf, Ptr<int>.Null);
        av1_cost_tokens_from_cdf(class_cost, mvcomp.classes_cdf, Ptr<int>.Null);
        av1_cost_tokens_from_cdf(class0_cost, mvcomp.class0_cdf, Ptr<int>.Null);
        for (i = 0; i < MV_OFFSET_BITS; ++i)
        {
            av1_cost_tokens_from_cdf(bits_cost[i], mvcomp.bits_cdf[i], Ptr<int>.Null);
        }

        if (precision > MV_SUBPEL_NONE)
        {
            for (i = 0; i < CLASS0_SIZE; ++i)
                av1_cost_tokens_from_cdf(class0_fp_cost[i], mvcomp.class0_fp_cdf[i],
                                         Ptr<int>.Null);
            av1_cost_tokens_from_cdf(fp_cost, mvcomp.fp_cdf, Ptr<int>.Null);
        }

        if (precision > MV_SUBPEL_LOW_PRECISION)
        {
            av1_cost_tokens_from_cdf(class0_hp_cost, mvcomp.class0_hp_cdf, Ptr<int>.Null);
            av1_cost_tokens_from_cdf(hp_cost, mvcomp.hp_cdf, Ptr<int>.Null);
        }

        // Instead of accumulating the cost of each vector component's bits
        //   individually, compute the costs based on smaller vectors. Costs for
        //   [2^exp, 2 * 2^exp - 1] are calculated based on [0, 2^exp - 1]
        //   respectively. Offsets are maintained to swap both 1) class costs when
        //   treated as a complete vector component with the highest set bit when
        //   treated as a mantissa (significand) and 2) leading zeros to account for
        //   the current exponent.

        // Cost offsets
        int[] cost_swap = new int[MV_OFFSET_BITS];
        // Delta to convert positive vector to negative vector costs
        int negate_sign = sign_cost[1] - sign_cost[0];

        // Initialize with offsets to swap the class costs with the costs of the
        //   highest set bit.
        for (i = 1; i < MV_OFFSET_BITS; ++i)
        {
            cost_swap[i] = bits_cost[i - 1][1];
            if (i > CLASS0_BITS) cost_swap[i] -= class_cost[i - CLASS0_BITS];
        }

        // Seed the fractional costs onto the output (overwritten latter).
        for (o = 0; o < MV_FP_SIZE; ++o)
        {
            int hp;
            for (hp = 0; hp < 2; ++hp)
            {
                v = 2 * o + hp + 1;
                mvcost[v] = fp_cost[o] + hp_cost[hp] + sign_cost[0];
            }
        }

        mvcost[0] = 0;
        // Fill the costs for each exponent's vectors, using the costs set in the
        //   previous exponents.
        for (i = 0; i < MV_OFFSET_BITS; ++i)
        {
            int exponent = (2 * MV_FP_SIZE) << i;

            int @class = 0;
            if (i >= CLASS0_BITS)
            {
                @class = class_cost[i - CLASS0_BITS + 1];
            }

            // Iterate through mantissas, keeping track of the location
            //   of the highest set bit for the mantissa.
            // To be clear: in the outer loop, the position of the highest set bit
            //   (exponent) is tracked and, in this loop, the highest set bit of the
            //   mantissa is tracked.
            mantissa = 0;
            for (j = 0; j <= i; ++j)
            {
                for (; mantissa < (2 * MV_FP_SIZE) << j; ++mantissa)
                {
                    int cost = mvcost[mantissa + 1] + @class + cost_swap[j];
                    v = exponent + mantissa + 1;
                    mvcost[v] = cost;
                    mvcost[-v] = cost + negate_sign;
                }
                cost_swap[j] += bits_cost[i][0];
            }
        }

        // Special case to avoid buffer overrun
        {
            int exponent = (2 * MV_FP_SIZE) << MV_OFFSET_BITS;
            int @class = class_cost[MV_CLASSES - 1];
            mantissa = 0;
            for (j = 0; j < MV_OFFSET_BITS; ++j)
            {
                for (; mantissa < (2 * MV_FP_SIZE) << j; ++mantissa)
                {
                    int cost = mvcost[mantissa + 1] + @class + cost_swap[j];
                    v = exponent + mantissa + 1;
                    mvcost[v] = cost;
                    mvcost[-v] = cost + negate_sign;
                }
            }
            // At this point: mantissa = exponent >> 1

            // Manually calculate the final cost offset
            int cost_swap_hi =
                bits_cost[MV_OFFSET_BITS - 1][1] - class_cost[MV_CLASSES - 2];
            for (; mantissa < exponent - 1; ++mantissa)
            {
                int cost = mvcost[mantissa + 1] + @class + cost_swap_hi;
                v = exponent + mantissa + 1;
                mvcost[v] = cost;
                mvcost[-v] = cost + negate_sign;
            }
        }

        // Fill costs for class0 vectors, overwriting previous placeholder values
        //   used for calculating the costs of the larger vectors.
        for (i = 0; i < CLASS0_SIZE; ++i)
        {
            int top = i * 2 * MV_FP_SIZE;
            for (o = 0; o < MV_FP_SIZE; ++o)
            {
                int hp;
                int cost = class0_fp_cost[i][o] + class_cost[0] + class0_cost[i];
                for (hp = 0; hp < 2; ++hp)
                {
                    v = top + 2 * o + hp + 1;
                    mvcost[v] = cost + class0_hp_cost[hp] + sign_cost[0];
                    mvcost[-v] = cost + class0_hp_cost[hp] + sign_cost[1];
                }
            }
        }
    }

    internal static void av1_encode_mv(AV1_COMP cpi, aom_writer w, ThreadData td, in MV mv,
                                       in MV @ref, nmv_context mvctx, int usehp)
    {
        MV diff = new((short)(mv.row - @ref.row), (short)(mv.col - @ref.col));
        MV_JOINT_TYPE j = av1_get_mv_joint(diff);
        // If the mv_diff is zero, then we should have used near or nearest instead.
        Debug.Assert(j != MV_JOINT_ZERO);
        if (cpi.common.features.cur_frame_force_integer_mv)
        {
            usehp = MV_SUBPEL_NONE;
        }
        aom_write_symbol(w, j, mvctx.joints_cdf, MV_JOINTS);
        if (mv_joint_vertical(j) != 0)
            encode_mv_component(w, diff.row, mvctx.comps[0], usehp);

        if (mv_joint_horizontal(j) != 0)
            encode_mv_component(w, diff.col, mvctx.comps[1], usehp);

        // If auto_mv_step_size is enabled then keep track of the largest
        // motion vector component used.
        if (cpi.sf.mv_sf.auto_mv_step_size != 0)
        {
            int maxv = AOMMAX(Math.Abs((int)mv.row), Math.Abs((int)mv.col)) >> 3;
            td.max_mv_magnitude = AOMMAX(maxv, td.max_mv_magnitude);
        }
    }

    // [subset] av1_encode_dv: intra block copy is off (never executed).

    // C: `int *mvcost[2]`, two pointers to the middle of MV_VALS arrays.
    internal static void av1_build_nmv_cost_table(Ptr<int> mvjoint, Ptr<int>[] mvcost,
                                                  nmv_context ctx,
                                                  MvSubpelPrecision precision)
    {
        av1_cost_tokens_from_cdf(mvjoint, ctx.joints_cdf, Ptr<int>.Null);
        av1_build_nmv_component_cost_table(mvcost[0], ctx.comps[0], precision);
        av1_build_nmv_component_cost_table(mvcost[1], ctx.comps[1], precision);
    }

    internal static int_mv av1_get_ref_mv_from_stack(int ref_idx,
                                                     Ptr<MV_REFERENCE_FRAME> ref_frame,
                                                     int ref_mv_idx,
                                                     MB_MODE_INFO_EXT mbmi_ext)
    {
        sbyte ref_frame_type = av1_ref_frame_type(ref_frame);
        CANDIDATE_MV[] curr_ref_mv_stack =
            mbmi_ext.ref_mv_stack[ref_frame_type];

        if (ref_frame[1] > INTRA_FRAME)
        {
            Debug.Assert(ref_idx == 0 || ref_idx == 1);
            return ref_idx != 0 ? curr_ref_mv_stack[ref_mv_idx].comp_mv
                                : curr_ref_mv_stack[ref_mv_idx].this_mv;
        }

        Debug.Assert(ref_idx == 0);
        return ref_mv_idx < mbmi_ext.ref_mv_count[ref_frame_type]
                   ? curr_ref_mv_stack[ref_mv_idx].this_mv
                   : mbmi_ext.global_mvs[ref_frame_type];
    }

    internal static int_mv av1_get_ref_mv(MACROBLOCK x, int ref_idx)
    {
        MACROBLOCKD xd = x.e_mbd;
        MB_MODE_INFO mbmi = xd.mi[0];
        int ref_mv_idx = mbmi.ref_mv_idx;
        if (mbmi.mode == NEAR_NEWMV || mbmi.mode == NEW_NEARMV)
        {
            Debug.Assert(has_second_ref(mbmi) != 0);
            ref_mv_idx += 1;
        }
        return av1_get_ref_mv_from_stack(ref_idx, mbmi.ref_frame, ref_mv_idx,
                                         x.mbmi_ext);
    }

    // C: stack array `MV_REFERENCE_FRAME ref_frames[2]` of the leaf function below.
    [ThreadStatic] private static MV_REFERENCE_FRAME[] s_ref_frames;

    internal static void av1_find_best_ref_mvs_from_stack(int allow_hp,
                                                          MB_MODE_INFO_EXT mbmi_ext,
                                                          MV_REFERENCE_FRAME ref_frame,
                                                          ref int_mv nearest_mv,
                                                          ref int_mv near_mv,
                                                          int is_integer)
    {
        const int ref_idx = 0;
        MV_REFERENCE_FRAME[] ref_frames = s_ref_frames ??= new MV_REFERENCE_FRAME[2];
        ref_frames[0] = ref_frame;
        ref_frames[1] = NONE_FRAME;
        nearest_mv = av1_get_ref_mv_from_stack(ref_idx, ref_frames, 0, mbmi_ext);
        lower_mv_precision(ref nearest_mv.as_mv, allow_hp, is_integer);
        near_mv = av1_get_ref_mv_from_stack(ref_idx, ref_frames, 1, mbmi_ext);
        lower_mv_precision(ref near_mv.as_mv, allow_hp, is_integer);
    }
}
