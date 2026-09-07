using System;
using System.Collections.Generic;
using AlphaTown.Core.Events;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using AlphaTown.Data.Progression;
using AlphaTown.Data.Town;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.Commands;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// The layer the UI talks to. These tests are the contract that says the screens cannot reach
    /// a state the simulation would refuse: every command goes through the same rules a direct
    /// call would, and every refusal comes back with something a player can read.
    /// </summary>
    public sealed class TownCommandsTests
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
            _world = new GameWorld(_database, _clock, _events, new Random(11));
            _world.InitialiseNewPlayer();
            _commands = new TownCommands(_world, _database, _clock);
        }

        string PlaceField(int x = 0, int y = 0)
        {
            var result = _world.Buildings.TryPlace(
                TestContent.FieldBuilding, new GridPosition(x, y), out var instanceId);

            Assert.That(result, Is.EqualTo(BuildingActionResult.Success));
            return instanceId;
        }

        [Test]
        public void PlantingAFieldStartsItsCrop()
        {
            var field = PlaceField();

            Assert.That(_commands.Plant(field).Success, Is.True);
            Assert.That(_world.TryGetProducer(field, out var producer), Is.True);
            Assert.That(producer.TryGetActiveOrder(out _), Is.True);
        }

        [Test]
        public void PlantingATileWithNothingOnItFails()
        {
            var result = _commands.Plant("building_does_not_exist");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.Not.Empty);
        }

        [Test]
        public void HarvestingBeforeTheCropIsReadyFails()
        {
            var field = PlaceField();
            _commands.Plant(field);

            var result = _commands.Harvest(field);

            Assert.That(result.Success, Is.False);
            Assert.That(_world.Barn.CountOf(TestContent.Wheat), Is.Zero);
        }

        [Test]
        public void HarvestingAReadyFieldFillsTheBarn()
        {
            var field = PlaceField();
            _commands.Plant(field);
            _time.Advance(TimeSpan.FromSeconds(TestContent.WheatGrowSeconds));
            _world.Sync();

            Assert.That(_commands.Harvest(field).Success, Is.True);
            Assert.That(_world.Barn.CountOf(TestContent.Wheat), Is.EqualTo(TestContent.WheatYield));
        }

        /// <summary>
        /// Harvest XP comes from the items collected, so a crop is worth the same whether it is
        /// harvested or delivered — one number prices it everywhere.
        /// </summary>
        [Test]
        public void HarvestingPaysXpForWhatWasCollected()
        {
            var field = PlaceField();
            _commands.Plant(field);
            _time.Advance(TimeSpan.FromSeconds(TestContent.WheatGrowSeconds));
            _world.Sync();

            var before = _world.Progression.TotalXp;
            _commands.Harvest(field);

            Assert.That(_world.Progression.TotalXp, Is.GreaterThan(before));
            Assert.That(_world.Progression.TotalXpFrom(XpSource.ProductionCollected), Is.GreaterThan(0L));
        }

        /// <summary>
        /// The sickle's entry point. It works from a cell rather than an instance id, and says
        /// nothing at all about tiles that were not ready — a swipe crosses plenty of those.
        /// </summary>
        [Test]
        public void HarvestAtCollectsTheFieldOnThatCell()
        {
            var field = PlaceField(2, 3);
            _commands.Plant(field);
            _time.Advance(TimeSpan.FromSeconds(TestContent.WheatGrowSeconds));
            _world.Sync();

            Assert.That(_commands.HarvestAt(new GridPosition(2, 3)), Is.True);
            Assert.That(_world.Barn.CountOf(TestContent.Wheat), Is.EqualTo(TestContent.WheatYield));
        }

        [Test]
        public void HarvestAtAnEmptyCellDoesNothing()
        {
            Assert.That(_commands.HarvestAt(new GridPosition(6, 6)), Is.False);
        }

        [Test]
        public void HarvestAtAnUnripeFieldDoesNothing()
        {
            var field = PlaceField(1, 1);
            _commands.Plant(field);

            Assert.That(_commands.HarvestAt(new GridPosition(1, 1)), Is.False);
        }

        [Test]
        public void CollectHarvestableListsOnlyFieldsWithSomethingWaiting()
        {
            var ripe = PlaceField(0, 0);
            PlaceField(1, 0);

            _commands.Plant(ripe);
            _time.Advance(TimeSpan.FromSeconds(TestContent.WheatGrowSeconds));
            _world.Sync();

            var results = new List<BuildingInstance>();
            _commands.CollectHarvestable(results);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].InstanceId, Is.EqualTo(ripe));
        }

        [Test]
        public void BuildingWithoutTheCoinsFailsWithAReadableReason()
        {
            _world.Wallet.ResetTo(Array.Empty<CurrencyAmount>());

            var result = _commands.Build(TestContent.BakeryBuilding, new GridPosition(0, 0));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("Not enough coins."));
        }

        [Test]
        public void DeliveringAnOrderTheBarnCannotCoverFails()
        {
            var order = _world.HelicopterOrders.Orders[0];
            var result = _commands.Deliver(order.OrderId);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("You are missing goods."));
        }

        [Test]
        public void DeliveringAnOrderThatDoesNotExistFails()
        {
            Assert.That(_commands.Deliver("order_nope").Success, Is.False);
        }

        /// <summary>
        /// The one-tap replant. With nothing named, the field re-sows what it grew last so long as
        /// that is still legal, which is what makes tapping a harvested field twice work.
        /// </summary>
        [Test]
        public void DefaultRecipePrefersTheLastOneGrownHere()
        {
            // Corn needs town level 2, so the player is levelled up before it can be preferred.
            _world.Progression.GrantXp(1000, XpSource.DebugGrant);

            Assert.That(_commands.DefaultRecipeFor(TestContent.Field, TestContent.CornCrop),
                Is.EqualTo(TestContent.CornCrop));
        }

        [Test]
        public void DefaultRecipeSkipsWhatIsNotUnlockedYet()
        {
            // Corn needs a higher town level than a new player has, so the field falls back to
            // wheat rather than offering a plant button that cannot work.
            var chosen = _commands.DefaultRecipeFor(TestContent.Field, TestContent.CornCrop);

            Assert.That(chosen, Is.EqualTo(TestContent.WheatCrop));
        }
    }

    /// <summary>
    /// What a brand-new player wakes up owning. Content, not code — but content that fails loudly
    /// rather than silently producing a town with nothing in it.
    /// </summary>
    public sealed class NewGameSeedingTests
    {
        [Test]
        public void StartingItemsAndBuildingsArePlaced()
        {
            var database = TestContent.Build(startingCoins: 500, includeFarming: true);
            database.NewGame = new FakeNewGameDefinition(
                startingBarnLevel: 2,
                items: new[] { new ItemStack(TestContent.Flour, 3) },
                buildings: new[]
                {
                    new StartingBuilding(TestContent.FieldBuilding, new GridPosition(1, 1)),
                    new StartingBuilding(TestContent.FieldBuilding, new GridPosition(2, 1))
                });

            var world = new GameWorld(database, new GameClock(new ManualTimeSource()), new EventBus());
            world.InitialiseNewPlayer();

            Assert.That(world.Barn.Level, Is.EqualTo(2));
            Assert.That(world.Barn.CountOf(TestContent.Flour), Is.EqualTo(3));
            Assert.That(world.Buildings.All.Count, Is.EqualTo(2));
        }

        /// <summary>
        /// A granted building is standing on day one, not behind a timer the player never chose.
        /// </summary>
        [Test]
        public void StartingBuildingsAreAlreadyBuilt()
        {
            var database = TestContent.Build(includeFarming: true);
            database.NewGame = new FakeNewGameDefinition(buildings: new[]
            {
                new StartingBuilding(TestContent.BakeryBuilding, new GridPosition(0, 0))
            });

            var world = new GameWorld(database, new GameClock(new ManualTimeSource()), new EventBus());
            world.InitialiseNewPlayer();

            var building = world.Buildings.All[0];
            Assert.That(building.IsBusy, Is.False);
            Assert.That(building.Level, Is.EqualTo(1));
        }

        /// <summary>Granting is not a purchase, so it must not touch the wallet or the ledger.</summary>
        [Test]
        public void GrantedBuildingsCostNothing()
        {
            var database = TestContent.Build(startingCoins: 500, includeFarming: true);
            database.NewGame = new FakeNewGameDefinition(buildings: new[]
            {
                new StartingBuilding(TestContent.BakeryBuilding, new GridPosition(0, 0))
            });

            var world = new GameWorld(database, new GameClock(new ManualTimeSource()), new EventBus());
            world.InitialiseNewPlayer();

            Assert.That(world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(500));
        }

        /// <summary>
        /// A starting layout that does not fit is a content bug. It must not take the rest of the
        /// town down with it.
        /// </summary>
        [Test]
        public void OverlappingStartingBuildingsAreSkippedNotFatal()
        {
            var database = TestContent.Build(includeFarming: true);
            database.NewGame = new FakeNewGameDefinition(buildings: new[]
            {
                new StartingBuilding(TestContent.BakeryBuilding, new GridPosition(0, 0)),
                new StartingBuilding(TestContent.BakeryBuilding, new GridPosition(0, 0))
            });

            var world = new GameWorld(database, new GameClock(new ManualTimeSource()), new EventBus());

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            world.InitialiseNewPlayer();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            Assert.That(world.Buildings.All.Count, Is.EqualTo(1));
        }

        [Test]
        public void NoNewGameDefinitionStillProducesAWorkingTown()
        {
            var database = TestContent.Build(startingCoins: 100);
            var world = new GameWorld(database, new GameClock(new ManualTimeSource()), new EventBus());

            world.InitialiseNewPlayer();

            Assert.That(world.Buildings.All, Is.Empty);
            Assert.That(world.HelicopterOrders.Orders, Is.Not.Empty);
        }
    }
}
