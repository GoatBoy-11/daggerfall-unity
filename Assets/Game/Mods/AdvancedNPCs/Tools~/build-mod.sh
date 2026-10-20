#!/bin/bash
# Builds advancednpcs.dfmod with Unity 2019.4.41f2 and installs it, plus the example
# ANPC folders, into a Daggerfall Unity install.
#
# Runs a normal (windowed) editor, not -batchmode: a Hub Personal license works there but not in
# batch mode. Needs the Unity Editor and the game closed. The first run imports the whole
# DFU project and can take 30-60+ minutes.
#
# Usage: build-mod.sh [DFU install dir] [--no-examples]
# ANPC_BACKGROUND=1 starts the editor minimized.
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
run_unity() {
  if [ "${ANPC_BACKGROUND:-0}" = 1 ]; then
    local code
    code=$(powershell -NoProfile -Command "\$p = Start-Process -FilePath '$(cygpath -w "$UNITY")' -WindowStyle Minimized -PassThru -ArgumentList '-quit','-projectPath','\"$(cygpath -w "$PROJECT")\"','-executeMethod','AdvancedNPCs.EditorTools.AdvancedNpcsModBuilder.Build','-modOut','\"$(cygpath -w "$OUT")\"','-logFile','\"$(cygpath -w "$LOG")\"'; \$p.WaitForExit(); \$p.ExitCode")
    [ "$(echo "$code" | tr -d '\r ')" = 0 ]
  else
    "$UNITY" -quit -projectPath "$(cygpath -w "$PROJECT")" \
      -executeMethod AdvancedNPCs.EditorTools.AdvancedNpcsModBuilder.Build \
      -modOut "$(cygpath -w "$OUT")" -logFile "$(cygpath -w "$LOG")"
  fi
}
if ! run_unity; then
  echo "BUILD FAILED. Last relevant log lines:"
  grep -E "AdvancedNPCs build|error CS|License|licen" "$LOG" | tail -15
  exit 1
fi

BUNDLE="$OUT/StandaloneWindows/advancednpcs.dfmod"
[ -f "$BUNDLE" ] || { echo "Build reported success but $BUNDLE is missing"; exit 1; }
cp "$BUNDLE" "$DFU/DaggerfallUnity_Data/StreamingAssets/Mods/"
echo "Installed $(basename "$BUNDLE") into $DFU"

if [ "$EXAMPLES" = 1 ]; then
  DEFS="$DFU/DaggerfallUnity_Data/StreamingAssets/ANPCs"
  mkdir -p "$DEFS"
  # An example's installed Sounds folder mirrors the repo's (no stale clips from an older version).
  for s in "$HERE/../Examples/ANPCs"/*/Sounds; do
    [ -d "$s" ] && rm -rf "$DEFS/$(basename "$(dirname "$s")")/Sounds"
  done
  rm -rf "$DEFS/_Sounds"/va_*     # voice packs too (local ones are applied again below)
  (cd "$HERE/../Examples/ANPCs" && find . -name '*.meta' -prune -o -type f -print | while read -r f; do
    mkdir -p "$DEFS/$(dirname "$f")"
    cp "$f" "$DEFS/$f"
  done)
  echo "Copied example ANPCs into $DEFS"

  # Local sounds (never in git, e.g. bought packs whose license forbids sharing the raw files):
  # <ANPC_LOCAL_SOUNDS>/<anpc>/Sounds/<pool>_N.ogg replaces that ANPC's whole pool in the install.
  LOCAL="${ANPC_LOCAL_SOUNDS:-/f/_Projects/Dagerfall/anpc_local_sounds}"
  if [ -d "$LOCAL" ]; then
    for dir in "$LOCAL"/*/Sounds; do
      [ -d "$dir" ] || continue
      dest="$DEFS/$(basename "$(dirname "$dir")")/Sounds"
      mkdir -p "$dest"
      for pool in $(ls "$dir" | sed -E 's/\.[^.]*$//; s/_[0-9]+$//' | sort -u); do
        rm -f "$dest/$pool".* "$dest/$pool"_[0-9]*.*
      done
      cp "$dir"/* "$dest/"
    done
    # <ANPC_LOCAL_SOUNDS>/_Sounds/va_<name>/ is a whole voice pack.
    for pack in "$LOCAL"/_Sounds/va_*; do
      [ -d "$pack" ] || continue
      mkdir -p "$DEFS/_Sounds"
      rm -rf "$DEFS/_Sounds/$(basename "$pack")"
      cp -r "$pack" "$DEFS/_Sounds/"
    done
    echo "Applied local sounds from $LOCAL"
  fi

  # Local meal sheets (made outside the repo; ship only as rendered sheets, never the 3D scenes): eat*/drink* sheets and their
  # sprites.json entries from <ANPC_LOCAL_MEALS>/<anpc>/sprites*/ are added to the installed sets again.
  MEALS="${ANPC_LOCAL_MEALS:-/f/_Projects/Dagerfall/output/arp_meals_batch/runtime/ANPCs}"
  [ -d "$MEALS" ] && python "$HERE/apply-local-meals.py" "$MEALS" "$DEFS"
fi
