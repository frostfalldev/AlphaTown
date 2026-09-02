using System.Collections.Generic;
using AlphaTown.Data.Buildings;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Expansion;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;
using AlphaTown.Data.Production;
using AlphaTown.Data.Progression;
using AlphaTown.Data.Recipes;
using AlphaTown.Data.Storage;
using AlphaTown.Data.Town;

namespace AlphaTown.Data.Catalog
{
    /// <summary>
    /// Read-only lookup from stable id to definition. The one dependency every gameplay system
    /// takes on authored content, and the one thing a test needs to fake.
    /// </summary>
    public interface IGameDatabase
    {
        bool TryGetItem(string id, out IItemDefinition item);
        bool TryGetRecipe(string id, out IRecipeDefinition recipe);
        bool TryGetProducer(string id, out IProducerDefinition producer);
        bool TryGetStorage(string id, out IStorageDefinition storage);
        bool TryGetCurrency(string id, out ICurrencyDefinition currency);
        bool TryGetOrderTemplate(string id, out IOrderTemplateDefinition template);
        bool TryGetBuilding(string id, out IBuildingDefinition building);
        bool TryGetOrderBoard(string id, out IOrderBoardDefinition board);
        bool TryGetExpansion(string id, out IExpansionDefinition expansion);

        // Enumeration, for systems that select content rather than look it up by id — order
        // generation needs to know what the player can currently produce.
        IReadOnlyList<IItemDefinition> Items { get; }
        IReadOnlyList<IRecipeDefinition> Recipes { get; }
        IReadOnlyList<ICurrencyDefinition> Currencies { get; }
        IReadOnlyList<IOrderTemplateDefinition> OrderTemplates { get; }
        IReadOnlyList<IBuildingDefinition> Buildings { get; }
        IReadOnlyList<IOrderBoardDefinition> OrderBoards { get; }
        IReadOnlyList<IExpansionDefinition> Expansions { get; }

        /// <summary>The starting barn. TODO: multiple storages (barn / silo) once inventory splits.</summary>
        IStorageDefinition DefaultStorage { get; }

        /// <summary>Coins. The currency orders pay out in.</summary>
        ICurrencyDefinition SoftCurrency { get; }

        /// <summary>Gems. Premium, and audited accordingly.</summary>
        ICurrencyDefinition HardCurrency { get; }

        IProgressionCurve ProgressionCurve { get; }

        /// <summary>Town bounds and layout config. Optional — the world falls back to defaults.</summary>
        ITownDefinition TownDefinition { get; }
    }
}
