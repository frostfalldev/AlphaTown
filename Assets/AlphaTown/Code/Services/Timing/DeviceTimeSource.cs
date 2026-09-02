using System;

namespace AlphaTown.Services.Timing
{
    /// <summary>The device's own clock. Correct offline, and trusting — see <see cref="ITimeSource"/>.</summary>
    public sealed class DeviceTimeSource : ITimeSource
    {
        public long UtcNowTicks => DateTime.UtcNow.Ticks;
    }
}
