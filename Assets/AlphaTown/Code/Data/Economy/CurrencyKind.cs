namespace AlphaTown.Data.Economy
{
    /// <summary>
    /// What a currency means to the economy. The distinction is not cosmetic: hard currency is
    /// bought with real money, so every hard-currency sink needs auditing and every hard-currency
    /// source needs to be accounted for against revenue.
    /// </summary>
    public enum CurrencyKind
    {
        /// <summary>Earned through play. Coins.</summary>
        Soft = 0,

        /// <summary>Premium. Bought, or granted sparingly. Gems.</summary>
        Hard = 1,

        /// <summary>Scoped to a limited-time event and usually expired afterwards.</summary>
        Event = 2
    }
}
