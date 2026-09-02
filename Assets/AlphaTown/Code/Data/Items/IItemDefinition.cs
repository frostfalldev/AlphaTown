namespace AlphaTown.Data.Items
{
    /// <summary>
    /// Everything the simulation needs to know about an item. Gameplay depends on this interface,
    /// not on the ScriptableObject, so tests can supply plain objects.
    /// </summary>
    public interface IItemDefinition
    {
        string Id { get; }

        /// <summary>Localisation key, not display text. Nothing in the simulation renders strings.</summary>
        string DisplayNameKey { get; }

        ItemCategory Category { get; }

        /// <summary>Barn space one unit consumes. Almost always 1; heavy goods can cost more.</summary>
        int StorageCost { get; }

        /// <summary>False for items that bypass the barn entirely.</summary>
        bool IsStorable { get; }

        /// <summary>
        /// Base worth in soft currency. Order payouts are derived from the summed value of the
        /// goods requested, so this is the single number that prices an item across the economy —
        /// change it here and every order, sale and reward re-prices with it.
        /// </summary>
        int CoinValue { get; }

        /// <summary>Base XP worth, scaled the same way as <see cref="CoinValue"/>.</summary>
        int XpValue { get; }
    }
}
