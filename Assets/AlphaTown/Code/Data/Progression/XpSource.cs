namespace AlphaTown.Data.Progression
{
    /// <summary>
    /// Where XP came from. In Data for the same reason as the currency taxonomy: the analytics
    /// service in Services needs the vocabulary without referencing Gameplay.
    ///
    /// XP has no sink — it is only ever granted — so there is no matching enum.
    /// </summary>
    public enum XpSource
    {
        /// <summary>Untagged. A bug — attribution records it so it can be alerted on.</summary>
        Unknown = 0,

        OrderReward = 1,
        ProductionCollected = 2,
        QuestReward = 3,
        BuildingConstructed = 4,
        AchievementReward = 5,

        DebugGrant = 90,
        Migration = 91
    }
}
