using AlphaTown.Core.Spatial;

namespace AlphaTown.Data.Buildings
{
    /// <summary>
    /// A placeable structure: how much room it takes, what it costs to raise and upgrade, and
    /// what it runs once it is standing.
    /// </summary>
    public interface IBuildingDefinition
    {
        string Id { get; }

        string DisplayNameKey { get; }

        BuildingCategory Category { get; }

        /// <summary>Cells occupied, axis-aligned. Rotation is presentation, not simulation.</summary>
        GridSize Footprint { get; }

        /// <summary>Town level before this building can be bought.</summary>
        int UnlockLevel { get; }

        /// <summary>Highest level within this definition. Levels are 1-based.</summary>
        int MaxLevel { get; }

        /// <summary>Clamped, so a save from a future build can never index out of bounds.</summary>
        IBuildingLevel GetLevel(int level);

        /// <summary>
        /// Producer this building runs once built, or empty for a building that produces nothing.
        /// The producer's level tracks the building's level.
        /// </summary>
        string ProducerDefinitionId { get; }

        /// <summary>
        /// Definition this becomes when upgraded past <see cref="MaxLevel"/>, or empty for none.
        /// A small house becoming a large one, rather than a house gaining a level.
        /// </summary>
        string UpgradesIntoId { get; }
    }
}
