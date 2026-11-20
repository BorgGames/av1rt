#!/usr/bin/env python3
"""Expands the per-block-size macro instantiations of aom_dsp/sad.c and aom_dsp/variance.c
(work package e5).

libaom generates dozens of functions per file with macros (SADMXN, SADMXN_AVG, SADMXN_SKIP,
SAD_MXNX4D, SAD_MXNX3D, VAR, SUBPIX_VAR, SUBPIX_AVG_VAR, MSE). This script writes their C#
expansions, each `_c` function followed by its aom_dsp_rtcd.h dispatched name as a forwarder,
into the regions between marker comments in

    src/Av1rt/Aom/aom_dsp/sad.c.cs        // <gen:sad_sizes> ... // </gen:sad_sizes>
    src/Av1rt/Aom/aom_dsp/variance.c.cs   // <gen:variance_sizes> ... // </gen:variance_sizes>

The size lists mirror the macro invocations of the C files with CONFIG_REALTIME_ONLY = 1
(the 4:1 / 1:4 sizes are compiled out) and CONFIG_AV1_HIGHBITDEPTH = 0.

Run from anywhere:  python -I tools/gen/dsp_sizes.py
"""

import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DSP = os.path.join(ROOT, "src", "Av1rt", "Aom", "aom_dsp")

# ---- sad.c ------------------------------------------------------------------------------------
# (m, n, kind) in C order. kind: ALL = SADMXN + _AVG + _SKIP, NO_SKIP = SADMXN + _AVG,
# PLAIN = SADMXN only. x4d: True = SAD_MXNX4D (with skip x4d), False = SAD_MXNX4D_NO_SKIP.
SAD_SIZES = [
    (128, 128, "ALL", True),
    (128, 64, "ALL", True),
    (64, 128, "ALL", True),
    (64, 64, "ALL", True),
    (64, 32, "ALL", True),
    (32, 64, "ALL", True),
    (32, 32, "ALL", True),
    (32, 16, "ALL", True),
    (16, 32, "ALL", True),
    (16, 16, "ALL", True),
    (16, 8, "NO_SKIP", False),
    (8, 16, "ALL", True),
    (8, 8, "NO_SKIP", False),
    (8, 4, "PLAIN", False),
    (4, 8, "PLAIN", False),
    (4, 4, "PLAIN", False),
]

# ---- variance.c -------------------------------------------------------------------------------
VARIANCE_SIZES = [
    (128, 128), (128, 64), (64, 128), (64, 64), (64, 32), (32, 64), (32, 32), (32, 16),
    (16, 32), (16, 16), (16, 8), (8, 16), (8, 8), (8, 4), (4, 8), (4, 4),
]
MSE_SIZES = [(16, 16), (16, 8), (8, 16), (8, 8)]

AGGR = "[MethodImpl(MethodImplOptions.AggressiveInlining)]"


def sadmxn(m, n):
    return f"""
    internal static uint aom_sad{m}x{n}_c(Ptr<byte> src, int src_stride,
                                       Ptr<byte> @ref, int ref_stride)
    {{
        return sad(src, src_stride, @ref, ref_stride, {m}, {n});
    }}

    {AGGR}
    internal static uint aom_sad{m}x{n}(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                     int ref_stride) =>
        aom_sad{m}x{n}_c(src_ptr, src_stride, ref_ptr, ref_stride);
"""


def sadmxn_avg(m, n):
    return f"""
    internal static uint aom_sad{m}x{n}_avg_c(Ptr<byte> src, int src_stride,
                                           Ptr<byte> @ref, int ref_stride,
                                           Ptr<byte> second_pred)
    {{
        Ptr<byte> comp_pred = comp_pred_buf();  // uint8_t comp_pred[m * n]
        aom_comp_avg_pred(comp_pred, second_pred, {m}, {n}, @ref, ref_stride);
        return sad(src, src_stride, comp_pred, {m}, {m}, {n});
    }}

    {AGGR}
    internal static uint aom_sad{m}x{n}_avg(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                         int ref_stride, Ptr<byte> second_pred) =>
        aom_sad{m}x{n}_avg_c(src_ptr, src_stride, ref_ptr, ref_stride, second_pred);
"""


def sadmxn_skip(m, n):
    return f"""
    internal static uint aom_sad_skip_{m}x{n}_c(Ptr<byte> src, int src_stride,
                                             Ptr<byte> @ref,
                                             int ref_stride)
    {{
        return 2 * sad(src, 2 * src_stride, @ref, 2 * ref_stride, ({m}), ({n} / 2));
    }}

    {AGGR}
    internal static uint aom_sad_skip_{m}x{n}(Ptr<byte> src_ptr, int src_stride, Ptr<byte> ref_ptr,
                                           int ref_stride) =>
        aom_sad_skip_{m}x{n}_c(src_ptr, src_stride, ref_ptr, ref_stride);
"""


def sad_mxnx4d_no_skip(m, n):
    return f"""
    // Calculate sad against 4 reference locations and store each in sad_array
    internal static void aom_sad{m}x{n}x4d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {{
        int i;
        for (i = 0; i < 4; ++i)
        {{
            sad_array[i] =
                aom_sad{m}x{n}_c(src, src_stride, ref_array[i], ref_stride);
        }}
    }}

    {AGGR}
    internal static void aom_sad{m}x{n}x4d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array) =>
        aom_sad{m}x{n}x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
"""


def sad_skip_mxnx4d(m, n):
    return f"""
    internal static void aom_sad_skip_{m}x{n}x4d_c(Ptr<byte> src, int src_stride,
                                                Ptr<Ptr<byte>> ref_array,
                                                int ref_stride, Ptr<uint> sad_array)
    {{
        int i;
        for (i = 0; i < 4; ++i)
        {{
            sad_array[i] = 2 * sad(src, 2 * src_stride, ref_array[i],
                                   2 * ref_stride, ({m}), ({n} / 2));
        }}
    }}

    {AGGR}
    internal static void aom_sad_skip_{m}x{n}x4d(Ptr<byte> src_ptr, int src_stride,
                                              Ptr<Ptr<byte>> ref_ptr, int ref_stride,
                                              Ptr<uint> sad_array) =>
        aom_sad_skip_{m}x{n}x4d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
"""


def sad_mxnx3d(m, n):
    return f"""
    // Call SIMD version of aom_sad_mxnx4d if the 3d version is unavailable.
    internal static void aom_sad{m}x{n}x3d_c(Ptr<byte> src, int src_stride,
                                          Ptr<Ptr<byte>> ref_array,
                                          int ref_stride, Ptr<uint> sad_array)
    {{
        aom_sad{m}x{n}x4d(src, src_stride, ref_array, ref_stride, sad_array);
    }}

    {AGGR}
    internal static void aom_sad{m}x{n}x3d(Ptr<byte> src_ptr, int src_stride, Ptr<Ptr<byte>> ref_ptr,
                                        int ref_stride, Ptr<uint> sad_array) =>
        aom_sad{m}x{n}x3d_c(src_ptr, src_stride, ref_ptr, ref_stride, sad_array);
"""


def gen_sad():
    out = []
    for m, n, kind, x4d_skip in SAD_SIZES:
        out.append(f"\n    // {m}x{n}")
        out.append(sadmxn(m, n))
        if kind in ("ALL", "NO_SKIP"):
            out.append(sadmxn_avg(m, n))
        if kind == "ALL":
            out.append(sadmxn_skip(m, n))
        out.append(sad_mxnx4d_no_skip(m, n))
        if x4d_skip:
            out.append(sad_skip_mxnx4d(m, n))
        out.append(sad_mxnx3d(m, n))
    return "".join(out)


def var(w, h):
    return f"""
    internal static uint aom_variance{w}x{h}_c(Ptr<byte> a, int a_stride,
                                            Ptr<byte> b, int b_stride,
                                            ref uint sse)
    {{
        int sum = 0;
        variance(a, a_stride, b, b_stride, {w}, {h}, ref sse, ref sum);
        return sse - (uint)(((long)sum * sum) / ({w} * {h}));
    }}

    {AGGR}
    internal static uint aom_variance{w}x{h}(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                          int ref_stride, ref uint sse) =>
        aom_variance{w}x{h}_c(src_ptr, source_stride, ref_ptr, ref_stride, ref sse);
"""


def subpix_var(w, h):
    return f"""
    internal static uint aom_sub_pixel_variance{w}x{h}_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse)
    {{
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, {h} + 1, {w},
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, {w}, {w}, {h}, {w},
                                             bilinear_filters_2t[yoffset]);

        return aom_variance{w}x{h}_c(temp2, {w}, b, b_stride, ref sse);
    }}

    {AGGR}
    internal static uint aom_sub_pixel_variance{w}x{h}(Ptr<byte> src_ptr, int source_stride,
                                                    int xoffset, int yoffset, Ptr<byte> ref_ptr,
                                                    int ref_stride, ref uint sse) =>
        aom_sub_pixel_variance{w}x{h}_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                       ref_stride, ref sse);
"""


def subpix_avg_var(w, h):
    return f"""
    internal static uint aom_sub_pixel_avg_variance{w}x{h}_c(
        Ptr<byte> a, int a_stride, int xoffset, int yoffset,
        Ptr<byte> b, int b_stride, ref uint sse,
        Ptr<byte> second_pred)
    {{
        Ptr<ushort> fdata3 = fdata3_buf();  // uint16_t fdata3[(H + 1) * W]
        Ptr<byte> temp2 = temp2_buf();      // uint8_t temp2[H * W]
        Ptr<byte> temp3 = temp3_buf();      // DECLARE_ALIGNED(16, uint8_t, temp3[H * W])

        var_filter_block2d_bil_first_pass_c(a, fdata3, (uint)a_stride, 1, {h} + 1, {w},
                                            bilinear_filters_2t[xoffset]);
        var_filter_block2d_bil_second_pass_c(fdata3, temp2, {w}, {w}, {h}, {w},
                                             bilinear_filters_2t[yoffset]);

        aom_comp_avg_pred(temp3, second_pred, {w}, {h}, temp2, {w});

        return aom_variance{w}x{h}_c(temp3, {w}, b, b_stride, ref sse);
    }}

    {AGGR}
    internal static uint aom_sub_pixel_avg_variance{w}x{h}(Ptr<byte> src_ptr, int source_stride,
                                                        int xoffset, int yoffset,
                                                        Ptr<byte> ref_ptr, int ref_stride,
                                                        ref uint sse, Ptr<byte> second_pred) =>
        aom_sub_pixel_avg_variance{w}x{h}_c(src_ptr, source_stride, xoffset, yoffset, ref_ptr,
                                           ref_stride, ref sse, second_pred);
"""


def mse(w, h):
    return f"""
    internal static uint aom_mse{w}x{h}_c(Ptr<byte> a, int a_stride,
                                       Ptr<byte> b, int b_stride,
                                       ref uint sse)
    {{
        int sum = 0;
        variance(a, a_stride, b, b_stride, {w}, {h}, ref sse, ref sum);
        return sse;
    }}

    {AGGR}
    internal static uint aom_mse{w}x{h}(Ptr<byte> src_ptr, int source_stride, Ptr<byte> ref_ptr,
                                     int recon_stride, ref uint sse) =>
        aom_mse{w}x{h}_c(src_ptr, source_stride, ref_ptr, recon_stride, ref sse);
"""


def gen_variance():
    out = ["\n    /* All three forms of the variance are available in the same sizes. */"]
    for w, h in VARIANCE_SIZES:
        out.append(f"\n    // VARIANCES({w}, {h})")
        out.append(var(w, h))
        out.append(subpix_var(w, h))
        out.append(subpix_avg_var(w, h))
    out.append("\n    /* Identical to the variance call except it does not calculate the")
    out.append("\n     * sse - sum^2 / w*h and returns sse in addtion to modifying the passed in")
    out.append("\n     * variable.")
    out.append("\n     */")
    for w, h in MSE_SIZES:
        out.append(mse(w, h))
    return "".join(out)


def rewrite(path, region, body):
    with open(path, encoding="utf-8") as f:
        src = f.read()
    begin, end = "// <gen:%s>" % region, "// </gen:%s>" % region
    pat = re.compile(r"([ \t]*" + re.escape(begin) + r")\n(?:.*?\n)?([ \t]*" + re.escape(end) + ")",
                     re.S)
    if not pat.search(src):
        raise SystemExit("region %s not found in %s" % (region, path))
    src = pat.sub(lambda m: m.group(1) + body.rstrip("\n") + "\n" + m.group(2), src, count=1)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(src)


def main():
    rewrite(os.path.join(DSP, "sad.c.cs"), "sad_sizes", gen_sad())
    rewrite(os.path.join(DSP, "variance.c.cs"), "variance_sizes", gen_variance())


if __name__ == "__main__":
    main()
