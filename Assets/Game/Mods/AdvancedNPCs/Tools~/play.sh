#!/bin/bash
# Starts Daggerfall Unity straight into a save for manual testing: no options screen, no title menu clicks,
# no self-test, no auto-quit. Uses the mod's autorun flag file (removed by the mod once read).
#
# While the game starts, settings.ini has ShowOptionsAtStart = False; the line is set back when the game
# exits (only that line, so settings changed in game are kept).
#
# Usage: play.sh [character] [save name] [DFU install]
#        defaults: Testo AtDaggerfall F:/_Projects/Dagerfall/DFU_testing
CHAR="${1:-Testo}"
SAVE="${2:-AtDaggerfall}"
DFU="${3:-/f/_Projects/Dagerfall/DFU_testing}"
DATA="$USERPROFILE/AppData/LocalLow/Daggerfall Workshop/Daggerfall Unity"
INI="$DATA/settings.ini"
FLAG="$DFU/DaggerfallUnity_Data/StreamingAssets/ANPCs/selftest-autorun.txt"

if tasklist | grep -qi DaggerfallUnity.exe; then
  echo "Close Daggerfall Unity first."
  exit 2
fi

SHOWED_OPTIONS=0
grep -q "^ShowOptionsAtStart = True" "$INI" && SHOWED_OPTIONS=1
restore() {
  [ "$SHOWED_OPTIONS" = 1 ] && sed -i 's/^ShowOptionsAtStart = False/ShowOptionsAtStart = True/' "$INI"
  rm -f "$FLAG"
}
trap restore EXIT

sed -i 's/^ShowOptionsAtStart = True/ShowOptionsAtStart = False/' "$INI"
mkdir -p "$(dirname "$FLAG")"
printf '%s\n%s\nplay\n' "$CHAR" "$SAVE" > "$FLAG"

echo "Starting Daggerfall Unity and loading '$SAVE' ($CHAR)..."
(cd "$DFU" && cmd //c start "" DaggerfallUnity.exe)
sleep 10
while tasklist | grep -qi DaggerfallUnity.exe; do
  sleep 5
done
echo "Game closed; settings.ini restored."
