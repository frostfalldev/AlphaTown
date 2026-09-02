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
}
