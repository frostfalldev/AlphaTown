using System.Collections.Generic;
using AlphaTown.Data.Economy;

namespace AlphaTown.Data.Progression
{
    /// <summary>
    /// The town level curve: how much XP each level costs and what reaching it pays out.
    ///
    /// Authored rather than computed. The pacing curve is the single biggest retention lever in
    /// this genre and gets retuned constantly, so it must never be a formula in code.
    /// </summary>
    public interface IProgressionCurve
    {
        /// <summary>Levels are 1-based. The player starts at level 1.</summary>
        int MaxLevel { get; }

        /// <summary>XP needed to leave <paramref name="level"/>. Zero at the cap.</summary>
        int XpToAdvance(int level);

        /// <summary>Granted once, on reaching <paramref name="level"/>. Level 1 pays nothing.</summary>
        IReadOnlyList<CurrencyAmount> RewardsForReaching(int level);
    }
}
