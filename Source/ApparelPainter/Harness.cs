using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApparelPainter
{
    /// <summary>
    /// Boots the regression harness when the game is launched with
    /// <c>-apparelpainter-harness</c> (devtools/run-harness.sh drives the whole
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
        internal const string Arg = "apparelpainter-harness";

        static HarnessBoot()
        {
            if (!GenCommandLine.CommandLineArgPassed(Arg))
            {
                return;
            }
            GameObject driver = new GameObject("ApparelPainterHarnessDriver");
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
                Log.Error("[ApparelPainter] harness threw: " + e);
            }
            Log.Message("[ApparelPainter] harness auto-run: " + (passed ? "PASSED" : "FAILED"));
            Root.Shutdown();
        }

        internal static bool Run(Map map)
        {
            Report.Length = 0;
            Passed = 0;
            Failed = 0;
            Skipped = 0;
            Report.AppendLine("[ApparelPainter] regression harness");

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
            CaseStorageAdapter(map);
            CaseRackAdapter();
            CaseAsfInterop();
            CaseDisplaySort();
            CaseStyleIndex();
            CaseStyleWrite(map);
            CaseStylePrecept();
            CaseStyleMenu();
            CaseStyleOverrideLabel();

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

        /// <summary>Texture path of the first cached body-apparel graphic —
        /// the style twin of CachedGraphicColor, since a style swaps the
        /// worn art rather than tinting it.</summary>
        /// <summary>BOTH cache lists, body first. The stand keeps headgear
        /// in its own list, and most styled apparel in the game is helmets
        /// — reading only the body list returned null for every helmet
        /// fixture and read as "cache unreadable" rather than "wrong
        /// list".</summary>
        internal static string CachedGraphicPath(Building_OutfitStand stand)
        {
            foreach (string fieldName in new[] { "cachedApparelGraphicsNonHeadgear", "cachedApparelGraphicsHeadgear" })
            {
                FieldInfo listField = typeof(Building_OutfitStand).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                IList list = listField?.GetValue(stand) as IList;
                if (list == null || list.Count == 0)
                {
                    continue;
                }
                object entry = list[0];
                FieldInfo graphicField = entry.GetType().GetField("graphic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                string path = (graphicField?.GetValue(entry) as Graphic)?.path;
                if (path != null)
                {
                    return path;
                }
            }
            return null;
        }

        /// <summary>An apparel def the index has styles for, whose first
        /// style swaps the worn art — so the stand's cached graphic path
        /// actually moves. Null when the loaded modlist ships no styles.</summary>
        internal static ThingDef StyledApparelDef()
        {
            StyleIndex.EnsureBuilt();
            foreach (KeyValuePair<ThingDef, List<StyleOption>> pair in StyleIndex.byDef)
            {
                if (pair.Key.IsApparel && pair.Value.Count > 0
                    && !pair.Value[0].Style.wornGraphicPath.NullOrEmpty())
                {
                    return pair.Key;
                }
            }
            return null;
        }

        // ---- cases ------------------------------------------------------

        /// <summary>Tripwires for "the game moved": every private engine
        /// member our reflection bridges and layout mirror lean on.</summary>
        internal static void CaseEngineMembers()
        {
            Check(StandGraphics.recacheMethod != null, "engine.RecacheGraphics", "Building_OutfitStand.RecacheGraphics gone");
            Check(typeof(Building_OutfitStand).GetField("cachedApparelGraphicsHeadgear", BindingFlags.Instance | BindingFlags.NonPublic) != null,
                "engine.graphicCacheHeadgearField", "Building_OutfitStand.cachedApparelGraphicsHeadgear gone");
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
                if (def.inspectorTabsResolved == null || !def.inspectorTabsResolved.Any(t => t is ITab_ApparelPainter))
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

        /// <summary>The generic Building_Storage door: a shelf
        /// with a shirt spawned in its cell lists the shirt, shows the tab,
        /// and hides it again once the apparel is gone.</summary>
        internal static void CaseStorageAdapter(Map map)
        {
            ThingDef shelfDef = DefDatabase<ThingDef>.GetNamedSilentFail("Shelf");
            if (shelfDef == null)
            {
                Check(false, "storage.adapter", "no Shelf def");
                return;
            }
            Predicate<IntVec3> usable = c => c.InBounds(map) && c.Standable(map) && !c.GetTerrain(map).IsWater;
            IntVec3 origin = map.Center;
            if (!usable(origin) && !CellFinderLoose.TryGetRandomCellWith(usable, map, 500, out origin))
            {
                Check(false, "storage.adapter", "no cell for the shelf fixture");
                return;
            }
            Thing shelf = GenSpawn.Spawn(ThingMaker.MakeThing(shelfDef, GenStuff.DefaultStuffFor(shelfDef)), origin, map);
            Apparel shirt = null;
            try
            {
                ContainerAdapter adapter = ContainerAdapter.For(shelf);
                bool isStorage = adapter is StorageAdapter;
                bool hiddenEmpty = adapter != null && !adapter.TabVisible(shelf);
                shirt = MakeApparel("Apparel_CollarShirt");
                GenSpawn.Spawn(shirt, shelf.Position, map);
                bool listed = false;
                if (adapter != null)
                {
                    foreach (Thing t in adapter.ListedItems(shelf))
                    {
                        if (t == shirt)
                        {
                            listed = true;
                        }
                    }
                }
                bool visibleFull = adapter != null && adapter.TabVisible(shelf);
                Check(isStorage && hiddenEmpty && listed && visibleFull, "storage.adapter",
                    $"storage={isStorage} hiddenEmpty={hiddenEmpty} listed={listed} visible={visibleFull}");
            }
            finally
            {
                if (shirt != null && !shirt.Destroyed)
                {
                    shirt.Destroy();
                }
                if (!shelf.Destroyed)
                {
                    shelf.Destroy();
                }
            }
        }

        /// <summary>Armor Racks integration tripwires — the reflected
        /// public fields the adapter's refresh depends on. SKIPs when the
        /// mod is not in the list; --full covers it.</summary>
        internal static void CaseRackAdapter()
        {
            if (ArmorRackAdapter.RackType == null)
            {
                Skip("rack.adapter", "Armor Racks not loaded (minimal list) — run --full to cover it");
                return;
            }
            bool fields = ArmorRackAdapter.DrawerField != null && ArmorRackAdapter.ResolvedField != null;
            int rackDefs = 0;
            int missing = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.thingClass == null || !ArmorRackAdapter.RackType.IsAssignableFrom(def.thingClass))
                {
                    continue;
                }
                rackDefs++;
                if (def.inspectorTabsResolved == null || !def.inspectorTabsResolved.Any(t => t is ITab_ApparelPainter))
                {
                    missing++;
                }
            }
            Check(fields && rackDefs > 0 && missing == 0, "rack.adapter",
                $"fields={fields} rackDefs={rackDefs} missingTabs={missing} — Armor Racks internals moved?");
        }

        /// <summary>ASF render-cache tripwires: painted items on ASF-family
        /// storage (sbz Neat Storage, Reel's) stay visually stale unless
        /// AsfInterop can reach Renderer.SetAllPrintDatasDirty. SKIPs when
        /// ASF is not in the list; --full covers it.</summary>
        internal static void CaseAsfInterop()
        {
            AsfInterop.EnsureInit();
            if (AsfInterop.thingClassType == null)
            {
                Skip("asf.interop", "Adaptive Storage Framework not loaded (minimal list) — run --full to cover it");
                return;
            }
            bool members = AsfInterop.rendererGetter != null && AsfInterop.setDirtyMethod != null;
            int asfDefs = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.thingClass != null && AsfInterop.thingClassType.IsAssignableFrom(def.thingClass))
                {
                    asfDefs++;
                }
            }
            Check(members && asfDefs > 0, "asf.interop",
                $"members={members} asfDefs={asfDefs} — ASF internals moved? Painted ASF storage will look stale.");
        }

        /// <summary>The canonical display sort: label groups, quality
        /// descends, condition descends — identical on every family.</summary>
        /// <summary>
        /// The index the style control reads. Two things can rot
        /// without an error: the engine's style plumbing moving, and a
        /// modlist producing a menu label that is really a defName —
        /// the failure the naming rules exist to prevent.
        /// </summary>
        internal static void CaseStyleIndex()
        {
            StyleIndex.EnsureBuilt();
            if (StyleIndex.byDef.Count == 0)
            {
                Skip("style.index", "no styles in this modlist (no Ideology/Anomaly/Royalty)");
                return;
            }
            string leaked = null;
            string duplicated = null;
            foreach (KeyValuePair<ThingDef, List<StyleOption>> pair in StyleIndex.byDef)
            {
                List<StyleOption> options = pair.Value;
                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i].Label.NullOrEmpty() || options[i].Label.Contains("_"))
                    {
                        leaked = pair.Key.defName + "/" + options[i].Style.defName + " → '" + options[i].Label + "'";
                    }
                    for (int j = i + 1; j < options.Count; j++)
                    {
                        if (options[i].Label == options[j].Label)
                        {
                            duplicated = pair.Key.defName + " → '" + options[i].Label + "' twice";
                        }
                    }
                }
            }
            Check(leaked == null, "style.index.labels", "raw defName reached a menu label: " + leaked);
            Check(duplicated == null, "style.index.distinct", "two options share a label: " + duplicated);
        }

        /// <summary>
        /// The style write end to end, on a real stand. The claim under
        /// test is that SetStyleDef ALONE shows nothing — the stand bakes
        /// worn art exactly as it bakes colour — and that our write plus
        /// the adapter's Refresh moves it. Same shape as
        /// CaseRecacheInvariant, because it is the same invariant.
        /// </summary>
        internal static void CaseStyleWrite(Map map)
        {
            ThingDef def = StyledApparelDef();
            if (def == null)
            {
                Skip("style.write", "no styled apparel def in this modlist");
                return;
            }
            Building_OutfitStand stand = SpawnStand(map, out _);
            if (stand == null)
            {
                Check(false, "style.write", "could not spawn a stand fixture");
                return;
            }
            try
            {
                ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                Apparel garment = (Apparel)ThingMaker.MakeThing(def, stuff);
                // Start from no-style deliberately: MakeThing rolls
                // randomStyle at PostMake, so a randomStyle def can arrive
                // pre-styled and the write below would be a no-op. Latent
                // flake, not a hypothetical — it depends on which def the
                // index yields first.
                StyleForcer.SetStyle(garment, null);
                stand.AddApparel(garment);
                string baked = CachedGraphicPath(stand);
                if (baked == null)
                {
                    Check(false, "style.write", "graphic cache unreadable after add");
                    return;
                }

                ThingStyleDef target = StyleIndex.For(def)[0].Style;
                bool wrote = StyleForcer.SetStyle(garment, target);
                string afterWrite = CachedGraphicPath(stand);
                bool bakeProven = wrote && afterWrite == baked
                    && garment.WornGraphicPath == target.wornGraphicPath;
                ContainerAdapter.For(stand).Refresh(stand);
                string afterRefresh = CachedGraphicPath(stand);
                bool refreshWorks = afterRefresh != null && afterRefresh != baked;

                Check(bakeProven, "style.cacheBakes",
                    "cache followed the style WITHOUT a refresh, or the write did not land — invariant gone, recalibrate");
                Check(refreshWorks, "style.refreshRedraws", "cache after refresh = " + afterRefresh);

                // Cycle wraps through "no style" and lands back where it
                // started, so the control can never strand an item on a
                // style the player cannot click their way out of.
                StyleForcer.SetStyle(garment, null);
                int steps = StyleIndex.For(def).Count + 1;
                for (int i = 0; i < steps; i++)
                {
                    StyleForcer.SetStyle(garment, StyleForcer.NextInCycle(garment, 1));
                }
                Check(garment.StyleDef == null, "style.cycleWraps",
                    "a full cycle did not return to no-style: " + garment.StyleDef?.defName);

                Check(!StyleForcer.CanRestyle(ThingMaker.MakeThing(ThingDefOf.Steel)),
                    "style.guardsUnstyleable", "offered a style control on a thing with no CompStyleable");
            }
            finally
            {
                Teardown(stand);
            }
        }

        /// <summary>
        /// The guard AGENTS.md calls the single gate: an item whose style
        /// comes from a PRECEPT is off limits, because
        /// CompStyleable.SourcePrecept re-derives styleDef from the precept
        /// and writing underneath leaves the pair disagreeing.
        ///
        /// The fixture has to be a def with randomStyleChance > 0, and that
        /// is not incidental: for every other def the SourcePrecept setter
        /// dereferences sourcePrecept.ideo, which a bare precept does not
        /// have. Vanilla's randomStyle apparel (prestige marine helmet,
        /// cultist masks) short-circuits that branch, so the guard can be
        /// tested without standing up an ideoligion.
        /// </summary>
        internal static void CaseStylePrecept()
        {
            StyleIndex.EnsureBuilt();
            ThingDef def = null;
            foreach (KeyValuePair<ThingDef, List<StyleOption>> pair in StyleIndex.byDef)
            {
                if (pair.Key.IsApparel && pair.Key.randomStyleChance > 0f && pair.Value.Count > 0)
                {
                    def = pair.Key;
                    break;
                }
            }
            if (def == null)
            {
                Skip("style.guardsPrecept", "no randomStyle apparel def (no Royalty/Anomaly)");
                return;
            }

            Thing item = ThingMaker.MakeThing(def);
            bool offeredBefore = StyleForcer.CanRestyle(item);
            item.StyleSourcePrecept = new Precept_ThingStyle();
            bool offeredAfter = StyleForcer.CanRestyle(item);
            bool writeRefused = !StyleForcer.SetStyle(item, StyleIndex.For(def)[0].Style)
                && item.StyleDef == null;

            Check(offeredBefore && !offeredAfter && writeRefused, "style.guardsPrecept",
                $"{def.defName}: offered before={offeredBefore} after={offeredAfter}, "
                + $"write refused={writeRefused}");
        }

        /// <summary>
        /// The menu is authored, not sorted: "no style" leads and the rest
        /// follow the index's cycle order, so the menu and the click-cycle
        /// never disagree about what comes next.
        /// </summary>
        internal static void CaseStyleMenu()
        {
            ThingDef def = StyledApparelDef();
            if (def == null)
            {
                Skip("style.menuOrder", "no styled apparel def in this modlist");
                return;
            }
            Thing item = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            List<StyleOption> options = StyleIndex.For(def);
            List<FloatMenuOption> menu = ITab_ApparelPainter.StyleMenuOptions(null, null, item);

            bool sized = menu.Count == options.Count + 1;
            bool leads = sized && menu[0].Label == "ApparelPainter_NoStyle".Translate().ToString();
            bool ordered = sized;
            for (int i = 0; sized && i < options.Count; i++)
            {
                if (menu[i + 1].Label != options[i].Label)
                {
                    ordered = false;
                }
            }
            Check(sized && leads && ordered, "style.menuOrder",
                $"{def.defName}: {menu.Count} options for {options.Count} styles, "
                + $"first={(menu.Count > 0 ? menu[0].Label : "-")}");
        }

        /// <summary>
        /// A style can RENAME its item — `overrideLabel` feeds GenLabel, so
        /// the tab row and every other label change with it. Vanilla sets it
        /// exactly once (PrestigeMarineHelmet_Samurai → "samurai helmet"),
        /// and both the style gif's closing beat and the store copy claim
        /// this, so it is asserted rather than assumed.
        /// </summary>
        internal static void CaseStyleOverrideLabel()
        {
            StyleIndex.EnsureBuilt();
            ThingDef def = null;
            ThingStyleDef renaming = null;
            foreach (KeyValuePair<ThingDef, List<StyleOption>> pair in StyleIndex.byDef)
            {
                if (!pair.Key.IsApparel)
                {
                    continue;
                }
                foreach (StyleOption option in pair.Value)
                {
                    if (!option.Style.overrideLabel.NullOrEmpty())
                    {
                        def = pair.Key;
                        renaming = option.Style;
                        break;
                    }
                }
                if (def != null)
                {
                    break;
                }
            }
            if (def == null)
            {
                Skip("style.overrideLabel", "no apparel style sets overrideLabel in this modlist");
                return;
            }

            Thing item = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            // MakeThing ROLLS randomStyle at PostMake (Verse/Thing.cs:790),
            // so a prestige marine helmet arrives already samurai half the
            // time and this case would measure a rename that had already
            // happened — which is exactly how it failed first time.
            StyleForcer.SetStyle(item, null);
            string before = item.LabelCap;
            bool wrote = StyleForcer.SetStyle(item, renaming);
            string after = item.LabelCap;
            bool renamed = wrote && after != before
                && after.ToLower().Contains(renaming.overrideLabel.ToLower());
            Check(renamed, "style.overrideLabel",
                $"{renaming.defName}: '{before}' → '{after}', expected to contain "
                + $"'{renaming.overrideLabel}'");
        }

        internal static void CaseDisplaySort()
        {
            Apparel shirtNormal = MakeApparel("Apparel_CollarShirt");
            Apparel shirtExcellent = MakeApparel("Apparel_CollarShirt");
            Apparel duster = MakeApparel("Apparel_Duster");
            shirtNormal.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
            shirtExcellent.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Colony);
            List<Thing> list = new List<Thing> { duster, shirtNormal, shirtExcellent };
            list.Sort(ITab_ApparelPainter.CompareForDisplay);
            bool ordered = list[0] == shirtExcellent && list[1] == shirtNormal && list[2] == duster;
            Check(ordered, "display.sort",
                "expected excellent shirt, normal shirt, duster; got " + string.Join(", ", list.Select(t => t.Label)));
            shirtNormal.Destroy();
            shirtExcellent.Destroy();
            duster.Destroy();
        }
    }
}
