using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Core.Services;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Catalog;
using AlphaTown.Gameplay.Commands;
using AlphaTown.Gameplay.Saving;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Save;
using AlphaTown.Services.Timing;
using UnityEngine;

namespace AlphaTown.Gameplay.Bootstrap
{
    /// <summary>
    /// The composition root and the only MonoBehaviour in the simulation.
    ///
    /// It builds the services, owns the world, and pumps it. Everything it constructs is plain
    /// C#, so the same object graph stands up in an EditMode test without a scene.
    ///
    /// TODO: move to an additive Boot scene with a loading flow once there is content to load.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameRunner : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] GameDatabase _database;

        [Header("Runtime")]
        [SerializeField, Min(30)]
        [Tooltip("Frame rate is driven here, not by vsync. 60 costs battery; revisit per device tier.")]
        int _targetFrameRate = 60;

        [SerializeField, Min(5f)] float _autoSaveIntervalSeconds = 30f;

        [Header("Time")]
        [SerializeField]
        [Tooltip("Server-verified time, or the raw device clock for local iteration. " +
                 "Never ship Device: every timer in the game is a comparison against this.")]
        TimeSourceMode _timeSourceMode = TimeSourceMode.Server;

        [SerializeField]
        [Tooltip("URL whose HTTP Date header is read as the time. Your own backend, ideally. " +
                 "Leave empty and the session runs on unverified device time.")]
        string _timeServerUrl = string.Empty;

        [SerializeField, Min(1)]
        [Tooltip("Seconds before a time request is abandoned.")]
        int _timeSyncTimeoutSeconds = 10;

        [SerializeField, Min(1f)]
        [Tooltip("Delay before the first retry. Doubles each failure, up to five minutes.")]
        float _timeSyncRetryDelaySeconds = 5f;

        [SerializeField, Min(0)]
        [Tooltip("Zero keeps retrying for the life of the session, which is what a phone that " +
                 "walks back into signal wants.")]
        int _timeSyncMaxAttempts;

        /// <summary>How often the device clock is checked for tampering. Cheap, but not free.</summary>
        const float ClockDriftPollSeconds = 1f;

        /// <summary>Its own slot: the clock's offset is infrastructure, not the player's town.</summary>
        const string TimeSaveSlot = "time";

        ServiceRegistry _services;
        EventBus _events;
        GameClock _clock;
        /// <summary>Non-null only in Server mode. Everything that touches it is guarded.</summary>
        ServerTimeSource _timeSource;

        /// <summary>Non-null only in Manual mode, so a debug menu can drive time by hand.</summary>
        ManualTimeSource _manualTime;

        ISaveService _saveService;
        GameWorld _world;
        TownCommands _commands;

        float _secondsSinceAutoSave;
        float _secondsSinceDriftPoll;
        float _secondsSinceSaveRequest;
        bool _saveRequested;

        /// <summary>
        /// How long a requested save waits before it is written. Long enough that a burst of taps
        /// costs one file write, short enough that a player killed from the task switcher a moment
        /// after harvesting keeps the harvest.
        /// </summary>
        const float SaveDebounceSeconds = 2f;

        public GameWorld World => _world;
        public IEventBus Events => _events;
        public IGameClock Clock => _clock;

        /// <summary>Every player-facing action, for the UI. Null until Awake has run.</summary>
        public TownCommands Commands => _commands;

        public IGameDatabase Database => _database;

        /// <summary>Whether this session's timers can be believed. See TimeTrust.</summary>
        public TimeTrust Trust => _clock != null ? _clock.Trust : TimeTrust.Untrusted;

        /// <summary>The hand-driven clock in Manual mode, for time-travel debugging. Null otherwise.</summary>
        public ManualTimeSource ManualTime => _manualTime;

        void Awake()
        {
            if (_database == null)
            {
                Log.Error("Bootstrap", "GameRunner has no GameDatabase assigned. Disabling.");
                enabled = false;
                return;
            }

            Application.targetFrameRate = _targetFrameRate;
            QualitySettings.vSyncCount = 0;

            _events = new EventBus();
            _clock = new GameClock(BuildTimeSource());
            _saveService = new SaveService(
                FileSaveStore.CreateDefault(),
                new JsonSaveSerializer(UnityEngine.Debug.isDebugBuild),
                _clock,
                GameWorld.SaveSchemaVersion);

            _world = new GameWorld(_database, _clock, _events);
            _commands = new TownCommands(_world, _database, _clock);

            _services = new ServiceRegistry();
            _services.Register<IEventBus>(_events);
            _services.Register<IGameClock>(_clock);
            _services.Register(_saveService);
            _services.Register(_world);

            if (_timeSource != null)
            {
                _timeSource.ClockJumpDetected += OnClockJumpDetected;
                RestoreTimeState();
            }

            LoadOrCreate();

            // Fired off after the world exists so the answer can catch it up when it lands.
            if (_timeSource != null) _timeSource.RequestSync(OnSyncCompleted);
        }

        /// <summary>
        /// Picks the clock. One switch, three concrete sources, and everything downstream reads
        /// the same <see cref="ITimeSource"/> without knowing which it got.
        /// </summary>
        ITimeSource BuildTimeSource()
        {
            if (_timeSourceMode == TimeSourceMode.Manual)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Log.Warn("Bootstrap",
                    "Running on a hand-driven clock. Debug only — drive it through GameRunner.ManualTime.");

                _manualTime = new ManualTimeSource(System.DateTime.UtcNow);
                return _manualTime;
#else
                Log.Error("Bootstrap",
                    "Manual time is not available in a release build. Using server time instead.");
#endif
            }

            if (_timeSourceMode == TimeSourceMode.Device)
            {
                Log.Warn("Bootstrap",
                    "Running on the raw device clock. Every timer in the game is unverified, and a " +
                    "player who changes their clock finishes all of them at once. Do not ship this.");

                return new DeviceTimeSource();
            }

            IServerTimeProvider provider = null;
            if (string.IsNullOrEmpty(_timeServerUrl))
            {
                Log.Warn("Bootstrap",
                    "Time source is Server but no URL is set. The session runs on unverified device " +
                    "time until one is configured.");
            }
            else
            {
                provider = new HttpDateHeaderTimeProvider(_timeServerUrl, _timeSyncTimeoutSeconds);
            }

            _timeSource = new ServerTimeSource(
                new DeviceTimeSource(),
                new StopwatchMonotonicClock(),
                provider,
                retryPolicy: new SyncRetryPolicy(
                    _timeSyncMaxAttempts,
                    System.TimeSpan.FromSeconds(_timeSyncRetryDelaySeconds)));

            return _timeSource;
        }

        void RestoreTimeState()
        {
            if (_saveService.TryLoad<TimeSyncSaveData>(TimeSaveSlot, out var state))
            {
                _timeSource.RestoreState(state);
                return;
            }

            Log.Info("Bootstrap", "No stored clock offset. First run on this device.");
        }

        void OnSyncCompleted(bool success)
        {
            if (!success)
            {
                // ServerTimeSource has already scheduled a retry and logged the backoff.
                Log.Warn("Bootstrap",
                    "Playing on " + Trust + " time. Timers will be verified once the server answers.");
                return;
            }

            // The authoritative time may differ from what the world just caught up to.
            if (_world != null) _world.Sync();
            SaveTimeState();
        }

        void OnClockJumpDetected(long driftTicks)
        {
            // TODO(live-ops): report this to analytics. A device clock that leaps mid-session is
            // the clearest tampering signal the client can produce on its own.
            Log.Warn("Bootstrap",
                "Device clock jumped by " + System.TimeSpan.FromTicks(driftTicks) + ". Re-syncing.");

            if (_timeSource != null) _timeSource.RequestSync(OnSyncCompleted);
        }

        void Update()
        {
            if (_world == null) return;

            var delta = Time.unscaledDeltaTime;
            _clock.Tick(delta);
            _world.Tick(_clock.ScaledDeltaSeconds);

            _secondsSinceDriftPoll += delta;
            if (_secondsSinceDriftPoll >= ClockDriftPollSeconds)
            {
                _secondsSinceDriftPoll = 0f;

                if (_timeSource != null)
                {
                    _timeSource.PollDeviceDrift();
                    _timeSource.TickSync();
                }
            }

            if (_saveRequested)
            {
                _secondsSinceSaveRequest += delta;
                if (_secondsSinceSaveRequest >= SaveDebounceSeconds)
                {
                    SaveGame();
                    return;
                }
            }

            _secondsSinceAutoSave += delta;
            if (_secondsSinceAutoSave < _autoSaveIntervalSeconds) return;

            SaveGame();
        }

        /// <summary>
        /// The save point that actually matters on mobile: Android can kill a backgrounded app
        /// without ever calling OnApplicationQuit.
        /// </summary>
        void OnApplicationPause(bool paused)
        {
            if (_world == null) return;

            if (paused)
            {
                SaveGame();
                return;
            }

            // The monotonic counter can stop while a device sleeps, so the clock is re-based
            // before anything reads it, and re-verified as soon as the network allows.
            if (_timeSource != null) _timeSource.RebaselineAfterSuspend();

            _world.Sync();

            if (_timeSource != null) _timeSource.RequestSync(OnSyncCompleted);
        }

        void OnApplicationQuit() => SaveGame();

        void LoadOrCreate()
        {
            if (_saveService.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var save))
            {
                _world.RestoreSave(save);
                Log.Info("Bootstrap", "Loaded save and caught up.");
                return;
            }

            // Starting balances, then whatever the database's NewGameDefinition seeds: barn
            // level, starting goods and the buildings the player wakes up owning.
            // TODO: tutorial state belongs here too, once there is a tutorial.
            _world.InitialiseNewPlayer();
            Log.Info("Bootstrap", "No save found. Started a new town.");
        }

        /// <summary>
        /// Asks for a save without writing one now. Called after anything the player would be
        /// upset to lose — planting, harvesting, building, delivering.
        ///
        /// Debounced rather than immediate because a sickle swipe harvests a dozen fields in a
        /// second, and serialising the whole town a dozen times would show up as a stutter under
        /// the player's finger.
        /// </summary>
        public void RequestSave()
        {
            _saveRequested = true;
        }

        public void SaveGame()
        {
            if (_world == null || _saveService == null) return;

            _saveRequested = false;
            _secondsSinceSaveRequest = 0f;
            _secondsSinceAutoSave = 0f;

            _world.Sync();
            if (!_saveService.TrySave(GameWorld.DefaultSaveSlot, _world.CaptureSave()))
                Log.Error("Bootstrap", "Auto-save failed.");

            SaveTimeState();
        }

        void SaveTimeState()
        {
            if (_timeSource == null || _saveService == null) return;

            _saveService.TrySave(TimeSaveSlot, _timeSource.CaptureState());
        }
    }
}
