"""Finds where two AV1 streams first differ in decoded block-level decisions.

Decodes both IVF files with libaom's inspection decoder (examples/inspect.c built with
CONFIG_INSPECTION=1 into .oracle/build-inspect) and compares, frame by frame, the frame
header fields and every per-mode-info map (block size, modes, MVs, references, skip,
transform size/type, filters, CDEF, segment, delta q...). Prints the first differing frame
and the first few differing 4x4 positions per map.

Usage: python -I tools/oracle/inspect_diff.py expected.ivf actual.ivf [--limit N] [--show 8]
"""
import argparse
import json
import os
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
INSPECT = os.path.join(ROOT, '.oracle', 'build-inspect', 'Release', 'inspect.exe')


def inspect(path, limit):
    args = [INSPECT, path, '-A']
    if limit:
        args += [f'--limit={limit}']
    out = subprocess.run(args, capture_output=True, text=True)
    if out.returncode != 0 and not out.stdout:
        sys.exit(f'inspect failed on {path}: {out.stderr.strip()}')
    frames = json.loads(out.stdout)
    return [f for f in frames if f is not None]


def is_map(v):
    return isinstance(v, list) and v and isinstance(v[0], list)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('expected')
    ap.add_argument('actual')
    ap.add_argument('--limit', type=int, default=0)
    ap.add_argument('--show', type=int, default=8)
    a = ap.parse_args()

    exp = inspect(a.expected, a.limit)
    act = inspect(a.actual, a.limit)
    print(f'decoded {len(exp)} expected and {len(act)} actual frames')
    for i, (fe, fa) in enumerate(zip(exp, act)):
        header = [k for k in fe if not is_map(fe[k]) and fe.get(k) != fa.get(k)]
        maps = [k for k in fe if is_map(fe[k]) and fe[k] != fa.get(k)]
        if not header and not maps:
            continue
        print(f'first difference in decoded frame {i} (frame number {fe.get("frame")}):')
        for k in header:
            print(f'  header {k}: expected {fe.get(k)!r} actual {fa.get(k)!r}')
        for k in maps:
            me, ma = fe[k], fa.get(k) or []
            shown = 0
            for r, (re_, ra) in enumerate(zip(me, ma)):
                for c, (ve, va) in enumerate(zip(re_, ra)):
                    if ve != va:
                        if shown < a.show:
                            print(f'  {k} at mi row {r} col {c} (pixel {c * 4},{r * 4}): '
                                  f'expected {ve!r} actual {va!r}')
                        shown += 1
            print(f'  {k}: {shown} differing positions')
        return 1
    if len(exp) != len(act):
        print(f'streams agree on {min(len(exp), len(act))} frames but differ in frame count')
        return 1
    print('no differences in decoded decisions')
    return 0


if __name__ == '__main__':
    sys.exit(main())
