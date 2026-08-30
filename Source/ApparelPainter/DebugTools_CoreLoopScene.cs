#if SCENES
using LudeonTK;
using RimWorld;
using Verse;

namespace ApparelPainter
{
    /// <summary>
    /// SCENES-only: Dev mode → Debug actions → Apparel Painter → Build
    /// core loop scene, then click the scene's south-west corner cell.
    ///
    /// The core-loop demo's minimal set, v3 (2026-08-29, principal): two
    /// formal stands parked in the dropper scene's proven screen zone —
    /// cluster LEFT of the picker, ABOVE the tab — so the final frame
    /// crops tight and the UI keeps ~1.5x the size of the original
    /// wardrobe-wall take.
    ///
    /// BOTH STANDS BUILD UNDYED — the whole wardrobe is the paint-me
    /// canvas, and every colour lands ON CAMERA off the saved band: the
    /// demo suits the men's stand piece by piece (shirt dress white,
    /// vest and top hat black tie — the vest carries the jacket read),
    /// then Paint all floods the women's outfit scarlet — shirt
    /// included, which is what bulk painting honestly does — and the
    /// per-item beat rescues her shirt back to dress white. End state:
    /// black suit, red dress, two white shirts.
    ///
    /// EVERY GARMENT ON CAMERA IS VANILLA+DLC (Royalty): the VAE suit
    /// jacket was cut 2026-08-29 — principal: hero shots must not
    /// depend on third-party apparel. Royalty is required, exactly
    /// like the wardrobe stage.
    /// </summary>
    internal static class DebugTools_CoreLoopScene
    {
        internal const int PadWidth = 10;
        internal const int PadDepth = 8;

        [DebugAction("Apparel Painter", "Build core loop scene", false, false,
            actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        internal static void BuildCoreLoopScene()
        {
            Map map = Find.CurrentMap;
            IntVec3 origin = UI.MouseCell();
            CellRect pad = new CellRect(origin.x, origin.z, PadWidth, PadDepth);
            if (!pad.InBounds(map))
            {
                Messages.Message("Core loop scene does not fit here.",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            ThingDef standDef = DefDatabase<ThingDef>.GetNamedSilentFail("Building_OutfitStand");
            if (standDef == null)
            {
                Messages.Message("No outfit stand def — Odyssey is required.",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            if (DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_ShirtRuffle") == null)
            {
                Messages.Message("No Royalty formal wear — enable Royalty for this scene.",
                    MessageTypeDefOf.RejectInput, historical: false);
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

            // Wall spacing (2 apart), both south-facing, dressed through
            // the wardrobe stage's own builders so the rigs match the
            // other assets garment for garment.
            Building_OutfitStand stand1 = (Building_OutfitStand)DebugTools_GifStage.SpawnClean(
                map, standDef, null, origin + new IntVec3(3, 0, 5), Rot4.South);
            DebugTools_WardrobeStage.Dress(stand1, "Apparel_ShirtRuffle", DebugTools_WardrobeStage.Inner);
            DebugTools_WardrobeStage.Dress(stand1, "Apparel_VestRoyal", DebugTools_WardrobeStage.Inner);
            DebugTools_WardrobeStage.Dress(stand1, "Apparel_HatTop", DebugTools_WardrobeStage.Outer);

            Building_OutfitStand stand2 = (Building_OutfitStand)DebugTools_GifStage.SpawnClean(
                map, standDef, null, origin + new IntVec3(5, 0, 5), Rot4.South);
            DebugTools_WardrobeStage.Dress(stand2, "Apparel_ShirtRuffle", DebugTools_WardrobeStage.Inner);
            DebugTools_WardrobeStage.Dress(stand2, "Apparel_Corset", DebugTools_WardrobeStage.Inner);
            DebugTools_WardrobeStage.Dress(stand2, "Apparel_RobeRoyal", DebugTools_WardrobeStage.Outer);
            DebugTools_WardrobeStage.Dress(stand2, "Apparel_HatLadies", DebugTools_WardrobeStage.Outer);

            Messages.Message("Core loop scene built: two formal stands, undyed.",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }
    }
}
#endif
