#!/bin/bash
# Mint Apparel Painter's Steam-page section banners from shift-change's
# banner template (same CSS, embedded RimWordFont, gradient), then export
# with the sibling's headless-Chrome + trim recipe.
set -eu

TEMPLATE="/Users/alexbeverage/Code/Apps/rimworld/shift-change/media/cards/_export/Banner_WhatItDoes.html"
EXPORT="/Users/alexbeverage/Code/Apps/rimworld/apparel-painter/media/cards/_export"
OUT="/Users/alexbeverage/Code/Apps/rimworld/apparel-painter/media/cards"
CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
PANEL="#1f242a"

mkdir -p "$EXPORT"

python3 - "$TEMPLATE" "$EXPORT" <<'EOF'
import sys
template_path, export_dir = sys.argv[1], sys.argv[2]
src = open(template_path).read()
banners = {
    "banner-what-it-does": "What it does",
    "banner-the-picker": "The picker",
    "banner-where-it-works": "Where it works",
    "banner-compatibility": "Compatibility",
    "banner-faq": "FAQ",
    "banner-source": "Source",
}
needle = '<span class="t">What it does</span>'
assert needle in src, "template body changed"
for name, title in banners.items():
    out = src.replace(needle, f'<span class="t">{title}</span>')
    open(f"{export_dir}/{name}.html", "w").write(out)
    print("wrote", name)
EOF

for page in "$EXPORT"/banner-*.html
do
  name=$(basename "$page" .html)
  "$CHROME" --headless --disable-gpu --hide-scrollbars \
            --force-device-scale-factor=2 \
            --default-background-color=00000000 \
            --window-size=640,4000 \
            --screenshot="$EXPORT/$name.raw.png" \
            "file://$page" 2>/dev/null
  magick "$EXPORT/$name.raw.png" -trim +repage \
         -background "$PANEL" -flatten "$OUT/$name.png"
  rm "$EXPORT/$name.raw.png"
  echo "exported $name.png"
done
