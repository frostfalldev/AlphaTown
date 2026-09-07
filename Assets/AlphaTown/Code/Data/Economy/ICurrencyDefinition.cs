namespace AlphaTown.Data.Economy
{
    /// <summary>
    /// A currency the player can hold.
    ///
    /// Currencies are deliberately not items: they never enter the barn, they are not bound by
    /// storage space, and their movements need source/sink auditing that item movements do not.
    /// </summary>
    public interface ICurrencyDefinition
    {
        string Id { get; }

        string DisplayNameKey { get; }

        CurrencyKind Kind { get; }

        /// <summary>Balance a brand-new player starts with.</summary>
        int StartingAmount { get; }

        /// <summary>Hard ceiling on the balance. Zero means uncapped.</summary>
        int MaxAmount { get; }
    }
}
