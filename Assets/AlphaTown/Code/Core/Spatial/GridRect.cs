using System;

namespace AlphaTown.Core.Spatial
{
    /// <summary>
    /// An axis-aligned block of cells: where a building sits and how much room it takes.
    /// <see cref="MaxX"/> and <see cref="MaxY"/> are inclusive, so a 1x1 rect has Min == Max.
    /// </summary>
    [Serializable]
    public readonly struct GridRect : IEquatable<GridRect>
    {
        public readonly GridPosition Origin;
        public readonly GridSize Size;

        public GridRect(GridPosition origin, GridSize size)
        {
            Origin = origin;
            Size = size;
        }

        public int MinX => Origin.X;
        public int MinY => Origin.Y;
        public int MaxX => Origin.X + Size.Width - 1;
        public int MaxY => Origin.Y + Size.Height - 1;

        public bool IsValid => Size.IsValid;

        public bool Contains(GridPosition cell) =>
            cell.X >= MinX && cell.X <= MaxX && cell.Y >= MinY && cell.Y <= MaxY;

        public bool Overlaps(GridRect other) =>
            MinX <= other.MaxX && other.MinX <= MaxX &&
            MinY <= other.MaxY && other.MinY <= MaxY;

        public bool Equals(GridRect other) => Origin.Equals(other.Origin) && Size.Equals(other.Size);

        public override bool Equals(object obj) => obj is GridRect other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Origin.GetHashCode() * 397) ^ Size.GetHashCode();
            }
        }

        public override string ToString() => Origin + " " + Size;
    }
}
