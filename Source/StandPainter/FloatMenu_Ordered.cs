using System.Collections.Generic;
using Verse;

namespace StandPainter
{
    /// <summary>
    /// FloatMenu re-sorts its options by Priority — and a disabled option
    /// (action == null, our category headers) hard-ranks DisabledOption,
    /// the lowest, so headers sink to the bottom in a block instead of
    /// separating their sections. The options field is protected, so this
    /// subclass simply restores the caller's order after the base ctor has
    /// done its sorting and size-mode setup. Display order is the only
    /// thing the list order affects.
    /// </summary>
    public class FloatMenu_Ordered : FloatMenu
    {
        public FloatMenu_Ordered(List<FloatMenuOption> optionsInOrder)
            : base(optionsInOrder)
        {
            options = optionsInOrder;
        }
    }
}
