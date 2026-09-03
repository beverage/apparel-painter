#!/usr/bin/env python3
"""Shoot the style-picking beats through RimBridgeServer.

    devtools/bridge/shoot-style.py <player.log | port token>

The subject is the wardrobe stage's ARMOUR PAIR, added for this shot:
marine armour + marine helmet, and prestige marine armour + prestige
marine helmet, four cells north of the formal row so no blessed frame
moves. Power armour SUITS carry no styles at all — the suit is the
silhouette, the helmet is the subject.

Beats:
  0  idle          undyed marine kit, Paint tab open, style button unset
  1  morbid        one click
  2  totemic       one click
  3  animalist     one click
  4  prestige      the prestige helmet's samurai style, whose overrideLabel
                   RE-LABELS the tab row — the beat that shows a style is
                   not a repaint

Two things this driver does differently, both deliberate:

ONE LAYOUT CAPTURE, NOT ONE PER CLICK. shoot-picker-card.py documents its
own ~10 minute runtime as dominated by three get_ui_layout walks per
swatch. The style button's targetId is stable across cycles because the
row does not move and the button never changes size, so the layout is
walked once per stand and the id reused for every click.

wait_for_game_loaded, NOT start_debug_game_ready. run-scene.sh boots with
-quicktest, and calling start_debug_game_ready on an already-booted
instance stalls indefinitely when the window is not frontmost
(gamedata/rimbridgeserver.md, operational traps).

The right-click menu is NOT filmed here and cannot be: synthetic
button==1 events do not reach handlers inside window bodies through the
fork (same cache, the input-fork section). Left-click cycling is the
primary gesture anyway; click_ui_target reaches it because the control is
a patched Widgets.ButtonInvisible call site.
"""
import sys
import time

from gabp import connect

# The set's recurring accent — Jane's duster in the other gifs — so the
# painted armour reads as part of the same family rather than a new colour
# introduced for one shot.
TEAL = "217878"

# run-scene.sh's documented stage origin: map centre on the quicktest map.
ORIGIN = (77, 93)

# Mirrors DebugTools_WardrobeStage: PadWidth 96, StandCount 8, PadDepth 64.
ROW_X = (96 - (8 * 2 - 1)) // 2      # 40
ROW_Z = 64 // 2                      # 32
ARMOUR_X = ROW_X + 2                 # 42
ARMOUR_Z = ROW_Z + 4                 # 36

MARINE = (ORIGIN[0] + ARMOUR_X, ORIGIN[1] + ARMOUR_Z)
PRESTIGE = (ORIGIN[0] + ARMOUR_X + 2, ORIGIN[1] + ARMOUR_Z)
# DO NOT "improve" this by nudging z to compose the shot. The camera does
# not land on the requested cell and does not respond linearly to it:
# Camera+ is in the scene list and the jump animates. Measured on frame at
# rootSize 12, requesting MARINE z put the stands at screen y≈697; z+4 put
# them at y≈1283, a 146px-per-cell response where the geometry predicts 60.
# The second take was unusable — the stands ended up clipped behind the
# gizmo bar.
#
# So the camera is left at the one offset with measured positions, and
# FRAMING IS DONE IN THE CROP, where iterating costs an ffmpeg run instead
# of a shoot. Layout at this offset, original 2560x1440 coordinates:
#   stands      y≈697          tab        y 755..1240
#   formal row  y≈1257         gizmo bar  y≈1320
# make-style-gif.sh crops to y 600..1245, which holds the subject and the
# whole tab and excludes both intruders.
CAM = (MARINE[0] + 1, MARINE[1])

# Where "Park picker window here" puts the picker's top-left. East of the
# stands so the window sits right of frame centre, leaving the stands and
# the Paint tab both clear during the live-preview beat.
PARK = (MARINE[0] + 4, MARINE[1] + 3)

# Framing is the historically expensive part (dropper.gif took five takes),
# so candidates are shot in ONE pass and the crop chosen from the contact
# sheet instead of from another launch.
#
# 12 is the working value, verified on frame: it puts the tab bottom-left
# with both stands just right of it, which is the region every other gif in
# this set crops to. The camera does NOT centre on the requested cell — it
# lands roughly 9 cells east and 6 north of it (Camera+ is in the scene
# list and the dolly animates), so do not "fix" CAM to centre the subject
# without re-shooting: the off-centre landing is what puts the stands
# beside the tab rather than behind it. Zooming IN pushes them out of
# frame, which is what made the first take's 8 and 10 come back as bare
# floor.
ZOOMS = [11, 12, 13]
ZOOM = 12


class Shoot:
    def __init__(self, bridge):
        self.b = bridge
        self.beats = []

    def beat(self, tag, seconds):
        self.b.tool("rimworld/clear_hover_target", ok=False)
        r = self.b.tool("rimworld/take_screenshot",
                        {"fileName": f"style-{len(self.beats):02d}-{tag}",
                         "includeTargets": False, "suppressMessage": True})
        self.beats.append((r.get("path"), seconds))
        print(f"  beat {tag:12} {seconds}s  {r.get('path')}", flush=True)

    def flat(self):
        """Every addressable ui-element, flattened out of one layout walk."""
        out = []

        def walk(node, surface):
            if isinstance(node, list):
                for item in node:
                    walk(item, surface)
                return
            if not isinstance(node, dict):
                return
            if node.get("surfaceTargetId"):
                surface = str(node.get("surfaceTargetId"))
            tid = node.get("targetId") or ""
            if str(tid).startswith("ui-element:"):
                rect = node.get("screenRect") or node.get("rect") or {}
                out.append({
                    "id": tid, "surface": surface,
                    "label": str(node.get("label") or ""),
                    "value": str(node.get("valueText") or ""),
                    "kind": str(node.get("kind") or ""),
                    "source": str(node.get("source") or ""),
                    "act": bool(node.get("actionable")),
                    "x": rect.get("x", 0), "y": rect.get("y", 0),
                    "w": rect.get("width", 0), "h": rect.get("height", 0),
                })
            for value in node.values():
                walk(value, surface)

        walk(self.b.tool("rimworld/get_ui_layout", {}), "?")
        return out

    def style_button(self):
        """The row's style control.

        There is NO surface named for us to filter on: an ITab is not a
        Window, its body renders into a `Verse.ImmediateWindow` whose
        surface id is a hash (`window:-235086:Verse.ImmediateWindow`) that
        changes run to run. So the discriminator is geometry and kind:

          24x22 kind=button       the style button      <- this
          44x22 kind=button       the colour swatch
          24x24 kind=icon_button  the info-card button

        `kind` separates it from the info-card buttons, which share its
        width, and width separates it from the swatches, which share its
        kind and height. On an armour stand only the helmet row has a
        style button at all — the suit carries no styles — so exactly one
        target matches and it is unambiguous.
        """
        items = self.flat()
        hits = [e for e in items
                if e["act"] and e["kind"] == "button"
                and 22 <= e["w"] <= 26 and 20 <= e["h"] <= 24]
        if len(hits) != 1:
            print(f"  !! expected 1 style button, found {len(hits)} — actionable targets:",
                  flush=True)
            for e in items:
                if e["act"]:
                    print(f"     {e['w']:>5}x{e['h']:<5} kind={e['kind']:<14} "
                          f"label={e['label']!r:<16} {e['surface']}", flush=True)
            return hits[0] if hits else None
        return hits[0]

    def cycle(self, target, tag, seconds):
        self.b.tool("rimworld/click_ui_target", {"targetId": target["id"], "timeoutMs": 8000})
        self.b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})
        self.beat(tag, seconds)

    def labelled(self, text, surface_hint=None):
        """First actionable target whose label matches, optionally scoped."""
        for e in self.flat():
            if not e["act"] or text.lower() not in e["label"].strip().lower():
                continue
            if surface_hint and surface_hint not in e["surface"]:
                continue
            return e
        return None

    def picker_items(self):
        return [e for e in self.flat() if "StandColorPicker" in e["surface"]]


def main():
    b, _ = connect(sys.argv[1:])
    s = Shoot(b)

    # NOT start_debug_game_ready — see the module docstring.
    b.tool("rimbridge/wait_for_game_loaded", {"timeoutMs": 300000})

    for _ in range(6):
        b.tool("rimworld/press_cancel", ok=False)
        b.tool("rimworld/close_window", ok=False)
    b.tool("rimworld/clear_selection")

    for pawn in (b.tool("rimworld/list_colonists") or {}).get("colonists") or []:
        pos = pawn.get("position") or {}
        if pos.get("x") is not None:
            for dx in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    b.tool("rimworld/execute_debug_action",
                           {"path": "Actions\\T: Destroy",
                            "x": pos["x"] + dx, "z": pos["z"] + dz}, ok=False)
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})

    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Build wardrobe stage", "x": ORIGIN[0], "z": ORIGIN[1]})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})
    b.tool("rimworld/execute_debug_action", {"path": "Actions\\Pin lighting: noon"})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})

    for letter in (b.tool("rimworld/list_letters") or {}).get("letters") or []:
        if letter.get("id") is not None:
            b.tool("rimworld/dismiss_letter", {"id": letter["id"]}, ok=False)

    b.tool("rimworld/close_window")
    b.tool("rimworld/clear_selection")
    b.tool("rimworld/jump_camera_to_cell", {"x": CAM[0], "z": CAM[1]})

    # -- framing contact sheet, one pass ------------------------------
    b.tool("rimworld/click_cell", {"x": MARINE[0], "z": MARINE[1]})
    b.tool("rimworld/open_inspect_tab", {"inspectTabId": "ITab_ApparelPainter"})
    for z in ZOOMS:
        b.tool("rimworld/set_camera_zoom", {"rootSize": z})
        b.tool("rimworld/take_screenshot",
               {"fileName": f"style-frame-z{z}", "includeTargets": False,
                "suppressMessage": True})
        print(f"  frame candidate zoom {z}", flush=True)
    b.tool("rimworld/set_camera_zoom", {"rootSize": ZOOM})

    s.beat("idle", 1.2)

    # -- paint the whole kit through the picker's own surfaces --------
    #
    # Paint all... rather than one row, so suit AND helmet take the
    # colour and the later style cycles land on a PAINTED helmet. That
    # is the whole point of the shot: the two controls compose, and they
    # compose because no vanilla style sets StyleDef.color, so
    # Apparel.DrawColor falls through to CompColorable.
    paint_all = s.labelled("paint all")
    if paint_all is None:
        raise SystemExit("no 'Paint all...' button in the tab layout")
    b.tool("rimworld/click_ui_target", {"targetId": paint_all["id"], "timeoutMs": 8000})
    time.sleep(0.3)

    # Park it clear of the stands: the next beat is live preview ON the
    # stand, which is worthless if the window is sitting on top of it.
    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\Park picker window here", "x": PARK[0], "z": PARK[1]})
    s.beat("picker-open", 1.2)

    # The picker has FOUR text fields — R, G, B and ours. The proven
    # discriminator (shoot-picker-card.py) is the value, not the kind:
    # only the hex field reads back six hex digits.
    hexfield = next((e for e in s.picker_items()
                     if e["kind"] == "text_field"
                     and len(e["value"].strip()) == 6
                     and all(c in "0123456789ABCDEFabcdef" for c in e["value"].strip())), None)
    if hexfield is None:
        raise SystemExit("no hex field in the picker layout")
    b.tool("rimworld/set_text_field",
           {"targetId": hexfield["id"], "text": TEAL, "mode": "typed",
            "charsPerSecond": 10, "jitterPercent": 30, "clearFirst": True,
            "controlName": "ApparelPainter_DirectInput"}, ok=False)
    setbtn = next((e for e in s.picker_items()
                   if e["act"] and e["label"].strip().lower() == "set"), None)
    if setbtn is None:
        raise SystemExit("no Set button in the picker layout")
    b.tool("rimworld/click_ui_target", {"targetId": setbtn["id"], "timeoutMs": 8000})
    time.sleep(0.3)
    s.beat("preview", 1.6)

    accept = next((e for e in s.picker_items()
                   if e["act"] and e["label"].strip().lower() in ("accept", "ok")), None)
    if accept is None:
        raise SystemExit("no Accept button in the picker layout")
    b.tool("rimworld/click_ui_target", {"targetId": accept["id"], "timeoutMs": 8000})
    time.sleep(0.3)
    s.beat("painted", 1.6)

    # -- now style the PAINTED helmet three ways ----------------------
    #
    # Re-found after the picker closes: the tab body lives in an
    # ImmediateWindow whose element ids renumber when the window stack
    # changes, so the id captured before the picker opened is stale.
    target = s.style_button()
    if target is None:
        raise SystemExit("style button not addressable after painting")
    print(f"  style button: {target['id']}", flush=True)
    for tag in ("morbid", "totemic", "animalist"):
        s.cycle(target, tag, 1.5)

    # -- tail: the one style in the game that RENAMES its item ---------
    #
    # PrestigeMarineHelmet_Samurai is the only ThingStyleDef in vanilla or
    # any surveyed mod that sets overrideLabel, so applying it re-labels
    # the tab row "Marine helmet" → "Samurai helmet" (GenLabel.cs:177).
    # Both beats are needed: the rename is only legible against the row
    # it replaces. Left undyed on purpose — the tail is about naming, and
    # painting it too would blur it into the colour story above.
    b.tool("rimworld/click_cell", {"x": PRESTIGE[0], "z": PRESTIGE[1]})
    b.tool("rimworld/open_inspect_tab", {"inspectTabId": "ITab_ApparelPainter"})
    prestige = s.style_button()
    if prestige is None:
        raise SystemExit("style button not addressable on the prestige stand")
    s.beat("prestige-before", 1.8)
    s.cycle(prestige, "samurai", 2.6)

    print("\nbeats:", flush=True)
    for path, dur in s.beats:
        print(f"  {dur:>4}  {path}", flush=True)


if __name__ == "__main__":
    main()
