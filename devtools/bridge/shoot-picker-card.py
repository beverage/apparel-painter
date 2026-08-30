#!/usr/bin/env python3
"""Shoot the picker-card master through the RimBridgeServer FORK.

    devtools/bridge/shoot-picker-card.py <player.log | port token>

The picker card (shot-list row 3) wants the colour picker at native res
with a POPULATED saved-swatch band — an empty band undersells the
feature. This driver builds the core-loop scene, opens the picker on
stand 1's formal shirt, then saves eight swatches through the picker's
own surfaces: the throne-room palette plus the set's teal, each typed
into the hex field at human cadence (fork tool `set_text_field`,
controlName ApparelPainter_DirectInput), applied via the Set button
(window-scope Enter is unreliable for synthetic Return — handoff doc),
and saved via the ~20px plus cell. Every save is verified by diffing
the band's button_invisible cells, every apply by RGB readback.

The final master is a full-screen still: picker centre-right with the
band populated and the current colour (teal) live-previewing on the
shirt, Paint tab lower-left with its swatch rows carrying the tab
droppers. Card crops (window-border native, or picker+tab) are cut in
assembly from this one master.

Fork required (mrbeverage.rimbridgeserverfork — run-scene.sh --bridge
loads it since 2026-08-29); patterns lifted from the verified
ap-verify/test-input-tools.py.

RUNTIME IS ~10 MINUTES and silent until exit (stdout buffers): every
swatch cycle takes three full get_ui_layout captures and those
dominate. Not a hang — check dist/scenedata/Screenshots for beats
before assuming one.
"""
import json
import sys
import time

from gabp import connect

ORIGIN = (135, 130)
STAND1 = (ORIGIN[0] + 3, ORIGIN[1] + 5)
CAM = (STAND1[0] + 5, STAND1[1] - 1)
ZOOM = 12

# Throne-room palette + the set's teal, ordered so the LAST applied
# colour (teal, the brand echo) stays live on the shirt in the master.
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


class Driver:
    def __init__(self, bridge):
        self.b = bridge
        self.shot = 0

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

    def invisible_cells(self, picker=None):
        picker = picker if picker is not None else self.picker_items()
        return {(round(e["x"]), round(e["y"]), round(e["w"]), round(e["h"])): e
                for e in picker
                if e["act"] and "button_invisible" in e["source"]
                and 22 <= e["w"] <= 32 and 22 <= e["h"] <= 32
                and abs(e["w"] - e["h"]) <= 4}

    def beat(self, tag):
        self.b.tool("rimworld/clear_hover_target", ok=False)
        r = self.b.tool("rimworld/take_screenshot",
                        {"fileName": f"pick-{self.shot:02d}-{tag}",
                         "includeTargets": False, "suppressMessage": True})
        self.shot += 1
        print(f"  shot {tag:16} {r.get('path')}")


def main():
    b, _ = connect(sys.argv[1:])
    d = Driver(b)

    b.tool("rimworld/start_debug_game_ready",
           {"readiness": "visual", "pauseIfNeeded": True, "timeoutMs": 300000})

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

    b.tool("rimworld/click_cell", {"x": STAND1[0], "z": STAND1[1]})
    b.tool("rimworld/open_inspect_tab", {"inspectTabId": "ITab_ApparelPainter"})
    d.close_debug_log()

    # The shirt row's swatch: label + same-row button_invisible inside the
    # tab's ImmediateWindow surface (the ap-verify duster-row pattern).
    items = d.flat()
    row_label = next((e for e in items
                      if "formal shirt (" in e["label"].lower()
                      and "ImmediateWindow" in e["surface"]), None)
    if row_label is None:
        raise SystemExit("no formal-shirt row in the Paint tab layout")
    row = [e for e in items
           if e["act"] and e["surface"] == row_label["surface"]
           and abs(e["y"] - row_label["y"]) < 14
           and e["source"] == "widgets.button_invisible"]
    if not row:
        raise SystemExit("formal-shirt row has no swatch button")
    b.tool("rimworld/click_ui_target", {"targetId": row[-1]["id"], "timeoutMs": 8000})
    time.sleep(0.3)
    d.close_debug_log()
    d.beat("picker-open")

    for hexval, want in SWATCHES:
        picker = d.picker_items()
        hexfield = d.hex_field(picker)
        if hexfield is None:
            raise SystemExit(f"hex field not found before typing {hexval}")
        r = b.tool("rimworld/set_text_field",
                   {"targetId": hexfield["id"], "text": hexval, "mode": "typed",
                    "charsPerSecond": 10, "jitterPercent": 30, "clearFirst": True,
                    "controlName": "ApparelPainter_DirectInput"}, ok=False)
        if not (isinstance(r, dict) and r.get("success")):
            raise SystemExit(f"set_text_field failed on {hexval}: {json.dumps(r)[:300]}")

        setbtn = next((e for e in d.picker_items()
                       if e["act"] and e["label"].strip().lower() == "set"), None)
        if setbtn is None:
            raise SystemExit(f"no Set button after typing {hexval}")
        b.tool("rimworld/click_ui_target", {"targetId": setbtn["id"], "timeoutMs": 8000})
        time.sleep(0.25)
        rgb = d.rgb_readback()
        if rgb != want:
            raise SystemExit(f"{hexval} did not apply: rgb={rgb}, wanted {want}")

        picker = d.picker_items()
        plus = d.plus_cell(picker)
        if plus is None:
            raise SystemExit(f"no save (+) cell before saving {hexval}")
        before = d.invisible_cells(picker)
        b.tool("rimworld/click_ui_target", {"targetId": plus["id"], "timeoutMs": 8000})
        time.sleep(0.25)
        after = d.invisible_cells()
        if len(after) != len(before) + 1:
            raise SystemExit(
                f"saving {hexval} did not add a band cell ({len(before)} -> {len(after)})")
        print(f"  saved {hexval}  rgb={rgb}  band={len(after)}")

    b.tool("rimworld/step_game_ticks", {"ticks": 1, "pauseFirst": True})
    d.close_debug_log()
    d.beat("card-master")

    print("\ndone — crop the card from the card-master shot")


if __name__ == "__main__":
    main()
