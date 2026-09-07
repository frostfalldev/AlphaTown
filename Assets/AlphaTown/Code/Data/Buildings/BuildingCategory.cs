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

        /// <summary>Crop fields. Swept with the sickle, sown with a tap.</summary>
        Farming = 5,

        Special = 6,

        /// <summary>
        /// Coops, sheds and pens. Producers like any other, and separated from
        /// <see cref="Farming"/> because they behave differently in the one way that matters: an
        /// animal eats. A field turns time into goods; livestock turns crops into better goods.
        /// </summary>
        Livestock = 7
    }
}
