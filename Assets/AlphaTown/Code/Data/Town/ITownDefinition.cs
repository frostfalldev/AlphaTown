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

        /// <summary>Buildable bounds in cells.</summary>
        GridSize Size { get; }
    }
}
