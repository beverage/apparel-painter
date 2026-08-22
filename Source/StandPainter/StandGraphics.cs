using System.Reflection;
using RimWorld;
using Verse;

namespace StandPainter
{
    /// <summary>
    /// Reflection bridge to Building_OutfitStand.RecacheGraphics (private).
    /// The stand bakes apparel.DrawColor into cached Graphics at
    /// add/remove/spawn only — every colour write MUST be followed by this
    /// call or the stand renders the old colour until reload (AGENTS
    /// invariant #1). The def is RealtimeOnly, so once the cache is rebuilt
    /// the map shows the new colour the same frame.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class StandGraphics
    {
        internal static readonly MethodInfo recacheMethod =
            typeof(Building_OutfitStand).GetMethod("RecacheGraphics", BindingFlags.Instance | BindingFlags.NonPublic);

        static StandGraphics()
        {
            // Surface a game-version breakage at load, not at first paint.
            if (recacheMethod == null)
            {
                Log.Error("[StandPainter] Building_OutfitStand.RecacheGraphics not found — painted stands will not refresh until a reload. Please report this with your game version.");
            }
        }

        internal static void Recache(Building_OutfitStand stand)
        {
            if (stand == null || stand.Destroyed)
            {
                return;
            }
            recacheMethod?.Invoke(stand, null);
        }
    }
}
