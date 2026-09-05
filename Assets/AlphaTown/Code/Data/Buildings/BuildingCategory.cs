namespace AlphaTown.Data.Buildings
{
    /// <summary>Grouping for build menus, analytics and unlock pacing.</summary>
    public enum BuildingCategory
    {
        Production = 0,
        Storage = 1,
        Housing = 2,
        Community = 3,
        Decoration = 4,

        /// <summary>Fields and pens. Wired up as ordinary producers in the next phase.</summary>
        Farming = 5,

        Special = 6
    }
}
