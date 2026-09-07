using AlphaTown.Core.Spatial;

namespace AlphaTown.Gameplay.Buildings
{
    /// <summary>
    /// One building's persisted state, in runtime terms.
    ///
    /// The save DTOs stay in Gameplay.Saving; this is what they map onto, so the building system
    /// never has to know the shape of a save file.
    /// </summary>
    public readonly struct BuildingRestoreData
    {
        public readonly string InstanceId;
        public readonly string DefinitionId;
        public readonly GridPosition Origin;
        public readonly int Level;
        public readonly int TargetLevel;
        public readonly long ConstructionStartedAtTicks;
        public readonly long ConstructionCompletesAtTicks;

        public BuildingRestoreData(string instanceId, string definitionId, GridPosition origin, int level,
                                   int targetLevel, long constructionStartedAtTicks,
                                   long constructionCompletesAtTicks)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Origin = origin;
            Level = level;
            TargetLevel = targetLevel;
            ConstructionStartedAtTicks = constructionStartedAtTicks;
            ConstructionCompletesAtTicks = constructionCompletesAtTicks;
        }
    }
}
