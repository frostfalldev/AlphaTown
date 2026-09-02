using System;
using System.Collections.Generic;
using AlphaTown.Core.Events;
using AlphaTown.Data.Items;
using AlphaTown.Gameplay.Orders;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    public sealed class OrderBoardTests
    {
        ManualTimeSource _time;
        GameClock _clock;
        EventBus _events;
        GameWorld _world;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeSource();
            _clock = new GameClock(_time);
            _events = new EventBus();
            _world = new GameWorld(TestContent.Build(), _clock, _events, new Random(20260902));
            _world.InitialiseNewPlayer();
        }

        static bool Requests(Order order, string itemId)
        {
            for (var i = 0; i < order.Requests.Count; i++)
            {
                if (order.Requests[i].ItemId == itemId) return true;
            }

            return false;
        }

        [Test]
        public void NewBoard_FillsToCapacity()
        {
            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(4));
        }

        /// <summary>
        /// The pool is derived from unlocked recipes, so the player can never be asked for
        /// something they have no way to make. Cake needs town level 3.
        /// </summary>
        [Test]
        public void Generation_NeverAsksForLockedGoods()
        {
            foreach (var order in _world.HelicopterOrders.Orders)
            {
                Assert.That(Requests(order, TestContent.Cake), Is.False, "cake is locked at level 1");
                Assert.That(Requests(order, TestContent.Bread), Is.True);
            }
        }

        [Test]
        public void CanComplete_IsFalseWithAnEmptyBarn()
        {
            var order = _world.HelicopterOrders.Orders[0];

            Assert.That(_world.HelicopterOrders.CanComplete(order.OrderId), Is.False);
            Assert.That(_world.HelicopterOrders.TryComplete(order.OrderId), Is.False);
            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(4), "a failed delivery keeps the order");
        }

        [Test]
        public void Complete_ConsumesGoodsAndPaysCoinsAndXp()
        {
            _world.Barn.Add(TestContent.Bread, 1);
            var order = _world.HelicopterOrders.Orders[0];

            Assert.That(_world.HelicopterOrders.TryComplete(order.OrderId), Is.True);

            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(0));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(TestContent.BreadCoinValue));
            Assert.That(_world.Progression.TotalXp, Is.EqualTo(TestContent.BreadXpValue));
        }

        [Test]
        public void Complete_PublishesTheOrderAndRefillsTheSlot()
        {
            _world.Barn.Add(TestContent.Bread, 1);
            var order = _world.HelicopterOrders.Orders[0];

            var completed = 0;
            using (_events.Subscribe<OrderCompletedEvent>(e =>
                   {
                       completed++;
                       Assert.That(e.OrderId, Is.EqualTo(order.OrderId));
                       Assert.That(e.XpAwarded, Is.EqualTo(TestContent.BreadXpValue));
                   }))
            {
                _world.HelicopterOrders.TryComplete(order.OrderId);
            }

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(_world.HelicopterOrders.TryGetOrder(order.OrderId, out _), Is.False);

            // The slot cools rather than refilling on the spot — see OrderBoardPacingTests.
            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(3));
        }

        [Test]
        public void Complete_PaysTheRewardTheOrderPromised()
        {
            _world.Barn.Add(TestContent.Bread, 1);
            var order = _world.HelicopterOrders.Orders[0];

            Assert.That(order.CurrencyRewards.Count, Is.EqualTo(1));
            Assert.That(order.CurrencyRewards[0].CurrencyId, Is.EqualTo(TestContent.Coins));

            var promised = order.CurrencyRewards[0].Amount;
            _world.HelicopterOrders.TryComplete(order.OrderId);

            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(promised));
        }

        /// <summary>
        /// The loop's payoff: levelling up widens the pool the next order is drawn from. With a
        /// template that always asks for two item types, every order after the unlock must
        /// include cake, so this holds without depending on the seed.
        /// </summary>
        [Test]
        public void Generation_WidensAsRecipesUnlock()
        {
            var world = new GameWorld(
                TestContent.Build(orderTemplate: TestContent.TwoItemTemplate()),
                _clock, _events, new Random(7));
            world.InitialiseNewPlayer();

            foreach (var order in world.Orders())
            {
                Assert.That(order.Requests.Count, Is.EqualTo(1), "only bread is producible at level 1");
            }

            world.Progression.GrantXp(300, Data.Progression.XpSource.DebugGrant);
            Assert.That(world.Progression.TownLevel, Is.EqualTo(3));

            var stale = new List<string>();
            foreach (var order in world.Orders()) stale.Add(order.OrderId);
            foreach (var id in stale) world.HelicopterOrders.TryDiscard(id);

            // Discarding puts each slot on cooldown, so wait it out before the board refills.
            _time.Advance(TimeSpan.FromSeconds(TestContent.OrderSlotCooldownSeconds + 1));
            world.Sync();

            foreach (var order in world.Orders())
            {
                Assert.That(order.Requests.Count, Is.EqualTo(2));
                Assert.That(Requests(order, TestContent.Cake), Is.True, "cake is unlocked at level 3");
            }
        }

        [Test]
        public void TimeLimitedOrders_ExpireOnSyncAndAreReplaced()
        {
            var world = new GameWorld(
                TestContent.Build(orderTemplate: TestContent.TimedTemplate(TimeSpan.FromHours(1))),
                _clock, _events, new Random(11));
            world.InitialiseNewPlayer();

            var original = new List<string>();
            foreach (var order in world.Orders()) original.Add(order.OrderId);
            Assert.That(original.Count, Is.EqualTo(4));

            var expired = 0;
            using (_events.Subscribe<OrderExpiredEvent>(_ => expired++))
            {
                _time.Advance(TimeSpan.FromHours(2));
                world.Sync();
            }

            Assert.That(expired, Is.EqualTo(4));
            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(0),
                "expired slots go on cooldown rather than refilling immediately");

            _time.Advance(TimeSpan.FromSeconds(TestContent.OrderSlotCooldownSeconds + 1));
            world.Sync();

            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(4), "the board refills after the cooldown");
            foreach (var order in world.Orders())
            {
                Assert.That(original, Does.Not.Contain(order.OrderId));
            }
        }

        [Test]
        public void ExpiredOrder_CannotBeCompleted()
        {
            var world = new GameWorld(
                TestContent.Build(orderTemplate: TestContent.TimedTemplate(TimeSpan.FromHours(1))),
                _clock, _events, new Random(13));
            world.InitialiseNewPlayer();
            world.Barn.Add(TestContent.Bread, 4);

            var order = world.HelicopterOrders.Orders[0];
            _time.Advance(TimeSpan.FromHours(2));

            Assert.That(world.HelicopterOrders.CanComplete(order.OrderId), Is.False);
            Assert.That(world.HelicopterOrders.TryComplete(order.OrderId), Is.False);
            Assert.That(world.Barn.CountOf(TestContent.Bread), Is.EqualTo(4));
        }

        [Test]
        public void NothingProducible_LeavesTheBoardEmpty()
        {
            var database = new FakeDatabase()
                .WithItem(new FakeItem(TestContent.Cake))
                .WithRecipe(new FakeRecipe(
                    TestContent.CakeRecipe,
                    TimeSpan.FromMinutes(1),
                    null,
                    new[] { new ItemStack(TestContent.Cake, 1) },
                    unlockLevel: 5))
                .WithStorage(new FakeStorage(100))
                .WithCurrency(new FakeCurrency(TestContent.Coins, Data.Economy.CurrencyKind.Soft))
                .WithProgressionCurve(new FakeProgressionCurve(100))
                .WithOrderTemplate(TestContent.SingleBreadTemplate());

            var world = new GameWorld(database, _clock, _events, new Random(3));
            world.InitialiseNewPlayer();

            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(0));
        }
    }

    internal static class OrderBoardTestExtensions
    {
        /// <summary>Snapshot of the board, so a test can iterate while the board is being changed.</summary>
        public static List<Order> Orders(this GameWorld world)
        {
            var copy = new List<Order>(world.HelicopterOrders.Orders.Count);
            for (var i = 0; i < world.HelicopterOrders.Orders.Count; i++)
            {
                copy.Add(world.HelicopterOrders.Orders[i]);
            }

            return copy;
        }
    }
}
