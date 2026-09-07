using System;
using System.Collections.Generic;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Items;

namespace AlphaTown.Data.Town
{
    /// <summary>A building the town starts with, already standing.</summary>
    public readonly struct StartingBuilding
    {
        public readonly string DefinitionId;
        public readonly GridPosition Origin;

        public StartingBuilding(string definitionId, GridPosition origin)
        {
            DefinitionId = definitionId;
            Origin = origin;
        }
    }

    /// <summary>
    /// What a brand-new town looks like before the player touches anything.
    ///
    /// Optional: without one the world still starts, just empty. With one, a first session opens
    /// on a town that already has something to do — which is the difference between a loop you can
    /// feel and a blank field.
    /// </summary>
    public interface INewGameDefinition
    {
        string Id { get; }

        int StartingBarnLevel { get; }

        /// <summary>Granted free. The seed of the loop, not a reward.</summary>
        IReadOnlyList<ItemStack> StartingItems { get; }

        /// <summary>Placed free and already built — a new player should not wait for a timer.</summary>
        IReadOnlyList<StartingBuilding> StartingBuildings { get; }
    }
}
