using System;
using AlphaTown.Core.Events;
using AlphaTown.Data.Items;
using AlphaTown.Data.Recipes;
using AlphaTown.Gameplay.Inventory;
using AlphaTown.Gameplay.Production;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    public sealed class ProducerTests
    {
        ManualTimeSource _time;
        GameClock _clock;
        FakeDatabase _database;
        BarnInventory _barn;
        Producer _producer;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeSource();
            _clock = new GameClock(_time);
            var events = new EventBus();

            var recipe = new FakeRecipe(
                "bread",
                TimeSpan.FromSeconds(60),
                new[] { new ItemStack("flour", 1) },
                new[] { new ItemStack("bread", 1) });

            _database = new FakeDatabase()
                .WithItem(new FakeItem("flour"))
                .WithItem(new FakeItem("bread"))
                .WithRecipe(recipe)
                .WithStorage(new FakeStorage(100));

            _database.WithProducer(new FakeProducerDefinition(
                "bakery",
                new IRecipeDefinition[] { recipe },
                new FakeProducerLevel(queueCapacity: 3, parallelSlots: 1)));

            _barn = new BarnInventory(_database, _database.DefaultStorage, events);
            _database.TryGetProducer("bakery", out var definition);
            _producer = new Producer("bakery_1", definition, _database, _clock, events);
        }

        [Test]
        public void Enqueue_ConsumesInputsImmediately()
        {
            _barn.Add("flour", 3);

            Assert.That(_producer.TryEnqueue("bread", _barn), Is.True);
            Assert.That(_barn.CountOf("flour"), Is.EqualTo(2));
        }

        [Test]
        public void Enqueue_FailsWhenInputsAreMissing()
        {
            Assert.That(_producer.TryEnqueue("bread", _barn), Is.False);
            Assert.That(_producer.Orders.Count, Is.EqualTo(0));
        }

        [Test]
        public void Enqueue_RespectsQueueCapacity()
        {
            _barn.Add("flour", 10);
            for (var i = 0; i < 3; i++) Assert.That(_producer.TryEnqueue("bread", _barn), Is.True);

            Assert.That(_producer.TryEnqueue("bread", _barn), Is.False);
            Assert.That(_barn.CountOf("flour"), Is.EqualTo(7), "a rejected order must not charge inputs");
        }

        /// <summary>
        /// The offline-progression guarantee: an absence longer than the whole queue resolves in
        /// one Sync, with no ticking and no lost orders.
        /// </summary>
        [Test]
        public void Sync_ResolvesAnEntireChainAfterAnAbsence()
        {
            _barn.Add("flour", 3);
            for (var i = 0; i < 3; i++) _producer.TryEnqueue("bread", _barn);

            _time.Advance(TimeSpan.FromMinutes(10));
            _producer.Sync();

            Assert.That(_producer.Orders.Count, Is.EqualTo(0));
            Assert.That(_producer.Ready.Count, Is.EqualTo(1));
            Assert.That(_producer.Ready[0].Count, Is.EqualTo(3));
        }

        /// <summary>
        /// Queued orders run one after another, not in parallel — a single slot with three orders
        /// is 180 seconds of work, so 90 seconds in exactly one is done.
        /// </summary>
        [Test]
        public void Sync_RunsQueuedOrdersSequentially()
        {
            _barn.Add("flour", 3);
            for (var i = 0; i < 3; i++) _producer.TryEnqueue("bread", _barn);

            _time.Advance(TimeSpan.FromSeconds(90));
            _producer.Sync();

            Assert.That(_producer.Ready.Count, Is.EqualTo(1));
            Assert.That(_producer.Ready[0].Count, Is.EqualTo(1));
            Assert.That(_producer.Orders.Count, Is.EqualTo(2));
        }

        [Test]
        public void CollectReady_MovesFinishedGoodsIntoTheBarn()
        {
            _barn.Add("flour", 1);
            _producer.TryEnqueue("bread", _barn);
            _time.Advance(TimeSpan.FromMinutes(2));
            _producer.Sync();

            var collected = _producer.CollectReady(_barn);

            Assert.That(collected, Is.EqualTo(1));
            Assert.That(_barn.CountOf("bread"), Is.EqualTo(1));
            Assert.That(_producer.Ready.Count, Is.EqualTo(0));
        }

        [Test]
        public void TryFinishNow_CompletesOnTheNextSync()
        {
            _barn.Add("flour", 1);
            _producer.TryEnqueue("bread", _barn);

            Assert.That(_producer.TryFinishNow(0), Is.True);
            Assert.That(_producer.Orders.Count, Is.EqualTo(0));
            Assert.That(_producer.Ready.Count, Is.EqualTo(1));
        }
    }
}
