#!/usr/bin/env bash
#
# Launch an INTERACTIVE, fully isolated RimWorld instance for driving the
# gif stage by hand — the observation twin of run-harness.sh, adapted from
# shift-change's run-scene.sh (same isolation, same safety posture).
#
#   devtools/run-scene.sh              # the scene mod list, refuses if a game runs
#   devtools/run-scene.sh --alongside  # start it BESIDE a running game
#   devtools/run-scene.sh --full       # your own mod list, copied (not swapped)
#   devtools/run-scene.sh --media      # the filming build (SCENES, no ECR)
#   devtools/run-scene.sh --bridge     # + RimBridgeServer for scripted capture
#
# Builds DEBUG by default — the stage only exists under SCENES, and Release
# does not define it. `--media` selects the Media config: same stage, no ECR
# instrumentation in the frame.
#
# The scene mod list is RimSort's "Shift Change Studio" loadout — the
# sibling's filming kit (Camera+, RimHUD, facial animation, VE apparel and
# textures, all five DLCs, and the stand/storage integrations: OSP, Armor
# Racks, ASF + sbz), taken verbatim with exactly ONE swap: Shift Change
# out, this mod in its slot. Its gizmos land on the very outfit stands
# being filmed and would photobomb every take. The wardrobe stage needs
# Royalty; the studio list carries it.
#
# --bridge appends the RimBridgeServer FORK (mrbeverage.rimbridgeserverfork,
# Mods/RimBridgeServerFork) for scripted capture — screenshots, stepped
# sequences, and since 2026-08-29 typed text entry and key presses (operating
# manual: rimworld-docs/rimbridge-input-tools-handoff.md; synthetic
# right-click inside window bodies is the one remaining gap). The port is
# FORCED per-instance through the GABS env handshake (GABP_SERVER_PORT +
# GABP_TOKEN, read by Lib.GAB.GabpServerBuilder): the LIVE game carries the
# bridge too and its standalone default owns 5174 — a bind collision found
# the hard way — so scene bridges run on 5175 with a fixed token, both
# printed at launch. Standalone-mode logs still announce port+token, and
# the devtools/bridge/ drivers accept either a log path or port+token.
#
# ISOLATION and the no-touching-a-running-game rule are run-harness.sh's,
# verbatim. Do not reach for pkill — a running instance is somebody's colony.
#
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP="/Users/alexbeverage/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app"
LIVE_CONFIG="/Users/alexbeverage/Library/Application Support/RimWorld/Config/ModsConfig.xml"
SCENEDATA="$REPO/dist/scenedata"
LOG="$SCENEDATA/Player.log"
PROC="RimWorld by Ludeon Studios"

SCENE_MODS=(
  brrainz.harmony
  ilyvion.loadingprogress
  taranchuk.fastergameloading
  ludeon.rimworld
  ludeon.rimworld.royalty
  ludeon.rimworld.ideology
  ludeon.rimworld.biotech
  ludeon.rimworld.anomaly
  ludeon.rimworld.odyssey
  adaptive.storage.framework
  oskarpotocki.vanillafactionsexpanded.core
  nals.facialanimation
  sbz.neatstorage
  lwm.deepstorage
  khamenman.armorracks
  brrainz.cameraplus
  void.charactereditor
  dracoix.doormat.r12a
  khamenman.outfitstandsplus
  jaxe.rimhud
  memegoddess.tdfindlib
  memegoddess.tdsbugfixes
  vanillaexpanded.vappe
  vanillaexpanded.vtexe
  vanillaexpanded.vtexvariations
  ih.clean.textures
  memegoddess.ctrlf
  mrbeverage.apparelpainter
  vanillaexpanded.vtexe.facialanims
)

FULL=0
ALONGSIDE=0
BRIDGE=0
CONFIG=Debug
for arg in "$@"
do
  case "$arg" in
    --full) FULL=1 ;;
    --alongside) ALONGSIDE=1 ;;
    --media) CONFIG=Media ;;
    --bridge) BRIDGE=1 ;;
    *) printf 'unknown option: %s (--full | --alongside | --media | --bridge)\n' "$arg" >&2; exit 2 ;;
  esac
done

if [ "$BRIDGE" = "1" ] && [ "$FULL" = "1" ]
then
  printf 'error: --bridge builds the scene list; it cannot ride the copied live list\n' >&2
  exit 2
fi

if [ "$BRIDGE" = "1" ]
then
  SCENE_MODS+=(mrbeverage.rimbridgeserverfork)
fi

die() { printf 'error: %s\n' "$1" >&2; exit 1; }

if pgrep -x "$PROC" >/dev/null && [ "$ALONGSIDE" = "0" ]
then
  die "RimWorld is already running, and this script will not touch it.
       Quit it by hand, or pass --alongside to start a second, fully
       isolated instance beside it."
fi

# The dll the game loads is the one on disk. BOTH configs write the same
# Assemblies/ApparelPainter.dll, which is also the committed artifact — a
# session that ends here leaves a SCENES build on disk. Rebuild Release
# before committing or publishing.
dotnet build "$REPO/Source/ApparelPainter/ApparelPainter.csproj" -c "$CONFIG" >/dev/null \
  || die "$CONFIG build failed — fix that first"
printf 'build: %s\n' "$CONFIG"

MODS_ENTRY="$APP/Mods/ApparelPainter"
[ -e "$MODS_ENTRY" ] || die "no Mods/ApparelPainter entry — the game cannot load this mod at all"
realpath_of() { python3 -c 'import os,sys; print(os.path.realpath(sys.argv[1]))' "$1"; }
if [ "$(realpath_of "$MODS_ENTRY")" != "$(realpath_of "$REPO")" ]
then
  die "Mods/ApparelPainter does not point at this checkout."
fi

rm -rf "$SCENEDATA"
mkdir -p "$SCENEDATA/Config"

if [ "$FULL" = "1" ]
then
  [ -f "$LIVE_CONFIG" ] || die "no live ModsConfig.xml to copy from"
  cp "$LIVE_CONFIG" "$SCENEDATA/Config/ModsConfig.xml"
  printf 'mod list: yours, copied (not swapped)\n'
else
  version="$(grep -m1 '<version>' "$LIVE_CONFIG" 2>/dev/null || printf '  <version>1.6.4871 rev595</version>')"
  {
    printf '<?xml version="1.0" ?>\n<ModsConfigData>\n'
    printf '%s\n  <activeMods>\n' "$version"
    for mod in "${SCENE_MODS[@]}"
    do
      printf '    <li>%s</li>\n' "$mod"
    done
    printf '  </activeMods>\n  <knownExpansions>\n'
    printf '    <li>ludeon.rimworld.royalty</li>\n'
    printf '    <li>ludeon.rimworld.ideology</li>\n'
    printf '    <li>ludeon.rimworld.biotech</li>\n'
    printf '    <li>ludeon.rimworld.anomaly</li>\n'
    printf '    <li>ludeon.rimworld.odyssey</li>\n'
    printf '  </knownExpansions>\n</ModsConfigData>\n'
  } > "$SCENEDATA/Config/ModsConfig.xml"
  xmllint --noout "$SCENEDATA/Config/ModsConfig.xml" || die "generated mod list is not well-formed"
  printf 'mod list: scene (%s mods, isolated)\n' "${#SCENE_MODS[@]}"
fi

# Seed Prefs: dev mode on (the stage lives behind it), keep simulating in
# the background, and stay silent beside a live game.
{
  printf '<?xml version="1.0" encoding="utf-8"?>\n'
  printf '<PrefsData>\n'
  printf '  <devMode>True</devMode>\n'
  printf '  <runInBackground>True</runInBackground>\n'
  printf '  <volumeMaster>0</volumeMaster>\n'
  printf '</PrefsData>\n'
} > "$SCENEDATA/Config/Prefs.xml"

if [ "$BRIDGE" = "1" ]
then
  export GABP_SERVER_PORT=5175
  export GABP_TOKEN=apparelpainter-scene-bridge
fi

printf 'save data: %s\n' "$SCENEDATA"
printf 'launching…\n'

"$APP/Contents/MacOS/$PROC" -quicktest \
  "-savedatafolder=$SCENEDATA" -logfile "$LOG" >/dev/null 2>&1 &
GAME_PID=$!

sleep 10
if ! kill -0 "$GAME_PID" 2>/dev/null
then
  die "instance exited within 10s — check $LOG"
fi

printf 'pid: %s (leave it to the player; this script does not manage it)\n' "$GAME_PID"
printf 'log: %s\n' "$LOG"
if [ "$BRIDGE" = "1" ]
then
  printf 'bridge: 127.0.0.1:%s  token %s\n' "$GABP_SERVER_PORT" "$GABP_TOKEN"
  printf 'bridge: devtools/bridge/gabp.py %s %s   # lists the tool surface\n' "$GABP_SERVER_PORT" "$GABP_TOKEN"
fi
printf 'in-game: dev mode is pre-seeded. Debug actions → Apparel Painter → Build gif stage or Build wardrobe stage, then click the stage south-west corner.\n'
