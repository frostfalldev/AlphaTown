using System;
using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Core.Spatial;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;
using AlphaTown.Data.Progression;
using AlphaTown.Data.Town;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.Economy;
using AlphaTown.Gameplay.Expansion;
using AlphaTown.Gameplay.Grid;
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
    public sealed class GameWorld : ITickable, IProducerHost
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

        /// <summary>Used when the database has no TownDefinition. TODO(expansion): always data.</summary>
        static readonly GridSize DefaultTownSize = new GridSize(32, 32);

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
            var boardDefinition = FindBoardDefinition(database, OrderKind.Helicopter)
                                  ?? new FallbackOrderBoardDefinition(OrderKind.Helicopter);

            HelicopterOrders = new OrderBoard(
                boardDefinition, clock, events, progression, Barn, wallet, generator);

            var town = database.TownDefinition;
            var gridSize = town != null && town.Size.IsValid ? town.Size : DefaultTownSize;
            var grid = new TownGrid(gridSize);

            // Expansion owns the grid's unlocked mask, so it is built before anything can be
            // placed. With no starting area authored it leaves the whole grid owned, which is what
            // a project without land content expects.
            Expansion = new TownExpansion(grid, database, events, wallet, Barn, progression, town);
            Buildings = new TownBuildings(
                grid, database, clock, events, wallet, Barn, progression, this);
        }

        public BarnInventory Barn { get; }

        public Wallet Wallet { get; }

        /// <summary>Lifetime source/sink totals. Read-only view for analytics and debug.</summary>
        public ICurrencyLedger Ledger => _ledger;

        public TownProgression Progression { get; }

        /// <summary>TODO: train and ship boards join this as separate OrderBoard instances.</summary>
        public OrderBoard HelicopterOrders { get; }

        public IReadOnlyList<Producer> Producers => _producers;

        /// <summary>Placed buildings and the grid they sit on. The primary coin sink.</summary>
        public TownBuildings Buildings { get; }

        /// <summary>Which land the player owns. Gated by deeds, not coins.</summary>
        public TownExpansion Expansion { get; }

        /// <summary>
        /// Seeds a brand-new town: starting balances, then whatever the new-game definition says
        /// the player begins with, then a first set of orders.
        ///
        /// Everything here is content. A project that authors no <see cref="INewGameDefinition"/>
        /// gets a bare town with starting currency and an order board, which is a legitimate — if
        /// unwelcoming — game, and never a crash.
        /// </summary>
        public void InitialiseNewPlayer()
        {
            this.Wallet.InitialiseNewPlayer();

            var newGame = _database.NewGame;
            if (newGame != null)
            {
                GrantStartingBarnLevel(newGame.StartingBarnLevel);
                GrantStartingItems(newGame.StartingItems);
                PlaceStartingBuildings(newGame.StartingBuildings);
            }

            Sync();
        }

        /// <summary>
        /// Raises the barn to the level the new-game definition asks for.
        ///
        /// This only sets the floor. Storage buildings raise it from there — see
        /// <see cref="ApplyStorageUpgrades"/>.
        ///
        /// <see cref="BarnInventory.SetLevel"/> clamps to what the storage definition actually
        /// offers, which is right for a save from a future build but wrong to pass over in
        /// silence here: content asking for a level that does not exist is a mistake someone
        /// should be told about, not a starting barn that is quietly smaller than intended.
        /// </summary>
        void GrantStartingBarnLevel(int level)
        {
            if (level <= Barn.Level) return;

            Barn.SetLevel(level);
            if (Barn.Level >= level) return;

            Log.Error("World",
                "New game asks for barn level " + level + " but the storage definition only goes to " +
                Barn.Level + ". The player starts with a smaller barn than intended.");
        }

        void GrantStartingItems(IReadOnlyList<ItemStack> items)
        {
            if (items == null) return;

            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].IsEmpty) continue;

                var added = Barn.Add(items[i].ItemId, items[i].Count);
                if (added >= items[i].Count) continue;

                // Starting goods that do not fit are a content bug, not a player problem: the barn
                // they start with is too small for the hand they were dealt.
                Log.Error("World",
                    "Starting items did not fit the barn: only " + added + " of " + items[i].Count +
                    " x '" + items[i].ItemId + "' were granted.");
            }
        }

        void PlaceStartingBuildings(IReadOnlyList<StartingBuilding> buildings)
        {
            if (buildings == null) return;

            for (var i = 0; i < buildings.Count; i++)
            {
                var entry = buildings[i];
                if (string.IsNullOrEmpty(entry.DefinitionId)) continue;

                var result = this.Buildings.GrantBuilding(entry.DefinitionId, entry.Origin, out _);
                if (result == BuildingActionResult.Success) continue;

                Log.Error("World",
                    "Could not place starting building '" + entry.DefinitionId + "' at " +
                    entry.Origin + ": " + result + ".");
            }
        }

        /// <summary>
        /// Collects a producer's finished goods and pays the XP they are worth.
        ///
        /// The XP lives here rather than in <see cref="Producer"/> so that production keeps its
        /// narrow dependency on <see cref="IUnlockGate"/> — a producer needs to know what is
        /// unlocked, not how to level the town up. Value comes from the items themselves, so
        /// harvesting a crop and delivering it are priced off the same number.
        /// </summary>
        public int Collect(string producerInstanceId)
        {
            if (!TryGetProducer(producerInstanceId, out var producer)) return 0;

            var before = SnapshotReady(producer.Ready);
            var collected = producer.CollectReady(Barn);
            if (collected <= 0) return 0;

            var xp = 0;
            for (var i = 0; i < before.Count; i++)
            {
                if (!_database.TryGetItem(before[i].ItemId, out var item)) continue;

                // Only what actually made it into the barn earns; a full barn leaves the rest in
                // the tray, and paying for it now would pay for it twice.
                var moved = before[i].Count - RemainingCount(producer.Ready, before[i].ItemId);
                if (moved > 0) xp += item.XpValue * moved;
            }

            if (xp > 0) this.Progression.GrantXp(xp, XpSource.ProductionCollected, producerInstanceId);
            return collected;
        }

        static List<ItemStack> SnapshotReady(IReadOnlyList<ItemStack> ready)
        {
            var copy = new List<ItemStack>(ready.Count);
            for (var i = 0; i < ready.Count; i++) copy.Add(ready[i]);
            return copy;
        }

        static int RemainingCount(IReadOnlyList<ItemStack> ready, string itemId)
        {
            for (var i = 0; i < ready.Count; i++)
            {
                if (string.Equals(ready[i].ItemId, itemId, StringComparison.Ordinal)) return ready[i].Count;
            }

            return 0;
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
        /// <see cref="IProducerHost"/>. Buildings call this when construction finishes; the
        /// producer's level tracks the building's.
        /// </summary>
        public Producer EnsureProducer(string instanceId, string producerDefinitionId, int level)
        {
            if (_producersByInstanceId.TryGetValue(instanceId ?? string.Empty, out var existing))
            {
                existing.SetLevel(level);
                return existing;
            }

            var producer = AddProducer(instanceId, producerDefinitionId);
            producer?.SetLevel(level);
            return producer;
        }

        /// <summary><see cref="IProducerHost"/>. Drops the producer and anything queued in it.</summary>
        public bool RemoveProducer(string instanceId)
        {
            if (!_producersByInstanceId.TryGetValue(instanceId ?? string.Empty, out var producer))
                return false;

            _producersByInstanceId.Remove(producer.InstanceId);
            _producers.Remove(producer);
            return true;
        }

        /// <summary>
        /// Brings the whole town up to date with the clock. Call on load, on app resume, and
        /// periodically while running.
        /// </summary>
        public void Sync()
        {
            // Buildings first: a build finishing offline can bring a producer into existence, and
            // that producer should catch up in the same pass rather than a second later.
            this.Buildings.Sync();
            ApplyStorageUpgrades();

            for (var i = 0; i < _producers.Count; i++) _producers[i].Sync();
            HelicopterOrders.Sync();
        }

        /// <summary>
        /// Sizes the barn to the best storage building standing.
        ///
        /// Recomputed on every sync rather than applied once when a granary finishes, which makes
        /// it self-healing: a restored save, a granary whose level was retuned in content, and a
        /// build that completed while the app was closed all arrive at the same answer without
        /// any of them being a special case.
        ///
        /// It only ever raises. Shrinking a barn below what is already in it would strand goods
        /// the player earned, so demolishing a granary keeps the space it bought — generous, and
        /// far better than the alternative.
        /// </summary>
        void ApplyStorageUpgrades()
        {
            var granted = this.Buildings.HighestStorageLevel();
            if (granted > Barn.Level) Barn.SetLevel(granted);
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
                OrderBoards = new[] { ToBoardData(HelicopterOrders) },
                Town = ToTownData(this.Buildings, this.Expansion)
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
                    Ready = ToStackData(producer.Ready),
                    LastRecipeId = producer.LastRecipeId
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

                    producer.RestoreState(data.Level, data.Orders, ToStacks(data.Ready), data.LastRecipeId);
                }
            }

            var town = save.Town ?? new TownSaveData();

            // Land before buildings: placement validation checks the unlocked mask, so a building
            // standing on bought land would be dropped if the land had not been restored yet.
            this.Expansion.RestoreState(town.UnlockedExpansionIds);

            // Buildings after producers: a restored building matches itself back to the producer
            // that was saved alongside it rather than creating a second one.
            this.Buildings.RestoreState(ToBuildingRestoreData(town.Buildings), town.NextBuildingNumber);

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

                // Built in one pass so order and slot index stay aligned even when an entry is
                // skipped as malformed.
                var orders = new List<Order>(data.Orders != null ? data.Orders.Length : 0);
                var slots = new List<int>(orders.Capacity);
                ToOrdersAndSlots(data.Orders, orders, slots);

                HelicopterOrders.RestoreState(
                    orders, slots, data.SlotNextAvailableAtTicks, data.NextOrderNumber);
                return;
            }
        }

        /// <summary>
        /// Board pacing is authored per kind. Without a definition the fallback still applies a
        /// cooldown — an instantly refilling board is unbounded income, and a project that has not
        /// tuned its pacing should not find that out in production.
        /// </summary>
        static IOrderBoardDefinition FindBoardDefinition(IGameDatabase database, OrderKind kind)
        {
            var boards = database.OrderBoards;
            if (boards == null) return null;

            for (var i = 0; i < boards.Count; i++)
            {
                if (boards[i] != null && boards[i].Kind == kind) return boards[i];
            }

            return null;
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
                SlotNextAvailableAtTicks = new long[board.SlotCount],
                Orders = new OrderSaveData[orders.Count]
            };

            for (var i = 0; i < board.SlotCount; i++)
            {
                data.SlotNextAvailableAtTicks[i] = board.SlotAvailableAtTicks(i);
            }

            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                data.Orders[i] = new OrderSaveData
                {
                    SlotIndex = board.SlotIndexOf(order.OrderId),
                    OrderId = order.OrderId,
                    TemplateId = order.TemplateId,
                    Kind = (int)order.Kind,
                    Requests = ToStackData(order.Requests),
                    CurrencyRewards = ToCurrencyData(order.CurrencyRewards),
                    ItemRewards = ToStackData(order.ItemRewards),
                    XpReward = order.XpReward,
                    CreatedAtTicks = order.CreatedAtTicks,
                    ExpiresAtTicks = order.ExpiresAtTicks
                };
            }

            return data;
        }

        static TownSaveData ToTownData(TownBuildings buildings, TownExpansion expansion)
        {
            var all = buildings.All;
            var data = new TownSaveData
            {
                UnlockedExpansionIds = expansion.Snapshot().ToArray(),
                NextBuildingNumber = buildings.NextInstanceNumber,
                Buildings = new BuildingSaveData[all.Count]
            };

            for (var i = 0; i < all.Count; i++)
            {
                var building = all[i];
                data.Buildings[i] = new BuildingSaveData
                {
                    InstanceId = building.InstanceId,
                    DefinitionId = building.DefinitionId,
                    X = building.Origin.X,
                    Y = building.Origin.Y,
                    Level = building.Level,
                    TargetLevel = building.TargetLevel,
                    ConstructionStartedAtTicks = building.ConstructionStartedAtTicks,
                    ConstructionCompletesAtTicks = building.ConstructionCompletesAtTicks
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

        static List<BuildingRestoreData> ToBuildingRestoreData(BuildingSaveData[] data)
        {
            var entries = new List<BuildingRestoreData>(data != null ? data.Length : 0);
            if (data == null) return entries;

            for (var i = 0; i < data.Length; i++)
            {
                var entry = data[i];
                if (entry == null || string.IsNullOrEmpty(entry.InstanceId)) continue;

                entries.Add(new BuildingRestoreData(
                    entry.InstanceId,
                    entry.DefinitionId,
                    new GridPosition(entry.X, entry.Y),
                    entry.Level,
                    entry.TargetLevel,
                    entry.ConstructionStartedAtTicks,
                    entry.ConstructionCompletesAtTicks));
            }

            return entries;
        }

        static void ToOrdersAndSlots(OrderSaveData[] data, List<Order> orders, List<int> slots)
        {
            if (data == null) return;

            for (var i = 0; i < data.Length; i++)
            {
                var entry = data[i];
                if (entry == null || string.IsNullOrEmpty(entry.OrderId)) continue;

                slots.Add(entry.SlotIndex);
                orders.Add(new Order(
                    entry.OrderId,
                    entry.TemplateId,
                    (OrderKind)entry.Kind,
                    ToStacks(entry.Requests),
                    ToCurrencyAmounts(entry.CurrencyRewards),
                    ToStacks(entry.ItemRewards),
                    entry.XpReward,
                    entry.CreatedAtTicks,
                    entry.ExpiresAtTicks));
            }
        }
    }
}
