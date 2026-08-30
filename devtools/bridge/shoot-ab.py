#!/usr/bin/env python3
"""Shoot the wardrobe wall before/after pair through RimBridgeServer.

    devtools/bridge/shoot-ab.py <player.log | port token> [--dusk] [--origin=X,Z]

Build, optionally re-pin the lighting to dusk, capture, paint, capture: the
same cell rect at the same pinned zoom either side of the paint, so the only
difference between a before frame and its after is colour. Captures land in
the instance's save-data Screenshots folder (each result carries the absolute
path); the closing lines print ready-to-run make-ab.sh commands for the row
and pair gifs.

Promoted from the 2026-08-27 salvage shoot.py, re-aimed at the committed
96x64 stage (the salvage predates it) and the lighting-pin debug actions.
Traps carried over: PIN THE ZOOM ONCE — screenshot_cell_rect selects the
right CELLS at whatever zoom is current and has no rootSize of its own.
CELL READS NEST UNDER result.cell, not result — reading result directly
returns empty and looks exactly like "nothing was built".
"""
import json
import sys

from gabp import connect

# DebugTools_WardrobeStage geometry — keep in lockstep with the C# constants.
PAD_W, PAD_D, STANDS = 96, 64, 8
ROW_X = (PAD_W - (STANDS * 2 - 1)) // 2      # first stand x-offset into the pad
ROW_Z = PAD_D // 2                           # row z-offset into the pad
PAIR_ZOOM, ROW_ZOOM = 12, 22


def resolve_action(b, label):
    """Find a debug action's stable path by label. search_debug_actions is
    authoritative; the literal candidates cover an offline result shape."""
    found = b.tool("rimworld/search_debug_actions", {"query": label}, ok=False)

    def paths(node):
        if isinstance(node, dict):
            p = node.get("path")
            if isinstance(p, str) and label.lower() in p.lower():
                yield p
            for v in node.values():
                yield from paths(v)
        elif isinstance(node, list):
            for v in node:
                yield from paths(v)

    for p in paths(found):
        return p
    for candidate in (f"Actions\\{label}", f"Actions\\T: {label}"):
        if "error" not in b.tool("rimworld/get_debug_action",
                                 {"path": candidate}, ok=False):
            return candidate
    raise SystemExit(f"cannot resolve debug action {label!r}")


def main():
    b, rest = connect(sys.argv[1:])
    dusk = "--dusk" in rest
    origin = (77, 93)          # centres the 96x64 pad on the 250x250 quicktest map
    for a in rest:
        if a.startswith("--origin="):
            origin = tuple(int(v) for v in a.split("=", 1)[1].split(","))

    b.tool("rimworld/start_debug_game_ready",
           {"readiness": "visual", "pauseIfNeeded": True, "timeoutMs": 300000})

    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Build wardrobe stage", "x": origin[0], "z": origin[1]})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})

    first = (origin[0] + ROW_X, origin[1] + ROW_Z)
    cell = (b.tool("rimworld/get_cell_info",
                   {"x": first[0], "z": first[1]}) or {}).get("cell") or {}
    things = [t.get("label", "?") for t in (cell.get("things") or [])]
    if not things:
        raise SystemExit("nothing at the first stand cell — stage did not build")
    print("built:", cell.get("terrainDefName"), things)

    if dusk:
        pin = resolve_action(b, "Pin lighting: dusk")
        b.tool("rimworld/execute_debug_action", {"path": pin})
        b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})
        print("lighting: dusk pinned via", json.dumps(pin))

    # Rects relative to the first stand: the pair is the leftmost man+woman,
    # the row is all eight, one cell of margin each side, z-1..z+1.
    # The pair is captured GENEROUS (9x8 cells with padding) because the
    # preview's final window is cut in post at half-tile precision: 6 tiles
    # wide, 5 tall, edges at x first-1.5..first+4.5 and z stand-1.5..+3.5,
    # which puts the stands on the second row from the bottom and leaves
    # the sky above them for the title (principal's framing, 2026-08-28).
    pair = dict(x=first[0] - 2, z=first[1] - 2, width=7, height=6, paddingCells=1)
    row = dict(x=first[0] - 1, z=first[1] - 1, width=STANDS * 2 + 1, height=3,
               paddingCells=1)

    def shot(rect, zoom, name):
        b.tool("rimworld/set_camera_zoom", {"rootSize": zoom})
        args = dict(rect)
        args.update(fileName=name, includeTargets=False)
        r = b.tool("rimworld/screenshot_cell_rect", args)
        c = r["clipRect"]
        print(f"  {name:14} {c['width']}x{c['height']}  {r['path']}")
        return (c["width"], c["height"]), r["path"]

    b.tool("rimworld/clear_selection")
    print("before:")
    dp1, p_before = shot(pair, PAIR_ZOOM, "before-pair")
    dr1, r_before = shot(row, ROW_ZOOM, "before-row")

    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Paint wardrobe stage", "x": origin[0], "z": origin[1]})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})
    b.tool("rimworld/clear_selection")
    print("after:")
    dp2, p_after = shot(pair, PAIR_ZOOM, "after-pair")
    dr2, r_after = shot(row, ROW_ZOOM, "after-row")

    print("\npair dims match:", dp1 == dp2, "| row dims match:", dr1 == dr2)
    tag = "dusk" if dusk else "noon"
    print("cut:")
    print(f"devtools/make-ab.sh '{r_before}' '{r_after}' wardrobe-row-{tag}.gif")
    print(f"devtools/make-ab.sh '{p_before}' '{p_after}' wardrobe-pair-{tag}.gif")


if __name__ == "__main__":
    main()
