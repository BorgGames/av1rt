"""Summarises a dotnet-trace speedscope file: self and inclusive sample share per method.

Usage: python -I tools/prof_summary.py trace.speedscope.json [--top 40] [--thread-filter substring]
"""
import argparse
import collections
import json

ap = argparse.ArgumentParser()
ap.add_argument('path')
ap.add_argument('--top', type=int, default=40)
a = ap.parse_args()
d = json.load(open(a.path, encoding='utf-8'))
frames = [f['name'] for f in d['shared']['frames']]
PSEUDO = {'CPU_TIME', 'UNMANAGED_CODE_TIME'}
self_t = collections.Counter()
incl_t = collections.Counter()
total = 0.0
for prof in d['profiles']:
    if prof.get('type') != 'evented':
        continue
    stack = []
    last = prof['startValue']
    for ev in prof['events']:
        t = ev['at']
        dt = t - last
        if stack and dt > 0:
            # dotnet-trace adds CPU_TIME / UNMANAGED_CODE_TIME pseudo frames as leaves.
            leaf = len(stack) - 1
            while leaf > 0 and frames[stack[leaf]] in PSEUDO:
                leaf -= 1
            self_t[stack[leaf]] += dt
            for f in set(stack):
                incl_t[f] += dt
            total += dt
        last = t
        if ev['type'] == 'O':
            stack.append(ev['frame'])
        else:
            stack.pop()


def short(n):
    n = n.split('(')[0]
    return n.replace('Av1rt.Aom.', '').replace('System.Private.CoreLib!', '')[:110]


print(f'total {total:.0f} ms of samples')
print('--- self ---')
for f, t in self_t.most_common(a.top):
    print(f'{100 * t / total:6.2f}%  {short(frames[f])}')
print('--- inclusive ---')
for f, t in incl_t.most_common(a.top):
    print(f'{100 * t / total:6.2f}%  {short(frames[f])}')
