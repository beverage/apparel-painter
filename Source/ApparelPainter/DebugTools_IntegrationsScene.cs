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
    /// SCENES-only: Dev mode → Debug actions → Apparel Painter → Build
    /// integrations scene, then click the scene's south-west corner cell.
    ///
    /// The modded-storage counterpart of <see cref="DebugTools_StorageScene"/>,
    /// under the same per-scene staging rule — integration mods appear only
    /// in shots about them, and this is that shot: one sbz Neat Storage
    /// hanger shelf (ASF-rendered, "holds up to 8 pieces of clothing")
    /// carrying eight dusters painted through a full rainbow, and one LWM
    /// Deep Storage clothing rack with a light Core fill. Both resolve by
    /// defName and are called out LOUDLY in the build message when their
    /// mod is missing — a silent degrade here would waste a take on camera.
    ///
    /// The fills are LEGAL storage, not spawn spam: the hanger holds exactly
    /// its advertised 8 (4 per cell), and the clothing rack's load sits far
    /// under LWM's own 2.5 kg/cell cap (the rack is rated "10 t-shirts per
    /// slot"; three 0.3 kg shirts and a bowler split over two cells).
    /// </summary>
    internal static class DebugTools_IntegrationsScene
    {
        internal const int PadWidth = 14;
        internal const int PadDepth = 8;

        /// <summary>Eight hues, red to magenta, one per hanger slot. All sit
        /// above the tint-multiply luminance floor the wardrobe stage
        /// measured — a dark tint crushes the texture's tonal range.</summary>
        internal static readonly Color[] Rainbow =
        {
            new Color(0.80f, 0.20f, 0.18f),   // red
            new Color(0.88f, 0.52f, 0.16f),   // orange
            new Color(0.90f, 0.82f, 0.28f),   // yellow
            new Color(0.30f, 0.68f, 0.30f),   // green
            new Color(0.18f, 0.65f, 0.62f),   // teal
            new Color(0.28f, 0.44f, 0.82f),   // blue
            new Color(0.56f, 0.34f, 0.76f),   // violet
            new Color(0.82f, 0.38f, 0.62f),   // magenta
        };

        [DebugAction("Apparel Painter", "Build integrations scene", false, false,
            actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        internal static void BuildIntegrationsScene()
        {
            Map map = Find.CurrentMap;
            IntVec3 origin = UI.MouseCell();
            CellRect pad = new CellRect(origin.x, origin.z, PadWidth, PadDepth);
            if (!pad.InBounds(map))
            {
                Messages.Message("Integrations scene does not fit here.",
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

            List<string> skipped = new List<string>();

            // -- sbz hanger shelf: the advertised 8, one hue per slot ------
            // Centre-placed like every multi-cell building: a spawn at +3
            // occupies +2..+3. Cells fill four-then-four in ascending x so
            // the rack reads as one red-to-magenta sweep — ASF's
            // itemGraphics columns index FOOTPRINT CELLS, so a round-robin
            // fill would interleave the hues across the two halves.
            Building_Storage hanger = DebugTools_GifStage.SpawnStorage(
                map, origin + new IntVec3(3, 0, 4), Rot4.South,
                "sbz_WideHangerShelfASF", "sbz_WideHangerShelf");
            if (hanger != null)
            {
                DebugTools_GifStage.Allow(hanger, "Apparel_Duster");
                List<IntVec3> cells = hanger.OccupiedRect().Cells
                    .OrderBy(c => c.x).ToList();
                for (int i = 0; i < Rainbow.Length; i++)
                {
                    GenSpawn.Spawn(
                        DebugTools_GifStage.Garment("Apparel_Duster", Rainbow[i]),
                        cells[i / 4], map);
                }
            }
            else
            {
                skipped.Add("sbz hanger shelf (sbz Neat Storage not loaded)");
            }

            // -- LWM clothing rack: a light, legal Core fill --------------
            // One shirt wears the set's teal so the Paint tab rows show a
            // painted item among naturals — LWM draws its own art over
            // stored items, so the TAB is where this half's story lives.
            Building_Storage rack = DebugTools_GifStage.SpawnStorage(
                map, origin + new IntVec3(6, 0, 4), Rot4.South,
                "LWM_Clothing_Rack");
            if (rack != null)
            {
                DebugTools_GifStage.Allow(rack, "Apparel_CollarShirt", "Apparel_BowlerHat");
                List<IntVec3> cells = rack.OccupiedRect().Cells
                    .OrderBy(c => c.x).ToList();
                GenSpawn.Spawn(DebugTools_GifStage.Garment(
                        "Apparel_CollarShirt", DebugTools_GifStage.JaneDusterTeal),
                    cells[0], map);
                GenSpawn.Spawn(DebugTools_GifStage.Garment("Apparel_CollarShirt"),
                    cells[0], map);
                GenSpawn.Spawn(DebugTools_GifStage.Garment("Apparel_CollarShirt"),
                    cells[cells.Count - 1], map);
                GenSpawn.Spawn(DebugTools_GifStage.Garment("Apparel_BowlerHat"),
                    cells[cells.Count - 1], map);
            }
            else
            {
                skipped.Add("LWM clothing rack (LWM's Deep Storage not loaded)");
            }

            // -- Armor Rack: the third adapter family on camera -----------
            // 1x1, filled through IThingHolder like the gif stage's racks —
            // no storage settings exist on this family. The armor set is
            // the principal's pick (2026-08-30): duster, flak jacket, flak
            // helmet. Placement is the where-it-works L idiom (principal,
            // same day): two tiles below the row, flush with the LWM
            // rack's right edge, FACING SOUTH — the three-in-a-line first
            // take left the frame's lower-middle gap empty and showed the
            // rack's side profile. Tab-order note: this family's contents
            // tab is labelled "Rack" and the strip reads
            // Rack | Paint | Storage — verified on camera 2026-08-30.
            ThingDef rackDef = DefDatabase<ThingDef>.GetNamedSilentFail("ArmorRacks_ArmorRack");
            if (rackDef != null)
            {
                Thing armor = DebugTools_GifStage.SpawnClean(
                    map, rackDef, GenStuff.DefaultStuffFor(rackDef),
                    origin + new IntVec3(6, 0, 2), Rot4.South);
                ThingOwner held = (armor as IThingHolder)?.GetDirectlyHeldThings();
                if (held != null)
                {
                    held.TryAdd(DebugTools_GifStage.Garment("Apparel_Duster"));
                    held.TryAdd(DebugTools_GifStage.Garment("Apparel_FlakJacket"));
                    held.TryAdd(DebugTools_GifStage.Garment("Apparel_AdvancedHelmet"));
                }
            }
            else
            {
                skipped.Add("armor rack (Armor Racks not loaded)");
            }

            Messages.Message(
                skipped.Count == 0
                    ? "Integrations scene built: rainbow hanger shelf (8) + LWM clothing rack + armor rack."
                    : "Integrations scene INCOMPLETE — skipped: " + string.Join("; ", skipped),
                skipped.Count == 0 ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput,
                historical: false);
        }
    }
}
#endif
