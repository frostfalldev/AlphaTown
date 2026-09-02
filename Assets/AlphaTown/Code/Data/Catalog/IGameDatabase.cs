using AlphaTown.Data.Items;
using AlphaTown.Data.Production;
using AlphaTown.Data.Recipes;
using AlphaTown.Data.Storage;

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

        /// <summary>The starting barn. TODO: multiple storages (barn / silo) once inventory splits.</summary>
        IStorageDefinition DefaultStorage { get; }
    }
}
