# AGENTS.md

A RimWorld 1.6 mod: one C# assembly that adds a Paint tab to outfit stands,
for recolouring the apparel they hold. No art, no defs of its own beyond
keyed strings, no Harmony patches, no scribed state.

| Doc | Contents |
|---|---|
| [README.md](README.md) | what the mod does, for players |

## Verify your work

```bash
dotnet build Source/StandPainter/StandPainter.csproj -c Release
```

End every session with a Release build: Debug and Release write the same
output path and Debug carries the ECR hot-reload rig beside our dll. The
Release build sweeps those artifacts; only `Assemblies/StandPainter.dll` is
ever tracked.

There is no test suite. Behaviour is verified in game on a clean Release
restart; UI layout may be iterated through ECR hot-swap on a Debug build, but
anything touching colour state is confirmed after a restart. Do not claim a
behavioural change works without saying how it was checked.

## Invariants that will not announce themselves

**Every colour change to a held item must be followed by a reflection call to
the stand's private `RecacheGraphics()`.** The stand bakes `apparel.DrawColor`
into cached `Graphic`s at add/remove/spawn only; `Notify_ColorChanged` dirties
the apparel, which the stand never re-reads. Miss it and the stand renders the
old colour until reload — the bug will look like painting doesn't work at all.

**`CompColorable.SetColor` no-ops on exact white for undyed items.** The
comp's private colour field defaults to white while inactive, and `SetColor`
early-outs on equality without activating. Route every colour write through
the `ColorForcer` helper (lands with the picker slice); never call `SetColor`
directly.

**`SetColor` silently clears `DesiredColor`** — the vanilla dye-queue field.
Irrelevant while the mod is instant-only; a future dye mode must never
stage-then-instant-paint the same garment.

**ITab injection is runtime, class-keyed, and touches both lists.**
`StaticConstructorOnStartup` runs after def resolution, so appending to
`inspectorTabs` alone does nothing — `inspectorTabsResolved` is the live
list. Never move the tab into an XML patch: list nodes on a shared vanilla
def are a commons (two mods' Adds clobber each other; root DEC-032).

**Do not mint graphics per drag frame.** Every unique colour creates a
permanent `GraphicDatabase` entry plus materials, never evicted. Push live
preview on colour commit (wheel mouse-up, palette click, hex entry), not per
frame.

**Declare members `internal`, never `private`, and never write an
auto-property.** Hot-swapped method bodies execute in a separate assembly and
Unity's Mono honours only `InternalsVisibleTo`; a `private` member or a
backing field throws at runtime mid-iteration.

**Do not bind hotkeys on gizmos or rows.** On storage-carrying buildings N, J
and F are taken (settings copy, settings paste, forbid) and O is reserved.

**Weapons sit in the same container.** `HeldItems` includes a held weapon,
which generally has no `CompColorable` — null-guard every comp access, and
render the row swatch-less rather than hiding it.

## File map

| File | Job |
|---|---|
| `StandPainterStartup.cs` | class-keyed ITab injection onto every stand def |
| `ITab_StandPainter.cs` | the Paint tab |
