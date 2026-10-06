#!/bin/bash
# Compiles the mod sources the way Daggerfall Unity does at runtime (DFU's own CSharpCompiler +
# mcs.dll, in memory, against the game's Managed assemblies) and then loads every type.
# Catches runtime-compiler problems that a normal compile misses, e.g. TypeLoadException.
# Usage: runtime-check.sh [path to DFU install]   (default: F:/_Projects/Dagerfall/DFU_testing)
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
MOD="$(cd "$HERE/.." && pwd)"
PROJECT="$(cd "$HERE/../../../../.." && pwd)"
DFU="${1:-/f/_Projects/Dagerfall/DFU_testing}"
MANAGED="$DFU/DaggerfallUnity_Data/Managed"
CC="$PROJECT/Assets/Game/Addons/CSharpCompiler"
MONO="/c/Program Files/Unity/Hub/Editor/2019.4.41f2/Editor/Data/MonoBleedingEdge"
OUTDIR="${TMP:-/tmp}/advancednpcs-runtime-check"
mkdir -p "$OUTDIR"
w() { cygpath -w "$1"; }

cp "$CC/Plugins/mcs.dll" "$OUTDIR/mcs.dll"
"$MONO/bin/mono.exe" "$(w "$MONO/lib/mono/4.5/mcs.exe")" -nologo -target:exe -nowarn:618 \
  "-out:$(w "$OUTDIR/harness.exe")" "-r:$(w "$OUTDIR/mcs.dll")" -r:System.dll \
  "$(w "$HERE/RuntimeCompileHarness.cs")" "$(w "$CC/CodeCompiler.cs")" \
  "$(w "$CC/CustomDynamicDriver.cs")" "$(w "$CC/CustomReportPrinter.cs")"

# DFU compiles the bundle's sources in an order of its own, and mcs fails to load a type whose enum field is
# declared in a later file (TypeLoadException "bad underlying type"). Check folder order and file-name order.
check() {
  sources=()
  while IFS= read -r src; do sources+=("$(w "$src")"); done
  "$MONO/bin/mono.exe" "$(w "$OUTDIR/harness.exe")" "$(w "$MANAGED")" "${sources[@]}"
}
echo "Order: by path"
find "$MOD/Scripts" -name '*.cs' | sort | check
echo "Order: by file name"
find "$MOD/Scripts" -name '*.cs' | awk -F/ '{print $NF "	" $0}' | sort | cut -f2 | check
