using System;
using AlphaTown.Core.Events;
using AlphaTown.Data.Orders;
using AlphaTown.Data.Progression;
using AlphaTown.Gameplay.Commands;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// A second board is where the order system stops being a special case and becomes a system.
    /// These check the three things that would quietly break: boards stay separate through a save,
    /// a locked board produces nothing, and every command finds an order wherever it lives.
    /// </summary>
    public sealed class MultipleOrderBoardTests
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

            _database = TestContent.Build(startingCoins: 5000)
                .WithOrderBoard(TestContent.TrainBoard())
                .WithOrderTemplate(TestContent.TrainTemplateDefinition());

            _world = new GameWorld(_database, _clock, _events, new Random(23));
            _world.InitialiseNewPlayer();
            _commands = new TownCommands(_world, _database, _clock);
        }

        AlphaTown.Gameplay.Orders.OrderBoard TrainBoard()
        {
            foreach (var board in _world.OrderBoards)
            {
                if (board.Kind == OrderKind.Train) return board;
            }

            return null;
        }

        [Test]
        public void EveryAuthoredBoardIsBuilt()
        {
            Assert.That(_world.OrderBoards.Count, Is.EqualTo(2));
            Assert.That(TrainBoard(), Is.Not.Null);
        }

        [Test]
        public void TheHelicopterBoardIsStillTheNamedOne()
        {
            Assert.That(_world.HelicopterOrders.Kind, Is.EqualTo(OrderKind.Helicopter));
        }

        /// <summary>
        /// A locked board is not an empty board. It generates nothing and starts no cooldowns, so
        /// unlocking it later hands over every slot at once.
        /// </summary>
        [Test]
        public void ALockedBoardHoldsNoOrders()
        {
            Assert.That(TrainBoard().IsUnlocked, Is.False);
            Assert.That(TrainBoard().Orders, Is.Empty);
            Assert.That(_world.HelicopterOrders.Orders, Is.Not.Empty, "the first board is unaffected");
        }

        [Test]
        public void UnlockingABoardFillsEverySlotAtOnce()
        {
            _world.Progression.GrantXp(10000, XpSource.DebugGrant);
            _world.Sync();

            Assert.That(TrainBoard().IsUnlocked, Is.True);
            Assert.That(TrainBoard().Orders.Count, Is.EqualTo(TrainBoard().SlotCount));
        }

        [Test]
        public void OrdersOnDifferentBoardsAreDistinct()
        {
            _world.Progression.GrantXp(10000, XpSource.DebugGrant);
            _world.Sync();

            foreach (var helicopter in _world.HelicopterOrders.Orders)
            {
                Assert.That(TrainBoard().TryGetOrder(helicopter.OrderId, out _), Is.False);
            }
        }

        /// <summary>The command layer must not assume the helicopter board, or trains are undeliverable.</summary>
        [Test]
        public void ATrainOrderCanBeFoundAndDelivered()
        {
            _world.Progression.GrantXp(10000, XpSource.DebugGrant);
            _world.Sync();

            var order = TrainBoard().Orders[0];
            Assert.That(_world.TryGetBoardFor(order.OrderId, out var found), Is.True);
            Assert.That(found.Kind, Is.EqualTo(OrderKind.Train));

            foreach (var request in order.Requests) _world.Barn.Add(request.ItemId, request.Count);

            Assert.That(_commands.Deliver(order.OrderId).Success, Is.True);
        }

        [Test]
        public void ATrainOrderCanBeRerolled()
        {
            _world.Progression.GrantXp(10000, XpSource.DebugGrant);
            _world.Sync();

            var before = TrainBoard().Orders[0].OrderId;

            Assert.That(_commands.Reroll(before).Success, Is.True);
            Assert.That(TrainBoard().TryGetOrder(before, out _), Is.False);
        }

        /// <summary>
        /// The failure this design most invites: both boards saved into one array, then restored
        /// onto whichever matched first. Each has to land back where it came from.
        /// </summary>
        [Test]
        public void EachBoardSurvivesASaveRoundTripSeparately()
        {
            _world.Progression.GrantXp(10000, XpSource.DebugGrant);
            _world.Sync();

            var helicopterIds = IdsOf(_world.HelicopterOrders.Orders);
            var trainIds = IdsOf(TrainBoard().Orders);

            var save = _world.CaptureSave();
            Assert.That(save.OrderBoards.Length, Is.EqualTo(2));

            var restored = new GameWorld(_database, _clock, _events, new Random(23));
            restored.RestoreSave(save);

            AlphaTown.Gameplay.Orders.OrderBoard restoredTrain = null;
            foreach (var board in restored.OrderBoards)
            {
                if (board.Kind == OrderKind.Train) restoredTrain = board;
            }

            Assert.That(IdsOf(restored.HelicopterOrders.Orders), Is.EqualTo(helicopterIds));
            Assert.That(IdsOf(restoredTrain.Orders), Is.EqualTo(trainIds));
        }

        [Test]
        public void ALockedBoardSurvivesASaveStillLocked()
        {
            var save = _world.CaptureSave();

            var restored = new GameWorld(_database, _clock, _events, new Random(23));
            restored.RestoreSave(save);

            foreach (var board in restored.OrderBoards)
            {
                if (board.Kind != OrderKind.Train) continue;

                Assert.That(board.IsUnlocked, Is.False);
                Assert.That(board.Orders, Is.Empty);
            }
        }

        /// <summary>Content with no boards at all still gets one, or there is no way to earn.</summary>
        [Test]
        public void AProjectWithNoAuthoredBoardsStillGetsOne()
        {
            var database = TestContent.Build();
            database.ClearOrderBoards();

            var world = new GameWorld(database, _clock, _events, new Random(1));
            world.InitialiseNewPlayer();

            Assert.That(world.OrderBoards.Count, Is.EqualTo(1));
            Assert.That(world.HelicopterOrders, Is.Not.Null);
        }

        static string[] IdsOf(System.Collections.Generic.IReadOnlyList<AlphaTown.Gameplay.Orders.Order> orders)
        {
            var ids = new string[orders.Count];
            for (var i = 0; i < orders.Count; i++) ids[i] = orders[i].OrderId;
            return ids;
        }
    }
}
