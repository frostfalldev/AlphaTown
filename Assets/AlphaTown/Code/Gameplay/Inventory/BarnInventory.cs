using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Items;
using AlphaTown.Data.Storage;

namespace AlphaTown.Gameplay.Inventory
{
    /// <summary>
    /// Space-limited item storage with an upgradeable capacity curve.
    ///
    /// Plain C#: no MonoBehaviour, no statics, no Unity API. Constructing one in a test costs
    /// nothing, which is the point of keeping the simulation out of the scene graph.
    /// </summary>
    public sealed class BarnInventory : IInventory
    {
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>(32);
        readonly IGameDatabase _database;
        readonly IStorageDefinition _storage;
        readonly IEventBus _events;

        int _level = 1;
        int _usedSpace;

        public BarnInventory(IGameDatabase database, IStorageDefinition storage, IEventBus events, int level = 1)
        {
            _database = Guard.NotNull(database, nameof(database));
            _storage = Guard.NotNull(storage, nameof(storage));
            _events = Guard.NotNull(events, nameof(events));
            _level = level < 1 ? 1 : level;
        }

        public int Level => _level;
        public int Capacity => _storage.GetCapacity(_level);
        public int UsedSpace => _usedSpace;
        public int FreeSpace => Capacity - _usedSpace;
        public IReadOnlyDictionary<string, int> Contents => _counts;

        public int CountOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            return _counts.TryGetValue(itemId, out var count) ? count : 0;
        }

        public bool Contains(string itemId, int count) => count <= 0 || CountOf(itemId) >= count;

        public bool ContainsAll(IReadOnlyList<ItemStack> stacks)
        {
            if (stacks == null) return true;

            for (var i = 0; i < stacks.Count; i++)
            {
                if (!Contains(stacks[i].ItemId, stacks[i].Count)) return false;
            }

            return true;
        }

        public int RoomFor(string itemId)
        {
            var cost = StorageCostOf(itemId);
            if (cost <= 0) return int.MaxValue; // Non-storable items bypass the barn entirely.

            var free = FreeSpace;
            return free <= 0 ? 0 : free / cost;
        }

        public int Add(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return 0;

            var room = RoomFor(itemId);
            var accepted = count < room ? count : room;
            var rejected = count - accepted;

            if (accepted > 0)
            {
                ApplyDelta(itemId, accepted);
            }

            if (rejected > 0)
            {
                _events.Publish(new InventoryOverflowEvent(itemId, rejected));
            }

            return accepted;
        }

        public bool TryAddExact(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (RoomFor(itemId) < count) return false;

            ApplyDelta(itemId, count);
            return true;
        }

        public bool TryRemove(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (CountOf(itemId) < count) return false;

            ApplyDelta(itemId, -count);
            return true;
        }

        public bool TryRemoveAll(IReadOnlyList<ItemStack> stacks)
        {
            if (stacks == null || stacks.Count == 0) return true;
            if (!ContainsAll(stacks)) return false;

            // Checked everything first, so this half cannot fail partway through.
            for (var i = 0; i < stacks.Count; i++)
            {
                ApplyDelta(stacks[i].ItemId, -stacks[i].Count);
            }

            return true;
        }

        /// <summary>Barn upgrade. Capacity can only grow, so stored items are never orphaned.</summary>
        public void SetLevel(int level)
        {
            var clamped = level < 1 ? 1 : (level > _storage.MaxLevel ? _storage.MaxLevel : level);
            if (clamped == _level) return;

            _level = clamped;
            _events.Publish(new InventorySpaceChangedEvent(_usedSpace, Capacity));
        }

        /// <summary>Restores contents from save without firing per-item events.</summary>
        public void ResetTo(int level, IReadOnlyList<ItemStack> stacks)
        {
            _counts.Clear();
            _usedSpace = 0;
            _level = level < 1 ? 1 : level;

            if (stacks != null)
            {
                for (var i = 0; i < stacks.Count; i++)
                {
                    var stack = stacks[i];
                    if (stack.IsEmpty) continue;

                    _counts[stack.ItemId] = CountOf(stack.ItemId) + stack.Count;
                    _usedSpace += stack.Count * StorageCostOf(stack.ItemId);
                }
            }

            _events.Publish(new InventorySpaceChangedEvent(_usedSpace, Capacity));
        }

        void ApplyDelta(string itemId, int delta)
        {
            var updated = CountOf(itemId) + delta;

            if (updated <= 0) _counts.Remove(itemId);
            else _counts[itemId] = updated;

            _usedSpace += delta * StorageCostOf(itemId);
            if (_usedSpace < 0) _usedSpace = 0;

            _events.Publish(new InventoryChangedEvent(itemId, updated > 0 ? updated : 0, delta));
            _events.Publish(new InventorySpaceChangedEvent(_usedSpace, Capacity));
        }

        int StorageCostOf(string itemId)
        {
            if (!_database.TryGetItem(itemId, out var item))
            {
                // An unknown id means content and save data disagree. Charge a unit so the barn
                // cannot be exploited, and make the mismatch loud.
                Log.Error("Inventory", "Unknown item id '" + itemId + "'. Treating storage cost as 1.");
                return 1;
            }

            return item.IsStorable ? (item.StorageCost < 1 ? 1 : item.StorageCost) : 0;
        }
    }
}
