namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// Where wall-clock time comes from. Abstracted so tests can drive it, and so a
    /// server-authoritative source can replace the device clock without touching the simulation.
    ///
    /// TODO(live-ops): add ServerTimeSource that syncs an offset against the backend on login.
    /// Device time is trivially spoofable — every timer in the game is a cheat surface until it lands.
    /// </summary>
    public interface ITimeSource
    {
        long UtcNowTicks { get; }
    }
}
