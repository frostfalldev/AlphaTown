using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Core.Spatial;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Buildings;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Economy;
using AlphaTown.Gameplay.Economy;
using AlphaTown.Gameplay.Grid;
using AlphaTown.Gameplay.Inventory;
using AlphaTown.Gameplay.Production;
using AlphaTown.Gameplay.Progression;

namespace AlphaTown.Gameplay.Buildings
{
    /// <summary>
    /// Everything placed in the town: what stands where, what it costs to raise and improve, and
    /// when construction finishes.
    ///
    /// This is the primary coin sink. Every charge goes through the wallet with a reason code, so
    /// the money spent on the town shows up in the economy numbers next to the money orders pay in.
    ///
    /// Construction uses absolute completion timestamps like production and orders, so a build
    /// started before the app closed resolves on the next <see cref="Sync"/> with no catch-up loop.
    /// </summary>
    public sealed class TownBuildings
    {
        readonly IGameDatabase _database;
        readonly IGameClock _clock;
        readonly IEventBus _events;
        readonly IWallet _wallet;
        readonly IInventory _barn;
        readonly IUnlockGate _unlocks;
        readonly IProducerHost _producers;
        readonly TownGrid _grid;

        readonly List<BuildingInstance> _buildings = new List<BuildingInstance>(32);
        readonly Dictionary<string, BuildingInstance> _byId = new Dictionary<string, BuildingInstance>(32);

        int _nextInstanceNumber = 1;

        public TownBuildings(
            TownGrid grid,
            IGameDatabase database,
            IGameClock clock,
            IEventBus events,
            IWallet wallet,
            IInventory barn,
            IUnlockGate unlocks,
            IProducerHost producers)
        {
            _grid = Guard.NotNull(grid, nameof(grid));
            _database = Guard.NotNull(database, nameof(database));
            _clock = Guard.NotNull(clock, nameof(clock));
            _events = Guard.NotNull(events, nameof(events));
            _wallet = Guard.NotNull(wallet, nameof(wallet));
            _barn = Guard.NotNull(barn, nameof(barn));
            _unlocks = Guard.NotNull(unlocks, nameof(unlocks));
            _producers = Guard.NotNull(producers, nameof(producers));
        }

        public TownGrid Grid => _grid;

        public IReadOnlyList<BuildingInstance> All => _buildings;

        public int NextInstanceNumber => _nextInstanceNumber;

        public bool TryGetBuilding(string instanceId, out BuildingInstance building) =>
            _byId.TryGetValue(instanceId ?? string.Empty, out building);

        /// <summary>Dry run of everything <see cref="TryPlace"/> checks. Nothing is charged.</summary>
        public BuildingActionResult ValidatePlacement(string definitionId, GridPosition origin)
        {
            if (!_database.TryGetBuilding(definitionId, out var definition))
                return BuildingActionResult.UnknownDefinition;

            if (!_unlocks.IsUnlocked(definition.UnlockLevel)) return BuildingActionResult.Locked;

            var placement = Map(_grid.Validate(new GridRect(origin, definition.Footprint)));
            if (placement != BuildingActionResult.Success) return placement;

            return CheckCost(definition.GetLevel(1));
        }

        /// <summary>Buys and places a building. The coin sink is <see cref="CurrencySink.BuildingPurchase"/>.</summary>
        public BuildingActionResult TryPlace(string definitionId, GridPosition origin, out string instanceId)
        {
            instanceId = null;

            if (!_database.TryGetBuilding(definitionId, out var definition))
                return BuildingActionResult.UnknownDefinition;

            if (!_unlocks.IsUnlocked(definition.UnlockLevel)) return BuildingActionResult.Locked;

            var rect = new GridRect(origin, definition.Footprint);
            var placement = Map(_grid.Validate(rect));
            if (placement != BuildingActionResult.Success) return placement;

            var firstLevel = definition.GetLevel(1);
            var affordable = CheckCost(firstLevel);
            if (affordable != BuildingActionResult.Success) return affordable;

            instanceId = "building_" + _nextInstanceNumber++;
            ChargeCost(firstLevel, CurrencySink.BuildingPurchase, instanceId);

            var building = new BuildingInstance(instanceId, definition, origin);
            _grid.Occupy(rect, instanceId);
            _buildings.Add(building);
            _byId.Add(instanceId, building);

            var now = _clock.UtcNowTicks;
            building.BeginConstruction(1, now, now + firstLevel.ConstructionTime.Ticks);
            _events.Publish(new BuildingPlacedEvent(instanceId, definition.Id, origin));

            // An instant build (zero construction time) finishes here rather than a second later.
            Sync();
            return BuildingActionResult.Success;
        }

        /// <summary>
        /// Improves a building: the next level within its definition, or — once it is at its last
        /// level and has somewhere to go — the definition it upgrades into. Sink is
        /// <see cref="CurrencySink.BuildingUpgrade"/> either way.
        /// </summary>
        public BuildingActionResult TryUpgrade(string instanceId)
        {
            if (!_byId.TryGetValue(instanceId ?? string.Empty, out var building))
                return BuildingActionResult.BuildingNotFound;

            if (building.IsBusy) return BuildingActionResult.BuildingBusy;

            return building.Level < building.Definition.MaxLevel
                ? UpgradeInPlace(building)
                : UpgradeIntoNewDefinition(building);
        }

        /// <summary>
        /// Relocates a building. Allowed while it is building or upgrading: where it stands has
        /// nothing to do with the timer running on it.
        /// </summary>
        public BuildingActionResult TryMove(string instanceId, GridPosition origin)
        {
            if (!_byId.TryGetValue(instanceId ?? string.Empty, out var building))
                return BuildingActionResult.BuildingNotFound;

            var rect = new GridRect(origin, building.Definition.Footprint);
            var placement = Map(_grid.Validate(rect, building.InstanceId));
            if (placement != BuildingActionResult.Success) return placement;

            var from = building.Origin;
            _grid.Release(building.Footprint, building.InstanceId);
            building.MoveTo(origin);
            _grid.Occupy(rect, building.InstanceId);

            _events.Publish(new BuildingMovedEvent(building.InstanceId, from, origin));
            return BuildingActionResult.Success;
        }

        /// <summary>
        /// Demolishes a building and drops its producer.
        ///
        /// Nothing is refunded. TODO: selling for a fraction of the build cost, granted through
        /// <see cref="CurrencySource.Refund"/> so it nets against the original sink.
        /// </summary>
        public BuildingActionResult TryRemove(string instanceId)
        {
            if (!_byId.TryGetValue(instanceId ?? string.Empty, out var building))
                return BuildingActionResult.BuildingNotFound;

            _grid.Release(building.Footprint, building.InstanceId);
            _byId.Remove(building.InstanceId);
            _buildings.Remove(building);
            _producers.RemoveProducer(building.InstanceId);

            _events.Publish(new BuildingRemovedEvent(building.InstanceId, building.Definition.Id));
            return BuildingActionResult.Success;
        }

        /// <summary>
        /// Finishes any construction whose timestamp has passed. Cost is proportional to the number
        /// of builds that completed, not to how long the player was away.
        /// </summary>
        public void Sync()
        {
            var now = _clock.UtcNowTicks;

            for (var i = 0; i < _buildings.Count; i++)
            {
                var building = _buildings[i];
                if (!building.IsBusy || building.ConstructionCompletesAtTicks > now) continue;

                var wasInitialBuild = building.Level <= 0;
                building.CompleteConstruction();
                AttachProducer(building);

                _events.Publish(new BuildingConstructionCompletedEvent(
                    building.InstanceId, building.Definition.Id, building.Level, wasInitialBuild));
            }
        }

        /// <summary>Restores from save. Call <see cref="Sync"/> afterwards to finish offline builds.</summary>
        public void RestoreState(IReadOnlyList<BuildingRestoreData> entries, int nextInstanceNumber)
        {
            _grid.Clear();
            _buildings.Clear();
            _byId.Clear();
            _nextInstanceNumber = nextInstanceNumber > 0 ? nextInstanceNumber : 1;

            if (entries == null) return;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrEmpty(entry.InstanceId) || _byId.ContainsKey(entry.InstanceId)) continue;

                if (!_database.TryGetBuilding(entry.DefinitionId, out var definition))
                {
                    Log.Error("Buildings",
                        "Save references unknown building '" + entry.DefinitionId + "'; dropping " +
                        entry.InstanceId + ".");
                    continue;
                }

                var rect = new GridRect(entry.Origin, definition.Footprint);
                var failure = _grid.Validate(rect);
                if (failure != PlacementFailure.None)
                {
                    // Only reachable when a footprint changed after the save was written, which
                    // must not happen post-launch. TODO: a relocation pass that finds the nearest
                    // legal spot rather than dropping the player's building.
                    Log.Error("Buildings",
                        "Cannot restore '" + entry.InstanceId + "' at " + entry.Origin + " (" + failure +
                        "). It has been dropped.");
                    continue;
                }

                var building = new BuildingInstance(entry.InstanceId, definition, entry.Origin);
                building.RestoreState(entry.Level, entry.TargetLevel,
                    entry.ConstructionStartedAtTicks, entry.ConstructionCompletesAtTicks);

                _grid.Occupy(rect, entry.InstanceId);
                _buildings.Add(building);
                _byId.Add(entry.InstanceId, building);
            }

            // Producers restored from save are matched back to their buildings here; anything
            // missing is recreated, so a save written before a building gained a producer heals.
            for (var i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].IsOperational) AttachProducer(_buildings[i]);
            }
        }

        // --- Upgrade paths ----------------------------------------------------------------------

        BuildingActionResult UpgradeInPlace(BuildingInstance building)
        {
            var targetLevel = building.Level + 1;
            var level = building.Definition.GetLevel(targetLevel);

            var affordable = CheckCost(level);
            if (affordable != BuildingActionResult.Success) return affordable;

            ChargeCost(level, CurrencySink.BuildingUpgrade, building.InstanceId);

            // The building keeps running at its current level while the upgrade is timed, which
            // is what makes an upgrade a decision rather than a shutdown.
            var now = _clock.UtcNowTicks;
            building.BeginConstruction(targetLevel, now, now + level.ConstructionTime.Ticks);

            _events.Publish(new BuildingUpgradeStartedEvent(
                building.InstanceId, building.Definition.Id, targetLevel,
                building.ConstructionCompletesAtTicks));

            Sync();
            return BuildingActionResult.Success;
        }

        BuildingActionResult UpgradeIntoNewDefinition(BuildingInstance building)
        {
            var nextId = building.Definition.UpgradesIntoId;
            if (string.IsNullOrEmpty(nextId)) return BuildingActionResult.AlreadyMaxLevel;

            if (!_database.TryGetBuilding(nextId, out var next)) return BuildingActionResult.UnknownDefinition;
            if (!_unlocks.IsUnlocked(next.UnlockLevel)) return BuildingActionResult.Locked;

            // The replacement may have a different footprint, so it has to fit before anything is
            // charged. Its own cells are ignored — it is allowed to overlap where it already is.
            var rect = new GridRect(building.Origin, next.Footprint);
            var placement = Map(_grid.Validate(rect, building.InstanceId));
            if (placement != BuildingActionResult.Success) return placement;

            var firstLevel = next.GetLevel(1);
            var affordable = CheckCost(firstLevel);
            if (affordable != BuildingActionResult.Success) return affordable;

            ChargeCost(firstLevel, CurrencySink.BuildingUpgrade, building.InstanceId);

            // Swap now rather than on completion, so the grid reserves the replacement's footprint
            // for the whole build instead of leaving it free for something else to take.
            _grid.Release(building.Footprint, building.InstanceId);
            building.SwapDefinition(next);
            _grid.Occupy(rect, building.InstanceId);

            // A transform replaces the machine, so queued production goes with it.
            // TODO: refund queued inputs, or refuse the upgrade while orders are running.
            _producers.RemoveProducer(building.InstanceId);

            var now = _clock.UtcNowTicks;
            building.BeginConstruction(1, now, now + firstLevel.ConstructionTime.Ticks);

            _events.Publish(new BuildingUpgradeStartedEvent(
                building.InstanceId, next.Id, 1, building.ConstructionCompletesAtTicks));

            Sync();
            return BuildingActionResult.Success;
        }

        // --- Cost -------------------------------------------------------------------------------

        BuildingActionResult CheckCost(IBuildingLevel level)
        {
            if (!_wallet.CanAffordAll(level.CurrencyCost)) return BuildingActionResult.InsufficientFunds;
            if (!_barn.ContainsAll(level.ItemCost)) return BuildingActionResult.InsufficientItems;

            return BuildingActionResult.Success;
        }

        /// <summary>
        /// Charges coins and materials together.
        ///
        /// Both halves are checked by <see cref="CheckCost"/> before either is applied, and nothing
        /// runs in between, so neither can fail here. Taking coins and then failing to take planks
        /// would charge the player for a building they never got.
        /// </summary>
        void ChargeCost(IBuildingLevel level, CurrencySink sink, string context)
        {
            _wallet.TrySpendAll(level.CurrencyCost, sink, context);
            _barn.TryRemoveAll(level.ItemCost);
        }

        void AttachProducer(BuildingInstance building)
        {
            var producerId = building.Definition.ProducerDefinitionId;
            if (string.IsNullOrEmpty(producerId)) return;

            _producers.EnsureProducer(building.InstanceId, producerId, building.Level);
        }

        static BuildingActionResult Map(PlacementFailure failure)
        {
            switch (failure)
            {
                case PlacementFailure.None: return BuildingActionResult.Success;
                case PlacementFailure.InvalidFootprint: return BuildingActionResult.InvalidFootprint;
                case PlacementFailure.OutOfBounds: return BuildingActionResult.OutOfBounds;
                case PlacementFailure.Overlaps: return BuildingActionResult.Overlaps;
                case PlacementFailure.AreaLocked: return BuildingActionResult.AreaLocked;
                default: return BuildingActionResult.OutOfBounds;
            }
        }
    }
}
