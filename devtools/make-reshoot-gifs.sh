#!/usr/bin/env bash
#
# Re-cut where-it-works.gif and integrations.gif (+ their cards) from a
# fresh take, using media/README.md's committed recipes VERBATIM — same
# crops, same durations, same encoder flags.
#
#   devtools/make-reshoot-gifs.sh [take-dir]
#
# Exists because the style control was added to every tab row, so every
# gif whose scene holds a styled garment had to be reshot. The scenes and
# framing are unchanged; only the mod under them moved. Keeping the crops
# byte-identical to the recipes is the point — these sit in a gallery
# beside stills that were NOT reshot.
#
# The cards are crops of one beat each, so they are regenerated here too:
# leaving them would put a pre-style card next to a post-style gif.
set -eu

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TAKE="$REPO/${1:-dist/scenedata/Screenshots}"
WORK="$REPO/dist/reshoot-assembly"

rm -rf "$WORK"
mkdir -p "$WORK"

# $1 = ffconcat path, then (name duration) pairs; last file is repeated
# because the concat demuxer drops the final entry's duration.
build_concat() {
  local out="$1"; shift
  local last=""
  printf 'ffconcat version 1.0\n' > "$out"
  while [ "$#" -gt 0 ]
  do
    printf "file '%s/%s.png'\nduration %s\n" "$TAKE" "$1" "$2" >> "$out"
    last="$1"
    shift 2
  done
  printf "file '%s/%s.png'\n" "$TAKE" "$last" >> "$out"
}

# $1 = ffconcat, $2 = full filter chain (crop, and scale where the recipe
# has one), $3 = out.gif
encode() {
  ffmpeg -y -loglevel error -f concat -safe 0 -i "$1" \
    -vf "$2,palettegen=max_colors=256:stats_mode=diff" "$WORK/palette.png"
  ffmpeg -y -loglevel error -f concat -safe 0 -i "$1" -i "$WORK/palette.png" \
    -lavfi "$2[x];[x][1:v]paletteuse=dither=bayer:diff_mode=rectangle" \
    -fps_mode vfr -loop 0 "$WORK/raw.gif"
  gifsicle -O3 --lossy=60 "$WORK/raw.gif" -o "$3"
  magick identify -format "%n frames  %wx%h  %b  $(basename "$3")\n" "$3" | head -1
}

# -- where-it-works: idle 1.0, then 1.8 per container ------------------
build_concat "$WORK/wiw.ffconcat" \
  wiw-00-idle 1.0 wiw-01-stand 1.8 wiw-02-shelf 1.8 wiw-03-small 1.8
encode "$WORK/wiw.ffconcat" "crop=1080:1105:0:740" "$REPO/media/where-it-works.gif"
magick "$TAKE/wiw-02-shelf.png" -crop 1080x1105+0+740 +repage \
  "$REPO/media/cards/card-where-it-works.png"

# -- integrations: idle 1.0, then 2.0 per container --------------------
build_concat "$WORK/int.ffconcat" \
  int-00-idle 1.0 int-01-hanger 2.0 int-02-rack 2.0 int-03-armor 2.0
encode "$WORK/int.ffconcat" "crop=1300:1234:0:620" "$REPO/media/integrations.gif"
magick "$TAKE/int-01-hanger.png" -crop 1300x1234+0+620 +repage \
  "$REPO/media/cards/card-integrations.png"

# -- dropper: needs the cursor composited in first ---------------------
#
# Pass the shoot driver's stdout as $2 and the pointer gets pasted onto
# the manifest-flagged beats before encoding — the v5 "make the clicks
# visible, subtly" pass. composite-clicks.py edits frames IN PLACE, which
# is safe here only because the take dir is dist/ and disposable. Skipped
# entirely when no manifest is given, so the other two can be re-cut on
# their own.
DROPPER_MANIFEST=${2:-}
if [ -n "$DROPPER_MANIFEST" ]
then
  "$REPO/devtools/bridge/composite-clicks.py" "$TAKE" "$DROPPER_MANIFEST" \
    "$REPO/devtools/bridge/stage-cursor-32.png"
  build_concat "$WORK/drop.ffconcat" \
    drop-00-idle 0.8 drop-01-selected 0.6 drop-02-tab-open 1.0 \
    drop-03-picker-open 1.2 drop-04-menu-on-jane 1.4 drop-05-sip-jane 1.6 \
    drop-06-sip-rug 1.6 drop-07-accepted 0.8 drop-08-finale 2.0
  encode "$WORK/drop.ffconcat" \
    "crop=1840:1325:0:520,scale=1380:994:flags=lanczos" \
    "$REPO/media/dropper.gif"
fi

magick identify -format "%wx%h  %b  card-where-it-works.png\n" \
  "$REPO/media/cards/card-where-it-works.png"
magick identify -format "%wx%h  %b  card-integrations.png\n" \
  "$REPO/media/cards/card-integrations.png"
