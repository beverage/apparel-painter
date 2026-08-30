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
    /// wardrobe stage, then click the stage's south-west corner cell.
    /// A quiet sibling to <see cref="DebugTools_GifStage"/>: one steel-tile
    /// pad, one row of eight undyed outfit stands, and nothing else. No
    /// racks, no shelves, no model pawns, no cloth stacks.
    ///
    /// WHY A SECOND STAGE rather than a flag on the first: the gif stage
    /// exists to show the mod's whole surface at once, so it is deliberately
    /// busy. This one is for framed stills and stepped sequences of the
    /// stands alone, where anything else in shot is a distraction. They
    /// share the pad idiom and <see cref="DebugTools_GifStage.SpawnClean"/>;
    /// they do not share a footprint.
    ///
    /// THE WARDROBE mirrors the principal's throne-room changing room:
    /// every stand carries a formal shirt, the men add a vest and a top
    /// hat, the women a corset, a ladies hat and a prestige robe. Every
    /// garment is Royalty — the VAE suit jacket was CUT 2026-08-29
    /// (principal: hero shots must not depend on third-party apparel);
    /// the black-tie vest carries the jacket read.
    ///
    /// TWO STUFFS, ON PURPOSE. Everything is undyed, so each garment shows
    /// its material's natural colour and nothing else — that is the "before"
    /// the paired shot argues from. One stuff would make the row a flat
    /// wash; two make the point that an unpainted wardrobe is whatever the
    /// bill happened to use. Synthread for the layers worn against the body,
    /// alpaca wool for hats and robes.
    ///
    /// ALTERNATING men and women, where the real room groups them in two
    /// columns. Alternating means ANY adjacent pair is one of each, so the
    /// two-stand shot is just a two-cell frame at the left end and the
    /// eight-stand shot is the same row widened. One fixture, both shots,
    /// no second build.
    ///
    /// ROYALTY IS REQUIRED for the formal garments (they live in
    /// Data/Royalty/Defs/ThingDefs_Misc/Apparel_Royal.xml). The gif stage
    /// degrades to Core wear when an integration is missing; this one has
    /// nothing to degrade to, so it says so and builds nothing.
    ///
    /// DESTRUCTIVE by design — it clears its footprint outright — and never
    /// ships: the whole file compiles out of Release.
    /// </summary>
    internal static class DebugTools_WardrobeStage
    {
        // Big enough that a zoomed-out frame contains ONLY stage. At rootSize
        // 24 the camera sees roughly 79x51 cells, so anything smaller than that
        // leaves biome at the edges and the shot stops looking deliberate.
        // Clearing is O(cells) and this is a debug action, so the cost is a
        // one-off second rather than anything that matters.
        internal const int PadWidth = 96;
        internal const int PadDepth = 64;
        internal const int StandCount = 8;

        /// <summary>Shirt, vest, corset.</summary>
        internal const string Inner = "Synthread";

        /// <summary>Outer and headwear: hats, robe.</summary>
        internal const string Outer = "WoolAlpaca";

        // The principal's throne-room palette, carried over from Shift Change's
        // rec room stage so a shot taken here matches the live colony.
        //
        // BLACK TIE IS 22% LIGHTNESS, NOT 13%, and that number is measured
        // rather than chosen: apparel tint MULTIPLIES the texture, so the
        // surviving tonal range is proportional to the tint's own luminance.
        // At 13% the vest kept a 43-level spread where a scarlet robe kept 167,
        // and its shadow tones had merged into the pawn outline. Do not
        // "correct" it darker.
        internal static readonly Color BlackTie = new Color(0.22f, 0.22f, 0.27f);
        internal static readonly Color DressWhite = new Color(0.95f, 0.94f, 0.92f);

        /// <summary>RETIRED from the wardrobe rig with the VAE jacket
        /// (2026-08-29): the men's vest now commits BlackTie, since with
        /// no jacket over it the vest IS the suit read. Kept as the
        /// palette's grey — the core-loop saved band still carries its
        /// hex (5C5C69).</summary>
        internal static readonly Color WaistcoatGrey = new Color(0.36f, 0.36f, 0.41f);

        /// <summary>One frock per woman, in row order, matching the live room.</summary>
        internal static readonly Color[] Frocks =
        {
            new Color(0.72f, 0.09f, 0.15f),   // scarlet
            new Color(0.85f, 0.68f, 0.24f),   // gold
            new Color(0.07f, 0.44f, 0.29f),   // emerald
            new Color(0.15f, 0.29f, 0.63f),   // sapphire
        };

        /// <summary>
        /// The stand carrying a combat kit, in row order — index 2 puts it at
        /// `first + 4`, the third and last stand inside the 7-tile preview
        /// window, so the flip reads formal / formal / field rather than four
        /// variations on a suit.
        /// </summary>
        internal const int SoldierIndex = 2;

        // Real service colours, and the kit is coherent the way an issued one
        // is: GREEN uniform, BROWN armour. Coyote brown has been the plate
        // carrier and helmet cover standard since roughly 2010, which is why
        // the vest and helmet share it — cover and carrier match in the field.
        //
        // All three clear the BlackTie luminance floor documented above (33-43%
        // against its 22%), so the multiply keeps its tonal range. Do not
        // darken them toward "tactical black": the texture detail goes with it.
        internal static readonly Color CoyoteBrown = new Color(0.506f, 0.380f, 0.235f);
        internal static readonly Color RangerGreen = new Color(0.420f, 0.439f, 0.361f);
        internal static readonly Color OliveDrab = new Color(0.353f, 0.384f, 0.216f);

        /// <summary>Tan 499 — the issued combat-shirt colour worn UNDER a
        /// coyote carrier, which is why the soldier's button-down takes it
        /// rather than another green. Bright enough (70% luminance) that the
        /// multiply keeps the weave.</summary>
        internal static readonly Color Tan499 = new Color(0.765f, 0.690f, 0.569f);

        /// <summary>
        /// Row placement inside the pad, CENTRED. On a pad this size a row
        /// pinned near one corner leaves the stands against an edge, which is
        /// the thing a big pad exists to avoid: centred, every zoom level has
        /// stage on all four sides of the subject.
        /// </summary>
        internal const int RowX = (PadWidth - (StandCount * 2 - 1)) / 2;

        internal const int RowZ = PadDepth / 2;

        /// <summary>
        /// The reference pair sits three cells south of the row, far enough to
        /// fall outside any frame drawn around the stands. The capture path
        /// crops by cell rect, so anything outside the requested rect is simply
        /// not in the picture and the source never needs cleaning up between
        /// the before and after shots.
        /// </summary>
        internal const int RefZ = RowZ - 3;

        [DebugAction("Apparel Painter", "Build wardrobe stage", false, false,
            actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        internal static void BuildWardrobeStage()
        {
            Map map = Find.CurrentMap;
            IntVec3 origin = UI.MouseCell();
            CellRect pad = new CellRect(origin.x, origin.z, PadWidth, PadDepth);
            if (!pad.InBounds(map))
            {
                Messages.Message("Wardrobe stage does not fit here.",
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
                Messages.Message("No Royalty formal wear — enable Royalty for this stage.",
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            GenDebug.ClearArea(pad, map);
            // Kill the fog of war for the WHOLE map, not just the pad:
            // reveal spreads from wherever the quicktest colonists happened
            // to spawn, and an unlucky world leaves half a frame in
            // dimmed-unseen shading (caught on camera 2026-08-30 — the
            // armor rack sat in the falloff). Every driver builds this
            // stage for its ambient field, so this is the one place to fix
            // all shoots at once.
            map.fogGrid.ClearAllFog();
            TerrainDef steelTile = DefDatabase<TerrainDef>.GetNamedSilentFail("MetalTile")
                ?? TerrainDefOf.PavedTile;
            foreach (IntVec3 cell in pad)
            {
                map.terrainGrid.SetTerrain(cell, steelTile);
                map.roofGrid.SetRoof(cell, null);
            }

            PinLighting(map);

            // Two cells apart so each stand reads as its own silhouette at
            // the zoom a card is framed at, and so a two-cell frame at the
            // left end holds exactly one man and one woman.
            for (int i = 0; i < StandCount; i++)
            {
                IntVec3 cell = origin + new IntVec3(RowX + i * 2, 0, RowZ);
                Building_OutfitStand stand = (Building_OutfitStand)
                    DebugTools_GifStage.SpawnClean(map, standDef, null, cell, Rot4.South);

                if (i == SoldierIndex)
                {
                    // THE FLAK HELMET'S defName IS Apparel_AdvancedHelmet.
                    // Label and defName disagree — "flak helmet" ships under
                    // the advanced name — so a defName search for
                    // Apparel_FlakHelmet finds nothing and reads as "vanilla
                    // has no flak helmet", which is wrong. SEARCH HEADGEAR BY
                    // <label>. It is Metallic-only, hence steel.
                    //
                    // The vest and pants take no stuff at all: no
                    // stuffCategories, and their cloth is a fixed costList
                    // ingredient (Cloth 30 + Steel 60) rather than a material
                    // choice, so the name passed here is inert — Garment skips
                    // the stuff lookup whenever MadeFromStuff is false. The
                    // duster is the one genuinely stuffable piece, and
                    // devilstrand is a Fabric, so that part is real.
                    // Button-down under the armour: vanilla has no tunic, and
                    // Apparel_CollarShirt IS the button-down shirt (Fabric,
                    // OnSkin), so it sits under the vest the way a combat shirt
                    // does. Checked by <label> after the helmet lesson.
                    Dress(stand, "Apparel_CollarShirt", "Cloth");
                    Dress(stand, "Apparel_FlakPants", "Cloth");
                    Dress(stand, "Apparel_FlakVest", "Cloth");
                    Dress(stand, "Apparel_AdvancedHelmet", "Steel");
                    Dress(stand, "Apparel_Duster", "Devilstrand");
                }
                else
                {
                    Dress(stand, "Apparel_ShirtRuffle", Inner);
                    if (i % 2 == 0)
                    {
                        Dress(stand, "Apparel_VestRoyal", Inner);
                        Dress(stand, "Apparel_HatTop", Outer);
                    }
                    else
                    {
                        Dress(stand, "Apparel_Corset", Inner);
                        Dress(stand, "Apparel_RobeRoyal", Outer);
                        Dress(stand, "Apparel_HatLadies", Outer);
                    }
                }
            }

            // -- the reference pair: the dropper's colour source -----------
            //
            // A stepped take wants an exact colour already ON the map: one
            // sip is a single click on camera, where typing a hex value is
            // a beat of dead film. These two are set with ColorForcer at
            // build time and are the ONLY dyed things the stage places.
            //
            // That is not a workaround dressed as a feature: matching a
            // garment you already own is what the dropper is for, and it is
            // the same trick DebugTools_GifStage uses when it puts a teal
            // duster on a model pawn purely to be sipped.
            Reference(map, origin + new IntVec3(RowX, 0, RefZ),
                "Apparel_ShirtRuffle", Inner, DressWhite);
            Reference(map, origin + new IntVec3(RowX + 2, 0, RefZ),
                "Apparel_VestRoyal", Inner, BlackTie);

            Messages.Message(
                $"Wardrobe stage built: {StandCount} undyed stands, {Inner} and {Outer}, "
                + "plus a dyed reference pair to dropper from.",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }

        /// <summary>
        /// SCENES-only: Dev mode → Debug actions → Apparel Painter → Paint
        /// wardrobe stage, clicking the SAME south-west corner cell the build
        /// action used. Turns the undyed row into the principal's throne-room
        /// palette in one step, so a before/after pair is two captures of one
        /// cell rect with a single action between them.
        ///
        /// THIS IS NOT A SHORTCUT PAST THE MOD. It commits through
        /// <c>Dialog_StandColorPicker.AcceptForTest</c>, which is the picker's
        /// own <c>SaveColor</c> path — the same code a mouse click reaches,
        /// including ColorForcer and the owner's adapter refresh. What it skips
        /// is the mouse, not the mod. The regression harness drives the picker
        /// the same way, headless, and has since before this stage existed.
        ///
        /// The dialog is never added to the window stack: constructing it and
        /// committing is enough, exactly as Harness.CaseDialogAccept does.
        /// </summary>
        [DebugAction("Apparel Painter", "Paint wardrobe stage", false, false,
            actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        internal static void PaintWardrobeStage()
        {
            Map map = Find.CurrentMap;
            IntVec3 origin = UI.MouseCell();
            int painted = 0;
            int frock = 0;

            for (int i = 0; i < StandCount; i++)
            {
                IntVec3 cell = origin + new IntVec3(RowX + i * 2, 0, RowZ);
                if (!cell.InBounds(map))
                {
                    continue;
                }
                Building_OutfitStand stand = cell.GetFirstThing<Building_OutfitStand>(map);
                if (stand == null)
                {
                    continue;
                }

                List<Thing> held = (stand as IThingHolder)?.GetDirectlyHeldThings()?.ToList();
                if (held == null || held.Count == 0)
                {
                    continue;
                }

                // The soldier is painted PER ITEM rather than in two groups,
                // which is the whole claim of the mod stated in one stand: four
                // garments, four separate commits, one coherent kit out the far
                // side. Note the duster is Shell over the vest's Middle and the
                // pants' OnSkin, and it covers Torso/Neck/Shoulders/Arms/Legs —
                // so on the rendered stand the visible read is duster plus
                // helmet. The vest and pants are still individually painted and
                // still listed in the Paint tab; they are simply worn under a
                // coat, exactly as they would be in play.
                if (i == SoldierIndex)
                {
                    Commit(stand, held.Where(t => IsDef(t, "Apparel_CollarShirt")).ToList(),
                        Tan499);
                    Commit(stand, held.Where(t => IsDef(t, "Apparel_FlakPants")).ToList(),
                        RangerGreen);
                    Commit(stand, held.Where(t => IsDef(t, "Apparel_FlakVest")).ToList(),
                        CoyoteBrown);
                    Commit(stand, held.Where(t => IsDef(t, "Apparel_AdvancedHelmet")).ToList(),
                        CoyoteBrown);
                    Commit(stand, held.Where(t => IsDef(t, "Apparel_Duster")).ToList(),
                        OliveDrab);
                    painted++;
                    continue;
                }

                // Shirts read as dress white on every stand. The men wear
                // the Stresemann — dark grey waistcoat under black jacket
                // and hat — and the women take one frock apiece.
                Commit(stand, held.Where(IsShirt).ToList(), DressWhite);
                if (i % 2 == 0)
                {
                    Commit(stand, held.Where(t => !IsShirt(t)).ToList(), BlackTie);
                }
                else
                {
                    Commit(stand, held.Where(t => !IsShirt(t)).ToList(),
                        Frocks[frock++ % Frocks.Length]);
                }
                painted++;
            }

            Messages.Message(
                painted == 0
                    ? "No wardrobe stands here — click the stage's south-west corner."
                    : $"Painted {painted} stands through the picker's own commit path.",
                painted == 0 ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.TaskCompletion,
                historical: false);
        }

        /// <summary>Hour the dusk action pins to. Glow crosses 0.6 near here
        /// (shadowless, warm cast); nudge it live via Dev → TweakValues
        /// between takes, then re-run the dusk action — the pin is a clock
        /// write and does not track this field on its own.</summary>
        [TweakValue("ApparelPainter", 12f, 23.5f)]
        internal static float DuskHour = 19f;

        [DebugAction("Apparel Painter", "Pin lighting: noon", false, false,
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        internal static void PinLightingNoon()
        {
            PinLighting(Find.CurrentMap);
            Messages.Message("Lighting pinned: noon, clear.",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }

        [DebugAction("Apparel Painter", "Pin lighting: dusk", false, false,
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        internal static void PinLightingDusk()
        {
            PinLighting(Find.CurrentMap, DuskHour);
            Messages.Message(
                $"Lighting pinned: dusk ({DuskHour:0.#}h), clear. TweakValue ApparelPainter.DuskHour dials it.",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }

        /// <summary>Advance the pinned clock by one hour, re-clamping the
        /// weather on the way. The sweep driver walks noon to past dusk with
        /// this so a whole hour curve comes off ONE world roll — the cast at a
        /// given hour is a function of the tile's latitude and the day of year
        /// (GenCelestial.CelestialSunGlowPercent), and a relaunch re-rolls
        /// both. That is why judging the cast BETWEEN takes never converged:
        /// each take was a different planet. Sweep within a take instead.
        /// Hour is read back off DayPercent, so it needs no new engine API and
        /// wraps past midnight for free (PinLighting's delta stays positive).
        /// </summary>
        [DebugAction("Apparel Painter", "Pin lighting: +1 hour", false, false,
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        internal static void PinLightingNextHour()
        {
            Map map = Find.CurrentMap;
            PinLighting(map, GenLocalDate.DayPercent(map) * 24f + 1f);
            Messages.Message(
                $"Lighting pinned: {GenLocalDate.DayPercent(map) * 24f:0.0}h, clear.",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }

        /// <summary>
        /// Pin the map to local noon under clear weather, so two captures taken
        /// minutes apart light identically.
        ///
        /// WHY NOT ForceSetCurSkyGlow: it writes curSkyGlowInt, and
        /// SkyManagerUpdate recomputes that from CurrentSkyTarget() on the very
        /// next frame. The glow is derived from the clock, so the clock is the
        /// only durable lever.
        ///
        /// NOON is the BUILD default, and the trade is real. Shadow strength
        /// is <c>Clamp01(|CurCelestialSunGlow - 0.6| / 0.15)</c>, so it falls
        /// to ZERO at glow 0.6 — dusk — giving perfectly flat light, and dusk
        /// drags the whole palette orange and dim. Noon gives the brightest,
        /// most neutral ambient: right for stills that must show true fabric
        /// colour. The principal chose that dusk cast as the MOOD for the A/B
        /// gifs (2026-08-27), so the pin is re-aimable after build: the
        /// "Pin lighting" debug actions re-pin the clock to noon or to
        /// <see cref="DuskHour"/> without rebuilding the stage.
        ///
        /// The band that spoiled the first capture was never the sun: it was
        /// the roof edge of a neighbouring building. Site the pad clear of
        /// roofed structures and it does not arise.
        /// </summary>
        internal static void PinLighting(Map map)
        {
            PinLighting(map, 12f);
        }

        /// <summary>Re-aim the pin at any hour; the weather stays clamped
        /// clear either way.</summary>
        internal static void PinLighting(Map map, float hour)
        {
            float dayPercent = GenLocalDate.DayPercent(map);
            int intoDay = (int)(dayPercent * GenDate.TicksPerDay);
            int target = (int)(hour / 24f * GenDate.TicksPerDay);
            int delta = target - intoDay;
            if (delta < 0)
            {
                delta += GenDate.TicksPerDay;
            }
            Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame + delta);

            if (map.weatherManager != null && WeatherDefOf.Clear != null)
            {
                map.weatherManager.TransitionTo(WeatherDefOf.Clear);
                map.weatherManager.curWeatherAge = 999999;
            }
        }

        internal static bool IsShirt(Thing t)
        {
            return t?.def?.defName == "Apparel_ShirtRuffle";
        }

        internal static bool IsDef(Thing t, string defName)
        {
            return t?.def?.defName == defName;
        }

        internal static bool IsVest(Thing t)
        {
            return t?.def?.defName == "Apparel_VestRoyal";
        }

        internal static void Commit(Building_OutfitStand stand, List<Thing> items, Color colour)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }
            new Dialog_StandColorPicker(stand, items).AcceptForTest(colour);
        }

        /// <summary>
        /// Drop one already-dyed garment on the floor as a dropper source.
        /// Loose on the ground rather than in a container: the dropper resolves
        /// the whole CELL and menus what it finds, so a bare floor tile with
        /// one item on it is the least ambiguous target there is.
        /// </summary>
        internal static void Reference(Map map, IntVec3 cell, string defName, string stuffName, Color tint)
        {
            Apparel garment = Garment(defName, stuffName);
            if (garment == null)
            {
                return;
            }
            ColorForcer.ForceSetColor(garment, tint);
            GenSpawn.Spawn(garment, cell, map);
        }

        internal static void Dress(Building_OutfitStand stand, string defName, string stuffName)
        {
            Apparel garment = Garment(defName, stuffName);
            if (garment != null)
            {
                stand.AddApparel(garment);
            }
        }

        /// <summary>
        /// Undyed and style-cleared. The stuff is named rather than taken
        /// from <c>GenStuff.DefaultStuffFor</c>, which is the whole point of
        /// this stage: the default would put every garment in the same
        /// material and the row would lose the base-colour variation the
        /// paired shot depends on.
        ///
        /// Colour goes through <see cref="ColorForcer"/>, never
        /// <c>SetColor</c> — the comp no-ops on exact white for an undyed
        /// item, so a direct write leaves it inactive and the garment paints
        /// wrong later.
        /// </summary>
        internal static Apparel Garment(string defName, string stuffName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return null;
            }

            ThingDef stuff = null;
            if (def.MadeFromStuff)
            {
                // A stuff sits in SEVERAL categories, so the test is whether
                // any of them is one this def accepts — not whether its first
                // one happens to match.
                stuff = DefDatabase<ThingDef>.GetNamedSilentFail(stuffName);
                if (stuff?.stuffProps?.categories == null
                    || !stuff.stuffProps.categories.Any(c => def.stuffCategories.Contains(c)))
                {
                    stuff = GenStuff.DefaultStuffFor(def);
                }
            }

            Apparel garment = (Apparel)ThingMaker.MakeThing(def, stuff);
            garment.SetStyleDef(null);
            garment.overrideGraphicIndex = 0;
            ColorForcer.ResetToNatural(garment);
            return garment;
        }
    }
}
#endif
