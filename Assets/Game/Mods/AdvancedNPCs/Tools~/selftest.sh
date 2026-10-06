#!/bin/bash
# Unattended in-game self-test: starts Daggerfall Unity, loads a save standing outdoors in a town,
# runs anpc_selftest, quits, and prints the SELFTEST lines. Exit 0 only if every check passed.
#
# While it runs, settings.ini has ShowOptionsAtStart = False (so the startup options screen is
# skipped); the original settings.ini is restored afterwards. A flag file in
# StreamingAssets/AdvancedNPCs tells the mod to auto-run; it is removed afterwards.
#
# Usage: selftest.sh [character] [save name] [DFU install]
#        defaults: Testo AtDaggerfall F:/_Projects/Dagerfall/DFU_testing
CHAR="${1:-Testo}"
SAVE="${2:-AtDaggerfall}"
DFU="${3:-/f/_Projects/Dagerfall/DFU_testing}"
DATA="$USERPROFILE/AppData/LocalLow/Daggerfall Workshop/Daggerfall Unity"
INI="$DATA/settings.ini"
LOG="$DATA/Player.log"
FLAG="$DFU/DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs/selftest-autorun.txt"

if tasklist | grep -qi DaggerfallUnity.exe; then
  echo "Close Daggerfall Unity first."
  exit 2
fi

cp "$INI" "$INI.anpc-backup"
restore() {
  mv -f "$INI.anpc-backup" "$INI"
  rm -f "$FLAG"
}
trap restore EXIT

sed -i 's/^ShowOptionsAtStart = True/ShowOptionsAtStart = False/' "$INI"
mkdir -p "$(dirname "$FLAG")"
printf '%s\n%s\n' "$CHAR" "$SAVE" > "$FLAG"

echo "Starting Daggerfall Unity; loading '$SAVE' ($CHAR) and running the self-test..."
(cd "$DFU" && cmd //c start "" DaggerfallUnity.exe)
sleep 10
for i in $(seq 1 60); do
  tasklist | grep -qi DaggerfallUnity.exe || break
  sleep 5
done
if tasklist | grep -qi DaggerfallUnity.exe; then
  echo "Timed out after 5 minutes; closing the game."
  taskkill //IM DaggerfallUnity.exe //F > /dev/null
fi

grep -E "\[AdvancedNPCs\] SELFTEST|Exception" "$LOG" | sed 's/^\[AdvancedNPCs\] //'
grep -q "SELFTEST DONE" "$LOG" && ! grep -q "SELFTEST FAIL" "$LOG"
