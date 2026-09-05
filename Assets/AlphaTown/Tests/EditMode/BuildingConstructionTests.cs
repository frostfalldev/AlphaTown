using System;
using AlphaTown.Core.Events;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Progression;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    public sealed class BuildingConstructionTests
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
            _world = new GameWorld(_database, _clock, _events, new Random(42));
            _world.InitialiseNewPlayer();
        }

        static GridPosition At(int x, int y) => new GridPosition(x, y);

        string PlaceBakery(int x = 0, int y = 0)
        {
            var result = _world.Buildings.TryPlace(TestContent.BakeryBuilding, At(x, y), out var instanceId);
            Assert.That(result, Is.EqualTo(BuildingActionResult.Success));
            return instanceId;
        }

        BuildingInstance Building(string instanceId)
        {
            Assert.That(_world.Buildings.TryGetBuilding(instanceId, out var building), Is.True);
            return building;
        }

        [Test]
        public void ZeroSecondBuild_IsOperationalImmediately()
        {
            _world.Buildings.TryPlace(TestContent.Shed, At(0, 0), out var instanceId);

            var shed = Building(instanceId);
            Assert.That(shed.State, Is.EqualTo(BuildingState.Operational));
            Assert.That(shed.Level, Is.EqualTo(1));
            Assert.That(shed.IsBusy, Is.False);
        }

        [Test]
        public void TimedBuild_StaysUnderConstructionUntilItsTimestamp()
        {
            var instanceId = PlaceBakery();

            var bakery = Building(instanceId);
            Assert.That(bakery.State, Is.EqualTo(BuildingState.UnderConstruction));
            Assert.That(bakery.Level, Is.EqualTo(0));

            _time.Advance(TimeSpan.FromSeconds(TestContent.BakeryLevel1BuildSeconds - 10));
            _world.Sync();
            Assert.That(bakery.State, Is.EqualTo(BuildingState.UnderConstruction));

            _time.Advance(TimeSpan.FromSeconds(20));
            _world.Sync();
            Assert.That(bakery.State, Is.EqualTo(BuildingState.Operational));
            Assert.That(bakery.Level, Is.EqualTo(1));
        }

        /// <summary>
        /// The point of absolute timestamps: a build started before the app closed is simply
        /// finished on the next sync, with nothing simulated in between.
        /// </summary>
        [Test]
        public void BuildStartedBeforeAnAbsence_CompletesOnTheNextSync()
        {
            var instanceId = PlaceBakery();

            _time.Advance(TimeSpan.FromDays(10));
            _world.Sync();

            Assert.That(Building(instanceId).State, Is.EqualTo(BuildingState.Operational));
        }

        [Test]
        public void FinishedConstruction_AttachesAProducerAtTheBuildingLevel()
        {
            var instanceId = PlaceBakery();
            Assert.That(_world.TryGetProducer(instanceId, out _), Is.False,
                "an unfinished building runs nothing");

            _time.Advance(TimeSpan.FromMinutes(5));
            _world.Sync();

            Assert.That(_world.TryGetProducer(instanceId, out var producer), Is.True);
            Assert.That(producer.DefinitionId, Is.EqualTo(TestContent.Bakery));
            Assert.That(producer.Level, Is.EqualTo(1));
        }

        [Test]
        public void ConstructionCompleted_PublishesAnEvent()
        {
            var completed = 0;
            using (_events.Subscribe<BuildingConstructionCompletedEvent>(e =>
                   {
                       completed++;
                       Assert.That(e.WasInitialBuild, Is.True);
                       Assert.That(e.Level, Is.EqualTo(1));
                   }))
            {
                PlaceBakery();
                _time.Advance(TimeSpan.FromMinutes(5));
                _world.Sync();
            }

            Assert.That(completed, Is.EqualTo(1));
        }

        /// <summary>
        /// An upgrade is a decision, not a shutdown: the building keeps running at its current
        /// level for the whole timer.
        /// </summary>
        [Test]
        public void Upgrade_KeepsTheBuildingRunningWhileItIsTimed()
        {
            var instanceId = PlaceBakery();
            _time.Advance(TimeSpan.FromMinutes(5));
            _world.Sync();

            Assert.That(_world.Buildings.TryUpgrade(instanceId), Is.EqualTo(BuildingActionResult.Success));

            var bakery = Building(instanceId);
            Assert.That(bakery.State, Is.EqualTo(BuildingState.Upgrading));
            Assert.That(bakery.Level, Is.EqualTo(1), "it still runs at level 1 while upgrading");
            Assert.That(bakery.TargetLevel, Is.EqualTo(2));
            Assert.That(_world.TryGetProducer(instanceId, out var producer), Is.True);
            Assert.That(producer.Level, Is.EqualTo(1));

            _time.Advance(TimeSpan.FromMinutes(5));
            _world.Sync();

            Assert.That(bakery.State, Is.EqualTo(BuildingState.Operational));
            Assert.That(bakery.Level, Is.EqualTo(2));
            Assert.That(producer.Level, Is.EqualTo(2), "producer level tracks building level");
        }

        [Test]
        public void Upgrade_ChargesTheUpgradeSinkSeparatelyFromThePurchaseSink()
        {
            var instanceId = PlaceBakery();
            _time.Advance(TimeSpan.FromMinutes(5));
            _world.Sync();

            _world.Buildings.TryUpgrade(instanceId);

            Assert.That(_world.Ledger.TotalTo(TestContent.Coins, CurrencySink.BuildingPurchase),
                Is.EqualTo(TestContent.BakeryLevel1CoinCost));
            Assert.That(_world.Ledger.TotalTo(TestContent.Coins, CurrencySink.BuildingUpgrade),
                Is.EqualTo(TestContent.BakeryLevel2CoinCost));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins),
                Is.EqualTo(1000 - TestContent.BakeryLevel1CoinCost - TestContent.BakeryLevel2CoinCost));
        }

        [Test]
        public void Upgrade_RejectedWhileAlreadyBuilding()
        {
            var instanceId = PlaceBakery();

            Assert.That(_world.Buildings.TryUpgrade(instanceId), Is.EqualTo(BuildingActionResult.BuildingBusy));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins),
                Is.EqualTo(1000 - TestContent.BakeryLevel1CoinCost));
        }

        [Test]
        public void Upgrade_RejectedWhenCoinsAreShort()
        {
            var database = TestContent.Build(startingCoins: TestContent.BakeryLevel1CoinCost);
            var world = new GameWorld(database, _clock, _events, new Random(1));
            world.InitialiseNewPlayer();
            world.Buildings.TryPlace(TestContent.BakeryBuilding, At(0, 0), out var instanceId);
            _time.Advance(TimeSpan.FromMinutes(5));
            world.Sync();

            Assert.That(world.Buildings.TryUpgrade(instanceId),
                Is.EqualTo(BuildingActionResult.InsufficientFunds));
            Assert.That(world.Buildings.TryGetBuilding(instanceId, out var bakery), Is.True);
            Assert.That(bakery.Level, Is.EqualTo(1));
            Assert.That(bakery.IsBusy, Is.False);
        }

        [Test]
        public void Upgrade_RejectedAtTheLastLevelWithNowhereToGo()
        {
            _world.Buildings.TryPlace(TestContent.Shed, At(0, 0), out var instanceId);

            Assert.That(_world.Buildings.TryUpgrade(instanceId),
                Is.EqualTo(BuildingActionResult.AlreadyMaxLevel));
        }

        /// <summary>
        /// A hut upgrades by becoming a villa rather than gaining a level: new definition, new
        /// footprint, back to level 1.
        /// </summary>
        [Test]
        public void Upgrade_TransformsIntoTheNextDefinitionAndClaimsItsFootprint()
        {
            _world.Buildings.TryPlace(TestContent.Hut, At(0, 0), out var instanceId);

            Assert.That(_world.Buildings.TryUpgrade(instanceId), Is.EqualTo(BuildingActionResult.Success));

            var villa = Building(instanceId);
            Assert.That(villa.DefinitionId, Is.EqualTo(TestContent.Villa));
            Assert.That(villa.Level, Is.EqualTo(1));
            Assert.That(villa.State, Is.EqualTo(BuildingState.Operational));
            Assert.That(villa.Footprint.Size, Is.EqualTo(new GridSize(2, 2)));

            Assert.That(_world.Buildings.Grid.TryGetOccupant(At(1, 1), out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(instanceId));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(1000 - 50 - 150));
        }

        [Test]
        public void Upgrade_RejectedWhenTheReplacementFootprintDoesNotFit()
        {
            _world.Buildings.TryPlace(TestContent.Hut, At(7, 7), out var instanceId);

            Assert.That(_world.Buildings.TryUpgrade(instanceId), Is.EqualTo(BuildingActionResult.OutOfBounds));

            var hut = Building(instanceId);
            Assert.That(hut.DefinitionId, Is.EqualTo(TestContent.Hut));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(1000 - 50),
                "a rejected transform must not charge the replacement's cost");
        }

        [Test]
        public void Progress_RunsFromZeroToOneAcrossTheBuild()
        {
            var instanceId = PlaceBakery();
            var bakery = Building(instanceId);

            Assert.That(bakery.Progress(_clock.UtcNowTicks), Is.EqualTo(0f).Within(0.001f));

            _time.Advance(TimeSpan.FromSeconds(TestContent.BakeryLevel1BuildSeconds / 2));
            Assert.That(bakery.Progress(_clock.UtcNowTicks), Is.EqualTo(0.5f).Within(0.01f));

            _time.Advance(TimeSpan.FromSeconds(TestContent.BakeryLevel1BuildSeconds));
            Assert.That(bakery.Progress(_clock.UtcNowTicks), Is.EqualTo(1f).Within(0.001f));
        }

        /// <summary>
        /// XP is paid when the building stands, not when it is bought. A decoration produces
        /// nothing and stores nothing, so this reward is the entire reason to raise one.
        /// </summary>
        [Test]
        public void ConstructionPaysItsXpOnCompletion()
        {
            var before = _world.Progression.TotalXp;

            Assert.That(_world.Buildings.TryPlace(TestContent.Statue, GridPosition.Zero, out _),
                Is.EqualTo(BuildingActionResult.Success));

            // Still under scaffolding: nothing has been earned yet.
            Assert.That(_world.Progression.TotalXp, Is.EqualTo(before));

            _time.Advance(TimeSpan.FromSeconds(TestContent.StatueBuildSeconds));
            _world.Sync();

            Assert.That(_world.Progression.TotalXp - before, Is.EqualTo(TestContent.StatueXpReward));
            Assert.That(_world.Progression.TotalXpFrom(XpSource.BuildingConstructed),
                Is.EqualTo((long)TestContent.StatueXpReward));
        }

        /// <summary>A build that finished while the app was closed still pays on the next sync.</summary>
        [Test]
        public void ConstructionThatFinishedOfflinePaysToo()
        {
            _world.Buildings.TryPlace(TestContent.Statue, GridPosition.Zero, out _);

            _time.Advance(TimeSpan.FromDays(3));
            _world.Sync();

            Assert.That(_world.Progression.TotalXpFrom(XpSource.BuildingConstructed),
                Is.EqualTo((long)TestContent.StatueXpReward));
        }

        [Test]
        public void ABuildingWithNoXpRewardPaysNothing()
        {
            var before = _world.Progression.TotalXp;

            _world.Buildings.TryPlace(TestContent.Shed, new GridPosition(3, 3), out _);
            _world.Sync();

            Assert.That(_world.Progression.TotalXp, Is.EqualTo(before));
        }
    }
}
