namespace AlphaTown.Gameplay.Production
{
    /// <summary>
    /// Lets the building system attach a producer to a finished building without knowing how
    /// producers are stored. <see cref="World.GameWorld"/> implements it.
    ///
    /// The seam exists so buildings and production stay independently testable: a building test
    /// can pass a recording stub and assert that a factory got a producer at the right level,
    /// without standing up the world.
    /// </summary>
    public interface IProducerHost
    {
        /// <summary>
        /// Returns the existing producer for this instance or creates one, then sets its level.
        /// Producer level tracks building level.
        /// </summary>
        Producer EnsureProducer(string instanceId, string producerDefinitionId, int level);

        /// <summary>Drops the producer and everything queued in it. False when there was none.</summary>
        bool RemoveProducer(string instanceId);
    }
}
