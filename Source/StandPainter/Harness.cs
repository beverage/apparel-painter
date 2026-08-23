using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace StandPainter
{
    /// <summary>
    /// Boots the regression harness when the game is launched with
    /// <c>-standpainter-harness</c> (devtools/run-harness.sh drives the whole
    /// loop: isolated save-data folder, wait on OUR pid, grep the report).
    ///
    /// The trigger is a flag-gated MonoBehaviour rather than shift-change's
    /// Harmony postfix — this mod ships no Harmony — and deliberately NOT a
    /// GameComponent: components are scribed into every save
    /// (Game.ExposeData, LookMode.Deep), and a dev-only feature has no
    /// business appearing in a player's save file. Without the flag, nothing
    /// here allocates so much as a GameObject.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class HarnessBoot
    {
        internal const string Arg = "standpainter-harness";

        static HarnessBoot()
        {
            if (!GenCommandLine.CommandLineArgPassed(Arg))
            {
                return;
            }
            GameObject driver = new GameObject("StandPainterHarnessDriver");
            UnityEngine.Object.DontDestroyOnLoad(driver);
            driver.AddComponent<HarnessDriver>();
        }
    }

    /// <summary>Waits for the quicktest map to finish loading, runs the
    /// harness once, and quits the process pass or fail — the caller waits on
    /// the pid and reads the log, never a screen.</summary>
    internal class HarnessDriver : MonoBehaviour
    {
        internal bool started;

        public void Update()
        {
            if (started)
            {
                return;
            }
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
            {
                return;
            }
            if (LongEventHandler.AnyEventNowOrWaiting)
            {
                return;
            }
            started = true;
            Harness.RunAndQuit();
        }
    }

    /// <summary>
    /// The regression harness. The mod's behaviour is not expected to move —
    /// but the game underneath it and the mods it integrates with are, so
    /// every engine surface this mod leans on is asserted here: the private
    /// members the reflection bridges and the layout mirror depend on, the
    /// comp warts ColorForcer exists to hide, the stand's graphic-cache bake,
    /// the picker's preview/cancel/accept state machine (headless — the
    /// dialog never enters the WindowStack), floor colour resolution across
    /// vanilla paint and Dubs Paint Shop, and FloatMenu ordering.
    ///
    /// The one rule that keeps it honest, inherited from shift-change's
    /// harness: call the engine's own entry points (GenSpawn.Spawn,
    /// ThingOwner adds, the real RecacheGraphics) and the mod's own public
    /// surface — never a hand-rolled imitation of either.
    ///
    /// This BODY ships in every configuration on purpose: run-harness.sh
    /// builds plain Release, so the gate asserts against the literal dll
    /// players install. There is no debug-menu entry at all — the launch
    /// flag is the only door.
    /// </summary>
    internal static class Harness
    {
        internal static readonly StringBuilder Report = new StringBuilder();
        internal static int Passed;
        internal static int Failed;
        internal static int Skipped;

        internal static void RunAndQuit()
        {
            bool passed = false;
            try
            {
                passed = Run(Find.CurrentMap);
            }
            catch (Exception e)
            {
                // Never let a throw leave the process alive: an automated
                // caller waiting on exit would hang forever.
                Log.Error("[StandPainter] harness threw: " + e);
            }
            Log.Message("[StandPainter] harness auto-run: " + (passed ? "PASSED" : "FAILED"));
            Root.Shutdown();
        }

        internal static bool Run(Map map)
        {
            Report.Length = 0;
            Passed = 0;
            Failed = 0;
            Skipped = 0;
            Report.AppendLine("[StandPainter] regression harness");

            CaseEngineMembers();
            CaseInjection();
            CaseParseTable();
            CasePalette();
            CaseMirrorConsistency();
            CaseWhiteWart();
            CaseForceAndReset();
            CaseRecacheInvariant(map);
            CaseDialogPreviewCancel(map);
            CaseDialogAccept(map);
            CaseFloorVanillaPaint(map);
            CaseFloorDubs(map);
            CaseMenuOrder();

            Report.AppendLine($"result: {Passed} passed, {Failed} failed, {Skipped} skipped");
            Log.Message(Report.ToString());
            return Failed == 0;
        }

        internal static void Check(bool condition, string name, string detail)
        {
            if (condition)
            {
                Passed++;
                Report.AppendLine("  PASS  " + name);
            }
            else
            {
                Failed++;
                Report.AppendLine("  FAIL  " + name + " — " + detail);
            }
        }

        internal static void Skip(string name, string why)
        {
            Skipped++;
            Report.AppendLine("  SKIP  " + name + " — " + why);
        }

        // ---- fixtures ---------------------------------------------------

        internal static ThingDef StandDef => DefDatabase<ThingDef>.GetNamedSilentFail("Building_OutfitStand");

        internal static Apparel MakeApparel(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed(defName);
            ThingDef stuff = def.MadeFromStuff ? DefDatabase<ThingDef>.GetNamed("Synthread") : null;
            return (Apparel)ThingMaker.MakeThing(def, stuff);
        }

        internal static Building_OutfitStand SpawnStand(Map map, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            ThingDef def = StandDef;
            if (def == null)
            {
                return null;
            }
            Predicate<IntVec3> usable = c => c.InBounds(map) && c.Standable(map) && !c.GetTerrain(map).IsWater;
            IntVec3 origin = map.Center;
            if (!usable(origin) && !CellFinderLoose.TryGetRandomCellWith(usable, map, 500, out origin))
            {
                return null;
            }
            cell = origin;
            Thing thing = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
            return (Building_OutfitStand)GenSpawn.Spawn(thing, origin, map);
        }

        internal static void Teardown(Building_OutfitStand stand)
        {
            if (stand == null || stand.Destroyed)
            {
                return;
            }
            // Destroy contents first so DeSpawn does not scatter them.
            List<Thing> held = stand.HeldItems.ToList();
            foreach (Thing t in held)
            {
                stand.RemoveApparel(t as Apparel);
                if (!t.Destroyed)
                {
                    t.Destroy();
                }
            }
            stand.Destroy();
        }

        /// <summary>Colour of the first cached body-apparel graphic the stand
        /// would draw, via the same private cache RecacheGraphics rebuilds.
        /// Null when the cache is empty or the engine moved (the dependent
        /// cases then fail loudly, which is the point).</summary>
        internal static Color? CachedGraphicColor(Building_OutfitStand stand)
        {
            FieldInfo listField = typeof(Building_OutfitStand).GetField("cachedApparelGraphicsNonHeadgear", BindingFlags.Instance | BindingFlags.NonPublic);
            if (listField == null)
            {
                return null;
            }
            IList list = listField.GetValue(stand) as IList;
            if (list == null || list.Count == 0)
            {
                return null;
            }
            object entry = list[0];
            FieldInfo graphicField = entry.GetType().GetField("graphic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Graphic graphic = graphicField?.GetValue(entry) as Graphic;
            if (graphic == null)
            {
                return null;
            }
            return graphic.color;
        }

        // ---- cases ------------------------------------------------------

        /// <summary>Tripwires for "the game moved": every private engine
        /// member our reflection bridges and layout mirror lean on.</summary>
        internal static void CaseEngineMembers()
        {
            Check(StandGraphics.recacheMethod != null, "engine.RecacheGraphics", "Building_OutfitStand.RecacheGraphics gone");
            Check(typeof(Building_OutfitStand).GetField("cachedApparelGraphicsNonHeadgear", BindingFlags.Instance | BindingFlags.NonPublic) != null,
                "engine.graphicCacheField", "cachedApparelGraphicsNonHeadgear gone — cache assertions blind");
            Check(typeof(Dialog_ColorPickerBase).GetMethod("ColorReadback", BindingFlags.Static | BindingFlags.NonPublic) != null,
                "engine.ColorReadback", "readback layout moved — re-verify the Old colour dropper");
            Check(typeof(FloatMenu).GetField("options", BindingFlags.Instance | BindingFlags.NonPublic) != null,
                "engine.FloatMenu.options", "options field moved — FloatMenu_Ordered dead");
            Check(typeof(TerrainGrid).GetMethod("ColorAt", new[] { typeof(IntVec3) }) != null,
                "engine.TerrainGrid.ColorAt", "terrain paint read moved");
            Check(TexButton.DragHash != null && TexButton.Plus != null, "engine.textures", "DragHash/Plus textures gone");
        }

        internal static void CaseInjection()
        {
            int stands = 0;
            int missing = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.thingClass == null || !typeof(Building_OutfitStand).IsAssignableFrom(def.thingClass))
                {
                    continue;
                }
                stands++;
                if (def.inspectorTabsResolved == null || !def.inspectorTabsResolved.Any(t => t is ITab_StandPainter))
                {
                    missing++;
                }
            }
            Check(stands > 0 && missing == 0, "injection.tabs",
                $"{stands} stand defs, {missing} without the Paint tab");
        }

        internal static void CaseParseTable()
        {
            bool ok = true;
            string bad = "";
            void Expect(string input, bool shouldParse, Color? expected = null)
            {
                bool parsed = Dialog_StandColorPicker.TryParseColorInput(input, out Color got);
                bool pass = parsed == shouldParse && (!parsed || expected == null || got.IndistinguishableFrom(expected.Value));
                if (!pass)
                {
                    ok = false;
                    bad += " [" + input + "]";
                }
            }
            Expect("EFD8AE", true, new Color(0xEF / 255f, 0xD8 / 255f, 0xAE / 255f));
            Expect("#EFD8AE", true, new Color(0xEF / 255f, 0xD8 / 255f, 0xAE / 255f));
            Expect("EFD8AEFF", true, new Color(0xEF / 255f, 0xD8 / 255f, 0xAE / 255f));
            Expect("237,216,174", true, new Color(237f / 255f, 216f / 255f, 174f / 255f));
            Expect("237, 216, 174", true, new Color(237f / 255f, 216f / 255f, 174f / 255f));
            Expect("0.5,0.5,1", true, new Color(0.5f, 0.5f, 1f));
            Expect("237,216,174,255", true, new Color(237f / 255f, 216f / 255f, 174f / 255f));
            Expect("1,1,1", true, Color.white);
            Expect("256,0,0", false);
            Expect("EFD8A", false);
            Expect("hello", false);
            Expect("", false);
            Check(ok, "parse.table", "unexpected results for" + bad);
        }

        internal static void CasePalette()
        {
            List<Color> palette = Dialog_StandColorPicker.Palette();
            Check(palette.Count > 0, "palette.nonEmpty", "no Structure ColorDefs loaded");
        }

        internal static void CaseMirrorConsistency()
        {
            float baseWidth = 600f - 36f;
            Dialog_StandColorPicker.MirroredLayout m = Dialog_StandColorPicker.MirrorBaseLayout(baseWidth);
            bool heights = m.requiredHeight > m.readbackTopOffset + m.readbackHeight
                && m.readbackTopOffset > m.blockTopOffset + m.blockHeight;
            bool wheel = m.wheelCenterXOffset > 125f && m.wheelCenterXOffset < baseWidth - 250f;
            Check(heights && wheel, "mirror.consistency",
                $"heights={heights} wheelX={m.wheelCenterXOffset:F0}");
        }

        internal static void CaseWhiteWart()
        {
            Apparel shirt = MakeApparel("Apparel_CollarShirt");
            CompColorable comp = shirt.TryGetComp<CompColorable>();
            if (comp == null)
            {
                Check(false, "forcer.whiteWart", "shirt has no CompColorable");
                shirt.Destroy();
                return;
            }
            if (comp.Active)
            {
                comp.Disable();
            }
            ColorForcer.ForceSetColor(shirt, Color.white);
            Check(comp.Active && comp.Color.IndistinguishableFrom(Color.white),
                "forcer.whiteWart", $"active={comp.Active} colour={comp.Color}");
            shirt.Destroy();
        }

        internal static void CaseForceAndReset()
        {
            Apparel shirt = MakeApparel("Apparel_CollarShirt");
            Color natural = ColorForcer.NaturalColorOf(shirt);
            ColorForcer.ForceSetColor(shirt, Color.red);
            bool setOk = shirt.DrawColor.IndistinguishableFrom(Color.red);
            ColorForcer.ResetToNatural(shirt);
            CompColorable comp = shirt.TryGetComp<CompColorable>();
            bool resetOk = comp != null && !comp.Active && shirt.DrawColor.IndistinguishableFrom(natural);
            Check(setOk && resetOk, "forcer.setAndReset",
                $"set={setOk} reset={resetOk} draw={shirt.DrawColor} natural={natural}");
            shirt.Destroy();
        }

        /// <summary>THE invariant the mod exists around: the stand bakes
        /// apparel colour into a private graphic cache that only
        /// RecacheGraphics rebuilds.</summary>
        internal static void CaseRecacheInvariant(Map map)
        {
            Building_OutfitStand stand = SpawnStand(map, out _);
            if (stand == null)
            {
                Check(false, "stand.recache", "could not spawn a stand fixture");
                return;
            }
            try
            {
                Apparel shirt = MakeApparel("Apparel_CollarShirt");
                ColorForcer.ResetToNatural(shirt);
                stand.AddApparel(shirt);
                Color? baked = CachedGraphicColor(stand);
                if (baked == null)
                {
                    Check(false, "stand.recache", "graphic cache unreadable after add");
                    return;
                }
                ColorForcer.ForceSetColor(shirt, Color.green);
                Color? afterPaint = CachedGraphicColor(stand);
                bool bakeProven = afterPaint != null && afterPaint.Value.IndistinguishableFrom(baked.Value)
                    && !afterPaint.Value.IndistinguishableFrom(Color.green);
                StandGraphics.Recache(stand);
                Color? afterRecache = CachedGraphicColor(stand);
                bool recacheWorks = afterRecache != null && afterRecache.Value.IndistinguishableFrom(Color.green);
                Check(bakeProven, "stand.cacheBakes", "cache followed the colour WITHOUT a recache — invariant gone, recalibrate");
                Check(recacheWorks, "stand.recacheRefreshes", $"cache after recache = {afterRecache}");
            }
            finally
            {
                Teardown(stand);
            }
        }

        /// <summary>The picker's state machine, headless — the dialog never
        /// enters the WindowStack. Preview pushes to the real items; cancel
        /// restores the snapshot both for a previously-natural item (back to
        /// inactive) and a previously-painted one (back to its paint).</summary>
        internal static void CaseDialogPreviewCancel(Map map)
        {
            Vector2? savedPos = Dialog_StandColorPicker.rememberedPosition;
            Building_OutfitStand stand = SpawnStand(map, out _);
            if (stand == null)
            {
                Check(false, "dialog.previewCancel", "could not spawn a stand fixture");
                return;
            }
            try
            {
                Apparel shirt = MakeApparel("Apparel_CollarShirt");
                ColorForcer.ResetToNatural(shirt);
                Color shirtNatural = ColorForcer.NaturalColorOf(shirt);
                Apparel duster = MakeApparel("Apparel_Duster");
                ColorForcer.ForceSetColor(duster, Color.blue);
                stand.AddApparel(shirt);
                stand.AddApparel(duster);

                Dialog_StandColorPicker dialog = new Dialog_StandColorPicker(stand, new List<Thing> { shirt, duster });
                dialog.AdoptColor(Color.red);
                dialog.PushPreview();
                bool previewed = shirt.DrawColor.IndistinguishableFrom(Color.red)
                    && duster.DrawColor.IndistinguishableFrom(Color.red);
                dialog.PostClose();
                CompColorable shirtComp = shirt.TryGetComp<CompColorable>();
                bool shirtReverted = shirtComp != null && !shirtComp.Active
                    && shirt.DrawColor.IndistinguishableFrom(shirtNatural);
                bool dusterReverted = duster.DrawColor.IndistinguishableFrom(Color.blue);
                Check(previewed, "dialog.preview", $"shirt={shirt.DrawColor} duster={duster.DrawColor}");
                Check(shirtReverted && dusterReverted, "dialog.cancelRevert",
                    $"shirtActive={shirtComp?.Active} shirt={shirt.DrawColor} duster={duster.DrawColor}");
            }
            finally
            {
                Teardown(stand);
                Dialog_StandColorPicker.rememberedPosition = savedPos;
            }
        }

        internal static void CaseDialogAccept(Map map)
        {
            Vector2? savedPos = Dialog_StandColorPicker.rememberedPosition;
            Building_OutfitStand stand = SpawnStand(map, out _);
            if (stand == null)
            {
                Check(false, "dialog.accept", "could not spawn a stand fixture");
                return;
            }
            try
            {
                Apparel shirt = MakeApparel("Apparel_CollarShirt");
                stand.AddApparel(shirt);
                Dialog_StandColorPicker dialog = new Dialog_StandColorPicker(stand, new List<Thing> { shirt });
                dialog.AdoptColor(Color.green);
                dialog.AcceptForTest(Color.green);
                dialog.PostClose();
                Check(shirt.DrawColor.IndistinguishableFrom(Color.green), "dialog.acceptKeeps",
                    $"shirt={shirt.DrawColor}");
            }
            finally
            {
                Teardown(stand);
                Dialog_StandColorPicker.rememberedPosition = savedPos;
            }
        }

        internal static void CaseFloorVanillaPaint(Map map)
        {
            IntVec3 cell = map.Center;
            ColorDef structure = DefDatabase<ColorDef>.AllDefsListForReading.FirstOrDefault(d => d.colorType == ColorType.Structure);
            if (structure == null)
            {
                Check(false, "floor.vanillaPaint", "no Structure ColorDef to paint with");
                return;
            }
            ColorDef before = map.terrainGrid.ColorAt(cell);
            try
            {
                map.terrainGrid.SetTerrainColor(cell, structure);
                Color resolved = Dialog_StandColorPicker.ResolveFloorColor(map, cell, out bool painted);
                Check(painted && resolved.IndistinguishableFrom(structure.color), "floor.vanillaPaint",
                    $"painted={painted} colour={resolved} expected={structure.color}");
            }
            finally
            {
                map.terrainGrid.SetTerrainColor(cell, before);
            }
        }

        /// <summary>Dubs Paint Shop integration, including the reload wart:
        /// Dubs never scribes its alpha grid, so the fallback GetColour path
        /// must carry painted floors after a load. Skipped (not failed) when
        /// Dubs is not in the mod list.</summary>
        internal static void CaseFloorDubs(Map map)
        {
            DubsInterop.EnsureInit();
            if (DubsInterop.componentType == null)
            {
                Skip("floor.dubs", "Dubs Paint Shop not loaded (minimal list) — run --full to cover it");
                return;
            }
            MapComponent comp = map.GetComponent(DubsInterop.componentType);
            if (comp == null)
            {
                Check(false, "floor.dubs", "component type resolved but map has no instance");
                return;
            }
            IntVec3 cell = map.Center;
            Color teal = new Color(0.1f, 0.6f, 0.6f);
            MethodInfo setColour = DubsInterop.componentType.GetMethod("SetColour", new[] { typeof(Color), typeof(IntVec3) });
            FieldInfo aField = DubsInterop.componentType.GetField("A");
            if (setColour == null || aField == null)
            {
                Check(false, "floor.dubs", "Dubs internals moved (SetColour/A) — re-verify DubsInterop");
                return;
            }
            try
            {
                setColour.Invoke(comp, new object[] { teal, cell });
                bool live = DubsInterop.TryGetFloorColor(map, cell, out Color liveColor) && liveColor.IndistinguishableFrom(teal);
                // Simulate the post-reload state: zero the alpha grid entry
                // (Dubs' ExposeData never saves it) and require the fallback
                // path to still see the paint.
                object aGrid = aField.GetValue(comp);
                MethodInfo setDepth = aGrid.GetType().GetMethod("SetDepth", new[] { typeof(IntVec3), typeof(float) });
                setDepth.Invoke(aGrid, new object[] { cell, 0f });
                bool reloaded = DubsInterop.TryGetFloorColor(map, cell, out Color reloadColor) && reloadColor.IndistinguishableFrom(teal);
                Check(live, "floor.dubsLive", $"live read failed: {liveColor}");
                Check(reloaded, "floor.dubsReload", "GetColour fallback lost the paint once alpha was gone");
            }
            finally
            {
                setColour.Invoke(comp, new object[] { Color.clear, cell });
            }
        }

        internal static void CaseMenuOrder()
        {
            List<FloatMenuOption> built = new List<FloatMenuOption>
            {
                new FloatMenuOption("header-a", null),
                new FloatMenuOption("entry-1", delegate { }),
                new FloatMenuOption("header-b", null),
                new FloatMenuOption("entry-2", delegate { }),
            };
            FloatMenu_Ordered menu = new FloatMenu_Ordered(built);
            List<FloatMenuOption> shown = menu.OptionsForTest;
            bool ordered = shown.Count == 4
                && shown[0].Label == "header-a" && shown[1].Label == "entry-1"
                && shown[2].Label == "header-b" && shown[3].Label == "entry-2";
            // Never added to the WindowStack, so there is nothing to Close —
            // closing an un-stacked window logs a spurious removal error.
            Check(ordered, "menu.order", "headers did not stay in place: " + string.Join(", ", shown.Select(o => o.Label)));
        }
    }
}
