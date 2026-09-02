namespace AlphaTown.Gameplay.Production
{
    public readonly struct ProductionOrderQueuedEvent
    {
        public readonly string ProducerInstanceId;
        public readonly string RecipeId;
        public readonly int QueueIndex;

        public ProductionOrderQueuedEvent(string producerInstanceId, string recipeId, int queueIndex)
        {
            ProducerInstanceId = producerInstanceId;
            RecipeId = recipeId;
            QueueIndex = queueIndex;
        }
    }

    public readonly struct ProductionOrderStartedEvent
    {
        public readonly string ProducerInstanceId;
        public readonly string RecipeId;
        public readonly long CompletesAtTicks;

        public ProductionOrderStartedEvent(string producerInstanceId, string recipeId, long completesAtTicks)
        {
            ProducerInstanceId = producerInstanceId;
            RecipeId = recipeId;
            CompletesAtTicks = completesAtTicks;
        }
    }

    /// <summary>
    /// Fires for offline completions too, and a long absence can fire several at once. Anything
    /// listening for celebration effects must be able to coalesce.
    /// </summary>
    public readonly struct ProductionOrderCompletedEvent
    {
        public readonly string ProducerInstanceId;
        public readonly string RecipeId;

        public ProductionOrderCompletedEvent(string producerInstanceId, string recipeId)
        {
            ProducerInstanceId = producerInstanceId;
            RecipeId = recipeId;
        }
    }

    public readonly struct ProductionCollectedEvent
    {
        public readonly string ProducerInstanceId;
        public readonly int UnitsCollected;

        public ProductionCollectedEvent(string producerInstanceId, int unitsCollected)
        {
            ProducerInstanceId = producerInstanceId;
            UnitsCollected = unitsCollected;
        }
    }
}
