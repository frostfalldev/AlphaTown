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
    /// <summary>
    /// A recipe may yield more than it promises. The rule that makes that safe is that the extra
    /// is decided by the completed order, not by a roll at the moment someone looks — so an
    /// offline harvest and a watched one agree, and re-syncing never changes an answer.
    /// </summary>
    public sealed class BonusYieldTests
    {
        const string Crop = "crop";
        const string CropRecipe = "recipe.crop";
        const string Plot = "plot";

        ManualTimeSource _time;
        GameClock _clock;
        EventBus _events;
        FakeDatabase _database;
        BarnInventory _barn;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeSource();
            _clock = new GameClock(_time);
            _events = new EventBus();
        }

        FakeDatabase BuildDatabase(int bonusOutputMax)
        {
            var recipe = new FakeRecipe(
                CropRecipe,
                TimeSpan.FromSeconds(60),
                null,
                new[] { new ItemStack(Crop, 2) },
                bonusOutputMax: bonusOutputMax);

            _database = new FakeDatabase()
                .WithItem(new FakeItem(Crop))
                .WithRecipe(recipe)
                .WithStorage(new FakeStorage(1000))
                .WithProducer(new FakeProducerDefinition(
                    Plot,
                    new IRecipeDefinition[] { recipe },
                    new FakeProducerLevel(queueCapacity: 4, parallelSlots: 1)));

            _barn = new BarnInventory(_database, _database.DefaultStorage, _events);
            return _database;
        }

        Producer CreateProducer(string instanceId = "plot_1")
        {
            _database.TryGetProducer(Plot, out var definition);
            return new Producer(instanceId, definition, _database, _clock, _events,
                new FakeUnlockGate(townLevel: 10));
        }

        int HarvestOnce(string instanceId)
        {
            var producer = CreateProducer(instanceId);
            producer.TryEnqueue(CropRecipe, _barn);
            _time.Advance(TimeSpan.FromSeconds(60));
            producer.Sync();

            return producer.Ready.Count == 0 ? 0 : producer.Ready[0].Count;
        }

        [Test]
        public void ARecipeWithNoBonusAlwaysYieldsWhatItSays()
        {
            BuildDatabase(bonusOutputMax: 0);

            Assert.That(HarvestOnce("plot_1"), Is.EqualTo(2));
        }

        [Test]
        public void ABonusNeverFallsBelowTheAuthoredYield()
        {
            BuildDatabase(bonusOutputMax: 3);

            for (var i = 0; i < 20; i++)
            {
                _time.Advance(TimeSpan.FromSeconds(7));
                Assert.That(HarvestOnce("plot_" + i), Is.InRange(2, 5));
            }
        }

        /// <summary>
        /// The property that matters: the same completed order gives the same answer every time it
        /// is resolved. A restored save is the same building running the same order over the same
        /// window, so it must not disagree with the session that wrote it.
        /// </summary>
        [Test]
        public void TheSameCompletedOrderAlwaysYieldsTheSameAmount()
        {
            BuildDatabase(bonusOutputMax: 4);

            _database.TryGetProducer(Plot, out var definition);

            var first = new Producer("plot_1", definition, _database, _clock, _events, new FakeUnlockGate(10));
            var second = new Producer("plot_1", definition, _database, _clock, _events, new FakeUnlockGate(10));

            first.TryEnqueue(CropRecipe, _barn);
            second.TryEnqueue(CropRecipe, _barn);

            _time.Advance(TimeSpan.FromSeconds(60));
            first.Sync();
            second.Sync();

            Assert.That(second.Ready[0].Count, Is.EqualTo(first.Ready[0].Count));
        }

        /// <summary>
        /// Catching up after a long absence must give the same total as having been present for
        /// every completion — the whole point of resolving from timestamps. Both sides are the
        /// same building, because that is what a reload is.
        /// </summary>
        [Test]
        public void OfflineCompletionsMatchWatchedOnes()
        {
            BuildDatabase(bonusOutputMax: 3);
            _database.TryGetProducer(Plot, out var definition);

            var watched = new Producer("watched", definition, _database, _clock, _events, new FakeUnlockGate(10));
            watched.TryEnqueue(CropRecipe, _barn);
            watched.TryEnqueue(CropRecipe, _barn);
            watched.TryEnqueue(CropRecipe, _barn);

            var offline = new Producer("watched", definition, _database, _clock, _events, new FakeUnlockGate(10));
            offline.TryEnqueue(CropRecipe, _barn);
            offline.TryEnqueue(CropRecipe, _barn);
            offline.TryEnqueue(CropRecipe, _barn);

            // One steps through each minute; the other sleeps through all three and resolves once.
            for (var i = 0; i < 3; i++)
            {
                _time.Advance(TimeSpan.FromSeconds(60));
                watched.Sync();
            }

            offline.Sync();

            Assert.That(offline.Ready[0].Count, Is.EqualTo(watched.Ready[0].Count));
        }
    }
}
