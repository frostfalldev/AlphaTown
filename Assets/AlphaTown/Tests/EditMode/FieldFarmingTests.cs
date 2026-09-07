using System;
using System.Collections.Generic;
using AlphaTown.Core.Events;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Buildings;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.Production;
using AlphaTown.Gameplay.Saving;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Save;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// A field is not a new system: it is a Farming building whose producer runs recipes with no
    /// inputs. These tests are the proof that the existing machinery covers the farming loop.
    /// </summary>
    public sealed class FieldFarmingTests
    {
        ManualTimeSource _time;
        GameClock _clock;
        EventBus _events;
        FakeDatabase _database;
        GameWorld _world;
        string _fieldId;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeSource();
            _clock = new GameClock(_time);
            _events = new EventBus();
            _world = CreateWorld();
        }

        GameWorld CreateWorld(int barnCapacity = 100)
        {
            _database = TestContent.Build(
                barnCapacity: barnCapacity, startingCoins: 1000, includeFarming: true);

            var world = new GameWorld(_database, _clock, _events, new Random(7));
            world.InitialiseNewPlayer();
            return world;
        }

        Producer PlaceField(GameWorld world, int x = 0, int y = 0)
        {
            var result = world.Buildings.TryPlace(
                TestContent.FieldBuilding, new GridPosition(x, y), out var instanceId);

            Assert.That(result, Is.EqualTo(BuildingActionResult.Success));
            _fieldId = instanceId;

            // The field builds instantly, so its producer exists the moment it is placed.
            Assert.That(world.TryGetProducer(instanceId, out var producer), Is.True);
            return producer;
        }

        void GrowFully() => _time.Advance(TimeSpan.FromSeconds(TestContent.WheatGrowSeconds + 1));

        [Test]
        public void APlacedField_StartsEmpty()
        {
            var field = PlaceField(_world);

            Assert.That(field.IsIdle, Is.True);
            Assert.That(field.HasReadyGoods, Is.False);
            Assert.That(field.LastRecipeId, Is.Null.Or.Empty);
        }

        [Test]
        public void Planting_CostsNothingAndStartsTheTimer()
        {
            var field = PlaceField(_world);
            var barnBefore = _world.Barn.UsedSpace;

            Assert.That(field.TryEnqueue(TestContent.WheatCrop, _world.Barn), Is.True);

            Assert.That(field.Orders.Count, Is.EqualTo(1));
            Assert.That(field.LastRecipeId, Is.EqualTo(TestContent.WheatCrop));
            Assert.That(_world.Barn.UsedSpace, Is.EqualTo(barnBefore), "a crop with no inputs is free to plant");
        }

        [Test]
        public void AFieldHoldsOneCropAtATime()
        {
            var field = PlaceField(_world);
            field.TryEnqueue(TestContent.WheatCrop, _world.Barn);

            Assert.That(field.TryEnqueue(TestContent.WheatCrop, _world.Barn), Is.False,
                "the field's queue capacity is one");
        }

        [Test]
        public void HarvestingEarly_YieldsNothing()
        {
            var field = PlaceField(_world);
            field.TryEnqueue(TestContent.WheatCrop, _world.Barn);

            _time.Advance(TimeSpan.FromSeconds(TestContent.WheatGrowSeconds / 2));
            _world.Sync();

            Assert.That(field.CollectReady(_world.Barn), Is.EqualTo(0));
            Assert.That(_world.Barn.CountOf(TestContent.Wheat), Is.EqualTo(0));
            Assert.That(field.Orders.Count, Is.EqualTo(1), "the crop is still growing");
        }

        [Test]
        public void Harvest_MovesTheCropIntoTheBarnAndEmptiesTheField()
        {
            var field = PlaceField(_world);
            field.TryEnqueue(TestContent.WheatCrop, _world.Barn);

            GrowFully();
            _world.Sync();

            Assert.That(field.HasReadyGoods, Is.True);
            Assert.That(field.CollectReady(_world.Barn), Is.EqualTo(TestContent.WheatYield));
            Assert.That(_world.Barn.CountOf(TestContent.Wheat), Is.EqualTo(TestContent.WheatYield));
            Assert.That(field.IsIdle, Is.True, "without auto-replant the field goes back to empty");
        }

        /// <summary>
        /// The whole point of timestamp-driven growth: a crop planted before the app closed is
        /// waiting to be harvested on the next launch, with nothing simulated in between.
        /// </summary>
        [Test]
        public void ACropGrowsWhileThePlayerIsAway()
        {
            var field = PlaceField(_world);
            field.TryEnqueue(TestContent.WheatCrop, _world.Barn);

            _time.Advance(TimeSpan.FromDays(2));
            _world.Sync();

            Assert.That(field.Orders.Count, Is.EqualTo(0));
            Assert.That(field.HasReadyGoods, Is.True);
        }

        [Test]
        public void ALockedCrop_CannotBePlanted()
        {
            var field = PlaceField(_world);

            Assert.That(_world.Progression.TownLevel, Is.EqualTo(1));
            Assert.That(field.TryEnqueue(TestContent.CornCrop, _world.Barn), Is.False,
                "corn needs town level 2");
            Assert.That(field.IsIdle, Is.True);
        }

        [Test]
        public void HarvestIntoAFullBarn_LeavesTheRemainderInTheField()
        {
            var world = CreateWorld(barnCapacity: 1);
            var field = PlaceField(world);
            field.TryEnqueue(TestContent.WheatCrop, world.Barn);

            GrowFully();
            world.Sync();

            Assert.That(field.CollectReady(world.Barn), Is.EqualTo(1), "only one unit fits");
            Assert.That(world.Barn.CountOf(TestContent.Wheat), Is.EqualTo(1));
            Assert.That(field.HasReadyGoods, Is.True, "the rest waits in the field");
        }

        /// <summary>Upgrading the field building to level 2 switches its producer to auto-replant.</summary>
        [Test]
        public void AutoReplant_ResowsWhenTheFieldIsCleared()
        {
            var field = PlaceField(_world);
            Assert.That(_world.Buildings.TryUpgrade(_fieldId), Is.EqualTo(BuildingActionResult.Success));
            Assert.That(field.Level, Is.EqualTo(2));

            field.TryEnqueue(TestContent.WheatCrop, _world.Barn);
            GrowFully();
            _world.Sync();
            field.CollectReady(_world.Barn);

            Assert.That(field.Orders.Count, Is.EqualTo(1), "the field re-sows itself");
            Assert.That(field.Orders[0].RecipeId, Is.EqualTo(TestContent.WheatCrop));
        }

        /// <summary>
        /// The economy guard rail. Auto-replant triggers on collection, never on completion, so a
        /// field left alone for a fortnight banks one harvest rather than a fortnight of them.
        /// </summary>
        [Test]
        public void AutoReplant_DoesNotCycleWhileThePlayerIsAway()
        {
            var field = PlaceField(_world);
            _world.Buildings.TryUpgrade(_fieldId);
            field.TryEnqueue(TestContent.WheatCrop, _world.Barn);

            _time.Advance(TimeSpan.FromDays(14));
            _world.Sync();

            Assert.That(field.Orders.Count, Is.EqualTo(0));
            Assert.That(field.CollectReady(_world.Barn), Is.EqualTo(TestContent.WheatYield),
                "exactly one harvest, however long the absence");
            Assert.That(field.Orders.Count, Is.EqualTo(1), "and it re-sows once collected");
        }

        [Test]
        public void FieldsAreDiscoverableByCategory()
        {
            PlaceField(_world, 0, 0);
            PlaceField(_world, 1, 0);
            _world.Buildings.TryPlace(TestContent.Shed, new GridPosition(2, 0), out _);

            var fields = new List<BuildingInstance>();
            _world.Buildings.CollectByCategory(BuildingCategory.Farming, fields);

            Assert.That(fields.Count, Is.EqualTo(2));
            Assert.That(_world.Buildings.All.Count, Is.EqualTo(3));
        }

        [Test]
        public void AGrowingCrop_SurvivesASaveRoundTrip()
        {
            var field = PlaceField(_world);
            field.TryEnqueue(TestContent.WheatCrop, _world.Barn);

            var saveService = new SaveService(
                new InMemorySaveStore(), new JsonSaveSerializer(), _clock,
                GameWorld.SaveSchemaVersion, null, "tests");

            Assert.That(saveService.TrySave(GameWorld.DefaultSaveSlot, _world.CaptureSave()), Is.True);
            _time.Advance(TimeSpan.FromDays(1));
            Assert.That(saveService.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var data), Is.True);

            var restored = new GameWorld(_database, _clock, _events, new Random(9));
            restored.RestoreSave(data);

            Assert.That(restored.TryGetProducer(_fieldId, out var restoredField), Is.True);
            Assert.That(restoredField.LastRecipeId, Is.EqualTo(TestContent.WheatCrop),
                "the field remembers what it was growing");
            Assert.That(restoredField.HasReadyGoods, Is.True, "and the crop finished while away");
            Assert.That(restoredField.CollectReady(restored.Barn), Is.EqualTo(TestContent.WheatYield));
        }
    }
}
