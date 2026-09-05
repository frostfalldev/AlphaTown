using System.Collections.Generic;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;

namespace AlphaTown.Data.Expansion
{
    /// <summary>
    /// One buyable patch of land.
    ///
    /// The gate is <see cref="ItemCost"/> — land deeds — not coins. A pure-coin gate collapses into
    /// "grind orders until rich", and coins are already the sink for buildings. Deeds come from
    /// orders at a rate the designer controls, so the town's growth stays paced by play rather than
    /// by arithmetic. Coins are supported as an optional secondary cost.
    /// </summary>
    public interface IExpansionDefinition
    {
        string Id { get; }

        string DisplayNameKey { get; }

        /// <summary>The rectangle this unlocks. Regions may touch but should not overlap.</summary>
        GridRect Region { get; }

        /// <summary>Town level before it can be bought at all.</summary>
        int UnlockLevel { get; }

        /// <summary>
        /// Expansion that must already be owned, or empty. This is what makes land spread outward
        /// from the town instead of letting a player buy a far corner first.
        /// </summary>
        string RequiresExpansionId { get; }

        /// <summary>The real gate: land deeds, and anything else the design wants.</summary>
        IReadOnlyList<ItemStack> ItemCost { get; }

        /// <summary>Optional secondary cost. Usually empty.</summary>
        IReadOnlyList<CurrencyAmount> CurrencyCost { get; }

        /// <summary>Presentation ordering for a land menu. Does not affect the rules.</summary>
        int SortOrder { get; }
    }
}
