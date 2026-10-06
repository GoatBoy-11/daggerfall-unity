#!/bin/bash
# Runs the AdvancedNPCs.Core EditMode tests headless in a small Unity 6 host project whose
# Assets/AdvancedNPCs_Core and Assets/AdvancedNPCs_Tests are junctions to this mod's
# Scripts/Core and Editor/Tests folders. (The DFU project itself needs Unity 2019.4, which
# has no batch-mode license on this machine; Core has no DFU dependencies, so any Unity runs it.)
# Exit 0 only when every test passed.
HOST="${ANPC_TEST_HOST:-F:/_Projects/Dagerfall/anpc_testhost}"
UNITY="${ANPC_TEST_UNITY:-/c/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe}"
OUTDIR="${ANPC_TEST_OUT:-F:/_Projects/Dagerfall}"
RESULTS="$OUTDIR/test-results.xml"
LOG="$OUTDIR/unity-test.log"

rm -f "$RESULTS"
"$UNITY" -batchmode -nographics -projectPath "$HOST" -runTests -testPlatform EditMode \
  -assemblyNames AdvancedNPCs.Tests -testResults "$RESULTS" -logFile "$LOG"
echo "unity exit: $?"
if [ ! -f "$RESULTS" ]; then
  echo "NO RESULTS FILE (compile error?)"
  grep -E "error CS[0-9]+" "$LOG" | sort -u | head -20
  exit 1
fi
grep -o '<test-run[^>]*>' "$RESULTS" | grep -o 'result="[^"]*"\|total="[^"]*"\|passed="[^"]*"\|failed="[^"]*"' | tr '\n' ' '; echo
grep -o '<test-case [^>]*result="Failed"[^>]*>' "$RESULTS" | grep -o 'fullname="[^"]*"' | head -20
grep -q '<test-run[^>]*result="Passed"' "$RESULTS"
