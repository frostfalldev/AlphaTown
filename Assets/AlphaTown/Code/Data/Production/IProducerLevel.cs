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

        /// <summary>
        /// Re-runs the last recipe when the output tray is emptied. This is the auto-replant
        /// upgrade for fields, and it is per-level so an upgrade can switch it on.
        ///
        /// It triggers on collection, never on completion — see Producer for why that bound matters.
        /// </summary>
        bool AutoRepeat { get; }
    }
}
