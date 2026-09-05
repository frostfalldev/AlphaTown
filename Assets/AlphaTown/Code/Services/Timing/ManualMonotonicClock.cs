using System;

namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// A monotonic clock driven by hand. Tests use it together with <see cref="ManualTimeSource"/>
    /// to move the device clock and the real passage of time independently — which is exactly what
    /// a player tampering with their clock does.
    /// </summary>
    public sealed class ManualMonotonicClock : IMonotonicClock
    {
        public long ElapsedTicks { get; private set; }

        public void Advance(TimeSpan amount)
        {
            if (amount <= TimeSpan.Zero) return;
            ElapsedTicks += amount.Ticks;
        }
    }
}
