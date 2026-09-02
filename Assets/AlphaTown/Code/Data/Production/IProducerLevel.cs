namespace AlphaTown.Data.Production
{
    /// <summary>Per-level tuning for a production building. Upgrades widen these numbers.</summary>
    public interface IProducerLevel
    {
        /// <summary>Total orders that may sit in the building, running plus waiting.</summary>
        int QueueCapacity { get; }

        /// <summary>How many orders run at once. Township-style buildings start at 1.</summary>
        int ParallelSlots { get; }

        /// <summary>Divides authored recipe duration. 2 means twice as fast.</summary>
        float SpeedMultiplier { get; }
    }
}
