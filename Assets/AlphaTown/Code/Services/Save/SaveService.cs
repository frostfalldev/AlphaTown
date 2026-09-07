using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Timing;

namespace AlphaTown.Services.Save
{
    /// <summary>
    /// Envelope + migration pipeline on top of an <see cref="ISaveStore"/>.
    ///
    /// Load path: read envelope, walk migrations up to the current schema version, deserialize.
    /// A save from a *newer* build is refused rather than partially read — downgrading a player
    /// by silently dropping fields they have already paid for is worse than failing to load.
    /// </summary>
    public sealed class SaveService : ISaveService
    {
        readonly ISaveStore _store;
        readonly ISaveSerializer _serializer;
        readonly IGameClock _clock;
        readonly List<ISaveMigration> _migrations = new List<ISaveMigration>();
        readonly string _appVersion;

        public SaveService(
            ISaveStore store,
            ISaveSerializer serializer,
            IGameClock clock,
            int currentSchemaVersion,
            IEnumerable<ISaveMigration> migrations = null,
            string appVersion = null)
        {
            _store = Guard.NotNull(store, nameof(store));
            _serializer = Guard.NotNull(serializer, nameof(serializer));
            _clock = Guard.NotNull(clock, nameof(clock));
            CurrentSchemaVersion = Guard.Positive(currentSchemaVersion, nameof(currentSchemaVersion));
            _appVersion = appVersion ?? UnityEngine.Application.version;

            if (migrations == null) return;
            foreach (var migration in migrations)
            {
                if (migration != null) _migrations.Add(migration);
            }
        }

        public int CurrentSchemaVersion { get; }

        public bool Exists(string slot) => _store.Exists(slot);

        public bool TrySave<TData>(string slot, TData data) where TData : class
        {
            Guard.NotNull(data, nameof(data));

            var envelope = new SaveEnvelope
            {
                SchemaVersion = CurrentSchemaVersion,
                SavedAtUtcTicks = _clock.UtcNowTicks,
                AppVersion = _appVersion,
                Payload = _serializer.Serialize(data)
            };

            return _store.TryWrite(slot, _serializer.Serialize(envelope));
        }

        public bool TryLoad<TData>(string slot, out TData data) where TData : class
        {
            data = null;

            if (!_store.TryRead(slot, out var text)) return false;
            if (!_serializer.TryDeserialize<SaveEnvelope>(text, out var envelope)) return false;

            if (envelope.SchemaVersion > CurrentSchemaVersion)
            {
                Log.Error("Save",
                    "Save '" + slot + "' is schema v" + envelope.SchemaVersion + " but this build reads v" +
                    CurrentSchemaVersion + ". Refusing to load rather than dropping data.");
                return false;
            }

            if (!TryMigrate(slot, envelope, out var payload)) return false;

            return _serializer.TryDeserialize(payload, out data);
        }

        public bool Delete(string slot) => _store.Delete(slot);

        bool TryMigrate(string slot, SaveEnvelope envelope, out string payload)
        {
            payload = envelope.Payload;
            var version = envelope.SchemaVersion;

            while (version < CurrentSchemaVersion)
            {
                var migration = FindMigration(version);
                if (migration == null)
                {
                    Log.Error("Save",
                        "No migration from schema v" + version + " for save '" + slot + "'.");
                    return false;
                }

                if (migration.ToVersion <= version)
                {
                    Log.Error("Save",
                        "Migration from v" + version + " does not move forward. Aborting to avoid a loop.");
                    return false;
                }

                payload = migration.Migrate(payload);
                version = migration.ToVersion;
                Log.Info("Save", "Migrated '" + slot + "' to schema v" + version + ".");
            }

            return true;
        }

        ISaveMigration FindMigration(int fromVersion)
        {
            for (var i = 0; i < _migrations.Count; i++)
            {
                if (_migrations[i].FromVersion == fromVersion) return _migrations[i];
            }

            return null;
        }
    }
}
