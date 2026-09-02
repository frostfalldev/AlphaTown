namespace AlphaTown.Data.Economy
{
    /// <summary>
    /// Where currency came from. Every faucet in the game.
    ///
    /// This lives in Data, not Gameplay, so the analytics service in Services can speak the same
    /// vocabulary without an upward reference.
    ///
    /// Once a value has shipped its meaning is frozen: dashboards and economy models are built on
    /// these names, so add new values, never repurpose old ones.
    /// </summary>
    public enum CurrencySource
    {
        /// <summary>Untagged. A bug — the ledger records it so it can be alerted on.</summary>
        Unknown = 0,

        OrderReward = 1,
        LevelUpReward = 2,
        QuestReward = 3,
        AchievementReward = 4,

        /// <summary>Real money. Must reconcile against store receipts.</summary>
        IapPurchase = 10,
        AdReward = 11,
        DailyBonus = 12,
        GiftFromFriend = 13,

        /// <summary>Given back after a cancellation. Not a faucet — nets against the original sink.</summary>
        Refund = 20,

        StartingBalance = 30,
        DebugGrant = 90,
        Migration = 91
    }
}
