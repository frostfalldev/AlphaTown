namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// A counter that only ever moves forward, at a rate the player cannot change.
    ///
    /// This is the whole basis of the anti-cheat: it measures *elapsed* time without reference to
    /// the wall clock, so once a session knows one authoritative instant it can carry that instant
    /// forward without ever asking the device what time it is again.
    ///
    /// It has one weakness — the counter can stop while a device sleeps — which is why the app
    /// re-baselines and re-syncs on resume rather than trusting it across a suspend.
    /// </summary>
    public interface IMonotonicClock
    {
        /// <summary>TimeSpan ticks since an arbitrary fixed point. Never decreases.</summary>
        long ElapsedTicks { get; }
    }
}
