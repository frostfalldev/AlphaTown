using AlphaTown.Data.Progression;

namespace AlphaTown.Gameplay.Progression
{
    /// <summary>
    /// Town level and XP. The gate that paces which recipes, buildings and orders exist for
    /// this player.
    /// </summary>
    public interface ITownProgression : IUnlockGate
    {
        /// <summary>XP accumulated since the last level up.</summary>
        long XpIntoLevel { get; }

        /// <summary>XP still needed to advance. Zero at the cap.</summary>
        int XpToNextLevel { get; }

        /// <summary>Lifetime XP, which keeps climbing past the cap.</summary>
        long TotalXp { get; }

        bool IsMaxLevel { get; }

        /// <summary>
        /// Adds XP, cascading through as many levels as it covers. Returns the number of levels
        /// gained, which is often more than one after a long absence.
        /// </summary>
        int GrantXp(int amount, XpSource source, string context = null);

        long TotalXpFrom(XpSource source);
    }
}
