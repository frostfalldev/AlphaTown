namespace AlphaTown.Gameplay.Progression
{
    /// <summary>
    /// Lifetime XP from one source. <see cref="Source"/> is the numeric value of an
    /// <see cref="Data.Progression.XpSource"/>, kept as an int so a value written by a newer build
    /// survives a round trip through an older one instead of collapsing onto Unknown.
    /// </summary>
    public readonly struct XpAttributionEntry
    {
        public readonly int Source;
        public readonly long Total;

        public XpAttributionEntry(int source, long total)
        {
            Source = source;
            Total = total;
        }
    }
}
