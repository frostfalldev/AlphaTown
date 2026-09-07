using System;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Timing;

namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// Wall-clock based game clock.
    ///
    /// Time-gated systems store absolute completion timestamps and compare them against
    /// <see cref="UtcNowTicks"/>. That is what makes offline progression free: nothing is
    /// simulated while the app is closed, because nothing needs to be — a wheat field that
    /// finished six hours ago simply reads as finished on the next launch.
    /// </summary>
    public sealed class GameClock : IGameClock, ITickable
    {
        readonly ITimeSource _source;

        long _offsetTicks;
        long _pausedAtTicks;
        bool _isPaused;
        float _timeScale = 1f;

        public GameClock(ITimeSource source)
        {
            _source = Guard.NotNull(source, nameof(source));
        }

        public DateTime UtcNow => new DateTime(UtcNowTicks, DateTimeKind.Utc);

        public long UtcNowTicks => (_isPaused ? _pausedAtTicks : _source.UtcNowTicks) + _offsetTicks;

        public float ScaledDeltaSeconds { get; private set; }

        public float TimeScale => _timeScale;

        public bool IsPaused => _isPaused;

        /// <summary>Forwarded from the time source. See <see cref="TimeTrust"/>.</summary>
        public TimeTrust Trust => _source.Trust;

        /// <summary>Convenience for the common question: can this session's timers be believed?</summary>
        public bool IsTimeTrusted => _source.Trust == TimeTrust.Synchronized;

        public void SetTimeScale(float scale) => _timeScale = Math.Max(0f, scale);

        /// <summary>
        /// Freezes simulation time. Editor and debug only — production timers are supposed to
        /// keep running while the app is backgrounded, so nothing in the shipping game pauses this.
        /// </summary>
        public void Pause()
        {
            if (_isPaused) return;

            _pausedAtTicks = _source.UtcNowTicks;
            _isPaused = true;
            ScaledDeltaSeconds = 0f;
        }

        public void Resume()
        {
            if (!_isPaused) return;

            // Absorb the paused span into the offset so simulation time stays continuous.
            _offsetTicks -= _source.UtcNowTicks - _pausedAtTicks;
            _isPaused = false;
        }

        /// <summary>
        /// Jumps simulation time forward. Debug and test only, and deliberately not persisted.
        ///
        /// The player-facing "speed up" mechanic must NOT go through here: it shortens one
        /// order via the producer's own API, so the effect survives a restart and cannot be
        /// used to fast-forward the entire town.
        /// </summary>
        public void Advance(TimeSpan amount) => _offsetTicks += amount.Ticks;

        public void Tick(float deltaSeconds)
        {
            if (_isPaused)
            {
                ScaledDeltaSeconds = 0f;
                return;
            }

            ScaledDeltaSeconds = deltaSeconds * _timeScale;

            // A time scale other than 1 bends wall-clock time, which is why it is debug-only too.
            if (_timeScale != 1f)
                _offsetTicks += (long)((_timeScale - 1f) * deltaSeconds * TimeSpan.TicksPerSecond);
        }
    }
}
