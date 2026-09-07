using System;
using AlphaTown.Core.Events;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Progression;
using AlphaTown.Gameplay.Commands;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// The cheats exist so the late boards can be looked at without playing an evening first, and
    /// content nobody looks at is content nobody has checked. They are tested for the same reason
    /// anything else is: a broken debug tool wastes the time it was built to save.
    ///
    /// The grants use the DebugGrant reason codes, so a test session can never be mistaken for a
    /// real one in the economy numbers. That is the assertion that matters most here.
    /// </summary>
    public sealed class DebugCommandTests
    {
        ManualTimeSource _time;
        GameClock _clock;
        EventBus _events;
        FakeDatabase _database;
        GameWorld _world;
        DebugCommands _debug;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeSource();
            _clock = new GameClock(_time);
            _events = new EventBus();

            _database = TestContent.Build(startingCoins: 1000, includeFarming: true, includeExpansion: true);
            _world = new GameWorld(_database, _clock, _events, new Random(31));
            _world.InitialiseNewPlayer();
            _debug = new DebugCommands(_world, _database, _clock);
        }

        [Test]
        public void LevelUpAdvancesExactlyOneLevel()
        {
            var before = _world.Progression.TownLevel;

            Assert.That(_debug.LevelUp().Success, Is.True);
            Assert.That(_world.Progression.TownLevel, Is.EqualTo(before + 1));
        }

        [Test]
        public void LevelUpStopsAtTheCap()
        {
            while (!_world.Progression.IsMaxLevel) _debug.LevelUp();

            Assert.That(_debug.LevelUp().Success, Is.False);
        }

        /// <summary>
        /// The assertion that keeps the ledger honest: debug coins are attributed to DebugGrant,
        /// so they never contaminate the numbers a real session produces.
        /// </summary>
        [Test]
        public void GrantedCoinsAreAttributedToDebug()
        {
            _debug.GrantCoins(500);

            Assert.That(_world.Ledger.TotalFrom(TestContent.Coins, CurrencySource.DebugGrant),
                Is.EqualTo(500L));

            Assert.That(_world.Ledger.TotalFrom(TestContent.Coins, CurrencySource.OrderReward),
                Is.EqualTo(0L), "a debug grant must never look like an order payout");
        }

        [Test]
        public void GrantedXpIsAttributedToDebug()
        {
            _debug.LevelUp();

            Assert.That(_world.Progression.TotalXpFrom(XpSource.DebugGrant), Is.GreaterThan(0L));
        }

        [Test]
        public void FillingTheBarnAddsGoods()
        {
            Assert.That(_debug.FillBarn(10).Success, Is.True);
            Assert.That(_world.Barn.UsedSpace, Is.GreaterThan(0));
        }

        [Test]
        public void FillingAFullBarnSaysSo()
        {
            _debug.FillBarn(1000);

            Assert.That(_debug.FillBarn(1000).Success, Is.False);
        }

        /// <summary>Deeds are found by category, so renaming the item does not break the cheat.</summary>
        [Test]
        public void SpecialItemsAreGrantedByCategory()
        {
            Assert.That(_debug.GrantSpecialItems(3).Success, Is.True);
            Assert.That(_world.Barn.CountOf(TestContent.Deed), Is.EqualTo(3));
        }

        /// <summary>
        /// The most useful cheat, and the one that proves the architecture: a skip resolves
        /// production through exactly the code path a player returning in the morning takes.
        /// </summary>
        [Test]
        public void SkippingTimeResolvesProductionThatFinished()
        {
            _world.Buildings.TryPlace(TestContent.FieldBuilding, AlphaTown.Core.Spatial.GridPosition.Zero, out var field);
            _world.Sync();

            Assert.That(_world.TryGetProducer(field, out var producer), Is.True);
            producer.TryEnqueue(TestContent.WheatCrop, _world.Barn);

            Assert.That(producer.HasReadyGoods, Is.False);

            Assert.That(_debug.SkipTime(TimeSpan.FromHours(1)).Success, Is.True);
            Assert.That(producer.HasReadyGoods, Is.True, "the crop finished during the skip");
        }

        [Test]
        public void SkippingNoTimeIsRefused()
        {
            Assert.That(_debug.SkipTime(TimeSpan.Zero).Success, Is.False);
        }

        /// <summary>
        /// A skip must be able to open a locked board, since reaching one honestly is the whole
        /// problem these cheats exist to solve.
        /// </summary>
        [Test]
        public void LevellingUpOpensALockedBoard()
        {
            var database = TestContent.Build()
                .WithOrderBoard(TestContent.TrainBoard())
                .WithOrderTemplate(TestContent.TrainTemplateDefinition());

            var world = new GameWorld(database, _clock, _events, new Random(31));
            world.InitialiseNewPlayer();
            var debug = new DebugCommands(world, database, _clock);

            AlphaTown.Gameplay.Orders.OrderBoard train = null;
            foreach (var board in world.OrderBoards)
            {
                if (board.Kind == AlphaTown.Data.Orders.OrderKind.Train) train = board;
            }

            Assert.That(train.IsUnlocked, Is.False);

            while (!train.IsUnlocked && !world.Progression.IsMaxLevel) debug.LevelUp();

            Assert.That(train.IsUnlocked, Is.True);
            Assert.That(train.Orders, Is.Not.Empty, "opening a board fills it");
        }
    }
}
