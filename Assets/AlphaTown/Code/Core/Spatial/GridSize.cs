using System;

namespace AlphaTown.Core.Spatial
{
    /// <summary>A footprint in cells. Axis-aligned; rotation is a presentation concern.</summary>
    [Serializable]
    public readonly struct GridSize : IEquatable<GridSize>
    {
        public static readonly GridSize One = new GridSize(1, 1);

        public readonly int Width;
        public readonly int Height;

        public GridSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Area => Width * Height;

        public bool IsValid => Width > 0 && Height > 0;

        public bool Equals(GridSize other) => Width == other.Width && Height == other.Height;

        public override bool Equals(object obj) => obj is GridSize other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        public override string ToString() => Width + "x" + Height;
    }
}
