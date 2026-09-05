using System;
using System.Collections.Generic;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;

namespace AlphaTown.Data.Buildings
{
    /// <summary>
    /// What one level of a building costs and how long it takes.
    ///
    /// Level 1 is the initial construction; every later entry is an upgrade to that level. Costs
    /// are the price of *reaching* the level, so the numbers read the way a designer thinks about
    /// them in a build menu.
    /// </summary>
    public interface IBuildingLevel
    {
        /// <summary>Zero builds instantly — normal for decorations.</summary>
        TimeSpan ConstructionTime { get; }

        IReadOnlyList<CurrencyAmount> CurrencyCost { get; }

        /// <summary>Materials taken from the barn. Township-style builds want planks and nails.</summary>
        IReadOnlyList<ItemStack> ItemCost { get; }

        /// <summary>
        /// The barn level this building grants while it stands. Zero for anything that is not
        /// storage, which is almost everything.
        ///
        /// A level rather than a capacity, so the numbers stay in one place: the storage
        /// definition owns how much space each level is worth, and this only says which level the
        /// player has earned. Retuning capacity is then one asset, not two.
        /// </summary>
        int StorageLevel { get; }

        /// <summary>
        /// XP paid when this level finishes building.
        ///
        /// It is what makes a decoration worth buying: it produces nothing and stores nothing, so
        /// without a reward for raising it there is no reason to spend the coins. Production
        /// buildings can pay it too — the reward for a long build being over.
        /// </summary>
        int XpReward { get; }
    }
}
