namespace AlphaTown.Core.Timing
{
    /// <summary>
    /// How much the current session's clock can be believed.
    ///
    /// Every timer in the game — crops, construction, production, order cooldowns — is a
    /// comparison against wall-clock time, so this is the single flag that says whether those
    /// comparisons mean anything.
    /// </summary>
    public enum TimeTrust
    {
        /// <summary>
        /// Never synchronised. Running on the device's own clock, which the player can set to
        /// anything. Treat every completed timer as unverified.
        /// </summary>
        Untrusted = 0,

        /// <summary>
        /// A previous session reached the server, and this one is running on the device clock plus
        /// that stored offset. Better than nothing — it survives a device whose clock was already
        /// wrong — but the player can still move the clock between sessions.
        /// </summary>
        Stale = 1,

        /// <summary>
        /// Synchronised with the server this session. Time advances on a monotonic counter taken
        /// at the moment of sync, so changing the device clock now has no effect at all.
        /// </summary>
        Synchronized = 2
    }
}
