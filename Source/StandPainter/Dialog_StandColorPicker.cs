using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace StandPainter
{
    /// <summary>
    /// Vanilla's colour picker (HSV wheel, RGB/hex textfields, palette row)
    /// pointed at one stand-held item or the whole stand.
    ///
    /// Live preview pushes the working colour to the real items on colour
    /// commit (wheel mouse-up, palette click, field entry), then recaches the
    /// stand — RealtimeOnly drawing shows it the same frame, on the map
    /// itself. Per-drag-frame pushes are deliberately off by default: every
    /// unique colour mints a permanent GraphicDatabase entry (AGENTS
    /// invariant); PreviewWhileDragging exists for feel testing.
    ///
    /// Accept keeps the last push; any other close (Cancel, Esc, click
    /// outside) restores the per-item (active, colour) snapshot taken at
    /// open — including full de-colouring via Disable for items that were
    /// natural.
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

        internal static List<Color> cachedPalette;

        internal readonly Building_OutfitStand stand;
        internal readonly List<Thing> targets;
        internal readonly List<Snapshot> snapshots = new List<Snapshot>();
        internal readonly Color naturalDefault;
        internal Color lastPushed;
        internal bool accepted;

        protected override bool ShowDarklight => false;

        protected override bool ShowColorTemperatureBar => false;

        protected override float ForcedColorValue => -1f;

        protected override Color DefaultColor => naturalDefault;

        protected override List<Color> PickableColors => Palette();

        public Dialog_StandColorPicker(Building_OutfitStand stand, List<Thing> targets)
            : base(Widgets.ColorComponents.All, Widgets.ColorComponents.All)
        {
            this.stand = stand;
            this.targets = targets;
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
