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
    /// this existed.
    ///
    /// Two getters, BOTH needed (verified against the 1.6 assembly,
    /// 2026-08-23): GetColourAlpha flags "painted" via a fourth alpha grid
    /// and so can even represent true-black paint — but Dubs' ExposeData
    /// scribes R/G/B (and the to-paint trio) WITHOUT the A grid, so after
    /// any reload the alpha layer is reconstructed empty and that getter
    /// returns clear for every painted cell. Plain GetColour keys on the
    /// scribed RGB depths and survives reload for everything except literal
    /// (0,0,0) — which the save round-trip makes indistinguishable from
    /// unpainted in Dubs' own data (unset cells init to -1 but scribe-clamp
    /// to 0), a limitation Dubs itself shares. Named "blacks" (3C3C3C etc.)
    /// read fine. Everything degrades silently when the mod is absent or
    /// its internals change.
    /// </summary>
    internal static class DubsInterop
    {
        internal static bool initialized;
        internal static Type componentType;
        internal static MethodInfo getColourAlphaMethod;
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
            getColourAlphaMethod = componentType.GetMethod("GetColourAlpha", new[] { typeof(IntVec3) });
            getColourMethod = componentType.GetMethod("GetColour", new[] { typeof(IntVec3) });
        }

        internal static bool TryGetFloorColor(Map map, IntVec3 cell, out Color color)
        {
            color = default;
            EnsureInit();
            if (componentType == null)
            {
                return false;
            }
            MapComponent comp = map.GetComponent(componentType);
            if (comp == null)
            {
                return false;
            }
            // Both getters return an opaque colour when they consider the
            // cell painted and Color.clear otherwise, so alpha is the
            // painted-test for each.
            if (TryInvoke(getColourAlphaMethod, comp, cell, out color))
            {
                return true;
            }
            return TryInvoke(getColourMethod, comp, cell, out color);
        }

        internal static bool TryInvoke(MethodInfo method, MapComponent comp, IntVec3 cell, out Color color)
        {
            color = default;
            if (method == null)
            {
                return false;
            }
            object result = method.Invoke(comp, new object[] { cell });
            if (!(result is Color c) || c.a <= 0f)
            {
                return false;
            }
            color = new Color(c.r, c.g, c.b, 1f);
            return true;
        }
    }
}
