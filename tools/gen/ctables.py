#!/usr/bin/env python3
"""Generates the mechanical parts of the av1/common header ports (work package p1a).

It reads the libaom headers in ../aom (sibling of this repo, read-only) and rewrites the regions
between marker comments in src/Av1rt/Aom/av1/common/*.h.cs:

    // <gen:NAME>
    ...generated, do not edit...
    // </gen:NAME>

Generated regions:
  * enum aliases and enumerator constants (`global using X = int;` + `internal const int`),
  * static const tables (values copied from the C initializers, never retyped; C zero-fill of
    short initializers is reproduced and the element count is checked against the C dimensions),
  * classes that hold CDF arrays (FRAME_CONTEXT, nmv_context, ...): jagged ushort arrays with the
    exact C dimensions, allocated at construction, plus a deep CopyFrom.

Run from anywhere:  python -I tools/gen/ctables.py
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
AOM = os.path.join(os.path.dirname(ROOT), "aom")
CS_DIR = os.path.join(ROOT, "src", "Av1rt", "Aom", "av1", "common")
CONFIG_H = os.path.join(ROOT, ".oracle", "build", "config", "aom_config.h")

# --------------------------------------------------------------------------------------------
# Minimal C preprocessing: comments, continuations, #if evaluation against the oracle config.
# --------------------------------------------------------------------------------------------


def read_config():
    cfg = {}
    with open(CONFIG_H, encoding="utf-8") as f:
        for line in f:
            m = re.match(r"\s*#define\s+(\w+)\s+(\d+)", line)
            if m:
                cfg[m.group(1)] = int(m.group(2))
    return cfg


CONFIG = read_config()


def strip_comments(text):
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if text.startswith("/*", i):
            j = text.find("*/", i + 2)
            # keep line structure so directives stay on their own lines
            out.append("\n" * text.count("\n", i, j))
            i = j + 2
        elif text.startswith("//", i):
            j = text.find("\n", i)
            i = n if j < 0 else j
        elif c == '"':
            j = i + 1
            while text[j] != '"':
                j += 2 if text[j] == "\\" else 1
            out.append(text[i : j + 1])
            i = j + 1
        else:
            out.append(c)
            i += 1
    return "".join(out)


def eval_pp_condition(expr, defined, values=None):
    expr = re.sub(r"defined\s*\(\s*(\w+)\s*\)", lambda m: "1" if m.group(1) in defined else "0", expr)
    expr = re.sub(r"defined\s+(\w+)", lambda m: "1" if m.group(1) in defined else "0", expr)

    # function-like macros in conditions (__has_attribute(x), ...) are unknown: 0
    expr = re.sub(r"\b[A-Za-z_]\w*\s*\([^()]*\)", "0", expr)

    def ident(m):
        name = m.group(0)
        if name in CONFIG:
            return str(CONFIG[name])
        if values and name in values:
            return "(" + values[name] + ")"  # object-like macro defined earlier
        return "0"  # C: unknown identifiers evaluate to 0

    for _ in range(10):
        expanded = re.sub(r"\b[A-Za-z_]\w*\b", ident, expr)
        if expanded == expr:
            break
        expr = expanded
    expr = expr.replace("&&", " and ").replace("||", " or ")
    expr = re.sub(r"!(?!=)", " not ", expr)
    return bool(eval(expr, {"__builtins__": {}}, {}))


def preprocess(text):
    """Returns the active source text (comments removed, inactive #if branches dropped).
    #define lines in active regions are kept; other directives are dropped."""
    text = strip_comments(text).replace("\\\n", " ")
    defined = set(CONFIG.keys())
    values = {}
    out = []
    stack = []  # entries: [active_now, taken_any, parent_active]
    active = True
    for line in text.split("\n"):
        s = line.strip()
        m = re.match(r"#\s*(\w+)\s*(.*)", s)
        if m:
            d, rest = m.group(1), m.group(2)
            if d in ("if", "ifdef", "ifndef"):
                if d == "if":
                    cond = eval_pp_condition(rest, defined, values)
                elif d == "ifdef":
                    cond = rest.split()[0] in defined
                else:
                    cond = rest.split()[0] not in defined
                stack.append([active and cond, cond, active])
                active = active and cond
            elif d == "elif":
                top = stack[-1]
                cond = (not top[1]) and eval_pp_condition(rest, defined, values)
                top[0] = top[2] and cond
                top[1] = top[1] or cond
                active = top[0]
            elif d == "else":
                top = stack[-1]
                top[0] = top[2] and not top[1]
                top[1] = True
                active = top[0]
            elif d == "endif":
                stack.pop()
                active = stack[-1][0] if stack else True
            elif d == "define" and active:
                name = rest.split("(")[0].split()[0]
                defined.add(name)
                mm = re.match(r"(\w+)\s+(.+)$", rest)
                if mm:  # object-like (a function-like one has '(' right after the name)
                    values[name] = mm.group(2)
                out.append(s)
            continue
        if active:
            out.append(line)
    return "\n".join(out)


_pp_cache = {}


def source(rel):
    if rel not in _pp_cache:
        with open(os.path.join(AOM, rel), encoding="utf-8") as f:
            _pp_cache[rel] = preprocess(f.read())
    return _pp_cache[rel]


# --------------------------------------------------------------------------------------------
# Symbol table (enum constants and object-like integer #defines) for evaluating dimensions.
# --------------------------------------------------------------------------------------------

SYMBOL_HEADERS = [
    "aom_dsp/txfm_common.h", "aom_dsp/prob.h", "aom_dsp/aom_filter.h", "aom/aom_image.h",
    "aom_ports/mem.h", "aom_dsp/flow_estimation/flow_estimation.h", "aom_scale/yv12config.h",
    "av1/common/enums.h", "av1/common/common_data.h", "av1/common/entropy.h",
    "av1/common/entropymode.h", "av1/common/entropymv.h", "av1/common/seg_common.h",
    "av1/common/filter.h", "av1/common/mv.h", "av1/common/scale.h", "av1/common/quant_common.h",
    "av1/common/av1_txfm.h", "av1/common/av1_loopfilter.h", "av1/common/cdef.h",
    "av1/common/cdef_block.h", "av1/common/restoration.h", "av1/common/blockd.h",
    "av1/common/av1_common_int.h", "av1/common/reconinter.h", "av1/common/reconintra.h",
    "av1/common/mvref_common.h", "av1/common/txb_common.h", "av1/common/cfl.h",
    "av1/common/scan.h", "av1/common/warped_motion.h", "av1/common/resize.h",
    "av1/common/av1_inv_txfm1d_cfg.h",
]

SYMS = {}  # name -> C expression string
_sym_values = {}


def collect_symbols():
    for rel in SYMBOL_HEADERS:
        text = source(rel)
        for m in re.finditer(r"^#define[ \t]+(\w+)[ \t]+(.+)$", text, re.M):
            name, val = m.group(1), m.group(2).strip()
            SYMS.setdefault(name, val)
        for e in parse_enums(text):
            prev = None
            for name, val in e["values"]:
                if val is None:
                    val = "0" if prev is None else "(%s) + 1" % prev
                SYMS.setdefault(name, val)
                prev = name


def c_to_py_expr(expr):
    expr = re.sub(r"\b(0[xX][0-9a-fA-F]+|\d+)[uUlL]+\b", r"\1", expr)
    expr = re.sub(r"\((?:int|unsigned|int8_t|uint8_t|int16_t|uint16_t|int32_t|uint32_t|int64_t)\)", "", expr)
    expr = expr.replace("/", "//").replace("&&", " and ").replace("||", " or ")
    return expr


def eval_int(expr, depth=0):
    """Evaluates a C integer constant expression using SYMS; returns None if unknown."""
    if depth > 50:
        return None
    expr = expr.strip()
    expr = re.sub(r"\bCDF_SIZE\s*\(", "(1+", expr)

    ok = True

    def ident(m):
        nonlocal ok
        name = m.group(0)
        if name in _sym_values:
            return "(%d)" % _sym_values[name]
        if name in SYMS:
            v = eval_int(SYMS[name], depth + 1)
            if v is None:
                ok = False
                return "0"
            _sym_values[name] = v
            return "(%d)" % v
        ok = False
        return "0"

    py = re.sub(r"\b[A-Za-z_]\w*\b", ident, c_to_py_expr(expr))
    if not ok:
        return None
    try:
        return int(eval(py, {"__builtins__": {}}, {}))
    except Exception:
        return None


# --------------------------------------------------------------------------------------------
# Enums
# --------------------------------------------------------------------------------------------

ENUM_RE = re.compile(
    r"(?P<typedef>typedef\s+)?enum\s+(?:ATTRIBUTE_PACKED\s+)?(?P<tag>\w+\s*)?\{(?P<body>[^{}]*)\}\s*(?P<tail>[^;{}]*);",
    re.S,
)


def split_top(s, sep=","):
    parts, depth, cur = [], 0, []
    for ch in s:
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        if ch == sep and depth == 0:
            parts.append("".join(cur))
            cur = []
        else:
            cur.append(ch)
    parts.append("".join(cur))
    return [p.strip() for p in parts if p.strip()]


def parse_enums(text):
    enums = []
    for m in ENUM_RE.finditer(text):
        tail = m.group("tail").strip()
        name = None
        mm = re.match(r"[US]ENUM\dBYTE\s*\(\s*(\w+)\s*\)", tail)
        if mm:
            name = mm.group(1)
        elif re.match(r"^\w+$", tail):
            name = tail
        elif m.group("tag"):
            name = m.group("tag").strip()  # `enum tag { ... };` used as `enum tag`
        values = []
        for item in split_top(m.group("body")):
            if "=" in item:
                k, v = item.split("=", 1)
                values.append((k.strip(), " ".join(v.split())))
            else:
                values.append((item.strip(), None))
        enums.append({"name": name, "values": values, "start": m.start()})
    return enums


def emit_enums(rel, skip=()):
    """Returns (aliases, consts) source text for all enums of a header."""
    aliases, consts = [], []
    for e in parse_enums(source(rel)):
        if e["name"] in skip:
            continue
        if e["name"]:
            aliases.append("global using %s = int;" % e["name"])
        prev = None
        lines = []
        for name, val in e["values"]:
            if val is None:
                if prev is None:
                    expr = "0"
                else:
                    v = eval_int(prev + " + 1")
                    expr = str(v) if v is not None else prev + " + 1"
            else:
                expr = cs_expr(val)
            lines.append("    internal const int %s = %s;" % (name, expr))
            prev = name
        consts.append("    // %s\n" % (e["name"] or "anonymous enum") + "\n".join(lines))
    return "\n".join(aliases), "\n\n".join(consts)


# --------------------------------------------------------------------------------------------
# Object-like #defines and scalar typedefs
# --------------------------------------------------------------------------------------------

DEFINE_TYPES = {}  # name -> C# type, for defines that are not int


def emit_defines(rel, skip=(), only=None, types=None):
    """`#define NAME expr` (object-like, integer or floating constant expression) -> const."""
    types = types or {}
    out = []
    for m in re.finditer(r"^#define[ \t]+(\w+)(\(?)[ \t]*(.*)$", source(rel), re.M):
        name, paren, val = m.group(1), m.group(2), m.group(3).strip()
        if paren or not val or name in skip or name.endswith("_H_"):
            continue
        if only is not None and name not in only:
            continue
        if not re.fullmatch(r"[\w\s()+\-*/<>&|^~.?:]+", val):
            continue
        if re.search(r"\d+\.\d*|\.\d+", val):
            t = types.get(name, "double")
        else:
            t = types.get(name, "int")
        out.append("    internal const %s %s = %s;" % (t, name, cs_expr(val)))
    return "\n".join(out)


def emit_typedef_aliases(rel, skip=(), types=None):
    """`typedef <scalar> NAME;` -> `global using NAME = int;` (PORTING 3.2)."""
    types = types or {}
    out = []
    for m in re.finditer(r"^\s*typedef\s+((?:unsigned\s+)?\w+)\s+(\w+)\s*;", source(rel), re.M):
        ctype, name = m.group(1), m.group(2)
        if name in skip or ctype not in CTYPE:
            continue
        out.append("global using %s = %s;" % (name, types.get(name, "int")))
    return "\n".join(out)


# --------------------------------------------------------------------------------------------
# Tables
# --------------------------------------------------------------------------------------------

CTYPE = {
    "uint8_t": "byte", "unsigned char": "byte", "int8_t": "sbyte", "char": "sbyte",
    "int16_t": "short", "uint16_t": "ushort", "int": "int", "int32_t": "int",
    "unsigned int": "uint", "uint32_t": "uint", "int64_t": "long", "uint64_t": "ulong",
    "double": "double", "float": "float", "aom_cdf_prob": "ushort", "tran_low_t": "int",
}


def cs_expr(e):
    e = " ".join(e.split())
    e = re.sub(r"\b(0[xX][0-9a-fA-F]+|\d+)(?:ULL|ull)\b", r"\1UL", e)
    e = re.sub(r"\b(0[xX][0-9a-fA-F]+|\d+)(?:LL|ll)\b", r"\1L", e)
    for c, cs in (("int8_t", "sbyte"), ("uint8_t", "byte"), ("int16_t", "short"),
                  ("uint16_t", "ushort"), ("int32_t", "int"), ("uint32_t", "uint")):
        e = re.sub(r"\(\s*%s\s*\)" % c, "(%s)" % cs, e)
    return e


def parse_init(s, i):
    """Parses a brace initializer starting at s[i] == '{'. Returns (node, next_index).
    node is a list of children (lists or (designator, leaf-string) tuples)."""
    assert s[i] == "{"
    i += 1
    items, cur, depth = [], [], 0
    while True:
        ch = s[i]
        if ch == "{" and depth == 0:
            pre = "".join(cur).strip()
            node, i = parse_init(s, i)
            if pre == "":
                items.append(node)
            elif pre.endswith("="):
                items.append(("desig", pre[:-1].strip(), node))
            else:
                raise ValueError("unexpected '{' after " + pre)
            cur = []
            continue
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth -= 1
        if ch == "}" and depth == 0:
            if "".join(cur).strip():
                items.append("".join(cur).strip())
            return items, i + 1
        if ch == "," and depth == 0:
            if "".join(cur).strip():
                items.append("".join(cur).strip())
            cur = []
        else:
            cur.append(ch)
        i += 1


def resolve_designators(items):
    out = []
    for it in items:
        if isinstance(it, tuple):
            _, desig, node = it
            m = re.match(r"\[(.*)\]$", desig)
            idx = eval_int(m.group(1))
            if idx is None:
                raise ValueError("cannot evaluate designator " + desig)
            while len(out) < idx:
                out.append(None)
            if len(out) == idx:
                out.append(resolve_designators(node))
            else:
                out[idx] = resolve_designators(node)
        elif isinstance(it, list):
            out.append(resolve_designators(it))
        else:
            m = re.match(r"\[(.*?)\]\s*=\s*(.*)$", it, re.S)
            if m:
                idx = eval_int(m.group(1))
                if idx is None:
                    raise ValueError("cannot evaluate designator " + it)
                while len(out) < idx:
                    out.append(None)
                if len(out) == idx:
                    out.append(m.group(2).strip())
                else:
                    out[idx] = m.group(2).strip()
            else:
                out.append(it)
    return out


def expand_macros(s, rel, names):
    """Expands invocations of the given function-like macros of a header in s."""
    text = source(rel)
    for name in names:
        m = re.search(r"^#define\s+%s\(([^)]*)\)\s*(.*)$" % name, text, re.M)
        params = [x.strip() for x in m.group(1).split(",")]
        body = m.group(2)
        out, i = [], 0
        for mm in re.finditer(r"\b%s\s*\(" % name, s):
            if mm.start() < i:
                continue
            depth, j = 0, mm.end() - 1
            while True:
                if s[j] == "(":
                    depth += 1
                elif s[j] == ")":
                    depth -= 1
                    if depth == 0:
                        break
                j += 1
            args = split_top(s[mm.end() : j])
            sub = body
            for prm, arg in zip(params, args):
                sub = re.sub(r"\b%s\b" % prm, arg, sub)
            out.append(s[i : mm.start()])
            out.append(sub)
            i = j + 1
        out.append(s[i:])
        s = "".join(out)
    return s


def find_table(rel, name, occurrence=0, after=None, expand=()):
    text = source(rel)
    start = 0
    if after:
        start = text.index(after)
    pat = re.compile(r"\b%s\s*((?:\[[^\]]*\]\s*)*)\)?\s*=\s*\{" % re.escape(name))
    ms = list(pat.finditer(text, start))
    m = ms[occurrence]
    dims = re.findall(r"\[([^\]]*)\]", m.group(1))
    head = text[max(0, m.start() - 300) : m.start()]
    ctype = None
    mm = re.search(r"DECLARE_ALIGNED\(\s*\w+\s*,\s*(?:static\s+)?const\s+([\w ]+?)\s*,\s*$", head)
    if mm:
        ctype = mm.group(1)
    else:
        mm = re.search(r"const\s+([\w ]+?)\s*$", head)
        if mm:
            ctype = mm.group(1).replace("static ", "").strip()
    brace = m.end() - 1
    if expand:
        _, end = parse_init(text, brace)
        init = expand_macros(text[brace:end], rel, expand)
        node, _ = parse_init(init, 0)
    else:
        node, _ = parse_init(text, brace)
    return ctype, dims, resolve_designators(node)


def wrap(items, indent, width=100):
    lines, cur = [], indent
    for k, it in enumerate(items):
        piece = it + ("," if k < len(items) - 1 else "")
        if len(cur) + len(piece) + 1 > width and cur.strip():
            lines.append(cur.rstrip())
            cur = indent
        cur += piece + " "
    if cur.strip():
        lines.append(cur.rstrip())
    return lines


def zero_of(cs_t, fields):
    if fields:
        return "default"
    return "0"


def emit_array(node, dims_v, cs_t, fields, indent, name):
    """Returns source lines of a (possibly jagged) array creation expression body."""
    d0 = dims_v[0]
    node = list(node) if node is not None else []
    if d0 is not None and len(node) > d0:
        raise ValueError("%s: %d initializers for dimension %d" % (name, len(node), d0))
    rank = len(dims_v)
    elem_t = cs_t + "[]" * (rank - 1)
    if rank == 1:
        leaves = []
        for x in node:
            if x is None:
                leaves.append(zero_of(cs_t, fields))
            elif isinstance(x, list):
                if fields:
                    vals = [cs_expr(v) if isinstance(v, str) else "0" for v in x]
                    while len(vals) < len(fields):
                        vals.append("0")
                    leaves.append("new() { " + ", ".join("%s = %s" % (f, v) for f, v in zip(fields, vals)) + " }")
                elif len(x) == 1 and isinstance(x[0], str):
                    leaves.append(cs_expr(x[0]))  # scalar in braces
                else:
                    raise ValueError("%s: unexpected nested initializer" % name)
            else:
                leaves.append(cs_expr(x))
        if d0 is not None:
            while len(leaves) < d0:
                leaves.append(zero_of(cs_t, fields))
        return leaves, True
    subs = []
    for k in range(d0 if d0 is not None else len(node)):
        child = node[k] if k < len(node) else None
        if isinstance(child, str):
            raise ValueError("%s: brace elision is not supported" % name)
        leaves, flat = emit_array(child, dims_v[1:], cs_t, fields, indent + "    ", name)
        sub_t = cs_t + "[]" * (rank - 1)
        if flat:
            body = wrap(leaves, indent + "    ")
            if len(body) == 1 and len(indent) + len(body[0].strip()) + len(sub_t) + 12 < 100:
                subs.append(["new %s { %s }" % (sub_t, body[0].strip())])
            else:
                subs.append(["new %s {" % sub_t] + body + [indent + "}"])
        else:
            subs.append(["new %s {" % sub_t] + leaves + [indent + "}"])
    lines = []
    for k, s in enumerate(subs):
        comma = "," if k < len(subs) - 1 else ""
        s = list(s)
        s[0] = indent + s[0]
        s[-1] = s[-1] + comma
        lines.extend(s)
    return lines, False


def flatten(node):
    out = []
    for x in node:
        if isinstance(x, list):
            out.extend(flatten(x))
        else:
            out.append(x)
    return out


def emit_table(spec):
    rel = spec["h"]
    name = spec["name"]
    ctype, dims, node = find_table(rel, name, spec.get("occurrence", 0), spec.get("after"),
                                   spec.get("expand", ()))
    cs_t = spec.get("t") or CTYPE.get(ctype, ctype)
    if spec.get("dims"):
        dims = spec["dims"]
    dims_v = [eval_int(d) if d.strip() else None for d in dims]
    if dims_v[0] is None:
        dims_v[0] = len(node)
    for k, d in enumerate(dims_v):
        if d is None:
            raise ValueError("%s: cannot evaluate dimension [%s]" % (name, dims[k]))
    fields = spec.get("fields")
    access = spec.get("access", "internal")
    cs_name = spec.get("cs_name", name)
    decl = "".join("[%s]" % d for d in dims)
    out = ["    // %s %s%s" % (ctype or cs_t, name, decl)]
    if spec.get("flatten"):
        leaves = [cs_expr(x) for x in flatten(node)]
        total = 1
        for d in dims_v:
            total *= d
        if len(leaves) != total:
            raise ValueError("%s: flatten needs a full initializer (%d of %d)" % (name, len(leaves), total))
        out.append("    %s static readonly %s[] %s = {" % (access, cs_t, cs_name))
        per = dims_v[-1]
        for r in range(0, len(leaves), per):
            out.extend(wrap(leaves[r : r + per], "        "))
            out[-1] += ","
        out[-1] = out[-1].rstrip(",")
        out.append("    };")
        return "\n".join(out)
    rank = len(dims_v)
    leaves, flat = emit_array(node, dims_v, cs_t, fields, "        ", name)
    t = cs_t + "[]" * rank
    if spec.get("span"):
        assert rank == 1
        out.append("    %s static ReadOnlySpan<%s> %s => new %s[] {" % (access, cs_t, cs_name, cs_t))
        out.extend(wrap(leaves, "        "))
        out.append("    };")
    elif flat:
        out.append("    %s static readonly %s %s = {" % (access, t, cs_name))
        out.extend(wrap(leaves, "        "))
        out.append("    };")
    else:
        out.append("    %s static readonly %s %s = {" % (access, t, cs_name))
        out.extend(leaves)
        out.append("    };")
    return "\n".join(out)


# --------------------------------------------------------------------------------------------
# CDF holder classes
# --------------------------------------------------------------------------------------------


def emit_cdf_class(rel, struct_re, cs_name, comment=None):
    text = source(rel)
    m = re.search(struct_re, text, re.S)
    if not m:
        raise ValueError("struct not found: " + struct_re)
    body = m.group(1)
    fields, allocs, copies = [], [], []
    for decl in body.split(";"):
        decl = " ".join(decl.split())
        if not decl:
            continue
        mm = re.match(r"(?:struct\s+)?(\w+)\s+(\w+)\s*((?:\[[^\]]*\]\s*)*)$", decl)
        if not mm:
            raise ValueError("unparsed field: " + decl)
        ctype, name, dimtxt = mm.groups()
        dims = [cs_expr(d) for d in re.findall(r"\[([^\]]*)\]", dimtxt)]
        if ctype == "aom_cdf_prob":
            t = "ushort" + "[]" * len(dims)
            fields.append("    public readonly %s %s = new_jagged<ushort>(%s);" % (t, name, ", ".join(dims))
                          if len(dims) > 1 else
                          "    public readonly ushort[] %s = new ushort[%s];" % (name, dims[0]))
            copies.append("        av1_copy(%s, other.%s);" % (name, name))
        elif ctype == "int" and not dims:
            fields.append("    public int %s;" % name)
            copies.append("        %s = other.%s;" % (name, name))
        elif not dims:
            fields.append("    public readonly %s %s = new();" % (ctype, name))
            copies.append("        %s.CopyFrom(other.%s);" % (name, name))
        elif len(dims) == 1:
            fields.append("    public readonly %s[] %s = { %s };" % (
                ctype, name, ", ".join(["new()"] * eval_int(dims[0]))))
            copies.append("        for (int i = 0; i < %s.Length; i++) %s[i].CopyFrom(other.%s[i]);" % (name, name, name))
        else:
            raise ValueError("unsupported field: " + decl)
    out = []
    if comment:
        out.append("// " + comment)
    out.append("internal sealed class %s" % cs_name)
    out.append("{")
    out.extend(fields)
    out.append("")
    out.append("    /// <summary>C struct assignment (<c>*this = *other</c>): deep copy of every field.</summary>")
    out.append("    public void CopyFrom(%s other)" % cs_name)
    out.append("    {")
    out.extend(copies)
    out.append("    }")
    out.append("}")
    return "\n".join(out)


# --------------------------------------------------------------------------------------------
# What to generate, per target file
# --------------------------------------------------------------------------------------------


def T(h, name, **kw):
    kw.update(h=h, name=name)
    return ("table", kw)


CD = "av1/common/common_data.h"
FI = "av1/common/filter.h"
RI = "av1/common/reconintra.h"
BD = "av1/common/blockd.h"
CI = "av1/common/av1_common_int.h"
TC = "av1/common/txb_common.h"
WM = "av1/common/warped_motion.h"


def std(h, defines_skip=(), define_types=None, typedef_types=None, typedef_skip=(), tables=()):
    """The usual regions of a header port: aliases (before the namespace) and consts/tables."""
    return {
        "aliases": [("enum_aliases", h),
                    ("typedefs", dict(rel=h, skip=typedef_skip, types=typedef_types))],
        "consts": [("defines", dict(rel=h, skip=defines_skip, types=define_types)),
                   ("enum_consts", h)] + list(tables),
    }


GEN = {
    "enums.h.cs": std("av1/common/enums.h"),
    "common_data.h.cs": std(CD, tables=[
        T(CD, "mi_size_wide_log2"), T(CD, "mi_size_high_log2"), T(CD, "mi_size_wide"),
        T(CD, "mi_size_high"), T(CD, "block_size_wide"), T(CD, "block_size_high"),
        T(CD, "size_group_lookup"), T(CD, "num_pels_log2_lookup"),
        T(CD, "subsize_lookup"), T(CD, "max_txsize_lookup"), T(CD, "max_txsize_rect_lookup"),
        T(CD, "vtx_tab"), T(CD, "htx_tab"), T(CD, "sub_tx_size_map"), T(CD, "txsize_horz_map"),
        T(CD, "txsize_vert_map"), T(CD, "tx_size_wide_log2"),
        T(CD, "tx_size_high_log2"), T(CD, "tx_size_wide"), T(CD, "tx_size_high"),
        T(CD, "tx_size_wide_unit"), T(CD, "tx_size_high_unit"),
        T(CD, "tx_size_wide_unit_log2"), T(CD, "tx_size_high_unit_log2"),
        T(CD, "txsize_log2_minus4"), T(CD, "txsize_sqr_map"), T(CD, "txsize_sqr_up_map"),
        T(CD, "tx_size_2d"), T(CD, "txsize_to_bsize"),
        T(CD, "tx_mode_to_biggest_tx_size"),
        T(CD, "partition_context_lookup", t="partition_context_lookup_entry",
          fields=["above", "left"]),
        T(CD, "intra_mode_context"), T(CD, "quant_dist_weight"),
        T(CD, "quant_dist_lookup_table"),
    ]),
    "common.h.cs": std("av1/common/common.h"),
    "mv.h.cs": std("av1/common/mv.h", define_types={"INVALID_MV": "uint"}),
    "entropy.h.cs": std("av1/common/entropy.h"),
    "entropymv.h.cs": dict(std("av1/common/entropymv.h"), classes=[
        ("cdf_class", ("av1/common/entropymv.h",
                       r"typedef struct \{([^{}]*)\} nmv_component;", "nmv_component")),
        ("cdf_class", ("av1/common/entropymv.h",
                       r"typedef struct \{([^{}]*)\} nmv_context;", "nmv_context")),
    ]),
    "entropymode.h.cs": dict(std("av1/common/entropymode.h", tables=[
        T("av1/common/entropymode.h", "av1_ext_tx_ind"),
        T("av1/common/entropymode.h", "av1_ext_tx_inv"),
    ]), classes=[
        ("cdf_class", ("av1/common/entropymode.h",
                       r"typedef struct frame_contexts \{([^{}]*)\} FRAME_CONTEXT;", "FRAME_CONTEXT")),
    ]),
    "seg_common.h.cs": dict(std("av1/common/seg_common.h"), classes=[
        ("cdf_class", ("av1/common/seg_common.h",
                       r"struct segmentation_probs \{([^{}]*)\};", "segmentation_probs")),
    ]),
    "scale.h.cs": std("av1/common/scale.h"),
    "convolve.h.cs": std("av1/common/convolve.h", typedef_types={"CONV_BUF_TYPE": "ushort"}),
    "quant_common.h.cs": std("av1/common/quant_common.h"),
    "av1_txfm.h.cs": std("av1/common/av1_txfm.h", tables=[
        T("av1/common/av1_txfm.h", "av1_cospi_arr_data_inline", access="private"),
        T("av1/common/av1_txfm.h", "av1_sinpi_arr_data_inline", access="private"),
    ]),
    "av1_inv_txfm1d_cfg.h.cs": std("av1/common/av1_inv_txfm1d_cfg.h", tables=[
        T("av1/common/av1_inv_txfm1d_cfg.h", "inv_start_range"),
        T("av1/common/av1_inv_txfm1d_cfg.h", "av1_inv_txfm_shift_ls"),
    ]),
    "idct.h.cs": std("av1/common/idct.h", tables=[
        T("av1/common/idct.h", "tx_scale", access="private"),
    ]),
    "scan.h.cs": std("av1/common/scan.h"),
    "timing.h.cs": std("av1/common/timing.h"),
    "tile_common.h.cs": std("av1/common/tile_common.h"),
    "av1_loopfilter.h.cs": std("av1/common/av1_loopfilter.h"),
    "cdef.h.cs": std("av1/common/cdef.h"),
    "cdef_block.h.cs": std("av1/common/cdef_block.h", defines_skip=("CDEF_BSTRIDE", "CDEF_INBUF_SIZE")),
    "restoration.h.cs": std("av1/common/restoration.h",
                            defines_skip=("SGRPROJ_TMPBUF_SIZE", "RESTORATION_TMPBUF_SIZE")),
    "reconintra.h.cs": std(RI, tables=[
        T(RI, "interintra_to_intra_mode"), T(RI, "intra_to_interintra_mode"),
        T(RI, "dr_intra_derivative"),
    ]),
    "resize.h.cs": std("av1/common/resize.h", tables=[
        T("av1/common/resize.h", "av1_down2_symeven_half_filter"),
        T("av1/common/resize.h", "av1_down2_symodd_half_filter"),
    ]),
    "warped_motion.h.cs": std(WM, typedef_types={"WarpedFilterCoeff": "sbyte"}, tables=[
        T(WM, "warp_pad_left"), T(WM, "warp_pad_right"),
    ]),
    "alloccommon.h.cs": std("av1/common/alloccommon.h"),
    "txb_common.h.cs": std(TC, tables=[
        T(TC, "base_level_count_to_index"), T(TC, "tx_type_to_class"),
        T(TC, "clip_max3", access="private"), T(TC, "nz_map_ctx_offset_1d", access="private"),
        T(TC, "kTxbCtxSigns", access="private"),
        T(TC, "kTxbCtxDcSignContexts", access="private"),
        T(TC, "kTxbCtxSkipContexts", access="private"),
    ]),
    "mvref_common.h.cs": std("av1/common/mvref_common.h", tables=[
        T("av1/common/mvref_common.h", "ref_frame_map", t="MV_REFERENCE_FRAME"),
        T("av1/common/mvref_common.h", "compound_mode_ctx_map", t="ushort"),
    ]),
    "reconinter.h.cs": std("av1/common/reconinter.h", typedef_skip=["wedge_masks_type"]),
    "av1_common_int.h.cs": std(CI, defines_skip=["AOM_FALLTHROUGH_INTENDED"], tables=[
        T(CI, "base_partitions", access="private"),
    ]),
    "blockd.h.cs": std(BD, tables=[
        T(BD, "fimode_to_intradir"), T(BD, "av1_num_ext_tx_set"), T(BD, "av1_ext_tx_used"),
        T(BD, "av1_derived_intra_tx_used_flag"), T(BD, "av1_reduced_intra_tx_used_flag"),
        T(BD, "av1_ext_tx_used_flag"),
        T(BD, "av1_ext_tx_set_lookup", expand=["EXT_TX_SET_LIST"]),
        T(BD, "ext_tx_set_index"), T(BD, "mode_to_angle_map"), T(BD, "max_neighbor_obmc"),
        T(BD, "uv2y", access="private"), T(BD, "_intra_mode_to_tx_type", access="private"),
        T(BD, "tw_w_log2_table", access="private"), T(BD, "tw_h_log2_table", access="private"),
        T(BD, "stride_log2_table", access="private"),
        T(BD, "bsize_to_max_depth_table", access="private"),
        T(BD, "bsize_to_tx_size_depth_table", access="private"),
        T(BD, "adjusted_tx_size", access="private"),
        T(BD, "lut", after="comp_ref0(int ref_idx)", cs_name="comp_ref0_lut", access="private"),
        T(BD, "lut", after="comp_ref1(int ref_idx)", cs_name="comp_ref1_lut", access="private"),
    ]),

    "filter.h.cs": std(FI, tables=[
        T(FI, "av1_bilinear_filters", t="short", flatten=True, dims=["SUBPEL_SHIFTS", "SUBPEL_TAPS"]),
        T(FI, "av1_sub_pel_filters_8", t="short", flatten=True, dims=["SUBPEL_SHIFTS", "SUBPEL_TAPS"]),
        T(FI, "av1_sub_pel_filters_8sharp", t="short", flatten=True, dims=["SUBPEL_SHIFTS", "SUBPEL_TAPS"]),
        T(FI, "av1_sub_pel_filters_8smooth", t="short", flatten=True, dims=["SUBPEL_SHIFTS", "SUBPEL_TAPS"]),
        T(FI, "av1_sub_pel_filters_12sharp", t="short", flatten=True),
        T(FI, "av1_intrabc_bilinear_filter"),
        T(FI, "av1_sub_pel_filters_4", t="short", flatten=True, dims=["SUBPEL_SHIFTS", "SUBPEL_TAPS"]),
        T(FI, "av1_sub_pel_filters_4smooth", t="short", flatten=True, dims=["SUBPEL_SHIFTS", "SUBPEL_TAPS"]),
        T(FI, "av1_interp_dual_filt_mask"),
    ]),
}


def enum_parts(rel):
    if rel not in _enum_cache:
        _enum_cache[rel] = emit_enums(rel)
    return _enum_cache[rel]


_enum_cache = {}


def render(item):
    kind, arg = item
    if kind == "enum_aliases" and not enum_parts(arg)[0]:
        return None
    if kind == "enum_consts" and not enum_parts(arg)[1]:
        return None
    if kind == "table":
        return emit_table(arg)
    if kind == "enum_aliases":
        return enum_parts(arg)[0]
    if kind == "enum_consts":
        return enum_parts(arg)[1]
    if kind == "cdf_class":
        return emit_cdf_class(*arg)
    if kind == "defines":
        return emit_defines(**arg)
    if kind == "typedefs":
        return emit_typedef_aliases(**arg)
    raise ValueError(kind)


def main():
    collect_symbols()
    failed = False
    for fname, regions in GEN.items():
        path = os.path.join(CS_DIR, fname)
        with open(path, encoding="utf-8") as f:
            text = f.read()
        for region, items in regions.items():
            begin, end = "// <gen:%s>" % region, "// </gen:%s>" % region
            a = text.find(begin)
            b = text.find(end)
            try:
                body = "\n\n".join(x for x in (render(it) for it in items) if x)
            except Exception as ex:  # report and keep going
                print("%s/%s: %s" % (fname, region, ex), file=sys.stderr)
                failed = True
                continue
            if a < 0 or b < 0:
                if body:
                    print("%s: missing markers for %s" % (fname, region), file=sys.stderr)
                    failed = True
                continue
            line_start = text.rfind("\n", 0, a) + 1
            indent = text[line_start:a]
            text = text[: a + len(begin)] + ("\n" + body if body else "") + "\n" + indent + text[b:]
        with open(path, encoding="utf-8") as f:
            if f.read() == text:
                continue
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print("wrote", os.path.relpath(path, ROOT))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
