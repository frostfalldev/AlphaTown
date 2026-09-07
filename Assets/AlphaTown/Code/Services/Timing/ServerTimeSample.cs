namespace AlphaTown.Services.Timing
{
    /// <summary>One answer from the backend about what time it is.</summary>
    public readonly struct ServerTimeSample
    {
        public readonly bool Success;

        /// <summary>UTC ticks as reported by the server, before latency compensation.</summary>
        public readonly long ServerUtcTicks;

        /// <summary>
        /// How long the request took. Half of it is added to the reading as an estimate of the
        /// one-way trip, so the sample lands closer to "now" than to "when the server answered".
        /// </summary>
        public readonly long RoundTripTicks;

        ServerTimeSample(bool success, long serverUtcTicks, long roundTripTicks)
        {
            Success = success;
            ServerUtcTicks = serverUtcTicks;
            RoundTripTicks = roundTripTicks;
        }

        public static ServerTimeSample Failed => new ServerTimeSample(false, 0, 0);

        public static ServerTimeSample From(long serverUtcTicks, long roundTripTicks) =>
            new ServerTimeSample(true, serverUtcTicks, roundTripTicks);
    }
}
