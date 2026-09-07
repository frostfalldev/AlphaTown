namespace AlphaTown.Data.Orders
{
    /// <summary>
    /// Delivery channel an order belongs to. Each kind is its own board with its own capacity,
    /// pacing and reward scaling.
    ///
    /// Only <see cref="Helicopter"/> is wired up. The rest are named now so that order data,
    /// save data and analytics do not need reshaping when they land.
    /// </summary>
    public enum OrderKind
    {
        /// <summary>Individual small orders, always available, the everyday coin and XP faucet.</summary>
        Helicopter = 0,

        /// <summary>Batched multi-wagon orders on a cooldown. TODO.</summary>
        Train = 1,

        /// <summary>Large timed orders with premium rewards. TODO.</summary>
        Ship = 2,

        /// <summary>Limited-time event orders paying event currency. TODO.</summary>
        Event = 3
    }
}
