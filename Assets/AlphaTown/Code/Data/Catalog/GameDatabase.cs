using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Data.Buildings;
using AlphaTown.Data.Definitions;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Expansion;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;
using AlphaTown.Data.Production;
using AlphaTown.Data.Progression;
using AlphaTown.Data.Recipes;
using AlphaTown.Data.Storage;
using AlphaTown.Data.Town;
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
        [Header("Content")]
        [SerializeField] ItemDefinition[] _items;
        [SerializeField] RecipeDefinition[] _recipes;
        [SerializeField] ProducerDefinition[] _producers;
        [SerializeField] StorageDefinition[] _storages;
        [SerializeField] CurrencyDefinition[] _currencies;
        [SerializeField] OrderTemplateDefinition[] _orderTemplates;
        [SerializeField] BuildingDefinition[] _buildings;
        [SerializeField] OrderBoardDefinition[] _orderBoards;
        [SerializeField] ExpansionDefinition[] _expansions;

        [Header("Well-known entries")]
        [SerializeField]
        [Tooltip("Barn the player starts with. Must also appear in the storages list.")]
        StorageDefinition _defaultStorage;

        [SerializeField]
        [Tooltip("Coins. Orders pay out in this. Must also appear in the currencies list.")]
        CurrencyDefinition _softCurrency;

        [SerializeField]
        [Tooltip("Gems. Must also appear in the currencies list.")]
        CurrencyDefinition _hardCurrency;

        [SerializeField] ProgressionCurve _progressionCurve;

        [SerializeField]
        [Tooltip("Optional. Town bounds; the world falls back to defaults when this is empty.")]
        TownDefinition _townDefinition;

        Dictionary<string, IItemDefinition> _itemsById;
        Dictionary<string, IRecipeDefinition> _recipesById;
        Dictionary<string, IProducerDefinition> _producersById;
        Dictionary<string, IStorageDefinition> _storagesById;
        Dictionary<string, ICurrencyDefinition> _currenciesById;
        Dictionary<string, IOrderTemplateDefinition> _orderTemplatesById;
        Dictionary<string, IBuildingDefinition> _buildingsById;
        Dictionary<string, IOrderBoardDefinition> _orderBoardsById;
        Dictionary<string, IExpansionDefinition> _expansionsById;

        IItemDefinition[] _itemList;
        IRecipeDefinition[] _recipeList;
        ICurrencyDefinition[] _currencyList;
        IOrderTemplateDefinition[] _orderTemplateList;
        IBuildingDefinition[] _buildingList;
        IOrderBoardDefinition[] _orderBoardList;
        IExpansionDefinition[] _expansionList;

        public IStorageDefinition DefaultStorage => _defaultStorage;
        public ICurrencyDefinition SoftCurrency => _softCurrency;
        public ICurrencyDefinition HardCurrency => _hardCurrency;
        public IProgressionCurve ProgressionCurve => _progressionCurve;
        public ITownDefinition TownDefinition => _townDefinition;
        public INewGameDefinition NewGame => _newGame;

        public IReadOnlyList<IItemDefinition> Items
        {
            get
            {
                EnsureIndexed();
                return _itemList;
            }
        }

        public IReadOnlyList<IRecipeDefinition> Recipes
        {
            get
            {
                EnsureIndexed();
                return _recipeList;
            }
        }

        public IReadOnlyList<ICurrencyDefinition> Currencies
        {
            get
            {
                EnsureIndexed();
                return _currencyList;
            }
        }

        public IReadOnlyList<IOrderTemplateDefinition> OrderTemplates
        {
            get
            {
                EnsureIndexed();
                return _orderTemplateList;
            }
        }

        public IReadOnlyList<IBuildingDefinition> Buildings
        {
            get
            {
                EnsureIndexed();
                return _buildingList;
            }
        }

        public IReadOnlyList<IOrderBoardDefinition> OrderBoards
        {
            get
            {
                EnsureIndexed();
                return _orderBoardList;
            }
        }

        public IReadOnlyList<IExpansionDefinition> Expansions
        {
            get
            {
                EnsureIndexed();
                return _expansionList;
            }
        }

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

        public bool TryGetCurrency(string id, out ICurrencyDefinition currency)
        {
            EnsureIndexed();
            return _currenciesById.TryGetValue(id ?? string.Empty, out currency);
        }

        public bool TryGetOrderTemplate(string id, out IOrderTemplateDefinition template)
        {
            EnsureIndexed();
            return _orderTemplatesById.TryGetValue(id ?? string.Empty, out template);
        }

        public bool TryGetBuilding(string id, out IBuildingDefinition building)
        {
            EnsureIndexed();
            return _buildingsById.TryGetValue(id ?? string.Empty, out building);
        }

        public bool TryGetOrderBoard(string id, out IOrderBoardDefinition board)
        {
            EnsureIndexed();
            return _orderBoardsById.TryGetValue(id ?? string.Empty, out board);
        }

        public bool TryGetExpansion(string id, out IExpansionDefinition expansion)
        {
            EnsureIndexed();
            return _expansionsById.TryGetValue(id ?? string.Empty, out expansion);
        }

        /// <summary>Rebuilds the indexes. Call after hot-reloading content in the editor.</summary>
        public void Reindex()
        {
            _itemsById = Index<ItemDefinition, IItemDefinition>(_items, "item");
            _recipesById = Index<RecipeDefinition, IRecipeDefinition>(_recipes, "recipe");
            _producersById = Index<ProducerDefinition, IProducerDefinition>(_producers, "producer");
            _storagesById = Index<StorageDefinition, IStorageDefinition>(_storages, "storage");
            _currenciesById = Index<CurrencyDefinition, ICurrencyDefinition>(_currencies, "currency");
            _orderTemplatesById =
                Index<OrderTemplateDefinition, IOrderTemplateDefinition>(_orderTemplates, "order template");
            _buildingsById = Index<BuildingDefinition, IBuildingDefinition>(_buildings, "building");
            _orderBoardsById = Index<OrderBoardDefinition, IOrderBoardDefinition>(_orderBoards, "order board");
            _expansionsById = Index<ExpansionDefinition, IExpansionDefinition>(_expansions, "expansion");

            _itemList = ToArray(_itemsById);
            _recipeList = ToArray(_recipesById);
            _currencyList = ToArray(_currenciesById);
            _orderTemplateList = ToArray(_orderTemplatesById);
            _buildingList = ToArray(_buildingsById);
            _orderBoardList = ToArray(_orderBoardsById);
            _expansionList = ToArray(_expansionsById);
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

        static TInterface[] ToArray<TInterface>(Dictionary<string, TInterface> map)
        {
            var result = new TInterface[map.Count];
            map.Values.CopyTo(result, 0);
            return result;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _itemsById = null;

            WarnIfUnlisted(_defaultStorage, _storages, "Default storage");
            WarnIfUnlisted(_softCurrency, _currencies, "Soft currency");
            WarnIfUnlisted(_hardCurrency, _currencies, "Hard currency");

            if (_progressionCurve == null)
                Log.Warn("GameDatabase", "No progression curve assigned — town level cannot advance.");
        }

        /// <summary>A well-known entry must be reachable by id too, or a save cannot restore it.</summary>
        static void WarnIfUnlisted<TAsset>(TAsset entry, TAsset[] list, string label) where TAsset : Object
        {
            if (entry == null) return;
            if (list != null && System.Array.IndexOf(list, entry) >= 0) return;

            Log.Warn("GameDatabase", label + " '" + entry.name + "' is not in its content list.");
        }
#endif
    }
}
