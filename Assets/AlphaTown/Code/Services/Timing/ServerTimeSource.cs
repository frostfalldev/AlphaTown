using System;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Timing;

namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// Wall-clock time that a player cannot move.
    ///
    /// The idea is one sentence long: **ask the device what time it is as rarely as possible.**
    /// On a successful sync the source records one authoritative instant together with a reading
    /// of a monotonic counter, and from then on it answers with
    /// <c>authoritativeInstant + (monotonicNow - monotonicThen)</c>. The device clock is not
    /// consulted again, so setting it forward mid-session does nothing whatsoever.
    ///
    /// The device clock is still needed at two moments — the first baseline before any sync, and
    /// after a suspend, since the monotonic counter can stop while a device sleeps. Both are
    /// marked by dropping <see cref="Trust"/>, and both are repaired by the next sync.
    ///
    /// See docs/TIME_AND_ANTI_CHEAT.md for the threat model and what remains exposed.
    /// </summary>
    public sealed class ServerTimeSource : ITimeSource
    {
        /// <summary>
        /// How far the device clock may drift before it is treated as tampering rather than an
        /// ordinary correction. Generous, because a legitimate NTP step can be minutes.
        /// </summary>
        public static readonly TimeSpan DefaultClockJumpTolerance = TimeSpan.FromMinutes(5);

        readonly ITimeSource _device;
        readonly IMonotonicClock _monotonic;
        readonly IServerTimeProvider _provider;
        readonly SyncRetryPolicy _retryPolicy;
        readonly long _toleranceTicks;

        Action<bool> _syncCallback;
        long _nextRetryMonotonicTicks;
        int _failedAttempts;
        bool _syncInFlight;
        bool _retryPending;

        long _baselineUtcTicks;
        long _baselineMonotonicTicks;
        long _offsetTicks;
        long _floorTicks;
        bool _hasOffset;
        TimeTrust _trust;

        public ServerTimeSource(
            ITimeSource deviceTime,
            IMonotonicClock monotonic,
            IServerTimeProvider provider = null,
            TimeSpan? clockJumpTolerance = null,
            SyncRetryPolicy retryPolicy = null)
        {
            _device = Guard.NotNull(deviceTime, nameof(deviceTime));
            _monotonic = Guard.NotNull(monotonic, nameof(monotonic));
            _provider = provider;
            _retryPolicy = retryPolicy ?? SyncRetryPolicy.Default;
            _toleranceTicks = (clockJumpTolerance ?? DefaultClockJumpTolerance).Ticks;

            BaselineFromDevice();
        }

        /// <summary>Raised the first time the device clock is seen to move by more than the tolerance.</summary>
        public event Action<long> ClockJumpDetected;

        public long UtcNowTicks
        {
            get
            {
                var elapsed = _monotonic.ElapsedTicks - _baselineMonotonicTicks;
                if (elapsed < 0) elapsed = 0;

                var now = _baselineUtcTicks + elapsed;
                return now < _floorTicks ? _floorTicks : now;
            }
        }

        public TimeTrust Trust => _trust;

        public bool IsTrusted => _trust == TimeTrust.Synchronized;

        /// <summary>Server UTC minus device UTC at the last sync. Persisted.</summary>
        public long OffsetTicks => _offsetTicks;

        public bool HasOffset => _hasOffset;

        /// <summary>True once the device clock has been caught moving during this session.</summary>
        public bool HasDetectedClockJump { get; private set; }

        /// <summary>Syncs that have failed since the last success. Reset by a successful sample.</summary>
        public int FailedSyncAttempts => _failedAttempts;

        /// <summary>True while a retry is waiting for its backoff to elapse.</summary>
        public bool IsRetryPending => _retryPending;

        /// <summary>Restores the offset and floor written by a previous session.</summary>
        public void RestoreState(TimeSyncSaveData state)
        {
            if (state == null) return;

            _offsetTicks = state.OffsetTicks;
            _hasOffset = state.HasOffset;
            _floorTicks = state.LastKnownUtcTicks > 0 ? state.LastKnownUtcTicks : 0;

            // A sync that already happened this session is better than anything on disk.
            if (_trust != TimeTrust.Synchronized) BaselineFromDevice();
        }

        public TimeSyncSaveData CaptureState() =>
            new TimeSyncSaveData
            {
                OffsetTicks = _offsetTicks,
                HasOffset = _hasOffset,
                LastKnownUtcTicks = UtcNowTicks
            };

        /// <summary>
        /// Asks the backend for the time and applies the answer. Failing is normal — being offline
        /// is a supported state, not an error — and leaves the previous offset in place.
        /// </summary>
        public void RequestSync(Action<bool> onComplete = null)
        {
            _syncCallback = onComplete;
            _failedAttempts = 0;
            _retryPending = false;

            BeginSyncAttempt();
        }

        /// <summary>
        /// Fires a retry whose backoff has elapsed. Call it from the same periodic pump as
        /// <see cref="PollDeviceDrift"/>; it is a comparison and a branch when nothing is due.
        ///
        /// Retries are driven by the monotonic clock rather than a timer or a coroutine, so the
        /// whole thing is testable by advancing a number, and a device clock change cannot make
        /// the backoff appear to have elapsed.
        /// </summary>
        public bool TickSync()
        {
            if (!_retryPending || _syncInFlight) return false;
            if (_monotonic.ElapsedTicks < _nextRetryMonotonicTicks) return false;

            _retryPending = false;
            BeginSyncAttempt();
            return true;
        }

        void BeginSyncAttempt()
        {
            if (_provider == null)
            {
                Log.Warn("Time",
                    "No server time provider configured. Running on " + _trust +
                    " time — every timer in the game is unverified.");
                _syncCallback?.Invoke(false);
                return;
            }

            if (_syncInFlight) return;

            _syncInFlight = true;
            _provider.RequestTime(OnSampleReceived);
        }

        void OnSampleReceived(ServerTimeSample sample)
        {
            _syncInFlight = false;

            if (sample.Success)
            {
                ApplyServerSample(sample.ServerUtcTicks, sample.RoundTripTicks);
                _failedAttempts = 0;
                _retryPending = false;
                _syncCallback?.Invoke(true);
                return;
            }

            _failedAttempts++;
            ScheduleRetry();
            _syncCallback?.Invoke(false);
        }

        void ScheduleRetry()
        {
            if (!_retryPolicy.ShouldRetry(_failedAttempts))
            {
                _retryPending = false;
                Log.Warn("Time",
                    "Gave up syncing after " + _failedAttempts + " attempts. Running on " + _trust +
                    " time until something asks again.");
                return;
            }

            var delay = _retryPolicy.DelayForAttempt(_failedAttempts);
            _nextRetryMonotonicTicks = _monotonic.ElapsedTicks + delay.Ticks;
            _retryPending = true;

            Log.Warn("Time",
                "Server time unavailable (attempt " + _failedAttempts + "). Retrying in " + delay +
                "; continuing on " + _trust + " time.");
        }

        /// <summary>
        /// Takes a server reading as the new truth. Half the round trip is added as an estimate of
        /// the one-way trip, so the instant lands nearer "now" than "when the server answered".
        /// </summary>
        public void ApplyServerSample(long serverUtcTicks, long roundTripTicks)
        {
            if (serverUtcTicks <= 0)
            {
                Log.Error("Time", "Ignoring a server sample with a nonsense timestamp.");
                return;
            }

            var oneWay = roundTripTicks > 0 ? roundTripTicks / 2 : 0;
            var serverNow = serverUtcTicks + oneWay;

            _baselineUtcTicks = serverNow;
            _baselineMonotonicTicks = _monotonic.ElapsedTicks;
            _offsetTicks = serverNow - _device.UtcNowTicks;
            _hasOffset = true;

            // The server outranks the floor. Without this, a session poisoned by a clock set far
            // into the future would keep that inflated floor forever and never recover.
            _floorTicks = serverNow;

            _trust = TimeTrust.Synchronized;
            HasDetectedClockJump = false;

            Log.Info("Time",
                "Synchronised. Device clock is off by " + TimeSpan.FromTicks(_offsetTicks) + ".");
        }

        /// <summary>
        /// Compares the device clock against our own reckoning and reports the difference.
        /// Cheap enough to call about once a second; deliberately not done inside
        /// <see cref="UtcNowTicks"/>, which is read by every timer in the game.
        ///
        /// While synchronised this is pure signal — the drift changes nothing, because the device
        /// clock is not what time is derived from. It is worth reporting anyway: a device whose
        /// clock leaps mid-session is worth knowing about.
        /// </summary>
        public long PollDeviceDrift()
        {
            if (!_hasOffset) return 0;

            var expectedDeviceTicks = UtcNowTicks - _offsetTicks;
            var drift = _device.UtcNowTicks - expectedDeviceTicks;
            var magnitude = drift < 0 ? -drift : drift;

            if (magnitude < _toleranceTicks) return drift;

            if (!HasDetectedClockJump)
            {
                HasDetectedClockJump = true;

                Log.Warn("Time",
                    "Device clock moved by " + TimeSpan.FromTicks(drift) + " during this session.");

                // While synchronised the jump is harmless. Otherwise the baseline came from this
                // clock, so everything since is suspect and the session says so.
                if (_trust != TimeTrust.Synchronized) _trust = TimeTrust.Untrusted;

                ClockJumpDetected?.Invoke(drift);
            }

            return drift;
        }

        /// <summary>
        /// Re-establishes the baseline after the app was suspended.
        ///
        /// The monotonic counter can stop while a device sleeps, so on resume our reckoning may be
        /// behind reality. Time is allowed to jump *forward* to the device's reading and never
        /// backward. This is the one place the device clock can still push time on, so trust drops
        /// and the caller should sync immediately.
        /// </summary>
        public void RebaselineAfterSuspend()
        {
            var ourReckoning = UtcNowTicks;
            var deviceReckoning = _device.UtcNowTicks + (_hasOffset ? _offsetTicks : 0);
            var movedForward = deviceReckoning > ourReckoning;

            _baselineUtcTicks = movedForward ? deviceReckoning : ourReckoning;
            _baselineMonotonicTicks = _monotonic.ElapsedTicks;

            if (!movedForward) return;

            if (_trust == TimeTrust.Synchronized)
            {
                _trust = _hasOffset ? TimeTrust.Stale : TimeTrust.Untrusted;
                Log.Info("Time", "Resumed from suspend on the device clock. Re-syncing.");
            }
        }

        void BaselineFromDevice()
        {
            _baselineUtcTicks = _device.UtcNowTicks + (_hasOffset ? _offsetTicks : 0);
            _baselineMonotonicTicks = _monotonic.ElapsedTicks;
            _trust = _hasOffset ? TimeTrust.Stale : TimeTrust.Untrusted;
        }
    }
}
