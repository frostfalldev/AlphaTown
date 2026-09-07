using AlphaTown.Data.Progression;

namespace AlphaTown.Gameplay.Progression
{
    public readonly struct XpGrantedEvent
    {
        public readonly int Amount;
        public readonly XpSource Source;
        public readonly long TotalXp;

        public XpGrantedEvent(int amount, XpSource source, long totalXp)
        {
            Amount = amount;
            Source = source;
            TotalXp = totalXp;
        }
    }

    /// <summary>
    /// Fires once per level gained, so a single large grant can raise several. Anything
    /// celebrating a level up must cope with a burst — after a long absence, or a big order,
    /// two or three arrive back to back.
    /// </summary>
    public readonly struct TownLevelUpEvent
    {
        public readonly int NewLevel;
        public readonly int PreviousLevel;
        public readonly bool IsMaxLevel;

        public TownLevelUpEvent(int newLevel, int previousLevel, bool isMaxLevel)
        {
            NewLevel = newLevel;
            PreviousLevel = previousLevel;
            IsMaxLevel = isMaxLevel;
        }
    }
}
