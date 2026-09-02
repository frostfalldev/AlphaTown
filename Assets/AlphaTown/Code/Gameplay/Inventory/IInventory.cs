using System.Collections.Generic;
using AlphaTown.Data.Items;

namespace AlphaTown.Gameplay.Inventory
{
    /// <summary>
    /// The barn. Space-limited rather than slot-limited: each item costs
    /// <see cref="IItemDefinition.StorageCost"/> units of a shared pool, which is what lets a
    /// barn upgrade read as "more room" instead of "more slots".
    /// </summary>
    public interface IInventory
    {
        int Level { get; }
        int Capacity { get; }
        int UsedSpace { get; }
        int FreeSpace { get; }

        IReadOnlyDictionary<string, int> Contents { get; }

        int CountOf(string itemId);
        bool Contains(string itemId, int count);
        bool ContainsAll(IReadOnlyList<ItemStack> stacks);

        /// <summary>How many units of this item would still fit.</summary>
        int RoomFor(string itemId);

        /// <summary>Adds what fits and returns the amount actually stored.</summary>
        int Add(string itemId, int count);

        /// <summary>All-or-nothing add. Leaves the inventory untouched when it will not fit.</summary>
        bool TryAddExact(string itemId, int count);

        bool TryRemove(string itemId, int count);

        /// <summary>All-or-nothing removal, for paying a recipe's inputs.</summary>
        bool TryRemoveAll(IReadOnlyList<ItemStack> stacks);
    }
}
