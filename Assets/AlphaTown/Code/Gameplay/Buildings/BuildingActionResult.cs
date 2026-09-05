namespace AlphaTown.Gameplay.Buildings
{
    /// <summary>
    /// Outcome of a build, upgrade, move or remove.
    ///
    /// An enum rather than a bool because every failure here has a different message and a
    /// different call to action — "you cannot afford this" sells gems, "that spot is taken" does not.
    /// </summary>
    public enum BuildingActionResult
    {
        Success = 0,

        UnknownDefinition = 1,

        /// <summary>Town level is too low for this building.</summary>
        Locked = 2,

        InvalidFootprint = 3,
        OutOfBounds = 4,
        Overlaps = 5,
        AreaLocked = 6,

        InsufficientFunds = 7,
        InsufficientItems = 8,

        BuildingNotFound = 9,

        /// <summary>Already building or upgrading. One job at a time.</summary>
        BuildingBusy = 10,

        /// <summary>At its last level with nothing to upgrade into.</summary>
        AlreadyMaxLevel = 11
    }
}
