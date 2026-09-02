using System;
using AlphaTown.Gameplay.Production;

namespace AlphaTown.Gameplay.Saving
{
    /// <summary>
    /// Root of the persisted game state.
    ///
    /// Shaped for JsonUtility: [Serializable] classes, public fields, arrays instead of
    /// dictionaries, no polymorphism. Deliberately separate from the runtime types — runtime
    /// state is free to be restructured, save data is a contract with every installed build.
    ///
    /// Enums are persisted as ints so a value written by a newer build survives a round trip
    /// through an older one rather than collapsing onto the zero member.
    /// </summary>
    [Serializable]
    public sealed class GameSaveData
    {
        public InventorySaveData Inventory = new InventorySaveData();
        public WalletSaveData Wallet = new WalletSaveData();
        public ProgressionSaveData Progression = new ProgressionSaveData();
        public ProducerSaveData[] Producers = Array.Empty<ProducerSaveData>();
        public OrderBoardSaveData[] OrderBoards = Array.Empty<OrderBoardSaveData>();
        public TownSaveData Town = new TownSaveData();
    }

    [Serializable]
    public sealed class InventorySaveData
    {
        public int Level = 1;
        public ItemStackSaveData[] Stacks = Array.Empty<ItemStackSaveData>();
    }

    [Serializable]
    public sealed class ProducerSaveData
    {
        public string InstanceId;
        public string DefinitionId;
        public int Level = 1;
        public ProductionOrder[] Orders = Array.Empty<ProductionOrder>();
        public ItemStackSaveData[] Ready = Array.Empty<ItemStackSaveData>();

        /// <summary>What auto-repeat re-runs, and what a field remembers it was growing.</summary>
        public string LastRecipeId;
    }

    [Serializable]
    public sealed class ItemStackSaveData
    {
        public string ItemId;
        public int Count;
    }

    [Serializable]
    public sealed class WalletSaveData
    {
        public CurrencyAmountSaveData[] Balances = Array.Empty<CurrencyAmountSaveData>();

        /// <summary>Lifetime source/sink aggregates. Fixed size regardless of how long play lasts.</summary>
        public LedgerEntrySaveData[] Ledger = Array.Empty<LedgerEntrySaveData>();
    }

    [Serializable]
    public sealed class CurrencyAmountSaveData
    {
        public string CurrencyId;
        public int Amount;
    }

    [Serializable]
    public sealed class LedgerEntrySaveData
    {
        public string CurrencyId;
        public bool IsSource;
        public int Reason;
        public long Total;
    }

    [Serializable]
    public sealed class ProgressionSaveData
    {
        public int Level = 1;
        public long XpIntoLevel;
        public long TotalXp;
        public XpAttributionSaveData[] Attribution = Array.Empty<XpAttributionSaveData>();
    }

    [Serializable]
    public sealed class XpAttributionSaveData
    {
        public int Source;
        public long Total;
    }

    [Serializable]
    public sealed class OrderBoardSaveData
    {
        public int Kind;
        public int NextOrderNumber = 1;

        /// <summary>
        /// One entry per slot: when that slot may produce its next order. Zero means available now.
        /// Without this a load would refill every cooling slot and hand back free income.
        /// </summary>
        public long[] SlotNextAvailableAtTicks = Array.Empty<long>();

        /// <summary>
        /// Only the occupied slots. Each carries its slot index rather than nesting orders inside
        /// slot objects, because JsonUtility cannot round-trip a null nested object.
        /// </summary>
        public OrderSaveData[] Orders = Array.Empty<OrderSaveData>();
    }

    [Serializable]
    public sealed class TownSaveData
    {
        /// <summary>Land the player owns. Permanent, so this only ever grows.</summary>
        public string[] UnlockedExpansionIds = Array.Empty<string>();

        public int NextBuildingNumber = 1;
        public BuildingSaveData[] Buildings = Array.Empty<BuildingSaveData>();
    }

    [Serializable]
    public sealed class BuildingSaveData
    {
        public string InstanceId;
        public string DefinitionId;

        /// <summary>Grid origin. Stored as two ints because JsonUtility has no struct shorthand.</summary>
        public int X;
        public int Y;

        /// <summary>Zero while the first build is still running.</summary>
        public int Level;

        public int TargetLevel;
        public long ConstructionStartedAtTicks;

        /// <summary>Zero when idle. Absolute, so an absence completes the build correctly.</summary>
        public long ConstructionCompletesAtTicks;
    }

    [Serializable]
    public sealed class OrderSaveData
    {
        public int SlotIndex;
        public string OrderId;
        public string TemplateId;
        public int Kind;
        public ItemStackSaveData[] Requests = Array.Empty<ItemStackSaveData>();
        public CurrencyAmountSaveData[] CurrencyRewards = Array.Empty<CurrencyAmountSaveData>();

        /// <summary>Bonus items, rolled at generation. Saved so the deed cannot be re-rolled.</summary>
        public ItemStackSaveData[] ItemRewards = Array.Empty<ItemStackSaveData>();

        public int XpReward;
        public long CreatedAtTicks;

        /// <summary>Zero means no time limit. Absolute, so an absence expires it correctly.</summary>
        public long ExpiresAtTicks;
    }
}
