using AlphaTown.Core.Spatial;

namespace AlphaTown.Data.Town
{
    /// <summary>
    /// Town-wide configuration. Optional: the world falls back to sane defaults when the database
    /// has none, so a project can get running before anyone has authored one.
    /// </summary>
    public interface ITownDefinition
    {
        string Id { get; }

        /// <summary>Maximum extent in cells. Expansion unlocks regions inside this.</summary>
        GridSize Size { get; }

        /// <summary>
        /// The patch the player starts on. A zero-sized rect means "the whole grid", which is what
        /// a project with no expansion content wants.
        /// </summary>
        GridRect StartingArea { get; }
    }
}
