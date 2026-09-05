using System;
using AlphaTown.Data.Orders;

namespace AlphaTown.Gameplay.Orders
{
    /// <summary>
    /// Used when the database has no <see cref="OrderBoardDefinition"/> for a kind.
    ///
    /// The cooldown is deliberately non-zero: an instantly refilling board is an unbounded coin
    /// faucet, and a project that has not authored its pacing yet should not discover that the
    /// hard way. Authoring a board definition is how you tune it, not how you turn it on.
    /// </summary>
    public sealed class FallbackOrderBoardDefinition : IOrderBoardDefinition
    {
        public const int DefaultSlotCount = 4;
        public const int DefaultCooldownSeconds = 300;

        public FallbackOrderBoardDefinition(OrderKind kind, int slotCount = DefaultSlotCount,
                                            int cooldownSeconds = DefaultCooldownSeconds)
        {
            Kind = kind;
            SlotCount = slotCount > 0 ? slotCount : DefaultSlotCount;
            Cooldown = TimeSpan.FromSeconds(cooldownSeconds > 0 ? cooldownSeconds : 0);
        }

        public string Id => "orderboard.fallback." + Kind;
        public OrderKind Kind { get; }
        public int SlotCount { get; }
        public TimeSpan Cooldown { get; }

        public TimeSpan CooldownForSlot(int slotIndex) => Cooldown;

        // Non-zero for the same reason the cooldown is: a project that has not tuned its pacing
        // should not discover that rerolling was free in production.
        public int RerollBaseCost => 25;
        public int RerollCostPercent => 40;
    }
}
