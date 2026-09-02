using System;

namespace AlphaTown.Core.Timing
{
    /// <summary>
    /// The single source of "now" for every time-gated system.
    ///
    /// Nothing in the simulation reads DateTime.UtcNow or UnityEngine.Time directly. Everything
    /// goes through this, which is what makes offline progression, debug time travel and
    /// deterministic tests all work through the same code path.
    /// </summary>
    public interface IGameClock
    {
        /// <summary>Simulation time. Wall clock plus any accumulated offset from pause or speed-ups.</summary>
        DateTime UtcNow { get; }

        /// <summary>Same value as <see cref="UtcNow"/> in ticks. Prefer this in hot paths and save data.</summary>
        long UtcNowTicks { get; }

        /// <summary>Frame delta with <see cref="TimeScale"/> applied. For animation, not for timers.</summary>
        float ScaledDeltaSeconds { get; }

        float TimeScale { get; }

        bool IsPaused { get; }
    }
}
