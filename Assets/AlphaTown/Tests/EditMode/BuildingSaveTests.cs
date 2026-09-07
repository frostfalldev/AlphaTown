using System;
using AlphaTown.Core.Events;
using AlphaTown.Core.Spatial;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.Saving;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Save;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    public sealed class BuildingSaveTests
    {
        ManualTimeSource _time;
        GameClock _clock;
        EventBus _events;
        FakeDatabase _database;
        ISaveService _saveService;
        GameWorld _world;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeSource();
            _clock = new GameClock(_time);
            _events = new EventBus();
            _database = TestContent.Build(startingCoins: 1000);

            _saveService = new SaveService(
                new InMemorySaveStore(), new JsonSaveSerializer(), _clock,
                GameWorld.SaveSchemaVersion, null, "tests");

            _world = new GameWorld(_database, _clock, _events, new Random(42));
            _world.InitialiseNewPlayer();
        }

        static GridPosition At(int x, int y) => new GridPosition(x, y);

        GameWorld SaveAndReload()
        {
            Assert.That(_saveService.TrySave(GameWorld.DefaultSaveSlot, _world.CaptureSave()), Is.True);
            Assert.That(_saveService.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var data), Is.True);

            var restored = new GameWorld(_database, _clock, _events, new Random(5));
            restored.RestoreSave(data);
            return restored;
        }

        [Test]
        public void PlacedBuildings_SurviveARoundTrip()
        {
            _world.Buildings.TryPlace(TestContent.Shed, At(2, 3), out var shedId);
            _world.Buildings.TryPlace(TestContent.BakeryBuilding, At(0, 0), out var bakeryId);
            _time.Advance(TimeSpan.FromMinutes(5));
            _world.Sync();

            var restored = SaveAndReload();

            Assert.That(restored.Buildings.All.Count, Is.EqualTo(2));

            Assert.That(restored.Buildings.TryGetBuilding(shedId, out var shed), Is.True);
            Assert.That(shed.Origin, Is.EqualTo(At(2, 3)));
            Assert.That(shed.DefinitionId, Is.EqualTo(TestContent.Shed));

            Assert.That(restored.Buildings.TryGetBuilding(bakeryId, out var bakery), Is.True);
            Assert.That(bakery.Level, Is.EqualTo(1));
            Assert.That(bakery.State, Is.EqualTo(BuildingState.Operational));
        }

        [Test]
        public void RestoredBuildings_ReclaimTheirGridCells()
        {
            _world.Buildings.TryPlace(TestContent.BakeryBuilding, At(4, 4), out var bakeryId);

            var restored = SaveAndReload();

            Assert.That(restored.Buildings.Grid.TryGetOccupant(At(5, 5), out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(bakeryId));
            Assert.That(restored.Buildings.TryPlace(TestContent.Shed, At(4, 4), out _),
                Is.EqualTo(BuildingActionResult.Overlaps));
        }

        /// <summary>
        /// The construction equivalent of offline production: a build in progress when the app
        /// closed is finished by the sync that follows the load.
        /// </summary>
        [Test]
        public void ConstructionInProgress_CompletesWhileThePlayerIsAway()
        {
            _world.Buildings.TryPlace(TestContent.BakeryBuilding, At(0, 0), out var bakeryId);
            Assert.That(_world.Buildings.All[0].State, Is.EqualTo(BuildingState.UnderConstruction));

            Assert.That(_saveService.TrySave(GameWorld.DefaultSaveSlot, _world.CaptureSave()), Is.True);
            _time.Advance(TimeSpan.FromDays(1));
            _saveService.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var data);

            var restored = new GameWorld(_database, _clock, _events, new Random(5));
            restored.RestoreSave(data);

            Assert.That(restored.Buildings.TryGetBuilding(bakeryId, out var bakery), Is.True);
            Assert.That(bakery.State, Is.EqualTo(BuildingState.Operational));
            Assert.That(bakery.Level, Is.EqualTo(1));
            Assert.That(restored.TryGetProducer(bakeryId, out _), Is.True,
                "finishing offline still attaches the producer");
        }

        [Test]
        public void UpgradeInProgress_SurvivesAndResumes()
        {
            _world.Buildings.TryPlace(TestContent.BakeryBuilding, At(0, 0), out var bakeryId);
            _time.Advance(TimeSpan.FromMinutes(5));
            _world.Sync();
            _world.Buildings.TryUpgrade(bakeryId);

            Assert.That(_saveService.TrySave(GameWorld.DefaultSaveSlot, _world.CaptureSave()), Is.True);
            _saveService.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var data);

            var restored = new GameWorld(_database, _clock, _events, new Random(5));
            restored.RestoreSave(data);

            Assert.That(restored.Buildings.TryGetBuilding(bakeryId, out var bakery), Is.True);
            Assert.That(bakery.State, Is.EqualTo(BuildingState.Upgrading));
            Assert.That(bakery.Level, Is.EqualTo(1));
            Assert.That(bakery.TargetLevel, Is.EqualTo(2));

            _time.Advance(TimeSpan.FromMinutes(5));
            restored.Sync();

            Assert.That(bakery.Level, Is.EqualTo(2));
            Assert.That(restored.TryGetProducer(bakeryId, out var producer), Is.True);
            Assert.That(producer.Level, Is.EqualTo(2));
        }

        [Test]
        public void InstanceNumbering_ContinuesAfterALoad()
        {
            _world.Buildings.TryPlace(TestContent.Shed, At(0, 0), out var first);

            var restored = SaveAndReload();
            restored.Buildings.TryPlace(TestContent.Shed, At(1, 0), out var second);

            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(restored.Buildings.All.Count, Is.EqualTo(2));
        }

        [Test]
        public void SpentCoins_StaySpentAcrossALoad()
        {
            _world.Buildings.TryPlace(TestContent.Shed, At(0, 0), out _);
            var balance = _world.Wallet.BalanceOf(TestContent.Coins);

            var restored = SaveAndReload();

            Assert.That(restored.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(balance));
            Assert.That(restored.Ledger.TotalTo(TestContent.Coins,
                Data.Economy.CurrencySink.BuildingPurchase), Is.EqualTo(TestContent.ShedCoinCost));
        }
    }
}
