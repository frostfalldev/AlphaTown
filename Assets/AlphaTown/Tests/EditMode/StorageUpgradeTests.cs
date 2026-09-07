using System;
using AlphaTown.Core.Events;
using AlphaTown.Core.Spatial;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.Saving;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// The granary is the only thing that grows the barn, and the barn filling is the pressure
    /// that sends the player to the order board. A bottleneck that can never be relieved stops
    /// being pressure and becomes a wall, so these pin down that the relief actually arrives —
    /// and that it never goes backwards, which would strand goods the player already earned.
    /// </summary>
    public sealed class StorageUpgradeTests
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

            _database = TestContent.Build(startingCoins: 1000);
            _world = new GameWorld(_database, _clock, _events, new Random(5));
            _world.InitialiseNewPlayer();
        }

        string PlaceGranary(int x = 0, int y = 0)
        {
            Assert.That(_world.Buildings.TryPlace(TestContent.Granary, new GridPosition(x, y), out var id),
                Is.EqualTo(BuildingActionResult.Success));

            return id;
        }

        [Test]
        public void TheBarnStartsAtTheLevelTheNewGameAsksFor()
        {
            Assert.That(_world.Barn.Level, Is.EqualTo(1));
        }

        [Test]
        public void AGranaryUnderConstructionHasNotGrownTheBarnYet()
        {
            PlaceGranary();

            Assert.That(_world.Barn.Level, Is.EqualTo(1), "a level is not earned until it is built");
        }

        [Test]
        public void AFinishedGranaryGrowsTheBarn()
        {
            var capacityBefore = _world.Barn.Capacity;
            PlaceGranary();

            _time.Advance(TimeSpan.FromSeconds(TestContent.GranaryBuildSeconds));
            _world.Sync();

            Assert.That(_world.Barn.Level, Is.EqualTo(TestContent.GranaryStorageLevel));
            Assert.That(_world.Barn.Capacity, Is.GreaterThan(capacityBefore));
        }

        /// <summary>A build that finished while the app was closed applies on the sync that resolves it.</summary>
        [Test]
        public void AGranaryFinishedOfflineGrowsTheBarnOnResume()
        {
            PlaceGranary();

            _time.Advance(TimeSpan.FromDays(2));
            _world.Sync();

            Assert.That(_world.Barn.Level, Is.EqualTo(TestContent.GranaryStorageLevel));
        }

        /// <summary>
        /// Storage is a tier reached, not a stack of bonuses — otherwise the cheapest granary,
        /// bought ten times, would be the whole economy.
        /// </summary>
        [Test]
        public void ASecondGranaryAddsNothing()
        {
            PlaceGranary(0, 0);
            PlaceGranary(2, 0);

            _time.Advance(TimeSpan.FromSeconds(TestContent.GranaryBuildSeconds));
            _world.Sync();

            Assert.That(_world.Barn.Level, Is.EqualTo(TestContent.GranaryStorageLevel));
        }

        /// <summary>
        /// Demolishing keeps the space. Shrinking the barn below what is already in it would
        /// strand goods, so the level only ever moves up.
        /// </summary>
        [Test]
        public void RemovingAGranaryDoesNotShrinkTheBarn()
        {
            var granary = PlaceGranary();
            _time.Advance(TimeSpan.FromSeconds(TestContent.GranaryBuildSeconds));
            _world.Sync();

            Assert.That(_world.Buildings.TryRemove(granary), Is.EqualTo(BuildingActionResult.Success));
            _world.Sync();

            Assert.That(_world.Barn.Level, Is.EqualTo(TestContent.GranaryStorageLevel));
        }

        [Test]
        public void TheGrownBarnSurvivesASaveRoundTrip()
        {
            PlaceGranary();
            _time.Advance(TimeSpan.FromSeconds(TestContent.GranaryBuildSeconds));
            _world.Sync();

            var save = _world.CaptureSave();

            var restored = new GameWorld(_database, _clock, _events, new Random(5));
            restored.RestoreSave(save);

            Assert.That(restored.Barn.Level, Is.EqualTo(TestContent.GranaryStorageLevel));
        }

        /// <summary>
        /// Recomputing every sync rather than applying once is what makes this self-healing: a
        /// save written before the granary shipped, or one whose storage level was retuned in
        /// content afterwards, lands on the right answer without a migration.
        /// </summary>
        [Test]
        public void ASaveWithAStaleBarnLevelHealsOnLoad()
        {
            PlaceGranary();
            _time.Advance(TimeSpan.FromSeconds(TestContent.GranaryBuildSeconds));
            _world.Sync();

            var save = _world.CaptureSave();
            save.Inventory.Level = 1; // As if written before storage buildings existed.

            var restored = new GameWorld(_database, _clock, _events, new Random(5));
            restored.RestoreSave(save);

            Assert.That(restored.Barn.Level, Is.EqualTo(TestContent.GranaryStorageLevel));
        }

        [Test]
        public void ATownWithNoStorageBuildingKeepsItsStartingBarn()
        {
            _world.Buildings.TryPlace(TestContent.Shed, GridPosition.Zero, out _);
            _world.Sync();

            Assert.That(_world.Barn.Level, Is.EqualTo(1));
        }
    }
}
