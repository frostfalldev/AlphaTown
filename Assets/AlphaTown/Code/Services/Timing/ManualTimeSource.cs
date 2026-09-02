using System;

namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// A clock you drive by hand. Used by tests to fast-forward hours in microseconds, and by
    /// the debug menu to reproduce offline-progression bugs without changing device settings.
    /// </summary>
    public sealed class ManualTimeSource : ITimeSource
    {
        public ManualTimeSource(DateTime startUtc)
        {
            UtcNowTicks = startUtc.Ticks;
        }

        public ManualTimeSource() : this(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        {
        }

        public long UtcNowTicks { get; private set; }

        public void Advance(TimeSpan amount) => UtcNowTicks += amount.Ticks;

        public void SetTo(DateTime utc) => UtcNowTicks = utc.Ticks;
    }
}
