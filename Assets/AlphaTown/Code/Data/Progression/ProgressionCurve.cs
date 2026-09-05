using System;
using System.Collections.Generic;
using AlphaTown.Data.Definitions;
using AlphaTown.Data.Economy;
using UnityEngine;

namespace AlphaTown.Data.Progression
{
    /// <summary>
    /// One entry per town level. Element 0 describes level 1, element 1 level 2, and so on.
    /// </summary>
    [Serializable]
    public sealed class ProgressionLevel
    {
        [SerializeField, Min(1)]
        [Tooltip("XP needed to leave this level. Ignored on the last entry, which is the cap.")]
        int _xpToAdvance = 100;

        [SerializeField]
        [Tooltip("Paid out on reaching this level. The first entry never pays — the player starts there.")]
        CurrencyEntry[] _rewards = Array.Empty<CurrencyEntry>();

        public int XpToAdvance => _xpToAdvance;
        public CurrencyEntry[] Rewards => _rewards;
    }

    [CreateAssetMenu(menuName = "AlphaTown/Economy/Progression Curve", fileName = "ProgressionCurve", order = 31)]
    public sealed class ProgressionCurve : GameDefinition, IProgressionCurve
    {
        static readonly CurrencyAmount[] NoRewards = Array.Empty<CurrencyAmount>();

        [SerializeField]
        ProgressionLevel[] _levels =
        {
            new ProgressionLevel()
        };

        CurrencyAmount[][] _cachedRewards;

        public int MaxLevel => _levels != null && _levels.Length > 0 ? _levels.Length : 1;

        public int XpToAdvance(int level)
        {
            if (_levels == null || _levels.Length == 0) return 0;
            if (level >= _levels.Length) return 0; // At the cap there is nowhere to advance to.

            var index = level < 1 ? 0 : level - 1;
            return _levels[index] != null ? Mathf.Max(1, _levels[index].XpToAdvance) : 0;
        }

        public IReadOnlyList<CurrencyAmount> RewardsForReaching(int level)
        {
            if (_levels == null || _levels.Length == 0) return NoRewards;
            if (level < 2 || level > _levels.Length) return NoRewards;

            EnsureRewardCache();
            return _cachedRewards[level - 1];
        }

        void OnEnable() => _cachedRewards = null;

        void EnsureRewardCache()
        {
            if (_cachedRewards != null && _cachedRewards.Length == _levels.Length) return;

            _cachedRewards = new CurrencyAmount[_levels.Length][];
            for (var i = 0; i < _levels.Length; i++)
            {
                var rewards = _levels[i] != null ? _levels[i].Rewards : null;
                if (rewards == null || rewards.Length == 0)
                {
                    _cachedRewards[i] = NoRewards;
                    continue;
                }

                var converted = new List<CurrencyAmount>(rewards.Length);
                for (var r = 0; r < rewards.Length; r++)
                {
                    if (rewards[r] == null || !rewards[r].IsValid) continue;
                    converted.Add(rewards[r].ToAmount());
                }

                _cachedRewards[i] = converted.ToArray();
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (_levels == null || _levels.Length == 0) _levels = new[] { new ProgressionLevel() };
            _cachedRewards = null;
        }
#endif
    }
}
