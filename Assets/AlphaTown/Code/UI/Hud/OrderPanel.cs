using System;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Items;
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
    ///
    /// A line the barn cannot cover offers to buy the difference. Buying belongs here rather than
    /// in a shop of its own because this is the moment a player discovers they are short — and it
    /// is priced to lose money against the payout, so it stays a way to skip a wait rather than a
    /// way to play.
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

            var requests = UiKit.Column(6f);
            for (var i = 0; i < order.Requests.Count; i++)
            {
                requests.Add(BuildRequestLine(world, order, order.Requests[i]));
            }

            card.Add(requests);
            card.Add(UiKit.Caption("Pays " + DescribeRewards(order)));

            var actions = UiKit.Row(10f);

            var deliver = UiKit.Action("Deliver", () => _report?.Invoke(_commands.Deliver(order.OrderId)));
            UiKit.SetEnabled(deliver, board.CanComplete(order.OrderId));
            actions.Add(deliver);

            // Buying out of a bad order is the recurring thing coins are for. Priced on the
            // button, like everything else here, because the decision is whether it is worth it.
            var rerollCost = board.RerollCost(order.OrderId);
            var reroll = UiKit.Action("Reroll  " + rerollCost,
                () => _report?.Invoke(_commands.Reroll(order.OrderId)));

            UiKit.SetEnabled(reroll, world.Wallet.CanAfford(SoftCurrencyId, rerollCost));
            actions.Add(reroll);

            card.Add(actions);

            return card;
        }

        /// <summary>
        /// One request, and — when the barn is short — what closing the gap would cost.
        ///
        /// The price is on the button rather than hidden behind it, because the answer to "should
        /// I buy this?" is nearly always no, and the player deserves to see that before tapping.
        /// </summary>
        VisualElement BuildRequestLine(GameWorld world, Order order, ItemStack request)
        {
            var row = UiKit.Row(10f);
            row.style.justifyContent = Justify.SpaceBetween;

            var held = world.Barn.CountOf(request.ItemId);
            var covered = held >= request.Count;

            var line = UiKit.Text(
                held + "/" + request.Count + " " + DisplayNames.ForItem(_database, request.ItemId), 22);

            line.style.color = covered ? UiKit.Accent : UiKit.Muted;
            row.Add(line);

            if (covered) return row;

            var shortfall = request.Count - held;
            var cost = world.Market.PriceToBuy(request.ItemId, shortfall);
            if (cost <= 0) return row;

            var buy = UiKit.Action("Buy " + shortfall + "  " + cost, () =>
                _report?.Invoke(_commands.BuyShortfall(order.OrderId, request.ItemId)));

            buy.style.minHeight = UiKit.TouchTarget * 0.7f;
            buy.style.fontSize = 22;
            UiKit.SetEnabled(buy, world.Wallet.CanAfford(SoftCurrencyId, cost) &&
                                  world.Barn.RoomFor(request.ItemId) >= shortfall);

            row.Add(buy);
            return row;
        }

        string SoftCurrencyId
        {
            get
            {
                var currency = _database != null ? _database.SoftCurrency : null;
                return currency != null ? currency.Id : string.Empty;
            }
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
