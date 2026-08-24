using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ApparelPainter
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
    public static class ApparelPainterStartup
    {
        static ApparelPainterStartup()
        {
            Type tabType = typeof(ITab_ApparelPainter);
            // The three adapter families (DEC-037). The rack type resolves
            // only when Armor Racks is loaded; storage covers every
            // Building_Storage subclass — the tab's apparel-present
            // visibility gate keeps it off crates and fridges.
            Type rackType = ArmorRackAdapter.RackType;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                Type tc = def.thingClass;
                if (tc == null)
                {
                    continue;
                }
                bool target = typeof(Building_OutfitStand).IsAssignableFrom(tc)
                    || typeof(Building_Storage).IsAssignableFrom(tc)
                    || (rackType != null && rackType.IsAssignableFrom(tc));
                if (!target)
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
