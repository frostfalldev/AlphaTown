using System;
using System.Collections.Generic;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Definitions;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using UnityEngine;

namespace AlphaTown.Data.Expansion
{
    [CreateAssetMenu(menuName = "AlphaTown/Town/Expansion", fileName = "Expansion_", order = 61)]
    public sealed class ExpansionDefinition : GameDefinition, IExpansionDefinition
    {
        [SerializeField] string _displayNameKey;

        [Header("Region")]
        [SerializeField, Min(0)] int _x;
        [SerializeField, Min(0)] int _y;
        [SerializeField, Min(1)] int _width = 4;
        [SerializeField, Min(1)] int _height = 4;

        [Header("Requirements")]
        [SerializeField, Min(1)] int _unlockLevel = 1;

        [SerializeField]
        [Tooltip("Optional. Must be owned before this one can be bought, so land spreads outward.")]
        ExpansionDefinition _requires;

        [Header("Cost")]
        [SerializeField]
        [Tooltip("The real gate. Land deeds go here.")]
        ItemAmount[] _itemCost = Array.Empty<ItemAmount>();

        [SerializeField]
        [Tooltip("Optional secondary cost. Usually empty — coins already pay for buildings.")]
        CurrencyEntry[] _currencyCost = Array.Empty<CurrencyEntry>();

        [SerializeField, Min(0)] int _sortOrder;

        ItemStack[] _cachedItemCost;
        CurrencyAmount[] _cachedCurrencyCost;

        public string DisplayNameKey => _displayNameKey;
        public GridRect Region => new GridRect(new GridPosition(_x, _y), new GridSize(_width, _height));
        public int UnlockLevel => _unlockLevel;
        public string RequiresExpansionId => _requires != null ? _requires.Id : string.Empty;
        public int SortOrder => _sortOrder;

        public IReadOnlyList<ItemStack> ItemCost =>
            _cachedItemCost ?? (_cachedItemCost = BuildItems(_itemCost));

        public IReadOnlyList<CurrencyAmount> CurrencyCost =>
            _cachedCurrencyCost ?? (_cachedCurrencyCost = BuildCurrency(_currencyCost));

        void OnEnable() => InvalidateCache();

        void InvalidateCache()
        {
            _cachedItemCost = null;
            _cachedCurrencyCost = null;
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

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (string.IsNullOrWhiteSpace(_displayNameKey)) _displayNameKey = "expansion." + Id;

            if (_requires == this)
            {
                Core.Diagnostics.Log.Error("ExpansionDefinition",
                    "'" + name + "' requires itself and could never be bought. Clearing.");
                _requires = null;
            }

            InvalidateCache();
        }
#endif
    }
}
