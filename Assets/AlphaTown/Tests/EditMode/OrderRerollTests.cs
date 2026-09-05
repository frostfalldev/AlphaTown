using System;
using AlphaTown.Core.Events;
using AlphaTown.Data.Economy;
using AlphaTown.Gameplay.Commands;
using AlphaTown.Gameplay.Orders;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// Rerolling is the recurring thing coins are for, and the only coin sink that cannot bend the
    /// production economy: it moves no goods, it only changes which order is on offer. What the
    /// player buys is the slot's cooldown — discarding for free already exists and leaves the slot
    /// cooling, so the wait is the only thing left to sell.
    /// </summary>
    public sealed class OrderRerollTests
    {
        ManualTimeSource _time;
        GameClock _clock;
        EventBus _events;
        FakeDatabase _database;
        GameWorld _world;
        TownCommands _commands;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeSource();
            _clock = new GameClock(_time);
            _events = new EventBus();

            _database = TestContent.Build(startingCoins: 5000);
            _world = new GameWorld(_database, _clock, _events, new Random(17));
            _world.InitialiseNewPlayer();
            _commands = new TownCommands(_world, _database, _clock);
        }

        Order FirstOrder() => _world.HelicopterOrders.Orders[0];

        [Test]
        public void RerollingReplacesTheOrderImmediately()
        {
            var before = FirstOrder().OrderId;
            var slots = _world.HelicopterOrders.Orders.Count;

            Assert.That(_world.HelicopterOrders.TryReroll(before), Is.True);

            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(slots),
                "the slot refills at once — skipping the cooldown is what was paid for");

            Assert.That(_world.HelicopterOrders.TryGetOrder(before, out _), Is.False);
        }

        [Test]
        public void RerollingCostsCoins()
        {
            var order = FirstOrder();
            var cost = _world.HelicopterOrders.RerollCost(order.OrderId);
            var before = _world.Wallet.BalanceOf(TestContent.Coins);

            Assert.That(cost, Is.GreaterThan(0));
            _world.HelicopterOrders.TryReroll(order.OrderId);

            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(before - cost));
        }

        /// <summary>
        /// Priced against what the order would have paid, so dodging a lucrative order costs more
        /// than clearing a trivial one.
        /// </summary>
        [Test]
        public void ThePriceScalesWithTheOrdersReward()
        {
            var order = FirstOrder();

            var reward = 0;
            for (var i = 0; i < order.CurrencyRewards.Count; i++)
            {
                if (order.CurrencyRewards[i].CurrencyId == TestContent.Coins)
                    reward += order.CurrencyRewards[i].Amount;
            }

            var expected = reward * TestContent.RerollCostPercent / 100;
            if (expected < TestContent.RerollBaseCost) expected = TestContent.RerollBaseCost;

            Assert.That(_world.HelicopterOrders.RerollCost(order.OrderId), Is.EqualTo(expected));
        }

        /// <summary>A free reroll turns the board into a slot machine you pull until it pays.</summary>
        [Test]
        public void ThePriceNeverFallsBelowTheFloor()
        {
            Assert.That(_world.HelicopterOrders.RerollCost(FirstOrder().OrderId),
                Is.GreaterThanOrEqualTo(TestContent.RerollBaseCost));
        }

        [Test]
        public void RerollingWithoutTheCoinsDoesNothing()
        {
            _world.Wallet.ResetTo(Array.Empty<CurrencyAmount>());

            var order = FirstOrder();
            Assert.That(_world.HelicopterOrders.TryReroll(order.OrderId), Is.False);
            Assert.That(_world.HelicopterOrders.TryGetOrder(order.OrderId, out _), Is.True,
                "the order is still there");
        }

        [Test]
        public void RerollingAnOrderThatIsGoneDoesNothing()
        {
            var before = _world.Wallet.BalanceOf(TestContent.Coins);

            Assert.That(_world.HelicopterOrders.TryReroll("order_nope"), Is.False);
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(before));
        }

        [Test]
        public void RerollingIsAttributedToItsOwnSink()
        {
            var order = FirstOrder();
            var cost = _world.HelicopterOrders.RerollCost(order.OrderId);

            _world.HelicopterOrders.TryReroll(order.OrderId);

            Assert.That(_world.Ledger.TotalTo(TestContent.Coins, CurrencySink.OrderReroll),
                Is.EqualTo((long)cost));
        }

        [Test]
        public void RerollingRaisesAnEvent()
        {
            var seen = 0;
            using (_events.Subscribe<OrderRerolledEvent>(_ => seen++))
            {
                _world.HelicopterOrders.TryReroll(FirstOrder().OrderId);
            }

            Assert.That(seen, Is.EqualTo(1));
        }

        /// <summary>
        /// Discarding is still free and still cools the slot. That contrast is the product: the
        /// coins buy the wait, nothing else.
        /// </summary>
        [Test]
        public void DiscardingIsStillFreeAndStillCoolsTheSlot()
        {
            var before = _world.Wallet.BalanceOf(TestContent.Coins);
            var slots = _world.HelicopterOrders.Orders.Count;

            Assert.That(_world.HelicopterOrders.TryDiscard(FirstOrder().OrderId), Is.True);

            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(before));
            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(slots - 1));
        }

        [Test]
        public void RerollingMovesNoGoods()
        {
            _world.Barn.Add(TestContent.Bread, 3);

            _world.HelicopterOrders.TryReroll(FirstOrder().OrderId);

            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(3),
                "a reroll cannot bend the goods economy because it never touches goods");
        }

        // --- Through the command layer ------------------------------------------------------

        [Test]
        public void TheCommandSaysWhatARerollCosts()
        {
            _world.Wallet.ResetTo(Array.Empty<CurrencyAmount>());

            var result = _commands.Reroll(FirstOrder().OrderId);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("costs"));
        }

        [Test]
        public void TheCommandRerolls()
        {
            Assert.That(_commands.Reroll(FirstOrder().OrderId).Success, Is.True);
        }
    }
}
