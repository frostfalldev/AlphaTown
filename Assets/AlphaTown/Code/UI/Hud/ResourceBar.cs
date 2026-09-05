using System.Collections.Generic;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Items;
using AlphaTown.Gameplay.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace AlphaTown.UI.Hud
{
    /// <summary>
    /// The strip along the top: coins, gems, land deeds, barn space, and the town's level.
    ///
    /// These are the five numbers the whole loop moves. Having them all visible at once is what
    /// makes the loop legible — you can see a delivery pay out and the barn empty in the same
    /// glance, which is the thing a first playable has to prove.
    /// </summary>
    public sealed class ResourceBar
    {
        readonly IGameDatabase _database;
        readonly Dictionary<string, Label> _currencyLabels = new Dictionary<string, Label>(4);
        readonly Dictionary<string, Label> _itemLabels = new Dictionary<string, Label>(4);
        readonly List<string> _trackedItemIds = new List<string>(4);

        Label _barn;
        Label _level;
        VisualElement _xpFill;

        public ResourceBar(IGameDatabase database, IReadOnlyList<string> trackedItemIds)
        {
            _database = database;
            Root = Build(trackedItemIds);
        }

        public VisualElement Root { get; }

        VisualElement Build(IReadOnlyList<string> trackedItemIds)
        {
            var bar = UiKit.Card(10f);
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.marginBottom = 8f;

            var left = UiKit.Row(18f);
            bar.Add(left);

            AddCurrency(left, _database?.SoftCurrency?.Id);
            AddCurrency(left, _database?.HardCurrency?.Id);

            ResolveTrackedItems(trackedItemIds);
            for (var i = 0; i < _trackedItemIds.Count; i++) AddItem(left, _trackedItemIds[i]);

            var right = UiKit.Row(18f);
            bar.Add(right);

            _barn = UiKit.Text("Barn 0/0");
            right.Add(_barn);

            var levelBlock = UiKit.Column(4f);
            levelBlock.style.minWidth = 150f;
            _level = UiKit.Text("Level 1", 24, true);
            levelBlock.Add(_level);
            levelBlock.Add(UiKit.ProgressBar(out _xpFill, 8f));
            right.Add(levelBlock);

            return bar;
        }

        /// <summary>
        /// Which barn items earn a slot on the bar.
        ///
        /// Land deeds are the one item the player tracks like a currency, so they belong here
        /// rather than buried in the barn list. Falling back to the Special category means a
        /// project that has not configured the bar still shows its deeds.
        /// </summary>
        void ResolveTrackedItems(IReadOnlyList<string> configured)
        {
            _trackedItemIds.Clear();

            if (configured != null && configured.Count > 0)
            {
                for (var i = 0; i < configured.Count; i++)
                {
                    if (!string.IsNullOrEmpty(configured[i])) _trackedItemIds.Add(configured[i]);
                }

                if (_trackedItemIds.Count > 0) return;
            }

            var items = _database?.Items;
            if (items == null) return;

            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].Category == ItemCategory.Special)
                    _trackedItemIds.Add(items[i].Id);
            }
        }

        void AddCurrency(VisualElement parent, string currencyId)
        {
            if (string.IsNullOrEmpty(currencyId) || _currencyLabels.ContainsKey(currencyId)) return;

            var label = UiKit.Text(DisplayNames.ForCurrency(_database, currencyId) + " 0");
            _currencyLabels.Add(currencyId, label);
            parent.Add(label);
        }

        void AddItem(VisualElement parent, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || _itemLabels.ContainsKey(itemId)) return;

            var label = UiKit.Text(DisplayNames.ForItem(_database, itemId) + " 0");
            _itemLabels.Add(itemId, label);
            parent.Add(label);
        }

        public void Refresh(GameWorld world)
        {
            if (world == null) return;

            foreach (var pair in _currencyLabels)
            {
                pair.Value.text = DisplayNames.ForCurrency(_database, pair.Key) + " " +
                                  world.Wallet.BalanceOf(pair.Key);
            }

            foreach (var pair in _itemLabels)
            {
                pair.Value.text = DisplayNames.ForItem(_database, pair.Key) + " " +
                                  world.Barn.CountOf(pair.Key);
            }

            _barn.text = "Barn " + world.Barn.UsedSpace + "/" + world.Barn.Capacity;

            // A full barn is the most common reason an action silently does nothing, so it says so
            // before the player finds out by tapping.
            _barn.style.color = world.Barn.FreeSpace <= 0 ? UiKit.Warn : UiKit.Ink;

            var progression = world.Progression;
            _level.text = "Level " + progression.TownLevel;

            var needed = progression.XpToNextLevel;
            var into = progression.XpIntoLevel;
            UiKit.SetProgress(_xpFill,
                progression.IsMaxLevel || needed + into <= 0L ? 1f : (float)((double)into / (into + needed)));
        }
    }
}
