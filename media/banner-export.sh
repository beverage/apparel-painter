#!/bin/bash
# Mint Apparel Painter's Steam-page section banners from shift-change's
# banner template (same CSS, embedded RimWordFont, gradient), then export
# with the sibling's headless-Chrome + trim recipe.
set -eu

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# The constellation checks its repos out side by side; SHIFT_CHANGE_REPO
# overrides that assumption.
SHIFT_CHANGE="${SHIFT_CHANGE_REPO:-$REPO/../shift-change}"
TEMPLATE="$SHIFT_CHANGE/media/cards/_export/banner-what-it-does.html"
EXPORT="$REPO/media/cards/_export"
OUT="$REPO/media/cards"
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
    "banner-pairs-with": "Pairs with",
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
