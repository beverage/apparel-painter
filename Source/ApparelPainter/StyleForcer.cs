using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ApparelPainter
{
    /// <summary>
    /// Style writes, with the same discipline ColorForcer imposes on colour
    /// writes: never call `SetStyleDef` directly, because on its own it
    /// changes nothing anyone can see.
    ///
    /// `ThingStyleHelper.SetStyleDef` writes the comp field and clears one
    /// cache (`cachedStyleCategoryDef`) and stops. It does NOT clear
    /// `Thing.styleGraphicInt`, the cached style graphic, and it does not
    /// dirty the map mesh — so a styled item on a shelf keeps drawing its
    /// old art until something else happens to invalidate it.
    /// `Notify_ColorChanged` does both (`Verse/Thing.cs:1666`), which is
    /// why a style write borrows it. Then the owner's adapter Refresh
    /// handles the bake sites, exactly as for colour.
    ///
    /// The engine never had to solve this: nothing in vanilla restyles a
    /// thing that already exists, so nothing in vanilla needed an
    /// invalidation path.
    /// </summary>
    internal static class StyleForcer
    {
        /// <summary>
        /// Whether the row should offer a style control at all.
        ///
        /// `StyleSourcePrecept` is the guard that matters: an item whose
        /// style comes from a precept (a relic, ideo-styled gear) has a
        /// two-way relationship with it — `CompStyleable.SourcePrecept`
        /// re-derives styleDef from the precept — so writing styleDef
        /// underneath it leaves the pair disagreeing. No vanilla apparel is
        /// a relic, but a stand's parked weapon can be.
        /// </summary>
        internal static bool CanRestyle(Thing item)
        {
            if (item == null || item.Destroyed)
            {
                return false;
            }
            if (!item.def.CanBeStyled())
            {
                return false;
            }
            if (item.StyleSourcePrecept != null)
            {
                return false;
            }
            return StyleIndex.For(item.def).Count > 0;
        }

        /// <summary>
        /// The write. Returns false when nothing changed, so callers can
        /// skip the adapter refresh — a refresh rebuilds a graphic cache,
        /// and a no-op click should not.
        /// </summary>
        internal static bool SetStyle(Thing item, ThingStyleDef style)
        {
            if (!CanRestyle(item) || item.StyleDef == style)
            {
                return false;
            }
            // The property, not the SetStyleDef extension: Thing.StyleDef is
            // virtual and blueprints/frames override it to forward the style
            // to what they build. Nothing we target overrides it today; going
            // through the property costs nothing and stays right if that
            // changes.
            item.StyleDef = style;
            item.Notify_ColorChanged();
            ColorForcer.NotifyWearerIfWorn(item);
            return true;
        }

        /// <summary>Position of the item's current style in the cycle;
        /// -1 is the "no style" stop.</summary>
        internal static int CurrentIndex(Thing item)
        {
            ThingStyleDef current = item.StyleDef;
            if (current == null)
            {
                return -1;
            }
            List<StyleOption> options = StyleIndex.For(item.def);
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Style == current)
                {
                    return i;
                }
            }
            // A style the index does not know: assigned by a mod that has
            // since been removed from the category, or set by another mod
            // outright. Treat it as off the cycle so the next click lands
            // on a known stop rather than silently keeping it.
            return -1;
        }

        /// <summary>
        /// Next stop, wrapping through "no style". Cycling is the primary
        /// gesture because 30 of the 45 styled defs on a heavily modded
        /// list have exactly ONE style, making this a straight toggle
        /// — the menu exists for the six defs with three or more.
        /// </summary>
        internal static ThingStyleDef NextInCycle(Thing item, int direction)
        {
            List<StyleOption> options = StyleIndex.For(item.def);
            if (options.Count == 0)
            {
                return null;
            }
            int next = CurrentIndex(item) + direction;
            if (next >= options.Count)
            {
                next = -1;
            }
            else if (next < -1)
            {
                next = options.Count - 1;
            }
            return next < 0 ? null : options[next].Style;
        }

        /// <summary>The display name of an item's current style, or null
        /// when it has none.</summary>
        internal static string CurrentLabel(Thing item)
        {
            int at = CurrentIndex(item);
            return at < 0 ? null : StyleIndex.For(item.def)[at].Label;
        }
    }
}
