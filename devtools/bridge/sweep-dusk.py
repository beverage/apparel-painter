#!/usr/bin/env python3
"""Sweep the wardrobe wall across an hour range and capture each hour.

    sweep-dusk.py <player.log | port token> [--from=12] [--to=21] [--origin=X,Z]

WHY A SWEEP: the dusk cast is a function of the tile's latitude and the day of
year (GenCelestial.CelestialSunGlowPercent), and quicktest re-rolls the world
every launch. Judging the cast BETWEEN takes never converged because each take
was a different planet. One world roll, many hours: the curve is comparable.

Captures land in the instance's Screenshots folder; measure them offline and
pick the hour, then shoot the real A/B at it.
"""
import sys, os

sys.path.insert(0, "/Users/alexbeverage/Code/Apps/rimworld/apparel-painter/devtools/bridge")
from gabp import connect

PAD_W, PAD_D, STANDS = 96, 64, 8
ROW_X = (PAD_W - (STANDS * 2 - 1)) // 2
ROW_Z = PAD_D // 2
PAIR_ZOOM, ROW_ZOOM = 12, 22


def resolve_action(b, label):
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
    origin = (77, 93)
    h_from, h_to = 12, 21
    for a in rest:
        if a.startswith("--origin="):
            origin = tuple(int(v) for v in a.split("=", 1)[1].split(","))
        if a.startswith("--from="):
            h_from = int(a.split("=", 1)[1])
        if a.startswith("--to="):
            h_to = int(a.split("=", 1)[1])

    b.tool("rimworld/start_debug_game_ready",
           {"readiness": "visual", "pauseIfNeeded": True, "timeoutMs": 300000})
    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Build wardrobe stage", "x": origin[0], "z": origin[1]})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})

    first = (origin[0] + ROW_X, origin[1] + ROW_Z)
    cell = (b.tool("rimworld/get_cell_info",
                   {"x": first[0], "z": first[1]}) or {}).get("cell") or {}
    if not (cell.get("things") or []):
        raise SystemExit("nothing at the first stand cell — stage did not build")
    print("built:", cell.get("terrainDefName"),
          [t.get("label", "?") for t in cell["things"]])

    # The stage builder pins noon on its way out, so hour 12 is where we start.
    nxt = resolve_action(b, "Pin lighting: +1 hour")
    print("hour step action:", nxt)

    pair = dict(x=first[0] - 2, z=first[1] - 2, width=7, height=6, paddingCells=1)
    row = dict(x=first[0] - 1, z=first[1] - 1, width=STANDS * 2 + 1, height=3,
               paddingCells=1)

    def shot(rect, zoom, name):
        b.tool("rimworld/set_camera_zoom", {"rootSize": zoom})
        args = dict(rect)
        args.update(fileName=name, includeTargets=False)
        r = b.tool("rimworld/screenshot_cell_rect", args)
        return r["path"]

    b.tool("rimworld/clear_selection")
    for hour in range(h_from, h_to + 1):
        p = shot(pair, PAIR_ZOOM, f"sweep-{hour:02d}-pair")
        r = shot(row, ROW_ZOOM, f"sweep-{hour:02d}-row")
        print(f"  {hour:02d}h  {os.path.basename(p)}  {os.path.basename(r)}")
        print(f"       {os.path.dirname(p)}")
        if hour < h_to:
            b.tool("rimworld/execute_debug_action", {"path": nxt})
            b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})

    print("\nsweep done — measure, then shoot the A/B at the chosen hour")


if __name__ == "__main__":
    main()
