using System;

namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// Fetches the current time from the backend.
    ///
    /// Callback-based rather than blocking, because a real implementation is a network round trip
    /// and nothing may stall a frame waiting for it. Swappable so tests can answer instantly and
    /// so the transport can change without the clock noticing.
    /// </summary>
    public interface IServerTimeProvider
    {
        /// <summary>
        /// Asks for the time. The callback runs with <see cref="ServerTimeSample.Failed"/> when the
        /// server cannot be reached — being offline is normal, not an error.
        /// </summary>
        void RequestTime(Action<ServerTimeSample> onComplete);
    }
}
