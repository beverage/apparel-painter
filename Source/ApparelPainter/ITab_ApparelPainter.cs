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
    /// recognises (stands, Armor Racks, vanilla-style storage):
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
        internal const float StyleWidth = 24f;
        internal const float StyleGap = 6f;

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
            // The style slot is RESERVED on every row, not just the ~12%
            // that can use it: the columns then line up whatever a stand
            // happens to hold, and 24px off a 245px label zone truncates
            // nothing that was not already truncating.
            float labelRight = width - StyleWidth - StyleGap - SwatchWidth - ResetWidth - 16f;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect labelRect = new Rect(labelLeft, y, labelRight - labelLeft, RowHeight);
            string label = item.LabelCap;
            Widgets.Label(labelRect, label.Truncate(labelRect.width));
            Text.Anchor = TextAnchor.UpperLeft;

            // Full item details on hover, the same construction ASF's
            // contents tab uses (LabelCap carries quality and wear;
            // DescriptionDetailed appends the apparel Layer/Covers block).
            // Only over the icon+label zone — the swatch and Reset keep
            // their own tips.
            Rect tipZone = new Rect(0f, y, labelRight, RowHeight);
            if (Mouse.IsOver(tipZone))
            {
                TooltipHandler.TipRegion(tipZone,
                    new TipSignal(label + "\n" + item.DescriptionDetailed, item.thingIDNumber ^ 0x2E5C1));
            }

            DoStyleButton(owner, adapter, item, width, y);

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

        /// <summary>
        /// The style control: a thumbnail of the item's current
        /// look. LEFT-CLICK CYCLES, RIGHT-CLICK OPENS THE LIST — the
        /// character editor's gesture pair, without its carousel, because
        /// 30 of the 45 styled defs on a heavily modded list have exactly
        /// one style and cycling them is a single-click toggle. Drawn only
        /// when the def actually has styles, which on a real wardrobe is
        /// about one row in eight; its presence is also the only signal
        /// anywhere in the game that an item CAN be styled, since style
        /// defs ship no label and the item's name never changes.
        ///
        /// Unlike the swatch, this does NOT become an eyedropper while a
        /// picker is open, and its writes are not part of the picker's
        /// snapshot: a restyle is immediate and permanent, exactly like the
        /// Reset button beside it. Preview-and-revert for style is the
        /// picker-band step, deliberately deferred.
        /// </summary>
        internal void DoStyleButton(Thing owner, ContainerAdapter adapter, Thing item, float width, float y)
        {
            if (!StyleForcer.CanRestyle(item))
            {
                return;
            }
            Rect styleRect = new Rect(
                width - StyleWidth - StyleGap - SwatchWidth - ResetWidth - 8f,
                y + (RowHeight - SwatchHeight) / 2f,
                StyleWidth,
                SwatchHeight);
            Widgets.DrawBox(styleRect);
            if (Mouse.IsOver(styleRect))
            {
                Widgets.DrawHighlight(styleRect);
            }
            // The engine's own icon resolution, handed the CURRENT style —
            // null draws the plain def icon, so the button is a live
            // thumbnail of the choice with no fallback chain of ours.
            Widgets.DefIcon(styleRect.ContractedBy(2f), item.def, item.Stuff, 1f, item.StyleDef);

            string current = StyleForcer.CurrentLabel(item) ?? "ApparelPainter_NoStyle".Translate().ToString();
            TooltipHandler.TipRegion(styleRect,
                new TipSignal("ApparelPainter_StyleTip".Translate(current), item.thingIDNumber ^ 0x51E1E));

            // BOTH buttons come back through ButtonInvisible — branch on
            // which, vanilla's own two-button idiom (Message.cs:171).
            // Do NOT test for MouseDown alongside this: GUI.DoControl
            // consumes MouseDown for ANY button and returns true on the
            // following MouseUp, so a right-click test placed after this
            // call sees a Used event and never fires, while the click falls
            // through to the left-click branch. That shipped once.
            if (Widgets.ButtonInvisible(styleRect))
            {
                if (Event.current.button == 1)
                {
                    OpenStyleMenu(owner, adapter, item);
                }
                else
                {
                    ApplyStyle(owner, adapter, item, StyleForcer.NextInCycle(item, 1));
                }
            }
        }

        /// <summary>
        /// The style menu's contents, built separately from showing it so
        /// the harness can assert the authored order without a WindowStack.
        /// "No style" leads; the rest follow the index's cycle order, so the
        /// menu and the click-cycle agree on what comes next.
        /// </summary>
        internal static List<FloatMenuOption> StyleMenuOptions(Thing owner, ContainerAdapter adapter, Thing item)
        {
            List<FloatMenuOption> menu = new List<FloatMenuOption>
            {
                new FloatMenuOption("ApparelPainter_NoStyle".Translate(),
                    () => ApplyStyle(owner, adapter, item, null)),
            };
            foreach (StyleOption option in StyleIndex.For(item.def))
            {
                StyleOption captured = option;
                menu.Add(new FloatMenuOption(captured.Label,
                    () => ApplyStyle(owner, adapter, item, captured.Style),
                    captured.Icon, Color.white));
            }
            return menu;
        }

        internal static void OpenStyleMenu(Thing owner, ContainerAdapter adapter, Thing item)
        {
            // Ordered, not the stock menu: this list is authored, and the
            // base re-sorts by priority.
            Find.WindowStack.Add(new FloatMenu_Ordered(StyleMenuOptions(owner, adapter, item)));
        }

        internal static void ApplyStyle(Thing owner, ContainerAdapter adapter, Thing item, ThingStyleDef style)
        {
            if (!StyleForcer.SetStyle(item, style))
            {
                return; // no change: do not rebuild a graphic cache for nothing
            }
            adapter.Refresh(owner);
            SoundDefOf.Click.PlayOneShotOnCamera();
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
