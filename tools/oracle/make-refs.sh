#!/usr/bin/env bash
# Encodes every case in tests/Av1rt.Tests/EndToEnd/cases.txt with the pure-C oracle into
# .oracle/ref/<name>.ivf and .oracle/ref/<name>.log (per-frame size/q lines).
# Usage: tools/oracle/make-refs.sh [case-name...]
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
oracle="$root/.oracle/oracle/Release/av1rt_oracle.exe"
mkdir -p "$root/.oracle/ref"
while IFS='|' read -r name clip opts; do
  name=$(echo "$name" | xargs); clip=$(echo "$clip" | xargs)
  [[ -z "$name" || "$name" == \#* ]] && continue
  if [[ $# -gt 0 && ! " $* " =~ " $name " ]]; then continue; fi
  echo "== $name"
  # shellcheck disable=SC2086
  "$oracle" "$root/.testdata/$clip" "$root/.oracle/ref/$name.ivf" $opts > "$root/.oracle/ref/$name.log"
  tail -1 "$root/.oracle/ref/$name.log"
done < "$root/tests/Av1rt.Tests/EndToEnd/cases.txt"
