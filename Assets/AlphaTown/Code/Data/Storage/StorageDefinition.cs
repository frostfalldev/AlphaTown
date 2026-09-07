using AlphaTown.Data.Definitions;
using UnityEngine;

namespace AlphaTown.Data.Storage
{
    [CreateAssetMenu(menuName = "AlphaTown/Economy/Storage Definition", fileName = "Storage_", order = 20)]
    public sealed class StorageDefinition : GameDefinition, IStorageDefinition
    {
        [SerializeField]
        [Tooltip("Element 0 is level 1. Township-style barns start small and roughly double.")]
        int[] _capacityPerLevel = { 50, 75, 100, 150, 200 };

        public int MaxLevel => _capacityPerLevel != null && _capacityPerLevel.Length > 0
            ? _capacityPerLevel.Length
            : 1;

        public int GetCapacity(int level)
        {
            if (_capacityPerLevel == null || _capacityPerLevel.Length == 0) return 0;

            var index = Mathf.Clamp(level, 1, _capacityPerLevel.Length) - 1;
            return Mathf.Max(0, _capacityPerLevel[index]);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (_capacityPerLevel == null || _capacityPerLevel.Length == 0)
                _capacityPerLevel = new[] { 50 };
        }
#endif
    }
}
