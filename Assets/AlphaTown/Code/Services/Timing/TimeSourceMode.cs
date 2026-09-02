namespace AlphaTown.Services.Timing
{
    /// <summary>Which clock a build runs on. Chosen at the composition root.</summary>
    public enum TimeSourceMode
    {
        /// <summary>
        /// Server-verified, falling back to the device clock when offline. The only correct choice
        /// for a build that hands out anything on a timer.
        /// </summary>
        Server = 0,

        /// <summary>
        /// The raw device clock, with no verification at all. For local iteration where reaching
        /// the network on every play is a nuisance — never for a build a player will see.
        /// </summary>
        Device = 1
    }
}
