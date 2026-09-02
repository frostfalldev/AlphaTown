using System;

namespace AlphaTown.Data.Orders
{
    /// <summary>
    /// The recipe for generating an order, not an order itself.
    ///
    /// Templates describe shape and payout scaling; the generator fills in which items are asked
    /// for, drawing only from what the player can actually produce. That split is what keeps
    /// content authoring small — one template covers a whole tier of orders forever.
    /// </summary>
    public interface IOrderTemplateDefinition
    {
        string Id { get; }

        OrderKind Kind { get; }

        /// <summary>Town level before this template enters the generation pool.</summary>
        int UnlockLevel { get; }

        int MinItemTypes { get; }
        int MaxItemTypes { get; }
        int MinQuantityPerItem { get; }
        int MaxQuantityPerItem { get; }

        /// <summary>Zero means the order never expires.</summary>
        TimeSpan TimeLimit { get; }

        /// <summary>Scales the summed coin value of the requested goods.</summary>
        float CoinMultiplier { get; }

        /// <summary>Scales the summed XP value of the requested goods.</summary>
        float XpMultiplier { get; }

        /// <summary>Flat hard-currency bonus. Normally zero — this is a real-money-equivalent faucet.</summary>
        int BonusHardCurrency { get; }
    }
}
