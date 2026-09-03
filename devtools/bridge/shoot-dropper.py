#!/usr/bin/env python3
"""Shoot the dropper demo (v2: pinned hover + click manifest) through
the RimBridgeServer FORK-era pipeline.

    devtools/bridge/shoot-dropper.py <player.log | port token>

Nine stepped beats on the MINIMAL dropper scene
(DebugTools_DropperScene: one dressed stand, Jane in the teal duster,
one burgundy rug), same composition and beat structure as the blessed
2026-08-29 cut: select stand, Paint tab, duster picker, arm the map
dropper, sip Jane's worn teal through the categorised float menu, sip
the rug straight off the floor, Accept, finale.

WHAT V2 FIXES — the parked pink ring (principal, 2026-08-29 late):
while a targeter is armed the engine highlights the cell under the
mouse, and clear_hover_target unmasks the HARDWARE mouse — so the
old cut carried the targeter ring wherever the physical cursor
happened to rest. V2 PINS the hover on the sip target for every
armed beat (set_hover_target after the clear), so the engine's own
ring — and the dropper mouse attachment that rides the perceived
mouse — mark the sip location natively. The Dropper.png post-
composite on the rug beat is expected to become unnecessary; judge
the frames before reaching for it.

V2 also dumps the click manifest (see shoot-core-loop.py): the
duster row swatch and the arm click get the subtle pointer in
assembly via devtools/bridge/composite-clicks.py; sips are map-side
and the pinned ring carries them; Accept stays unmarked (the closing
dialog is its own signal).

Timing lessons carried from the blessed cut: the click-feedback ring
is TICK-aged and the shoot runs paused — step ~40 ticks after the
menu pick to expire Jane's ring; capture the rug sip after ONE tick
so its fresh fleck marks the source.
"""
import json
import sys
import time

from gabp import connect

ORIGIN = (135, 130)
STAND1 = (ORIGIN[0] + 3, ORIGIN[1] + 5)       # the one dressed stand
JANE = (ORIGIN[0] + 6, ORIGIN[1] + 5)         # teal duster + cream bowler
RUG = (ORIGIN[0] + 6, ORIGIN[1] + 2)          # burgundy rug, centre cell
CAM = (STAND1[0] + 5, STAND1[1] - 1)          # cluster lands left of the picker
ZOOM = 12


class Driver:
    def __init__(self, bridge):
        self.b = bridge
        self.beats = []
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

    def row_swatch(self, garment):
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

    def arm_map_dropper(self):
        """The picker's map dropper is its BOTTOM-MOST icon_button (the
        Set-row eyedropper); the higher one is the Old-colour revert."""
        drops = [e for e in self.picker_items()
                 if e["act"] and "icon_button" in e["kind"]]
        if not drops:
            drops = [e for e in self.picker_items()
                     if e["act"] and "button_image" in e["source"]]
        if not drops:
            raise SystemExit("no dropper toggle in the picker surface")
        drops.sort(key=lambda e: e["y"])
        self.click(drops[-1])
        self.mark(drops[-1])

    def beat(self, tag, seconds, hover=None):
        self.b.tool("rimworld/clear_hover_target", ok=False)
        if hover is not None:
            self.b.tool("rimworld/set_hover_target",
                        {"x": hover[0], "z": hover[1]}, ok=False)
        r = self.b.tool("rimworld/take_screenshot",
                        {"fileName": f"drop-{len(self.beats):02d}-{tag}",
                         "includeTargets": False, "suppressMessage": True})
        self.beats.append((r.get("path"), seconds))
        if self.pending is not None:
            self.manifest.append({"beat": len(self.beats) - 1, "tag": tag,
                                  **self.pending})
            self.pending = None
        print(f"  beat {tag:16} {seconds}s  {r.get('path')}")


def main():
    b, _ = connect(sys.argv[1:])
    d = Driver(b)

    # wait_for_game_loaded, NOT start_debug_game_ready: run-scene.sh boots
    # with -quicktest, so the game is already up, and asking an already-booted
    # instance to generate one stalls indefinitely whenever its window is not
    # frontmost (gamedata/rimbridgeserver.md, operational traps).
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
           {"path": "Actions\\T: Build wardrobe stage", "x": 77, "z": 93})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})
    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Build dropper scene", "x": ORIGIN[0], "z": ORIGIN[1]})
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

    d.beat("idle", 0.8)

    b.tool("rimworld/click_cell", {"x": STAND1[0], "z": STAND1[1]})
    d.beat("selected", 0.6)

    b.tool("rimworld/open_inspect_tab", {"inspectTabId": "ITab_ApparelPainter"})
    d.close_debug_log()
    d.beat("tab-open", 1.0)

    d.row_swatch("duster")
    time.sleep(0.3)
    d.beat("picker-open", 1.2)

    d.arm_map_dropper()
    b.tool("rimworld/click_cell", {"x": JANE[0], "z": JANE[1]})
    d.beat("menu-on-jane", 1.4, hover=JANE)

    b.tool("rimworld/execute_context_menu_option",
           {"optionIndex": -1, "label": "duster"})
    b.tool("rimworld/step_game_ticks", {"ticks": 40, "pauseFirst": True})
    d.beat("sip-jane", 1.6, hover=JANE)

    b.tool("rimworld/click_cell", {"x": RUG[0], "z": RUG[1]})
    b.tool("rimworld/step_game_ticks", {"ticks": 1, "pauseFirst": True})
    d.beat("sip-rug", 1.6, hover=RUG)
    b.tool("rimworld/step_game_ticks", {"ticks": 40, "pauseFirst": True})

    d.click_label("Accept")
    b.tool("rimworld/step_game_ticks", {"ticks": 1, "pauseFirst": True})
    d.beat("accepted", 0.8)

    b.tool("rimworld/clear_selection")
    d.beat("finale", 2.0)

    print("\nbeats:")
    for path, dur in d.beats:
        print(f"  {dur:>4}  {path}")
    print("\nmanifest:")
    print(json.dumps(d.manifest, indent=1))


if __name__ == "__main__":
    main()
