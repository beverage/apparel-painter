#!/usr/bin/env python3
"""Composite the subtle click pointer onto manifest-flagged beat frames.

    composite-clicks.py <take-dir> <task-output-file> <cursor-png>

Reads the shoot driver's stdout (the task output file), extracts the
trailing "manifest:" JSON block, and for each entry pastes the pointer
sprite onto the matching loop-NN-tag.png IN PLACE in the take dir —
run it on the working copies, never on raw masters.

Placement: manifest rects are ALREADY master-pixel space — RimWorld
renders its UI 1:1 on the retina buffer (verified against the band
cells at x1120+28n, y1217, w26: SwatchCell's own 26 with a 2px gap).
The pointer's tip lands at ~(62%, 66%) of the control — the classic
"tip on the control, body hanging off" screenshot idiom — so the
control's colour stays visible under the click.
"""
import json
import os
import subprocess
import sys

TIP_X, TIP_Y = 3, 1   # the sprite's hotspot: arrow tip in the 32px art


def main():
    take_dir, output_file, cursor = sys.argv[1], sys.argv[2], sys.argv[3]
    with open(output_file) as f:
        text = f.read()
    at = text.rindex("manifest:")
    manifest = json.loads(text[at + len("manifest:"):])
    if not manifest:
        raise SystemExit("manifest is empty — nothing to composite")

    names = [name for name in os.listdir(take_dir) if name.endswith(".png")]

    for entry in manifest:
        suffix = f"-{entry['beat']:02d}-{entry['tag']}.png"
        hits = [name for name in names if name.endswith(suffix)]
        if len(hits) != 1:
            raise SystemExit(f"expected one frame matching *{suffix} in "
                             f"{take_dir}, found {hits}")
        prefix = hits[0]
        tip_px = round(entry["x"] + entry["w"] * 0.62)
        tip_py = round(entry["y"] + entry["h"] * 0.66)
        paste_x = tip_px - TIP_X
        paste_y = tip_py - TIP_Y
        path = os.path.join(take_dir, prefix)
        subprocess.run(
            ["magick", path, cursor,
             "-geometry", f"+{paste_x}+{paste_y}", "-composite", path],
            check=True)
        print(f"  marked {prefix} at ({tip_px},{tip_py})")

    print(f"{len(manifest)} frames marked")


if __name__ == "__main__":
    main()
