using AlphaTown.Data.Definitions;
using UnityEngine;

namespace AlphaTown.Data.Economy
{
    [CreateAssetMenu(menuName = "AlphaTown/Economy/Currency Definition", fileName = "Currency_", order = 30)]
    public sealed class CurrencyDefinition : GameDefinition, ICurrencyDefinition
    {
        [SerializeField] string _displayNameKey;
        [SerializeField] CurrencyKind _kind = CurrencyKind.Soft;
        [SerializeField, Min(0)] int _startingAmount;

        [SerializeField, Min(0)]
        [Tooltip("Hard ceiling on the balance. Zero means uncapped.")]
        int _maxAmount;

        [Header("Presentation")]
        [SerializeField] Sprite _icon;

        public string DisplayNameKey => _displayNameKey;
        public CurrencyKind Kind => _kind;
        public int StartingAmount => _startingAmount;
        public int MaxAmount => _maxAmount;
        public Sprite Icon => _icon;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (string.IsNullOrWhiteSpace(_displayNameKey)) _displayNameKey = "currency." + Id;
        }
#endif
    }
}
