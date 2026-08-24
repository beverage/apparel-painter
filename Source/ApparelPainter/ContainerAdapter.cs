using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace ApparelPainter
{
    /// <summary>
    /// The seam that makes the mod "wherever apparel sits" (DEC-037): one
    /// adapter per family of apparel-holding building. First match wins;
    /// stands and racks are container models (unspawned items, baked
    /// graphics needing a refresh), generic storage is the spawned model
    /// (the map repaints items itself via Notify_ColorChanged, and the
    /// dropper's cell scan already sees them — hence ItemsSpawned).
    /// </summary>
    internal abstract class ContainerAdapter
    {
        internal static readonly List<ContainerAdapter> All = new List<ContainerAdapter>
        {
            new StandAdapter(),
            new ArmorRackAdapter(),
            new StorageAdapter(),
        };

        internal static ContainerAdapter For(Thing building)
        {
            if (building == null)
            {
                return null;
            }
            foreach (ContainerAdapter adapter in All)
            {
                if (adapter.Handles(building))
                {
                    return adapter;
                }
            }
            return null;
        }

        internal abstract bool Handles(Thing building);

        /// <summary>The rows the Paint tab lists and the picker targets.</summary>
        internal abstract IEnumerable<Thing> ListedItems(Thing building);

        /// <summary>Called after every colour write batch.</summary>
        internal virtual void Refresh(Thing building)
        {
        }

        /// <summary>True when listed items are spawned map things — the
        /// dropper's cell scan surfaces them on its own.</summary>
        internal virtual bool ItemsSpawned => false;

        /// <summary>Stands and racks are apparel furniture and always show
        /// the tab; generic storage only when it actually holds apparel, so
        /// food crates and fridges stay clean (DEC-037).</summary>
        internal virtual bool TabVisible(Thing building)
        {
            return true;
        }
    }

    internal class StandAdapter : ContainerAdapter
    {
        internal override bool Handles(Thing building)
        {
            return building is Building_OutfitStand;
        }

        internal override IEnumerable<Thing> ListedItems(Thing building)
        {
            return ((Building_OutfitStand)building).HeldItems;
        }

        internal override void Refresh(Thing building)
        {
            StandGraphics.Recache((Building_OutfitStand)building);
        }
    }

    /// <summary>
    /// Armor Racks (khamenman.armorracks) — container model with the
    /// stand's bake pattern: a cached ApparelGraphics list behind a public
    /// IsApparelResolved flag (verified from its shipped Source,
    /// 2026-08-23). Enumeration is vanilla IThingHolder — no reflection;
    /// the refresh is two cached public FieldInfos. DubsInterop posture:
    /// present-only, degrades silently.
    /// </summary>
    internal class ArmorRackAdapter : ContainerAdapter
    {
        internal static readonly Type RackType = GenTypes.GetTypeInAnyAssembly("ArmorRacks.Things.ArmorRack");
        internal static readonly FieldInfo DrawerField = RackType?.GetField("ContentsDrawer");
        internal static readonly FieldInfo ResolvedField = DrawerField?.FieldType.GetField("IsApparelResolved");

        internal override bool Handles(Thing building)
        {
            return RackType != null && RackType.IsInstanceOfType(building);
        }

        internal override IEnumerable<Thing> ListedItems(Thing building)
        {
            ThingOwner held = (building as IThingHolder)?.GetDirectlyHeldThings();
            if (held == null)
            {
                yield break;
            }
            foreach (Thing t in held)
            {
                yield return t;
            }
        }

        internal override void Refresh(Thing building)
        {
            if (DrawerField == null || ResolvedField == null)
            {
                return;
            }
            object drawer = DrawerField.GetValue(building);
            if (drawer != null)
            {
                ResolvedField.SetValue(drawer, false);
            }
        }
    }

    /// <summary>
    /// Any vanilla-style storage (Building_Storage and subclasses: shelves,
    /// [sbz] Neat Storage, most storage mods). Items are SPAWNED in the
    /// building's cells — Dubs can paint them en masse; we are the
    /// fine-grained per-item layer. Rows are everything PAINTABLE
    /// (CompColorable — apparel plus the odd colorable shield or modded
    /// item), which keeps the tab truthful: wherever it appears, what it
    /// lists can be painted, and bulk goods never summon it. Refresh
    /// handles the ASF bake; plain vanilla-drawn items repaint via
    /// Notify_ColorChanged on their own.
    /// </summary>
    internal class StorageAdapter : ContainerAdapter
    {
        internal override bool Handles(Thing building)
        {
            return building is Building_Storage;
        }

        internal override IEnumerable<Thing> ListedItems(Thing building)
        {
            SlotGroup group = ((Building_Storage)building).slotGroup;
            if (group?.HeldThings == null)
            {
                yield break;
            }
            foreach (Thing t in group.HeldThings)
            {
                if (t.TryGetComp<CompColorable>() != null)
                {
                    yield return t;
                }
            }
        }

        internal override bool ItemsSpawned => true;

        internal override void Refresh(Thing building)
        {
            // Plain spawned items repaint via Notify_ColorChanged on their
            // own — but ASF-family storage (sbz Neat Storage, Reel's, ...)
            // bakes per-item print data that a colour change never
            // invalidates. Poke its renderer; a no-op for vanilla shelves.
            AsfInterop.TryRefresh(building);
        }

        internal override bool TabVisible(Thing building)
        {
            foreach (Thing _ in ListedItems(building))
            {
                return true;
            }
            return false;
        }
    }
}
