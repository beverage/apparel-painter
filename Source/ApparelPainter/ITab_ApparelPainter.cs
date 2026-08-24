using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ApparelPainter
{
    /// <summary>
    /// The Paint tab on any apparel-holding building the adapter seam
    /// recognises (stands, Armor Racks, vanilla-style storage — DEC-037):
    /// one row per listed item — info card, icon, label, a colour swatch
    /// that opens the picker, and a reset back to natural — plus
    /// whole-building paint/reset above the list. Items without
    /// CompColorable (a stand's weapon slot) render swatch-less.
    ///
    /// While a picker window is open the swatches become EYEDROPPERS: a
    /// dropper overlay appears and clicking a swatch adopts that item's
    /// colour into the open picker instead of opening a new one. Works
    /// across buildings — select another and sip colours from its tab.
    /// (Depends on the picker being palette-not-modal; see its class doc.)
    /// </summary>
    public class ITab_ApparelPainter : ITab
    {
        internal const float Margin = 10f;
        internal const float ButtonRowHeight = 30f;
        internal const float RowHeight = 30f;
        internal const float IconSize = 27f;
        internal const float SwatchWidth = 44f;
        internal const float SwatchHeight = 22f;
        internal const float ResetWidth = 58f;

        internal Vector2 scrollPosition;

        public ITab_ApparelPainter()
        {
            size = new Vector2(460f, 450f);
            labelKey = "ApparelPainter_Tab";
        }

        internal Thing Owner => SelThing;

        internal ContainerAdapter Adapter => ContainerAdapter.For(SelThing);

        public override bool IsVisible
        {
            get
            {
                Thing owner = Owner;
                if (owner == null || owner.Faction != Faction.OfPlayer)
                {
                    return false;
                }
                ContainerAdapter adapter = Adapter;
                return adapter != null && adapter.TabVisible(owner);
            }
        }

        /// <summary>One canonical row order on every family (principal call,
        /// 2026-08-24): label A→Z groups same-def garments, then quality
        /// high→low, then hit points high→low — the order a wardrobe is
        /// scanned in. The tab and the dropper's apparel section both use
        /// this. Raw enumeration order is spatial for storage and
        /// chronological for containers, matching neither each other nor
        /// the neighbours' own sorted content tabs.</summary>
        internal static int CompareForDisplay(Thing a, Thing b)
        {
            int result = string.Compare(a.def.label, b.def.label, StringComparison.OrdinalIgnoreCase);
            if (result != 0)
            {
                return result;
            }
            bool hasA = a.TryGetQuality(out QualityCategory qualityA);
            bool hasB = b.TryGetQuality(out QualityCategory qualityB);
            int rankA = hasA ? (int)qualityA : -1;
            int rankB = hasB ? (int)qualityB : -1;
            result = rankB.CompareTo(rankA);
            if (result != 0)
            {
                return result;
            }
            float hpA = a.def.useHitPoints ? (float)a.HitPoints / a.MaxHitPoints : 1f;
            float hpB = b.def.useHitPoints ? (float)b.HitPoints / b.MaxHitPoints : 1f;
            result = hpB.CompareTo(hpA);
            if (result != 0)
            {
                return result;
            }
            return a.thingIDNumber.CompareTo(b.thingIDNumber);
        }

        protected override void FillTab()
        {
            Thing owner = Owner;
            ContainerAdapter adapter = Adapter;
            if (owner == null || adapter == null)
            {
                return;
            }

            Rect outRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);
            List<Thing> listed = new List<Thing>(adapter.ListedItems(owner));
            listed.Sort(CompareForDisplay);
            List<Thing> colorable = new List<Thing>();
            foreach (Thing t in listed)
            {
                if (t.TryGetComp<CompColorable>() != null)
                {
                    colorable.Add(t);
                }
            }
            Dialog_StandColorPicker openPicker = Find.WindowStack.WindowOfType<Dialog_StandColorPicker>();
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
            if (Widgets.ButtonText(paintAllRect, "ApparelPainter_PaintAll".Translate(), active: colorable.Count > 0))
            {
                OpenPicker(owner, colorable);
            }
            Rect resetAllRect = new Rect(paintAllRect.xMax + 8f, curY, 110f, 26f);
            if (Widgets.ButtonText(resetAllRect, "ApparelPainter_ResetAll".Translate(), active: anyActive))
            {
                foreach (Thing t in colorable)
                {
                    ColorForcer.ResetToNatural(t);
                }
                adapter.Refresh(owner);
            }
            curY += ButtonRowHeight;

            Widgets.ListSeparator(ref curY, outRect.width, "ApparelPainter_Contents".Translate());

            if (listed.Count == 0)
            {
                Widgets.NoneLabel(ref curY, outRect.width);
                return;
            }

            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, listed.Count * RowHeight);
            Rect scrollRect = new Rect(outRect.x, curY, outRect.width, outRect.yMax - curY);
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
            float y = 0f;
            for (int i = 0; i < listed.Count; i++)
            {
                DoRow(owner, adapter, listed[i], i, viewRect.width, ref y, openPicker);
            }
            Widgets.EndScrollView();
        }

        internal void DoRow(Thing owner, ContainerAdapter adapter, Thing item, int index, float width, ref float y, Dialog_StandColorPicker openPicker)
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
                return; // e.g. a stand's weapon slot: nothing to paint
            }

            Rect swatchRect = new Rect(width - SwatchWidth - ResetWidth - 8f, y + (RowHeight - SwatchHeight) / 2f, SwatchWidth, SwatchHeight);
            Widgets.DrawBoxSolid(swatchRect, item.DrawColor);
            Widgets.DrawBox(swatchRect);
            if (Mouse.IsOver(swatchRect))
            {
                Widgets.DrawHighlight(swatchRect);
            }
            if (openPicker != null)
            {
                // Eyedropper mode: sip this item's colour into the open
                // picker rather than opening a new one.
                GUI.DrawTexture(new Rect(swatchRect.xMax - 18f, swatchRect.y + 3f, 16f, 16f), ApparelPainterTex.Dropper);
                TooltipHandler.TipRegionByKey(swatchRect, "ApparelPainter_DropperTip");
                if (Widgets.ButtonInvisible(swatchRect))
                {
                    openPicker.AdoptColor(item.DrawColor);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }
            else
            {
                TooltipHandler.TipRegionByKey(swatchRect, "ApparelPainter_SwatchTip");
                if (Widgets.ButtonInvisible(swatchRect))
                {
                    OpenPicker(owner, new List<Thing> { item });
                }
            }

            if (comp.Active)
            {
                Rect resetRect = new Rect(width - ResetWidth, y + 3f, ResetWidth, RowHeight - 6f);
                if (Widgets.ButtonText(resetRect, "ApparelPainter_Reset".Translate()))
                {
                    ColorForcer.ResetToNatural(item);
                    adapter.Refresh(owner);
                }
            }
            y += RowHeight;
        }

        internal static void OpenPicker(Thing owner, List<Thing> targets)
        {
            if (targets.Count == 0)
            {
                return;
            }
            // One picker at a time: closing the old one first runs its
            // cancel-revert before the new one snapshots.
            Find.WindowStack.TryRemove(typeof(Dialog_StandColorPicker), false);
            Find.WindowStack.Add(new Dialog_StandColorPicker(owner, targets));
        }
    }
}
