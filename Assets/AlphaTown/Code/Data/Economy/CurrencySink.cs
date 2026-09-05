namespace AlphaTown.Data.Economy
{
    /// <summary>
    /// Where currency went. Every sink in the game.
    ///
    /// Kept separate from <see cref="CurrencySource"/> rather than merged into one reason enum so
    /// the compiler enforces the split: a sink cannot be passed to a grant. Source/sink balance is
    /// the number economy tuning actually runs on.
    /// </summary>
    public enum CurrencySink
    {
        /// <summary>Untagged. A bug — the ledger records it so it can be alerted on.</summary>
        Unknown = 0,

        BuildingPurchase = 1,
        BuildingUpgrade = 2,
        StorageUpgrade = 3,
        ExpansionPurchase = 4,

        ProductionSpeedUp = 10,
        OrderReroll = 11,
        InstantRefill = 12,

        MarketPurchase = 20,
        DebugSpend = 90
    }
}
