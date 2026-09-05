using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Progression;
using AlphaTown.Data.Recipes;
using AlphaTown.Gameplay.Economy;

namespace AlphaTown.Gameplay.Progression
{
    /// <summary>
    /// Town level, XP and the unlock gate.
    ///
    /// The curve is authored data, never a formula here — pacing is the biggest retention lever
    /// in this genre and gets retuned constantly.
    /// </summary>
    public sealed class TownProgression : ITownProgression
    {
        readonly IProgressionCurve _curve;
        readonly IWallet _wallet;
        readonly IEventBus _events;
        readonly Dictionary<int, long> _xpBySource = new Dictionary<int, long>(8);

        public TownProgression(IProgressionCurve curve, IWallet wallet, IEventBus events)
        {
            _curve = Guard.NotNull(curve, nameof(curve));
            _wallet = Guard.NotNull(wallet, nameof(wallet));
            _events = Guard.NotNull(events, nameof(events));
            TownLevel = 1;
        }

        public int TownLevel { get; private set; }

        public long XpIntoLevel { get; private set; }

        public long TotalXp { get; private set; }

        public bool IsMaxLevel => TownLevel >= _curve.MaxLevel;

        public int XpToNextLevel
        {
            get
            {
                var needed = _curve.XpToAdvance(TownLevel);
                if (needed <= 0) return 0;

                var remaining = needed - XpIntoLevel;
                return remaining > 0 ? (int)remaining : 0;
            }
        }

        public bool IsUnlocked(int requiredLevel) => TownLevel >= requiredLevel;

        public bool IsRecipeUnlocked(IRecipeDefinition recipe) => recipe != null && IsUnlocked(recipe.UnlockLevel);

        public int GrantXp(int amount, XpSource source, string context = null)
        {
            if (amount <= 0) return 0;

            if (source == XpSource.Unknown)
            {
                Log.Warn("Progression",
                    "Untagged XP grant of " + amount + ". Every source needs a reason code.");
            }

            TotalXp += amount;
            XpIntoLevel += amount;

            _xpBySource.TryGetValue((int)source, out var running);
            _xpBySource[(int)source] = running + amount;

            _events.Publish(new XpGrantedEvent(amount, source, TotalXp));

            return AdvanceLevels(context);
        }

        public long TotalXpFrom(XpSource source) =>
            _xpBySource.TryGetValue((int)source, out var total) ? total : 0L;

        /// <summary>Restores from save. Does not replay level-up rewards.</summary>
        public void RestoreState(int level, long xpIntoLevel, long totalXp,
                                 IReadOnlyList<XpAttributionEntry> attribution)
        {
            TownLevel = level < 1 ? 1 : (level > _curve.MaxLevel ? _curve.MaxLevel : level);
            XpIntoLevel = xpIntoLevel < 0 ? 0 : xpIntoLevel;
            TotalXp = totalXp < 0 ? 0 : totalXp;

            _xpBySource.Clear();
            if (attribution == null) return;

            for (var i = 0; i < attribution.Count; i++)
            {
                _xpBySource[attribution[i].Source] = attribution[i].Total;
            }
        }

        public List<XpAttributionEntry> SnapshotAttribution()
        {
            var snapshot = new List<XpAttributionEntry>(_xpBySource.Count);
            foreach (var pair in _xpBySource)
            {
                snapshot.Add(new XpAttributionEntry(pair.Key, pair.Value));
            }

            return snapshot;
        }

        /// <summary>
        /// Consumes banked XP one level at a time. A single grant can cover several levels, which
        /// is normal after a long absence or a large order.
        /// </summary>
        int AdvanceLevels(string context)
        {
            var gained = 0;

            while (!IsMaxLevel)
            {
                var needed = _curve.XpToAdvance(TownLevel);
                if (needed <= 0) break;
                if (XpIntoLevel < needed) break;

                XpIntoLevel -= needed;
                var previousLevel = TownLevel;
                TownLevel++;
                gained++;

                GrantLevelRewards(TownLevel, context);
                _events.Publish(new TownLevelUpEvent(TownLevel, previousLevel, IsMaxLevel));
            }

            // At the cap, leftover XP is deliberately kept rather than discarded: raising the cap
            // in a later update should credit it immediately, not throw away what was earned.
            return gained;
        }

        void GrantLevelRewards(int level, string context)
        {
            var rewards = _curve.RewardsForReaching(level);
            if (rewards == null || rewards.Count == 0) return;

            _wallet.GrantAll(rewards, CurrencySource.LevelUpReward, context ?? "level_up");
        }
    }
}
