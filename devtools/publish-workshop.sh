#!/usr/bin/env bash
#
# Stage the mod for a Steam Workshop upload, and swap it into the game's Mods
# folder in place of the development symlink. Ported from shift-change's
# publish-workshop.sh — same verbs, same safety posture, this mod's names and
# file set.
#
# WHY THIS EXISTS
#
# RimWorld's in-game uploader publishes the mod's folder verbatim. Workshop.cs
# hands `hook.Directory.FullName` straight to `SteamUGC.SetItemContent`, that
# resolves to `ModMetaData.GetWorkshopUploadDirectory()`, and that returns
# `RootDir` with no filtering — the one hook that could strip anything,
# `PrepareForWorkshopUpload()`, has an empty body. `CanToUploadToWorkshop()`
# also requires the mod to sit in `Mods/`.
#
# `Mods/ApparelPainter` is a symlink to the working repository. Uploading
# through it would publish `Source/`, `media/`, `devtools/`, `dist/`,
# `.DS_Store` and the entire `.git` directory to every subscriber — the
# sibling mod shipped 61 MB exactly that way, twice, on 2026-08-22. This
# script stages the release file set and uploads from that instead.
#
# USAGE
#
#   devtools/publish-workshop.sh            # stage + install, the safe default
#   devtools/publish-workshop.sh stage      # build + assemble dist/ApparelPainter
#   devtools/publish-workshop.sh install    # swap it into Mods/, dev link aside
#   devtools/publish-workshop.sh restore    # dev link back, recover the item id
#
# The normal run is (no argument) -> upload in game -> restore.
#
# STAGING ALONE CHANGES NOTHING ABOUT WHAT THE GAME UPLOADS. `stage` only fills
# dist/; until `install` has replaced the dev symlink, the uploader still
# publishes the whole repository through it. That is why the bare invocation
# does both, and why `stage` says so on its way out.
#
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST="$REPO/dist/ApparelPainter"
MODS="/Users/alexbeverage/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods"
LIVE="$MODS/ApparelPainter"

# Exactly the release allowlist docs/DEVELOPMENT.md documents: what the game
# loads, plus the human-readable documents. Keep the two in step. (No Defs/
# and no Patches/ — this mod ships none; Textures/ carries the dropper icon.)
CONTENT=(About Assemblies Languages Textures docs LICENSE README.md)

die() { printf 'error: %s\n' "$1" >&2; exit 1; }

# install and restore both replace the path a running game loaded the mod
# from, and the game rewrites ModsConfig.xml on exit over whatever the swap
# did. Same refusal as run-harness.sh: quit first.
ensure_game_closed() {
  if pgrep -x "RimWorld by Ludeon Studios" >/dev/null; then
    die "RimWorld is running — quit it before touching Mods/"
  fi
}

cmd_stage() {
  command -v dotnet >/dev/null || die "dotnet not on PATH"

  # Release, always. Debug and Media write the same Assemblies/ path; one
  # carries the hot-reload rig, both carry the destructive scene stages.
  dotnet build "$REPO/Source/ApparelPainter/ApparelPainter.csproj" -c Release

  rm -rf "$DIST"
  mkdir -p "$DIST"
  for item in "${CONTENT[@]}"; do
    [ -e "$REPO/$item" ] || die "missing from the repo: $item"
    cp -R "$REPO/$item" "$DIST/"
  done

  # Steam identifies the item by a file the game writes INSIDE the upload root
  # after a successful publish. If a previous publish left one in the repo, it
  # has to travel with the staged copy, or the uploader takes the create
  # branch and mints a second, duplicate listing.
  if [ -f "$REPO/About/PublishedFileId.txt" ]; then
    printf 'carrying existing item id: %s\n' "$(cat "$REPO/About/PublishedFileId.txt")"
  else
    printf 'no PublishedFileId.txt — the uploader will CREATE a Workshop item.\n'
    printf 'On the FIRST publish that is exactly right (the file appears after\n'
    printf 'the upload; restore recovers it, then commit it). On any LATER\n'
    printf 'publish it means the item id was lost — stop and recover it from\n'
    printf 'git history before uploading, or a duplicate listing is minted.\n'
  fi

  # Belt and braces, twice. The allowlist is positive, but a stray dll under
  # Assemblies/ is the one thing that reaches the game's load path — and a
  # .dds beside a PNG silently shadows it in game, no timestamp check, and
  # texture tools on this machine write .dds into the repo through the dev
  # symlink. Neither ever ships.
  find "$DIST" -name '.DS_Store' -delete
  find "$DIST" -name '*.dds' -delete
  local strays
  strays="$(find "$DIST/Assemblies" -type f ! -name 'ApparelPainter.dll' | wc -l | tr -d ' ')"
  [ "$strays" = "0" ] || die "Assemblies/ holds $strays file(s) besides ApparelPainter.dll"

  printf '\nstaged %s\n' "$DIST"
  du -sh "$DIST"
  printf '\ncontents:\n'
  ls -1 "$DIST"

  if [ -L "$LIVE" ]; then
    printf '\n!! STAGED ONLY. %s is still the dev symlink, so an upload now\n' "$LIVE"
    printf '!! would publish the whole repository. Run: %s install\n' "$0"
  fi
}

cmd_install() {
  ensure_game_closed
  [ -d "$DIST" ] || die "nothing staged — run 'stage' first"
  [ -d "$MODS" ] || die "game Mods folder not found: $MODS"

  # Two folders sharing packageId MrBeverage.ApparelPainter would both appear
  # in the mod list, and ModLister's first-wins race would decide which one
  # the uploader publishes. A dot prefix does NOT hide a directory from the
  # game — ModLister.cs:111 enumerates GetDirectories() unfiltered — so the
  # dev symlink cannot be parked anywhere inside Mods/. Delete it instead;
  # restore recreates it with one ln -s.
  if [ -L "$LIVE" ]; then
    rm "$LIVE"
    printf 'removed the dev symlink (restore recreates it)\n'
  elif [ -d "$LIVE" ]; then
    die "$LIVE is a real directory, not the dev symlink — resolve by hand"
  fi

  cp -R "$DIST" "$LIVE"

  # The confirmation Steam never gives you. The uploader shows no manifest, no
  # size and no file list before publishing — SetItemContent takes the folder
  # verbatim and PrepareForWorkshopUpload() is an empty method — so this is
  # the only chance to see what is about to go out.
  printf '\n=== this is what the game will upload ===\n'
  printf 'from:      %s\n' "$LIVE"
  if [ -f "$LIVE/About/PublishedFileId.txt" ]; then
    printf 'item id:   %s (updates the existing item)\n' "$(cat "$LIVE/About/PublishedFileId.txt")"
  else
    printf 'item id:   NONE — the uploader will CREATE the Workshop item.\n'
    printf '           Expected on the FIRST publish and on no other: if this\n'
    printf '           mod is already on the Workshop, STOP — uploading now\n'
    printf '           mints a second, duplicate listing.\n'
  fi
  printf 'size:      %s\n' "$(du -sh "$LIVE" | cut -f1)"
  printf 'top level: %s\n' "$(ls -1 "$LIVE" | tr '\n' ' ')"
  printf '=========================================\n'
  cat <<'EOF'

Now, in game (launch fresh — Development mode must be ON in Options, or the
upload entry does not exist at all; Page_ModsConfig.cs:718):

  1. Enable Apparel Painter in the mod list and confirm it loads clean:
     exactly ONE Apparel Painter entry, and no "same packageId multiple
     times" error in the log.
  2. Select it -> More actions -> Upload to Steam Workshop -> Confirm.
     The confirm dialog has a "Tag as translation" checkbox — leave it
     UNTICKED, or the item trades its Mod tag for Translation and drops out
     of the Workshop's default mod browse.
  3. Wait for the progress dialog to finish and the item page to open.
     Then quit the game.

THEN, IMMEDIATELY, IN THIS ORDER:

  1. BROWSER: set the item's visibility to PRIVATE and confirm it by eye
     (owner controls on the item page). The game never sets visibility —
     SetItemVisibility appears nowhere in the engine — so until checked the
     item sits in whatever state Steam defaulted it to. Everything below
     happens while it is private.

  2. BROWSER: confirm the item actually published — open its page from a
     private window. The game reports success even if the Workshop Legal
     Agreement was never accepted, and an unaccepted agreement leaves an
     item only its owner can see.

  3. TERMINAL: devtools/publish-workshop.sh restore
     Then commit About/PublishedFileId.txt — it is the only record of which
     Workshop item this mod is, and skipping restore leaves the game loading
     a frozen snapshot instead of the checkout.

  4. BROWSER: confirm github.com/beverage/apparel-painter is PUBLIC before
     touching the description. Every image on the store page embeds from
     raw.githubusercontent.com/beverage/apparel-painter/main/media/ — on a
     private repo the description renders as broken images top to bottom.

  5. BROWSER: paste media/steam-description.bbcode into the description.

     Do not skip this and plan to do it later from the game. RimWorld calls
     SetItemDescription only on the CREATE branch (Workshop.cs:262-265), from
     About.xml's <description> — so the store page opens showing the in-game
     mod-list blurb, and no later in-game update will ever replace it. The
     web editor is the only route. The BBCode carries the gifs, the banner
     cards, the FAQ, the source link and the AI-assistance disclosure, none
     of which are in About.xml by design. Validate first if it changed:
     devtools/bbcode-preview.py reports tags and the 8,000-character margin.

  6. BROWSER: add the gallery images through the item page's owner controls.
     media/preview-animated.gif goes in FIRST — it is gallery slot 1 — then
     media/gallery-wardrobe-before.png and -after.png, and any cards worth
     repeating (media/cards/). All current candidates are under Steam's
     ~1 MB item-image limit; the game itself uploads only About/Preview.png
     (SetItemPreview, Workshop.cs:266-272) and never calls
     AddItemPreviewFile, so this browser step is the only route up.

  7. VERIFY AS A SUBSCRIBER, still private: subscribe in the browser, launch
     the game, and enable the STEAM-sourced Apparel Painter entry (with the
     dev symlink back, both copies appear in the list; leave the local one
     disabled). Confirm a clean load, a Paint tab on a stocked stand or
     shelf, and no red errors. Quit, unsubscribe, re-enable the local copy.

  8. Only then set visibility to PUBLIC.

  9. REPO: drop the live Workshop link into README.md's Status section (the
     one-line edit planned there) and commit it with PublishedFileId.txt if
     that is still uncommitted.
EOF
}

cmd_restore() {
  ensure_game_closed
  # The uploader wrote the new item id into the staged copy. It is the only
  # record of which Workshop item this mod is, and losing it means the next
  # upload creates a duplicate listing instead of updating this one. Guarded
  # on the staged copy actually being present: with the dev symlink already
  # back in place there is nothing to recover, and a repeat run is a no-op,
  # not an error.
  if [ ! -L "$LIVE" ] && [ -f "$LIVE/About/PublishedFileId.txt" ]; then
    local id
    id="$(cat "$LIVE/About/PublishedFileId.txt")"
    if [ -f "$REPO/About/PublishedFileId.txt" ] \
       && ! diff -q "$LIVE/About/PublishedFileId.txt" "$REPO/About/PublishedFileId.txt" >/dev/null; then
      die "item id changed ($id) — a duplicate listing was probably created; resolve by hand"
    fi
    cp "$LIVE/About/PublishedFileId.txt" "$REPO/About/"
    printf 'recovered item id %s into the repo — COMMIT IT\n' "$id"
  elif [ ! -L "$LIVE" ] && [ -d "$LIVE" ]; then
    printf 'no PublishedFileId.txt in the uploaded copy (upload not run, or it failed)\n'
  fi

  if [ -d "$LIVE" ] && [ ! -L "$LIVE" ]; then
    rm -rf "$LIVE"
  fi
  ln -sfn "$REPO" "$LIVE"
  printf 'dev symlink in place\n'
}

case "${1:-publish}" in
  publish) cmd_stage; cmd_install ;;
  stage)   cmd_stage ;;
  install) cmd_install ;;
  restore) cmd_restore ;;
  *)       die "unknown command: $1 (publish | stage | install | restore)" ;;
esac
