using System;
using System.Collections.Generic;
using AlphaTown.Data.Items;

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

        /// <summary>
        /// Roughly how much an order should want of each good, measured in coin value rather than
        /// units. Zero keeps the flat quantity range above.
        ///
        /// A flat range treats wheat and cake as the same ask, which is fine while orders are
        /// small and absurd once they are not: eighteen cakes is hours of bakery time and fifty
        /// crops, where eighteen wheat is one field. Sizing by value asks for cheap goods in bulk
        /// and expensive ones in handfuls, so every line of an order costs about the same effort.
        ///
        /// <see cref="Items.IItemDefinition.CoinValue"/> is the yardstick because it is already
        /// the one number that prices an item across the whole economy.
        /// </summary>
        int ValuePerItemType { get; }

        /// <summary>Zero means the order never expires.</summary>
        TimeSpan TimeLimit { get; }

        /// <summary>Scales the summed coin value of the requested goods.</summary>
        float CoinMultiplier { get; }

        /// <summary>Scales the summed XP value of the requested goods.</summary>
        float XpMultiplier { get; }

        /// <summary>Flat hard-currency bonus. Normally zero — this is a real-money-equivalent faucet.</summary>
        int BonusHardCurrency { get; }

        /// <summary>
        /// Items granted on completion on top of the coins — this is how land deeds reach the
        /// player. Empty on most templates.
        /// </summary>
        IReadOnlyList<ItemStack> BonusItems { get; }

        /// <summary>
        /// Chance from 0 to 1 that <see cref="BonusItems"/> is granted. Rolled once when the order
        /// is generated and baked into it, so the player can see the deed before committing goods.
        /// </summary>
        float BonusItemChance { get; }
    }
}
