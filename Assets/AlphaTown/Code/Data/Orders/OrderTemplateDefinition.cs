using System;
using System.Collections.Generic;
using AlphaTown.Data.Definitions;
using AlphaTown.Data.Items;
using UnityEngine;

namespace AlphaTown.Data.Orders
{
    [CreateAssetMenu(menuName = "AlphaTown/Orders/Order Template", fileName = "OrderTemplate_", order = 40)]
    public sealed class OrderTemplateDefinition : GameDefinition, IOrderTemplateDefinition
    {
        [SerializeField] OrderKind _kind = OrderKind.Helicopter;
        [SerializeField, Min(1)] int _unlockLevel = 1;

        [Header("Shape")]
        [SerializeField, Min(1)] int _minItemTypes = 1;
        [SerializeField, Min(1)] int _maxItemTypes = 3;
        [SerializeField, Min(1)] int _minQuantityPerItem = 1;
        [SerializeField, Min(1)] int _maxQuantityPerItem = 5;

        [SerializeField, Min(0)]
        [Tooltip("Coin value wanted of each good, sizing cheap goods in bulk and dear ones in " +
                 "handfuls. 0 keeps the flat range above.")]
        int _valuePerItemType;

        [SerializeField, Min(0)]
        [Tooltip("Seconds before the order expires. Zero means it never does.")]
        int _timeLimitSeconds;

        [Header("Payout")]
        [SerializeField, Min(0f)] float _coinMultiplier = 1f;
        [SerializeField, Min(0f)] float _xpMultiplier = 1f;

        [SerializeField, Min(0)]
        [Tooltip("Flat hard-currency bonus. Keep this at zero unless the design calls for a faucet.")]
        int _bonusHardCurrency;

        [SerializeField]
        [Tooltip("Bonus items on completion. Land deeds go here.")]
        ItemAmount[] _bonusItems = Array.Empty<ItemAmount>();

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance the bonus drops. Rolled when the order is generated, not when completed.")]
        float _bonusItemChance = 1f;

        public OrderKind Kind => _kind;
        public int UnlockLevel => _unlockLevel;
        public int MinItemTypes => _minItemTypes;
        public int MaxItemTypes => Mathf.Max(_minItemTypes, _maxItemTypes);
        public int MinQuantityPerItem => _minQuantityPerItem;
        public int MaxQuantityPerItem => Mathf.Max(_minQuantityPerItem, _maxQuantityPerItem);
        public int ValuePerItemType => _valuePerItemType;
        public TimeSpan TimeLimit => TimeSpan.FromSeconds(_timeLimitSeconds);
        public float CoinMultiplier => _coinMultiplier;
        public float XpMultiplier => _xpMultiplier;
        public int BonusHardCurrency => _bonusHardCurrency;
        public float BonusItemChance => _bonusItemChance;

        public IReadOnlyList<ItemStack> BonusItems =>
            _cachedBonusItems ?? (_cachedBonusItems = BuildBonusItems());

        ItemStack[] _cachedBonusItems;

        ItemStack[] BuildBonusItems()
        {
            if (_bonusItems == null || _bonusItems.Length == 0) return Array.Empty<ItemStack>();

            var stacks = new List<ItemStack>(_bonusItems.Length);
            for (var i = 0; i < _bonusItems.Length; i++)
            {
                if (_bonusItems[i] == null || !_bonusItems[i].IsValid) continue;
                stacks.Add(_bonusItems[i].ToStack());
            }

            return stacks.ToArray();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (_maxItemTypes < _minItemTypes) _maxItemTypes = _minItemTypes;
            if (_maxQuantityPerItem < _minQuantityPerItem) _maxQuantityPerItem = _minQuantityPerItem;
            _cachedBonusItems = null;
        }
#endif
    }
}
