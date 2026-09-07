using System;
using AlphaTown.Core.Events;
using AlphaTown.Data.Orders;
using AlphaTown.Gameplay.Orders;
using AlphaTown.Gameplay.Saving;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Save;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// Slot cooldowns are the throttle on the game's main coin faucet. With fields producing for
    /// free, a board that refilled the instant it emptied would be unbounded income.
    /// </summary>
    public sealed class OrderBoardPacingTests
    {
        const int Cooldown = TestContent.OrderSlotCooldownSeconds;

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
            _world.Barn.Add(TestContent.Bread, 20);
        }

        GameWorld CreateWorld(IOrderTemplateDefinition template = null, IOrderBoardDefinition board = null)
        {
            _database = TestContent.Build(startingCoins: 100, orderTemplate: template, orderBoard: board);
            var world = new GameWorld(_database, _clock, _events, new Random(3));
            world.InitialiseNewPlayer();
            return world;
        }

        static Order OrderInSlot(GameWorld world, int slotIndex)
        {
            var orders = world.HelicopterOrders.Orders;
            for (var i = 0; i < orders.Count; i++)
            {
                if (world.HelicopterOrders.SlotIndexOf(orders[i].OrderId) == slotIndex) return orders[i];
            }

            return null;
        }

        void CompleteFirstOrder() =>
            Assert.That(_world.HelicopterOrders.TryComplete(_world.HelicopterOrders.Orders[0].OrderId), Is.True);

        [Test]
        public void ANewBoard_FillsEverySlotAtOnce()
        {
            Assert.That(_world.HelicopterOrders.SlotCount, Is.EqualTo(4));
            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(4));

            for (var i = 0; i < 4; i++)
            {
                Assert.That(_world.HelicopterOrders.IsSlotOnCooldown(i), Is.False);
            }
        }

        [Test]
        public void CompletingAnOrder_PutsItsSlotOnCooldown()
        {
            var slot = _world.HelicopterOrders.SlotIndexOf(_world.HelicopterOrders.Orders[0].OrderId);
            CompleteFirstOrder();

            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(3));
            Assert.That(_world.HelicopterOrders.IsSlotOnCooldown(slot), Is.True);
            Assert.That(_world.HelicopterOrders.SlotAvailableAtTicks(slot),
                Is.EqualTo(_clock.UtcNowTicks + TimeSpan.FromSeconds(Cooldown).Ticks));
        }

        [Test]
        public void ACooledSlot_DoesNotRefillEarly()
        {
            CompleteFirstOrder();

            _time.Advance(TimeSpan.FromSeconds(Cooldown - 1));
            _world.Sync();

            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(3));
        }

        [Test]
        public void ACooledSlot_RefillsOnceTheCooldownPasses()
        {
            CompleteFirstOrder();

            _time.Advance(TimeSpan.FromSeconds(Cooldown + 1));
            _world.Sync();

            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(4));
        }

        [Test]
        public void AnExpiredOrder_CoolsItsSlotToo()
        {
            var world = CreateWorld(template: TestContent.TimedTemplate(TimeSpan.FromHours(1)));
            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(4));

            _time.Advance(TimeSpan.FromHours(2));
            world.Sync();

            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(0));

            _time.Advance(TimeSpan.FromSeconds(Cooldown + 1));
            world.Sync();

            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(4));
        }

        /// <summary>Rerolling for free would let a player fish for a better payout all day.</summary>
        [Test]
        public void DiscardingAnOrder_CostsACooldown()
        {
            var orderId = _world.HelicopterOrders.Orders[0].OrderId;

            Assert.That(_world.HelicopterOrders.TryDiscard(orderId), Is.True);
            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(3));

            _world.Sync();
            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(3));
        }

        [Test]
        public void SlotsCanHaveDifferentCooldowns()
        {
            // Slot 0 refills instantly, slot 1 takes the full cooldown.
            var world = CreateWorld(
                board: new FakeOrderBoardDefinition(OrderKind.Helicopter, 0, Cooldown));
            world.Barn.Add(TestContent.Bread, 20);

            Assert.That(world.HelicopterOrders.SlotCount, Is.EqualTo(2));
            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(2));

            var slotZero = OrderInSlot(world, 0);
            Assert.That(world.HelicopterOrders.TryComplete(slotZero.OrderId), Is.True);
            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(2),
                "a zero cooldown refills in the same sync");

            var slotOne = OrderInSlot(world, 1);
            Assert.That(world.HelicopterOrders.TryComplete(slotOne.OrderId), Is.True);
            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(1));

            _time.Advance(TimeSpan.FromSeconds(Cooldown + 1));
            world.Sync();
            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(2));
        }

        /// <summary>
        /// Cooldowns are absolute timestamps, so a week away resolves in one sync rather than
        /// needing anything to have been running.
        /// </summary>
        [Test]
        public void ALongAbsence_RefillsEveryCooledSlotInOneSync()
        {
            for (var i = 0; i < 4; i++) CompleteFirstOrder();
            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(0));

            _time.Advance(TimeSpan.FromDays(7));
            _world.Sync();

            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(4));
        }

        [Test]
        public void StartingACooldown_PublishesAnEvent()
        {
            var slotIndex = -1;
            using (_events.Subscribe<OrderSlotCooldownStartedEvent>(e => slotIndex = e.SlotIndex))
            {
                CompleteFirstOrder();
            }

            Assert.That(slotIndex, Is.InRange(0, 3));
        }

        /// <summary>
        /// Without this the board would hand back a full set of orders on every launch, which is
        /// a free income multiplier for anyone willing to restart the app.
        /// </summary>
        [Test]
        public void Cooldowns_SurviveASaveRoundTrip()
        {
            CompleteFirstOrder();
            CompleteFirstOrder();
            Assert.That(_world.HelicopterOrders.Orders.Count, Is.EqualTo(2));

            var saveService = new SaveService(
                new InMemorySaveStore(), new JsonSaveSerializer(), _clock,
                GameWorld.SaveSchemaVersion, null, "tests");

            Assert.That(saveService.TrySave(GameWorld.DefaultSaveSlot, _world.CaptureSave()), Is.True);
            Assert.That(saveService.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var data), Is.True);

            var restored = new GameWorld(_database, _clock, _events, new Random(11));
            restored.RestoreSave(data);

            Assert.That(restored.HelicopterOrders.Orders.Count, Is.EqualTo(2),
                "reloading must not refill the cooling slots");

            _time.Advance(TimeSpan.FromSeconds(Cooldown + 1));
            restored.Sync();
            Assert.That(restored.HelicopterOrders.Orders.Count, Is.EqualTo(4));
        }

        [Test]
        public void SurvivingOrders_ReturnToTheSameSlotsAfterALoad()
        {
            var before = _world.HelicopterOrders.Orders[0];
            var slot = _world.HelicopterOrders.SlotIndexOf(before.OrderId);

            var saveService = new SaveService(
                new InMemorySaveStore(), new JsonSaveSerializer(), _clock,
                GameWorld.SaveSchemaVersion, null, "tests");
            saveService.TrySave(GameWorld.DefaultSaveSlot, _world.CaptureSave());
            saveService.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var data);

            var restored = new GameWorld(_database, _clock, _events, new Random(11));
            restored.RestoreSave(data);

            Assert.That(restored.HelicopterOrders.SlotIndexOf(before.OrderId), Is.EqualTo(slot));
        }
    }
}
