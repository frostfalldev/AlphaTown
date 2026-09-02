namespace AlphaTown.Gameplay.Grid
{
    /// <summary>Why a footprint cannot sit somewhere. Purely spatial — costs are not the grid's concern.</summary>
    public enum PlacementFailure
    {
        None = 0,

        /// <summary>A zero or negative footprint. A content bug, not a player action.</summary>
        InvalidFootprint = 1,

        OutOfBounds = 2,
        Overlaps = 3,

        /// <summary>Inside the grid but on land the player has not bought. TODO(expansion).</summary>
        AreaLocked = 4
    }
}
