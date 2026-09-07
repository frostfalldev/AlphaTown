using System.Collections.Generic;
using AlphaTown.Data.Recipes;

namespace AlphaTown.Data.Production
{
    /// <summary>A building that turns inputs into outputs over time.</summary>
    public interface IProducerDefinition
    {
        string Id { get; }

        string DisplayNameKey { get; }

        /// <summary>Recipes this building can run, before unlock-level filtering.</summary>
        IReadOnlyList<IRecipeDefinition> Recipes { get; }

        /// <summary>Highest level the building can be upgraded to. Levels are 1-based.</summary>
        int MaxLevel { get; }

        /// <summary>Clamped to the authored range, so a stale save can never index out of bounds.</summary>
        IProducerLevel GetLevel(int level);
    }
}
