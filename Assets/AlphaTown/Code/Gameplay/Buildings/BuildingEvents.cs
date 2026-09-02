using AlphaTown.Core.Spatial;

namespace AlphaTown.Gameplay.Buildings
{
    public readonly struct BuildingPlacedEvent
    {
        public readonly string InstanceId;
        public readonly string DefinitionId;
        public readonly GridPosition Origin;

        public BuildingPlacedEvent(string instanceId, string definitionId, GridPosition origin)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Origin = origin;
        }
    }

    public readonly struct BuildingUpgradeStartedEvent
    {
        public readonly string InstanceId;
        public readonly string DefinitionId;
        public readonly int TargetLevel;
        public readonly long CompletesAtTicks;

        public BuildingUpgradeStartedEvent(string instanceId, string definitionId, int targetLevel,
                                           long completesAtTicks)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            TargetLevel = targetLevel;
            CompletesAtTicks = completesAtTicks;
        }
    }

    /// <summary>
    /// Fires for offline completions too, and a long absence can fire several at once. Anything
    /// playing a build-finished effect must be able to coalesce.
    /// </summary>
    public readonly struct BuildingConstructionCompletedEvent
    {
        public readonly string InstanceId;
        public readonly string DefinitionId;
        public readonly int Level;

        /// <summary>True for the first build, false for an upgrade finishing.</summary>
        public readonly bool WasInitialBuild;

        public BuildingConstructionCompletedEvent(string instanceId, string definitionId, int level,
                                                  bool wasInitialBuild)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Level = level;
            WasInitialBuild = wasInitialBuild;
        }
    }

    public readonly struct BuildingMovedEvent
    {
        public readonly string InstanceId;
        public readonly GridPosition From;
        public readonly GridPosition To;

        public BuildingMovedEvent(string instanceId, GridPosition from, GridPosition to)
        {
            InstanceId = instanceId;
            From = from;
            To = to;
        }
    }

    public readonly struct BuildingRemovedEvent
    {
        public readonly string InstanceId;
        public readonly string DefinitionId;

        public BuildingRemovedEvent(string instanceId, string definitionId)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
        }
    }
}
