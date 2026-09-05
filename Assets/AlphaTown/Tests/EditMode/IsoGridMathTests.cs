using AlphaTown.Core.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// The one place that knows a cell is drawn as a diamond. Worth pinning, because a projection
    /// that is subtly wrong shows up as taps landing on the wrong tile rather than as an error.
    /// </summary>
    public sealed class IsoGridMathTests
    {
        const float Tolerance = 0.0001f;

        [Test]
        public void TheOriginCellSitsAtTheWorldOrigin()
        {
            var world = IsoGridMath.GridToWorld(0, 0);

            Assert.That(world.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(world.y, Is.EqualTo(0f).Within(Tolerance));
        }

        /// <summary>+X goes right and down-screen, +Y goes left and down-screen: a diamond.</summary>
        [Test]
        public void TheAxesProjectIntoADiamond()
        {
            var alongX = IsoGridMath.GridToWorld(1, 0);
            var alongY = IsoGridMath.GridToWorld(0, 1);

            Assert.That(alongX.x, Is.EqualTo(IsoGridMath.TileWidth * 0.5f).Within(Tolerance));
            Assert.That(alongX.y, Is.EqualTo(IsoGridMath.TileHeight * 0.5f).Within(Tolerance));

            Assert.That(alongY.x, Is.EqualTo(-IsoGridMath.TileWidth * 0.5f).Within(Tolerance));
            Assert.That(alongY.y, Is.EqualTo(IsoGridMath.TileHeight * 0.5f).Within(Tolerance));
        }

        [Test]
        public void MovingDiagonallyGoesStraightDownScreen()
        {
            var world = IsoGridMath.GridToWorld(1, 1);

            Assert.That(world.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(world.y, Is.EqualTo(IsoGridMath.TileHeight).Within(Tolerance));
        }

        /// <summary>Picking a tap has to land back on the cell it came from.</summary>
        [Test]
        public void WorldToGrid_InvertsGridToWorld()
        {
            for (var x = -8; x <= 8; x++)
            {
                for (var y = -8; y <= 8; y++)
                {
                    var cell = new GridPosition(x, y);
                    Assert.That(IsoGridMath.WorldToGrid(IsoGridMath.GridToWorld(cell)), Is.EqualTo(cell));
                }
            }
        }

        [Test]
        public void WorldToGrid_SnapsToTheNearestCell()
        {
            var nearOrigin = IsoGridMath.GridToWorld(3, 5) + new Vector3(0.05f, 0.02f, 0f);

            Assert.That(IsoGridMath.WorldToGrid(nearOrigin), Is.EqualTo(new GridPosition(3, 5)));
        }

        [Test]
        public void RectCentre_SitsBetweenTheCornerCells()
        {
            var rect = new GridRect(new GridPosition(0, 0), new GridSize(2, 2));

            var centre = IsoGridMath.RectCentreToWorld(rect);
            var expected = IsoGridMath.GridToWorld(0.5f, 0.5f);

            Assert.That(centre.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(centre.y, Is.EqualTo(expected.y).Within(Tolerance));
        }

        [Test]
        public void ACellNearerTheCamera_SortsInFront()
        {
            var back = IsoGridMath.SortingOrder(new GridPosition(0, 0), 32);
            var front = IsoGridMath.SortingOrder(new GridPosition(3, 4), 32);

            Assert.That(front, Is.GreaterThan(back));
        }
    }
}
