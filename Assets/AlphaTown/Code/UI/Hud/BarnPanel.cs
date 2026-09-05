using System;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Presentation;
using AlphaTown.Gameplay.Commands;
using AlphaTown.Gameplay.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace AlphaTown.UI.Hud
{
    /// <summary>
    /// What is in the barn, and how much room is left.
    ///
    /// The barn is the loop's bottleneck by design — a full barn stops harvesting, which is what
    /// pushes the player towards delivering orders and upgrading storage. So this screen leads
    /// with the capacity bar rather than the list: the number that matters is how close to full
    /// it is, not the alphabetical inventory beneath it.
    ///
    /// It is also the market, because there is no moment when a player wants to sell that is not
    /// the moment they are staring at a full barn. A separate shop screen would put the cure one
    /// navigation step away from the symptom.
    /// </summary>
    public sealed class BarnPanel
    {
        readonly TownCommands _commands;
        readonly IGameDatabase _database;
        readonly Action<CommandResult> _report;
        readonly Label _capacity;
        readonly Label _rate;
        readonly VisualElement _capacityFill;
        readonly VisualElement _list;

        public BarnPanel(TownCommands commands, IGameDatabase database, Action<CommandResult> report)
        {
            _commands = commands;
            _database = database;
            _report = report;

            var card = UiKit.Card();
            card.style.minWidth = 460f;
            card.style.maxHeight = 640f;

            var header = UiKit.Column(8f);
            header.Add(UiKit.Text("Barn", 30, true));
            _capacity = UiKit.Caption("0 / 0");
            header.Add(_capacity);
            header.Add(UiKit.ProgressBar(out _capacityFill));

            _rate = UiKit.Caption("");
            header.Add(_rate);

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.style.flexGrow = 1f;
            _list.style.marginTop = 10f;

            card.Add(header);
            card.Add(_list);
            Root = card;
        }

        public VisualElement Root { get; }

        public void Refresh(GameWorld world)
        {
            if (world == null) return;

            var barn = world.Barn;
            _capacity.text = barn.UsedSpace + " / " + barn.Capacity + " used";

            var fraction = barn.Capacity <= 0 ? 1f : (float)barn.UsedSpace / barn.Capacity;
            UiKit.SetProgress(_capacityFill, fraction);
            _capacityFill.style.backgroundColor = fraction >= 1f ? UiKit.Warn : UiKit.Accent;

            // Said out loud, because a player who does not know selling is a bad deal will do it
            // instead of delivering and wonder why they are poor.
            _rate.text = fraction >= 1f
                ? "Full. Sell something, or deliver an order for far more."
                : "Selling pays a fraction of what an order does. It is for surplus.";

            // Rebuilt wholesale: the barn holds a handful of item types in the slice, and a diff
            // would be more code than the redraw costs.
            _list.Clear();

            foreach (var pair in barn.Contents)
            {
                if (pair.Value <= 0) continue;
                _list.Add(BuildRow(world, pair.Key, pair.Value));
            }

            if (_list.childCount == 0) _list.Add(UiKit.Caption("Empty. Harvest something."));
        }

        VisualElement BuildRow(GameWorld world, string itemId, int count)
        {
            var row = UiKit.Row(10f);
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingTop = 6f;
            row.style.paddingBottom = 6f;

            var left = UiKit.Row(10f);

            if (_database != null && _database.TryGetItem(itemId, out var item) && item is IItemVisuals visuals &&
                visuals.Icon != null)
            {
                var icon = new Image { sprite = visuals.Icon };
                icon.style.width = 40f;
                icon.style.height = 40f;
                left.Add(icon);
            }

            left.Add(UiKit.Text(DisplayNames.ForItem(_database, itemId)));
            row.Add(left);

            var right = UiKit.Row(10f);
            right.Add(UiKit.Text(count.ToString(), 26, true));
            AddSellControls(right, world, itemId, count);
            row.Add(right);

            return row;
        }

        /// <summary>
        /// Sell one, or sell the lot. Two buttons rather than one, because a single tap that
        /// dumps an entire stack is a mistake waiting to happen and the whole stack is still the
        /// thing you usually want when the barn is full.
        /// </summary>
        void AddSellControls(VisualElement row, GameWorld world, string itemId, int count)
        {
            var unit = world.Market.UnitPrice(itemId);
            if (unit <= 0)
            {
                row.Add(UiKit.Caption("not for sale"));
                return;
            }

            var one = UiKit.Action("Sell 1  " + unit, () => _report?.Invoke(_commands.Sell(itemId, 1)));
            one.style.minWidth = UiKit.TouchTarget * 1.2f;
            row.Add(one);

            if (count <= 1) return;

            var all = UiKit.Action("All  " + unit * count, () => _report?.Invoke(_commands.SellAll(itemId)));
            all.style.minWidth = UiKit.TouchTarget * 1.2f;
            row.Add(all);
        }
    }
}
