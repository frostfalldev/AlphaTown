using System;
using AlphaTown.Core.Events;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Buildings;
using AlphaTown.Data.Economy;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    public sealed class BuildingPlacementTests
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
            _world = CreateWorld();
        }

        GameWorld CreateWorld(int startingCoins = 1000)
        {
            _database = TestContent.Build(startingCoins: startingCoins);
            var world = new GameWorld(_database, _clock, _events, new Random(42));
            world.InitialiseNewPlayer();
            return world;
        }

        static GridPosition At(int x, int y) => new GridPosition(x, y);

        [Test]
        public void Place_ChargesCoinsThroughTheWalletWithAPurchaseReason()
        {
            var result = _world.Buildings.TryPlace(TestContent.Shed, At(0, 0), out var instanceId);

            Assert.That(result, Is.EqualTo(BuildingActionResult.Success));
            Assert.That(instanceId, Is.Not.Null.And.Not.Empty);
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(1000 - TestContent.ShedCoinCost));
            Assert.That(_world.Ledger.TotalTo(TestContent.Coins, CurrencySink.BuildingPurchase),
                Is.EqualTo(TestContent.ShedCoinCost));
            Assert.That(_world.Buildings.All.Count, Is.EqualTo(1));
        }

        [Test]
        public void Place_FailsAndChargesNothingWhenCoinsAreShort()
        {
            var world = CreateWorld(startingCoins: 5);

            var result = world.Buildings.TryPlace(TestContent.Shed, At(0, 0), out _);

            Assert.That(result, Is.EqualTo(BuildingActionResult.InsufficientFunds));
            Assert.That(world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(5));
            Assert.That(world.Buildings.All.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Coins and materials are checked together before either is taken. Charging coins and
        /// then failing on planks would bill the player for a building they never got.
        /// </summary>
        [Test]
        public void Place_FailsWithoutSpendingCoinsWhenMaterialsAreShort()
        {
            var result = _world.Buildings.TryPlace(TestContent.Workshop, At(0, 0), out _);

            Assert.That(result, Is.EqualTo(BuildingActionResult.InsufficientItems));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(1000),
                "the coin half of the cost must not be taken");
            Assert.That(_world.Buildings.All.Count, Is.EqualTo(0));
        }

        [Test]
        public void Place_TakesCoinsAndMaterialsTogether()
        {
            _world.Barn.Add(TestContent.Flour, 5);

            var result = _world.Buildings.TryPlace(TestContent.Workshop, At(0, 0), out _);

            Assert.That(result, Is.EqualTo(BuildingActionResult.Success));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins),
                Is.EqualTo(1000 - TestContent.WorkshopCoinCost));
            Assert.That(_world.Barn.CountOf(TestContent.Flour),
                Is.EqualTo(5 - TestContent.WorkshopFlourCost));
        }

        [Test]
        public void Place_RejectsAnOverlappingFootprint()
        {
            _world.Buildings.TryPlace(TestContent.BakeryBuilding, At(2, 2), out _);

            var result = _world.Buildings.TryPlace(TestContent.Shed, At(3, 3), out _);

            Assert.That(result, Is.EqualTo(BuildingActionResult.Overlaps));
            Assert.That(_world.Buildings.All.Count, Is.EqualTo(1));
        }

        [Test]
        public void Place_RejectsAFootprintRunningOffTheEdge()
        {
            var result = _world.Buildings.TryPlace(TestContent.BakeryBuilding, At(7, 7), out _);

            Assert.That(result, Is.EqualTo(BuildingActionResult.OutOfBounds));
        }

        [Test]
        public void Place_RejectsBuildingsAboveTheTownLevel()
        {
            _database.TryGetBuilding(TestContent.Shed, out var definition);
            ((FakeBuildingDefinition)definition).UnlockLevel = 5;

            Assert.That(_world.Progression.TownLevel, Is.EqualTo(1));
            Assert.That(_world.Buildings.TryPlace(TestContent.Shed, At(0, 0), out _),
                Is.EqualTo(BuildingActionResult.Locked));
        }

        [Test]
        public void Place_RejectsAnUnknownDefinition()
        {
            Assert.That(_world.Buildings.TryPlace("building.nope", At(0, 0), out _),
                Is.EqualTo(BuildingActionResult.UnknownDefinition));
        }

        [Test]
        public void ValidatePlacement_ReportsTheSameAnswerWithoutCharging()
        {
            Assert.That(_world.Buildings.ValidatePlacement(TestContent.Shed, At(0, 0)),
                Is.EqualTo(BuildingActionResult.Success));
            Assert.That(_world.Buildings.ValidatePlacement(TestContent.Workshop, At(0, 0)),
                Is.EqualTo(BuildingActionResult.InsufficientItems));

            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(1000));
            Assert.That(_world.Buildings.All.Count, Is.EqualTo(0));
        }

        [Test]
        public void Move_FreesTheOldCellsAndClaimsTheNew()
        {
            _world.Buildings.TryPlace(TestContent.Shed, At(0, 0), out var instanceId);

            var result = _world.Buildings.TryMove(instanceId, At(5, 5));

            Assert.That(result, Is.EqualTo(BuildingActionResult.Success));
            Assert.That(_world.Buildings.Grid.TryGetOccupant(At(0, 0), out _), Is.False);
            Assert.That(_world.Buildings.Grid.TryGetOccupant(At(5, 5), out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(instanceId));
        }

        /// <summary>A one-cell nudge overlaps the building's own footprint, and must still work.</summary>
        [Test]
        public void Move_AllowsShiftingOntoItsOwnCells()
        {
            _world.Buildings.TryPlace(TestContent.BakeryBuilding, At(0, 0), out var instanceId);

            Assert.That(_world.Buildings.TryMove(instanceId, At(1, 1)),
                Is.EqualTo(BuildingActionResult.Success));
            Assert.That(_world.Buildings.Grid.TryGetOccupant(At(0, 0), out _), Is.False);
            Assert.That(_world.Buildings.Grid.TryGetOccupant(At(2, 2), out _), Is.True);
        }

        [Test]
        public void Move_RejectsAnOccupiedDestination()
        {
            _world.Buildings.TryPlace(TestContent.Shed, At(0, 0), out var first);
            _world.Buildings.TryPlace(TestContent.Shed, At(4, 4), out _);

            Assert.That(_world.Buildings.TryMove(first, At(4, 4)),
                Is.EqualTo(BuildingActionResult.Overlaps));
            Assert.That(_world.Buildings.Grid.TryGetOccupant(At(0, 0), out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(first), "a rejected move must leave the building put");
        }

        [Test]
        public void Remove_FreesTheCellsAndDropsTheProducer()
        {
            _world.Buildings.TryPlace(TestContent.BakeryBuilding, At(0, 0), out var instanceId);
            _time.Advance(TimeSpan.FromMinutes(5));
            _world.Sync();
            Assert.That(_world.TryGetProducer(instanceId, out _), Is.True);

            Assert.That(_world.Buildings.TryRemove(instanceId), Is.EqualTo(BuildingActionResult.Success));

            Assert.That(_world.Buildings.All.Count, Is.EqualTo(0));
            Assert.That(_world.Buildings.Grid.TryGetOccupant(At(0, 0), out _), Is.False);
            Assert.That(_world.TryGetProducer(instanceId, out _), Is.False);
        }

        [Test]
        public void Remove_ReportsAnUnknownBuilding()
        {
            Assert.That(_world.Buildings.TryRemove("building_999"),
                Is.EqualTo(BuildingActionResult.BuildingNotFound));
        }

        [Test]
        public void Place_PublishesAPlacedEvent()
        {
            var placed = 0;
            using (_events.Subscribe<BuildingPlacedEvent>(e =>
                   {
                       placed++;
                       Assert.That(e.DefinitionId, Is.EqualTo(TestContent.Shed));
                       Assert.That(e.Origin, Is.EqualTo(At(3, 4)));
                   }))
            {
                _world.Buildings.TryPlace(TestContent.Shed, At(3, 4), out _);
            }

            Assert.That(placed, Is.EqualTo(1));
        }
    }
}
