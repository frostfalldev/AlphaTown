using System.Diagnostics;

namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// Monotonic time from <see cref="Stopwatch"/>, which reads the platform's monotonic counter
    /// and is unaffected by changes to the system clock.
    /// </summary>
    public sealed class StopwatchMonotonicClock : IMonotonicClock
    {
        readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public long ElapsedTicks => _stopwatch.Elapsed.Ticks;
    }
}
