# Testing

How this mod is verified, and why the approach took the shape it did. For
build and debug, see [DEVELOPMENT.md](DEVELOPMENT.md); for what the code
interfaces with, see [DESIGN.md](DESIGN.md).

Engine claims were checked against the decompiled game assembly, version
**1.6.4871**.

## Constraints

| Constraint | Consequence |
|---|---|
| The engine cannot be mocked | Every claim this mod makes is about how RimWorld and four neighbour mods behave. A test double would assert the author's model of them, which is the thing most likely to be wrong. The suite runs **inside the game** and drives the engine's own entry points. |
| The mod will not move much; everything under it will | This mod is small and, once shipped, changes rarely. The game updates, and so do Armor Racks, ASF and Dubs. So the suite is built as a **tripwire field**: it pins every private member the reflection bridges and the layout mirror lean on, so an update breaks a named assertion in twenty seconds instead of a feature on somebody's shelf. |
| A test that is slow is not run | One command, no hands, roughly twenty seconds on the minimal list. Anything requiring a human to click through menus stops happening within a week. |
| It must not touch a live game | Runs against its own save-data folder and its own log, refuses to run beside an existing instance unless told to, and only ever waits on — or signals — a process it started. The machine this was written on is also the machine somebody plays on. |
| Some things stay out of reach | The dialog is exercised headless, so everything that needs a real window — input, dragging, the targeter loop — is named below rather than approximated. |

## Running it

```bash
devtools/run-harness.sh          # a three-mod list — the iteration loop
devtools/run-harness.sh --full   # your own mod list, copied — the release gate
devtools/run-harness.sh --alongside  # a second instance beside a live game
```

Builds Release, launches with `-quicktest -apparelpainter-harness`, waits
for the game to run every case and quit itself, prints the report, exits
non-zero on any failure. Measured 2026-08-23: the minimal list in ~25 s, a
large live list under `--full` in ~160 s — almost all of it RimWorld's own
load time.

There is deliberately **no debug-menu entry**: the launch flag is the only
door, on every build. A suite you can only reach by the same route the
release gate uses cannot drift away from it.

**Keep the window focused.** RimWorld is throttled hard in the background
and a backgrounded loading screen can stall outright; the script allows
1200 s before giving up, and a run that looks hung is usually starved, not
broken. Duration measured from an unfocused run means nothing.

**The minimal list is for iterating; `--full` is what a release is signed
off on.** The three integration cases (Dubs, Armor Racks, ASF) SKIP on the
minimal list by construction — the mods are not there — and become real
assertions only under `--full`. The first `--full` run also caught the game
having quietly moved to a new revision, and certified every reflection
surface against it the same day; that is the suite doing precisely its job.

**ASF is asserted at the framework, never per skin.** The tripwire pins
`StorageRenderer`'s public members, which every Adaptive Storage skin
inherits, so one assertion stands behind [sbz] Neat Storage, Reel's Expanded
Storage and the rest. Individual skins are confirmed by hand on the scene
list, which carries both at once: they do not collide at the def level
(checked 2026-09-02 — Neat Storage patches none of the five VE defs Reel's
rewrites), so the whole cost of loading them together is a Furniture
category crowded with near-identical buildings. Reel's confirmed 2026-09-02:
tab on the apparel locker, paint landing on the shelf without a reload.

## Isolation

`-savedatafolder` gives the test instance its own `ModsConfig.xml`, saves
and prefs under `dist/testdata`; Unity's `-logfile` moves `Player.log` there
too. Nothing under the live installation is written; the live mod list is
*read once* for `--full` and copied, never swapped.

Two guards exist because their failure modes are silent:

- **The load-path check.** The build is not the thing the game loads: the
  game reads `Mods/ApparelPainter`, and if that entry is a parked release
  copy rather than the checkout under test, a green run silently asserts
  against the wrong bits — the worst shape a test result can take. The
  script resolves the symlink and refuses to launch when it points anywhere
  but the repository it just built.
- **The copied list must activate the mod.** A copied `--full` list that
  predates the mod's rename, or simply has it disabled, boots the game
  without the mod under test. "The harness never ran" is the best case;
  the worst is a run that asserts nothing and looks green. The script
  migrates the old packageId or appends the current one — in the copy
  only; the live file is never touched.

The runner launches the game binary directly rather than through `open`, so
it has a real pid and only ever waits on that one. Another instance already
running is somebody's colony with unsaved progress: the default is to stop,
and `--alongside` is the deliberate opt-in.

## What the cases assert

Seventeen cases, six kinds. A case can carry several assertions; the
minimal list reports around 21 passing with 3 skips, and `--full` converts
the skips.

**Engine-member tripwires.** The private engine surface this mod leans on,
pinned by name: the stand's `RecacheGraphics` and its private graphic-cache
field, the picker base's `ColorReadback` (the layout mirror's anchor),
`FloatMenu`'s protected options list, `TerrainGrid.ColorAt`, and the two
vanilla textures the UI borrows. Each failure message says what to
re-verify, so a game update turns into a checklist instead of a bug hunt.

**Injection.** Every def whose class makes it a stand carries the Paint tab
in its resolved tab list — and under `--full`, every Armor Racks def does
too. This is the assertion that the startup walked the real, fully-modded
def database and missed nothing.

**Pure-logic tables.** The direct-input parser (hex, prefixed hex,
8-digit-with-alpha, byte and float triplets, and the strings that must be
rejected), the palette source, the layout mirror's internal consistency,
the canonical display sort, and the dropper menu's header ordering. No map
needed; these run in microseconds and fail with the offending input named.

**Comp warts.** Forcing pure white onto an undyed item must *activate* the
comp and land the color — the exact sequence `SetColor`'s early-out
sabotages — and a set-then-reset round trip must end genuinely inactive at
the natural color, not painted to look like it.

**The bake invariant.** The case the mod is organized around, in two halves
that control each other: after a raw color write, the stand's cached
graphic must **not** have followed it — proving the bake the mod exists to
work around is still there — and after `Recache`, it must. If the first
half ever fails, the engine has started refreshing on its own and the mod
should be recalibrated, not patched; the failure text says so.

**The headless picker.** The dialog is constructed and driven without ever
entering the WindowStack: preview must land on the real items, cancel must
restore a previously-natural item to *inactive* and a previously-painted
item to *its paint* — the two cases a single "restore color" write would
conflate — and accept must keep. This pins the snapshot model, not the
window.

**Floor resolution.** Vanilla-painted terrain resolves through
`TerrainGrid.ColorAt`; a Dubs-painted cell resolves live; and the Dubs
post-reload state is *simulated in place* — the alpha grid entry is zeroed,
because Dubs never scribes it, and the fallback getter must still see the
paint. That last assertion is the reload wart that cost a play session,
pinned forever.

**Integration tripwires.** Armor Racks' reflected fields and ASF's renderer
members, present and reachable. On the minimal list these SKIP, with the
reason and the command that would run them (`--full`) in the report.

## What is not covered

- **Everything visual.** No case draws a row, lands an overlay on the
  base's pixels, or opens a real window. The layout mirror's arithmetic is
  checked for consistency; whether it matches vanilla's *pixels* after a
  game update is the post-update eyeball ritual in
  [DESIGN.md](DESIGN.md#the-layout-mirror).
- **The real window lifecycle.** The picker cases run headless, so
  dragging, Esc ordering, position memory, and the close-the-old-picker
  path are verified in play only.
- **The map dropper's live loop.** The targeting parameters, the
  continuous re-arm, and the cell-stack menu assembly run only in play.
  The floor resolver and the menu-ordering primitive under them are
  pinned; the targeter flow itself is not.
- **The wearer path.** No case stages a picker target that walks away
  mid-dialog; the wearer-dirtying write is exercised in play only.
- **LWM Deep Storage.** No case; its storage conversion and the tab's
  placement beside its Inventory tab were verified in play (2026-08-29).
- **Save round trips.** Nothing of this mod's is in the save file, and
  vanilla owns `CompColorable` scribing; no case loads a save.
- **Mod compatibility beyond the loaded list.** `--full` proves the suite
  passes with one large list — whichever is active on the machine that ran
  it. It proves nothing about a mod that was not installed.

A green run is a floor, not a certificate. The dropper's cell-stack
resolution exists because of a bug that no assertion of the day would have
caught — two stands sharing cells with wall heaters — and it was found by
playing. Play observation is still required.

## Rules the suite follows

**Drive the engine's own entry points.** Cases call `GenSpawn.Spawn`, the
stand's real `AddApparel`, the real private cache through the same
reflection the mod uses, and Dubs' real map component — never a hand-rolled
imitation of any of them.

**Pair a negative with its control.** "The cache did not change" is
worthless alone — an unreadable cache satisfies it too. The bake case reads
the cache before and after, and its two halves guard each other: one proves
the problem still exists, the other proves the fix still works.

**A SKIP is not a PASS.** Skips are counted separately, reported with their
reason, and name the command that runs them. The release gate is `--full`
precisely because it turns all three into assertions.

**A throw must still quit the process.** The driver wraps the whole run;
an exception is logged and the game still shuts down, because the caller is
waiting on a pid, and a hang is a worse failure report than a red one.

**Leave the world as found.** Every fixture tears down its stand and
contents, restores the terrain color, the Dubs cell and the picker's
remembered position. Cases stay order-independent, and the map after a run
looks like the map before it.

**Traps paid for**, recorded so they stay paid: a `FloatMenu` under test
must never enter the WindowStack — closing an un-stacked window logs a
spurious removal error, so the ordering case reads the list without ever
showing the menu. And the `--full` mod-list copy must be checked for the
mod's own id — the rename aftershock above — because a harness that boots
without its subject reports exactly nothing, in green.

## Adding a case

Register it in `Harness.Run`. `Check(condition, name, detail)` asserts;
`Skip(name, why)` records a counted, reasoned skip — use it whenever the
case depends on a mod the minimal list does not carry, and say `--full` in
the reason. Fixtures: `MakeApparel` for garments, `SpawnStand` +
`Teardown` for a stand with guaranteed cleanup, and restore any static you
touch (`rememberedPosition` has a saved-and-restored discipline in the
dialog cases; follow it).

The standing rule, inherited from the sibling repo: anything that goes
wrong in play leaves a case behind that fails without its fix — and any new
integration adapter arrives with a tripwire case that SKIPs when its mod is
absent.
