using System;
using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Items;
using AlphaTown.Gameplay.Inventory;
using AlphaTown.Gameplay.Production;
using AlphaTown.Gameplay.Saving;

namespace AlphaTown.Gameplay.World
{
    /// <summary>
    /// Root of the simulation: the barn, every production building, and the save round trip.
    ///
    /// Headless by construction — no MonoBehaviour, no scene, no Unity API. A test builds one
    /// with a manual clock and can play out a week of production in a millisecond.
    /// </summary>
    public sealed class GameWorld : ITickable
    {
        /// <summary>Bump on every breaking save change and add the matching migration.</summary>
        public const int SaveSchemaVersion = 1;

        public const string DefaultSaveSlot = "player";

        /// <summary>
        /// Polling cadence for completion checks. Timers are timestamp-derived, so this only
        /// decides how soon the UI notices — a per-frame sync would burn battery for nothing.
        /// </summary>
        const float SyncIntervalSeconds = 1f;

        readonly IGameDatabase _database;
        readonly IGameClock _clock;
        readonly IEventBus _events;
        readonly List<Producer> _producers = new List<Producer>(16);
        readonly Dictionary<string, Producer> _producersByInstanceId = new Dictionary<string, Producer>(16);

        float _secondsSinceSync;

        public GameWorld(IGameDatabase database, IGameClock clock, IEventBus events)
        {
            _database = Guard.NotNull(database, nameof(database));
            _clock = Guard.NotNull(clock, nameof(clock));
            _events = Guard.NotNull(events, nameof(events));

            var storage = database.DefaultStorage;
            if (storage == null)
                throw new InvalidOperationException("GameDatabase has no default storage assigned.");

            Barn = new BarnInventory(database, storage, events);
        }

        public BarnInventory Barn { get; }

        public IReadOnlyList<Producer> Producers => _producers;

        /// <summary>Places a production building. Instance ids must be unique within a town.</summary>
        public Producer AddProducer(string instanceId, string definitionId)
        {
            Guard.NotNullOrEmpty(instanceId, nameof(instanceId));

            if (_producersByInstanceId.ContainsKey(instanceId))
            {
                Log.Error("World", "Producer instance '" + instanceId + "' already exists.");
                return _producersByInstanceId[instanceId];
            }

            if (!_database.TryGetProducer(definitionId, out var definition))
            {
                Log.Error("World", "Unknown producer definition '" + definitionId + "'.");
                return null;
            }

            var producer = new Producer(instanceId, definition, _database, _clock, _events);
            _producers.Add(producer);
            _producersByInstanceId.Add(instanceId, producer);
            return producer;
        }

        public bool TryGetProducer(string instanceId, out Producer producer) =>
            _producersByInstanceId.TryGetValue(instanceId ?? string.Empty, out producer);

        /// <summary>
        /// Brings the whole town up to date with the clock. Call on load, on app resume, and
        /// periodically while running.
        /// </summary>
        public void Sync()
        {
            for (var i = 0; i < _producers.Count; i++) _producers[i].Sync();
        }

        public void Tick(float deltaSeconds)
        {
            _secondsSinceSync += deltaSeconds;
            if (_secondsSinceSync < SyncIntervalSeconds) return;

            _secondsSinceSync = 0f;
            Sync();
        }

        public GameSaveData CaptureSave()
        {
            var save = new GameSaveData
            {
                Inventory = new InventorySaveData
                {
                    Level = Barn.Level,
                    Stacks = ToStackData(Barn.Contents)
                },
                Producers = new ProducerSaveData[_producers.Count]
            };

            for (var i = 0; i < _producers.Count; i++)
            {
                var producer = _producers[i];
                save.Producers[i] = new ProducerSaveData
                {
                    InstanceId = producer.InstanceId,
                    DefinitionId = producer.DefinitionId,
                    Level = producer.Level,
                    Orders = ToArray(producer.Orders),
                    Ready = ToStackData(producer.Ready)
                };
            }

            return save;
        }

        /// <summary>
        /// Restores a save and catches up. Offline progression happens here and costs nothing:
        /// every timer is absolute, so <see cref="Sync"/> resolves days of absence in one pass.
        /// </summary>
        public void RestoreSave(GameSaveData save)
        {
            Guard.NotNull(save, nameof(save));

            var inventory = save.Inventory ?? new InventorySaveData();
            Barn.ResetTo(inventory.Level, ToStacks(inventory.Stacks));

            _producers.Clear();
            _producersByInstanceId.Clear();

            var producers = save.Producers;
            if (producers != null)
            {
                for (var i = 0; i < producers.Length; i++)
                {
                    var data = producers[i];
                    if (data == null || string.IsNullOrEmpty(data.InstanceId)) continue;

                    var producer = AddProducer(data.InstanceId, data.DefinitionId);
                    if (producer == null) continue; // Definition removed since the save was written.

                    producer.RestoreState(data.Level, data.Orders, ToStacks(data.Ready));
                }
            }

            Sync();
        }

        static ItemStackSaveData[] ToStackData(IReadOnlyDictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0) return Array.Empty<ItemStackSaveData>();

            var result = new ItemStackSaveData[counts.Count];
            var index = 0;
            foreach (var pair in counts)
            {
                result[index++] = new ItemStackSaveData { ItemId = pair.Key, Count = pair.Value };
            }

            return result;
        }

        static ItemStackSaveData[] ToStackData(IReadOnlyList<ItemStack> stacks)
        {
            if (stacks == null || stacks.Count == 0) return Array.Empty<ItemStackSaveData>();

            var result = new ItemStackSaveData[stacks.Count];
            for (var i = 0; i < stacks.Count; i++)
            {
                result[i] = new ItemStackSaveData { ItemId = stacks[i].ItemId, Count = stacks[i].Count };
            }

            return result;
        }

        static List<ItemStack> ToStacks(ItemStackSaveData[] data)
        {
            var stacks = new List<ItemStack>(data != null ? data.Length : 0);
            if (data == null) return stacks;

            for (var i = 0; i < data.Length; i++)
            {
                if (data[i] == null || string.IsNullOrEmpty(data[i].ItemId)) continue;
                stacks.Add(new ItemStack(data[i].ItemId, data[i].Count));
            }

            return stacks;
        }

        static ProductionOrder[] ToArray(IReadOnlyList<ProductionOrder> orders)
        {
            if (orders == null || orders.Count == 0) return Array.Empty<ProductionOrder>();

            var result = new ProductionOrder[orders.Count];
            for (var i = 0; i < orders.Count; i++) result[i] = orders[i];
            return result;
        }
    }
}
