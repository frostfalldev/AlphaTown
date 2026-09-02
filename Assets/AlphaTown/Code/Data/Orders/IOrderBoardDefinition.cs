using System;

namespace AlphaTown.Data.Orders
{
    /// <summary>
    /// The pacing of one delivery board: how many slots it has and how long each stays empty
    /// after the order in it is completed or expires.
    ///
    /// Cooldowns are the throttle on the game's main coin faucet. With fields producing for free,
    /// an instantly refilling board turns into unbounded income, so this is authored data rather
    /// than a constant — it is a number that gets retuned every time the economy is rebalanced.
    /// </summary>
    public interface IOrderBoardDefinition
    {
        string Id { get; }

        OrderKind Kind { get; }

        /// <summary>Number of slots. Each holds at most one order.</summary>
        int SlotCount { get; }

        /// <summary>
        /// How long this slot stays empty before a new order appears. Per-slot so a board can
        /// have a fast first slot and slower later ones. Zero refills immediately.
        /// </summary>
        TimeSpan CooldownForSlot(int slotIndex);
    }
}
