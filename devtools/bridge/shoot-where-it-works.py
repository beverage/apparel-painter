#!/usr/bin/env python3
"""Shoot the where-it-works beats through RimBridgeServer.

    devtools/bridge/shoot-where-it-works.py <player.log | port token>

Four stills on the vanilla storage scene (DebugTools_StorageScene: one
dressed stand, one Core shelf holding three garments — two pre-tinted
in the set's palette — one small shelf): idle, then the selection hops
stand → shelf → small shelf and the Paint tab follows. The shelf beat
is the money still for the gallery card; all four beats also cut into
a "same tab, wherever apparel sits" flip, since gallery slots animate.

Vanilla only, per the per-scene staging rule — the modded integrations
(Armor Racks, sbz, OSP) get their own secondary shots later.
"""
import sys

from gabp import connect

ORIGIN = (135, 130)
STAND = (ORIGIN[0] + 3, ORIGIN[1] + 4)
SHELF = (ORIGIN[0] + 6, ORIGIN[1] + 4)
# The small shelf sits two below the 2x1, flush with its right edge —
# the shelf's rect is (o+5,o+4)-(o+6,o+4), centre-placed.
SMALL = (ORIGIN[0] + 6, ORIGIN[1] + 2)
# Camera east of the cluster so it renders over the tab's right
# shoulder, mid-frame — the first take floated it high in dead space.
CAM = (ORIGIN[0] + 9, ORIGIN[1] + 4)
ZOOM = 12


class Shoot:
    def __init__(self, bridge):
        self.b = bridge
        self.beats = []

    def beat(self, tag, seconds):
        self.b.tool("rimworld/clear_hover_target", ok=False)
        r = self.b.tool("rimworld/take_screenshot",
                        {"fileName": f"wiw-{len(self.beats):02d}-{tag}",
                         "includeTargets": False, "suppressMessage": True})
        self.beats.append((r.get("path"), seconds))
        print(f"  beat {tag:14} {seconds}s  {r.get('path')}")


def main():
    b, _ = connect(sys.argv[1:])
    s = Shoot(b)

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

    # Wardrobe stage purely for its 96x64 ambient steel field.
    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Build wardrobe stage", "x": 77, "z": 93})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})

    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Build storage scene", "x": ORIGIN[0], "z": ORIGIN[1]})
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

    s.beat("idle", 1.0)

    for tag, cell in (("stand", STAND), ("shelf", SHELF), ("small", SMALL)):
        b.tool("rimworld/click_cell", {"x": cell[0], "z": cell[1]})
        b.tool("rimworld/open_inspect_tab", {"inspectTabId": "ITab_ApparelPainter"})
        s.beat(tag, 1.8)

    print("\nbeats:")
    for path, dur in s.beats:
        print(f"  {dur:>4}  {path}")


if __name__ == "__main__":
    main()
