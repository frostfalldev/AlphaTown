using System;
using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;
using AlphaTown.Gameplay.Economy;
using AlphaTown.Gameplay.Inventory;
using AlphaTown.Gameplay.Orders;
using AlphaTown.Gameplay.Production;
using AlphaTown.Gameplay.Progression;
using AlphaTown.Gameplay.Saving;

namespace AlphaTown.Gameplay.World
{
    /// <summary>
    /// Root of the simulation: the barn, the wallet, town progression, the order boards and every
    /// production building, plus the save round trip.
    ///
    /// This is where the economic loop is wired together — produce, deliver, earn, level up,
    /// unlock, produce more — and it is entirely headless. No MonoBehaviour, no scene, no Unity
    /// API. A test builds one with a manual clock and plays out a week in a millisecond.
    /// </summary>
    public sealed class GameWorld : ITickable
    {
        /// <summary>
        /// Bump on the first breaking save change *after* launch, and add the matching migration.
        /// Pre-launch there are no saves in the wild to migrate, so bumping now would only create
        /// migration debt for saves that do not exist.
        /// </summary>
        public const int SaveSchemaVersion = 1;

        public const string DefaultSaveSlot = "player";

        /// <summary>
        /// Polling cadence for completion and expiry checks. Timers are timestamp-derived, so this
        /// only decides how soon the UI notices — a per-frame sync would burn battery for nothing.
        /// </summary>
        const float SyncIntervalSeconds = 1f;

        const int HelicopterBoardCapacity = 4;

        readonly IGameDatabase _database;
        readonly IGameClock _clock;
        readonly IEventBus _events;
        readonly CurrencyLedger _ledger = new CurrencyLedger();
        readonly List<Producer> _producers = new List<Producer>(16);
        readonly Dictionary<string, Producer> _producersByInstanceId = new Dictionary<string, Producer>(16);

        float _secondsSinceSync;

        /// <param name="random">
        /// Drives order generation. Injected so tests are deterministic; generated orders are
        /// persisted, so nothing depends on reproducing a sequence across sessions.
        /// </param>
        public GameWorld(IGameDatabase database, IGameClock clock, IEventBus events, Random random = null)
        {
            _database = Guard.NotNull(database, nameof(database));
            _clock = Guard.NotNull(clock, nameof(clock));
            _events = Guard.NotNull(events, nameof(events));

            // Fail at construction rather than shipping a build where half the economy silently
            // does nothing. Each of these is required for the loop to close.
            var storage = Require(database.DefaultStorage, "default storage");
            var curve = Require(database.ProgressionCurve, "progression curve");
            Require(database.SoftCurrency, "soft currency");

            var wallet = new Wallet(database, clock, events, _ledger);
            var progression = new TownProgression(curve, wallet, events);

            Barn = new BarnInventory(database, storage, events);
            Wallet = wallet;
            Progression = progression;

            var generator = new OrderGenerator(database, random ?? new Random());
            HelicopterOrders = new OrderBoard(
                OrderKind.Helicopter, HelicopterBoardCapacity,
                clock, events, progression, Barn, wallet, generator);
        }

        public BarnInventory Barn { get; }

        public Wallet Wallet { get; }

        /// <summary>Lifetime source/sink totals. Read-only view for analytics and debug.</summary>
        public ICurrencyLedger Ledger => _ledger;

        public TownProgression Progression { get; }

        /// <summary>TODO: train and ship boards join this as separate OrderBoard instances.</summary>
        public OrderBoard HelicopterOrders { get; }

        public IReadOnlyList<Producer> Producers => _producers;

        /// <summary>Seeds a brand-new town: starting balances, then a first set of orders.</summary>
        public void InitialiseNewPlayer()
        {
            this.Wallet.InitialiseNewPlayer();
            Sync();
        }

        /// <summary>Places a production building. Instance ids must be unique within a town.</summary>
        public Producer AddProducer(string instanceId, string definitionId)
        {
            Guard.NotNullOrEmpty(instanceId, nameof(instanceId));

            if (_producersByInstanceId.TryGetValue(instanceId, out var existing))
            {
                Log.Error("World", "Producer instance '" + instanceId + "' already exists.");
                return existing;
            }

            if (!_database.TryGetProducer(definitionId, out var definition))
            {
                Log.Error("World", "Unknown producer definition '" + definitionId + "'.");
                return null;
            }

            var producer = new Producer(instanceId, definition, _database, _clock, _events, this.Progression);
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
            HelicopterOrders.Sync();
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
                Wallet = new WalletSaveData
                {
                    Balances = ToCurrencyData(this.Wallet.Snapshot()),
                    Ledger = ToLedgerData(_ledger.Snapshot())
                },
                Progression = new ProgressionSaveData
                {
                    Level = this.Progression.TownLevel,
                    XpIntoLevel = this.Progression.XpIntoLevel,
                    TotalXp = this.Progression.TotalXp,
                    Attribution = ToAttributionData(this.Progression.SnapshotAttribution())
                },
                Producers = new ProducerSaveData[_producers.Count],
                OrderBoards = new[] { ToBoardData(HelicopterOrders) }
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
        /// every timer is absolute, so <see cref="Sync"/> resolves days of absence in one pass —
        /// production finishes, and time-limited orders that ran out are expired and replaced.
        /// </summary>
        public void RestoreSave(GameSaveData save)
        {
            Guard.NotNull(save, nameof(save));

            var inventory = save.Inventory ?? new InventorySaveData();
            Barn.ResetTo(inventory.Level, ToStacks(inventory.Stacks));

            var wallet = save.Wallet ?? new WalletSaveData();
            this.Wallet.ResetTo(ToCurrencyAmounts(wallet.Balances));
            _ledger.RestoreFrom(ToLedgerEntries(wallet.Ledger));

            var progression = save.Progression ?? new ProgressionSaveData();
            this.Progression.RestoreState(
                progression.Level, progression.XpIntoLevel, progression.TotalXp,
                ToAttributionEntries(progression.Attribution));

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

            RestoreBoards(save.OrderBoards);
            Sync();
        }

        void RestoreBoards(OrderBoardSaveData[] boards)
        {
            if (boards == null) return;

            for (var i = 0; i < boards.Length; i++)
            {
                var data = boards[i];
                if (data == null || (OrderKind)data.Kind != HelicopterOrders.Kind) continue;

                HelicopterOrders.RestoreState(ToOrders(data.Orders), data.NextOrderNumber);
                return;
            }
        }

        static TValue Require<TValue>(TValue value, string label) where TValue : class
        {
            if (value != null) return value;

            throw new InvalidOperationException(
                "GameDatabase has no " + label + " assigned. The economy cannot run without it.");
        }

        // --- Runtime to save ------------------------------------------------------------------

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

        static CurrencyAmountSaveData[] ToCurrencyData(IReadOnlyList<CurrencyAmount> amounts)
        {
            if (amounts == null || amounts.Count == 0) return Array.Empty<CurrencyAmountSaveData>();

            var result = new CurrencyAmountSaveData[amounts.Count];
            for (var i = 0; i < amounts.Count; i++)
            {
                result[i] = new CurrencyAmountSaveData
                {
                    CurrencyId = amounts[i].CurrencyId,
                    Amount = amounts[i].Amount
                };
            }

            return result;
        }

        static LedgerEntrySaveData[] ToLedgerData(IReadOnlyList<LedgerEntry> entries)
        {
            if (entries == null || entries.Count == 0) return Array.Empty<LedgerEntrySaveData>();

            var result = new LedgerEntrySaveData[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                result[i] = new LedgerEntrySaveData
                {
                    CurrencyId = entries[i].CurrencyId,
                    IsSource = entries[i].IsSource,
                    Reason = entries[i].Reason,
                    Total = entries[i].Total
                };
            }

            return result;
        }

        static XpAttributionSaveData[] ToAttributionData(IReadOnlyList<XpAttributionEntry> entries)
        {
            if (entries == null || entries.Count == 0) return Array.Empty<XpAttributionSaveData>();

            var result = new XpAttributionSaveData[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                result[i] = new XpAttributionSaveData { Source = entries[i].Source, Total = entries[i].Total };
            }

            return result;
        }

        static OrderBoardSaveData ToBoardData(OrderBoard board)
        {
            var orders = board.Orders;
            var data = new OrderBoardSaveData
            {
                Kind = (int)board.Kind,
                NextOrderNumber = board.NextOrderNumber,
                Orders = new OrderSaveData[orders.Count]
            };

            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                data.Orders[i] = new OrderSaveData
                {
                    OrderId = order.OrderId,
                    TemplateId = order.TemplateId,
                    Kind = (int)order.Kind,
                    Requests = ToStackData(order.Requests),
                    CurrencyRewards = ToCurrencyData(order.CurrencyRewards),
                    XpReward = order.XpReward,
                    CreatedAtTicks = order.CreatedAtTicks,
                    ExpiresAtTicks = order.ExpiresAtTicks
                };
            }

            return data;
        }

        static ProductionOrder[] ToArray(IReadOnlyList<ProductionOrder> orders)
        {
            if (orders == null || orders.Count == 0) return Array.Empty<ProductionOrder>();

            var result = new ProductionOrder[orders.Count];
            for (var i = 0; i < orders.Count; i++) result[i] = orders[i];
            return result;
        }

        // --- Save to runtime ------------------------------------------------------------------

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

        static List<CurrencyAmount> ToCurrencyAmounts(CurrencyAmountSaveData[] data)
        {
            var amounts = new List<CurrencyAmount>(data != null ? data.Length : 0);
            if (data == null) return amounts;

            for (var i = 0; i < data.Length; i++)
            {
                if (data[i] == null || string.IsNullOrEmpty(data[i].CurrencyId)) continue;
                amounts.Add(new CurrencyAmount(data[i].CurrencyId, data[i].Amount));
            }

            return amounts;
        }

        static List<LedgerEntry> ToLedgerEntries(LedgerEntrySaveData[] data)
        {
            var entries = new List<LedgerEntry>(data != null ? data.Length : 0);
            if (data == null) return entries;

            for (var i = 0; i < data.Length; i++)
            {
                if (data[i] == null || string.IsNullOrEmpty(data[i].CurrencyId)) continue;
                entries.Add(new LedgerEntry(data[i].CurrencyId, data[i].IsSource, data[i].Reason, data[i].Total));
            }

            return entries;
        }

        static List<XpAttributionEntry> ToAttributionEntries(XpAttributionSaveData[] data)
        {
            var entries = new List<XpAttributionEntry>(data != null ? data.Length : 0);
            if (data == null) return entries;

            for (var i = 0; i < data.Length; i++)
            {
                if (data[i] == null) continue;
                entries.Add(new XpAttributionEntry(data[i].Source, data[i].Total));
            }

            return entries;
        }

        static List<Order> ToOrders(OrderSaveData[] data)
        {
            var orders = new List<Order>(data != null ? data.Length : 0);
            if (data == null) return orders;

            for (var i = 0; i < data.Length; i++)
            {
                var entry = data[i];
                if (entry == null || string.IsNullOrEmpty(entry.OrderId)) continue;

                orders.Add(new Order(
                    entry.OrderId,
                    entry.TemplateId,
                    (OrderKind)entry.Kind,
                    ToStacks(entry.Requests),
                    ToCurrencyAmounts(entry.CurrencyRewards),
                    entry.XpReward,
                    entry.CreatedAtTicks,
                    entry.ExpiresAtTicks));
            }

            return orders;
        }
    }
}
