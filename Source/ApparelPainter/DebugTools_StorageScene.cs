#if SCENES
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApparelPainter
{
    /// <summary>
    /// SCENES-only: Dev mode → Debug actions → Apparel Painter → Build
    /// storage scene, then click the scene's south-west corner cell.
    ///
    /// The where-it-works shot, under the per-scene staging rule:
    /// VANILLA PIECES ONLY — one dressed outfit stand (Odyssey), one
    /// Core shelf, one Core small shelf — and the storage holds only
    /// what fits (vanilla shelves store three items per cell; the
    /// retired universal stage spilled eight dusters over three cells).
    /// Two stored garments are pre-tinted in the set's palette (Jane's
    /// teal, the rug's burgundy) so the shelf reads as "painted right
    /// here, in storage". The Paint tab is the star: the driver hops
    /// the selection across all three containers and the tab follows.
    /// </summary>
    internal static class DebugTools_StorageScene
    {
        internal const int PadWidth = 14;
        internal const int PadDepth = 8;

        internal static readonly Color Burgundy = new Color(0.36f, 0.16f, 0.18f);

        [DebugAction("Apparel Painter", "Build storage scene", false, false,
            actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        internal static void BuildStorageScene()
        {
            Map map = Find.CurrentMap;
            IntVec3 origin = UI.MouseCell();
            CellRect pad = new CellRect(origin.x, origin.z, PadWidth, PadDepth);
            if (!pad.InBounds(map))
            {
                Messages.Message("Storage scene does not fit here.",
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
                map, standDef, null, origin + new IntVec3(3, 0, 4), Rot4.South);
            stand.AddApparel(DebugTools_GifStage.Garment("Apparel_Duster"));
            stand.AddApparel(DebugTools_GifStage.Garment("Apparel_CollarShirt"));
            stand.AddApparel(DebugTools_GifStage.Garment("Apparel_BowlerHat"));

            // The 2x1 shelf: teal duster + natural shirt on one cell, the
            // burgundy duster on the other — three items, capacity is six.
            // Items spawn into the shelf's OWN OccupiedRect: multi-cell
            // buildings place by centre, so raw origin-relative cells miss
            // the real footprint (the first take left the burgundy duster
            // on the floor beside the shelf — honestly excluded from the
            // tab, but exactly the improper storage this scene must not
            // show).
            ThingDef shelfDef = DefDatabase<ThingDef>.GetNamed("Shelf");
            Building shelf = (Building)DebugTools_GifStage.SpawnClean(
                map, shelfDef, ThingDefOf.WoodLog,
                origin + new IntVec3(6, 0, 4), Rot4.South);
            System.Collections.Generic.List<IntVec3> shelfCells =
                System.Linq.Enumerable.ToList(shelf.OccupiedRect().Cells);
            GenSpawn.Spawn(DebugTools_GifStage.Garment("Apparel_Duster",
                DebugTools_GifStage.JaneDusterTeal), shelfCells[0], map);
            GenSpawn.Spawn(DebugTools_GifStage.Garment("Apparel_CollarShirt"),
                shelfCells[0], map);
            GenSpawn.Spawn(DebugTools_GifStage.Garment("Apparel_Duster", Burgundy),
                shelfCells[shelfCells.Count - 1], map);

            // The 1x1 small shelf: one natural bowler hat, tucked two tiles
            // below the 2x1 and flush with its RIGHT edge (principal's
            // layout, 2026-08-28 — vanilla storage is exactly these two
            // shelves, so the row folds into an L and the frame narrows).
            // The edge comes from the spawned shelf's own rect: multi-cell
            // buildings place by centre, so hardcoded cells drift.
            IntVec3 shelfRight = shelfCells[shelfCells.Count - 1];
            ThingDef smallDef = DefDatabase<ThingDef>.GetNamed("ShelfSmall");
            Building small = (Building)DebugTools_GifStage.SpawnClean(
                map, smallDef, ThingDefOf.WoodLog,
                new IntVec3(shelfRight.x, 0, shelfRight.z - 2), Rot4.South);
            GenSpawn.Spawn(DebugTools_GifStage.Garment("Apparel_BowlerHat"),
                small.Position, map);

            Messages.Message(
                "Storage scene built: one stand, one shelf (3 of 6 slots), one small shelf.",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }
    }
}
#endif
