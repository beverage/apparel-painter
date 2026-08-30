#if SCENES
using LudeonTK;
using RimWorld;
using Verse;

namespace ApparelPainter
{
    /// <summary>
    /// SCENES-only: Dev mode → Debug actions → Apparel Painter → Build
    /// dropper scene, then click the scene's south-west corner cell.
    ///
    /// PER-SCENE MINIMAL STAGING (principal's direction, 2026-08-28,
    /// after the dropper gif was first shot on the universal gif stage):
    /// a scene stages EXACTLY what its shot needs, positioned so the
    /// world pieces and the fixed UI panels — tab lower-left, picker
    /// centre — sit adjacent on screen and the final frame crops tight.
    /// No universal stage, no set dressing, and VANILLA-ONLY pieces in
    /// main shots; integration mods appear only in shots about them.
    /// Future storage scenes must configure storage that actually holds
    /// its fill — the universal stage's shelves overflowed onto the
    /// floor because eight dusters were spawned at three cells.
    ///
    /// THIS scene is one dressed stand, Jane (the worn-apparel dropper
    /// source, teal duster), and one burgundy rug (the floor source) in
    /// a 4x4 cluster. The driver's camera puts the cluster LEFT of the
    /// picker and ABOVE the tab, filling the screen zone every earlier
    /// take wasted as empty steel.
    /// </summary>
    internal static class DebugTools_DropperScene
    {
        internal const int PadWidth = 10;
        internal const int PadDepth = 8;

        [DebugAction("Apparel Painter", "Build dropper scene", false, false,
            actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        internal static void BuildDropperScene()
        {
            Map map = Find.CurrentMap;
            IntVec3 origin = UI.MouseCell();
            CellRect pad = new CellRect(origin.x, origin.z, PadWidth, PadDepth);
            if (!pad.InBounds(map))
            {
                Messages.Message("Dropper scene does not fit here.",
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

            GenDebug.ClearArea(pad, map);
            TerrainDef steelTile = DefDatabase<TerrainDef>.GetNamedSilentFail("MetalTile")
                ?? TerrainDefOf.PavedTile;
            foreach (IntVec3 cell in pad)
            {
                map.terrainGrid.SetTerrain(cell, steelTile);
                map.roofGrid.SetRoof(cell, null);
            }

            Building_OutfitStand stand = (Building_OutfitStand)DebugTools_GifStage.SpawnClean(
                map, standDef, null, origin + new IntVec3(3, 0, 5), Rot4.South);
            stand.AddApparel(DebugTools_GifStage.Garment("Apparel_Duster"));
            stand.AddApparel(DebugTools_GifStage.Garment("Apparel_CollarShirt"));
            stand.AddApparel(DebugTools_GifStage.Garment("Apparel_BowlerHat"));

            Pawn jane = DebugTools_GifStage.ModelPawn(Gender.Female, "Jane");
            GenSpawn.Spawn(jane, origin + new IntVec3(6, 0, 5), map);
            DebugTools_GifStage.Wear(jane,
                DebugTools_GifStage.Garment("Apparel_Duster", DebugTools_GifStage.JaneDusterTeal));
            DebugTools_GifStage.Wear(jane,
                DebugTools_GifStage.Garment("Apparel_BowlerHat", DebugTools_GifStage.JaneHatCream));
            // Face the camera: she spawned facing north in the first cut,
            // showing the duster's featureless back — which read as another
            // outfit stand, not a worn pawn. The paused clock keeps the
            // facing she is given here.
            jane.Rotation = Rot4.South;
            jane.Drawer?.renderer?.SetAllGraphicsDirty();

            DebugTools_GifStage.Rug(map, origin + new IntVec3(5, 0, 2), "CarpetBurgundy");

            Messages.Message("Dropper scene built: one dressed stand, Jane, one rug.",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }
    }
}
#endif
