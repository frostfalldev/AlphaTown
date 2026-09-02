using System;
using AlphaTown.Core.Timing;

namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// The device's own clock. Always available, never verified — a player who sets their clock
    /// forward finishes every timer in the game instantly.
    ///
    /// Use it as the fallback inside <see cref="ServerTimeSource"/> rather than on its own.
    /// </summary>
    public sealed class DeviceTimeSource : ITimeSource
    {
        public long UtcNowTicks => DateTime.UtcNow.Ticks;

        public TimeTrust Trust => TimeTrust.Untrusted;
    }
}
