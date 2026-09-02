using System;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    public sealed class GameClockTests
    {
        [Test]
        public void Clock_FollowsItsTimeSource()
        {
            var time = new ManualTimeSource();
            var clock = new GameClock(time);
            var start = clock.UtcNowTicks;

            time.Advance(TimeSpan.FromHours(3));

            Assert.That(clock.UtcNowTicks, Is.EqualTo(start + TimeSpan.FromHours(3).Ticks));
        }

        [Test]
        public void Pause_HoldsSimulationTimeStill()
        {
            var time = new ManualTimeSource();
            var clock = new GameClock(time);
            var start = clock.UtcNowTicks;

            clock.Pause();
            time.Advance(TimeSpan.FromMinutes(5));

            Assert.That(clock.UtcNowTicks, Is.EqualTo(start));
        }

        /// <summary>
        /// Resuming must not hand back the paused span. If it did, every timer in the town would
        /// jump forward the moment the debug pause was lifted.
        /// </summary>
        [Test]
        public void Resume_KeepsSimulationTimeContinuous()
        {
            var time = new ManualTimeSource();
            var clock = new GameClock(time);
            var start = clock.UtcNowTicks;

            clock.Pause();
            time.Advance(TimeSpan.FromMinutes(5));
            clock.Resume();

            Assert.That(clock.UtcNowTicks, Is.EqualTo(start));

            time.Advance(TimeSpan.FromSeconds(10));
            Assert.That(clock.UtcNowTicks, Is.EqualTo(start + TimeSpan.FromSeconds(10).Ticks));
        }

        [Test]
        public void Advance_MovesSimulationTimeForward()
        {
            var time = new ManualTimeSource();
            var clock = new GameClock(time);
            var start = clock.UtcNowTicks;

            clock.Advance(TimeSpan.FromDays(1));

            Assert.That(clock.UtcNowTicks, Is.EqualTo(start + TimeSpan.FromDays(1).Ticks));
        }
    }
}
