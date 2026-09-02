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
    /// </summary>
    [Serializable]
    public sealed class GameSaveData
    {
        public InventorySaveData Inventory = new InventorySaveData();
        public ProducerSaveData[] Producers = Array.Empty<ProducerSaveData>();
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
    }

    [Serializable]
    public sealed class ItemStackSaveData
    {
        public string ItemId;
        public int Count;
    }
}
