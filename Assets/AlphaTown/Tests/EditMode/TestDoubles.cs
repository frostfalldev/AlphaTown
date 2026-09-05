using System;
using System.Collections.Generic;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Buildings;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Expansion;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;
using AlphaTown.Data.Production;
using AlphaTown.Data.Progression;
using AlphaTown.Data.Recipes;
using AlphaTown.Data.Storage;
using AlphaTown.Data.Town;
using AlphaTown.Gameplay.Production;
using AlphaTown.Gameplay.Progression;
using AlphaTown.Services.Save;
using AlphaTown.Services.Timing;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// Plain-object stand-ins for the ScriptableObject definitions.
    ///
    /// Nothing here touches Unity assets, which is the whole reason the gameplay systems depend
    /// on interfaces: a test builds a content set in a few lines instead of importing a project.
    /// </summary>
    internal sealed class FakeItem : IItemDefinition
    {
        public FakeItem(string id, int storageCost = 1, bool isStorable = true,
                        int coinValue = 10, int xpValue = 2)
        {
            Id = id;
            StorageCost = storageCost;
            IsStorable = isStorable;
            CoinValue = coinValue;
            XpValue = xpValue;
        }

        public string Id { get; }
        public string DisplayNameKey => "item." + Id;
        public ItemCategory Category => ItemCategory.Ingredient;
        public int StorageCost { get; }
        public bool IsStorable { get; }
        public int CoinValue { get; }
        public int XpValue { get; }
    }

    internal sealed class FakeRecipe : IRecipeDefinition
    {
        public FakeRecipe(string id, TimeSpan duration, ItemStack[] inputs, ItemStack[] outputs,
                          int unlockLevel = 1, int bonusOutputMax = 0)
        {
            Id = id;
            Duration = duration;
            Inputs = inputs ?? Array.Empty<ItemStack>();
            Outputs = outputs ?? Array.Empty<ItemStack>();
            UnlockLevel = unlockLevel;
            BonusOutputMax = bonusOutputMax;
        }

        public string Id { get; }
        public IReadOnlyList<ItemStack> Inputs { get; }
        public IReadOnlyList<ItemStack> Outputs { get; }
        public TimeSpan Duration { get; }
        public int UnlockLevel { get; }
        public int BonusOutputMax { get; }
    }

    internal sealed class FakeProducerLevel : IProducerLevel
    {
        public FakeProducerLevel(int queueCapacity, int parallelSlots, float speedMultiplier = 1f,
                                 bool autoRepeat = false)
        {
            QueueCapacity = queueCapacity;
            ParallelSlots = parallelSlots;
            SpeedMultiplier = speedMultiplier;
            AutoRepeat = autoRepeat;
        }

        public int QueueCapacity { get; }
        public int ParallelSlots { get; }
        public float SpeedMultiplier { get; }
        public bool AutoRepeat { get; }
    }

    internal sealed class FakeProducerDefinition : IProducerDefinition
    {
        readonly IProducerLevel[] _levels;

        public FakeProducerDefinition(string id, IReadOnlyList<IRecipeDefinition> recipes,
                                      params IProducerLevel[] levels)
        {
            Id = id;
            Recipes = recipes ?? Array.Empty<IRecipeDefinition>();
            _levels = levels != null && levels.Length > 0
                ? levels
                : new IProducerLevel[] { new FakeProducerLevel(3, 1) };
        }

        public string Id { get; }
        public string DisplayNameKey => "producer." + Id;
        public IReadOnlyList<IRecipeDefinition> Recipes { get; }
        public int MaxLevel => _levels.Length;

        public IProducerLevel GetLevel(int level)
        {
            var index = level < 1 ? 0 : (level > _levels.Length ? _levels.Length - 1 : level - 1);
            return _levels[index];
        }
    }

    internal sealed class FakeStorage : IStorageDefinition
    {
        readonly int[] _capacities;

        public FakeStorage(params int[] capacities)
        {
            _capacities = capacities != null && capacities.Length > 0 ? capacities : new[] { 100 };
        }

        public string Id => "storage.test";
        public int MaxLevel => _capacities.Length;

        public int GetCapacity(int level)
        {
            var index = level < 1 ? 0 : (level > _capacities.Length ? _capacities.Length - 1 : level - 1);
            return _capacities[index];
        }
    }

    internal sealed class FakeCurrency : ICurrencyDefinition
    {
        public FakeCurrency(string id, CurrencyKind kind = CurrencyKind.Soft,
                            int startingAmount = 0, int maxAmount = 0)
        {
            Id = id;
            Kind = kind;
            StartingAmount = startingAmount;
            MaxAmount = maxAmount;
        }

        public string Id { get; }
        public string DisplayNameKey => "currency." + Id;
        public CurrencyKind Kind { get; }
        public int StartingAmount { get; }
        public int MaxAmount { get; }
    }

    internal sealed class FakeProgressionCurve : IProgressionCurve
    {
        static readonly CurrencyAmount[] None = Array.Empty<CurrencyAmount>();

        readonly int[] _xpToAdvance;
        readonly Dictionary<int, CurrencyAmount[]> _rewards = new Dictionary<int, CurrencyAmount[]>();

        /// <summary>One entry per level. The last entry is the cap, and its value is unused.</summary>
        public FakeProgressionCurve(params int[] xpToAdvance)
        {
            _xpToAdvance = xpToAdvance != null && xpToAdvance.Length > 0 ? xpToAdvance : new[] { 100 };
        }

        public FakeProgressionCurve WithRewardForReaching(int level, params CurrencyAmount[] rewards)
        {
            _rewards[level] = rewards ?? None;
            return this;
        }

        public int MaxLevel => _xpToAdvance.Length;

        public int XpToAdvance(int level)
        {
            if (level >= _xpToAdvance.Length) return 0;
            return _xpToAdvance[level < 1 ? 0 : level - 1];
        }

        public IReadOnlyList<CurrencyAmount> RewardsForReaching(int level) =>
            _rewards.TryGetValue(level, out var rewards) ? rewards : None;
    }

    internal sealed class FakeOrderTemplate : IOrderTemplateDefinition
    {
        public FakeOrderTemplate(string id, OrderKind kind = OrderKind.Helicopter)
        {
            Id = id;
            Kind = kind;
        }

        public string Id { get; }
        public OrderKind Kind { get; }
        public int UnlockLevel { get; set; } = 1;
        public int MinItemTypes { get; set; } = 1;
        public int MaxItemTypes { get; set; } = 1;
        public int MinQuantityPerItem { get; set; } = 1;
        public int MaxQuantityPerItem { get; set; } = 1;
        public TimeSpan TimeLimit { get; set; } = TimeSpan.Zero;
        public float CoinMultiplier { get; set; } = 1f;
        public float XpMultiplier { get; set; } = 1f;
        public int BonusHardCurrency { get; set; }
        public IReadOnlyList<ItemStack> BonusItems { get; set; } = Array.Empty<ItemStack>();
        public float BonusItemChance { get; set; } = 1f;
    }

    internal sealed class FakeOrderBoardDefinition : IOrderBoardDefinition
    {
        readonly int[] _cooldownSeconds;

        /// <summary>One cooldown per slot; the count of them is the slot count.</summary>
        public FakeOrderBoardDefinition(OrderKind kind, params int[] cooldownSeconds)
        {
            Kind = kind;
            _cooldownSeconds = cooldownSeconds != null && cooldownSeconds.Length > 0
                ? cooldownSeconds
                : new[] { 0 };
        }

        public string Id => "orderboard." + Kind;
        public OrderKind Kind { get; }
        public int SlotCount => _cooldownSeconds.Length;

        public TimeSpan CooldownForSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _cooldownSeconds.Length) return TimeSpan.Zero;
            return TimeSpan.FromSeconds(_cooldownSeconds[slotIndex]);
        }
    }

    /// <summary>A gate pinned to a fixed level, for testing production without a whole economy.</summary>
    internal sealed class FakeUnlockGate : IUnlockGate
    {
        public FakeUnlockGate(int townLevel = 99)
        {
            TownLevel = townLevel;
        }

        public int TownLevel { get; set; }

        public bool IsUnlocked(int requiredLevel) => TownLevel >= requiredLevel;

        public bool IsRecipeUnlocked(IRecipeDefinition recipe) => recipe != null && IsUnlocked(recipe.UnlockLevel);
    }

    internal sealed class FakeBuildingLevel : IBuildingLevel
    {
        public FakeBuildingLevel(int constructionSeconds = 0, CurrencyAmount[] currencyCost = null,
                                 ItemStack[] itemCost = null, int xpReward = 0)
        {
            ConstructionTime = TimeSpan.FromSeconds(constructionSeconds);
            CurrencyCost = currencyCost ?? Array.Empty<CurrencyAmount>();
            ItemCost = itemCost ?? Array.Empty<ItemStack>();
            XpReward = xpReward;
        }

        public TimeSpan ConstructionTime { get; }
        public IReadOnlyList<CurrencyAmount> CurrencyCost { get; }
        public IReadOnlyList<ItemStack> ItemCost { get; }
        public int XpReward { get; }
    }

    internal sealed class FakeBuildingDefinition : IBuildingDefinition
    {
        readonly IBuildingLevel[] _levels;

        public FakeBuildingDefinition(string id, GridSize footprint, params IBuildingLevel[] levels)
        {
            Id = id;
            Footprint = footprint;
            _levels = levels != null && levels.Length > 0
                ? levels
                : new IBuildingLevel[] { new FakeBuildingLevel() };
        }

        public string Id { get; }
        public string DisplayNameKey => "building." + Id;
        public BuildingCategory Category { get; set; } = BuildingCategory.Production;
        public GridSize Footprint { get; }
        public int UnlockLevel { get; set; } = 1;
        public int MaxLevel => _levels.Length;
        public string ProducerDefinitionId { get; set; } = string.Empty;
        public string UpgradesIntoId { get; set; } = string.Empty;

        public IBuildingLevel GetLevel(int level)
        {
            var index = level < 1 ? 0 : (level > _levels.Length ? _levels.Length - 1 : level - 1);
            return _levels[index];
        }
    }

    internal sealed class FakeTownDefinition : ITownDefinition
    {
        /// <summary>A zero-sized starting area means the whole grid is owned from the start.</summary>
        public FakeTownDefinition(int width, int height)
            : this(width, height, new GridRect(GridPosition.Zero, new GridSize(0, 0)))
        {
        }

        public FakeTownDefinition(int width, int height, GridRect startingArea)
        {
            Size = new GridSize(width, height);
            StartingArea = startingArea;
        }

        public string Id => "town.test";
        public GridSize Size { get; }
        public GridRect StartingArea { get; }
    }

    internal sealed class FakeExpansionDefinition : IExpansionDefinition
    {
        public FakeExpansionDefinition(string id, GridRect region, ItemStack[] itemCost = null,
                                       CurrencyAmount[] currencyCost = null)
        {
            Id = id;
            Region = region;
            ItemCost = itemCost ?? Array.Empty<ItemStack>();
            CurrencyCost = currencyCost ?? Array.Empty<CurrencyAmount>();
        }

        public string Id { get; }
        public string DisplayNameKey => "expansion." + Id;
        public GridRect Region { get; }
        public int UnlockLevel { get; set; } = 1;
        public string RequiresExpansionId { get; set; } = string.Empty;
        public IReadOnlyList<ItemStack> ItemCost { get; }
        public IReadOnlyList<CurrencyAmount> CurrencyCost { get; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Records producer attachment without standing up a world, so building tests can assert that
    /// a finished factory got a producer at the right level. Returning null is fine — TownBuildings
    /// never reads the result.
    /// </summary>
    internal sealed class RecordingProducerHost : IProducerHost
    {
        public readonly List<string> Ensured = new List<string>();
        public readonly List<string> Removed = new List<string>();
        public readonly Dictionary<string, int> LevelsByInstance = new Dictionary<string, int>();

        public Producer EnsureProducer(string instanceId, string producerDefinitionId, int level)
        {
            Ensured.Add(instanceId);
            LevelsByInstance[instanceId] = level;
            return null;
        }

        public bool RemoveProducer(string instanceId)
        {
            Removed.Add(instanceId);
            return true;
        }
    }

    internal sealed class FakeNewGameDefinition : INewGameDefinition
    {
        public FakeNewGameDefinition(int startingBarnLevel = 1, ItemStack[] items = null,
                                     StartingBuilding[] buildings = null)
        {
            Id = "new_game";
            StartingBarnLevel = startingBarnLevel;
            StartingItems = items ?? Array.Empty<ItemStack>();
            StartingBuildings = buildings ?? Array.Empty<StartingBuilding>();
        }

        public string Id { get; }
        public int StartingBarnLevel { get; }
        public IReadOnlyList<ItemStack> StartingItems { get; }
        public IReadOnlyList<StartingBuilding> StartingBuildings { get; }
    }

    internal sealed class FakeDatabase : IGameDatabase
    {
        readonly Dictionary<string, IItemDefinition> _items = new Dictionary<string, IItemDefinition>();
        readonly Dictionary<string, IRecipeDefinition> _recipes = new Dictionary<string, IRecipeDefinition>();
        readonly Dictionary<string, IProducerDefinition> _producers = new Dictionary<string, IProducerDefinition>();
        readonly Dictionary<string, IStorageDefinition> _storages = new Dictionary<string, IStorageDefinition>();
        readonly Dictionary<string, ICurrencyDefinition> _currencies = new Dictionary<string, ICurrencyDefinition>();

        readonly Dictionary<string, IOrderTemplateDefinition> _orderTemplates =
            new Dictionary<string, IOrderTemplateDefinition>();

        readonly Dictionary<string, IBuildingDefinition> _buildings = new Dictionary<string, IBuildingDefinition>();

        readonly Dictionary<string, IOrderBoardDefinition> _orderBoards =
            new Dictionary<string, IOrderBoardDefinition>();

        readonly Dictionary<string, IExpansionDefinition> _expansions =
            new Dictionary<string, IExpansionDefinition>();

        readonly List<IItemDefinition> _itemList = new List<IItemDefinition>();
        readonly List<IRecipeDefinition> _recipeList = new List<IRecipeDefinition>();
        readonly List<ICurrencyDefinition> _currencyList = new List<ICurrencyDefinition>();
        readonly List<IOrderTemplateDefinition> _orderTemplateList = new List<IOrderTemplateDefinition>();
        readonly List<IBuildingDefinition> _buildingList = new List<IBuildingDefinition>();
        readonly List<IOrderBoardDefinition> _orderBoardList = new List<IOrderBoardDefinition>();
        readonly List<IExpansionDefinition> _expansionList = new List<IExpansionDefinition>();

        public IStorageDefinition DefaultStorage { get; set; }
        public ICurrencyDefinition SoftCurrency { get; set; }
        public ICurrencyDefinition HardCurrency { get; set; }
        public IProgressionCurve ProgressionCurve { get; set; }
        public ITownDefinition TownDefinition { get; set; }
        public INewGameDefinition NewGame { get; set; }

        public IReadOnlyList<IItemDefinition> Items => _itemList;
        public IReadOnlyList<IRecipeDefinition> Recipes => _recipeList;
        public IReadOnlyList<ICurrencyDefinition> Currencies => _currencyList;
        public IReadOnlyList<IOrderTemplateDefinition> OrderTemplates => _orderTemplateList;
        public IReadOnlyList<IBuildingDefinition> Buildings => _buildingList;
        public IReadOnlyList<IOrderBoardDefinition> OrderBoards => _orderBoardList;
        public IReadOnlyList<IExpansionDefinition> Expansions => _expansionList;

        public FakeDatabase WithItem(IItemDefinition item)
        {
            _items[item.Id] = item;
            _itemList.Add(item);
            return this;
        }

        public FakeDatabase WithRecipe(IRecipeDefinition recipe)
        {
            _recipes[recipe.Id] = recipe;
            _recipeList.Add(recipe);
            return this;
        }

        public FakeDatabase WithProducer(IProducerDefinition producer)
        {
            _producers[producer.Id] = producer;
            return this;
        }

        public FakeDatabase WithStorage(IStorageDefinition storage, bool asDefault = true)
        {
            _storages[storage.Id] = storage;
            if (asDefault) DefaultStorage = storage;
            return this;
        }

        public FakeDatabase WithCurrency(ICurrencyDefinition currency)
        {
            _currencies[currency.Id] = currency;
            _currencyList.Add(currency);

            if (currency.Kind == CurrencyKind.Soft && SoftCurrency == null) SoftCurrency = currency;
            if (currency.Kind == CurrencyKind.Hard && HardCurrency == null) HardCurrency = currency;
            return this;
        }

        public FakeDatabase WithOrderTemplate(IOrderTemplateDefinition template)
        {
            _orderTemplates[template.Id] = template;
            _orderTemplateList.Add(template);
            return this;
        }

        public FakeDatabase WithBuilding(IBuildingDefinition building)
        {
            _buildings[building.Id] = building;
            _buildingList.Add(building);
            return this;
        }

        public FakeDatabase WithOrderBoard(IOrderBoardDefinition board)
        {
            _orderBoards[board.Id] = board;
            _orderBoardList.Add(board);
            return this;
        }

        public FakeDatabase WithExpansion(IExpansionDefinition expansion)
        {
            _expansions[expansion.Id] = expansion;
            _expansionList.Add(expansion);
            return this;
        }

        public FakeDatabase WithTown(ITownDefinition town)
        {
            TownDefinition = town;
            return this;
        }

        public FakeDatabase WithNewGame(INewGameDefinition newGame)
        {
            NewGame = newGame;
            return this;
        }

        public FakeDatabase WithProgressionCurve(IProgressionCurve curve)
        {
            ProgressionCurve = curve;
            return this;
        }

        public bool TryGetItem(string id, out IItemDefinition item) =>
            _items.TryGetValue(id ?? string.Empty, out item);

        public bool TryGetRecipe(string id, out IRecipeDefinition recipe) =>
            _recipes.TryGetValue(id ?? string.Empty, out recipe);

        public bool TryGetProducer(string id, out IProducerDefinition producer) =>
            _producers.TryGetValue(id ?? string.Empty, out producer);

        public bool TryGetStorage(string id, out IStorageDefinition storage) =>
            _storages.TryGetValue(id ?? string.Empty, out storage);

        public bool TryGetCurrency(string id, out ICurrencyDefinition currency) =>
            _currencies.TryGetValue(id ?? string.Empty, out currency);

        public bool TryGetOrderTemplate(string id, out IOrderTemplateDefinition template) =>
            _orderTemplates.TryGetValue(id ?? string.Empty, out template);

        public bool TryGetBuilding(string id, out IBuildingDefinition building) =>
            _buildings.TryGetValue(id ?? string.Empty, out building);

        public bool TryGetOrderBoard(string id, out IOrderBoardDefinition board) =>
            _orderBoards.TryGetValue(id ?? string.Empty, out board);

        public bool TryGetExpansion(string id, out IExpansionDefinition expansion) =>
            _expansions.TryGetValue(id ?? string.Empty, out expansion);
    }

    /// <summary>
    /// Answers time requests instantly and synchronously, so a test can drive sync, failure and
    /// latency without a network or a frame delay.
    /// </summary>
    internal sealed class FakeServerTimeProvider : IServerTimeProvider
    {
        public long ServerUtcTicks;
        public long RoundTripTicks;
        public bool IsReachable = true;
        public int RequestCount;

        public void RequestTime(Action<ServerTimeSample> onComplete)
        {
            RequestCount++;
            onComplete?.Invoke(IsReachable
                ? ServerTimeSample.From(ServerUtcTicks, RoundTripTicks)
                : ServerTimeSample.Failed);
        }
    }

    /// <summary>Save store backed by a dictionary, so save tests never touch the filesystem.</summary>
    internal sealed class InMemorySaveStore : ISaveStore
    {
        readonly Dictionary<string, string> _files = new Dictionary<string, string>();

        public bool Exists(string key) => _files.ContainsKey(key);

        public bool TryRead(string key, out string contents) => _files.TryGetValue(key, out contents);

        public bool TryWrite(string key, string contents)
        {
            _files[key] = contents;
            return true;
        }

        public bool Delete(string key) => _files.Remove(key);
    }
}
