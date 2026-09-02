using System;
using AlphaTown.Core.Events;
using AlphaTown.Core.Timing;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// The device clock and the real passage of time are driven separately here, which is exactly
    /// what a player tampering with their clock does: the wall clock leaps, the world does not.
    /// </summary>
    public sealed class ServerTimeSourceTests
    {
        static readonly DateTime DeviceStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        static readonly DateTime ServerStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        static readonly long ExpectedOffset = ServerStart.Ticks - DeviceStart.Ticks;

        ManualTimeSource _device;
        ManualMonotonicClock _monotonic;
        FakeServerTimeProvider _provider;
        ServerTimeSource _source;

        [SetUp]
        public void SetUp()
        {
            _device = new ManualTimeSource(DeviceStart);
            _monotonic = new ManualMonotonicClock();
            _provider = new FakeServerTimeProvider { ServerUtcTicks = ServerStart.Ticks };
            _source = new ServerTimeSource(_device, _monotonic, _provider);
        }

        // --- Syncing --------------------------------------------------------------------------

        [Test]
        public void BeforeAnySync_TimeIsUntrustedAndFollowsTheDevice()
        {
            Assert.That(_source.Trust, Is.EqualTo(TimeTrust.Untrusted));
            Assert.That(_source.UtcNowTicks, Is.EqualTo(DeviceStart.Ticks));
            Assert.That(_source.HasOffset, Is.False);
        }

        [Test]
        public void AfterASync_TimeIsSynchronisedAndFollowsTheServer()
        {
            _source.RequestSync();

            Assert.That(_source.Trust, Is.EqualTo(TimeTrust.Synchronized));
            Assert.That(_source.IsTrusted, Is.True);
            Assert.That(_source.UtcNowTicks, Is.EqualTo(ServerStart.Ticks));
        }

        [Test]
        public void Sync_RecordsTheOffsetFromTheDeviceClock()
        {
            _source.RequestSync();

            Assert.That(_source.HasOffset, Is.True);
            Assert.That(_source.OffsetTicks, Is.EqualTo(ExpectedOffset));
        }

        [Test]
        public void Sync_CountsHalfTheRoundTripAsTheOneWayTrip()
        {
            _provider.RoundTripTicks = TimeSpan.FromSeconds(2).Ticks;

            _source.RequestSync();

            Assert.That(_source.UtcNowTicks,
                Is.EqualTo(ServerStart.Ticks + TimeSpan.FromSeconds(1).Ticks));
        }

        [Test]
        public void TimeAdvancesWithTheMonotonicClock()
        {
            _source.RequestSync();
            _monotonic.Advance(TimeSpan.FromHours(3));

            Assert.That(_source.UtcNowTicks,
                Is.EqualTo(ServerStart.Ticks + TimeSpan.FromHours(3).Ticks));
        }

        /// <summary>
        /// The headline guarantee. Once synchronised, time comes from a monotonic counter and the
        /// device clock is never read again — so setting it forward achieves nothing.
        /// </summary>
        [Test]
        public void WhileSynchronised_MovingTheDeviceClockChangesNothing()
        {
            _source.RequestSync();
            var before = _source.UtcNowTicks;

            _device.Advance(TimeSpan.FromDays(30));

            Assert.That(_source.UtcNowTicks, Is.EqualTo(before));
        }

        // --- Offline behaviour ----------------------------------------------------------------

        [Test]
        public void WithoutAProvider_SyncFailsAndTrustStaysUntrusted()
        {
            var offline = new ServerTimeSource(_device, _monotonic);
            var reported = true;

            offline.RequestSync(success => reported = success);

            Assert.That(reported, Is.False);
            Assert.That(offline.Trust, Is.EqualTo(TimeTrust.Untrusted));
            Assert.That(offline.UtcNowTicks, Is.EqualTo(DeviceStart.Ticks));
        }

        [Test]
        public void AnUnreachableServer_LeavesTheSessionOnDeviceTime()
        {
            _provider.IsReachable = false;
            var reported = true;

            _source.RequestSync(success => reported = success);

            Assert.That(reported, Is.False);
            Assert.That(_provider.RequestCount, Is.EqualTo(1));
            Assert.That(_source.Trust, Is.EqualTo(TimeTrust.Untrusted));
        }

        [Test]
        public void AStoredOffset_IsUsedWhenTheServerCannotBeReached()
        {
            _provider.IsReachable = false;
            var restored = new ServerTimeSource(_device, _monotonic, _provider);

            restored.RestoreState(new TimeSyncSaveData { OffsetTicks = ExpectedOffset, HasOffset = true });
            restored.RequestSync();

            Assert.That(restored.Trust, Is.EqualTo(TimeTrust.Stale));
            Assert.That(restored.UtcNowTicks, Is.EqualTo(ServerStart.Ticks),
                "a stale offset still corrects a device whose clock was already wrong");
        }

        [Test]
        public void AFailedSync_KeepsTheStoredOffset()
        {
            _source.RestoreState(new TimeSyncSaveData { OffsetTicks = ExpectedOffset, HasOffset = true });
            _provider.IsReachable = false;

            _source.RequestSync();

            Assert.That(_source.OffsetTicks, Is.EqualTo(ExpectedOffset));
            Assert.That(_source.Trust, Is.EqualTo(TimeTrust.Stale));
        }

        // --- Clock jumps ----------------------------------------------------------------------

        [Test]
        public void PollDeviceDrift_DetectsALargeJump()
        {
            _source.RequestSync();

            long reported = 0;
            _source.ClockJumpDetected += drift => reported = drift;

            _device.Advance(TimeSpan.FromDays(1));
            _source.PollDeviceDrift();

            Assert.That(_source.HasDetectedClockJump, Is.True);
            Assert.That(reported, Is.EqualTo(TimeSpan.FromDays(1).Ticks));
        }

        [Test]
        public void PollDeviceDrift_IgnoresAnOrdinaryCorrection()
        {
            _source.RequestSync();
            _device.Advance(TimeSpan.FromSeconds(30));

            _source.PollDeviceDrift();

            Assert.That(_source.HasDetectedClockJump, Is.False);
        }

        /// <summary>A jump cannot hurt a synchronised session, because time does not come from there.</summary>
        [Test]
        public void AJumpWhileSynchronised_DoesNotDowngradeTrust()
        {
            _source.RequestSync();
            _device.Advance(TimeSpan.FromDays(1));
            var before = _source.UtcNowTicks;

            _source.PollDeviceDrift();

            Assert.That(_source.Trust, Is.EqualTo(TimeTrust.Synchronized));
            Assert.That(_source.UtcNowTicks, Is.EqualTo(before));
        }

        /// <summary>
        /// Unsynchronised, the baseline came from the device clock — so catching that clock moving
        /// means everything since is suspect, and the session says so.
        /// </summary>
        [Test]
        public void AJumpWhileUnverified_DowngradesTrust()
        {
            _source.RestoreState(new TimeSyncSaveData { OffsetTicks = ExpectedOffset, HasOffset = true });
            Assert.That(_source.Trust, Is.EqualTo(TimeTrust.Stale));

            _device.Advance(TimeSpan.FromDays(1));
            _source.PollDeviceDrift();

            Assert.That(_source.Trust, Is.EqualTo(TimeTrust.Untrusted));
        }

        // --- Floor and recovery ---------------------------------------------------------------

        [Test]
        public void TimeNeverFallsBelowTheStoredFloor()
        {
            var floor = DeviceStart.Ticks + TimeSpan.FromDays(10).Ticks;
            _source.RestoreState(new TimeSyncSaveData { LastKnownUtcTicks = floor });

            Assert.That(_source.UtcNowTicks, Is.EqualTo(floor),
                "winding the clock back must not un-complete what the player saw finish");
        }

        /// <summary>
        /// Without this a session poisoned by a clock set far into the future would keep that
        /// inflated floor forever, and syncing would never bring it back.
        /// </summary>
        [Test]
        public void AServerSampleClearsAPoisonedFloor()
        {
            var poisoned = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            _source.RestoreState(new TimeSyncSaveData { LastKnownUtcTicks = poisoned });
            Assert.That(_source.UtcNowTicks, Is.EqualTo(poisoned));

            _source.RequestSync();

            Assert.That(_source.UtcNowTicks, Is.EqualTo(ServerStart.Ticks));
            Assert.That(_source.Trust, Is.EqualTo(TimeTrust.Synchronized));
        }

        // --- Suspend --------------------------------------------------------------------------

        [Test]
        public void RebaselineAfterSuspend_CatchesUpToTheDeviceClock()
        {
            _source.RequestSync();

            // The monotonic counter stopped while the device slept; the wall clock did not.
            _device.Advance(TimeSpan.FromHours(2));
            _source.RebaselineAfterSuspend();

            Assert.That(_source.UtcNowTicks,
                Is.EqualTo(ServerStart.Ticks + TimeSpan.FromHours(2).Ticks));
            Assert.That(_source.Trust, Is.EqualTo(TimeTrust.Stale),
                "the wall clock pushed time forward, so the session is no longer verified");
        }

        [Test]
        public void RebaselineAfterSuspend_NeverMovesTimeBackward()
        {
            _source.RequestSync();
            _monotonic.Advance(TimeSpan.FromHours(5));
            var before = _source.UtcNowTicks;

            _source.RebaselineAfterSuspend();

            Assert.That(_source.UtcNowTicks, Is.EqualTo(before));
            Assert.That(_source.Trust, Is.EqualTo(TimeTrust.Synchronized));
        }

        // --- Plumbing -------------------------------------------------------------------------

        [Test]
        public void CaptureState_RoundTripsThroughRestore()
        {
            _source.RequestSync();
            _monotonic.Advance(TimeSpan.FromHours(1));
            var state = _source.CaptureState();

            var restored = new ServerTimeSource(_device, new ManualMonotonicClock());
            restored.RestoreState(state);

            Assert.That(restored.OffsetTicks, Is.EqualTo(ExpectedOffset));
            Assert.That(restored.HasOffset, Is.True);
            Assert.That(restored.UtcNowTicks, Is.EqualTo(state.LastKnownUtcTicks));
        }

        [Test]
        public void GameClock_ReportsTheSourcesTrust()
        {
            var clock = new GameClock(_source);
            Assert.That(clock.Trust, Is.EqualTo(TimeTrust.Untrusted));
            Assert.That(clock.IsTimeTrusted, Is.False);

            _source.RequestSync();

            Assert.That(clock.Trust, Is.EqualTo(TimeTrust.Synchronized));
            Assert.That(clock.IsTimeTrusted, Is.True);
            Assert.That(clock.UtcNowTicks, Is.EqualTo(ServerStart.Ticks));
        }

        // --- The systems that depend on it ----------------------------------------------------

        GameWorld BuildWorld(out GameClock clock)
        {
            _source.RequestSync();
            clock = new GameClock(_source);

            var world = new GameWorld(TestContent.Build(startingCoins: 100), clock, new EventBus(), new Random(1));
            world.InitialiseNewPlayer();
            return world;
        }

        /// <summary>
        /// The reason this phase exists: production, construction, crops and cooldowns are all
        /// comparisons against the clock, so a clock the player cannot move finishes all of them.
        /// </summary>
        [Test]
        public void ProductionIgnoresASpoofedDeviceClock()
        {
            var world = BuildWorld(out _);
            var bakery = world.AddProducer("bakery_1", TestContent.Bakery);
            world.Barn.Add(TestContent.Flour, 5);
            Assert.That(bakery.TryEnqueue(TestContent.BreadRecipe, world.Barn), Is.True);

            // The player winds the device clock a year forward.
            _device.Advance(TimeSpan.FromDays(365));
            world.Sync();

            Assert.That(bakery.Orders.Count, Is.EqualTo(1), "a spoofed clock must not finish the bake");
            Assert.That(bakery.HasReadyGoods, Is.False);

            // Actual time passing does finish it.
            _monotonic.Advance(TimeSpan.FromMinutes(2));
            world.Sync();

            Assert.That(bakery.HasReadyGoods, Is.True);
        }

        [Test]
        public void OrderSlotCooldownsIgnoreASpoofedDeviceClock()
        {
            var world = BuildWorld(out _);
            world.Barn.Add(TestContent.Bread, 10);

            world.HelicopterOrders.TryComplete(world.HelicopterOrders.Orders[0].OrderId);
            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(3));

            _device.Advance(TimeSpan.FromDays(1));
            world.Sync();
            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(3),
                "a spoofed clock must not clear the slot cooldown");

            _monotonic.Advance(TimeSpan.FromSeconds(TestContent.OrderSlotCooldownSeconds + 1));
            world.Sync();
            Assert.That(world.HelicopterOrders.Orders.Count, Is.EqualTo(4));
        }

        [Test]
        public void ConstructionIgnoresASpoofedDeviceClock()
        {
            var world = BuildWorld(out _);
            world.Wallet.Grant(TestContent.Coins, 1000, Data.Economy.CurrencySource.DebugGrant);

            var placed = world.Buildings.TryPlace(
                TestContent.BakeryBuilding, new Core.Spatial.GridPosition(0, 0), out var instanceId);
            Assert.That(placed, Is.EqualTo(Gameplay.Buildings.BuildingActionResult.Success));

            _device.Advance(TimeSpan.FromDays(365));
            world.Sync();

            Assert.That(world.Buildings.TryGetBuilding(instanceId, out var bakery), Is.True);
            Assert.That(bakery.State, Is.EqualTo(Gameplay.Buildings.BuildingState.UnderConstruction));

            _monotonic.Advance(TimeSpan.FromMinutes(5));
            world.Sync();

            Assert.That(bakery.State, Is.EqualTo(Gameplay.Buildings.BuildingState.Operational));
        }
    }
}
