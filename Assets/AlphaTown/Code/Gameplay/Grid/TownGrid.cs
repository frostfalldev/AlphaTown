using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Spatial;

namespace AlphaTown.Gameplay.Grid
{
    /// <summary>
    /// Cell occupancy for the town. Deliberately not a tilemap engine: a flat array of "which
    /// building instance owns this cell", and nothing else.
    ///
    /// It knows nothing about buildings, costs or construction — only ids and rectangles. That is
    /// what keeps placement rules testable on their own and stops the grid growing into a
    /// second copy of the building system.
    /// </summary>
    public sealed class TownGrid
    {
        readonly string[] _occupants;

        public TownGrid(GridSize size)
        {
            if (!size.IsValid)
            {
                Log.Error("Grid", "Invalid town size " + size + ". Falling back to 1x1.");
                size = GridSize.One;
            }

            Size = size;
            _occupants = new string[size.Area];
        }

        public GridSize Size { get; }

        public bool IsInBounds(GridPosition cell) =>
            cell.X >= 0 && cell.Y >= 0 && cell.X < Size.Width && cell.Y < Size.Height;

        public bool IsInBounds(GridRect rect) =>
            rect.IsValid && rect.MinX >= 0 && rect.MinY >= 0 &&
            rect.MaxX < Size.Width && rect.MaxY < Size.Height;

        /// <summary>
        /// Whether the player may build on this cell.
        ///
        /// TODO(expansion): every cell in bounds is buildable today. Expansion will gate this on
        /// purchased regions — keeping the question here means placement validation needs no
        /// change when it lands, only this method.
        /// </summary>
        public bool IsUnlocked(GridPosition cell) => IsInBounds(cell);

        /// <param name="ignoreInstanceId">
        /// Cells owned by this instance count as free. Needed for moving and for re-footprinting
        /// a building in place, where the building would otherwise collide with itself.
        /// </param>
        public PlacementFailure Validate(GridRect rect, string ignoreInstanceId = null)
        {
            if (!rect.IsValid) return PlacementFailure.InvalidFootprint;
            if (!IsInBounds(rect)) return PlacementFailure.OutOfBounds;

            for (var y = rect.MinY; y <= rect.MaxY; y++)
            {
                for (var x = rect.MinX; x <= rect.MaxX; x++)
                {
                    var cell = new GridPosition(x, y);
                    if (!IsUnlocked(cell)) return PlacementFailure.AreaLocked;

                    var occupant = _occupants[IndexOf(cell)];
                    if (occupant == null) continue;
                    if (ignoreInstanceId != null && occupant == ignoreInstanceId) continue;

                    return PlacementFailure.Overlaps;
                }
            }

            return PlacementFailure.None;
        }

        public bool IsFree(GridRect rect, string ignoreInstanceId = null) =>
            Validate(rect, ignoreInstanceId) == PlacementFailure.None;

        /// <summary>Claims every cell in the rect. Validate first — this does not check.</summary>
        public void Occupy(GridRect rect, string instanceId)
        {
            if (!IsInBounds(rect)) return;

            for (var y = rect.MinY; y <= rect.MaxY; y++)
            {
                for (var x = rect.MinX; x <= rect.MaxX; x++)
                {
                    _occupants[IndexOf(new GridPosition(x, y))] = instanceId;
                }
            }
        }

        /// <summary>
        /// Frees the rect's cells, but only those the instance actually owns — so releasing a
        /// stale rect cannot silently steal a neighbour's cells.
        /// </summary>
        public void Release(GridRect rect, string instanceId)
        {
            if (!IsInBounds(rect)) return;

            for (var y = rect.MinY; y <= rect.MaxY; y++)
            {
                for (var x = rect.MinX; x <= rect.MaxX; x++)
                {
                    var index = IndexOf(new GridPosition(x, y));
                    if (_occupants[index] == instanceId) _occupants[index] = null;
                }
            }
        }

        public bool TryGetOccupant(GridPosition cell, out string instanceId)
        {
            if (!IsInBounds(cell))
            {
                instanceId = null;
                return false;
            }

            instanceId = _occupants[IndexOf(cell)];
            return instanceId != null;
        }

        public void Clear()
        {
            for (var i = 0; i < _occupants.Length; i++) _occupants[i] = null;
        }

        int IndexOf(GridPosition cell) => (cell.Y * Size.Width) + cell.X;
    }
}
