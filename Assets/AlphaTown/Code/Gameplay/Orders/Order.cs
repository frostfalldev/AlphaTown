using System;
using System.Collections.Generic;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;

namespace AlphaTown.Gameplay.Orders
{
    /// <summary>
    /// A concrete delivery request, generated from a template and then frozen.
    ///
    /// Rewards are baked in at generation time rather than recomputed on completion. If item
    /// values are retuned in a live-ops update, orders already on the board still pay what they
    /// promised — a player who stockpiled goods for a visible reward must get that reward.
    /// </summary>
    public sealed class Order
    {
        public Order(
            string orderId,
            string templateId,
            OrderKind kind,
            IReadOnlyList<ItemStack> requests,
            IReadOnlyList<CurrencyAmount> currencyRewards,
            IReadOnlyList<ItemStack> itemRewards,
            int xpReward,
            long createdAtTicks,
            long expiresAtTicks)
        {
            OrderId = orderId;
            TemplateId = templateId;
            Kind = kind;
            Requests = requests ?? Array.Empty<ItemStack>();
            CurrencyRewards = currencyRewards ?? Array.Empty<CurrencyAmount>();
            ItemRewards = itemRewards ?? Array.Empty<ItemStack>();
            XpReward = xpReward;
            CreatedAtTicks = createdAtTicks;
            ExpiresAtTicks = expiresAtTicks;
        }

        public string OrderId { get; }
        public string TemplateId { get; }
        public OrderKind Kind { get; }
        public IReadOnlyList<ItemStack> Requests { get; }
        public IReadOnlyList<CurrencyAmount> CurrencyRewards { get; }

        /// <summary>
        /// Items paid on completion — how land deeds reach the player. Rolled when the order was
        /// generated, so the deed is visible before any goods are committed to it.
        /// </summary>
        public IReadOnlyList<ItemStack> ItemRewards { get; }
        public int XpReward { get; }
        public long CreatedAtTicks { get; }

        /// <summary>Zero means the order never expires.</summary>
        public long ExpiresAtTicks { get; }

        public bool HasTimeLimit => ExpiresAtTicks > 0;

        public bool IsExpired(long nowTicks) => HasTimeLimit && nowTicks >= ExpiresAtTicks;

        public long RemainingTicks(long nowTicks)
        {
            if (!HasTimeLimit) return long.MaxValue;

            var remaining = ExpiresAtTicks - nowTicks;
            return remaining > 0 ? remaining : 0;
        }
    }
}
