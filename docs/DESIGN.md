# Design

What this mod interfaces with inside RimWorld, and why each interface took the
shape it did. For build and debug, see [DEVELOPMENT.md](DEVELOPMENT.md); for
how it is verified, see [TESTING.md](TESTING.md).

Engine claims were checked against the decompiled game assembly, version
**1.6.4871**. References like `Verse/Thing.cs:638` point into that
decompilation. Line numbers drift across game versions; method names drift
much more slowly. Claims about other mods were checked against their shipped
assemblies or, where they publish it, their source, at the versions current
on 2026-08-23 through 2026-08-30.

## What it does

A **Paint** tab appears on every building that holds apparel — outfit stands,
armor racks, storage — listing what is inside. Each row opens the base game's
color picker at that one item; the picker previews on the actual map, reverts
exactly on cancel, and commits on accept. That is the whole mod: it ships one
assembly, one 16-pixel icon, and a file of UI strings. No Harmony, no defs,
no patches.

## Constraints

| Constraint | Consequence |
|---|---|
| No Harmony | The entire engine surface is a startup list append plus reflection against four neighbours' internals. Nothing is patched, so load order is irrelevant and there is no patch for another mod to collide with. The price: every integration must fail soft when a neighbour's internals move, and the [harness](TESTING.md) exists largely to notice when they do. |
| No save-file state | Colors are vanilla `CompColorable` state, scribed by the base game; saved swatches are ModSettings, a config file. Removing the mod costs the tools and nothing else. Any future feature that wants scribing (a dye-paying mode, per-building state) reopens this constraint deliberately. |
| The per-item layer, not the bulk painter | Dubs Paint Shop repaints en masse with designators. This mod is one garment, one building at a time, previewed and resettable — including inside containers designators cannot target. No designators here, ever; the two mods divide the problem instead of fighting over it. |
| Vanilla UI, densified | The picker is the base game's own dialog with bands added around it, so every control a player already knows keeps working. The price is a mirror of the base's private layout arithmetic, and a specific failure mode when the game updates — see [the layout mirror](#the-layout-mirror). |

## RimWorld in five concepts

No modding experience assumed; this is everything the rest of the document
uses.

1. **Defs.** Game content is XML (`ThingDef` for every building and item),
   loaded into a global database at startup. A def names its C# behaviour
   class in `thingClass`, and carries UI wiring such as the list of
   inspector tabs. Mods can add defs, patch others' defs with XML, or — as
   here — edit the loaded lists from C# after everything is resolved.
2. **Comps.** A `ThingComp` attaches per-thing state and behaviour by
   composition. `CompColorable` is the one that matters here: it is Core
   (attached to the root apparel abstract,
   `Data/Core/Defs/ThingDefs_Misc/Apparel_Various.xml:31`), DLC-free, and
   holds an item's applied color behind an `Active` flag. Ideology gates
   only vanilla's recolor *pipeline* (the styling station and its job); the
   comp itself carries no checks.
3. **Inspect tabs.** The tab strip on a selected building. Each `ThingDef`
   lists tab *types* (`inspectorTabs`) which resolve to shared singleton
   instances (`inspectorTabsResolved`, built in `ResolveReferences`,
   `Verse/ThingDef.cs:1653`). The strip draws right to left from that list.
4. **Graphics.** Things draw through `Graphic` objects cached in
   `GraphicDatabase`, keyed by path, shader, size *and color* — a recolored
   item needs a different cache entry. A spawned item reacts to
   `Notify_ColorChanged` by re-resolving its graphic. The trouble, and most
   of this mod's engine knowledge, is that several renderers *bake* copies
   of item graphics and never hear that notification — see
   [Renderers bake color](#renderers-bake-color).
5. **Scribing.** The save system. Objects write fields via `Scribe_*.Look`;
   unrecognized fields are skipped silently on load, which is what makes
   "remove the mod, keep the save" work. ModSettings scribe to a config
   file instead of the save — same API, different lifetime.

## The tab, and how it gets there

`ITab_ApparelPainter` is injected at startup by a `StaticConstructorOnStartup`
class that walks every loaded `ThingDef` and appends the tab to each def whose
`thingClass` belongs to one of the three adapter families. Class-keyed, not
defName-keyed: every def whose class is `Building_OutfitStand` or a subclass
qualifies — the vanilla stand, the Biotech kid stand, Outfit Stands Plus, any
future stand mod — without naming any of them. Without Odyssey no stand def
exists, the scan matches storage only, and nothing breaks.

Two mechanics matter:

- **Both lists, not one.** Static constructors run after def resolution, so
  `inspectorTabs` (the type list) has already been resolved into
  `inspectorTabsResolved` (the live instance list). Appending to the type
  list alone changes nothing until a re-resolve that never comes; the
  injection appends to both, fetching the shared instance through
  `InspectTabManager.GetSharedInstance` exactly as `ResolveReferences` does.
- **Why not an XML patch.** List nodes on a shared vanilla def are a
  commons: two mods that each `PatchOperationAdd` a list node onto the same
  def do not merge — the loader reports "defines the same field twice" and
  the last one wins, silently deleting the earlier mod's additions. A
  runtime append has no such failure mode and no load-order sensitivity.
  This mod patches no XML anywhere, so the hazard never arises.

**Contents before Paint.** The tab strip should read "what this holds" before
"what you can do to it", on every family. The strip draws right-to-left from
the def list, and the families reach the screen by different routes: outfit
stands carry their Contents tab *on the def*, so a plain append would land
Paint leftmost — the injection instead inserts Paint *before* a def-listed
tab whose type name contains `Contents` (or `Inventory`, which is what LWM's
Deep Storage calls its def-listed contents tab). `Building_Storage` families
get their Contents tab contributed dynamically after the def list, so for
them the plain append already displays in the same order. Armor Racks names
its contents tab "Rack" and its strip reads Rack | Paint | Storage as-is
(verified in game, 2026-08-30); no wider match is needed.

The tab is visible only on player-faction buildings, and only when the
adapter says so — see the visibility gate below.

## The adapter seam

"Wherever apparel sits" is one tab backed by one seam: `ContainerAdapter`,
one implementation per family of apparel-holding building, first match wins.

| Adapter | Matches | Items | Refresh after painting |
|---|---|---|---|
| `StandAdapter` | `Building_OutfitStand` and subclasses | `HeldItems` — everything, including a parked weapon | reflection call to the stand's private `RecacheGraphics` |
| `ArmorRackAdapter` | `ArmorRacks.Things.ArmorRack` (resolved only when the mod is loaded) | vanilla `IThingHolder` enumeration — no reflection needed to read | set `ContentsDrawer.IsApparelResolved = false`, two cached public `FieldInfo`s |
| `StorageAdapter` | `Building_Storage` and subclasses | `slotGroup.HeldThings`, filtered to things carrying `CompColorable` | `AsfInterop` renderer poke; a no-op on plain vanilla storage |

The split that explains everything else in the table: **stands and racks are
containers, storage is spawned.** Container-held items are unspawned things
inside a `ThingOwner`; the map does not draw them — the *building's* renderer
does, from baked copies, which is why those two adapters need a refresh hook.
Storage items are real spawned things standing in the building's cells; the
map draws them itself and `Notify_ColorChanged` is enough — except for the
ASF family, which bakes anyway (next section). The same split drives
`ItemsSpawned`, which the map dropper uses: a stand's contents have to be fed
into the dropper's menu explicitly, while a shelf's contents are found by the
cell scan like any other item on the ground.

Two listing policies, deliberately different:

- **Stands list everything they hold.** The tab is the stand's honest
  inventory; a held weapon renders as a row without a swatch rather than
  being hidden, because a tab that omits items misreports the building.
- **Storage lists only what is paintable.** Rows are things with
  `CompColorable` — mostly apparel, but also textile stacks, unfinished
  apparel, and modded colorables. This keeps the tab truthful in the other
  direction: wherever it appears, everything it lists can be painted.

**The visibility gate is load-bearing.** Stands and racks are apparel
furniture and always show the tab. Storage shows it only when the building
currently holds something paintable — `TabVisible` enumerates and returns on
the first hit — because `Building_Storage` matches every crate, fridge and
pallet in the modded ecosystem, and a Paint tab on a food bin is clutter that
would read as a bug report waiting to happen.

## Renderers bake color

The invariant the mod is organized around: **renderers bake item color, and
each family bakes somewhere else.** A `CompColorable` write updates the
item's state and its own graphic; whether anything *on screen* changes
depends on who is drawing it. Four bake sites, each found the hard way:

| Site | What is baked | Invalidated on | Our hook |
|---|---|---|---|
| Outfit stand | a private list of `Graphic`s built with `apparel.DrawColor` at bake time (`RecacheGraphics` → `ApparelGraphicRecordGetter.TryGetGraphicApparel` → `GraphicDatabase.Get<Graphic_Multi>(…, apparel.DrawColor)`) | add, remove, spawn — never color | invoke `RecacheGraphics` by reflection after every write batch |
| Armor Racks | `ContentsDrawer`'s cached `ApparelGraphics` list behind a public `IsApparelResolved` flag (verified from its shipped source) | its own resolve flag | write the flag false; the drawer re-resolves on next draw |
| ASF-family storage ([sbz] Neat Storage, Reel's, every skin) | per-item "print data" baked by `StorageRenderer` | add, remove, settings — never color | `Renderer.SetAllPrintDatasDirty()` + `TryUpdateCurrentGraphic()`, all public, via reflection |
| Dubs Paint Shop floors (the read side) | per-cell color grids in its own map component, invisible to `TerrainGrid.ColorAt` | — | read both of its getters — see [the droppers](#the-droppers) |

Plain vanilla-drawn spawned items are the one case `Notify_ColorChanged`
handles alone. Everything else renders the old color until a reload — which
in play reads as "painting doesn't work on exactly this shelf", with no error
anywhere. Hence the rule the adapters encode: **every color write batch ends
with the owner's `Refresh`**, and when a new storage integration misrenders
after painting, the first question is *where does it bake*.

The stand's def is `drawerType RealtimeOnly`, so a recache shows on the map
the same frame. That single engine fact is what makes live preview *on the
actual stand* better than any paper-doll: the thing you are looking at is the
mod's render target.

One more cache sits on pawns: **worn apparel renders from graphics cached on
the wearer**, which `Notify_ColorChanged` also does not reach. A picker
target can leave the building mid-dialog (a colonist takes the outfit while
you are choosing), and both preview and revert must still land — so every
write checks whether the item's holder is now a `Pawn_ApparelTracker` and
dirties that pawn's renderer when it is.

## CompColorable, and the writes that lie

Color resolves comp-first: active comp color, else stuff color, else the
def's own `graphicData` tint (`Verse/Thing.cs:638`). "Reset to natural" is
therefore not a write but a `Disable()` — the comp deactivates, vanilla
scribes it back to inactive, and the item is genuinely un-dyed rather than
painted to look un-dyed.

Every write goes through `ColorForcer`, because `SetColor` has two warts:

- **It no-ops on exact white for undyed items.** The comp's private color
  field defaults to white while inactive, and `SetColor` early-outs on
  equality *without activating*. A player painting an undyed uniform pure
  white would see nothing happen. The forcer nudges first — a write offset
  by 1/255 in blue, which can equal neither the private white nor the
  target — then writes the real color, so the real write never hits the
  early-out.
- **It silently clears `DesiredColor`**, vanilla's dye-queue field.
  Harmless while this mod is instant-only; recorded here because a future
  dye-paying mode must never stage-then-instant-paint the same garment.

## The picker

`Dialog_ColorPickerBase` is Core and abstract: HSV wheel, palette row,
default swatch, current/old readback, Accept/Cancel, six abstract members,
and — the property everything below leans on — `color` and `oldColor` are
`protected`, so a subclass can watch and steer the working color every
frame. `Dialog_GlowerColorPicker` is vanilla's model subclass. Ours returns
`ForcedColorValue => -1`: the glower forces brightness to 1, apparel wants
the full range — and that choice is why the brightness slider exists, since
the wheel encodes hue and saturation only and, unforced, every drag
*preserves* the current brightness. The slider is the missing third axis,
not decoration.

**A palette, not a modal.** The point of the window is seeing and touching
the world while it is open, and four base-window flags are off on purpose,
each with a feature hanging from it: `closeOnClickedOutside` (map clicks
must not end the session), `absorbInputAroundWindow` (the Paint tab stays
live — its swatches become eyedroppers), `preventCameraMotion` (walk the
camera to the next room mid-pick), and `draggable` (the base's drag branch
eats stray mousedowns; dragging belongs exclusively to the top strip, drawn
in `LateWindowOnGUI` in *window* space so it spans edge to edge, with
`GUI.DragWindow` consuming the press before the base window can). The window
remembers its position for the session; only Accept, Cancel or Esc close it.

**Four owned bands** — drag strip, the vanilla base, saved swatches, direct
input — with the base handed a rect that excludes ours, so its own layout
never learns the window grew.

- **The RGB fields are ours, not the base's.** The base is constructed with
  `ColorComponents.None`, which blanks its fields but still reserves their
  125-pixel column, so the wheel keeps its place. We draw the engine's own
  `Widgets.ColorTextfields` into that column — same control names, so the
  base's Tab-cycling still works — centred on the wheel's centreline.
- **Direct input** takes hex (`EFD8AE`, `#`-optional, 8 digits tolerated
  with alpha dropped) or a decimal triplet, using the engine's own
  `ParseColor` idiom: any component above 1 means the triplet is bytes,
  otherwise 0–1 floats. Unfocused, the field reads back the working color
  as canonical hex — the copy-a-color-between-buildings workflow. Vanilla
  shows RGB and HSV numerics but no hex anywhere, so this is new surface.
- **The palette row is vanilla's Structure `ColorDef`s** in shipped display
  order — the same chart wall paint uses, which players already know, and
  which paint-adding mods extend automatically.
- **Saved swatches** are a band of vanilla `ColorBox` cells: click adopts,
  right-click removes, the trailing `+` saves, capped at 60, persisted in
  ModSettings. Adding or removing a row re-derives the window height in
  place from the same formula as `InitialSize`.

### The layout mirror

The base lays itself out with private statics and `RectDivider` arithmetic,
and exposes none of it. Two things need those numbers anyway: the window's
height (the base assumes it owns the whole window; ours must be exactly base
consumption plus our bands) and the overlays — the brightness slider under
the wheel, the RGB fields on its centreline, the revert dropper on the Old
color box. `MirrorBaseLayout` replays the arithmetic — header, block row as
max(palette, wheel), the reserved 125px fields column and 250px palette
column with the wheel centred in the *remaining middle*, the readback rows,
the button row — with every constant verified against the 1.6.4871
decompile.

Because size and overlays share one formula, **vanilla layout drift shows up
as a misplaced overlay, never an error**. The overlays themselves skip
drawing when the mirrored numbers stop adding up (a slider that would land
outside the block, a readback box that would overlap the buttons), so the
failure mode is a missing affordance, not a corrupted window. After any game
update: open the picker and eyeball the Old-color dropper before trusting
the rest of the layout. The harness pins the mirror's internal consistency
and the engine members it replays, but only eyes can confirm pixels.

### Preview, revert, accept

Live preview pushes the working color to the *real items* on color commit —
wheel mouse-up, palette click, swatch click, field entry — then runs the
owner's adapter refresh, and the map shows it the same frame. Deliberately
not per drag frame: every unique color mints a permanent `GraphicDatabase`
entry plus materials, never evicted, so a dragged wheel would leak hundreds
of cache entries per second. (`PreviewWhileDragging` exists as a dev
TweakValue for feel testing, default off.)

Revert is a snapshot, not an undo stack. At open, the dialog records
`(wasActive, color)` per target; on close without accept, previously-painted
items get their color back and previously-natural items are `Disable`d back
to natural — the two cases a single "restore color" write would conflate.
Destroyed targets are skipped; departed-but-worn targets are reached through
the wearer-dirtying path above. Opening a picker while one is already open
closes the old one *through its own cancel path* first, so its snapshot
restores before the new one is taken.

## The droppers

Three surfaces, one rule: **sampling reads `DrawColor`, so a source never
needs `CompColorable`** — walls, stuffed furniture, floors and pawns are all
valid sources even though none can be painted here.

- **Tab swatches become eyedroppers** while any picker is open (the tab
  checks the WindowStack for one). This works across buildings: select the
  next stand and its tab sips into the same open picker. This is the
  feature the palette-not-modal flags exist for.
- **The Old color box carries a revert dropper**, overlaid through the
  layout mirror: one click re-adopts the color the picker opened with.
- **The map dropper** rides the native `Find.Targeter` with the dropper
  icon as mouse attachment. Continuous by design: each sip sets a flag and
  the next `WindowUpdate` re-arms targeting — calling `BeginTargeting` from
  inside the targeter's own callback fights its stop sequence. Esc ordering
  matters: while a sip is armed, `OnCancelKeyPressed` stops the targeting
  and eats the key, so Esc ends the dropper first and the window only on
  the next press. One `TargetingParameters` trap is load-bearing:
  `mapObjectTargetsMustBeAutoAttackable` defaults **true** and silently
  refuses most items and buildings; it must be forced off.

**Clicks resolve to the cell, never the targeter's pick.** The targeter
chooses one thing per click by draw altitude, which is the wrong authority
for sampling: the first field report was two stands whose dropper "didn't
work" — each shared its cell with a wall-mounted heater, which occupies the
room-side cell and outranks the stand, so every click sampled the heater.
`OnDropperTarget` therefore ignores the pick, re-reads the whole cell, and
menus the stack: **apparel** (worn by any pawn or corpse there, plus the
held items of any container-model building there), **things** (topmost
first, by altitude layer — the building itself is a legitimate stuff-color
source), **floor** (always last). A cell with only its floor skips the menu
and sips instantly, keeping the bare-carpet gesture one click; bare cells
are targetable at all because `canTargetLocations` is on. Spawned-storage
contents are deliberately *not* fed in as "apparel" — the cell scan already
lists them as things.

The stock `FloatMenu` would wreck that menu: it re-sorts by priority and
hard-ranks disabled options (our section headers) to the bottom, sinking the
headers into a block. The `options` field is protected, so a one-line
subclass (`FloatMenu_Ordered`) restores the caller's order after the base
constructor has done its sizing.

**Floors resolve through a three-step chain**: vanilla paint
(`TerrainGrid.ColorAt`), then Dubs Paint Shop's grids, then the terrain
def's own `DrawColor`. The Dubs read needs both of its getters, in order:
`GetColourAlpha` flags "painted" via a fourth alpha grid and can even
represent true-black paint — but Dubs' `ExposeData` scribes the RGB grids
*without* the alpha grid, so after any reload that getter returns clear for
every painted cell. Plain `GetColour` keys on the scribed RGB and survives
reload for everything except literal (0,0,0), which Dubs' own save format
makes indistinguishable from unpainted — a limitation Dubs itself shares,
and named "blacks" (`3C3C3C` and friends) read fine. Everything degrades
silently when Dubs is absent or its internals move.

## State across save, load and uninstall

- **Nothing of this mod's is scribed into saves.** Item colors are vanilla
  `CompColorable` state, written and read by the base game whether or not
  this mod is present — which is the whole clean-removal story: remove the
  mod and every painted item keeps its color, because the mod never owned
  that data in the first place.
- **Saved swatches are ModSettings** — a config file beside the game's own
  prefs, not save data. `Color` is a registered ParseHelper value type
  (`ParseHelper.cs:389`), so the list round-trips as plain values. Painted
  colors survive uninstalling; so do the swatches, invisibly, in case the
  mod comes back.
- **The harness trigger is a flag-gated MonoBehaviour, not a
  GameComponent**, and the distinction is a save-hygiene rule:
  `Game.ExposeData` deep-scribes its components list, so a GameComponent
  writes its class name into every save it touches, costing players a
  one-time load error after uninstalling. A dev-only feature has no
  business in anyone's save file. Without the launch flag, the trigger
  allocates nothing at all.
- **Session statics are process-lifetime UI state only**: the picker's
  remembered position and the cached palette list. Neither refers to a map,
  a save or a thing, so nothing needs resetting when the loaded game
  changes — the property that lets this mod skip the session-guard
  machinery its sibling mods need.

## Development tooling

The shape follows the sibling repos, and the reasoning lives with the code
that enforces it:

- **The scene builders never ship.** `SCENES` (defined on Debug and Media
  builds, never Release) compiles in the film-set stages, which clear
  multi-hundred-cell footprints without confirmation and leave
  player-faction buildings and colonists behind. Players demonstrably use
  the debug actions menu on finished mods; destructive fixtures do not
  belong in it.
- **The harness body ships in every configuration**, because the release
  gate must assert against the literal dll players install — and it has
  **no debug-menu entry at all**. The `-apparelpainter-harness` launch flag
  is the only door. See [TESTING.md](TESTING.md).
- **One TweakValue ships** (`PreviewWhileDragging`). The bar is
  destructiveness, not reachability: it moves a preview policy, resets at
  next launch, and exists so a feel report can be walked through live.
