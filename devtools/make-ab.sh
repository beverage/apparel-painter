#!/bin/bash
#
# Build an A/B crossfade GIF from a before/after pair.
#
#   devtools/make-ab.sh <before.png> <after.png> <out.gif>
#
# Recovered from the 2026-08-27 bridge session (full salvage:
# ~/Movies/apparel-painter-bridge-2026-08-27) — this is the cut behind
# wardrobe-row.gif and wardrobe-pair.gif, adopted verbatim.
#
# Holds on each state, then blends between them. This only works because the
# two captures are the SAME cell rect at the SAME pinned zoom, so every pixel
# corresponds — a morph between misaligned frames reads as a smear, not a
# recolour. Manual shooting satisfies that for free: two screenshots with the
# camera untouched and only the Paint debug action between them.
set -eu

B=${1:?before}
A=${2:?after}
OUT=${3:?out.gif}
HOLD=${HOLD:-16}      # frames held on each state
BLEND=${BLEND:-8}     # interpolated frames per transition
DELAY=${DELAY:-6}     # centiseconds per frame

magick \
  \( "$B" -duplicate "$HOLD" \) \
  \( "$B" "$A" -morph "$BLEND" -delete 0 \) \
  \( "$A" -duplicate "$HOLD" \) \
  \( "$A" "$B" -morph "$BLEND" -delete 0 \) \
  -set delay "$DELAY" -loop 0 -layers Optimize "$OUT"

magick identify -format "%n frames  %wx%h  %b\n" "$OUT" | head -1
