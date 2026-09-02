using AlphaTown.Core.Spatial;
using AlphaTown.Gameplay.Grid;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    public sealed class TownGridTests
    {
        TownGrid _grid;

        [SetUp]
        public void SetUp() => _grid = new TownGrid(new GridSize(8, 8));

        static GridRect Rect(int x, int y, int width, int height) =>
            new GridRect(new GridPosition(x, y), new GridSize(width, height));

        [Test]
        public void EmptyGrid_AcceptsAnythingInBounds()
        {
            Assert.That(_grid.Validate(Rect(0, 0, 1, 1)), Is.EqualTo(PlacementFailure.None));
            Assert.That(_grid.Validate(Rect(6, 6, 2, 2)), Is.EqualTo(PlacementFailure.None));
        }

        [Test]
        public void Validate_RejectsFootprintsThatRunOffTheEdge()
        {
            Assert.That(_grid.Validate(Rect(7, 7, 2, 2)), Is.EqualTo(PlacementFailure.OutOfBounds));
            Assert.That(_grid.Validate(Rect(-1, 0, 1, 1)), Is.EqualTo(PlacementFailure.OutOfBounds));
            Assert.That(_grid.Validate(Rect(0, 8, 1, 1)), Is.EqualTo(PlacementFailure.OutOfBounds));
        }

        [Test]
        public void Validate_RejectsAZeroSizedFootprint()
        {
            Assert.That(_grid.Validate(Rect(0, 0, 0, 2)), Is.EqualTo(PlacementFailure.InvalidFootprint));
        }

        [Test]
        public void Occupy_BlocksOverlappingFootprints()
        {
            _grid.Occupy(Rect(2, 2, 2, 2), "a");

            Assert.That(_grid.Validate(Rect(3, 3, 2, 2)), Is.EqualTo(PlacementFailure.Overlaps));
            Assert.That(_grid.Validate(Rect(0, 0, 2, 2)), Is.EqualTo(PlacementFailure.None),
                "a footprint that only touches the corner still fits");
        }

        /// <summary>
        /// Moving a building shifts it onto cells it already owns, so its own footprint has to be
        /// invisible to the check or nothing could ever move by one cell.
        /// </summary>
        [Test]
        public void Validate_IgnoresTheCellsTheMoverAlreadyOwns()
        {
            _grid.Occupy(Rect(2, 2, 2, 2), "a");

            Assert.That(_grid.Validate(Rect(3, 3, 2, 2), "a"), Is.EqualTo(PlacementFailure.None));
            Assert.That(_grid.Validate(Rect(3, 3, 2, 2), "b"), Is.EqualTo(PlacementFailure.Overlaps));
        }

        [Test]
        public void Release_FreesTheCells()
        {
            var rect = Rect(2, 2, 2, 2);
            _grid.Occupy(rect, "a");
            _grid.Release(rect, "a");

            Assert.That(_grid.Validate(rect), Is.EqualTo(PlacementFailure.None));
            Assert.That(_grid.TryGetOccupant(new GridPosition(2, 2), out _), Is.False);
        }

        /// <summary>
        /// Releasing a rect only clears cells the caller actually owns, so a stale rect after a
        /// move cannot quietly delete a neighbour's occupancy.
        /// </summary>
        [Test]
        public void Release_LeavesCellsOwnedBySomeoneElseAlone()
        {
            _grid.Occupy(Rect(0, 0, 2, 2), "a");
            _grid.Occupy(Rect(2, 0, 2, 2), "b");

            _grid.Release(Rect(0, 0, 4, 2), "a");

            Assert.That(_grid.TryGetOccupant(new GridPosition(2, 0), out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo("b"));
        }

        [Test]
        public void TryGetOccupant_ReportsWhoOwnsACell()
        {
            _grid.Occupy(Rect(4, 4, 2, 1), "shed");

            Assert.That(_grid.TryGetOccupant(new GridPosition(5, 4), out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo("shed"));
            Assert.That(_grid.TryGetOccupant(new GridPosition(5, 5), out _), Is.False);
            Assert.That(_grid.TryGetOccupant(new GridPosition(99, 99), out _), Is.False);
        }

        [Test]
        public void Clear_EmptiesTheWholeGrid()
        {
            _grid.Occupy(Rect(0, 0, 8, 8), "a");
            _grid.Clear();

            Assert.That(_grid.Validate(Rect(0, 0, 8, 8)), Is.EqualTo(PlacementFailure.None));
        }
    }
}
