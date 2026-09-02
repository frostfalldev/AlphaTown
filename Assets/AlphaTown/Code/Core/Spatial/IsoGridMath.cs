using UnityEngine;

namespace AlphaTown.Core.Spatial
{
    /// <summary>
    /// Converts between logical grid cells and world positions for a 2D isometric view.
    ///
    /// The simulation only ever deals in <see cref="GridPosition"/>; this is the one place that
    /// knows a cell is drawn as a diamond on the X/Y plane. Keeping the projection here means the
    /// art direction can change — tile size, or a switch to a 3D camera — without a single
    /// gameplay system noticing.
    ///
    /// Cells project to a diamond: moving +1 in grid X goes right and down-screen, +1 in grid Y
    /// goes left and down-screen.
    /// </summary>
    public static class IsoGridMath
    {
        /// <summary>Width of one cell's diamond in world units. Must match the sprite art.</summary>
        public const float TileWidth = 1f;

        /// <summary>Height of one cell's diamond. Half the width gives the classic 2:1 look.</summary>
        public const float TileHeight = 0.5f;

        const float HalfWidth = TileWidth * 0.5f;
        const float HalfHeight = TileHeight * 0.5f;

        public static Vector3 GridToWorld(float x, float y) =>
            new Vector3((x - y) * HalfWidth, (x + y) * HalfHeight, 0f);

        public static Vector3 GridToWorld(int x, int y) => GridToWorld((float)x, y);

        public static Vector3 GridToWorld(GridPosition cell) => GridToWorld((float)cell.X, cell.Y);

        /// <summary>Centre of a footprint, for placing a sprite that spans several cells.</summary>
        public static Vector3 RectCentreToWorld(GridRect rect)
        {
            var centreX = rect.Origin.X + (rect.Size.Width - 1) * 0.5f;
            var centreY = rect.Origin.Y + (rect.Size.Height - 1) * 0.5f;
            return GridToWorld(centreX, centreY);
        }

        /// <summary>Nearest cell to a world position. The inverse of the diamond projection.</summary>
        public static GridPosition WorldToGrid(Vector3 world)
        {
            // difference = x - y, sum = x + y. Solving the pair recovers the cell.
            var difference = world.x / HalfWidth;
            var sum = world.y / HalfHeight;

            return new GridPosition(
                Mathf.RoundToInt((difference + sum) * 0.5f),
                Mathf.RoundToInt((sum - difference) * 0.5f));
        }

        /// <summary>
        /// Draw order for a cell. Higher grid coordinates are nearer the camera, so they sort in
        /// front — the standard isometric painter's ordering.
        /// </summary>
        public static int SortingOrder(GridPosition cell, int gridHeight) => (cell.X + cell.Y) * gridHeight;
    }
}
