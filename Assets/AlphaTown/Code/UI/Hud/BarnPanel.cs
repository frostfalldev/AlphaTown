using AlphaTown.Data.Catalog;
using AlphaTown.Data.Presentation;
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
    /// </summary>
    public sealed class BarnPanel
    {
        readonly IGameDatabase _database;
        readonly Label _capacity;
        readonly VisualElement _capacityFill;
        readonly VisualElement _list;

        public BarnPanel(IGameDatabase database)
        {
            _database = database;

            var card = UiKit.Card();
            card.style.minWidth = 460f;
            card.style.maxHeight = 640f;

            var header = UiKit.Column(8f);
            header.Add(UiKit.Text("Barn", 30, true));
            _capacity = UiKit.Caption("0 / 0");
            header.Add(_capacity);
            header.Add(UiKit.ProgressBar(out _capacityFill));

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

            // Rebuilt wholesale: the barn holds a handful of item types in the slice, and a diff
            // would be more code than the redraw costs.
            _list.Clear();

            foreach (var pair in barn.Contents)
            {
                if (pair.Value <= 0) continue;
                _list.Add(BuildRow(pair.Key, pair.Value));
            }

            if (_list.childCount == 0) _list.Add(UiKit.Caption("Empty. Harvest something."));
        }

        VisualElement BuildRow(string itemId, int count)
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
            row.Add(UiKit.Text(count.ToString(), 26, true));

            return row;
        }
    }
}
