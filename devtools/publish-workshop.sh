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
#   devtools/publish-workshop.sh strip      # with the game at the main menu:
#                                           # delete what it wrote into the copy
#   devtools/publish-workshop.sh restore    # dev link back, recover the item id
#
# The normal run is (no argument) -> launch the game -> strip -> upload in
# game -> restore.
#
# STRIP EXISTS BECAUSE THE GAME WRITES INTO THE COPY IT IS ABOUT TO UPLOAD.
# Texture tools that convert PNGs to .dds on load (Faster Game Loading, mod
# managers with an auto-convert option) write the .dds beside each PNG in the
# installed copy, after `install` has checked it and before Upload reads it.
# The sibling Neat Edges' first upload carried 28 of them this way (2026-10-07).
# This mod ships one texture, so the exposure is one file; it is the same bug.
#
# STAGING ALONE CHANGES NOTHING ABOUT WHAT THE GAME UPLOADS. `stage` only fills
# dist/; until `install` has replaced the dev symlink, the uploader still
# publishes the whole repository through it. That is why the bare invocation
# does both, and why `stage` says so on its way out.
#
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST="$REPO/dist/ApparelPainter"
# RIMWORLD_APP overrides where the game is: the folder whose Mods/ it reads.
# 1.6 keeps Mods/ inside RimWorldMac.app; 1.7 keeps it at the install root,
# beside RimWorld.app.
STEAM_RIMWORLD="$HOME/Library/Application Support/Steam/steamapps/common/RimWorld"
if [ -n "${RIMWORLD_APP:-}" ]; then
  MODS="$RIMWORLD_APP/Mods"
elif [ -d "$STEAM_RIMWORLD/RimWorld.app" ]; then
  MODS="$STEAM_RIMWORLD/Mods"
else
  MODS="$STEAM_RIMWORLD/RimWorldMac.app/Mods"
fi
LIVE="$MODS/ApparelPainter"
# Where install notes the link it removed, so restore puts that link back (a
# play-test worktree, say) instead of always linking this checkout.
LINK_RECORD="$REPO/dist/.dev-link"

# Exactly the release allowlist docs/DEVELOPMENT.md documents: what the game
# loads, plus the human-readable documents. Keep the two in step. (No Defs/
# and no Patches/ — this mod ships none; Textures/ carries the dropper icon.)
CONTENT=(About Assemblies Languages Textures docs LICENSE README.md)

die() { printf 'error: %s\n' "$1" >&2; exit 1; }

# install and restore both replace the path a running game loaded the mod
# from, and the game rewrites ModsConfig.xml on exit over whatever the swap
# did. Same refusal as run-harness.sh: quit first. 1.6 runs as "RimWorld by
# Ludeon Studios" and 1.7 as "RimWorld", so check for both.
ensure_game_closed() {
  if pgrep -x "RimWorld by Ludeon Studios" >/dev/null || pgrep -x RimWorld >/dev/null; then
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

  # What is about to go out, checked as the bytes about to go out — not as the
  # copy in the repo. No debug scenes, no harness, no -apparelpainter-harness
  # launch flag, feature surface intact. The Release build above should make
  # all of that true by construction; this is the assertion that it did, on
  # the one file a subscriber actually receives.
  python3 "$REPO/devtools/check-shipped-dll.py" "$DIST/Assemblies/ApparelPainter.dll" \
    || die "the staged dll is not shippable — see above, and do not upload it"

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
  # dev symlink cannot be parked anywhere inside Mods/. Delete it instead,
  # noting where it pointed; restore recreates it with one ln -s.
  if [ -L "$LIVE" ]; then
    readlink "$LIVE" > "$LINK_RECORD"
    rm "$LIVE"
    printf 'removed the dev symlink to %s (restore puts it back)\n' "$(cat "$LINK_RECORD")"
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
  2. TERMINAL, with the game still open at the main menu:
     devtools/publish-workshop.sh strip
     It removes the .dds files the game wrote into the copy while starting,
     and must report that the copy matches the staged one before you upload.
  3. Select it -> More actions -> Upload to Steam Workshop -> Confirm.
     The confirm dialog has a "Tag as translation" checkbox — leave it
     UNTICKED, or the item trades its Mod tag for Translation and drops out
     of the Workshop's default mod browse.
  4. Wait for the progress dialog to finish and the item page to open.
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

  6. BROWSER: add the gallery images through the item page's owner controls,
     if any changed: media/preview-static.png (the titled static card, gallery
     slot 1), then media/gallery-wardrobe-before.png
     and -after.png, and any cards worth repeating (media/cards/). The
     animated preview needs nothing here: About/Preview.png IS
     media/preview-animated.gif's bytes under the PNG name, and the game
     uploads it with every update (SetItemPreview, Workshop.cs:266-272). It
     never calls AddItemPreviewFile, so this browser step is the only route
     up for the gallery.

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

cmd_strip() {
  # Deliberately no ensure_game_closed: this runs with the game open, after it
  # has loaded every texture, so the files it removes are no longer read.
  [ -d "$LIVE" ] && [ ! -L "$LIVE" ] || die "$LIVE is not an installed copy; run install first"
  [ -d "$DIST" ] || die "nothing staged to compare against; run stage first"
  local count
  count="$(find "$LIVE" \( -name '*.dds' -o -name '.DS_Store' \) | wc -l | tr -d ' ')"
  find "$LIVE" \( -name '*.dds' -o -name '.DS_Store' \) -delete
  printf 'removed %s file(s) the game wrote into the upload\n' "$count"
  printf 'size:      %s (staged: %s)\n' "$(du -sh "$LIVE" | cut -f1)" "$(du -sh "$DIST" | cut -f1)"
  printf 'top level: %s\n' "$(ls -1 "$LIVE" | tr '\n' ' ')"
  # Compared against the staged copy file by file, so anything else the game
  # added between install and upload shows up here too. The item id is the
  # one file the uploader itself writes, so it is the one difference allowed.
  local drift
  drift="$(diff -rq "$DIST" "$LIVE" | grep -v 'PublishedFileId.txt' || true)"
  [ -z "$drift" ] || die "the installed copy differs from the staged one; do not upload:
$drift"
  printf 'matches the staged copy: ready to upload\n'
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

  # A link already in place is left alone: re-pointing it here would move a
  # play-test link to this checkout on a repeat run.
  if [ -L "$LIVE" ]; then
    rm -f "$LINK_RECORD"
    printf 'dev symlink already in place: %s -> %s\n' "$LIVE" "$(readlink "$LIVE")"
    return
  fi
  if [ -d "$LIVE" ]; then
    rm -rf "$LIVE"
  fi
  local target="$REPO"
  if [ -f "$LINK_RECORD" ]; then
    target="$(cat "$LINK_RECORD")"
  fi
  ln -sfn "$target" "$LIVE"
  rm -f "$LINK_RECORD"
  printf 'dev symlink back in place: %s -> %s\n' "$LIVE" "$target"
}

case "${1:-publish}" in
  publish) cmd_stage; cmd_install ;;
  stage)   cmd_stage ;;
  install) cmd_install ;;
  strip)   cmd_strip ;;
  restore) cmd_restore ;;
  *)       die "unknown command: $1 (publish | stage | install | strip | restore)" ;;
esac
