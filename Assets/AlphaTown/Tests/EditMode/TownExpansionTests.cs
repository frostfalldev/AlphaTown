using System;
using System.Collections.Generic;
using AlphaTown.Core.Events;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Expansion;
using AlphaTown.Data.Orders;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.Expansion;
using AlphaTown.Gameplay.Saving;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Save;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// The town is 8x8 and starts owning the bottom-left 4x4. East and North grow straight off it;
    /// Northeast needs East first.
    /// </summary>
    public sealed class TownExpansionTests
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

        GameWorld CreateWorld(int startingCoins = 1000, IOrderTemplateDefinition template = null)
        {
            _database = TestContent.Build(
                startingCoins: startingCoins, orderTemplate: template, includeExpansion: true);

            var world = new GameWorld(_database, _clock, _events, new Random(4));
            world.InitialiseNewPlayer();
            return world;
        }

        static GridPosition At(int x, int y) => new GridPosition(x, y);

        void GiveDeeds(GameWorld world, int count) => world.Barn.Add(TestContent.Deed, count);

        GameWorld SaveAndReload(GameWorld world)
        {
            var saveService = new SaveService(
                new InMemorySaveStore(), new JsonSaveSerializer(), _clock,
                GameWorld.SaveSchemaVersion, null, "tests");

            Assert.That(saveService.TrySave(GameWorld.DefaultSaveSlot, world.CaptureSave()), Is.True);
            Assert.That(saveService.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var data), Is.True);

            var restored = new GameWorld(_database, _clock, _events, new Random(8));
            restored.RestoreSave(data);
            return restored;
        }

        // --- Starting state -----------------------------------------------------------------

        [Test]
        public void ANewTown_OwnsOnlyItsStartingArea()
        {
            Assert.That(_world.Buildings.Grid.UnlockedCellCount,
                Is.EqualTo(TestContent.StartingAreaSize * TestContent.StartingAreaSize));
            Assert.That(_world.Buildings.Grid.IsUnlocked(At(0, 0)), Is.True);
            Assert.That(_world.Buildings.Grid.IsUnlocked(At(4, 0)), Is.False);
            Assert.That(_world.Expansion.OwnedCount, Is.EqualTo(0));
        }

        [Test]
        public void BuildingInsideTheStartingArea_IsAllowed()
        {
            Assert.That(_world.Buildings.TryPlace(TestContent.Shed, At(1, 1), out _),
                Is.EqualTo(BuildingActionResult.Success));
        }

        [Test]
        public void BuildingOnLandTheTownDoesNotOwn_IsRejected()
        {
            Assert.That(_world.Buildings.TryPlace(TestContent.Shed, At(5, 0), out _),
                Is.EqualTo(BuildingActionResult.AreaLocked));
            Assert.That(_world.Buildings.All.Count, Is.EqualTo(0));
        }

        [Test]
        public void MovingABuildingOntoLandTheTownDoesNotOwn_IsRejected()
        {
            _world.Buildings.TryPlace(TestContent.Shed, At(0, 0), out var instanceId);

            Assert.That(_world.Buildings.TryMove(instanceId, At(6, 6)),
                Is.EqualTo(BuildingActionResult.AreaLocked));
            Assert.That(_world.Buildings.Grid.TryGetOccupant(At(0, 0), out _), Is.True);
        }

        // --- Buying land --------------------------------------------------------------------

        [Test]
        public void Unlocking_FailsWithoutDeeds()
        {
            Assert.That(_world.Expansion.TryUnlock(TestContent.ExpansionEast),
                Is.EqualTo(ExpansionResult.InsufficientItems));
            Assert.That(_world.Buildings.Grid.IsUnlocked(At(4, 0)), Is.False);
        }

        [Test]
        public void Unlocking_SpendsDeedsAndOpensTheLand()
        {
            GiveDeeds(_world, TestContent.EastDeedCost);

            Assert.That(_world.Expansion.TryUnlock(TestContent.ExpansionEast),
                Is.EqualTo(ExpansionResult.Success));

            Assert.That(_world.Barn.CountOf(TestContent.Deed), Is.EqualTo(0));
            Assert.That(_world.Expansion.IsUnlocked(TestContent.ExpansionEast), Is.True);
            Assert.That(_world.Buildings.Grid.IsUnlocked(At(4, 0)), Is.True);
            Assert.That(_world.Buildings.Grid.UnlockedCellCount, Is.EqualTo(32));
        }

        [Test]
        public void BoughtLand_CanBeBuiltOn()
        {
            GiveDeeds(_world, TestContent.EastDeedCost);
            _world.Expansion.TryUnlock(TestContent.ExpansionEast);

            Assert.That(_world.Buildings.TryPlace(TestContent.Shed, At(5, 0), out _),
                Is.EqualTo(BuildingActionResult.Success));
        }

        [Test]
        public void Unlocking_PublishesAnEvent()
        {
            GiveDeeds(_world, TestContent.EastDeedCost);

            var unlocked = 0;
            using (_events.Subscribe<TownExpandedEvent>(e =>
                   {
                       unlocked++;
                       Assert.That(e.ExpansionId, Is.EqualTo(TestContent.ExpansionEast));
                       Assert.That(e.Region.Origin, Is.EqualTo(At(4, 0)));
                   }))
            {
                _world.Expansion.TryUnlock(TestContent.ExpansionEast);
            }

            Assert.That(unlocked, Is.EqualTo(1));
        }

        [Test]
        public void BuyingTheSameLandTwice_IsRejectedAndCostsNothing()
        {
            GiveDeeds(_world, TestContent.EastDeedCost + 5);
            _world.Expansion.TryUnlock(TestContent.ExpansionEast);
            var deedsLeft = _world.Barn.CountOf(TestContent.Deed);

            Assert.That(_world.Expansion.TryUnlock(TestContent.ExpansionEast),
                Is.EqualTo(ExpansionResult.AlreadyUnlocked));
            Assert.That(_world.Barn.CountOf(TestContent.Deed), Is.EqualTo(deedsLeft));
        }

        // --- Order and gating ---------------------------------------------------------------

        /// <summary>Land spreads outward: the far corner cannot be bought before its neighbour.</summary>
        [Test]
        public void APlotBehindAnotherPlot_NeedsThatOneFirst()
        {
            GiveDeeds(_world, 20);

            Assert.That(_world.Expansion.TryUnlock(TestContent.ExpansionNortheast),
                Is.EqualTo(ExpansionResult.PrerequisiteNotMet));

            Assert.That(_world.Expansion.TryUnlock(TestContent.ExpansionEast),
                Is.EqualTo(ExpansionResult.Success));
            Assert.That(_world.Expansion.TryUnlock(TestContent.ExpansionNortheast),
                Is.EqualTo(ExpansionResult.Success));
            Assert.That(_world.Buildings.Grid.UnlockedCellCount, Is.EqualTo(48));
        }

        [Test]
        public void AnOptionalCoinCost_GoesThroughTheExpansionSink()
        {
            GiveDeeds(_world, 20);
            _world.Expansion.TryUnlock(TestContent.ExpansionEast);
            _world.Expansion.TryUnlock(TestContent.ExpansionNortheast);

            Assert.That(_world.Ledger.TotalTo(TestContent.Coins, CurrencySink.ExpansionPurchase),
                Is.EqualTo(TestContent.NortheastCoinCost));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins),
                Is.EqualTo(1000 - TestContent.NortheastCoinCost));
        }

        /// <summary>Deeds and coins are checked together, so a coin shortfall keeps the deeds.</summary>
        [Test]
        public void MissingCoins_LeaveTheDeedsUnspent()
        {
            var world = CreateWorld(startingCoins: 100);
            GiveDeeds(world, 20);
            world.Expansion.TryUnlock(TestContent.ExpansionEast);
            var deedsLeft = world.Barn.CountOf(TestContent.Deed);

            Assert.That(world.Expansion.TryUnlock(TestContent.ExpansionNortheast),
                Is.EqualTo(ExpansionResult.InsufficientFunds));
            Assert.That(world.Barn.CountOf(TestContent.Deed), Is.EqualTo(deedsLeft));
            Assert.That(world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(100));
        }

        [Test]
        public void LandAboveTheTownLevel_CannotBeBought()
        {
            _database.TryGetExpansion(TestContent.ExpansionEast, out var definition);
            ((FakeExpansionDefinition)definition).UnlockLevel = 5;
            GiveDeeds(_world, 20);

            Assert.That(_world.Expansion.TryUnlock(TestContent.ExpansionEast),
                Is.EqualTo(ExpansionResult.Locked));
        }

        [Test]
        public void AnUnknownExpansion_IsReported()
        {
            Assert.That(_world.Expansion.TryUnlock("expansion.nowhere"),
                Is.EqualTo(ExpansionResult.UnknownExpansion));
        }

        [Test]
        public void CollectAvailable_ShowsWhatCanBeBoughtNext()
        {
            var available = new List<IExpansionDefinition>();

            _world.Expansion.CollectAvailable(available);
            Assert.That(available.Count, Is.EqualTo(2), "Northeast is behind East");

            GiveDeeds(_world, TestContent.EastDeedCost);
            _world.Expansion.TryUnlock(TestContent.ExpansionEast);
            _world.Expansion.CollectAvailable(available);

            Assert.That(available.Count, Is.EqualTo(2));
            Assert.That(available.Exists(e => e.Id == TestContent.ExpansionNortheast), Is.True);
            Assert.That(available.Exists(e => e.Id == TestContent.ExpansionEast), Is.False);
        }

        // --- Land deeds ---------------------------------------------------------------------

        [Test]
        public void Deeds_TakeNoBarnSpace()
        {
            GiveDeeds(_world, 100);

            Assert.That(_world.Barn.CountOf(TestContent.Deed), Is.EqualTo(100));
            Assert.That(_world.Barn.UsedSpace, Is.EqualTo(0),
                "a deed is a token, not produce — it must not compete with crops for the barn");
        }

        [Test]
        public void CompletingAnOrder_CanPayALandDeed()
        {
            var world = CreateWorld(template: TestContent.DeedTemplate());
            world.Barn.Add(TestContent.Bread, 10);

            var order = world.HelicopterOrders.Orders[0];
            Assert.That(order.ItemRewards.Count, Is.EqualTo(1));
            Assert.That(order.ItemRewards[0].ItemId, Is.EqualTo(TestContent.Deed));

            Assert.That(world.HelicopterOrders.TryComplete(order.OrderId), Is.True);
            Assert.That(world.Barn.CountOf(TestContent.Deed), Is.EqualTo(1));
        }

        /// <summary>Deeds earned from orders are what actually buys land.</summary>
        [Test]
        public void DeedsEarnedFromOrders_BuyLand()
        {
            var world = CreateWorld(template: TestContent.DeedTemplate());
            world.Barn.Add(TestContent.Bread, 10);

            for (var i = 0; i < TestContent.EastDeedCost; i++)
            {
                Assert.That(world.HelicopterOrders.TryComplete(world.HelicopterOrders.Orders[0].OrderId), Is.True);
            }

            Assert.That(world.Barn.CountOf(TestContent.Deed), Is.EqualTo(TestContent.EastDeedCost));
            Assert.That(world.Expansion.TryUnlock(TestContent.ExpansionEast),
                Is.EqualTo(ExpansionResult.Success));
        }

        // --- Persistence --------------------------------------------------------------------

        [Test]
        public void BoughtLand_SurvivesASaveRoundTrip()
        {
            GiveDeeds(_world, TestContent.EastDeedCost);
            _world.Expansion.TryUnlock(TestContent.ExpansionEast);

            var restored = SaveAndReload(_world);

            Assert.That(restored.Expansion.IsUnlocked(TestContent.ExpansionEast), Is.True);
            Assert.That(restored.Buildings.Grid.IsUnlocked(At(4, 0)), Is.True);
            Assert.That(restored.Buildings.Grid.UnlockedCellCount, Is.EqualTo(32));
        }

        /// <summary>
        /// Land has to restore before buildings do, or a building standing on bought land would
        /// fail its placement check on load and be dropped.
        /// </summary>
        [Test]
        public void BuildingsOnBoughtLand_SurviveALoad()
        {
            GiveDeeds(_world, TestContent.EastDeedCost);
            _world.Expansion.TryUnlock(TestContent.ExpansionEast);
            _world.Buildings.TryPlace(TestContent.Shed, At(6, 2), out var instanceId);

            var restored = SaveAndReload(_world);

            Assert.That(restored.Buildings.TryGetBuilding(instanceId, out var shed), Is.True);
            Assert.That(shed.Origin, Is.EqualTo(At(6, 2)));
        }

        [Test]
        public void UnboughtLand_StaysLockedAfterALoad()
        {
            var restored = SaveAndReload(_world);

            Assert.That(restored.Buildings.Grid.IsUnlocked(At(4, 0)), Is.False);
            Assert.That(restored.Buildings.TryPlace(TestContent.Shed, At(5, 5), out _),
                Is.EqualTo(BuildingActionResult.AreaLocked));
        }

        [Test]
        public void ADeedRewardOnAnOrder_SurvivesASaveRoundTrip()
        {
            var world = CreateWorld(template: TestContent.DeedTemplate());
            var orderId = world.HelicopterOrders.Orders[0].OrderId;

            var restored = SaveAndReload(world);

            Assert.That(restored.HelicopterOrders.TryGetOrder(orderId, out var order), Is.True);
            Assert.That(order.ItemRewards.Count, Is.EqualTo(1));
            Assert.That(order.ItemRewards[0].ItemId, Is.EqualTo(TestContent.Deed),
                "the deed was rolled at generation and must not be re-rolled on load");
        }
    }
}
