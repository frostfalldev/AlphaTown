namespace AlphaTown.Gameplay.Inventory
{
    /// <summary>Raised whenever a stack count changes. UI listens; the simulation does not.</summary>
    public readonly struct InventoryChangedEvent
    {
        public readonly string ItemId;
        public readonly int NewCount;
        public readonly int Delta;

        public InventoryChangedEvent(string itemId, int newCount, int delta)
        {
            ItemId = itemId;
            NewCount = newCount;
            Delta = delta;
        }
    }

    /// <summary>Raised when used space or capacity moves, including on barn upgrades.</summary>
    public readonly struct InventorySpaceChangedEvent
    {
        public readonly int UsedSpace;
        public readonly int Capacity;

        public InventorySpaceChangedEvent(int usedSpace, int capacity)
        {
            UsedSpace = usedSpace;
            Capacity = capacity;
        }
    }

    /// <summary>Raised when an add was clipped by capacity. The hook for "barn full" messaging.</summary>
    public readonly struct InventoryOverflowEvent
    {
        public readonly string ItemId;
        public readonly int RejectedCount;

        public InventoryOverflowEvent(string itemId, int rejectedCount)
        {
            ItemId = itemId;
            RejectedCount = rejectedCount;
        }
    }
}
