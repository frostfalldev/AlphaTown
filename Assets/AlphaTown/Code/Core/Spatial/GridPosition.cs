using System;

namespace AlphaTown.Core.Spatial
{
    /// <summary>
    /// A cell on the town grid. Integer coordinates only — the simulation never deals in world
    /// units, which is what keeps placement exact and save data stable across art changes.
    /// </summary>
    [Serializable]
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public static readonly GridPosition Zero = new GridPosition(0, 0);

        public readonly int X;
        public readonly int Y;

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridPosition operator +(GridPosition a, GridPosition b) =>
            new GridPosition(a.X + b.X, a.Y + b.Y);

        public static GridPosition operator -(GridPosition a, GridPosition b) =>
            new GridPosition(a.X - b.X, a.Y - b.Y);

        public static bool operator ==(GridPosition a, GridPosition b) => a.Equals(b);

        public static bool operator !=(GridPosition a, GridPosition b) => !a.Equals(b);

        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString() => "(" + X + ", " + Y + ")";
    }
}
