#!/usr/bin/env bash
#
# Run the Stand Painter regression harness end to end, in an ISOLATED game
# instance. Adapted from shift-change's run-harness.sh — same isolation, same
# safety posture, same exit-code contract.
#
#   devtools/run-harness.sh          # a three-mod list — the iteration loop
#   devtools/run-harness.sh --full   # your own mod list, copied — the release
#                                    # gate, and the only list that covers the
#                                    # Dubs Paint Shop interop case
#
# Builds Release, launches RimWorld with -quicktest -standpainter-harness
# against a throwaway save-data folder, waits for the game to run every case
# and quit itself, prints the report. Exits non-zero if any case failed.
#
# ISOLATION: -savedatafolder gives the test instance its own ModsConfig.xml,
# Saves/ and Prefs; -logfile moves Player.log. Nothing under
# ~/Library/Application Support/RimWorld or ~/Library/Logs is read or written
# (the live ModsConfig.xml is READ once for --full, never written).
#
# IT WILL NOT TOUCH A RUNNING GAME. If RimWorld is running, this refuses and
# stops. It does not kill it, and nobody should reach for pkill to get past
# it: that instance is somebody's colony with unsaved progress. Ask, then
# quit it by hand — or pass --alongside once the machine is confirmed able to
# carry both.
#
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP="/Users/alexbeverage/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app"
LIVE_CONFIG="/Users/alexbeverage/Library/Application Support/RimWorld/Config/ModsConfig.xml"
TESTDATA="$REPO/dist/testdata"
LOG="$TESTDATA/Player.log"
PROC="RimWorld by Ludeon Studios"
# A passing run takes ~20s; the ceiling covers a backgrounded loading screen,
# which stalls until the window comes forward (see shift-change's notes).
TIMEOUT=1200

# Core, Odyssey (the outfit stand) and us. No Harmony — this mod does not
# use it. Dubs Paint Shop is deliberately absent here: its interop case
# SKIPs on this list and runs on --full.
MINIMAL_MODS=(
  ludeon.rimworld
  ludeon.rimworld.odyssey
  mrbeverage.standpainter
)

FULL=0
ALONGSIDE=0
for arg in "$@"
do
  case "$arg" in
    --full) FULL=1 ;;
    --alongside) ALONGSIDE=1 ;;
    *) printf 'unknown option: %s (--full | --alongside)\n' "$arg" >&2; exit 2 ;;
  esac
done

die() { printf 'error: %s\n' "$1" >&2; exit 1; }

if pgrep -x "$PROC" >/dev/null && [ "$ALONGSIDE" = "0" ]
then
  die "RimWorld is already running, and this script will not touch it.
       Confirm that instance is free and quit it by hand, or pass --alongside
       to start a second, fully isolated one beside it."
fi

# The dll the game loads is the one on disk, not the one in your editor.
dotnet build "$REPO/Source/StandPainter/StandPainter.csproj" -c Release >/dev/null \
  || die "Release build failed — fix that first"

# THE BUILD IS NOT THE THING THE GAME LOADS. -savedatafolder isolates Config,
# Saves and Prefs, but NOT the mod: the game reads Mods/StandPainter out of
# the app bundle. A release-staging copy or a worktree there means a green
# run silently asserts against the wrong bits — the worst shape a test result
# can take. Compare canonical paths and refuse.
MODS_ENTRY="$APP/Mods/StandPainter"
[ -e "$MODS_ENTRY" ] || die "no Mods/StandPainter entry — the game cannot load this mod at all"
realpath_of() { python3 -c 'import os,sys; print(os.path.realpath(sys.argv[1]))' "$1"; }
ENTRY_REAL="$(realpath_of "$MODS_ENTRY")"
REPO_REAL="$(realpath_of "$REPO")"
if [ "$ENTRY_REAL" != "$REPO_REAL" ]
then
  die "the game would NOT load the build this script just made.

       built:  $REPO_REAL
       loads:  $ENTRY_REAL

       Point Mods/StandPainter at the checkout under test and run again."
fi
printf 'load path: %s\n' "$ENTRY_REAL"

rm -rf "$TESTDATA"
mkdir -p "$TESTDATA/Config"

if [ "$FULL" = "1" ]
then
  [ -f "$LIVE_CONFIG" ] || die "no live ModsConfig.xml to copy from"
  cp "$LIVE_CONFIG" "$TESTDATA/Config/ModsConfig.xml"
  printf 'mod list: yours, copied (not swapped)\n'
else
  version="$(grep -m1 '<version>' "$LIVE_CONFIG" 2>/dev/null || printf '  <version>1.6.4871 rev595</version>')"
  {
    printf '<?xml version="1.0" ?>\n<ModsConfigData>\n'
    printf '%s\n  <activeMods>\n' "$version"
    for mod in "${MINIMAL_MODS[@]}"
    do
      printf '    <li>%s</li>\n' "$mod"
    done
    printf '  </activeMods>\n  <knownExpansions>\n'
    printf '    <li>ludeon.rimworld.odyssey</li>\n'
    printf '  </knownExpansions>\n</ModsConfigData>\n'
  } > "$TESTDATA/Config/ModsConfig.xml"
  xmllint --noout "$TESTDATA/Config/ModsConfig.xml" || die "generated mod list is not well-formed"
  printf 'mod list: minimal (%s mods, isolated)\n' "${#MINIMAL_MODS[@]}"
fi

printf 'save data: %s\n' "$TESTDATA"
printf 'launching…\n'

# The binary directly, not `open`: $! must be OUR pid, and only ever ours.
"$APP/Contents/MacOS/$PROC" -quicktest -standpainter-harness \
  "-savedatafolder=$TESTDATA" -logfile "$LOG" >/dev/null 2>&1 &
GAME_PID=$!
printf 'pid: %s\n' "$GAME_PID"

elapsed=0
until [ "$elapsed" -ge "$TIMEOUT" ]
do
  sleep 5
  elapsed=$((elapsed + 5))
  kill -0 "$GAME_PID" 2>/dev/null || break
done

if kill -0 "$GAME_PID" 2>/dev/null
then
  kill "$GAME_PID" 2>/dev/null || true
  die "timed out after ${TIMEOUT}s; stopped pid $GAME_PID — check $LOG"
fi
printf 'game exited after ~%ss\n\n' "$elapsed"

[ -f "$LOG" ] || die "no log at $LOG — did -logfile take?"
grep -q "harness auto-run" "$LOG" \
  || die "the harness never ran — is -standpainter-harness still wired up? See $LOG"

sed -n '/\[StandPainter\] regression harness/,/harness auto-run/p' "$LOG"

grep -q "harness auto-run: PASSED" "$LOG"
