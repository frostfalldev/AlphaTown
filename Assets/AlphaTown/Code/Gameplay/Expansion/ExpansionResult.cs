namespace AlphaTown.Gameplay.Expansion
{
    /// <summary>
    /// Outcome of a land purchase. An enum rather than a bool because each failure needs its own
    /// answer: missing deeds points at the order board, a missing prerequisite points at the
    /// neighbouring plot, and a level requirement points at the whole game.
    /// </summary>
    public enum ExpansionResult
    {
        Success = 0,

        UnknownExpansion = 1,

        /// <summary>Already owned. Land is bought once and kept.</summary>
        AlreadyUnlocked = 2,

        /// <summary>The plot it grows from has not been bought yet.</summary>
        PrerequisiteNotMet = 3,

        /// <summary>Town level is too low.</summary>
        Locked = 4,

        /// <summary>Not enough land deeds. The usual answer.</summary>
        InsufficientItems = 5,

        InsufficientFunds = 6,

        /// <summary>The authored region is empty or falls outside the town. A content bug.</summary>
        InvalidRegion = 7
    }
}
