using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace StandPainter
{
    /// <summary>
    /// Vanilla's colour picker (HSV wheel, RGB/HSV textfields, palette row)
    /// pointed at one stand-held item or the whole stand, plus a direct-input
    /// field vanilla lacks: hex or decimal-triplet entry, doubling as a
    /// copyable readout of the current colour.
    ///
    /// Live preview pushes the working colour to the real items on colour
    /// commit (wheel mouse-up, palette click, field entry), then recaches the
    /// stand — RealtimeOnly drawing shows it the same frame, on the map
    /// itself. The window is therefore draggable (and remembers its dragged
    /// position for the session): the whole point is seeing the stand while
    /// picking. For the same reason clicking the map does NOT close it —
    /// only Cancel/Esc (revert) or Accept (keep) end the session.
    ///
    /// Per-drag-frame preview pushes are deliberately off by default: every
    /// unique colour mints a permanent GraphicDatabase entry (AGENTS
    /// invariant); PreviewWhileDragging exists for feel testing.
    ///
    /// Accept keeps the last push; any other close restores the per-item
    /// (active, colour) snapshot taken at open — including full de-colouring
    /// via Disable for items that were natural.
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

        internal const string DirectInputControlName = "StandPainter_DirectInput";
        internal const float DirectInputRowHeight = 30f;
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

        /// <summary>
        /// The stock 600x450 was sized for the glower picker's 54-swatch,
        /// 6-row palette. Ours is the live ColorDef set — vanilla's 63
        /// Structure colours already need a 7th row (the base's divider then
        /// errors "Rect height was too small by 22" and squeezes its
        /// readback row into the buttons), and modded palettes grow further.
        /// Each extra row of 9 swatches is 26px. The direct-input band is
        /// added on top; DoWindowContents keeps it out of the base's rect.
        /// </summary>
        public override Vector2 InitialSize
        {
            get
            {
                int paletteRows = Mathf.CeilToInt(Palette().Count / 9f);
                float extraPalette = Mathf.Max(0, paletteRows - 6) * 26f;
                return new Vector2(600f, 450f + extraPalette + DirectInputRowHeight);
            }
        }

        public Dialog_StandColorPicker(Building_OutfitStand stand, List<Thing> targets)
            : base(Widgets.ColorComponents.All, Widgets.ColorComponents.All)
        {
            this.stand = stand;
            this.targets = targets;
            draggable = true;
            // The base closes on any outside click; with the window dragged
            // aside to watch the stand, a stray map click would silently
            // cancel-and-revert an in-progress pick. Explicit exits only.
            closeOnClickedOutside = false;
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

        public override void DoWindowContents(Rect inRect)
        {
            // The base's RectDivider must never see our reserved bottom band,
            // and we never draw into its rect — the two layouts cannot
            // collide however either one grows.
            Rect baseRect = inRect;
            baseRect.yMax -= DirectInputRowHeight;
            base.DoWindowContents(baseRect);
            DoDirectInputRow(new Rect(inRect.x, inRect.yMax - 26f, inRect.width, 26f));
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
