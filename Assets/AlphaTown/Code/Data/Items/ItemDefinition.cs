using AlphaTown.Data.Definitions;
using AlphaTown.Data.Presentation;
using UnityEngine;

namespace AlphaTown.Data.Items
{
    [CreateAssetMenu(menuName = "AlphaTown/Items/Item Definition", fileName = "Item_", order = 0)]
    public sealed class ItemDefinition : GameDefinition, IItemDefinition, IItemVisuals
    {
        [SerializeField] string _displayNameKey;
        [SerializeField] ItemCategory _category = ItemCategory.Crop;
        [SerializeField, Min(1)] int _storageCost = 1;
        [SerializeField] bool _isStorable = true;

        [Header("Economy")]
        [Tooltip("Base worth in soft currency. Order payouts are derived from this.")]
        [SerializeField, Min(0)] int _coinValue = 1;

        [SerializeField, Min(0)] int _xpValue = 1;

        [Header("Presentation")]
        [Tooltip("Icon is presentation only. The simulation never touches it.")]
        [SerializeField] Sprite _icon;

        public string DisplayNameKey => _displayNameKey;
        public ItemCategory Category => _category;
        public int StorageCost => _storageCost;
        public bool IsStorable => _isStorable;
        public int CoinValue => _coinValue;
        public int XpValue => _xpValue;
        public Sprite Icon => _icon;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (string.IsNullOrWhiteSpace(_displayNameKey)) _displayNameKey = "item." + Id;
            if (_storageCost < 1) _storageCost = 1;
        }
#endif
    }
}
