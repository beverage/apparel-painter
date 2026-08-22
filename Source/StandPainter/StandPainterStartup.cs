using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace StandPainter
{
    /// <summary>
    /// Injects the Paint tab onto every outfit-stand def at startup.
    ///
    /// Runtime injection, not an XML patch, on purpose: list nodes on a
    /// shared vanilla def are a commons (root DEC-032 — two mods' Adds
    /// clobber each other), while appending from C# is load-order-independent
    /// and collides with nobody.
    ///
    /// Class-keyed, not defName-keyed: every def whose thingClass is
    /// Building_OutfitStand or a subclass gets the tab — the vanilla stand,
    /// the Biotech kid stand, and any modded stand reusing the class. Without
    /// Odyssey no def matches and startup is a harmless no-op.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class StandPainterStartup
    {
        static StandPainterStartup()
        {
            Type tabType = typeof(ITab_StandPainter);
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.thingClass == null || !typeof(Building_OutfitStand).IsAssignableFrom(def.thingClass))
                {
                    continue;
                }
                // StaticConstructorOnStartup runs after ResolveReferences, so
                // inspectorTabsResolved (the live list) must be appended too;
                // inspectorTabs alone would only matter on a future re-resolve.
                if (def.inspectorTabs == null)
                {
                    def.inspectorTabs = new List<Type>();
                }
                if (def.inspectorTabsResolved == null)
                {
                    def.inspectorTabsResolved = new List<InspectTabBase>();
                }
                if (!def.inspectorTabs.Contains(tabType))
                {
                    def.inspectorTabs.Add(tabType);
                    def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(tabType));
                }
            }
        }
    }
}
