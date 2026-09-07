namespace AlphaTown.Data.Storage
{
    /// <summary>
    /// The barn: how much space the player has, and how that grows with upgrades.
    /// Capacity is authored per level rather than computed, because the upgrade curve is an
    /// economy lever that gets retuned in live ops.
    /// </summary>
    public interface IStorageDefinition
    {
        string Id { get; }

        /// <summary>Levels are 1-based.</summary>
        int MaxLevel { get; }

        /// <summary>Clamped, so a save from a future build cannot index out of bounds.</summary>
        int GetCapacity(int level);
    }
}
