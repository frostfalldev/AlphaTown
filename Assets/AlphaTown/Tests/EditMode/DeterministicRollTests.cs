using AlphaTown.Core.Randomness;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// The roll is the reason a variable yield is safe in a game that resolves timers from
    /// timestamps. If it were not a pure function of the event, a harvest would pay differently
    /// depending on when the player happened to open the app.
    /// </summary>
    public sealed class DeterministicRollTests
    {
        [Test]
        public void SameSeedGivesSameResult()
        {
            var first = DeterministicRoll.Seed("recipe.wheat", 637_000_000_000_000_000L);
            var second = DeterministicRoll.Seed("recipe.wheat", 637_000_000_000_000_000L);

            Assert.That(DeterministicRoll.Range(second, 0, 5), Is.EqualTo(DeterministicRoll.Range(first, 0, 5)));
        }

        [Test]
        public void DifferentTimestampsGiveDifferentSeeds()
        {
            var first = DeterministicRoll.Seed("recipe.wheat", 1_000L);
            var second = DeterministicRoll.Seed("recipe.wheat", 1_001L);

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void DifferentRecipesGiveDifferentSeeds()
        {
            var wheat = DeterministicRoll.Seed("recipe.wheat", 1_000L);
            var corn = DeterministicRoll.Seed("recipe.corn", 1_000L);

            Assert.That(corn, Is.Not.EqualTo(wheat));
        }

        [Test]
        public void RangeStaysWithinBounds()
        {
            for (var i = 0; i < 500; i++)
            {
                var value = DeterministicRoll.Range(DeterministicRoll.Seed("x", i), 2, 5);
                Assert.That(value, Is.InRange(2, 5));
            }
        }

        [Test]
        public void EmptyRangeReturnsTheMinimum()
        {
            Assert.That(DeterministicRoll.Range(DeterministicRoll.Seed("x", 1L), 3, 3), Is.EqualTo(3));
            Assert.That(DeterministicRoll.Range(DeterministicRoll.Seed("x", 1L), 3, 1), Is.EqualTo(3));
        }

        /// <summary>
        /// A hash that clustered would make a "0 to 2 bonus" always pay the same. Not a statistical
        /// proof — just enough to catch a mix that has collapsed.
        /// </summary>
        [Test]
        public void RangeCoversItsWholeSpread()
        {
            var seen = new bool[4];
            for (var i = 0; i < 400; i++)
            {
                seen[DeterministicRoll.Range(DeterministicRoll.Seed("spread", i), 0, 3)] = true;
            }

            Assert.That(seen, Is.All.True);
        }
    }
}
