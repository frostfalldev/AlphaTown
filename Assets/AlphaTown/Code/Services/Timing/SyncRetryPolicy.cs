using System;

namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// How hard to keep trying to reach the time server.
    ///
    /// Backoff matters more here than in most retry loops: a phone that cannot reach the network
    /// usually cannot reach it for a while, and hammering the request costs battery for nothing.
    /// The delay grows to a ceiling and then stays there, so a session that starts on a plane
    /// still verifies itself within minutes of landing.
    /// </summary>
    public sealed class SyncRetryPolicy
    {
        /// <summary>Guards the growth loop against a multiplier that never actually grows.</summary>
        const float MinimumMultiplier = 1.1f;
        const int MaximumGrowthSteps = 32;

        public SyncRetryPolicy(
            int maxAttempts = 0,
            TimeSpan? initialDelay = null,
            float backoffMultiplier = 2f,
            TimeSpan? maxDelay = null)
        {
            MaxAttempts = maxAttempts < 0 ? 0 : maxAttempts;
            InitialDelay = initialDelay ?? TimeSpan.FromSeconds(5);
            BackoffMultiplier = backoffMultiplier < MinimumMultiplier ? MinimumMultiplier : backoffMultiplier;
            MaxDelay = maxDelay ?? TimeSpan.FromMinutes(5);

            if (InitialDelay <= TimeSpan.Zero) InitialDelay = TimeSpan.FromSeconds(1);
            if (MaxDelay < InitialDelay) MaxDelay = InitialDelay;
        }

        /// <summary>Default: keep trying forever, 5s growing to a 5 minute ceiling.</summary>
        public static SyncRetryPolicy Default => new SyncRetryPolicy();

        /// <summary>Zero means keep trying for as long as the session lasts.</summary>
        public int MaxAttempts { get; }

        public TimeSpan InitialDelay { get; }

        public float BackoffMultiplier { get; }

        public TimeSpan MaxDelay { get; }

        public bool ShouldRetry(int failedAttempts) => MaxAttempts <= 0 || failedAttempts < MaxAttempts;

        /// <summary>Delay before the attempt that follows <paramref name="failedAttempts"/> failures.</summary>
        public TimeSpan DelayForAttempt(int failedAttempts)
        {
            if (failedAttempts < 1) failedAttempts = 1;

            var ceiling = (double)MaxDelay.Ticks;
            var delayTicks = (double)InitialDelay.Ticks;

            var steps = failedAttempts - 1;
            if (steps > MaximumGrowthSteps) steps = MaximumGrowthSteps;

            for (var i = 0; i < steps; i++)
            {
                delayTicks *= BackoffMultiplier;
                if (delayTicks >= ceiling) return MaxDelay;
            }

            return delayTicks >= ceiling ? MaxDelay : TimeSpan.FromTicks((long)delayTicks);
        }
    }
}
