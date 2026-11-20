#!/usr/bin/env python3
"""Generates the tables of work package e1 (entropy) from the libaom sources.

Reuses the C parser of ctables.py and rewrites the `// <gen:NAME>` regions of:

  src/Av1rt/Aom/av1/common/token_cdfs.h.cs     default coefficient CDFs (token_cdfs.h)
  src/Av1rt/Aom/av1/common/entropymode.c.cs    default mode CDFs (entropymode.c)
  src/Av1rt/Aom/av1/common/entropymv.c.cs      default_nmv_context (entropymv.c)
  src/Av1rt/Aom/av1/common/scan.c.cs           scan / iscan tables and av1_scan_orders (scan.c)
  src/Av1rt/Aom/av1/common/txb_common.c.cs     padded index / nz-map context / eob tables
  src/Av1rt/Aom/av1/common/quant_common.c.cs   dc/ac quantizer lookups
  src/Av1rt/Aom/av1/common/seg_common.c.cs     segment feature limits
  src/Av1rt/Aom/av1/encoder/cost.c.cs          av1_prob_cost

CDF tables are emitted as their C memory image: the AOM_CDFn / AOM_ICDF macros of
aom_dsp/prob.h are expanded and evaluated, short initializers are zero-filled exactly as C does
(including brace elision), and the flat values are reshaped into the jagged C# arrays that
FRAME_CONTEXT uses (jagged_from_flat). Values are never typed by hand.

Run from anywhere (works in a git worktree too):  python -I tools/gen/e1_tables.py
"""

import os
import re
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))


def main_checkout():
    common = subprocess.check_output(
        ["git", "-C", ROOT, "rev-parse", "--path-format=absolute", "--git-common-dir"],
        text=True).strip()
    return os.path.dirname(common)


MAIN = main_checkout()
AOM = os.path.join(os.path.dirname(MAIN), "aom")

# ctables.py reads <its checkout>/.oracle/build/config/aom_config.h when imported; in a worktree
# that (git-ignored) file only exists in the main checkout.
_cfg = os.path.join(ROOT, ".oracle", "build", "config", "aom_config.h")
if not os.path.exists(_cfg):
    os.makedirs(os.path.dirname(_cfg), exist_ok=True)
    shutil.copy(os.path.join(MAIN, ".oracle", "build", "config", "aom_config.h"), _cfg)

sys.dont_write_bytecode = True  # no __pycache__ in the source tree
sys.path.insert(0, HERE)
import ctables as ct  # noqa: E402

ct.AOM = AOM

CS = os.path.join(ROOT, "src", "Av1rt", "Aom")
PROB_H = "aom_dsp/prob.h"
CDF_MACROS = ["AOM_CDF%d" % n for n in range(16, 1, -1)] + ["AOM_ICDF"]

# --------------------------------------------------------------------------------------------
# C initializer -> flat memory image (with C zero fill and brace elision)
# --------------------------------------------------------------------------------------------


class Scalar:
    size = 1

    def members(self):
        raise TypeError("scalar has no members")


class Array:
    def __init__(self, n, elem):
        self.n, self.elem = n, elem
        self.size = n * elem.size

    def members(self):
        return [self.elem] * self.n


class Struct:
    def __init__(self, fields):
        self.fields = fields  # [(name, type)]
        self.size = sum(t.size for _, t in fields)

    def members(self):
        return [t for _, t in self.fields]


SCALAR = Scalar()


def array_type(dims, elem=SCALAR):
    t = elem
    for d in reversed(dims):
        t = Array(d, t)
    return t


def eval_scalar(s):
    v = ct.eval_int(s)
    if v is None:
        raise ValueError("cannot evaluate initializer value: " + s)
    return v


def fill_from(t, items, cur):
    """Initializes one object of type t from items[cur[0]:] (C 6.7.9 brace rules)."""
    it = items[cur[0]]
    if isinstance(it, tuple) or it is None:
        raise ValueError("designated initializers are not supported")
    if isinstance(t, Scalar):
        cur[0] += 1
        if isinstance(it, list):  # braced scalar
            if len(it) != 1 or isinstance(it[0], list):
                raise ValueError("bad braced scalar")
            return [eval_scalar(it[0])]
        return [eval_scalar(it)]
    if isinstance(it, list):  # braced sub-aggregate: initializes exactly this object
        cur[0] += 1
        return init_aggregate(it, t)
    return fill_members(t, items, cur)  # brace elision: members come from the same list


def fill_members(t, items, cur):
    out = []
    for mt in t.members():
        if cur[0] >= len(items):
            out.extend([0] * mt.size)  # members without initializer are zero
        else:
            out.extend(fill_from(mt, items, cur))
    return out


def init_aggregate(items, t):
    cur = [0]
    out = fill_members(t, items, cur)
    if cur[0] != len(items):
        raise ValueError("excess initializers (%d of %d used)" % (cur[0], len(items)))
    return out


def raw_initializer(rel, name):
    """(dims, initializer node) of a C table, with the CDF macros of prob.h expanded."""
    text = ct.source(rel)
    pat = re.compile(r"\b%s\s*((?:\[[^\]]*\]\s*)*)\)?\s*=\s*\{" % re.escape(name))
    m = pat.search(text)
    if not m:
        raise ValueError("table not found: %s in %s" % (name, rel))
    dims = [re.sub(r"\s+\)", ")", re.sub(r"\(\s+", "(", " ".join(d.split())))
            for d in re.findall(r"\[([^\]]*)\]", m.group(1))]
    brace = m.end() - 1
    _, end = ct.parse_init(text, brace)
    init = text[brace:end]
    for names in (CDF_MACROS[:-1], CDF_MACROS[-1:]):  # AOM_CDFn first; they expand to AOM_ICDF
        init = ct.expand_macros(init, PROB_H, [n for n in names if re.search(r"\b%s\s*\(" % n, init)])
    node, _ = ct.parse_init(init, 0)
    return dims, node


def eval_dims(dims, name):
    out = []
    for d in dims:
        v = ct.eval_int(d)
        if v is None:
            raise ValueError("%s: cannot evaluate dimension [%s]" % (name, d))
        out.append(v)
    return out


def flat_lines(values, row, indent="        "):
    """Source lines of the flat values, one innermost row per line (wrapped)."""
    lines = []
    for r in range(0, len(values), row):
        lines.extend(ct.wrap([str(v) for v in values[r : r + row]], indent))
        lines[-1] += ","
    lines[-1] = lines[-1].rstrip(",")
    return lines


def cdf_table(rel, name, access="private"):
    dims, node = raw_initializer(rel, name)
    dims_v = eval_dims(dims, name)
    values = init_aggregate(node, array_type(dims_v))
    for v in values:
        if not 0 <= v <= 32768:
            raise ValueError("%s: CDF value out of range: %d" % (name, v))
    cs_dims = ", ".join(ct.cs_expr(d) for d in dims)
    out = ["    // aom_cdf_prob %s%s" % (name, "".join("[%s]" % d for d in dims))]
    t = "aom_cdf_prob" + "[]" * len(dims)
    if len(dims) == 1:
        out.append("    %s static readonly %s %s = {" % (access, t, name))
        out.extend(flat_lines(values, dims_v[-1]))
        out.append("    };")
    else:
        out.append("    %s static readonly %s %s = jagged_from_flat(new aom_cdf_prob[] {" % (access, t, name))
        out.extend(flat_lines(values, dims_v[-1]))
        out.append("    }, %s);" % cs_dims)
    return "\n".join(out)


# --------------------------------------------------------------------------------------------
# default_nmv_context (struct initializer)
# --------------------------------------------------------------------------------------------


def struct_type(rel, struct_re, known):
    text = ct.source(rel)
    m = re.search(struct_re, text, re.S)
    fields = []
    for decl in m.group(1).split(";"):
        decl = " ".join(decl.split())
        if not decl:
            continue
        mm = re.match(r"(?:struct\s+)?(\w+)\s+(\w+)\s*((?:\[[^\]]*\]\s*)*)$", decl)
        ctype, fname, dimtxt = mm.groups()
        dims = eval_dims(re.findall(r"\[([^\]]*)\]", dimtxt), fname)
        elem = SCALAR if ctype == "aom_cdf_prob" else known[ctype]
        fields.append((fname, array_type(dims, elem)))
    return Struct(fields)


def nmv_default():
    rel = "av1/common/entropymv.h"
    comp = struct_type(rel, r"typedef struct \{([^{}]*)\} nmv_component;", {})
    ctx = struct_type(rel, r"typedef struct \{([^{}]*)\} nmv_context;", {"nmv_component": comp})
    _, node = raw_initializer("av1/common/entropymv.c", "default_nmv_context")
    values = init_aggregate(node, ctx)
    out = ["    // nmv_context default_nmv_context (flat C memory image, see nmv_context_from_flat)"]
    out.append("    private static readonly nmv_context default_nmv_context = nmv_context_from_flat(new aom_cdf_prob[] {")
    out.extend(flat_lines(values, 16))
    out.append("    });")
    return "\n".join(out)


# --------------------------------------------------------------------------------------------
# scan.c / txb_common.c pointer tables
# --------------------------------------------------------------------------------------------


def tx_size_names():
    for e in ct.parse_enums(ct.source("aom_dsp/txfm_common.h")):
        names = [n for n, _ in e["values"]]
        if names and names[0] == "TX_4X4":
            return names
    raise ValueError("TX_SIZE enum not found")


def scan_orders():
    _, _, node = ct.find_table("av1/common/scan.c", "av1_scan_orders")
    names = tx_size_names()
    out = ["    // const SCAN_ORDER av1_scan_orders[TX_SIZES_ALL][TX_TYPES]",
           "    internal static readonly SCAN_ORDER[][] av1_scan_orders = {"]
    if len(node) != eval_dims(["TX_SIZES_ALL"], "av1_scan_orders")[0]:
        raise ValueError("av1_scan_orders: unexpected row count")
    for k, row in enumerate(node):
        if len(row) != 16:
            raise ValueError("av1_scan_orders: unexpected column count")
        out.append("        new SCAN_ORDER[] {")
        out.append("            // %s" % names[k])
        for j, (s, i) in enumerate(row):
            out.append("            new(%s, %s)%s" % (s, i, "," if j < len(row) - 1 else ""))
        out.append("        }%s" % ("," if k < len(node) - 1 else ""))
    out.append("    };")
    return "\n".join(out)


def pointer_table(rel, name, cs_t):
    ctype, dims, node = ct.find_table(rel, name)
    n = eval_dims(dims, name)[0]
    if len(node) != n:
        raise ValueError("%s: %d of %d entries" % (name, len(node), n))
    out = ["    // %s *const %s[%s]" % (ctype, name, dims[0]),
           "    internal static readonly %s[] %s = {" % (cs_t, name)]
    out.extend(ct.wrap(node, "        "))
    out.append("    };")
    return "\n".join(out)


def tables_of(rel, prefix_re, access="private"):
    """All `static const` 1-D tables of a file whose name matches prefix_re, in C order."""
    text = ct.source(rel)
    names = re.findall(r"(?:const\s+\w+\s*,\s*|const\s+\w+\s+)(%s\w*)\s*\[" % prefix_re, text)
    return [ct.emit_table(dict(h=rel, name=n, access=access)) for n in names]


# --------------------------------------------------------------------------------------------
# What to generate
# --------------------------------------------------------------------------------------------

EM = "av1/common/entropymode.c"
TOKEN_CDFS = [
    "av1_default_dc_sign_cdfs", "av1_default_txb_skip_cdfs", "av1_default_eob_extra_cdfs",
    "av1_default_eob_multi16_cdfs", "av1_default_eob_multi32_cdfs", "av1_default_eob_multi64_cdfs",
    "av1_default_eob_multi128_cdfs", "av1_default_eob_multi256_cdfs",
    "av1_default_eob_multi512_cdfs", "av1_default_eob_multi1024_cdfs",
    "av1_default_coeff_lps_multi_cdfs", "av1_default_coeff_base_multi_cdfs",
    "av1_default_coeff_base_eob_multi_cdfs",
]


def entropymode_cdf_names():
    text = ct.source(EM)
    return re.findall(r"static\s+const\s+aom_cdf_prob\s+(default_\w+)", text)


def gen_regions():
    def T(rel, name, **kw):
        return ct.emit_table(dict(h=rel, name=name, **kw))

    return {
        "av1/common/token_cdfs.h.cs": {
            "tables": lambda: [cdf_table("av1/common/token_cdfs.h", n, access="internal") for n in TOKEN_CDFS],
        },
        "av1/common/entropymode.c.cs": {
            "tables": lambda: [cdf_table(EM, n) for n in entropymode_cdf_names()],
        },
        "av1/common/entropymv.c.cs": {
            "tables": lambda: [nmv_default()],
        },
        "av1/common/scan.c.cs": {
            "tables": lambda: tables_of("av1/common/scan.c", r"(?:default_scan|mcol_scan|mrow_scan|av1_\w*iscan)")
            + [scan_orders()],
        },
        "av1/common/txb_common.c.cs": {
            "padded_idx": lambda: tables_of("av1/common/txb_common.c", r"padded_idx_h")
            + [pointer_table("av1/common/txb_common.c", "av1_txb_padded_idx", "Ptr<short>")],
            "nz_map_ctx_offset": lambda: tables_of("av1/common/txb_common.c", r"av1_nz_map_ctx_offset_")
            + [pointer_table("av1/common/txb_common.c", "av1_nz_map_ctx_offset", "Ptr<sbyte>"),
               T("av1/common/txb_common.c", "av1_eob_group_start"),
               T("av1/common/txb_common.c", "av1_eob_offset_bits")],
        },
        "av1/common/quant_common.c.cs": {
            "tables": lambda: [T("av1/common/quant_common.c", n, access="private") for n in (
                "dc_qlookup_QTX", "dc_qlookup_10_QTX", "dc_qlookup_12_QTX",
                "ac_qlookup_QTX", "ac_qlookup_10_QTX", "ac_qlookup_12_QTX")],
        },
        "av1/common/seg_common.c.cs": {
            "tables": lambda: [T("av1/common/seg_common.c", "seg_feature_data_signed", access="private"),
                               T("av1/common/seg_common.c", "seg_feature_data_max", access="private")],
        },
        "av1/encoder/cost.c.cs": {
            "tables": lambda: [T("av1/encoder/cost.c", "av1_prob_cost")],
        },
    }


def main():
    ct.collect_symbols()
    failed = False
    for fname, regions in gen_regions().items():
        path = os.path.join(CS, *fname.split("/"))
        with open(path, encoding="utf-8") as f:
            text = f.read()
        orig = text
        for region, fn in regions.items():
            begin, end = "// <gen:%s>" % region, "// </gen:%s>" % region
            a, b = text.find(begin), text.find(end)
            if a < 0 or b < 0:
                print("%s: missing markers for %s" % (fname, region), file=sys.stderr)
                failed = True
                continue
            try:
                body = "\n\n".join(fn())
            except Exception as ex:  # report and keep going
                print("%s/%s: %s" % (fname, region, ex), file=sys.stderr)
                failed = True
                continue
            line_start = text.rfind("\n", 0, a) + 1
            indent = text[line_start:a]
            text = text[: a + len(begin)] + "\n" + body + "\n" + indent + text[b:]
        if text != orig:
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write(text)
            print("wrote", os.path.relpath(path, ROOT))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
