#!/usr/bin/env bash
#
# Assemble the style gif from shoot-style.py's beats.
#
#   devtools/make-style-gif.sh [take-dir] [out.gif]
#
# Defaults to dist/scenedata/Screenshots → media/styles.gif.
#
# Same cut as core-loop.gif and dropper.gif: dwell lives in ffconcat
# durations so each beat is ONE frame, then ffmpeg's palettegen
# stats_mode=diff + bayer dither (ImageMagick's quantizer speckles these
# flat steel gradients), then gifsicle for the squeeze.
#
# CROP is the only thing worth tuning here, and it is chosen to hold the
# Paint tab (bottom-left, where the style button is being clicked) AND both
# armour stands in one frame — the point of the shot is that the click and
# the helmet change are visibly the same event.
set -eu

TAKE=${1:-dist/scenedata/Screenshots}
OUT=${2:-media/styles.gif}
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="$REPO/dist/style-assembly"

# Measured against the take, not guessed (original frames are 2560x1440):
# the stands sit at y≈697 and the tab spans y 755..1240, while the
# formal-wear row bleeds in at y≈1257 and the gizmo bar at y≈1320. Ending
# the crop at 1245 keeps subject and tab whole and excludes both.
# Top edge at 560, not 600: the prestige stand's plume is the tallest thing
# in the frame and 600 clipped it on the idle beat. Bottom stays at 1245,
# which is the last row that still holds the Contents|Paint|Storage strip —
# the "Paint" label is what identifies the feature, so it does not get cut
# to chase the one formal-row hat that peeks in at y≈1213.
CROP=${CROP:-1560:685:0:560}
SCALE=${SCALE:-1170:514}

rm -rf "$WORK"
mkdir -p "$WORK"

beat() {
  printf "file '%s/%s.png'\nduration %s\n" "$REPO/$TAKE" "$1" "$2" >> "$WORK/beats.ffconcat"
}

# The picker beats (01-picker-open, 02-preview) are SHOT but not cut in.
# Parked anywhere this crop can hold, the window covers the stands — and a
# live-preview beat whose subject is hidden proves nothing. Holding tab,
# subject and a ~570px picker side by side needs a ~3:1 letterbox that
# would not sit next to the other gifs in this set. The colour story is
# carried by the tab instead: swatches turn teal and Reset buttons appear
# between idle and painted. core-loop.gif is where the picker gets its
# own showcase.
printf 'ffconcat version 1.0\n' > "$WORK/beats.ffconcat"
beat style-00-idle 1.4
beat style-03-painted 1.8
beat style-04-morbid 1.5
beat style-05-totemic 1.5
beat style-06-animalist 1.8
# The tail: the selection hops to the prestige stand and the row RENAMES
# itself. Held longer than the cycle beats because the change is text, not
# silhouette, and needs reading time.
beat style-07-prestige-before 1.8
beat style-08-samurai 2.6
# The concat demuxer drops the last entry's duration unless the file is
# repeated, so the closing beat would flash past without this.
printf "file '%s/%s.png'\n" "$REPO/$TAKE" style-08-samurai >> "$WORK/beats.ffconcat"

ffmpeg -y -loglevel error -f concat -safe 0 -i "$WORK/beats.ffconcat" \
  -vf "crop=$CROP,scale=$SCALE:flags=lanczos,palettegen=max_colors=256:stats_mode=diff" \
  "$WORK/palette.png"

ffmpeg -y -loglevel error -f concat -safe 0 -i "$WORK/beats.ffconcat" -i "$WORK/palette.png" \
  -lavfi "crop=$CROP,scale=$SCALE:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:diff_mode=rectangle" \
  -fps_mode vfr -loop 0 "$WORK/raw.gif"

gifsicle -O3 --lossy=60 "$WORK/raw.gif" -o "$REPO/$OUT"

magick identify -format "%n frames  %wx%h  %b\n" "$REPO/$OUT" | head -1
