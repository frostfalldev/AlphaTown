using AlphaTown.Data.Definitions;
using UnityEngine;

namespace AlphaTown.Data.Orders
{
    [CreateAssetMenu(menuName = "AlphaTown/Orders/Order Board", fileName = "OrderBoard_", order = 41)]
    public sealed class OrderBoardDefinition : GameDefinition, IOrderBoardDefinition
    {
        [SerializeField] OrderKind _kind = OrderKind.Helicopter;

        [SerializeField]
        [Tooltip("One entry per slot: seconds that slot stays empty after its order clears. " +
                 "The array length is the slot count. Zero refills immediately.")]
        int[] _slotCooldownSeconds = { 300, 300, 300, 300 };

        [Header("Rerolling")]
        [SerializeField, Min(0)]
        [Tooltip("Least a reroll can cost. Zero makes the board a free slot machine.")]
        int _rerollBaseCost = 25;

        [SerializeField, Min(0)]
        [Tooltip("Reroll price as a percentage of the order's coin reward, floored by the base.")]
        int _rerollCostPercent = 40;

        public OrderKind Kind => _kind;
        public int RerollBaseCost => _rerollBaseCost;
        public int RerollCostPercent => _rerollCostPercent;

        public int SlotCount =>
            _slotCooldownSeconds != null && _slotCooldownSeconds.Length > 0 ? _slotCooldownSeconds.Length : 1;

        public System.TimeSpan CooldownForSlot(int slotIndex)
        {
            if (_slotCooldownSeconds == null || _slotCooldownSeconds.Length == 0)
                return System.TimeSpan.Zero;

            var index = Mathf.Clamp(slotIndex, 0, _slotCooldownSeconds.Length - 1);
            var seconds = _slotCooldownSeconds[index];
            return seconds > 0 ? System.TimeSpan.FromSeconds(seconds) : System.TimeSpan.Zero;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (_slotCooldownSeconds == null || _slotCooldownSeconds.Length == 0)
                _slotCooldownSeconds = new[] { 300 };

            for (var i = 0; i < _slotCooldownSeconds.Length; i++)
            {
                if (_slotCooldownSeconds[i] < 0) _slotCooldownSeconds[i] = 0;
            }
        }
#endif
    }
}
