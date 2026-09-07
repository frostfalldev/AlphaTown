using System;
using AlphaTown.Core.Events;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Buildings;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.Commands;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// An animal is a producer that eats, and that one input is the whole difference between a
    /// coop and a field. It also means a coop can be *blocked* in a way a field never is, which is
    /// why the panel now has to explain itself rather than saying "no crop is unlocked".
    /// </summary>
    public sealed class LivestockTests
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

            _database = TestContent.Build(startingCoins: 1000, includeFarming: true);
            _world = new GameWorld(_database, _clock, _events, new Random(13));
            _world.InitialiseNewPlayer();
            _commands = new TownCommands(_world, _database, _clock);
        }

        string PlaceCoop(int x = 0, int y = 0)
        {
            Assert.That(_world.Buildings.TryPlace(TestContent.CoopBuilding, new GridPosition(x, y), out var id),
                Is.EqualTo(BuildingActionResult.Success));

            return id;
        }

        [Test]
        public void HungryAnimalsCannotStart()
        {
            var coop = PlaceCoop();

            var result = _commands.Plant(coop);

            Assert.That(result.Success, Is.False);
            Assert.That(_world.TryGetProducer(coop, out var producer), Is.True);
            Assert.That(producer.IsIdle, Is.True);
        }

        /// <summary>
        /// The message is the feature. "No crop is unlocked" is fair at an empty field and
        /// baffling at a coop whose hens are merely out of feed.
        /// </summary>
        [Test]
        public void AHungryCoopSaysWhatItNeeds()
        {
            var coop = PlaceCoop();

            var result = _commands.Plant(coop);

            Assert.That(result.Message, Does.Contain("Needs"));
            Assert.That(result.Message, Does.Contain("Feed"));
        }

        [Test]
        public void FeedingAnimalsStartsThem()
        {
            var coop = PlaceCoop();
            _world.Barn.Add(TestContent.Feed, TestContent.FeedPerEggCollection);

            Assert.That(_commands.Plant(coop).Success, Is.True);
            Assert.That(_world.TryGetProducer(coop, out var producer), Is.True);
            Assert.That(producer.TryGetActiveOrder(out _), Is.True);
        }

        /// <summary>The feed is gone the moment they are set going, not when they finish.</summary>
        [Test]
        public void FeedIsConsumedOnStarting()
        {
            var coop = PlaceCoop();
            _world.Barn.Add(TestContent.Feed, TestContent.FeedPerEggCollection + 1);

            _commands.Plant(coop);

            Assert.That(_world.Barn.CountOf(TestContent.Feed), Is.EqualTo(1));
        }

        [Test]
        public void FedAnimalsProduce()
        {
            var coop = PlaceCoop();
            _world.Barn.Add(TestContent.Feed, TestContent.FeedPerEggCollection);
            _commands.Plant(coop);

            _time.Advance(TimeSpan.FromSeconds(TestContent.EggCollectSeconds));
            _world.Sync();

            Assert.That(_commands.Harvest(coop).Success, Is.True);
            Assert.That(_world.Barn.CountOf(TestContent.Eggs), Is.GreaterThanOrEqualTo(TestContent.EggYield));
        }

        /// <summary>
        /// The chain that makes livestock worth having: crops in, better goods out. Grow wheat,
        /// mill it into feed, feed the hens, collect eggs — all through the ordinary producer
        /// machinery, with no special case for animals anywhere.
        /// </summary>
        [Test]
        public void TheWholeChainRunsFromWheatToEggs()
        {
            var field = PlaceField();
            var mill = PlaceMill();
            var coop = PlaceCoop(4, 4);

            // Wheat.
            _commands.Plant(field, TestContent.WheatCrop);
            _time.Advance(TimeSpan.FromSeconds(TestContent.WheatGrowSeconds));
            _world.Sync();
            _commands.Harvest(field);

            Assert.That(_world.Barn.CountOf(TestContent.Wheat), Is.GreaterThanOrEqualTo(2));

            // Feed.
            Assert.That(_world.TryGetProducer(mill, out var miller), Is.True);
            Assert.That(miller.TryEnqueue(TestContent.FeedRecipe, _world.Barn), Is.True);
            _time.Advance(TimeSpan.FromSeconds(60));
            _world.Sync();
            _world.Collect(mill);

            Assert.That(_world.Barn.CountOf(TestContent.Feed), Is.GreaterThan(0));

            // Not enough feed for a collection yet, so the coop still says what it wants.
            while (_world.Barn.CountOf(TestContent.Feed) < TestContent.FeedPerEggCollection)
            {
                _world.Barn.Add(TestContent.Feed, 1);
            }

            Assert.That(_commands.Plant(coop).Success, Is.True);

            _time.Advance(TimeSpan.FromSeconds(TestContent.EggCollectSeconds));
            _world.Sync();
            _commands.Harvest(coop);

            Assert.That(_world.Barn.CountOf(TestContent.Eggs), Is.GreaterThanOrEqualTo(TestContent.EggYield));
        }

        [Test]
        public void LivestockIsNotFiledUnderFarming()
        {
            PlaceCoop();

            var fields = new System.Collections.Generic.List<BuildingInstance>();
            _world.Buildings.CollectByCategory(BuildingCategory.Farming, fields);
            Assert.That(fields, Is.Empty);

            var pens = new System.Collections.Generic.List<BuildingInstance>();
            _world.Buildings.CollectByCategory(BuildingCategory.Livestock, pens);
            Assert.That(pens.Count, Is.EqualTo(1));
        }

        string PlaceField()
        {
            Assert.That(_world.Buildings.TryPlace(TestContent.FieldBuilding, new GridPosition(1, 0), out var id),
                Is.EqualTo(BuildingActionResult.Success));

            return id;
        }

        string PlaceMill()
        {
            Assert.That(_world.Buildings.TryPlace(TestContent.MillBuilding, new GridPosition(2, 2), out var id),
                Is.EqualTo(BuildingActionResult.Success));

            return id;
        }
    }
}
