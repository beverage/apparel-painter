#if SCENES
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApparelPainter
{
    /// <summary>
    /// SCENES-only: Dev mode → Debug actions → Apparel Painter → Build gif
    /// stage, then click the stage's south-west corner cell. Builds the
    /// whole footage set on a uniform steel-tile pad: a row of undyed
    /// outfit stands (the paint-me canvas), two Armor Racks, the sbz
    /// hanger + display shelves stocked with dusters, shirts and cloth
    /// stacks, two model pawns — one dressed in colour as a dropper
    /// source — and two carpet rugs as the dropper's floor targets.
    /// Everything faces the camera; the pad is unroofed so daylight does
    /// the lighting.
    ///
    /// DESTRUCTIVE by design (it clears its footprint outright) and never
    /// ships: the whole file compiles out of Release. Integration defs
    /// (sbz, Armor Racks) resolve by name and skip silently when absent,
    /// so the stage degrades to its vanilla pieces on the minimal list.
    /// Royalty apparel is deliberately absent — the scene modlist is
    /// Core+Odyssey, so the wardrobe is dusters, collar shirts and bowler
    /// hats (all Core, all cloth, all undyed until the camera rolls).
    /// </summary>
    internal static class DebugTools_GifStage
    {
        internal const int PadWidth = 22;
        internal const int PadDepth = 12;

        internal static readonly Color JaneDusterTeal = new Color(0.13f, 0.47f, 0.47f);
        internal static readonly Color JaneHatCream = new Color(0.93f, 0.88f, 0.72f);

        [DebugAction("Apparel Painter", "Build gif stage", false, false,
            actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        internal static void BuildGifStage()
        {
            Map map = Find.CurrentMap;
            IntVec3 origin = UI.MouseCell();
            CellRect pad = new CellRect(origin.x, origin.z, PadWidth, PadDepth);
            if (!pad.InBounds(map))
            {
                Messages.Message("Stage does not fit here.", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            GenDebug.ClearArea(pad, map);
            TerrainDef steelTile = DefDatabase<TerrainDef>.GetNamedSilentFail("MetalTile")
                ?? TerrainDefOf.PavedTile;
            foreach (IntVec3 cell in pad)
            {
                map.terrainGrid.SetTerrain(cell, steelTile);
                map.roofGrid.SetRoof(cell, null);
            }

            // -- the paint-me canvas: six identical undyed stands ---------
            ThingDef standDef = DefDatabase<ThingDef>.GetNamedSilentFail("Building_OutfitStand");
            if (standDef != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    IntVec3 cell = origin + new IntVec3(4 + i * 2, 0, 9);
                    Building_OutfitStand stand = (Building_OutfitStand)SpawnClean(map, standDef, null, cell, Rot4.South);
                    stand.AddApparel(Garment("Apparel_Duster"));
                    stand.AddApparel(Garment("Apparel_CollarShirt"));
                    stand.AddApparel(Garment("Apparel_BowlerHat"));
                }
            }

            // -- Armor Racks (skips without the mod) ----------------------
            ThingDef rackDef = DefDatabase<ThingDef>.GetNamedSilentFail("ArmorRacks_ArmorRack");
            for (int i = 0; rackDef != null && i < 2; i++)
            {
                IntVec3 cell = origin + new IntVec3(1, 0, 3 + i * 3);
                Thing rack = SpawnClean(map, rackDef, GenStuff.DefaultStuffFor(rackDef), cell, Rot4.East);
                ThingOwner held = (rack as IThingHolder)?.GetDirectlyHeldThings();
                if (held != null)
                {
                    held.TryAdd(Garment("Apparel_FlakVest"));
                    held.TryAdd(Garment("Apparel_AdvancedHelmet"));
                    held.TryAdd(Garment("Apparel_Pants"));
                }
            }

            // -- sbz shelves (skip without the mod); ASF def first --------
            Building_Storage hanger = SpawnStorage(map, origin + new IntVec3(16, 0, 8), Rot4.South,
                "sbz_WideHangerShelfASF", "sbz_WideHangerShelf");
            if (hanger != null)
            {
                FillStorage(hanger, Enumerable.Range(0, 8).Select(_ => (Thing)Garment("Apparel_Duster")));
            }
            Building_Storage display = SpawnStorage(map, origin + new IntVec3(16, 0, 4), Rot4.South,
                "sbz_DisplayShelfASF", "sbz_DisplayShelf");
            if (display != null)
            {
                List<Thing> stock = new List<Thing>();
                for (int i = 0; i < 4; i++)
                {
                    stock.Add(Garment("Apparel_CollarShirt"));
                }
                for (int i = 0; i < 2; i++)
                {
                    Thing cloth = ThingMaker.MakeThing(ThingDefOf.Cloth);
                    cloth.stackCount = 75;
                    stock.Add(cloth);
                }
                FillStorage(display, stock);
            }

            // -- models: one dressed in colour as a dropper source --------
            Pawn jane = ModelPawn(Gender.Female, "Jane");
            GenSpawn.Spawn(jane, origin + new IntVec3(7, 0, 2), map);
            Wear(jane, Garment("Apparel_Duster", JaneDusterTeal));
            Wear(jane, Garment("Apparel_BowlerHat", JaneHatCream));

            Pawn john = ModelPawn(Gender.Male, "John");
            GenSpawn.Spawn(john, origin + new IntVec3(11, 0, 2), map);
            Wear(john, Garment("Apparel_CollarShirt"));
            Wear(john, Garment("Apparel_Pants"));

            // -- rugs: born-coloured carpets, the dropper's floor beat ----
            Rug(map, origin + new IntVec3(4, 0, 0), "CarpetBurgundy");
            Rug(map, origin + new IntVec3(8, 0, 0), "CarpetGreenForest");

            Messages.Message("Gif stage built.", MessageTypeDefOf.TaskCompletion, historical: false);
        }

        /// <summary>Style-cleared spawn — every take reads identically
        /// (the shift-change fixture rule: no ideo restyles, no random
        /// graphic variants).</summary>
        internal static Thing SpawnClean(Map map, ThingDef def, ThingDef stuff, IntVec3 cell, Rot4 rot)
        {
            Thing thing = ThingMaker.MakeThing(def, stuff ?? GenStuff.DefaultStuffFor(def));
            thing.SetFactionDirect(Faction.OfPlayer);
            thing.SetStyleDef(null);
            thing.overrideGraphicIndex = 0;
            Thing spawned = GenSpawn.Spawn(thing, cell, map, rot);
            spawned.SetStyleDef(null);
            spawned.overrideGraphicIndex = 0;
            spawned.Notify_ColorChanged();
            return spawned;
        }

        internal static Building_Storage SpawnStorage(Map map, IntVec3 cell, Rot4 rot, params string[] defNames)
        {
            foreach (string name in defNames)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null)
                {
                    continue;
                }
                return SpawnClean(map, def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null, cell, rot) as Building_Storage;
            }
            return null;
        }

        /// <summary>Spawn stock into a storage building's own cells,
        /// round-robin so multi-cell shelves fill evenly.</summary>
        internal static void FillStorage(Building_Storage storage, IEnumerable<Thing> items)
        {
            List<IntVec3> cells = storage.OccupiedRect().Cells.ToList();
            int i = 0;
            foreach (Thing item in items)
            {
                GenSpawn.Spawn(item, cells[i % cells.Count], storage.Map);
                i++;
            }
        }

        /// <summary>Lay a 3x2 rug of a generated carpet def — "Carpet" +
        /// a structural ColorDef name minus its "Structure_" prefix
        /// (TerrainDefGenerator_Carpet's join). Born-coloured terrain, no
        /// paint call: the dropper reads it through ResolveFloorColor's
        /// unpainted branch, exactly what a player's built carpet is.
        /// Skips silently if the def set changes.</summary>
        internal static void Rug(Map map, IntVec3 sw, string carpetDefName)
        {
            TerrainDef carpet = DefDatabase<TerrainDef>.GetNamedSilentFail(carpetDefName);
            if (carpet == null)
            {
                return;
            }
            foreach (IntVec3 cell in new CellRect(sw.x, sw.z, 3, 2))
            {
                map.terrainGrid.SetTerrain(cell, carpet);
            }
        }

        /// <summary>Cloth-stuffed, style-cleared, and explicitly coloured —
        /// CompColorable.Initialize otherwise rolls the def's random
        /// clothing colours, and staged garments must read identically
        /// take after take. Null tint = the stuff's natural colour, i.e.
        /// the undyed paint-me state.</summary>
        internal static Apparel Garment(string defName, Color? tint = null)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed(defName);
            ThingDef stuff = null;
            if (def.MadeFromStuff)
            {
                stuff = def.stuffCategories != null && def.stuffCategories.Contains(StuffCategoryDefOf.Fabric)
                    ? ThingDefOf.Cloth
                    : GenStuff.DefaultStuffFor(def);
            }
            Apparel garment = (Apparel)ThingMaker.MakeThing(def, stuff);
            garment.SetStyleDef(null);
            garment.overrideGraphicIndex = 0;
            if (tint.HasValue)
            {
                ColorForcer.ForceSetColor(garment, tint.Value);
            }
            else
            {
                ColorForcer.ResetToNatural(garment);
            }
            return garment;
        }

        /// <summary>A plain 30-year-old baseliner with no traits and tidy
        /// hair, so nothing rolled hijacks a take (the sibling's
        /// AveragePawn, trimmed — no work requirements here).</summary>
        internal static Pawn ModelPawn(Gender gender, string nick)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(
                PawnKindDefOf.Colonist, Faction.OfPlayer,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false,
                fixedBiologicalAge: 30f, fixedChronologicalAge: 30f,
                fixedGender: gender);
            XenotypeDef baseliner = DefDatabase<XenotypeDef>.GetNamedSilentFail("Baseliner");
            if (baseliner != null)
            {
                request.ForcedXenotype = baseliner;
            }
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            pawn.Name = new NameTriple(gender == Gender.Female ? "Jane" : "John", nick, "Doe");
            pawn.story.traits.allTraits.Clear();
            pawn.story.HairColor = new Color(0.35f, 0.24f, 0.15f);
            pawn.story.bodyType = gender == Gender.Female ? BodyTypeDefOf.Female : BodyTypeDefOf.Male;
            if (pawn.style != null)
            {
                pawn.style.beardDef = DefDatabase<BeardDef>.GetNamedSilentFail("NoBeard");
                pawn.style.FaceTattoo = DefDatabase<TattooDef>.GetNamedSilentFail("NoTattoo_Face");
                pawn.style.BodyTattoo = DefDatabase<TattooDef>.GetNamedSilentFail("NoTattoo_Body");
            }
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            return pawn;
        }

        internal static void Wear(Pawn pawn, Apparel garment)
        {
            pawn.apparel?.Wear(garment, dropReplacedApparel: false);
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }
    }
}
#endif
