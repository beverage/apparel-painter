using RimWorld;
using UnityEngine;
using Verse;

namespace ApparelPainter
{
    /// <summary>
    /// Comp-wart-safe colour writes (AGENTS invariants). All colour changes
    /// go through here — never call CompColorable.SetColor directly.
    /// </summary>
    internal static class ColorForcer
    {
        /// <summary>
        /// Sets an item's colour through CompColorable, working around the
        /// engine wart where SetColor early-outs against its private colour
        /// field (default white while inactive) WITHOUT activating — a user
        /// painting an undyed item pure white would otherwise see nothing
        /// happen. The nudge write below always differs from both that
        /// private white and the target, so the real write always lands.
        /// </summary>
        internal static bool ForceSetColor(Thing item, Color target)
        {
            CompColorable comp = item.TryGetComp<CompColorable>();
            if (comp == null)
            {
                return false;
            }
            if (!comp.Active)
            {
                comp.SetColor(new Color(target.r, target.g, Mathf.Abs(target.b - 1f / 255f), target.a));
            }
            comp.SetColor(target);
            NotifyWearerIfWorn(item);
            return true;
        }

        /// <summary>Back to un-dyed: stuff colour, or the def's art tint.</summary>
        internal static bool ResetToNatural(Thing item)
        {
            CompColorable comp = item.TryGetComp<CompColorable>();
            if (comp == null || !comp.Active)
            {
                return false;
            }
            comp.Disable();
            NotifyWearerIfWorn(item);
            return true;
        }

        /// <summary>
        /// The colour the item shows with no comp colour active — what
        /// Disable reverts to. Mirrors Thing.DrawColor's fallback chain.
        /// </summary>
        internal static Color NaturalColorOf(Thing item)
        {
            if (item.Stuff != null)
            {
                return item.def.GetColorForStuff(item.Stuff);
            }
            if (item.def.graphicData != null)
            {
                return item.def.graphicData.color;
            }
            return Color.white;
        }

        /// <summary>
        /// A target can leave the stand mid-dialog (a pawn takes the outfit).
        /// Worn apparel renders from graphics cached on the pawn, which
        /// Notify_ColorChanged does not reach — dirty the wearer explicitly
        /// so a preview or revert never strands a stale colour on a colonist.
        /// </summary>
        internal static void NotifyWearerIfWorn(Thing item)
        {
            if (item.ParentHolder is Pawn_ApparelTracker tracker)
            {
                tracker.pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
        }
    }
}
