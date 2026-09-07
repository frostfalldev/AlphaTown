using System;
using AlphaTown.Core.Events;
using AlphaTown.Data.Economy;
using AlphaTown.Gameplay.Saving;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Save;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// End-to-end cover for the loop the three systems exist to close:
    /// produce → deliver → earn coins and XP → level up → unlock more production.
    /// </summary>
    public sealed class EconomicLoopTests
    {
        ManualTimeSource _time;
        GameClock _clock;
        EventBus _events;
        FakeDatabase _database;
        GameWorld _world;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeSource();
            _clock = new GameClock(_time);
            _events = new EventBus();

            // Eight XP per level, four XP per bread order: two deliveries per level.
            _database = TestContent.Build(xpCurve: new[] { 8, 8, 0 });
            _world = new GameWorld(_database, _clock, _events, new Random(20260902));
            _world.InitialiseNewPlayer();
        }

        [Test]
        public void LockedRecipe_IsRejectedEvenWithTheIngredientsInHand()
        {
            var bakery = _world.AddProducer("bakery_1", TestContent.Bakery);
            _world.Barn.Add(TestContent.Bread, 5);

            Assert.That(_world.Progression.TownLevel, Is.EqualTo(1));
            Assert.That(bakery.TryEnqueue(TestContent.CakeRecipe, _world.Barn), Is.False);
            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(5),
                "a rejected order must not charge its inputs");
        }

        [Test]
        public void FullLoop_ProduceDeliverEarnUnlock()
        {
            var bakery = _world.AddProducer("bakery_1", TestContent.Bakery);
            _world.Barn.Add(TestContent.Flour, 10);

            // Produce six loaves: fill the queue, wait it out, collect, repeat.
            for (var batch = 0; batch < 2; batch++)
            {
                for (var i = 0; i < 3; i++)
                {
                    Assert.That(bakery.TryEnqueue(TestContent.BreadRecipe, _world.Barn), Is.True);
                }

                _time.Advance(TimeSpan.FromHours(1));
                bakery.Sync();
                bakery.CollectReady(_world.Barn);
            }

            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(6));
            Assert.That(_world.Barn.CountOf(TestContent.Flour), Is.EqualTo(4));

            // Deliver four orders. Each was generated below level 3, so each asks for bread.
            for (var i = 0; i < 4; i++)
            {
                var order = _world.HelicopterOrders.Orders[0];
                Assert.That(_world.HelicopterOrders.TryComplete(order.OrderId), Is.True);
            }

            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins),
                Is.EqualTo(4 * TestContent.BreadCoinValue));
            Assert.That(_world.Progression.TownLevel, Is.EqualTo(3));
            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(2));

            // The loop closes: the level earned by delivering unlocks the next tier of production.
            Assert.That(bakery.TryEnqueue(TestContent.CakeRecipe, _world.Barn), Is.True);
            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(0));
        }

        [Test]
        public void EveryCoinEarned_IsAttributedToItsSource()
        {
            _world.Barn.Add(TestContent.Bread, 2);

            for (var i = 0; i < 2; i++)
            {
                _world.HelicopterOrders.TryComplete(_world.HelicopterOrders.Orders[0].OrderId);
            }

            var earned = _world.Ledger.TotalEarned(TestContent.Coins);

            Assert.That(_world.Ledger.TotalFrom(TestContent.Coins, CurrencySource.OrderReward),
                Is.EqualTo(earned), "orders are the only faucet in this scenario");
            Assert.That(earned - _world.Ledger.TotalSpent(TestContent.Coins),
                Is.EqualTo(_world.Wallet.BalanceOf(TestContent.Coins)));
        }

        [Test]
        public void EconomyState_SurvivesASaveRoundTrip()
        {
            var saveService = new SaveService(
                new InMemorySaveStore(), new JsonSaveSerializer(), _clock,
                GameWorld.SaveSchemaVersion, null, "tests");

            _world.Barn.Add(TestContent.Bread, 3);
            _world.HelicopterOrders.TryComplete(_world.HelicopterOrders.Orders[0].OrderId);

            var coins = _world.Wallet.BalanceOf(TestContent.Coins);
            var totalXp = _world.Progression.TotalXp;
            var boardSize = _world.HelicopterOrders.Orders.Count;

            Assert.That(saveService.TrySave(GameWorld.DefaultSaveSlot, _world.CaptureSave()), Is.True);
            Assert.That(saveService.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var data), Is.True);

            var restored = new GameWorld(_database, _clock, _events, new Random(5));
            restored.RestoreSave(data);

            Assert.That(restored.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(coins));
            Assert.That(restored.Progression.TotalXp, Is.EqualTo(totalXp));
            Assert.That(restored.Barn.CountOf(TestContent.Bread), Is.EqualTo(2));
            Assert.That(restored.HelicopterOrders.Orders.Count, Is.EqualTo(boardSize));

            // Lifetime attribution has to survive too, or the economy numbers reset every session.
            Assert.That(restored.Ledger.TotalFrom(TestContent.Coins, CurrencySource.OrderReward),
                Is.EqualTo(coins));
        }
    }
}
