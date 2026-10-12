#!/usr/bin/env bash
#
# Run the Apparel Painter regression harness end to end, in an ISOLATED game
# instance. Adapted from shift-change's run-harness.sh — same isolation, same
# safety posture, same exit-code contract.
#
#   devtools/run-harness.sh          # three mods plus your DLCs — the iteration loop
#   devtools/run-harness.sh --full   # your own mod list, copied — the release
#                                    # gate, and the only list that covers the
#                                    # Dubs Paint Shop interop case
#   devtools/run-harness.sh --no-dlc # the three, and no DLC but Odyssey
#
# THE MINIMAL LIST CARRIES EVERY DLC YOU OWN, UNLESS --no-dlc
#
# RimWorld switches on any installed expansion that ModsConfig.xml does not
# list under knownExpansions (ModsConfig's static constructor). This list used
# to name Odyssey alone there, which switched nothing off, so the default is
# Core, all five DLCs and us, and every pass count recorded before this was
# measured that way. It stays the default (decided 2026-10-12): most players
# own them, and the style cases need Ideology, Royalty or Anomaly to run.
#
# --no-dlc lists all five as known, so only the DLCs the list itself names
# stay on: Odyssey, whose outfit stand nearly every case spawns. Without the
# other four the style index is empty and the style cases SKIP, as they would
# for a player who owns none of them. Neat Edges' copy of this option found a
# bug that only players without Ideology could hit.
#
# Builds Release with -p:Harness=true (a plain Release build compiles the
# harness out), launches RimWorld with -quicktest -apparelpainter-harness
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
# quit it by hand — or pass --alongside, with the note below.
#
# --alongside USED TO NEED A CLICK ON THE NEW WINDOW. IT NO LONGER DOES.
# On 2026-09-02 a run beside a live game stalled at "Initializing new game
# with mods" until the 1200s timeout, because an unfocused instance never got
# past its loading screen. The cause was RimWorld's own runInBackground
# preference, off by default in a fresh save-data folder; this script now
# seeds it on (the Prefs.xml block below has the measurement). Another game
# can still make a run slower by competing for the CPU.
#
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Steam's default install and RimWorld's save folder. Override either for a
# library on another volume.
APP="${RIMWORLD_APP:-$HOME/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app}"
LIVE_CONFIG="${RIMWORLD_USERDATA:-$HOME/Library/Application Support/RimWorld}/Config/ModsConfig.xml"
TESTDATA="$REPO/dist/testdata"
LOG="$TESTDATA/Player.log"
PROC="RimWorld by Ludeon Studios"
# A passing run takes ~20s on the minimal list and a few minutes on a large
# one. The ceiling is a backstop: since runInBackground is seeded, an unfocused
# instance no longer stalls, so a run that reaches it is a real hang.
TIMEOUT=1200

# Core, Odyssey (the outfit stand) and us. No Harmony — this mod does not
# use it. Dubs Paint Shop is deliberately absent here: its interop case
# SKIPs on this list and runs on --full. The other four DLCs come on by
# themselves unless --no-dlc is passed; see the top of this file.
MINIMAL_MODS=(
  ludeon.rimworld
  ludeon.rimworld.odyssey
  mrbeverage.apparelpainter
)

# Listed as known by --no-dlc, so the game leaves off every one the list does
# not name. Naming one a player does not own does nothing.
EXPANSIONS=(
  ludeon.rimworld.royalty
  ludeon.rimworld.ideology
  ludeon.rimworld.biotech
  ludeon.rimworld.anomaly
  ludeon.rimworld.odyssey
)

FULL=0
ALONGSIDE=0
NO_DLC=0
for arg in "$@"
do
  case "$arg" in
    --full) FULL=1 ;;
    --alongside) ALONGSIDE=1 ;;
    --no-dlc) NO_DLC=1 ;;
    *) printf 'unknown option: %s (--full | --alongside | --no-dlc)\n' "$arg" >&2; exit 2 ;;
  esac
done

die() { printf 'error: %s\n' "$1" >&2; exit 1; }

[ "$FULL" = "0" ] || [ "$NO_DLC" = "0" ] \
  || die "--no-dlc shapes the minimal list, and --full uses yours instead; pick one"

if pgrep -x "$PROC" >/dev/null && [ "$ALONGSIDE" = "0" ]
then
  die "RimWorld is already running, and this script will not touch it.
       Confirm that instance is free and quit it by hand, or pass --alongside
       to start a second, fully isolated one beside it."
fi

# The dll the game loads is the one on disk, not the one in your editor.
#
# -p:Harness=true is what compiles the harness and -apparelpainter-harness back
# in. They are NOT in a shipping build (see the configuration table in the
# csproj), so plain Release would launch a game that ignores the flag and sits
# there until the timeout. Release codegen otherwise, exactly as shipped.
dotnet build "$REPO/Source/ApparelPainter/ApparelPainter.csproj" -c Release -p:Harness=true >/dev/null \
  || die "harness build failed — fix that first"

# Leave Assemblies/ holding the SHIPPING dll again, whatever happens below.
# The game's Mods entry is a symlink to this checkout, so an un-swept harness
# build is what the next play session loads and what a careless `git add`
# commits. Same philosophy as the csproj's CleanDevArtifacts target: hygiene by
# construction, not by memory. It never changes this script's exit status.
restore_shipping_dll() {
  dotnet build "$REPO/Source/ApparelPainter/ApparelPainter.csproj" -c Release >/dev/null \
    || printf 'WARNING: Assemblies/ still holds the HARNESS build. Rebuild with:
         dotnet build Source/ApparelPainter/ApparelPainter.csproj -c Release\n' >&2
}
trap restore_shipping_dll EXIT

# THE BUILD IS NOT THE THING THE GAME LOADS. -savedatafolder isolates Config,
# Saves and Prefs, but NOT the mod: the game reads Mods/ApparelPainter out of
# the app bundle. A release-staging copy or a worktree there means a green
# run silently asserts against the wrong bits — the worst shape a test result
# can take. Compare canonical paths and refuse.
MODS_ENTRY="$APP/Mods/ApparelPainter"
[ -e "$MODS_ENTRY" ] || die "no Mods/ApparelPainter entry — the game cannot load this mod at all"
realpath_of() { python3 -c 'import os,sys; print(os.path.realpath(sys.argv[1]))' "$1"; }
ENTRY_REAL="$(realpath_of "$MODS_ENTRY")"
REPO_REAL="$(realpath_of "$REPO")"
if [ "$ENTRY_REAL" != "$REPO_REAL" ]
then
  die "the game would NOT load the build this script just made.

       built:  $REPO_REAL
       loads:  $ENTRY_REAL

       Point Mods/ApparelPainter at the checkout under test and run again."
fi
printf 'load path: %s\n' "$ENTRY_REAL"

rm -rf "$TESTDATA"
mkdir -p "$TESTDATA/Config"

if [ "$FULL" = "1" ]
then
  [ -f "$LIVE_CONFIG" ] || die "no live ModsConfig.xml to copy from"
  cp "$LIVE_CONFIG" "$TESTDATA/Config/ModsConfig.xml"
  printf 'mod list: yours, copied (not swapped)\n'
  # The copy must ACTIVATE the mod under test — a list that predates the
  # Apparel Painter rename (mrbeverage.standpainter) or simply has the mod
  # disabled would boot without us, and "the harness never ran" is the best
  # case; the worst is asserting nothing. Migrate the old id in the COPY,
  # or append ours. The live file is never written.
  if ! grep -qi 'mrbeverage.apparelpainter' "$TESTDATA/Config/ModsConfig.xml"
  then
    python3 - "$TESTDATA/Config/ModsConfig.xml" <<'EOF'
import sys
path = sys.argv[1]
s = open(path).read()
s = s.replace('<li>mrbeverage.standpainter</li>', '<li>mrbeverage.apparelpainter</li>')
if 'mrbeverage.apparelpainter' not in s:
    s = s.replace('</activeMods>', '  <li>mrbeverage.apparelpainter</li>\n  </activeMods>')
open(path, 'w').write(s)
EOF
    printf 'mod list: activated mrbeverage.apparelpainter in the copy (old id migrated or appended)\n'
  fi
else
  # No <version> element, on purpose. ModsConfig's static constructor throws
  # the whole list away ("Mods config data is from version ... Resetting.")
  # when that element names another major or minor version than the game
  # running, and skips the check when it is absent. This used to copy the line
  # from the live ModsConfig.xml, which carries the version of whichever game
  # last wrote it: once a 1.7 game had, every 1.6 run here booted without this
  # mod and the harness never ran.
  {
    printf '<?xml version="1.0" ?>\n<ModsConfigData>\n'
    printf '  <activeMods>\n'
    for mod in "${MINIMAL_MODS[@]}"
    do
      printf '    <li>%s</li>\n' "$mod"
    done
    printf '  </activeMods>\n'
    if [ "$NO_DLC" = "1" ]
    then
      printf '  <knownExpansions>\n'
      for dlc in "${EXPANSIONS[@]}"
      do
        printf '    <li>%s</li>\n' "$dlc"
      done
      printf '  </knownExpansions>\n'
    fi
    printf '</ModsConfigData>\n'
  } > "$TESTDATA/Config/ModsConfig.xml"
  xmllint --noout "$TESTDATA/Config/ModsConfig.xml" || die "generated mod list is not well-formed"
  printf 'mod list: minimal (%s mods, isolated, %s)\n' "${#MINIMAL_MODS[@]}" \
    "$([ "$NO_DLC" = "1" ] && printf 'no DLC unless named' || printf 'plus every DLC installed')"
fi

# SEED Prefs.xml — ported from the siblings 2026-09-18, after a run here took
# 635s against a measured ~25s because this script never received the fix.
#
# Windowed and muted because a fresh -savedatafolder has no Prefs.xml, so the
# instance starts on RimWorld's defaults, and the default is fullscreen — which
# on macOS asks for a fullscreen space a second instance cannot have. That part
# is shape, not a cure.
#
# runInBackground IS the cure. It is one of RimWorld's own preferences
# (Verse/PrefsData.cs:68) and DEFAULTS TO FALSE — the field has no initializer,
# and PrefsData.Apply() hands it straight to Unity as
# `Application.runInBackground` (:153). A fresh -savedatafolder therefore built a
# test instance with it OFF, and an unfocused instance then stalls on its
# loading screen: measured on the siblings 2026-09-11, 6 runs with focus held on
# another app, True passed 3/3 (startup in 20s) and False stalled 3/3.
#
# It lands early enough to matter because Prefs.Init() ENDS with an unqualified
# Apply() (Prefs.cs:861 — a grep for `Prefs.Apply` does not find it), inside
# Root.CheckGlobalInit() (Root.cs:104). Root.cs:72 sets the same flag
# unconditionally and is TOO LATE: with the pref false, an unfocused instance
# never reaches it.
{
  printf '<?xml version="1.0" encoding="utf-8"?>\n<PrefsData>\n'
  printf '  <screenWidth>1280</screenWidth>\n'
  printf '  <screenHeight>720</screenHeight>\n'
  printf '  <fullscreen>False</fullscreen>\n'
  printf '  <runInBackground>True</runInBackground>\n'
  printf '  <volumeMaster>0</volumeMaster>\n'
  printf '</PrefsData>\n'
} > "$TESTDATA/Config/Prefs.xml"
xmllint --noout "$TESTDATA/Config/Prefs.xml" || die "generated Prefs.xml is not well-formed"
printf 'display: windowed 1280x720, muted, runInBackground=True\n'

printf 'save data: %s\n' "$TESTDATA"
printf 'launching…\n'

# The binary directly, not `open`: $! must be OUR pid, and only ever ours.
"$APP/Contents/MacOS/$PROC" -quicktest -apparelpainter-harness \
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
  || die "the harness never ran — is -apparelpainter-harness still wired up? See $LOG"

sed -n '/\[ApparelPainter\] regression harness/,/harness auto-run/p' "$LOG"

grep -q "harness auto-run: PASSED" "$LOG"
