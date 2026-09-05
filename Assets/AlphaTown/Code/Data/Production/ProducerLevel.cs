using System;
using UnityEngine;

namespace AlphaTown.Data.Production
{
    [Serializable]
    public sealed class ProducerLevel : IProducerLevel
    {
        [SerializeField, Min(1)] int _queueCapacity = 3;
        [SerializeField, Min(1)] int _parallelSlots = 1;
        [SerializeField, Min(0.01f)] float _speedMultiplier = 1f;

        [SerializeField]
        [Tooltip("Re-runs the last recipe when the tray is emptied. The auto-replant upgrade.")]
        bool _autoRepeat;

        public int QueueCapacity => _queueCapacity;
        public int ParallelSlots => _parallelSlots;
        public float SpeedMultiplier => _speedMultiplier <= 0f ? 1f : _speedMultiplier;
        public bool AutoRepeat => _autoRepeat;
    }
}
