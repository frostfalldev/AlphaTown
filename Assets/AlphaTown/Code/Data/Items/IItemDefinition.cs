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

        /// <summary>False for items that bypass the barn entirely, such as soft currency.</summary>
        bool IsStorable { get; }
    }
}
