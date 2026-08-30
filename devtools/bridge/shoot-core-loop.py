#!/usr/bin/env python3
"""Shoot the core-loop demo (v3: paint-all-then-rescue) through the
RimBridgeServer FORK.

    devtools/bridge/shoot-core-loop.py <player.log | port token>

Sixteen stepped beats at noon on the core-loop scene
(DebugTools_CoreLoopScene: BOTH stands undyed — every colour lands
on camera, and every garment is vanilla+DLC; the VAE suit jacket was
cut 2026-08-29). Act one suits the men's stand piece by piece off
the saved band: shirt dress white, vest black tie, top hat black tie
— the vest carries the jacket read. Act two is the women's sequence,
locked as shot: Paint all floods her outfit scarlet — shirt
included, which is exactly what bulk painting does — then the
per-item beat rescues her shirt back to dress white. End state:
black suit, red dress, two white shirts. Paint-all, per-item
precision, and the saved swatches in one arc.

Composition: dropper screen zone (stands upper-left, tab lower-left)
with the picker PARKED via the SCENES action "Park picker window
here" each time it opens — the deterministic window-position lever
the 2026-08-29 fork session added — so the frame packs ~4:3 with no
dead centre.

SETUP REBUILDS THE SAVED BAND FIRST: run-scene.sh wipes scenedata on
every launch, and the band lives in that instance's ModSettings — so
the driver re-saves all eight swatches through the picker-card
routine (typed hex → Set → plus, each verified) before rolling a
single beat, then Cancels that setup picker so nothing staged leaks.

RUNTIME IS ~12 MINUTES and silent until exit (stdout buffers): the
band rebuild's layout captures dominate. Not a hang — check
dist/scenedata/Screenshots for loop-* beats before assuming one.

Superseded compositions, for the record: v1 wardrobe wall at (77,93)
stands +40/+42,+32 (near-full-frame crop halved the UI); v2 minimal
scene, same beats as v1 with palette-hunted scarlet/sapphire.
"""
import json
import sys
import time

from gabp import connect

ORIGIN = (135, 130)
STAND1 = (ORIGIN[0] + 3, ORIGIN[1] + 5)   # men's rig — pre-suited context
STAND2 = (STAND1[0] + 2, STAND1[1])       # women's rig — the demo canvas
CAM = (STAND1[0] + 5, STAND1[1] - 1)
PARK = (ORIGIN[0] + 6, ORIGIN[1] + 5)     # picker top-left lands at this cell
ZOOM = 12

# The picker-card palette: throne-room + the set's teal. Band order is
# save order, so WHITE is cell 0 and SCARLET is cell 6.
SWATCHES = [
    ("F2F0EB", [242, 240, 235]),   # dress white
    ("5C5C69", [92, 92, 105]),     # waistcoat grey
    ("383845", [56, 56, 69]),      # black tie
    ("D9AD3D", [217, 173, 61]),    # gold
    ("12704A", [18, 112, 74]),     # emerald
    ("264AA1", [38, 74, 161]),     # sapphire
    ("B81726", [184, 23, 38]),     # scarlet
    ("217878", [33, 120, 120]),    # teal
]
WHITE_IDX, WHITE_RGB = 0, [242, 240, 235]
BLACK_IDX, BLACK_RGB = 2, [56, 56, 69]
RED_IDX, RED_RGB = 6, [184, 23, 38]


class Driver:
    def __init__(self, bridge):
        self.b = bridge
        self.beats = []
        # Click manifest for the subtle-cursor post pass (principal,
        # 2026-08-29): each interaction marks the control it hit, the
        # NEXT beat adopts the pending mark, and assembly composites a
        # pointer sprite there. Rects are UI points — double them for
        # the retina masters.
        self.manifest = []
        self.pending = None

    def flat(self):
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
                    "id": tid,
                    "surface": surface,
                    "label": str(node.get("label") or ""),
                    "value": str(node.get("valueText") or ""),
                    "kind": str(node.get("kind") or ""),
                    "source": str(node.get("source") or ""),
                    "act": bool(node.get("actionable")),
                    "x": rect.get("x", 0), "y": rect.get("y", 0),
                    "w": rect.get("width", 0), "h": rect.get("height", 0),
                })
            for value in (node.values() if isinstance(node, dict) else []):
                walk(value, surface)

        walk(self.b.tool("rimworld/get_ui_layout", {}), "?")
        return out

    def picker_items(self, items=None):
        items = items if items is not None else self.flat()
        return [e for e in items if "StandColorPicker" in e["surface"]]

    def close_debug_log(self):
        self.b.tool("rimworld/close_window", {"windowType": "EditWindow_Log"}, ok=False)

    def click(self, element):
        self.b.tool("rimworld/click_ui_target",
                    {"targetId": element["id"], "timeoutMs": 8000})

    def click_label(self, label):
        want = label.lower()
        hit = next((e for e in self.flat()
                    if e["act"] and want in e["label"].lower()), None)
        if hit is None:
            raise SystemExit(f"no actionable element labelled {label!r}")
        self.click(hit)
        return hit

    def mark(self, element):
        self.pending = {"x": element["x"], "y": element["y"],
                        "w": element["w"], "h": element["h"]}

    def mark_tab_button(self):
        """The Paint tab strip button, exact-label so 'Paint all' stays
        out. Missing is fine — a frame just goes unmarked."""
        hit = next((e for e in self.flat()
                    if e["act"] and e["label"].strip() == "Paint"), None)
        if hit is not None:
            self.mark(hit)

    def row_swatch(self, garment):
        """GARMENT's row swatch inside the Paint tab's ImmediateWindow
        surface: the row label anchors, the swatch is the
        button_invisible on the same row (the ap-verify pattern)."""
        items = self.flat()
        row_label = next((e for e in items
                          if garment.lower() in e["label"].lower()
                          and "ImmediateWindow" in e["surface"]), None)
        if row_label is None:
            raise SystemExit(f"no {garment!r} row in the Paint tab layout")
        row = [e for e in items
               if e["act"] and e["surface"] == row_label["surface"]
               and abs(e["y"] - row_label["y"]) < 14
               and e["source"] == "widgets.button_invisible"]
        if not row:
            raise SystemExit(f"{garment!r} row has no swatch button")
        self.click(row[-1])
        self.mark(row[-1])

    def hex_field(self, picker=None):
        for e in (picker if picker is not None else self.picker_items()):
            v = e["value"].strip().upper()
            if e["kind"] == "text_field" and len(v) == 6 \
                    and all(c in "0123456789ABCDEF" for c in v):
                return e
        return None

    def rgb_readback(self, picker=None):
        vals = [int(e["value"]) for e in (picker if picker is not None else self.picker_items())
                if e["kind"] == "text_field" and e["value"].strip().isdigit()]
        return vals[:3] if len(vals) >= 3 else None

    def plus_cell(self, picker=None):
        picker = picker if picker is not None else self.picker_items()
        cands = [e for e in picker
                 if e["act"] and "button_image" in e["source"]
                 and 18 <= e["w"] <= 22 and 18 <= e["h"] <= 22]
        cands.sort(key=lambda e: (-e["y"], e["x"]))
        return cands[0] if cands else None

    def band_cells(self, picker=None):
        """Saved-band cells only: square button_invisible swatches in the
        band row just ABOVE the hex field — the y-window keeps the
        vanilla palette grid (same widget, higher up) out."""
        picker = picker if picker is not None else self.picker_items()
        anchor = self.hex_field(picker)
        if anchor is None:
            raise SystemExit("hex field not found while resolving the band")
        cells = [e for e in picker
                 if e["act"] and "button_invisible" in e["source"]
                 and 22 <= e["w"] <= 32 and 22 <= e["h"] <= 32
                 and abs(e["w"] - e["h"]) <= 4
                 and anchor["y"] - 50 <= e["y"] <= anchor["y"] - 8]
        cells.sort(key=lambda e: e["x"])
        return cells

    def adopt_band(self, index, want_rgb, name):
        cells = self.band_cells()
        if len(cells) <= index:
            raise SystemExit(f"band has {len(cells)} cells, wanted index {index}")
        self.click(cells[index])
        self.mark(cells[index])
        time.sleep(0.25)
        rgb = self.rgb_readback()
        if rgb != want_rgb:
            raise SystemExit(f"band {name} did not adopt: rgb={rgb}, wanted {want_rgb}")
        print(f"  band adopt {name}: rgb={rgb}")

    def park_picker(self):
        self.b.tool("rimworld/execute_debug_action",
                    {"path": "Actions\\T: Park picker window here",
                     "x": PARK[0], "z": PARK[1]})
        time.sleep(0.2)

    def rebuild_band(self):
        for hexval, want in SWATCHES:
            picker = self.picker_items()
            hexfield = self.hex_field(picker)
            if hexfield is None:
                raise SystemExit(f"hex field not found before typing {hexval}")
            r = self.b.tool("rimworld/set_text_field",
                            {"targetId": hexfield["id"], "text": hexval,
                             "mode": "typed", "charsPerSecond": 12,
                             "jitterPercent": 20, "clearFirst": True,
                             "controlName": "ApparelPainter_DirectInput"}, ok=False)
            if not (isinstance(r, dict) and r.get("success")):
                raise SystemExit(f"set_text_field failed on {hexval}: {json.dumps(r)[:300]}")
            setbtn = next((e for e in self.picker_items()
                           if e["act"] and e["label"].strip().lower() == "set"), None)
            if setbtn is None:
                raise SystemExit(f"no Set button after typing {hexval}")
            self.click(setbtn)
            time.sleep(0.25)
            rgb = self.rgb_readback()
            if rgb != want:
                raise SystemExit(f"{hexval} did not apply: rgb={rgb}, wanted {want}")
            plus = self.plus_cell()
            if plus is None:
                raise SystemExit(f"no save (+) cell before saving {hexval}")
            self.click(plus)
            time.sleep(0.25)
            print(f"  band save {hexval}")

    def beat(self, tag, seconds):
        self.b.tool("rimworld/clear_hover_target", ok=False)
        r = self.b.tool("rimworld/take_screenshot",
                        {"fileName": f"loop-{len(self.beats):02d}-{tag}",
                         "includeTargets": False, "suppressMessage": True})
        self.beats.append((r.get("path"), seconds))
        if self.pending is not None:
            self.manifest.append({"beat": len(self.beats) - 1, "tag": tag,
                                  **self.pending})
            self.pending = None
        print(f"  beat {tag:16} {seconds}s  {r.get('path')}")

    def paint_item(self, row_label, band_idx, want_rgb, tag, dwell, open_beat=None):
        """One compressed per-item cycle: row swatch → park → (optional
        picker-open beat) → band adopt → applied beat → Accept. Later
        cycles skip the open beat — the viewer learned the flow on the
        first one, and the applied frame carries the story."""
        self.row_swatch(row_label)
        time.sleep(0.3)
        self.park_picker()
        if open_beat is not None:
            self.beat(open_beat[0], open_beat[1])
        self.adopt_band(band_idx, want_rgb, tag)
        self.b.tool("rimworld/step_game_ticks", {"ticks": 1, "pauseFirst": True})
        self.beat(tag, dwell)
        self.click_label("Accept")
        self.b.tool("rimworld/step_game_ticks", {"ticks": 1, "pauseFirst": True})


def main():
    b, _ = connect(sys.argv[1:])
    d = Driver(b)

    b.tool("rimworld/start_debug_game_ready",
           {"readiness": "visual", "pauseIfNeeded": True, "timeoutMs": 300000})

    # Sweep leftover windows BEFORE rebuilding: a stale picker must cancel
    # while its snapshot targets still exist.
    for _ in range(6):
        b.tool("rimworld/press_cancel", ok=False)
        b.tool("rimworld/close_window", ok=False)
    b.tool("rimworld/clear_selection")

    # Destroy the quicktest colonists AND their droppings: a destroyed
    # pawn drops everything it wore, and model pawns from earlier takes
    # keep living between shoots.
    for pawn in (b.tool("rimworld/list_colonists") or {}).get("colonists") or []:
        pos = pawn.get("position") or {}
        if pos.get("x") is not None:
            for dx in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    b.tool("rimworld/execute_debug_action",
                           {"path": "Actions\\T: Destroy",
                            "x": pos["x"] + dx, "z": pos["z"] + dz}, ok=False)
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})

    # Wardrobe stage purely for its 96x64 ambient steel field — the
    # scene's own 10x8 pad is smaller than the zoom-12 frame.
    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Build wardrobe stage", "x": 77, "z": 93})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})

    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Build core loop scene",
            "x": ORIGIN[0], "z": ORIGIN[1]})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})
    b.tool("rimworld/execute_debug_action", {"path": "Actions\\Pin lighting: noon"})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})

    for letter in (b.tool("rimworld/list_letters") or {}).get("letters") or []:
        if letter.get("id") is not None:
            b.tool("rimworld/dismiss_letter", {"id": letter["id"]}, ok=False)

    b.tool("rimworld/close_window")
    b.tool("rimworld/clear_selection")
    b.tool("rimworld/jump_camera_to_cell", {"x": CAM[0], "z": CAM[1]})
    b.tool("rimworld/set_camera_zoom", {"rootSize": ZOOM})
    d.close_debug_log()

    # ---- setup: rebuild the saved band (scenedata was wiped at launch),
    # then cancel the setup picker so nothing staged leaks into beat 0.
    b.tool("rimworld/click_cell", {"x": STAND2[0], "z": STAND2[1]})
    b.tool("rimworld/open_inspect_tab", {"inspectTabId": "ITab_ApparelPainter"})
    d.close_debug_log()
    d.row_swatch("formal shirt")
    time.sleep(0.3)
    d.rebuild_band()
    d.click_label("Cancel")
    time.sleep(0.2)
    b.tool("rimworld/clear_selection")
    d.close_debug_log()
    d.pending = None  # setup's row-swatch mark must not leak into beat 0

    # ---- the sixteen beats ----------------------------------------------
    d.beat("idle", 1.0)

    # Act one: his suit, piece by piece off the saved band.
    b.tool("rimworld/click_cell", {"x": STAND1[0], "z": STAND1[1]})
    d.beat("selected-suit", 0.6)

    b.tool("rimworld/open_inspect_tab", {"inspectTabId": "ITab_ApparelPainter"})
    d.close_debug_log()
    d.mark_tab_button()
    d.beat("tab-open-suit", 1.0)

    d.paint_item("formal shirt", WHITE_IDX, WHITE_RGB, "shirt-white-his", 1.2,
                 open_beat=("shirt-picker-his", 1.0))
    d.paint_item("formal vest", BLACK_IDX, BLACK_RGB, "vest-black", 1.2)
    d.paint_item("top hat", BLACK_IDX, BLACK_RGB, "hat-black", 1.4)

    # Act two: her sequence, locked exactly as shot in the prior take.
    b.tool("rimworld/click_cell", {"x": STAND2[0], "z": STAND2[1]})
    d.beat("selected", 0.6)

    b.tool("rimworld/open_inspect_tab", {"inspectTabId": "ITab_ApparelPainter"})
    d.close_debug_log()
    d.mark_tab_button()
    d.beat("tab-open", 1.2)

    d.mark(d.click_label("Paint all"))
    time.sleep(0.3)
    d.park_picker()
    d.beat("paintall-picker", 1.2)

    d.adopt_band(RED_IDX, RED_RGB, "scarlet")
    b.tool("rimworld/step_game_ticks", {"ticks": 1, "pauseFirst": True})
    d.beat("outfit-red", 1.6)

    d.click_label("Accept")
    b.tool("rimworld/step_game_ticks", {"ticks": 1, "pauseFirst": True})
    d.beat("accepted", 0.8)

    d.row_swatch("formal shirt")
    time.sleep(0.3)
    d.park_picker()
    d.beat("shirt-picker", 1.0)

    d.adopt_band(WHITE_IDX, WHITE_RGB, "white")
    b.tool("rimworld/step_game_ticks", {"ticks": 1, "pauseFirst": True})
    d.beat("shirt-white", 1.8)

    d.click_label("Accept")
    b.tool("rimworld/step_game_ticks", {"ticks": 1, "pauseFirst": True})
    d.beat("accepted2", 0.8)

    b.tool("rimworld/clear_selection")
    d.beat("finale", 2.2)

    print("\nbeats:")
    print(json.dumps(d.beats, indent=1))
    print("\nmanifest:")
    print(json.dumps(d.manifest, indent=1))


if __name__ == "__main__":
    main()
