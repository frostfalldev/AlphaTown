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
    }
}
