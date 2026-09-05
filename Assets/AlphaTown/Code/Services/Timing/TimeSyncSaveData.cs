using System;

namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// The clock's own persisted state.
    ///
    /// Kept out of the game save on purpose: this is infrastructure, not progress. A player's town
    /// and the device's clock offset have nothing to do with each other, and rolling one back
    /// should never roll back the other.
    /// </summary>
    [Serializable]
    public sealed class TimeSyncSaveData
    {
        /// <summary>Server UTC minus device UTC at the last successful sync.</summary>
        public long OffsetTicks;

        /// <summary>False until the backend has been reached at least once on this device.</summary>
        public bool HasOffset;

        /// <summary>
        /// The latest time the game believed. Restored as a floor so a clock wound backwards
        /// cannot un-complete something the player has already seen finish.
        /// </summary>
        public long LastKnownUtcTicks;
    }
}
