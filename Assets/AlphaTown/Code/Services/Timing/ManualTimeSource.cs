using System;
using AlphaTown.Core.Timing;

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

        /// <summary>
        /// Reported as synchronised: a test drives this clock directly, so it is exactly as
        /// authoritative as the test intends. It also keeps trust-aware code on its normal path
        /// in every existing test rather than on a fallback branch.
        /// </summary>
        public TimeTrust Trust => TimeTrust.Synchronized;

        public void Advance(TimeSpan amount) => UtcNowTicks += amount.Ticks;

        public void SetTo(DateTime utc) => UtcNowTicks = utc.Ticks;
    }
}
