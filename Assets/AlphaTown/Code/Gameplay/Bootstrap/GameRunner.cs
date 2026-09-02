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

        ServiceRegistry _services;
        EventBus _events;
        GameClock _clock;
        ISaveService _saveService;
        GameWorld _world;

        float _secondsSinceAutoSave;

        public GameWorld World => _world;
        public IEventBus Events => _events;
        public IGameClock Clock => _clock;

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
            _clock = new GameClock(new DeviceTimeSource());
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

            LoadOrCreate();
        }

        void Update()
        {
            if (_world == null) return;

            var delta = Time.unscaledDeltaTime;
            _clock.Tick(delta);
            _world.Tick(_clock.ScaledDeltaSeconds);

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

            // Back from the background: catch up before the player sees a stale town.
            _world.Sync();
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

            // TODO: seed a new town from a NewGameDefinition asset (starting barn level,
            // starting producers, tutorial state) instead of an empty world.
            Log.Info("Bootstrap", "No save found. Starting a new town.");
        }

        public void SaveGame()
        {
            if (_world == null || _saveService == null) return;

            _world.Sync();
            if (!_saveService.TrySave(GameWorld.DefaultSaveSlot, _world.CaptureSave()))
                Log.Error("Bootstrap", "Auto-save failed.");
        }
    }
}
