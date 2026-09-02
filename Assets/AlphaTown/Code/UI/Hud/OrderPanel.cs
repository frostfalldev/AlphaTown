using System;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Catalog;
using AlphaTown.Gameplay.Commands;
using AlphaTown.Gameplay.Orders;
using AlphaTown.Gameplay.World;
using UnityEngine.UIElements;

namespace AlphaTown.UI.Hud
{
    /// <summary>
    /// The order board: what the helicopter wants, what it pays, and whether the barn can cover it.
    ///
    /// This is the loop's closing move — the sink that takes the goods away and the source that
    /// pays the coins and XP back. Every request line is coloured by whether the player has it, so
    /// the decision "what do I grow next" is answerable from this one screen.
    /// </summary>
    public sealed class OrderPanel
    {
        readonly TownCommands _commands;
        readonly IGameDatabase _database;
        readonly IGameClock _clock;
        readonly Action<CommandResult> _report;
        readonly VisualElement _list;

        public OrderPanel(TownCommands commands, IGameDatabase database, IGameClock clock,
                          Action<CommandResult> report)
        {
            _commands = commands;
            _database = database;
            _clock = clock;
            _report = report;

            var card = UiKit.Card();
            card.style.minWidth = 520f;
            card.style.maxHeight = 640f;

            card.Add(UiKit.Text("Orders", 30, true));

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.style.flexGrow = 1f;
            _list.style.marginTop = 10f;
            card.Add(_list);

            Root = card;
        }

        public VisualElement Root { get; }

        public void Refresh(GameWorld world)
        {
            if (world == null) return;

            _list.Clear();

            var board = world.HelicopterOrders;
            var orders = board.Orders;

            for (var i = 0; i < orders.Count; i++) _list.Add(BuildCard(world, board, orders[i]));

            if (_list.childCount == 0) _list.Add(UiKit.Caption("No orders right now. Check back shortly."));
        }

        VisualElement BuildCard(GameWorld world, OrderBoard board, Order order)
        {
            var card = UiKit.Column(8f);
            card.style.marginBottom = 12f;
            card.style.paddingTop = 10f;
            card.style.paddingBottom = 10f;
            card.style.paddingLeft = 12f;
            card.style.paddingRight = 12f;
            card.style.backgroundColor = new UnityEngine.Color(1f, 1f, 1f, 0.05f);
            UiKit.Round(card, 10f);

            var header = UiKit.Row();
            header.style.justifyContent = Justify.SpaceBetween;
            header.Add(UiKit.Text(DisplayNames.Pretty(order.TemplateId), 24, true));

            if (order.HasTimeLimit)
            {
                var remaining = order.RemainingTicks(_clock.UtcNowTicks);
                var timer = UiKit.Caption(DisplayNames.DurationFromTicks(remaining) + " left");
                if (remaining <= TimeSpan.TicksPerMinute) timer.style.color = UiKit.Warn;
                header.Add(timer);
            }

            card.Add(header);

            var requests = UiKit.Row(14f);
            for (var i = 0; i < order.Requests.Count; i++)
            {
                var request = order.Requests[i];
                var held = world.Barn.CountOf(request.ItemId);
                var line = UiKit.Text(
                    held + "/" + request.Count + " " + DisplayNames.ForItem(_database, request.ItemId), 22);

                line.style.color = held >= request.Count ? UiKit.Accent : UiKit.Muted;
                requests.Add(line);
            }

            card.Add(requests);
            card.Add(UiKit.Caption("Pays " + DescribeRewards(order)));

            var deliver = UiKit.Action("Deliver", () => _report?.Invoke(_commands.Deliver(order.OrderId)));
            UiKit.SetEnabled(deliver, board.CanComplete(order.OrderId));
            card.Add(deliver);

            return card;
        }

        string DescribeRewards(Order order)
        {
            var text = string.Empty;

            for (var i = 0; i < order.CurrencyRewards.Count; i++)
            {
                if (text.Length > 0) text += ", ";
                text += order.CurrencyRewards[i].Amount + " " +
                        DisplayNames.ForCurrency(_database, order.CurrencyRewards[i].CurrencyId);
            }

            for (var i = 0; i < order.ItemRewards.Count; i++)
            {
                if (text.Length > 0) text += ", ";
                text += order.ItemRewards[i].Count + " " +
                        DisplayNames.ForItem(_database, order.ItemRewards[i].ItemId);
            }

            if (order.XpReward > 0)
            {
                if (text.Length > 0) text += ", ";
                text += order.XpReward + " XP";
            }

            return text.Length == 0 ? "nothing" : text;
        }
    }
}
