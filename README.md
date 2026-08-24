# Apparel Painter

A RimWorld 1.6 mod: paint the apparel held on outfit stands — the one
container designator-based painting tools cannot reach.

Select an outfit stand and open its **Paint** tab to recolour any item it
holds, or the whole stand at once, with the base game's full colour picker
(HSV wheel, RGB/hex fields, palette). Instant and free.

- Requires **Odyssey** (the outfit stand). No other dependencies — not even
  Harmony.
- Colours are ordinary base-game apparel colours, saved by vanilla: install
  or remove mid-save freely; painted apparel keeps its colour even with the
  mod removed.
- Works on the vanilla stand, the Biotech kid stand, and any modded stand
  whose building reuses the vanilla stand class.

**Status: in development — not yet on the Workshop.** The current build
injects the tab and lists stand contents; the painting UI is in progress.

## Building

```
dotnet build Source/ApparelPainter/ApparelPainter.csproj -c Release
```

MIT licensed.
