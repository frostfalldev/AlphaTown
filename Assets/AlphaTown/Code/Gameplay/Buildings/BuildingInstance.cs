using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Buildings;

namespace AlphaTown.Gameplay.Buildings
{
    /// <summary>
    /// One placed building.
    ///
    /// Construction is an absolute completion timestamp, exactly like production and orders, so a
    /// build started before the app closed is simply finished on the next launch — no ticking, no
    /// catch-up loop, and the same answer on any device.
    ///
    /// Mutation goes through <see cref="TownBuildings"/>, which owns the grid; changing an origin
    /// or footprint without it would leave occupancy out of step with reality.
    /// </summary>
    public sealed class BuildingInstance
    {
        internal BuildingInstance(string instanceId, IBuildingDefinition definition, GridPosition origin)
        {
            InstanceId = Guard.NotNullOrEmpty(instanceId, nameof(instanceId));
            Definition = Guard.NotNull(definition, nameof(definition));
            Origin = origin;
        }

        public string InstanceId { get; }

        public IBuildingDefinition Definition { get; private set; }

        public string DefinitionId => Definition.Id;

        public GridPosition Origin { get; private set; }

        /// <summary>Zero while the first build is still running.</summary>
        public int Level { get; private set; }

        /// <summary>The level being built toward. Equals <see cref="Level"/> when idle.</summary>
        public int TargetLevel { get; private set; }

        public long ConstructionStartedAtTicks { get; private set; }

        /// <summary>Only meaningful while <see cref="IsBusy"/>.</summary>
        public long ConstructionCompletesAtTicks { get; private set; }

        public GridRect Footprint => new GridRect(Origin, Definition.Footprint);

        /// <summary>
        /// Derived from the levels rather than from a flag or a sentinel timestamp, so it survives
        /// a save round trip without a separate field to keep in step — and so a build with zero
        /// construction time still reads as work in progress until the next Sync finishes it.
        /// </summary>
        public bool IsBusy => TargetLevel > Level;

        public BuildingState State =>
            Level <= 0 ? BuildingState.UnderConstruction
                : IsBusy ? BuildingState.Upgrading
                : BuildingState.Operational;

        public bool IsOperational => State == BuildingState.Operational;

        public long RemainingTicks(long nowTicks)
        {
            if (!IsBusy) return 0;

            var remaining = ConstructionCompletesAtTicks - nowTicks;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>0 to 1. Returns 1 when nothing is being built.</summary>
        public float Progress(long nowTicks)
        {
            if (!IsBusy) return 1f;

            var span = ConstructionCompletesAtTicks - ConstructionStartedAtTicks;
            if (span <= 0) return 1f;

            var elapsed = nowTicks - ConstructionStartedAtTicks;
            if (elapsed <= 0) return 0f;
            if (elapsed >= span) return 1f;

            return (float)((double)elapsed / span);
        }

        internal void BeginConstruction(int targetLevel, long startedAtTicks, long completesAtTicks)
        {
            TargetLevel = targetLevel;
            ConstructionStartedAtTicks = startedAtTicks;

            // Zero construction time is allowed and lands in the past-or-present, so the Sync that
            // follows completes it in the same call. Decorations are meant to build instantly.
            ConstructionCompletesAtTicks = completesAtTicks < startedAtTicks ? startedAtTicks : completesAtTicks;
        }

        /// <summary>
        /// Becomes a different definition and drops back to level 0. Used by transform upgrades,
        /// where the building is torn down and replaced rather than improved in place.
        /// </summary>
        internal void SwapDefinition(IBuildingDefinition definition)
        {
            Definition = Guard.NotNull(definition, nameof(definition));
            Level = 0;
            TargetLevel = 0;
        }

        internal void CompleteConstruction()
        {
            Level = TargetLevel > 0 ? TargetLevel : 1;
            TargetLevel = Level;
            ConstructionStartedAtTicks = 0;
            ConstructionCompletesAtTicks = 0;
        }

        /// <summary>Restores the level pair straight from save; busy-ness follows from it.</summary>

        internal void MoveTo(GridPosition origin) => Origin = origin;

        internal void RestoreState(int level, int targetLevel, long startedAtTicks, long completesAtTicks)
        {
            Level = level < 0 ? 0 : level;
            TargetLevel = targetLevel < Level ? Level : targetLevel;
            ConstructionStartedAtTicks = startedAtTicks;
            ConstructionCompletesAtTicks = completesAtTicks;
        }
    }
}
