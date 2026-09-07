namespace AlphaTown.Gameplay.Orders
{
    public readonly struct OrderGeneratedEvent
    {
        public readonly string OrderId;
        public readonly string TemplateId;
        public readonly long ExpiresAtTicks;

        public OrderGeneratedEvent(string orderId, string templateId, long expiresAtTicks)
        {
            OrderId = orderId;
            TemplateId = templateId;
            ExpiresAtTicks = expiresAtTicks;
        }
    }

    /// <summary>
    /// Delivered. <see cref="LevelsGained"/> is usually zero, and greater than one when a big
    /// order carries the player through several levels at once.
    /// </summary>
    public readonly struct OrderCompletedEvent
    {
        public readonly string OrderId;
        public readonly string TemplateId;
        public readonly int XpAwarded;
        public readonly int LevelsGained;

        public OrderCompletedEvent(string orderId, string templateId, int xpAwarded, int levelsGained)
        {
            OrderId = orderId;
            TemplateId = templateId;
            XpAwarded = xpAwarded;
            LevelsGained = levelsGained;
        }
    }

    /// <summary>
    /// A slot emptied and is now cooling. The hook a board UI uses to show a timer instead of a
    /// gap, and the signal that the coin faucet is throttled right now.
    /// </summary>
    public readonly struct OrderSlotCooldownStartedEvent
    {
        public readonly int SlotIndex;
        public readonly long AvailableAtTicks;

        public OrderSlotCooldownStartedEvent(int slotIndex, long availableAtTicks)
        {
            SlotIndex = slotIndex;
            AvailableAtTicks = availableAtTicks;
        }
    }

    /// <summary>
    /// A time-limited order ran out. Fires on the next Sync, which after a long absence means it
    /// can arrive for an order that expired days ago.
    /// </summary>
    public readonly struct OrderExpiredEvent
    {
        public readonly string OrderId;
        public readonly string TemplateId;

        public OrderExpiredEvent(string orderId, string templateId)
        {
            OrderId = orderId;
            TemplateId = templateId;
        }
    }

    /// <summary>
    /// An order was paid away and replaced. Separate from expiry and completion because it is the
    /// one that costs the player money — analytics wants to see how often the board is bad enough
    /// to buy out of.
    /// </summary>
    public readonly struct OrderRerolledEvent
    {
        public readonly string OrderId;
        public readonly int SlotIndex;
        public readonly int CoinsPaid;

        public OrderRerolledEvent(string orderId, int slotIndex, int coinsPaid)
        {
            OrderId = orderId;
            SlotIndex = slotIndex;
            CoinsPaid = coinsPaid;
        }
    }
}
