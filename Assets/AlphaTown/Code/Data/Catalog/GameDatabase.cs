using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Data.Definitions;
using AlphaTown.Data.Items;
using AlphaTown.Data.Production;
using AlphaTown.Data.Recipes;
using AlphaTown.Data.Storage;
using UnityEngine;

namespace AlphaTown.Data.Catalog
{
    /// <summary>
    /// The authored content registry, indexed by id on first use.
    ///
    /// TODO(live-ops): definitions are compiled into the build today. When remote content lands,
    /// keep this interface and back it with an Addressables catalog plus a remote-config overlay
    /// so tuning changes ship without a store release.
    /// </summary>
    [CreateAssetMenu(menuName = "AlphaTown/Game Database", fileName = "GameDatabase", order = 100)]
    public sealed class GameDatabase : ScriptableObject, IGameDatabase
    {
        [SerializeField] ItemDefinition[] _items;
        [SerializeField] RecipeDefinition[] _recipes;
        [SerializeField] ProducerDefinition[] _producers;
        [SerializeField] StorageDefinition[] _storages;

        [SerializeField]
        [Tooltip("Barn the player starts with. Must also appear in the storages list.")]
        StorageDefinition _defaultStorage;

        Dictionary<string, IItemDefinition> _itemsById;
        Dictionary<string, IRecipeDefinition> _recipesById;
        Dictionary<string, IProducerDefinition> _producersById;
        Dictionary<string, IStorageDefinition> _storagesById;

        public IStorageDefinition DefaultStorage => _defaultStorage;

        public bool TryGetItem(string id, out IItemDefinition item)
        {
            EnsureIndexed();
            return _itemsById.TryGetValue(id ?? string.Empty, out item);
        }

        public bool TryGetRecipe(string id, out IRecipeDefinition recipe)
        {
            EnsureIndexed();
            return _recipesById.TryGetValue(id ?? string.Empty, out recipe);
        }

        public bool TryGetProducer(string id, out IProducerDefinition producer)
        {
            EnsureIndexed();
            return _producersById.TryGetValue(id ?? string.Empty, out producer);
        }

        public bool TryGetStorage(string id, out IStorageDefinition storage)
        {
            EnsureIndexed();
            return _storagesById.TryGetValue(id ?? string.Empty, out storage);
        }

        /// <summary>Rebuilds the indexes. Call after hot-reloading content in the editor.</summary>
        public void Reindex()
        {
            _itemsById = Index<ItemDefinition, IItemDefinition>(_items, "item");
            _recipesById = Index<RecipeDefinition, IRecipeDefinition>(_recipes, "recipe");
            _producersById = Index<ProducerDefinition, IProducerDefinition>(_producers, "producer");
            _storagesById = Index<StorageDefinition, IStorageDefinition>(_storages, "storage");
        }

        void OnEnable() => _itemsById = null;

        void EnsureIndexed()
        {
            if (_itemsById == null) Reindex();
        }

        static Dictionary<string, TInterface> Index<TAsset, TInterface>(TAsset[] assets, string label)
            where TAsset : GameDefinition, TInterface
        {
            var map = new Dictionary<string, TInterface>(assets != null ? assets.Length : 0);
            if (assets == null) return map;

            for (var i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                if (asset == null) continue;

                if (!asset.HasValidId)
                {
                    Log.Error("GameDatabase", "The " + label + " '" + asset.name + "' has no id and was skipped.");
                    continue;
                }

                if (map.ContainsKey(asset.Id))
                {
                    Log.Error("GameDatabase",
                        "Duplicate " + label + " id '" + asset.Id + "' on '" + asset.name + "'. Ids must be unique.");
                    continue;
                }

                map.Add(asset.Id, asset);
            }

            return map;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _itemsById = null;
            if (_defaultStorage == null) return;

            // The default barn has to be reachable by id too, or a save cannot restore it.
            if (_storages == null || System.Array.IndexOf(_storages, _defaultStorage) < 0)
                Log.Warn("GameDatabase", "Default storage '" + _defaultStorage.name + "' is not in the storages list.");
        }
#endif
    }
}
