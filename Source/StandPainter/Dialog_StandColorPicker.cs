using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace StandPainter
{
    /// <summary>
    /// Vanilla's colour picker pointed at one stand-held item or the whole
    /// stand, plus surface vanilla lacks: a brightness slider under the HSV
    /// wheel (the wheel encodes only hue and saturation — with no forced
    /// colour value it PRESERVES brightness on every drag, so the slider is
    /// the missing third axis, not decoration), a hex / decimal-triplet
    /// direct-input field (doubling as a copyable readout), an eyedropper on
    /// the Old colour box for one-click revert, a saved-swatch band (the
    /// user's own palette, persisted in ModSettings across games), and a
    /// map-wide dropper that sips a colour off anything through the native
    /// Targeter — the whole cell stack, floors included (BL-080).
    ///
    /// The R/G/B textfields are OURS, not the base's: the base is
    /// constructed with ColorComponents.None (its ColorTextfields
    /// early-returns but still reserves the 125px left column, so the wheel
    /// keeps its place), and we draw the same Widgets.ColorTextfields —
    /// same "colorTextfields" control names, so the base's Tab cycling
    /// still works — vertically centred on the wheel's centreline. Vanilla's
    /// own picker (the glower) shows Hue|Sat only; the HSV numerics were
    /// noise once the wheel + slider cover that space analogically.
    ///
    /// Live preview pushes the working colour to the real items on colour
    /// commit (wheel mouse-up, palette click, field entry), then recaches the
    /// stand — RealtimeOnly drawing shows it the same frame, on the map
    /// itself.
    ///
    /// THE PICKER IS A PALETTE, NOT A MODAL. The whole point is seeing and
    /// touching the world while picking: the window drags by its full-width
    /// top strip (the only drag zone), remembers its position for the
    /// session, does not absorb clicks around itself (the Paint tab stays
    /// live — its swatches become eyedroppers while a picker is open, via
    /// AdoptColor), does not lock the camera, and never closes from a map
    /// click. Only Cancel/Esc (revert) or Accept (keep) end the session —
    /// and while the map dropper is targeting, Esc stops the targeting
    /// first (OnCancelKeyPressed), not the window.
    ///
    /// LAYOUT. Four owned bands — drag strip (window space) / vanilla base /
    /// saved swatches / direct input — with the base handed a rect that
    /// excludes ours. The base's internal layout (private statics,
    /// RectDivider maths) is additionally MIRRORED in MirrorBaseLayout: the
    /// window height derives from it, and the overlays (Old colour dropper,
    /// brightness slider, our RGB fields) anchor through it. The block row
    /// consumes the 125px fields column + 17 margin at left and the 250px
    /// palette + 17 at right; the wheel centres in the REMAINING MIDDLE
    /// COLUMN — not the window. Mirror and size share one formula, so
    /// vanilla layout drift shows up as a misplaced overlay after a game
    /// update, never an error — re-verify there.
    ///
    /// Per-drag-frame preview pushes are deliberately off by default: every
    /// unique colour mints a permanent GraphicDatabase entry (AGENTS
    /// invariant); PreviewWhileDragging exists for feel testing.
    /// </summary>
    public class Dialog_StandColorPicker : Dialog_ColorPickerBase
    {
        // Assigned at runtime by the TweakValue dev menu, hence the explicit
        // initializer rather than a CS0649 suppression.
        [TweakValue("StandPainter")]
        internal static bool PreviewWhileDragging = false;

        internal struct Snapshot
        {
            internal Thing item;
            internal bool wasActive;
            internal Color color;
        }

        /// <summary>Mirror of Dialog_ColorPickerBase.DoWindowContents'
        /// consumption, in baseRect-relative terms. All row constants are
        /// verified against the 1.6.4871 decompile.</summary>
        internal struct MirroredLayout
        {
            internal float blockTopOffset;
            internal float blockHeight;
            internal float wheelCenterXOffset;
            internal float readbackTopOffset;
            internal float readbackHeight;
            internal float requiredHeight;
        }

        internal const string DirectInputControlName = "StandPainter_DirectInput";
        internal const float DirectInputRowHeight = 30f;
        internal const float DragStripHeight = 24f;
        internal const float DragStripGap = 4f;
        internal const float SwatchCell = 26f;
        internal const float SwatchPitch = 28f;
        internal const int MaxSwatches = 60;
        internal const float WheelSize = 128f;

        internal static readonly Widgets.ColorComponents RgbComponents =
            Widgets.ColorComponents.Red | Widgets.ColorComponents.Green | Widgets.ColorComponents.Blue;

        internal static readonly Color BadInputTint = new Color(1f, 0.35f, 0.35f);

        internal static List<Color> cachedPalette;

        /// <summary>Where the user last dragged the window, reused for every
        /// open this session so repositioning survives a six-stand painting
        /// pass. Position only — size stays computed from the palette.</summary>
        internal static Vector2? rememberedPosition;

        internal readonly Building_OutfitStand stand;
        internal readonly List<Thing> targets;
        internal readonly List<Snapshot> snapshots = new List<Snapshot>();
        internal readonly Color naturalDefault;
        internal Color lastPushed;
        internal bool accepted;
        internal string directInputBuffer = "";
        internal bool mapDropperActive;
        internal bool retargetNextFrame;

        // Our own ColorTextfields state — the same trio the base keeps for
        // its (now blanked) fields. previousFocused is sampled on Layout
        // events, mirroring the base's commit-on-unfocus semantics. Not
        // readonly: Widgets.ColorTextfields takes the array by ref.
        internal string[] textfieldBuffers = new string[6];
        internal Color textfieldColorBuffer;
        internal string previousFocusedControlName;

        protected override bool ShowDarklight => false;

        protected override bool ShowColorTemperatureBar => false;

        protected override float ForcedColorValue => -1f;

        protected override Color DefaultColor => naturalDefault;

        protected override List<Color> PickableColors => Palette();

        internal static int SwatchesPerRow => Mathf.Max(1, Mathf.FloorToInt((600f - 36f + (SwatchPitch - SwatchCell)) / SwatchPitch));

        public override Vector2 InitialSize
        {
            get
            {
                MirroredLayout m = MirrorBaseLayout(600f - Margin * 2f);
                float stripOverhang = Mathf.Max(0f, DragStripHeight - Margin) + DragStripGap;
                return new Vector2(600f, m.requiredHeight + Margin * 2f + stripOverhang + SwatchBandHeight() + DirectInputRowHeight);
            }
        }

        public Dialog_StandColorPicker(Building_OutfitStand stand, List<Thing> targets)
            : base(Widgets.ColorComponents.None, Widgets.ColorComponents.None)
        {
            this.stand = stand;
            this.targets = targets;
            // Palette, not modal (see class doc): the tab's dropper flow and
            // camera freedom depend on all three of these staying exactly so.
            // NOT draggable — the base then eats stray mousedowns, and
            // dragging is exclusively the strip's job (LateWindowOnGUI).
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            foreach (Thing item in targets)
            {
                CompColorable comp = item.TryGetComp<CompColorable>();
                if (comp == null)
                {
                    continue;
                }
                snapshots.Add(new Snapshot
                {
                    item = item,
                    wasActive = comp.Active,
                    color = comp.Color,
                });
            }
            naturalDefault = targets.Count > 0 ? ColorForcer.NaturalColorOf(targets[0]) : Color.white;
            color = targets.Count > 0 ? targets[0].DrawColor : Color.white;
            oldColor = color;
            lastPushed = color;
        }

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            if (rememberedPosition.HasValue)
            {
                windowRect.x = Mathf.Clamp(rememberedPosition.Value.x, 0f, UI.screenWidth - windowRect.width);
                windowRect.y = Mathf.Clamp(rememberedPosition.Value.y, 0f, UI.screenHeight - windowRect.height);
            }
        }

        /// <summary>
        /// The palette row: vanilla's Structure ColorDefs — the curated paint
        /// chart every player already knows from wall painting — in shipped
        /// display order. Mods adding Structure colours appear automatically.
        /// </summary>
        internal static List<Color> Palette()
        {
            if (cachedPalette == null)
            {
                cachedPalette = DefDatabase<ColorDef>.AllDefsListForReading
                    .Where(d => d.colorType == ColorType.Structure)
                    .OrderBy(d => d.displayOrder)
                    .Select(d => d.color)
                    .ToList();
            }
            return cachedPalette;
        }

        /// <summary>
        /// Replays the base's layout arithmetic. Sources (1.6.4871,
        /// Dialog_ColorPickerBase.DoWindowContents read in full):
        /// RectDivider default margin (17,4); zero-rows cost 4; the palette
        /// column is 250 wide → 9 swatches per 28px row, ColorSelector's out
        /// height is (rows-1)*28+26 and ColorPalette adds its 26px
        /// default-swatch row + 2; the base's ColorTextfields is blanked
        /// (None) so its aggregator stays (125, 0) — the block row still
        /// consumes that 125+17 at left and 250+17 at right, and the wheel
        /// centres in the remaining middle column; the block row is
        /// max(palette, 128); then 10 + 34 (temperature, allocated even when
        /// hidden) + 26 mystery row, each +4 margin; bottom-justified
        /// buttons ButSize.y+4 and a zero-row. Readback gets the remainder —
        /// here exactly two Text.LineHeight rows + margin + 6 slack, because
        /// requiredHeight is built from the same terms.
        /// </summary>
        internal static MirroredLayout MirrorBaseLayout(float baseWidth)
        {
            float headerHeight;
            using (new TextBlock(GameFont.Medium))
            {
                headerHeight = Text.CalcHeight("ChooseAColor".Translate().CapitalizeFirst(), baseWidth);
            }
            int paletteRows = Mathf.CeilToInt(Palette().Count / 9f);
            float paletteHeight = (paletteRows - 1) * 28f + 26f + 26f + 2f;
            float blockHeight = Mathf.Max(paletteHeight, 128f);
            float middleLeft = 125f + 17f;
            float middleRight = baseWidth - 250f - 17f;
            MirroredLayout result = default;
            result.blockTopOffset = headerHeight + 4f + 4f;
            result.blockHeight = blockHeight;
            result.wheelCenterXOffset = middleLeft + (middleRight - middleLeft) / 2f;
            result.readbackTopOffset = result.blockTopOffset + blockHeight + 4f + 14f + 38f + 30f;
            result.readbackHeight = Text.LineHeight * 2f + 4f + 6f;
            result.requiredHeight = result.readbackTopOffset + result.readbackHeight + 4f + ButSize.y + 4f;
            return result;
        }

        /// <summary>
        /// Accepts hex (EFD8AE, #EFD8AE; 8 digits tolerated, alpha dropped)
        /// or a decimal triplet (237,216,174). Triplets follow the engine's
        /// own ParseColor idiom: any component above 1 means the whole
        /// triplet is bytes, otherwise 0–1 floats. A fourth component is
        /// tolerated and ignored — colours here are triplets, never quads.
        /// </summary>
        internal static bool TryParseColorInput(string raw, out Color result)
        {
            result = Color.white;
            if (raw.NullOrEmpty())
            {
                return false;
            }
            string s = raw.Trim();
            string hex = s.StartsWith("#") ? s.Substring(1) : s;
            if (hex.Length == 6 || hex.Length == 8)
            {
                bool allHex = true;
                foreach (char c in hex)
                {
                    if (!Uri.IsHexDigit(c))
                    {
                        allHex = false;
                        break;
                    }
                }
                if (allHex)
                {
                    if (!ColorUtility.TryParseHtmlString("#" + hex.Substring(0, 6), out Color parsedHex))
                    {
                        return false;
                    }
                    parsedHex.a = 1f;
                    result = parsedHex;
                    return true;
                }
            }
            string[] parts = s.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3 && parts.Length != 4)
            {
                return false;
            }
            float[] vals = new float[3];
            bool bytes = false;
            for (int i = 0; i < 3; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                {
                    return false;
                }
                if (v < 0f || v > 255f)
                {
                    return false;
                }
                if (v > 1f)
                {
                    bytes = true;
                }
                vals[i] = v;
            }
            float scale = bytes ? 255f : 1f;
            result = new Color(vals[0] / scale, vals[1] / scale, vals[2] / scale, 1f);
            return true;
        }

        /// <summary>
        /// Adopt a colour picked from outside the base's own controls — the
        /// Paint tab's swatch eyedroppers, the Old colour revert dropper, a
        /// saved swatch, or the map dropper. Preview then pushes on the next
        /// WindowUpdate through the normal commit gate.
        /// </summary>
        internal void AdoptColor(Color c)
        {
            color = c;
            // Unfocus the direct-input field so its buffer re-canonicalises
            // to the adopted colour's hex.
            GUIUtility.keyboardControl = 0;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Four owned bands: strip (window space, LateWindowOnGUI) / base
            // / saved swatches / input. The base's RectDivider never sees our
            // bands, and we never draw into its rect — except the overlays
            // located through the layout mirror (RGB fields, brightness
            // slider, Old colour dropper).
            float swatchBandHeight = SwatchBandHeight();
            Rect baseRect = inRect;
            baseRect.yMin += Mathf.Max(0f, DragStripHeight - Margin) + DragStripGap;
            baseRect.yMax -= DirectInputRowHeight + swatchBandHeight;
            base.DoWindowContents(baseRect);
            MirroredLayout m = MirrorBaseLayout(baseRect.width);
            DoRgbFields(baseRect, m);
            DoBrightnessSlider(baseRect, m);
            DoOldColorDropper(baseRect, m);
            DoSavedSwatchBand(new Rect(inRect.x, inRect.yMax - DirectInputRowHeight - swatchBandHeight + DragStripGap, inRect.width, swatchBandHeight - DragStripGap));
            DoDirectInputRow(new Rect(inRect.x, inRect.yMax - 26f, inRect.width, 26f));
            // The base samples its (unused) focus tracker on Layout; ours
            // feeds the commit-on-unfocus semantics of OUR fields.
            if (Event.current.type == EventType.Layout)
            {
                previousFocusedControlName = GUI.GetNameOfFocusedControl();
            }
        }

        /// <summary>
        /// Our R/G/B textfields, drawn with the engine's own
        /// Widgets.ColorTextfields in the base's (reserved but blank) left
        /// column — repositioned so the middle field's centre sits on the
        /// wheel's centreline, spacing untouched. Aggregator rows land at
        /// top+4+i*34 with height 30, so the middle row's centre is 53px
        /// below the aggregator origin.
        /// </summary>
        internal void DoRgbFields(Rect baseRect, MirroredLayout m)
        {
            float wheelCenterY = baseRect.y + m.blockTopOffset + m.blockHeight / 2f;
            float fieldsTop = wheelCenterY - 53f;
            RectAggregator aggregator = new RectAggregator(new Rect(new Vector2(baseRect.x, fieldsTop), new Vector2(125f, 0f)), 195906069);
            Widgets.ColorTextfields(ref aggregator, ref color, ref textfieldBuffers, ref textfieldColorBuffer, previousFocusedControlName, "colorTextfields", RgbComponents, RgbComponents);
        }

        /// <summary>
        /// The wheel's missing third axis. The base centres a 128px HSV
        /// wheel in the middle column and, with no forced colour value,
        /// carries the CURRENT colour's brightness through every drag — so
        /// without this slider brightness is only reachable by typing.
        /// Wheel-width, centred exactly under the wheel; skips silently if
        /// the mirrored block leaves no room (layout drift guard, same
        /// policy as the Old colour dropper).
        /// </summary>
        internal void DoBrightnessSlider(Rect baseRect, MirroredLayout m)
        {
            float blockTop = baseRect.y + m.blockTopOffset;
            float wheelBottom = blockTop + (m.blockHeight - WheelSize) / 2f + WheelSize;
            Rect sliderRect = new Rect(baseRect.x + m.wheelCenterXOffset - WheelSize / 2f, wheelBottom + 12f, WheelSize, 22f);
            if (sliderRect.yMax > blockTop + m.blockHeight)
            {
                return;
            }
            Color.RGBToHSV(color, out float h, out float s, out float v);
            float newV = Widgets.HorizontalSlider(sliderRect, v, 0f, 1f);
            TooltipHandler.TipRegionByKey(sliderRect, "StandPainter_BrightnessTip");
            if (Mathf.Abs(newV - v) > 0.0005f)
            {
                Color adjusted = Color.HSVToRGB(h, s, newV);
                adjusted.a = 1f;
                color = adjusted;
            }
        }

        /// <summary>Band height for the saved swatches + the save cell,
        /// grid-wrapped. Feeds both InitialSize and the band rect.</summary>
        internal static float SwatchBandHeight()
        {
            int cells = StandPainterMod.Settings.savedSwatches.Count + 1;
            int rows = Mathf.CeilToInt((float)cells / SwatchesPerRow);
            return rows * SwatchPitch + DragStripGap;
        }

        /// <summary>
        /// The user's own palette: vanilla ColorBox cells (native look,
        /// selection border, click sound). Click adopts; right-click removes
        /// via float menu; the trailing + cell saves the current colour.
        /// Persisted in ModSettings — shared across saves and sessions.
        /// </summary>
        internal void DoSavedSwatchBand(Rect band)
        {
            List<Color> saved = StandPainterMod.Settings.savedSwatches;
            int perRow = SwatchesPerRow;
            for (int i = 0; i <= saved.Count; i++)
            {
                int row = i / perRow;
                int col = i % perRow;
                Rect cell = new Rect(band.x + col * SwatchPitch, band.y + row * SwatchPitch, SwatchCell, SwatchCell);
                if (i == saved.Count)
                {
                    if (Widgets.ButtonImage(cell.ContractedBy(4f), TexButton.Plus))
                    {
                        SaveCurrentSwatch();
                    }
                    TooltipHandler.TipRegionByKey(cell, "StandPainter_SaveSwatchTip");
                    continue;
                }
                Color swatch = saved[i];
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && cell.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    int index = i;
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        new FloatMenuOption("StandPainter_RemoveSwatch".Translate(), delegate { RemoveSwatch(index); }),
                    }));
                }
                TooltipHandler.TipRegion(cell, "StandPainter_SavedSwatchTip".Translate(ColorUtility.ToHtmlStringRGB(swatch)));
                if (Widgets.ColorBox(cell, ref color, swatch))
                {
                    AdoptColor(color);
                }
            }
        }

        internal void SaveCurrentSwatch()
        {
            List<Color> saved = StandPainterMod.Settings.savedSwatches;
            foreach (Color existing in saved)
            {
                if (existing.IndistinguishableFrom(color))
                {
                    Messages.Message("StandPainter_SwatchExists".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
            }
            if (saved.Count >= MaxSwatches)
            {
                Messages.Message("StandPainter_SwatchLimit".Translate(MaxSwatches), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            Color toSave = color;
            toSave.a = 1f;
            saved.Add(toSave);
            StandPainterMod.Instance.WriteSettings();
            RefreshWindowHeight();
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        internal void RemoveSwatch(int index)
        {
            List<Color> saved = StandPainterMod.Settings.savedSwatches;
            if (index < 0 || index >= saved.Count)
            {
                return;
            }
            saved.RemoveAt(index);
            StandPainterMod.Instance.WriteSettings();
            RefreshWindowHeight();
        }

        /// <summary>Grow or shrink the open window in place when the swatch
        /// band wraps to a different row count — position kept, size
        /// recomputed from the same formula as InitialSize.</summary>
        internal void RefreshWindowHeight()
        {
            Vector2 size = InitialSize;
            windowRect = new Rect(windowRect.x, windowRect.y, size.x, size.y);
        }

        /// <summary>
        /// The revert eyedropper, overlaid on the base's Old colour box —
        /// located by replaying ColorReadback's own arithmetic on the
        /// mirrored readback rect. Skips silently when the numbers stop
        /// adding up (a vanilla layout change), rather than misdrawing.
        /// </summary>
        internal void DoOldColorDropper(Rect baseRect, MirroredLayout m)
        {
            float readbackTop = baseRect.y + m.readbackTopOffset;
            if (readbackTop + m.readbackHeight > baseRect.yMax - ButSize.y - 4f + 1f)
            {
                return;
            }
            Rect readback = new Rect(baseRect.x, readbackTop, baseRect.width, m.readbackHeight);
            readback.SplitVertically((readback.width - 26f) / 2f, out Rect left, out _);
            float lineHeight = Text.LineHeight;
            float labelWidth = Mathf.Max(100f,
                Mathf.Max("CurrentColor".Translate().CapitalizeFirst().GetWidthCached(),
                    "OldColor".Translate().CapitalizeFirst().GetWidthCached()));
            Rect oldBox = new Rect(left.x + labelWidth + 17f, readbackTop + lineHeight + 4f, left.width - labelWidth - 17f, lineHeight);
            if (oldBox.width < 40f || oldBox.yMax > baseRect.yMax)
            {
                return;
            }
            GUI.DrawTexture(new Rect(oldBox.xMax - 18f, oldBox.y + (oldBox.height - 16f) / 2f, 16f, 16f), StandPainterTex.Dropper);
            if (Mouse.IsOver(oldBox))
            {
                Widgets.DrawHighlight(oldBox);
            }
            TooltipHandler.TipRegionByKey(oldBox, "StandPainter_OldColorTip");
            if (Widgets.ButtonInvisible(oldBox))
            {
                AdoptColor(oldColor);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }

        // ---- Map-wide dropper -------------------------------------------

        /// <summary>
        /// Targeting rules for the map dropper. The dropper READS — sources
        /// need no CompColorable — so anything spawned and unfogged is fair
        /// game, and bare cells are targetable too: terrain is a colour
        /// source (BL-080) and clicking empty floor is the natural
        /// sample-the-carpet gesture. mapObjectTargetsMustBeAutoAttackable
        /// defaults TRUE and must be forced off or most items and buildings
        /// refuse targeting.
        /// </summary>
        internal static TargetingParameters DropperTargetParams()
        {
            return new TargetingParameters
            {
                canTargetPawns = true,
                canTargetAnimals = true,
                canTargetHumans = true,
                canTargetMechs = true,
                canTargetItems = true,
                canTargetBuildings = true,
                canTargetCorpses = true,
                canTargetLocations = true,
                mapObjectTargetsMustBeAutoAttackable = false,
                validator = DropperValidator,
            };
        }

        internal static bool DropperValidator(TargetInfo ti)
        {
            if (ti.Map == null)
            {
                return false;
            }
            if (ti.HasThing)
            {
                return ti.Thing.Spawned && !ti.Thing.Position.Fogged(ti.Thing.Map);
            }
            return ti.Cell.InBounds(ti.Map) && !ti.Cell.Fogged(ti.Map);
        }

        /// <summary>Toggle the map dropper: native Targeter with our dropper
        /// icon as the mouse attachment. Continuous — each sip re-arms on
        /// the next WindowUpdate; right-click or Esc stops.</summary>
        internal void BeginMapDropper()
        {
            if (mapDropperActive)
            {
                Find.Targeter.StopTargeting();
                return;
            }
            mapDropperActive = true;
            Find.Targeter.BeginTargeting(DropperTargetParams(), OnDropperTarget,
                caster: null, actionWhenFinished: OnDropperFinished,
                mouseAttachment: StandPainterTex.Dropper, requiresCastedSelected: false);
        }

        /// <summary>
        /// Every colour source at the clicked cell, categorised (BL-080).
        /// The targeter's own per-click pick follows draw altitude — a
        /// wall-mounted heater outranks the stand sharing its cell, an
        /// overlay building outranks the carpet, and terrain is not a thing
        /// at all — so the click resolves to the CELL, and a menu offers
        /// the whole stack: worn/held apparel, then things (topmost first),
        /// then the floor. A cell with only its floor skips the menu and
        /// sips instantly — the bare-carpet gesture stays one click.
        /// </summary>
        internal void OnDropperTarget(LocalTargetInfo t)
        {
            retargetNextFrame = true;
            Map map = Find.CurrentMap;
            if (map == null || !t.Cell.InBounds(map))
            {
                return;
            }
            IntVec3 cell = t.Cell;

            List<Thing> apparelItems = new List<Thing>();
            List<Thing> surfaceThings = new List<Thing>();
            List<Thing> cellThings = cell.GetThingList(map);
            foreach (Thing th in cellThings)
            {
                Pawn wearer = th as Pawn ?? (th as Corpse)?.InnerPawn;
                if (wearer != null)
                {
                    List<Apparel> worn = wearer.apparel?.WornApparel;
                    if (worn != null)
                    {
                        apparelItems.AddRange(worn);
                    }
                    continue;
                }
                if (th is Building_OutfitStand standTarget)
                {
                    // Held items go under Apparel; the stand itself still
                    // lists under Things — its stuff colour is a real
                    // sample too.
                    apparelItems.AddRange(standTarget.HeldItems);
                }
                ThingCategory category = th.def.category;
                if (category == ThingCategory.Building || category == ThingCategory.Item || category == ThingCategory.Plant)
                {
                    surfaceThings.Add(th);
                }
            }
            // Visual stack order: topmost first.
            surfaceThings.SortBy(th => -(int)th.def.altitudeLayer);

            TerrainDef terrain = cell.GetTerrain(map);
            ColorDef paint = map.terrainGrid.ColorAt(cell);
            Color floorColor;
            bool floorPainted;
            if (paint != null)
            {
                floorColor = paint.color;
                floorPainted = true;
            }
            else if (DubsInterop.TryGetFloorColor(map, cell, out Color dubsColor))
            {
                // Dubs Paint Shop keeps floor paint in its own map
                // component, invisible to TerrainGrid.ColorAt.
                floorColor = dubsColor;
                floorPainted = true;
            }
            else
            {
                floorColor = terrain.DrawColor;
                floorPainted = false;
            }

            if (apparelItems.Count == 0 && surfaceThings.Count == 0)
            {
                AdoptColor(floorColor);
                SoundDefOf.Click.PlayOneShotOnCamera();
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            if (apparelItems.Count > 0)
            {
                options.Add(new FloatMenuOption("StandPainter_HeaderApparel".Translate(), null));
                foreach (Thing item in apparelItems)
                {
                    options.Add(SourceOption(item));
                }
            }
            if (surfaceThings.Count > 0)
            {
                options.Add(new FloatMenuOption("StandPainter_HeaderThings".Translate(), null));
                foreach (Thing th in surfaceThings)
                {
                    options.Add(SourceOption(th));
                }
            }
            options.Add(new FloatMenuOption("StandPainter_HeaderFloor".Translate(), null));
            string floorLabel = terrain.LabelCap;
            if (floorPainted)
            {
                floorLabel += ", " + "StandPainter_Painted".Translate();
            }
            floorLabel += " (" + ColorUtility.ToHtmlStringRGB(floorColor) + ")";
            Color capturedFloor = floorColor;
            options.Add(new FloatMenuOption(floorLabel, delegate
            {
                AdoptColor(capturedFloor);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }, BaseContent.WhiteTex, capturedFloor));
            // FloatMenu_Ordered: plain FloatMenu sorts disabled options (the
            // headers) to the bottom, un-categorising everything.
            Find.WindowStack.Add(new FloatMenu_Ordered(options));
        }

        /// <summary>One menu entry per colour source: the thing's own icon,
        /// hex in the label. Sampling only reads DrawColor — no
        /// CompColorable required.</summary>
        internal FloatMenuOption SourceOption(Thing source)
        {
            string label = source.LabelShortCap + " (" + ColorUtility.ToHtmlStringRGB(source.DrawColor) + ")";
            return new FloatMenuOption(label, delegate
            {
                AdoptColor(source.DrawColor);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }, source, Color.white);
        }

        internal void OnDropperFinished()
        {
            mapDropperActive = false;
        }

        public override void OnCancelKeyPressed()
        {
            // Esc while sipping stops the targeting, not the window; the
            // next Esc reverts and closes as usual.
            if (mapDropperActive && Find.Targeter.IsTargeting)
            {
                Find.Targeter.StopTargeting();
                return;
            }
            base.OnCancelKeyPressed();
        }

        protected override void LateWindowOnGUI(Rect inRect)
        {
            base.LateWindowOnGUI(inRect);
            // Runs outside the contents group, in window space — so the strip
            // can span the FULL panel width, edge to edge across the margins,
            // like a title bar. Visual and drag hitbox are the same rect;
            // GUI.DragWindow consumes the mousedown before Window's
            // !draggable branch can eat it.
            Rect stripRect = new Rect(0f, 0f, windowRect.width, DragStripHeight);
            // Vanilla's title-band chrome reads as a real bar edge to edge;
            // the old LightHighlight wash was too faint to register as one.
            Widgets.DrawTitleBG(stripRect);
            if (Mouse.IsOver(stripRect))
            {
                Widgets.DrawHighlight(stripRect);
            }
            Color guiPrev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.4f);
            Widgets.DrawLineHorizontal(0f, stripRect.yMax - 1f, stripRect.width);
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(stripRect.center.x - 9f, stripRect.center.y - 9f, 18f, 18f), TexButton.DragHash);
            GUI.color = guiPrev;
            TooltipHandler.TipRegionByKey(stripRect, "StandPainter_DragStripTip");
            GUI.DragWindow(stripRect);
        }

        /// <summary>
        /// The hex / decimal-triplet field, in its own reserved band under
        /// the base's buttons, plus the map-dropper toggle. Unfocused the
        /// field tracks the working colour as canonical hex — a copy source
        /// for carrying a colour to another stand; focused it is an input
        /// applied on Enter or Set.
        /// </summary>
        internal void DoDirectInputRow(Rect rowRect)
        {
            Rect labelRect = new Rect(rowRect.x, rowRect.y, 78f, rowRect.height);
            Rect fieldRect = new Rect(labelRect.xMax + 4f, rowRect.y, 132f, rowRect.height);
            Rect setRect = new Rect(fieldRect.xMax + 6f, rowRect.y, 44f, rowRect.height);
            Rect dropperRect = new Rect(setRect.xMax + 10f, rowRect.y + (rowRect.height - 24f) / 2f, 24f, 24f);

            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(labelRect, "StandPainter_DirectInputLabel".Translate());
            }

            bool focused = GUI.GetNameOfFocusedControl() == DirectInputControlName;
            if (!focused)
            {
                directInputBuffer = ColorUtility.ToHtmlStringRGB(color);
            }
            bool parseOk = TryParseColorInput(directInputBuffer, out Color parsed);

            bool enterPressed = focused
                && Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if (enterPressed)
            {
                Event.current.Use();
            }

            GUI.SetNextControlName(DirectInputControlName);
            Color guiPrev = GUI.color;
            if (focused && !parseOk && !directInputBuffer.NullOrEmpty())
            {
                GUI.color = BadInputTint;
            }
            directInputBuffer = Widgets.TextField(fieldRect, directInputBuffer);
            GUI.color = guiPrev;
            TooltipHandler.TipRegionByKey(fieldRect, "StandPainter_DirectInputTip");

            bool setClicked = Widgets.ButtonText(setRect, "StandPainter_ApplyColor".Translate(), active: parseOk);
            if ((enterPressed || setClicked) && parseOk)
            {
                color = parsed;
                // Unfocus so the buffer re-canonicalises to the applied hex.
                GUIUtility.keyboardControl = 0;
            }

            if (mapDropperActive)
            {
                Widgets.DrawHighlight(dropperRect);
                Widgets.DrawBox(dropperRect);
            }
            if (Widgets.ButtonImage(dropperRect, StandPainterTex.Dropper))
            {
                BeginMapDropper();
            }
            TooltipHandler.TipRegionByKey(dropperRect, "StandPainter_MapDropperTip");
        }

        internal void PushPreview()
        {
            foreach (Thing item in targets)
            {
                ColorForcer.ForceSetColor(item, color);
            }
            StandGraphics.Recache(stand);
            lastPushed = color;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (retargetNextFrame)
            {
                retargetNextFrame = false;
                if (!Find.Targeter.IsTargeting && !mapDropperActive)
                {
                    BeginMapDropper();
                }
            }
            if (color != lastPushed && (PreviewWhileDragging || !Input.GetMouseButton(0)))
            {
                PushPreview();
            }
        }

        protected override void SaveColor(Color color)
        {
            this.color = color;
            PushPreview();
            accepted = true;
        }

        public override void PostClose()
        {
            base.PostClose();
            if (mapDropperActive && Find.Targeter.IsTargeting)
            {
                Find.Targeter.StopTargeting();
            }
            rememberedPosition = new Vector2(windowRect.x, windowRect.y);
            if (accepted)
            {
                return;
            }
            foreach (Snapshot snap in snapshots)
            {
                if (snap.item == null || snap.item.Destroyed)
                {
                    continue;
                }
                if (snap.wasActive)
                {
                    ColorForcer.ForceSetColor(snap.item, snap.color);
                }
                else
                {
                    ColorForcer.ResetToNatural(snap.item);
                }
            }
            StandGraphics.Recache(stand);
        }
    }
}
