using System;
using System.Collections.Generic;
using AlphaTown.Data.Items;

namespace AlphaTown.Data.Recipes
{
    /// <summary>One step of a production chain: inputs in, wall-clock wait, outputs out.</summary>
    public interface IRecipeDefinition
    {
        string Id { get; }

        /// <summary>Consumed when the order is queued, not when it starts.</summary>
        IReadOnlyList<ItemStack> Inputs { get; }

        /// <summary>Produced on completion, held in the building until collected.</summary>
        IReadOnlyList<ItemStack> Outputs { get; }

        /// <summary>Authored duration at producer level 1, before any speed multiplier.</summary>
        TimeSpan Duration { get; }

        /// <summary>Town level the player must reach before this recipe is offered.</summary>
        int UnlockLevel { get; }

        /// <summary>
        /// Extra units of the first output that a run may yield, on top of the authored count.
        /// Zero — the default — means the recipe always produces exactly what it says.
        ///
        /// The roll is a hash of the completed order, not a random draw, so a harvest that
        /// finished while the game was closed yields the same amount it would have while the
        /// player watched. See <c>AlphaTown.Core.Randomness.DeterministicRoll</c>.
        /// </summary>
        int BonusOutputMax { get; }
    }
}
