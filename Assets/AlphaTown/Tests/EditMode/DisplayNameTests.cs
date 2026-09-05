using AlphaTown.UI.Hud;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// Definitions carry localisation keys, never display text — nothing in the simulation should
    /// hold a string a player reads. With no string table yet, this turns the key back into
    /// something legible, and it is the only thing standing between the player and "item.goat_milk".
    ///
    /// It matters more now that names have started diverging from ids: the egg item is still
    /// `eggs` in every save file and reads as "Chicken Eggs" on screen.
    /// </summary>
    public sealed class DisplayNameTests
    {
        [Test]
        public void AKeyBecomesItsLastSegment()
        {
            Assert.That(DisplayNames.Pretty("item.bacon"), Is.EqualTo("Bacon"));
        }

        [Test]
        public void UnderscoresBecomeWordBreaks()
        {
            Assert.That(DisplayNames.Pretty("item.goat_milk"), Is.EqualTo("Goat Milk"));
            Assert.That(DisplayNames.Pretty("building.duck_pond"), Is.EqualTo("Duck Pond"));
        }

        /// <summary>The one that made this worth having: the id and the name are not the same word.</summary>
        [Test]
        public void ANameMayDifferFromTheIdItCameFrom()
        {
            Assert.That(DisplayNames.Pretty("item.chicken_eggs"), Is.EqualTo("Chicken Eggs"));
        }

        [Test]
        public void CamelCaseIsBrokenUpToo()
        {
            Assert.That(DisplayNames.Pretty("item.goatMilk"), Is.EqualTo("Goat Milk"));
        }

        [Test]
        public void AKeyWithNoPrefixStillReads()
        {
            Assert.That(DisplayNames.Pretty("duck_meat"), Is.EqualTo("Duck Meat"));
        }

        [Test]
        public void NothingInMeansNothingOut()
        {
            Assert.That(DisplayNames.Pretty(null), Is.Empty);
            Assert.That(DisplayNames.Pretty(""), Is.Empty);
        }

        /// <summary>A trailing dot has no tail to show, so the key itself is better than blank.</summary>
        [Test]
        public void ATrailingDotFallsBackToTheWholeKey()
        {
            Assert.That(DisplayNames.Pretty("item."), Is.Not.Empty);
        }
    }
}
