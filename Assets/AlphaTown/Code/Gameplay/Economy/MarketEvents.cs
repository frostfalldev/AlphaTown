namespace AlphaTown.Gameplay.Economy
{
    /// <summary>
    /// Goods sold for coins. Carries the price paid so a view can animate the number without
    /// recomputing it, and so analytics can see what players actually choose to dump.
    /// </summary>
    public readonly struct ItemSoldEvent
    {
        public readonly string ItemId;
        public readonly int Count;
        public readonly int CoinsPaid;

        public ItemSoldEvent(string itemId, int count, int coinsPaid)
        {
            ItemId = itemId;
            Count = count;
            CoinsPaid = coinsPaid;
        }
    }
}
