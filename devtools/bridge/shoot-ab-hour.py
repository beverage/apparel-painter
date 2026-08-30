#!/usr/bin/env python3
"""Shoot the wardrobe A/B at an ARBITRARY pinned hour.

    shoot-ab-hour.py <player.log | port token> --hour=17 [--origin=X,Z]

shoot-ab.py can only reach DuskHour (19.0, a TweakValue the bridge cannot
set). This walks there from noon instead: the stage builder pins noon on its
way out, so N presses of "Pin lighting: +1 hour" land on hour 12+N exactly,
re-clamping the weather to Clear at every step. Same world roll as the sweep
that chose the hour, so the measured cast is the cast you get.

Everything else is shoot-ab.py verbatim: same rects, same pinned zooms, same
paint-in-between, so make-ab.sh's morph still has pixel correspondence.
"""
import sys

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
    hour = None
    minutes = 0
    for a in rest:
        if a.startswith("--origin="):
            origin = tuple(int(v) for v in a.split("=", 1)[1].split(","))
        if a.startswith("--hour="):
            hour = int(a.split("=", 1)[1])
        if a.startswith("--minutes="):
            minutes = int(a.split("=", 1)[1])
    if hour is None:
        raise SystemExit("--hour=N required")

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

    steps = hour - 12
    if steps < 0:
        raise SystemExit("--hour must be >= 12 (the stage pins noon)")
    nxt = resolve_action(b, "Pin lighting: +1 hour")
    for _ in range(steps):
        b.tool("rimworld/execute_debug_action", {"path": nxt})
        b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})
    # Sub-hour precision comes from raw ticks (2500/hour): the debug action is
    # integer-only and a finer one would need a rebuild, and a rebuild means a
    # relaunch, which re-rolls the world the hour was chosen on.
    if minutes:
        b.tool("rimworld/step_game_ticks",
               {"ticks": int(2500 * minutes / 60), "pauseFirst": True})
    print(f"lighting: pinned {hour}:{minutes:02d} ({steps} hour steps from noon"
          f"{f', +{minutes}min in ticks' if minutes else ''})")

    pair = dict(x=first[0] - 2, z=first[1] - 2, width=7, height=6, paddingCells=1)
    row = dict(x=first[0] - 1, z=first[1] - 1, width=STANDS * 2 + 1, height=3,
               paddingCells=1)

    def shot(rect, zoom, name):
        b.tool("rimworld/set_camera_zoom", {"rootSize": zoom})
        args = dict(rect)
        args.update(fileName=name, includeTargets=False)
        r = b.tool("rimworld/screenshot_cell_rect", args)
        c = r["clipRect"]
        print(f"  {name:18} {c['width']}x{c['height']}  {r['path']}")
        return (c["width"], c["height"]), r["path"]

    b.tool("rimworld/clear_selection")
    print("before:")
    dp1, p_before = shot(pair, PAIR_ZOOM, f"before-pair-{hour:02d}{minutes:02d}")
    dr1, r_before = shot(row, ROW_ZOOM, f"before-row-{hour:02d}{minutes:02d}")

    b.tool("rimworld/execute_debug_action",
           {"path": "Actions\\T: Paint wardrobe stage", "x": origin[0], "z": origin[1]})
    b.tool("rimworld/step_game_ticks", {"ticks": 2, "pauseFirst": True})
    b.tool("rimworld/clear_selection")
    print("after:")
    dp2, p_after = shot(pair, PAIR_ZOOM, f"after-pair-{hour:02d}{minutes:02d}")
    dr2, r_after = shot(row, ROW_ZOOM, f"after-row-{hour:02d}{minutes:02d}")

    print("\npair dims match:", dp1 == dp2, "| row dims match:", dr1 == dr2)
    print("cut:")
    print(f"devtools/make-ab.sh '{r_before}' '{r_after}' wardrobe-row-dusk.gif")
    print(f"devtools/make-ab.sh '{p_before}' '{p_after}' wardrobe-pair-dusk.gif")


if __name__ == "__main__":
    main()
