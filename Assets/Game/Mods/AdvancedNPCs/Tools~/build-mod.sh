#!/bin/bash
# Builds advancednpcs.dfmod with Unity 2019.4.41f2 and installs it, plus the example
# NPC definitions, into a Daggerfall Unity install.
#
# Runs a normal (windowed) editor, not -batchmode: a Hub Personal license works there but not in
# batch mode. Needs the Unity Editor and the game closed. The first run imports the whole
# DFU project and can take 30-60+ minutes.
#
# Usage: build-mod.sh [DFU install dir] [--no-examples]
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$(cd "$HERE/../../../../.." && pwd)"
DFU="/f/_Projects/Dagerfall/DFU_testing"
EXAMPLES=1
for arg in "$@"; do
  case "$arg" in
    --no-examples) EXAMPLES=0 ;;
    *) DFU="$arg" ;;
  esac
done
UNITY="${ANPC_UNITY_2019:-/c/Program Files/Unity/Hub/Editor/2019.4.41f2/Editor/Unity.exe}"
OUT="$PROJECT/Builds/AdvancedNPCs"
LOG="$PROJECT/Builds/advancednpcs-build.log"
mkdir -p "$OUT"

echo "Building with Unity 2019.4 (an editor window opens and closes by itself; log: $LOG)..."
if ! "$UNITY" -quit -projectPath "$(cygpath -w "$PROJECT")" \
     -executeMethod AdvancedNPCs.EditorTools.AdvancedNpcsModBuilder.Build \
     -modOut "$(cygpath -w "$OUT")" -logFile "$(cygpath -w "$LOG")"; then
  echo "BUILD FAILED. Last relevant log lines:"
  grep -E "AdvancedNPCs build|error CS|License|licen" "$LOG" | tail -15
  exit 1
fi

BUNDLE="$OUT/StandaloneWindows/advancednpcs.dfmod"
[ -f "$BUNDLE" ] || { echo "Build reported success but $BUNDLE is missing"; exit 1; }
cp "$BUNDLE" "$DFU/DaggerfallUnity_Data/StreamingAssets/Mods/"
echo "Installed $(basename "$BUNDLE") into $DFU"

if [ "$EXAMPLES" = 1 ]; then
  DEFS="$DFU/DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs"
  mkdir -p "$DEFS"
  cp "$HERE/../Examples/"*.json "$DEFS/"
  echo "Copied example NPCs into $DEFS"
fi
