namespace AlphaTown.Services.Timing
{
    /// <summary>Which clock a build runs on. Chosen at the composition root.</summary>
    public enum TimeSourceMode
    {
        /// <summary>
        /// <see cref="ServerTimeSource"/>: verified against the backend, falling back to the device
        /// clock when offline. The only correct choice for a build that hands out anything on a timer.
        /// </summary>
        Server = 0,

        /// <summary>
        /// <see cref="DeviceTimeSource"/>: the raw device clock with no verification and no
        /// monotonic protection. For local iteration where reaching the network on every play is a
        /// nuisance — never for a build a player will see.
        /// </summary>
        Device = 1,

        /// <summary>
        /// <see cref="ManualTimeSource"/>: a clock driven by hand, for time-travel debugging.
        /// Editor and development builds only; a release build refuses it and uses server time.
        /// </summary>
        Manual = 2
    }
}
