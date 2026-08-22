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
    /// Vanilla's colour picker (HSV wheel, RGB/HSV textfields, palette row)
    /// pointed at one stand-held item or the whole stand, plus surface
    /// vanilla lacks: a hex / decimal-triplet direct-input field (doubling
    /// as a copyable readout) and an eyedropper on the Old colour box for
    /// one-click revert to the colour the picker opened with.
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
    /// click. Only Cancel/Esc (revert) or Accept (keep) end the session.
    ///
    /// LAYOUT. Three owned bands — drag strip (window space) / vanilla base
    /// / direct input — with the base handed a rect that excludes ours.
    /// The base's internal layout (private statics, RectDivider maths) is
    /// additionally MIRRORED in MirrorBaseLayout: the window height derives
    /// from it (no magic 450 — the base's divider errors and squeezes when
    /// starved; it was sized for the glower's 54-swatch palette, and our
    /// palette is the live ColorDef set), and the Old colour box position
    /// falls out of the same maths for the revert dropper. Mirror and size
    /// share one formula, so vanilla layout drift shows up as a misplaced
    /// overlay after a game update, never an error — re-verify there.
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
            internal float readbackTopOffset;
            internal float readbackHeight;
            internal float requiredHeight;
        }

        internal const string DirectInputControlName = "StandPainter_DirectInput";
        internal const float DirectInputRowHeight = 30f;
        internal const float DragStripHeight = 24f;
        internal const float DragStripGap = 4f;
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

        protected override bool ShowDarklight => false;

        protected override bool ShowColorTemperatureBar => false;

        protected override float ForcedColorValue => -1f;

        protected override Color DefaultColor => naturalDefault;

        protected override List<Color> PickableColors => Palette();

        public override Vector2 InitialSize
        {
            get
            {
                MirroredLayout m = MirrorBaseLayout(600f - Margin * 2f);
                float stripOverhang = Mathf.Max(0f, DragStripHeight - Margin) + DragStripGap;
                return new Vector2(600f, m.requiredHeight + Margin * 2f + stripOverhang + DirectInputRowHeight);
            }
        }

        public Dialog_StandColorPicker(Building_OutfitStand stand, List<Thing> targets)
            : base(Widgets.ColorComponents.All, Widgets.ColorComponents.All)
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
        /// Replays the base's layout arithmetic. Sources (1.6.4871):
        /// RectDivider default margin (17,4); zero-rows cost 4; the palette
        /// column is 250 wide → 9 swatches per 28px row, ColorSelector's out
        /// height is (rows-1)*28+26 and ColorPalette adds its 26px
        /// default-swatch row + 2; ColorTextfields aggregates six 30px rows
        /// at 4 margin (204); the block row is max(palette, 128, fields);
        /// then 10 + 34 (temperature, allocated even when hidden) + 26
        /// mystery row, each +4 margin; bottom-justified buttons ButSize.y+4
        /// and a zero-row. Readback gets the remainder — here exactly two
        /// Text.LineHeight rows + margin + 6 slack, because requiredHeight
        /// is built from the same terms.
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
            float fieldsHeight = 6f * 34f;
            float blockHeight = Mathf.Max(paletteHeight, Mathf.Max(128f, fieldsHeight));
            MirroredLayout result = default;
            result.readbackTopOffset = headerHeight + 4f + 4f + blockHeight + 4f + 14f + 38f + 30f;
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
        /// Paint tab's swatch eyedroppers, or the Old colour revert dropper.
        /// Preview then pushes on the next WindowUpdate through the normal
        /// commit gate.
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
            // Three owned bands: strip (window space, LateWindowOnGUI) /
            // base / input. The base's RectDivider never sees our bands, and
            // we never draw into its rect — except the Old colour dropper,
            // which overlays the base's readback box via the layout mirror.
            Rect baseRect = inRect;
            baseRect.yMin += Mathf.Max(0f, DragStripHeight - Margin) + DragStripGap;
            baseRect.yMax -= DirectInputRowHeight;
            base.DoWindowContents(baseRect);
            DoOldColorDropper(baseRect);
            DoDirectInputRow(new Rect(inRect.x, inRect.yMax - 26f, inRect.width, 26f));
        }

        /// <summary>
        /// The revert eyedropper, overlaid on the base's Old colour box —
        /// located by replaying ColorReadback's own arithmetic on the
        /// mirrored readback rect. Skips silently when the numbers stop
        /// adding up (a vanilla layout change), rather than misdrawing.
        /// </summary>
        internal void DoOldColorDropper(Rect baseRect)
        {
            MirroredLayout m = MirrorBaseLayout(baseRect.width);
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

        protected override void LateWindowOnGUI(Rect inRect)
        {
            base.LateWindowOnGUI(inRect);
            // Runs outside the contents group, in window space — so the strip
            // can span the FULL panel width, edge to edge across the margins,
            // like a title bar. Visual and drag hitbox are the same rect;
            // GUI.DragWindow consumes the mousedown before Window's
            // !draggable branch can eat it.
            Rect stripRect = new Rect(0f, 0f, windowRect.width, DragStripHeight);
            if (Mouse.IsOver(stripRect))
            {
                Widgets.DrawHighlight(stripRect);
            }
            else
            {
                Widgets.DrawLightHighlight(stripRect);
            }
            Color guiPrev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(stripRect.center.x - 9f, stripRect.center.y - 9f, 18f, 18f), TexButton.DragHash);
            GUI.color = guiPrev;
            TooltipHandler.TipRegionByKey(stripRect, "StandPainter_DragStripTip");
            GUI.DragWindow(stripRect);
        }

        /// <summary>
        /// The hex / decimal-triplet field, in its own reserved band under
        /// the base's buttons. Unfocused it tracks the working colour as
        /// canonical hex — a copy source for carrying a colour to another
        /// stand; focused it is an input applied on Enter or Set.
        /// </summary>
        internal void DoDirectInputRow(Rect rowRect)
        {
            Rect labelRect = new Rect(rowRect.x, rowRect.y, 78f, rowRect.height);
            Rect fieldRect = new Rect(labelRect.xMax + 4f, rowRect.y, 132f, rowRect.height);
            Rect setRect = new Rect(fieldRect.xMax + 6f, rowRect.y, 44f, rowRect.height);

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
