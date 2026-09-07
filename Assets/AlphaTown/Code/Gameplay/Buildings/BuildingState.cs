namespace AlphaTown.Gameplay.Buildings
{
    /// <summary>Derived from level and construction timer rather than stored, so it cannot drift.</summary>
    public enum BuildingState
    {
        /// <summary>Level 0: the first build has not finished. Nothing runs yet.</summary>
        UnderConstruction = 0,

        Operational = 1,

        /// <summary>Built and running at its current level while the next one is paid for and timed.</summary>
        Upgrading = 2
    }
}
