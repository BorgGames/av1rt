"""Summarise llvm-cov export JSON into per-file / per-function executed lists.

Usage: python -I cov_summary.py export.json out_dir aom_root
Writes:
  out_dir/files.txt       per source file: executed functions, executed lines
  out_dir/functions.txt   per source file: each executed function with call count and line range
  out_dir/unexecuted.txt  per source file: functions that were compiled but never executed
"""
import json
import os
import sys
from collections import defaultdict

export_path, out_dir, aom_root = sys.argv[1], sys.argv[2], os.path.normcase(os.path.abspath(sys.argv[3]))
os.makedirs(out_dir, exist_ok=True)
data = json.load(open(export_path, encoding="utf-8"))["data"][0]


def rel(path):
    p = os.path.normcase(os.path.abspath(path))
    if p.startswith(aom_root):
        return p[len(aom_root) + 1:].replace("\\", "/")
    return None


executed = defaultdict(list)
unexecuted = defaultdict(list)
for fn in data["functions"]:
    files = fn["filenames"]
    if not files:
        continue
    r = rel(files[0])
    if r is None:
        continue
    regions = [reg for reg in fn["regions"] if reg[5] == 0]  # file id 0 = defining file
    if not regions:
        continue
    start = min(reg[0] for reg in regions)
    end = max(reg[2] for reg in regions)
    name = fn["name"].split(":")[-1]
    (executed if fn["count"] > 0 else unexecuted)[r].append((start, end, name, fn["count"]))

line_stats = {}
for f in data["files"]:
    r = rel(f["filename"])
    if r is None:
        continue
    s = f["summary"]["lines"]
    line_stats[r] = (s["covered"], s["count"])

with open(os.path.join(out_dir, "files.txt"), "w") as out:
    out.write("# file: executed_functions/total_functions executed_lines/instrumented_lines\n")
    for r in sorted(set(executed) | set(unexecuted)):
        e, u = len(executed[r]), len(unexecuted[r])
        cl, tl = line_stats.get(r, (0, 0))
        if e == 0:
            continue
        out.write(f"{r}: {e}/{e + u} funcs, {cl}/{tl} lines\n")

with open(os.path.join(out_dir, "functions.txt"), "w") as out:
    for r in sorted(executed):
        out.write(f"## {r}\n")
        for start, end, name, count in sorted(set(executed[r])):
            out.write(f"  {name}  lines {start}-{end}  calls {count}\n")

with open(os.path.join(out_dir, "unexecuted.txt"), "w") as out:
    for r in sorted(unexecuted):
        if not executed[r]:
            continue
        out.write(f"## {r}\n")
        for start, end, name, count in sorted(set(unexecuted[r])):
            out.write(f"  {name}  lines {start}-{end}\n")
