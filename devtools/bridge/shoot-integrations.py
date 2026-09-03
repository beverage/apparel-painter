#!/usr/bin/env python3
"""Shoot the modded-storage integrations beats through RimBridgeServer.

    devtools/bridge/shoot-integrations.py <player.log | port token>

Three stills on the integrations scene (DebugTools_IntegrationsScene:
one sbz Neat Storage hanger shelf carrying eight dusters painted
through a full rainbow, one LWM Deep Storage clothing rack with a
light Core fill, one shirt in the set's teal): idle, then the
selection hops hanger -> rack and the Paint tab follows. The hanger
beat is the money still for the card; all three beats also cut into a
flip, gallery-style, matching where-it-works.

The secondary-shot counterpart of shoot-where-it-works.py: integration
mods appear only in shots about them (per-scene staging rule), and
this scene is exactly that shot. The scene's build message calls out
any missing integration mod — if a container is absent from frame,
read the message line in the idle beat before blaming the driver.

Machinery carried from the sibling drivers: pre-build window sweep,
colonist destroy sweep with the 3x3 droppings pass, wardrobe stage as
ambient steel field, surface-anchored tab opens.
"""
import sys

from gabp import connect

ORIGIN = (135, 130)
# Multi-cell buildings place by CENTRE: a 2x1 spawned at +3 occupies
# +2..+3, at +6 occupies +5..+6. Clicking the spawn cell itself always
# hits the building.
HANGER = (ORIGIN[0] + 3, ORIGIN[1] + 4)
RACK = (ORIGIN[0] + 6, ORIGIN[1] + 4)
# The armor rack folds into the where-it-works L (principal,
# 2026-08-30): two below the row, flush with the LWM rack's right
# edge, facing south — it fills the frame's lower-middle gap instead
# of stretching the row.
ARMOR = (ORIGIN[0] + 6, ORIGIN[1] + 2)
# Camera east of the cluster, storage-scene style: containers render
# over the tab's right shoulder, mid-frame. Back to +8 — the L keeps
# all content within x+2..+6, the take-2 fit.
CAM = (ORIGIN[0] + 8, ORIGIN[1] + 4)
ZOOM = 12


class Shoot:
    def __init__(self, bridge):
        self.b = bridge
        self.beats = []

    def beat(self, tag, seconds):
        self.b.tool("rimworld/clear_hover_target", ok=False)
        r = self.b.tool("rimworld/take_screenshot",
                        {"fileName": f"int-{len(self.beats):02d}-{tag}",
                         "includeTargets": False, "suppressMessage": True})
        self.beats.append((r.get("path"), seconds))
        print(f"  beat {tag:14} {seconds}s  {r.get('path')}")


def main():
    b, _ = connect(sys.argv[1:])
    s = Shoot(b)

    # wait_for_game_loaded, NOT start_debug_game_ready: run-scene.sh boots
    # with -quicktest, so the game is already up, and asking an already-booted
    # instance to generate one stalls indefinitely whenever its window is not
    # frontmost (gamedata/rimbridgeserver.md, operational traps).
    b.tool("rimbridge/wait_for_game_loaded", {"timeoutMs": 300000})

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
    # scene's own pad is smaller than the zoom-12 frame.
    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Build wardrobe stage", "x": 77, "z": 93})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})

    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Build integrations scene",
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

    s.beat("idle", 1.0)

    for tag, cell in (("hanger", HANGER), ("rack", RACK), ("armor", ARMOR)):
        b.tool("rimworld/click_cell", {"x": cell[0], "z": cell[1]})
        b.tool("rimworld/open_inspect_tab", {"inspectTabId": "ITab_ApparelPainter"})
        s.beat(tag, 2.0)

    print("\nbeats:")
    for path, dur in s.beats:
        print(f"  {dur:>4}  {path}")


if __name__ == "__main__":
    main()
