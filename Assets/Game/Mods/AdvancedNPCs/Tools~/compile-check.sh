#!/bin/bash
# Compiles the Advanced NPCs mod sources against a Daggerfall Unity install's own assemblies,
# the way DFU compiles mod sources at runtime. Catches API mismatches without opening Unity.
# Usage: compile-check.sh [path to DFU install]   (default: F:/_Projects/Dagerfall/DFU_testing)
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
MOD="$(cd "$HERE/.." && pwd)"
DFU="${1:-/f/_Projects/Dagerfall/DFU_testing}"
MANAGED="$DFU/DaggerfallUnity_Data/Managed"
MONO="/c/Program Files/Unity/Hub/Editor/2019.4.41f2/Editor/Data/MonoBleedingEdge"
OUT="$(cygpath -w "${TMP:-/tmp}")\advancednpcs-compile-check.dll"

w() { cygpath -w "$1"; }

args=(-noconfig -nostdlib -langversion:4 -target:library -nowarn:1591 "-out:$OUT" "-r:$(w "$MANAGED/mscorlib.dll")")
for dll in "$MANAGED"/UnityEngine*.dll "$MANAGED/Assembly-CSharp.dll" "$MANAGED/Assembly-CSharp-firstpass.dll" \
           "$MANAGED/FullSerializer - Unity.dll" "$MANAGED/System.dll" "$MANAGED/System.Core.dll" "$MANAGED/netstandard.dll"; do
  args+=("-r:$(w "$dll")")
done
count=0
while IFS= read -r src; do
  args+=("$(w "$src")")
  count=$((count + 1))
done < <(find "$MOD/Scripts" -name '*.cs' | sort)

"$MONO/bin/mono.exe" "$(w "$MONO/lib/mono/4.5/mcs.exe")" "${args[@]}"
echo "compile-check: OK ($count source files)"
