using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Core.Services;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Catalog;
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

        /// <summary>How often the device clock is checked for tampering. Cheap, but not free.</summary>
        const float ClockDriftPollSeconds = 1f;

        /// <summary>Its own slot: the clock's offset is infrastructure, not the player's town.</summary>
        const string TimeSaveSlot = "time";

        ServiceRegistry _services;
        EventBus _events;
        GameClock _clock;
        ServerTimeSource _timeSource;
        ISaveService _saveService;
        GameWorld _world;

        float _secondsSinceAutoSave;
        float _secondsSinceDriftPoll;

        public GameWorld World => _world;
        public IEventBus Events => _events;
        public IGameClock Clock => _clock;

        /// <summary>Whether this session's timers can be believed. See TimeTrust.</summary>
        public TimeTrust Trust => _timeSource != null ? _timeSource.Trust : TimeTrust.Untrusted;

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
            _timeSource = BuildTimeSource();
            _timeSource.ClockJumpDetected += OnClockJumpDetected;
            _clock = new GameClock(_timeSource);
            _saveService = new SaveService(
                FileSaveStore.CreateDefault(),
                new JsonSaveSerializer(UnityEngine.Debug.isDebugBuild),
                _clock,
                GameWorld.SaveSchemaVersion);

            _world = new GameWorld(_database, _clock, _events);

            _services = new ServiceRegistry();
            _services.Register<IEventBus>(_events);
            _services.Register<IGameClock>(_clock);
            _services.Register(_saveService);
            _services.Register(_world);

            RestoreTimeState();
            LoadOrCreate();

            // Fired off after the world exists so the answer can catch it up when it lands.
            _timeSource.RequestSync(OnSyncCompleted);
        }

        /// <summary>
        /// Both modes go through <see cref="ServerTimeSource"/>; Device mode simply has nothing to
        /// ask. Even unsynced, that beats the raw device clock — time still advances on a
        /// monotonic counter, so moving the clock mid-session does nothing.
        /// </summary>
        ServerTimeSource BuildTimeSource()
        {
            IServerTimeProvider provider = null;

            if (_timeSourceMode == TimeSourceMode.Server)
            {
                if (string.IsNullOrEmpty(_timeServerUrl))
                {
                    Log.Warn("Bootstrap",
                        "Time source is Server but no URL is set. Falling back to unverified device time.");
                }
                else
                {
                    provider = new HttpDateHeaderTimeProvider(_timeServerUrl);
                }
            }
            else
            {
                Log.Warn("Bootstrap",
                    "Running on device time. Timers are unverified — this must not ship.");
            }

            return new ServerTimeSource(new DeviceTimeSource(), new StopwatchMonotonicClock(), provider);
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
                Log.Warn("Bootstrap",
                    "Playing on " + _timeSource.Trust + " time. Timers will be verified once the " +
                    "server is reachable.");
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

            _timeSource.RequestSync(OnSyncCompleted);
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
                _timeSource.PollDeviceDrift();
            }

            _secondsSinceAutoSave += delta;
            if (_secondsSinceAutoSave < _autoSaveIntervalSeconds) return;

            _secondsSinceAutoSave = 0f;
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
            _timeSource.RebaselineAfterSuspend();
            _world.Sync();
            _timeSource.RequestSync(OnSyncCompleted);
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

            // Starting balances from the currency definitions, then a first board of orders.
            // TODO: extend with a NewGameDefinition asset (starting barn level, starting
            // producers, tutorial state) rather than leaving the town otherwise empty.
            _world.InitialiseNewPlayer();
            Log.Info("Bootstrap", "No save found. Started a new town.");
        }

        public void SaveGame()
        {
            if (_world == null || _saveService == null) return;

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
