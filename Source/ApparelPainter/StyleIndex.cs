using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApparelPainter
{
    /// <summary>
    /// One stop on a row's style cycle: the ThingStyleDef, the category it
    /// came from (null for a randomStyle variant, which belongs to none),
    /// and the name we show. The "no style" stop is NOT in here — callers
    /// prepend it, because null is the engine's own representation of it.
    /// </summary>
    internal class StyleOption
    {
        internal ThingStyleDef Style;
        internal StyleCategoryDef Category;
        internal string Label;

        /// <summary>Menu icon: the category glyph players know from
        /// ideoligion creation, else the style's own resolved icon.</summary>
        internal Texture2D Icon => Category != null ? Category.Icon : Style?.UIIcon;
    }

    /// <summary>
    /// ThingDef → the styles that exist for it. The engine has no such
    /// index: `StyleCategoryDef.GetStyleForThingDef` walks one category,
    /// `Ideo.style.StyleForThingDef` is scoped to one ideoligion, and
    /// `ThingStyleDef.Category` scans every category to answer for one
    /// style. Nothing anywhere answers "what could this garment look
    /// like", because until now nothing needed to — styles are assigned at
    /// craft time from the crafter's ideo (`GenRecipe:106`), rolled from
    /// `randomStyle`, or baked in at world generation, and the player is
    /// never asked.
    ///
    /// Two sources, in this order, and the order IS the cycle order:
    ///
    /// 1. Every `StyleCategoryDef.thingDefStyles` entry, in DefDatabase
    ///    order — the ideoligion looks (Spikecore, Totemic, Morbid, and
    ///    whatever VIE-Memes-and-Structures or a wardrobe mod adds).
    /// 2. Every def's own `randomStyle` list — Anomaly's four ritual-mask
    ///    faces and Royalty's samurai helmet, which belong to no category
    ///    and are therefore invisible to every category-based lookup.
    ///
    /// DefDatabase order is stable for a given modlist, so a player's
    /// muscle memory for "click twice for Totemic" holds across sessions.
    ///
    /// Not filtered to apparel. The tab only ever asks about things it
    /// lists, and a stand's parked weapon renders from `Thing.Graphic`,
    /// which is style-aware the same way — so a styled bow costs no extra
    /// code and excluding it would take some.
    ///
    /// Built lazily on first use rather than in a static constructor:
    /// nothing needs it before a tab draws, and lazy sidesteps any question
    /// about DefDatabase readiness at startup.
    /// </summary>
    internal static class StyleIndex
    {
        internal static readonly Dictionary<ThingDef, List<StyleOption>> byDef =
            new Dictionary<ThingDef, List<StyleOption>>();

        internal static readonly List<StyleOption> none = new List<StyleOption>();

        internal static bool built;

        /// <summary>The styles for a def, never null, in cycle order.</summary>
        internal static List<StyleOption> For(ThingDef def)
        {
            EnsureBuilt();
            if (def == null)
            {
                return none;
            }
            List<StyleOption> found;
            return byDef.TryGetValue(def, out found) ? found : none;
        }

        internal static void EnsureBuilt()
        {
            if (built)
            {
                return;
            }
            built = true;

            // Without Ideology the category defs never load and this loop
            // is empty — which is the whole no-hard-DLC-requirement story:
            // the randomStyle pass below still finds the Anomaly and
            // Royalty variants.
            foreach (StyleCategoryDef cat in DefDatabase<StyleCategoryDef>.AllDefsListForReading)
            {
                List<ThingDefStyle> entries = cat.thingDefStyles;
                if (entries == null)
                {
                    continue;
                }
                for (int i = 0; i < entries.Count; i++)
                {
                    ThingDefStyle entry = entries[i];
                    if (entry?.ThingDef != null && entry.StyleDef != null)
                    {
                        Add(entry.ThingDef, entry.StyleDef, cat);
                    }
                }
            }

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                List<ThingStyleChance> roll = def.randomStyle;
                if (roll == null)
                {
                    continue;
                }
                for (int i = 0; i < roll.Count; i++)
                {
                    if (roll[i]?.StyleDef != null)
                    {
                        Add(def, roll[i].StyleDef, null);
                    }
                }
            }

            foreach (KeyValuePair<ThingDef, List<StyleOption>> pair in byDef)
            {
                NameOptions(pair.Value);
            }
        }

        internal static void Add(ThingDef def, ThingStyleDef style, StyleCategoryDef category)
        {
            List<StyleOption> list;
            if (!byDef.TryGetValue(def, out list))
            {
                list = new List<StyleOption>();
                byDef[def] = list;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Style == style)
                {
                    return; // a style listed by both a category and randomStyle
                }
            }
            list.Add(new StyleOption { Style = style, Category = category });
        }

        /// <summary>
        /// Names every option in one def's list. Style defs ship no
        /// `label` — checked across vanilla and every subscribed mod, not
        /// one sets it — so the name has to be derived, and the character
        /// editor's habit of showing the raw defName ("Spikecore_Duster")
        /// is what this exists to avoid.
        ///
        /// Category label first, because that is the word players already
        /// know from ideoligion creation. `overrideLabel` beats it when
        /// set, since that is the item's actual in-game name (vanilla sets
        /// it exactly once: PrestigeMarineHelmet_Samurai → "samurai
        /// helmet"). Failing both, split the defName's tail — the Anomaly
        /// masks are category-less, and CultistMask_TwistedMask reads
        /// perfectly well as "Twisted mask".
        /// </summary>
        internal static void NameOptions(List<StyleOption> options)
        {
            for (int i = 0; i < options.Count; i++)
            {
                options[i].Label = PreferredLabel(options[i]);
            }

            // Two styles from one category on ONE def is legal and common:
            // vanilla's Ideogram carries an A and a B variant in every
            // category (Hindu_IdeogramA, Hindu_IdeogramB, and the same pair
            // for Christian, Islamic, Morbid…). Both take the category
            // label, so both collide.
            //
            // Disambiguate by APPENDING the derived tail, never by replacing
            // the label with it. The first version replaced — which fixed
            // the clash inside Hindu and immediately created a worse one
            // across categories, because every category's B variant derives
            // to the same "Ideogram B". The category is the only thing
            // making those distinct, so it has to stay. Caught by the
            // harness, invisible to the eye.
            for (int i = 0; i < options.Count; i++)
            {
                bool collides = false;
                for (int j = 0; j < options.Count; j++)
                {
                    if (j != i && options[j].Label == options[i].Label)
                    {
                        collides = true;
                        break;
                    }
                }
                if (!collides)
                {
                    continue;
                }
                string derived = DerivedLabel(options[i].Style);
                options[i].Label = options[i].Category != null
                    ? options[i].Category.LabelCap + " — " + derived
                    : derived;
            }
        }

        internal static string PreferredLabel(StyleOption option)
        {
            if (!option.Style.overrideLabel.NullOrEmpty())
            {
                return option.Style.overrideLabel.CapitalizeFirst();
            }
            if (!option.Style.label.NullOrEmpty())
            {
                return option.Style.LabelCap;
            }
            if (option.Category != null)
            {
                return option.Category.LabelCap;
            }
            return DerivedLabel(option.Style);
        }

        /// <summary>"CultistMask_TwistedMask" → "Twisted mask". The tail
        /// after the last underscore is the variant; the prefix repeats the
        /// ThingDef the player is already looking at.</summary>
        internal static string DerivedLabel(ThingStyleDef style)
        {
            string name = style.defName;
            int cut = name.LastIndexOf('_');
            if (cut >= 0 && cut < name.Length - 1)
            {
                name = name.Substring(cut + 1);
            }
            return GenText.SplitCamelCase(name).ToLower().CapitalizeFirst();
        }
    }
}
