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
        /// Town level before this board exists at all.
        ///
        /// A board is gated as a whole rather than left to fall empty because its templates are
        /// locked: an empty board on screen reads as a bug, where a board that says when it opens
        /// reads as something to play towards.
        /// </summary>
        int UnlockLevel { get; }

        /// <summary>
        /// How long this slot stays empty before a new order appears. Per-slot so a board can
        /// have a fast first slot and slower later ones. Zero refills immediately.
        /// </summary>
        TimeSpan CooldownForSlot(int slotIndex);

        /// <summary>
        /// Least a reroll can cost, whatever the order was worth. Zero makes rerolling free,
        /// which turns the board into a slot machine the player pulls until it pays.
        /// </summary>
        int RerollBaseCost { get; }

        /// <summary>
        /// Reroll price as a percentage of the coins the order would have paid, floored by
        /// <see cref="RerollBaseCost"/>.
        ///
        /// Priced against the reward rather than flat, because that is what the player is
        /// actually giving up. Dodging a lucrative order they cannot fill should cost more than
        /// clearing a trivial one.
        /// </summary>
        int RerollCostPercent { get; }
    }
}
