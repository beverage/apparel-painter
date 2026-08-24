using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ApparelPainter
{
    /// <summary>
    /// Mod entry: owns the settings (saved swatches). Deliberately no
    /// settings window — SettingsCategory stays empty, so no Options entry
    /// appears; swatches are managed inside the picker (the + cell saves,
    /// right-click removes).
    /// </summary>
    public class ApparelPainterMod : Mod
    {
        internal static ApparelPainterMod Instance;

        internal static ApparelPainterSettings Settings => Instance.settings;

        internal readonly ApparelPainterSettings settings;

        public ApparelPainterMod(ModContentPack content) : base(content)
        {
            Instance = this;
            settings = GetSettings<ApparelPainterSettings>();
        }
    }

    /// <summary>
    /// Config-file state, NOT save-file state — the mod still scribes
    /// nothing into saves (the clean-removal invariant holds). Color is a
    /// registered ParseHelper value type (ParseHelper.cs:389), so
    /// LookMode.Value round-trips the list.
    /// </summary>
    public class ApparelPainterSettings : ModSettings
    {
        internal List<Color> savedSwatches = new List<Color>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref savedSwatches, "savedSwatches", LookMode.Value);
            if (savedSwatches == null)
            {
                savedSwatches = new List<Color>();
            }
        }
    }
}
