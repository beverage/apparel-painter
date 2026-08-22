using UnityEngine;
using Verse;

namespace StandPainter
{
    /// <summary>
    /// Texture handles. Textures must load on the main thread at startup,
    /// hence the StaticConstructorOnStartup class (engine requirement).
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class StandPainterTex
    {
        /// <summary>Eyedropper overlay for tab swatches while a picker is
        /// open. Light fill + dark outline so it reads on any swatch hue.</summary>
        internal static readonly Texture2D Dropper = ContentFinder<Texture2D>.Get("StandPainter/UI/Dropper");
    }
}
