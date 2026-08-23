using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace StandPainter
{
    /// <summary>
    /// Read-only reflection bridge to Dubs Paint Shop's floor paint. Dubs
    /// predates vanilla floor paint and stores per-cell floor colours in its
    /// own DubRoss.MapComponent_PaintShop — invisible to
    /// TerrainGrid.ColorAt, so a Dubs-painted floor read as unpainted until
    /// this existed. GetColourAlpha is preferred: it flags "painted" via the
    /// alpha layer, where plain GetColour treats painted-black as unpainted
    /// (both verified against the 1.6 assembly, 2026-08-23). Everything
    /// degrades silently when the mod is absent or its internals change.
    /// </summary>
    internal static class DubsInterop
    {
        internal static bool initialized;
        internal static Type componentType;
        internal static MethodInfo getColourMethod;

        internal static void EnsureInit()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            componentType = GenTypes.GetTypeInAnyAssembly("DubRoss.MapComponent_PaintShop");
            if (componentType == null)
            {
                return;
            }
            getColourMethod = componentType.GetMethod("GetColourAlpha", new[] { typeof(IntVec3) })
                ?? componentType.GetMethod("GetColour", new[] { typeof(IntVec3) });
        }

        internal static bool TryGetFloorColor(Map map, IntVec3 cell, out Color color)
        {
            color = default;
            EnsureInit();
            if (getColourMethod == null)
            {
                return false;
            }
            MapComponent comp = map.GetComponent(componentType);
            if (comp == null)
            {
                return false;
            }
            object result = getColourMethod.Invoke(comp, new object[] { cell });
            if (!(result is Color c) || c.a <= 0f)
            {
                return false;
            }
            color = new Color(c.r, c.g, c.b, 1f);
            return true;
        }
    }
}
