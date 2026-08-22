using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace StandPainter
{
    /// <summary>
    /// The Paint tab on an outfit stand: one row per held item — info card,
    /// icon, label, a colour swatch that opens the picker, and a reset back
    /// to natural — plus whole-stand paint/reset above the list. The weapon
    /// slot has no CompColorable and renders swatch-less (AGENTS invariant).
    /// </summary>
    public class ITab_StandPainter : ITab
    {
        internal const float Margin = 10f;
        internal const float ButtonRowHeight = 30f;
        internal const float RowHeight = 30f;
        internal const float IconSize = 27f;
        internal const float SwatchWidth = 44f;
        internal const float SwatchHeight = 22f;
        internal const float ResetWidth = 58f;

        internal Vector2 scrollPosition;

        public ITab_StandPainter()
        {
            size = new Vector2(460f, 450f);
            labelKey = "StandPainter_Tab";
        }

        internal Building_OutfitStand Stand => SelThing as Building_OutfitStand;

        public override bool IsVisible
        {
            get
            {
                Building_OutfitStand stand = Stand;
                return stand != null && stand.Faction == Faction.OfPlayer;
            }
        }

        internal static List<Thing> ColorableItems(Building_OutfitStand stand)
        {
            List<Thing> result = new List<Thing>();
            foreach (Thing item in stand.HeldItems)
            {
                if (item.TryGetComp<CompColorable>() != null)
                {
                    result.Add(item);
                }
            }
            return result;
        }

        protected override void FillTab()
        {
            Building_OutfitStand stand = Stand;
            if (stand == null)
            {
                return;
            }

            Rect outRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);
            List<Thing> colorable = ColorableItems(stand);
            float curY = outRect.y;

            bool anyActive = false;
            foreach (Thing t in colorable)
            {
                CompColorable comp = t.TryGetComp<CompColorable>();
                if (comp != null && comp.Active)
                {
                    anyActive = true;
                    break;
                }
            }

            Rect paintAllRect = new Rect(outRect.x, curY, 150f, 26f);
            if (Widgets.ButtonText(paintAllRect, "StandPainter_PaintAll".Translate(), active: colorable.Count > 0))
            {
                OpenPicker(stand, colorable);
            }
            Rect resetAllRect = new Rect(paintAllRect.xMax + 8f, curY, 110f, 26f);
            if (Widgets.ButtonText(resetAllRect, "StandPainter_ResetAll".Translate(), active: anyActive))
            {
                foreach (Thing t in colorable)
                {
                    ColorForcer.ResetToNatural(t);
                }
                StandGraphics.Recache(stand);
            }
            curY += ButtonRowHeight;

            Widgets.ListSeparator(ref curY, outRect.width, "StandPainter_Contents".Translate());

            IReadOnlyList<Thing> held = stand.HeldItems;
            if (held.Count == 0)
            {
                Widgets.NoneLabel(ref curY, outRect.width);
                return;
            }

            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, held.Count * RowHeight);
            Rect scrollRect = new Rect(outRect.x, curY, outRect.width, outRect.yMax - curY);
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
            float y = 0f;
            for (int i = 0; i < held.Count; i++)
            {
                DoRow(stand, held[i], i, viewRect.width, ref y);
            }
            Widgets.EndScrollView();
        }

        internal void DoRow(Building_OutfitStand stand, Thing item, int index, float width, ref float y)
        {
            Rect rowRect = new Rect(0f, y, width, RowHeight);
            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
            }
            else if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(rowRect);
            }
            Widgets.InfoCardButton(0f, y + 3f, item);
            Widgets.ThingIcon(new Rect(28f, y + 1f, IconSize, IconSize), item);

            CompColorable comp = item.TryGetComp<CompColorable>();
            float labelLeft = 28f + IconSize + 6f;
            float labelRight = width - SwatchWidth - ResetWidth - 16f;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect labelRect = new Rect(labelLeft, y, labelRight - labelLeft, RowHeight);
            string label = item.LabelCap;
            Widgets.Label(labelRect, label.Truncate(labelRect.width));
            Text.Anchor = TextAnchor.UpperLeft;

            if (comp == null)
            {
                y += RowHeight;
                return; // the weapon slot: nothing to paint
            }

            Rect swatchRect = new Rect(width - SwatchWidth - ResetWidth - 8f, y + (RowHeight - SwatchHeight) / 2f, SwatchWidth, SwatchHeight);
            Widgets.DrawBoxSolid(swatchRect, item.DrawColor);
            Widgets.DrawBox(swatchRect);
            TooltipHandler.TipRegionByKey(swatchRect, "StandPainter_SwatchTip");
            if (Widgets.ButtonInvisible(swatchRect))
            {
                OpenPicker(stand, new List<Thing> { item });
            }

            if (comp.Active)
            {
                Rect resetRect = new Rect(width - ResetWidth, y + 3f, ResetWidth, RowHeight - 6f);
                if (Widgets.ButtonText(resetRect, "StandPainter_Reset".Translate()))
                {
                    ColorForcer.ResetToNatural(item);
                    StandGraphics.Recache(stand);
                }
            }
            y += RowHeight;
        }

        internal static void OpenPicker(Building_OutfitStand stand, List<Thing> targets)
        {
            if (targets.Count == 0)
            {
                return;
            }
            // One picker at a time: closing the old one first runs its
            // cancel-revert before the new one snapshots.
            Find.WindowStack.TryRemove(typeof(Dialog_StandColorPicker), false);
            Find.WindowStack.Add(new Dialog_StandColorPicker(stand, targets));
        }
    }
}
