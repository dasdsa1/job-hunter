#!/usr/bin/env bash
# ponytail: dotnet format only (zero-config, ships with the SDK). Extend to
# Angular/TS lint once eslint+prettier are actually set up in those repos.
set -eu
input="$(cat)"
file="$(printf '%s' "$input" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("tool_input",{}).get("file_path",""))' 2>/dev/null || true)"
[ -z "$file" ] && exit 0
case "$file" in *.cs) ;; *) exit 0 ;; esac
dir="$(dirname "$file")"
while [ "$dir" != "/" ] && [ "$dir" != "." ]; do
  proj="$(find "$dir" -maxdepth 1 -name '*.csproj' 2>/dev/null | head -1)"
  [ -n "$proj" ] && break
  dir="$(dirname "$dir")"
done
[ -z "${proj:-}" ] && exit 0
dotnet format "$proj" --include "$file" --verbosity quiet 2>&1 || true
exit 0
