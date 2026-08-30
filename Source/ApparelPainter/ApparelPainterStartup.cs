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
                    // CONTENTS BEFORE PAINT, on every family (principal,
                    // 2026-08-28: what a container holds reads before what
                    // you can do to it). Tabs draw right-to-left from this
                    // list, and the two families reach the screen by
                    // different routes: outfit stands carry their Contents
                    // tab ON the def, so Paint must insert BEFORE it to
                    // land left of it; Building_Storage families get their
                    // Contents tab dynamically AFTER the def list, so a
                    // plain append already displays the same way. Both
                    // routes converge on "Contents | Paint | Storage".
                    // (Armor Racks' own tab name is unverified against the
                    // "Contents" match — check its display order when that
                    // integration gets its own shot. LWM's Deep Storage
                    // def-lists its contents tab as
                    // ITab_DeepStorage_Inventory, hence the "Inventory"
                    // match — added 2026-08-29 for the integrations shot.)
                    int at = def.inspectorTabs.FindIndex(
                        t => t != null && (t.Name.Contains("Contents")
                            || t.Name.Contains("Inventory")));
                    if (at >= 0)
                    {
                        def.inspectorTabs.Insert(at, tabType);
                    }
                    else
                    {
                        def.inspectorTabs.Add(tabType);
                    }
                    int atResolved = def.inspectorTabsResolved.FindIndex(
                        t => t != null && t.GetType().Name.Contains("Contents"));
                    if (atResolved >= 0)
                    {
                        def.inspectorTabsResolved.Insert(
                            atResolved, InspectTabManager.GetSharedInstance(tabType));
                    }
                    else
                    {
                        def.inspectorTabsResolved.Add(
                            InspectTabManager.GetSharedInstance(tabType));
                    }
                }
            }
        }
    }
}
