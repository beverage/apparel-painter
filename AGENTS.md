# AGENTS.md

A RimWorld 1.6 mod: one C# assembly that adds a Paint tab wherever apparel
sits — outfit stands, Armor Racks, and vanilla-style storage, through the
`ContainerAdapter` seam (DEC-037; storage shows the tab only when it holds
apparel). Designator painters recolour en masse; this is the per-item layer.
No Harmony patches, no defs of its own
beyond keyed strings; art is a single UI icon texture
(`Textures/ApparelPainter/UI/`). **No save-file state** — item colours are
vanilla `CompColorable` scribing and the user's saved swatches live in
ModSettings (config file), so mid-save removal stays clean.

| Doc | Contents |
|---|---|
| [README.md](README.md) | what the mod does, for players |
| [docs/DESIGN.md](docs/DESIGN.md) | engine interfaces and why each took its shape — injection, adapter seam, bake sites, comp warts, layout mirror, droppers |
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | build, configurations, engine navigation, hot reload, scenes, upload staging, file map |
| [docs/TESTING.md](docs/TESTING.md) | what the harness asserts, what it deliberately does not, isolation, rules |

## Verify your work

```bash
dotnet build Source/ApparelPainter/ApparelPainter.csproj -c Release
```

End every session with a Release build: Debug and Release write the same
output path and Debug carries the ECR hot-reload rig beside our dll. The
Release build sweeps those artifacts; only `Assemblies/ApparelPainter.dll` is
ever tracked.

The regression harness is the release gate:

```bash
devtools/run-harness.sh          # three-mod list, ~20s, no hands
devtools/run-harness.sh --full   # your live mod list — covers the Dubs
                                 # interop case; run before a release
```

It launches an ISOLATED game instance (-savedatafolder; it refuses to touch
a running one), fires the mod's real entry points at spawned fixtures, and
exits non-zero on any failure. It asserts every engine surface this mod
leans on: the reflection bridges' private members (tripwires for game
updates), tab injection across the loaded modlist, the ColorForcer warts,
the stand's graphic-cache bake + recache, the picker's headless
preview/cancel/accept state machine, floor resolution across vanilla and
Dubs paint (including Dubs' unscribed-alpha reload wart), parse table,
layout-mirror consistency, and FloatMenu ordering. The harness BODY ships
in every configuration so the gate asserts the literal dll players install;
the launch flag is its only door — there is no debug-menu entry.

UI FEEL is still verified by eye in game (drag strip, overlays landing on
the base's pixels, targeting affordances) — the harness proves state, not
pixels. UI layout may be iterated through ECR hot-swap on a Debug build,
but anything touching colour state is confirmed on a clean Release restart.
Do not claim a behavioural change works without saying how it was checked.

## Invariants that will not announce themselves

**Every colour change must be followed by the owner's adapter Refresh —
renderers BAKE colours, and each family bakes somewhere else.** Four bake
sites so far, all found the hard way: the outfit stand's private graphic
cache (rebuilt only at add/remove/spawn — `StandGraphics.Recache`), Armor
Racks' `ContentsDrawer.IsApparelResolved`, ASF-family storage's per-item
print data (`AsfInterop` → `StorageRenderer.SetAllPrintDatasDirty`; plain
vanilla-drawn items are the ONE case `Notify_ColorChanged` handles alone),
and Dubs' floor grids on the read side. Miss one and painting "doesn't
work" on exactly that family until a reload. When a new storage
integration misrenders after paint, look for its bake first.

**`CompColorable.SetColor` no-ops on exact white for undyed items.** The
comp's private colour field defaults to white while inactive, and `SetColor`
early-outs on equality without activating. Route every colour write through
`ColorForcer`; never call `SetColor` directly.

**`SetColor` silently clears `DesiredColor`** — the vanilla dye-queue field.
Irrelevant while the mod is instant-only; a future dye mode must never
stage-then-instant-paint the same garment.

**The picker is a palette, not a modal.** `absorbInputAroundWindow`,
`preventCameraMotion`, `closeOnClickedOutside` and `draggable` are all OFF on
purpose: the map, camera and Paint tab stay live while it is open — the
tab-swatch eyedropper flow, the map-wide dropper and the on-map preview
depend on every one of these. Dragging is exclusively the full-width top
strip's job (`LateWindowOnGUI`: it runs outside the contents group, in
window space, before the base Window eats unhandled mousedowns). Do not
re-modalise the window or hand dragging back to the `draggable` flag.

**The map dropper rides the native Targeter, and Esc ordering matters.**
`OnCancelKeyPressed` stops targeting instead of closing the window while a
sip is armed; each successful sip re-arms via `retargetNextFrame` on the
next `WindowUpdate` (calling BeginTargeting from inside the targeter's own
callback would fight its stop sequence). `TargetingParameters` trap:
`mapObjectTargetsMustBeAutoAttackable` defaults TRUE and silently refuses
most items/buildings — keep it forced off. The dropper READS anything
(`DrawColor`); sources never need `CompColorable`.

**Dropper clicks resolve to the CELL, never the targeter's pick.** The
targeter chooses one thing per click by draw altitude — a wall-mounted
heater outranks the stand sharing its cell, an overlay building outranks
the carpet, and terrain is not a thing at all (it lives in `terrainGrid`;
painted colour via `ColorAt`, else `TerrainDef.DrawColor`). So
`OnDropperTarget` reads the whole cell and menus the stack — apparel /
things (topmost first) / floor — with bare cells targetable
(`canTargetLocations`) for the instant carpet sip. Do not reintroduce a
single-thing resolution.

**The picker's height and the Old-colour dropper share one layout mirror.**
`MirrorBaseLayout` replays `Dialog_ColorPickerBase`'s private RectDivider
arithmetic (constants verified against the 1.6.4871 decompile); the window's
`InitialSize` derives from it AND the revert dropper overlays the base's
readback box through it. Because size and mirror share the formula, a
vanilla layout change shows up as a **misplaced overlay, never an error** —
after any game update, open the picker and eyeball the Old-colour dropper
before trusting the rest of the layout.

**ITab injection is runtime, class-keyed, and touches both lists.**
`StaticConstructorOnStartup` runs after def resolution, so appending to
`inspectorTabs` alone does nothing — `inspectorTabsResolved` is the live
list. Never move the tab into an XML patch: list nodes on a shared vanilla
def are a commons (two mods' Adds clobber each other; root DEC-032).

**Do not mint graphics per drag frame.** Every unique colour creates a
permanent `GraphicDatabase` entry plus materials, never evicted. The picker
pushes live preview on colour commit (wheel mouse-up, palette click, field
entry), not per frame; `PreviewWhileDragging` (TweakValue) exists for feel
testing and must stay off by default.

**A picker target can leave the stand mid-dialog.** A pawn can take the
outfit while the dialog is open; worn apparel renders from graphics cached on
the pawn, which `Notify_ColorChanged` does not reach. Every write and revert
runs through `ColorForcer`, which dirties the wearer's renderer when the item
turns out to be worn — keep it that way.

**Declare members `internal`, never `private`, and never write an
auto-property.** Hot-swapped method bodies execute in a separate assembly and
Unity's Mono honours only `InternalsVisibleTo`; a `private` member or a
backing field throws at runtime mid-iteration.

**Do not bind hotkeys on gizmos or rows.** On storage-carrying buildings N, J
and F are taken (settings copy, settings paste, forbid) and O is reserved.

**Weapons sit in the same container.** `HeldItems` includes a held weapon,
which generally has no `CompColorable` — null-guard every comp access, and
render the row swatch-less rather than hiding it.

**A `.dds` beside a PNG silently shadows it.** Texture tools on player
machines (FasterGameLoading et al.) write `.dds` into the mod folder through
the symlink, with no timestamp check. `*.dds` is gitignored; when
regenerating `Textures/ApparelPainter/UI/Dropper.png` (scratch script,
`uv run --with pillow`), delete any `.dds` sibling first — the generator
script does this itself.

## File map

| File | Job |
|---|---|
| `ApparelPainterStartup.cs` | class-keyed ITab injection onto all three adapter families, both def lists, Contents-before-Paint order |
| `ITab_ApparelPainter.cs` | the Paint tab — rows, swatches, eyedropper mode, whole-building actions, the canonical display sort |
| `ContainerAdapter.cs` | the DEC-037 seam — stand / Armor Racks / generic-storage adapters: listing, refresh, spawned-ness, the tab-visibility gate |
| `AsfInterop.cs` | reflection poke of ASF's render cache (`SetAllPrintDatasDirty`) — the fourth bake site; present-only, degrades silently |
| `Harness.cs` | the regression suite + its flag-gated boot (`-apparelpainter-harness`); body ships in every configuration |
| `Dialog_StandColorPicker.cs` | picker subclass — drag strip, live preview on commit, direct input, snapshot revert |
| `ColorForcer.cs` | comp-wart-safe colour writes, natural colour, wearer dirtying |
| `StandGraphics.cs` | reflection bridge to the stand's private `RecacheGraphics` |
| `ApparelPainterTex.cs` | startup-loaded texture handles (dropper icon) |
| `ApparelPainterMod.cs` | Mod entry + ModSettings (saved swatches; no settings window on purpose) |
| `FloatMenu_Ordered.cs` | FloatMenu that keeps caller order — disabled header rows sink to the bottom in the stock one |
| `DubsInterop.cs` | reflection read of Dubs Paint Shop's floor-paint map component (its paint is invisible to TerrainGrid.ColorAt) |
| `DebugTools_*.cs` | SCENES-only film-set stages behind the media; destructive by design, never shipped |
