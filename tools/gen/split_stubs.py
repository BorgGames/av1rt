"""Regroups src/Av1rt/Aom/_stubs/*.cs so there is one stub file per libaom C source.

A porter of <dir>/<name>.c deletes _stubs/<dir>_<name>_c.cs. Sections inside a stub class
start with a comment naming the C file: `// av1/common/scan.c ...` or `// ---- av1/x.c ----`.
Run from the repository root: python -I tools/gen/split_stubs.py
"""
import collections
import os
import re

STUBS = 'src/Av1rt/Aom/_stubs'
SECTION = re.compile(r'^\s*//[\s-]*((?:av1|aom_dsp|aom_scale|aom_mem|aom_util|aom)/[\w/]+\.c)\b')

sections = collections.OrderedDict()
usings = set()
for fn in sorted(os.listdir(STUBS)):
    src = open(os.path.join(STUBS, fn), encoding='utf-8').read()
    usings.update(re.findall(r'^using [\w.]+;', src, re.M))
    body = src[src.index('{', src.index('internal static class')) + 1: src.rindex('}')]
    cur = None
    for line in body.split('\n'):
        m = SECTION.match(line)
        if m:
            cur = m.group(1)
            sections.setdefault(cur, []).append(line)
            continue
        if cur is None:
            if line.strip():
                raise SystemExit(f'orphan line in {fn}: {line}')
            continue
        sections[cur].append(line)

for fn in os.listdir(STUBS):
    os.remove(os.path.join(STUBS, fn))

for cfile, lines in sections.items():
    flat = cfile.replace('/', '_')[:-2] + '_c'
    while lines and not lines[-1].strip():
        lines.pop()
    out = [f'// Stubs for {cfile}: delete this file when {cfile} is ported.',
           f'global using static Av1rt.Aom.stubs_{flat};', '']
    out += sorted(usings)
    out += ['', 'namespace Av1rt.Aom;', '', f'internal static class stubs_{flat}', '{']
    out += lines
    out += ['}', '']
    with open(os.path.join(STUBS, flat + '.cs'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(out))
    print(flat + '.cs', sum('NotImplementedException' in l for l in lines))
