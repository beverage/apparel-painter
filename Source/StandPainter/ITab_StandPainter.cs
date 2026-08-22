using RimWorld;
using UnityEngine;
using Verse;

namespace StandPainter
{
    /// <summary>
    /// The Paint tab on an outfit stand. Bootstrap slice: proves the
    /// injection and the container read by listing held items read-only.
    /// The BL-079 build order grows this into swatch rows plus the picker.
    /// </summary>
    public class ITab_StandPainter : ITab
    {
        internal const float Margin = 10f;
        internal const float RowHeight = 28f;
        internal const float IconSize = 28f;

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

        protected override void FillTab()
        {
            Building_OutfitStand stand = Stand;
            if (stand == null)
            {
                return;
            }
            Rect outRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(Margin);
            float curY = outRect.y;
            Widgets.ListSeparator(ref curY, outRect.width, "StandPainter_Contents".Translate());
            if (stand.HeldItems.Count == 0)
            {
                Widgets.NoneLabel(ref curY, outRect.width);
                return;
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            foreach (Thing item in stand.HeldItems)
            {
                Widgets.ThingIcon(new Rect(outRect.x, curY, IconSize, IconSize), item);
                Widgets.Label(new Rect(outRect.x + IconSize + 8f, curY, outRect.width - IconSize - 8f, RowHeight), item.LabelCap);
                curY += RowHeight;
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
