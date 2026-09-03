#!/usr/bin/env python3
"""Dump every actionable UI target with its surface, to find how the
inspect-pane ITab body is addressed. Throwaway triage, not a shoot driver."""
import sys
from gabp import connect

def main():
    b, _ = connect(sys.argv[1:])
    out = []
    def walk(node, surface):
        if isinstance(node, list):
            for i in node: walk(i, surface)
            return
        if not isinstance(node, dict): return
        if node.get("surfaceTargetId"): surface = str(node.get("surfaceTargetId"))
        tid = node.get("targetId") or ""
        if str(tid).startswith("ui-element:"):
            r = node.get("screenRect") or node.get("rect") or {}
            out.append((surface, r.get("width",0), r.get("height",0),
                        bool(node.get("actionable")), str(node.get("kind") or ""),
                        str(node.get("label") or ""), tid))
        for v in node.values(): walk(v, surface)
    walk(b.tool("rimworld/get_ui_layout", {}), "?")
    surfaces = {}
    for s,*_ in out: surfaces[s] = surfaces.get(s,0)+1
    print(f"{len(out)} targets across {len(surfaces)} surfaces")
    for s,n in sorted(surfaces.items(), key=lambda kv:-kv[1]):
        print(f"  {n:4d}  {s}")
    print("\nactionable targets 18-50px wide (swatch/style candidates):")
    for s,w,h,act,kind,label,tid in out:
        if act and 18 <= w <= 50 and 14 <= h <= 30:
            print(f"  {w:>4}x{h:<4} kind={kind:<18} label={label!r:<20} surface={s}\n        {tid}")

if __name__ == "__main__":
    main()
