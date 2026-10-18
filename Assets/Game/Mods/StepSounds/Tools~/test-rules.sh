#!/bin/bash
# Compiles FootstepRules.cs (C# 4, as DFU's runtime compiler) with RulesTests.cs and runs the tests.
# Needs mcs and mono (Mono), or Unity's MonoBleedingEdge: MONO_BIN=".../MonoBleedingEdge/bin" test-rules.sh
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="${TMPDIR:-/tmp}/step_sounds-rules-tests.exe"
MCS="mcs"; MONO="mono"
if [ -n "$MONO_BIN" ]; then MCS="$MONO_BIN/mcs"; MONO="$MONO_BIN/mono"; fi
"$MCS" -langversion:4 -nologo -out:"$OUT" "$HERE/../Scripts/FootstepRules.cs" "$HERE/RulesTests.cs"
"$MONO" "$OUT"
