using System;
using System.Collections.Generic;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using UnityEngine;

namespace AlphaTown.Data.Buildings
{
    [Serializable]
    public sealed class BuildingLevel : IBuildingLevel
    {
        [SerializeField, Min(0)]
        [Tooltip("Seconds to build or upgrade to this level. Zero completes immediately.")]
        int _constructionSeconds;

        [SerializeField] CurrencyEntry[] _currencyCost = Array.Empty<CurrencyEntry>();
        [SerializeField] ItemAmount[] _itemCost = Array.Empty<ItemAmount>();

        [SerializeField, Min(0)]
        [Tooltip("XP paid when this level finishes building. The only reason to buy a decoration.")]
        int _xpReward;

        [SerializeField, Min(0)]
        [Tooltip("Barn level this grants while it stands. 0 for anything that is not storage.")]
        int _storageLevel;

        CurrencyAmount[] _cachedCurrencyCost;
        ItemStack[] _cachedItemCost;

        public TimeSpan ConstructionTime => TimeSpan.FromSeconds(_constructionSeconds);

        public int ConstructionSeconds => _constructionSeconds;

        public IReadOnlyList<CurrencyAmount> CurrencyCost =>
            _cachedCurrencyCost ?? (_cachedCurrencyCost = BuildCurrency(_currencyCost));

        public IReadOnlyList<ItemStack> ItemCost =>
            _cachedItemCost ?? (_cachedItemCost = BuildItems(_itemCost));

        public int XpReward => _xpReward;

        public int StorageLevel => _storageLevel;

        public void InvalidateCache()
        {
            _cachedCurrencyCost = null;
            _cachedItemCost = null;
        }

        static CurrencyAmount[] BuildCurrency(CurrencyEntry[] entries)
        {
            if (entries == null || entries.Length == 0) return Array.Empty<CurrencyAmount>();

            var amounts = new List<CurrencyAmount>(entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null || !entries[i].IsValid) continue;
                amounts.Add(entries[i].ToAmount());
            }

            return amounts.ToArray();
        }

        static ItemStack[] BuildItems(ItemAmount[] amounts)
        {
            if (amounts == null || amounts.Length == 0) return Array.Empty<ItemStack>();

            var stacks = new List<ItemStack>(amounts.Length);
            for (var i = 0; i < amounts.Length; i++)
            {
                if (amounts[i] == null || !amounts[i].IsValid) continue;
                stacks.Add(amounts[i].ToStack());
            }

            return stacks.ToArray();
        }
    }
}
