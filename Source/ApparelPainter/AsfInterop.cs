using System;
using System.Reflection;
using Verse;

namespace ApparelPainter
{
    /// <summary>
    /// Reflection bridge to Adaptive Storage Framework's render cache. ASF
    /// storage — and every skin of it: [sbz] Neat Storage, Reel's Expanded
    /// Storage, most display-storage mods — draws stored items from
    /// per-item "print data" its StorageRenderer bakes. That cache is
    /// invalidated on add/remove/settings changes and NEVER on a pure
    /// colour change, so painted items kept their old look on the shelf
    /// until a reload (found in play, 2026-08-24 — the fourth bake site
    /// after the stand cache, the rack drawer and Dubs' floor grids).
    /// All members used here are public (verified against the 1.6
    /// decompile: ThingClass.Renderer, SetAllPrintDatasDirty,
    /// TryUpdateCurrentGraphic); present-only, degrades silently.
    /// </summary>
    internal static class AsfInterop
    {
        internal static bool initialized;
        internal static Type thingClassType;
        internal static MethodInfo rendererGetter;
        internal static MethodInfo setDirtyMethod;
        internal static MethodInfo updateGraphicMethod;

        internal static void EnsureInit()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            thingClassType = GenTypes.GetTypeInAnyAssembly("AdaptiveStorage.ThingClass");
            if (thingClassType == null)
            {
                return;
            }
            PropertyInfo renderer = thingClassType.GetProperty("Renderer");
            rendererGetter = renderer?.GetGetMethod();
            Type rendererType = renderer?.PropertyType;
            setDirtyMethod = rendererType?.GetMethod("SetAllPrintDatasDirty", Type.EmptyTypes);
            updateGraphicMethod = rendererType?.GetMethod("TryUpdateCurrentGraphic", Type.EmptyTypes);
        }

        /// <summary>Dirty the ASF renderer when the building is ASF-classed.
        /// Returns whether it was one (informational; failure to resolve
        /// internals just means no refresh, never an error).</summary>
        internal static bool TryRefresh(Thing building)
        {
            EnsureInit();
            if (thingClassType == null || !thingClassType.IsInstanceOfType(building))
            {
                return false;
            }
            if (rendererGetter == null || setDirtyMethod == null)
            {
                return true;
            }
            object renderer = rendererGetter.Invoke(building, null);
            if (renderer == null)
            {
                return true;
            }
            setDirtyMethod.Invoke(renderer, null);
            updateGraphicMethod?.Invoke(renderer, null);
            return true;
        }
    }
}
