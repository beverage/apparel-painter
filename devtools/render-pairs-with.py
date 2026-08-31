#!/usr/bin/env python3
"""Emit export-grade HTML for Card_PairsWith + its banner into Apparel
Painter's _export dir.

The card is DRAFT in shift-change's generator, and --export deliberately
walks ORDER only, so this driver imports the generator as a module and
reproduces the --export branch (SHOW_LABELS off, EXPORT_CSS, the parts-path
rewrite) for exactly one card. Nothing under shift-change/ is written.
The banner reuses Apparel Painter's own banner-export title-swap.
"""
import base64
import importlib.util
import os

SC_MEDIA = "/Users/alexbeverage/Code/Apps/rimworld/shift-change/media"
AP_EXPORT = "/Users/alexbeverage/Code/Apps/rimworld/apparel-painter/media/cards/_export"
TEMPLATE = os.path.join(SC_MEDIA, "cards", "_export", "banner-what-it-does.html")

spec = importlib.util.spec_from_file_location(
    "card_mockup", os.path.join(SC_MEDIA, "card-mockup.py"))
cm = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cm)

cm.SHOW_LABELS = False
cards = {c["id"]: c for c in cm.CARDS}
card = cards["Card_PairsWith"]

b64 = base64.b64encode(open(cm.FONT, "rb").read()).decode("ascii")
face = ("@font-face{font-family:RimWord;src:url(data:font/ttf;base64,"
        + b64 + ") format('truetype');}")

style = ("<style>" + face + (cm.CSS % {"W": cm.CARD_W})
         + (cm.EXPORT_CSS % {"W": cm.CARD_W}) + "</style>")
node = cm.render_card(card)
node = node.replace('src="cards/', 'src="../')  # --export's rewrite; no-op on a text card

os.makedirs(AP_EXPORT, exist_ok=True)
card_out = os.path.join(AP_EXPORT, "card-pairs-with.html")
open(card_out, "w").write("<!doctype html><meta charset=utf-8>" + style + node)
print("wrote", card_out)

src = open(TEMPLATE).read()
needle = '<span class="t">What it does</span>'
assert needle in src, "banner template body changed"
banner_out = os.path.join(AP_EXPORT, "banner-pairs-with.html")
open(banner_out, "w").write(src.replace(needle, '<span class="t">Pairs with</span>'))
print("wrote", banner_out)
