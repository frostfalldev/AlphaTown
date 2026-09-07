using AlphaTown.Core.Timing;

namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// Where wall-clock time comes from. Abstracted so tests can drive it, and so a
    /// server-authoritative source can replace the device clock without the simulation noticing.
    ///
    /// <see cref="ServerTimeSource"/> is the one to use in a build; the device clock is trivially
    /// spoofable and every timer in the game is a comparison against it.
    /// </summary>
    public interface ITimeSource
    {
        long UtcNowTicks { get; }

        /// <summary>How far this source can be believed. Surfaced to gameplay through IGameClock.</summary>
        TimeTrust Trust { get; }
    }
}
