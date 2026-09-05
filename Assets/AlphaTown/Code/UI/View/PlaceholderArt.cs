using UnityEngine;

namespace AlphaTown.UI.View
{
    /// <summary>
    /// Sprites drawn in code, so the game is legible before any art exists.
    ///
    /// Every one of these is replaceable by dropping a real sprite into the matching serialized
    /// field. They exist so that "is the loop any good" can be answered without waiting on an
    /// artist, not because procedural art is a goal.
    /// </summary>
    public static class PlaceholderArt
    {
        // Generated once and kept. The textures are HideAndDontSave, so they survive a scene load
        // and would otherwise pile up one copy per load.
        static Sprite _solid;
        static Sprite _sickle;

        /// <summary>One white pixel, tinted per use. Ground tiles and buildings are built from this.</summary>
        public static Sprite Solid()
        {
            if (_solid != null) return _solid;

            var texture = NewTexture(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            return _solid = Finish(texture, "AlphaTown Solid", pixelsPerUnit: 1f);
        }

        /// <summary>
        /// A sickle: curved blade, short handle, dark outline so it reads against the grass.
        ///
        /// Drawn rather than imported because the tool has to be visible for the mode to make
        /// sense — a mode you cannot see you are in is a mode that feels like a bug. The pivot
        /// sits at the handle, so rotating it toward the swipe swings the blade rather than
        /// spinning the whole thing about its middle.
        /// </summary>
        public static Sprite Sickle()
        {
            if (_sickle != null) return _sickle;

            const int size = 64;
            const float outlineWidth = 1.6f;

            var blade = new Color(0.86f, 0.88f, 0.92f);
            var handle = new Color(0.45f, 0.30f, 0.17f);
            var outline = new Color(0.08f, 0.09f, 0.11f);

            var texture = NewTexture(size, size);
            var pixels = new Color[size * size];

            // Blade: an arc opening upward, swept from the handle end round to the tip.
            var arcCentre = new Vector2(34f, 16f);
            const float arcRadius = 22f;
            const float arcThickness = 5f;

            // Handle: a short bar running down-left from the arc's near end.
            var handleFrom = new Vector2(30f, 16f);
            var handleTo = new Vector2(16f, 4f);
            const float handleThickness = 4f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);

                    var bladeDistance = ArcDistance(point, arcCentre, arcRadius, arcThickness, 25f, 190f);
                    var handleDistance = SegmentDistance(point, handleFrom, handleTo) - handleThickness;

                    // Whichever shape the pixel is nearest decides its colour; the outline is just
                    // the same shapes grown outward, drawn underneath.
                    var inside = Mathf.Min(bladeDistance, handleDistance);

                    Color colour;
                    if (inside <= 0f) colour = bladeDistance <= handleDistance ? blade : handle;
                    else if (inside <= outlineWidth) colour = outline;
                    else colour = Color.clear;

                    pixels[y * size + x] = colour;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // Pivot at the handle's far end so the blade swings from the grip.
            return _sickle = Finish(texture, "AlphaTown Sickle", pixelsPerUnit: size,
                pivot: new Vector2(handleTo.x / size, handleTo.y / size));
        }

        // --- Drawing helpers --------------------------------------------------------------------

        /// <summary>
        /// Signed distance to a thick arc: negative inside, positive outside. Pixels outside the
        /// angular sweep fall back to the distance from the nearer end cap, which rounds the ends
        /// instead of chopping them square.
        /// </summary>
        static float ArcDistance(Vector2 point, Vector2 centre, float radius, float thickness,
                                 float startDegrees, float endDegrees)
        {
            var offset = point - centre;
            var angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            if (angle >= startDegrees && angle <= endDegrees)
                return Mathf.Abs(offset.magnitude - radius) - thickness * 0.5f;

            var start = centre + Radial(startDegrees) * radius;
            var end = centre + Radial(endDegrees) * radius;

            return Mathf.Min(Vector2.Distance(point, start), Vector2.Distance(point, end)) - thickness * 0.5f;
        }

        static Vector2 Radial(float degrees) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        /// <summary>Distance from a point to a line segment. Zero on the segment itself.</summary>
        static float SegmentDistance(Vector2 point, Vector2 from, Vector2 to)
        {
            var line = to - from;
            var lengthSquared = line.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon) return Vector2.Distance(point, from);

            var t = Mathf.Clamp01(Vector2.Dot(point - from, line) / lengthSquared);
            return Vector2.Distance(point, from + line * t);
        }

        static Texture2D NewTexture(int width, int height) =>
            new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

        static Sprite Finish(Texture2D texture, string name, float pixelsPerUnit, Vector2? pivot = null)
        {
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                pivot ?? new Vector2(0.5f, 0.5f),
                pixelsPerUnit);

            sprite.hideFlags = HideFlags.HideAndDontSave;
            sprite.name = name;
            return sprite;
        }
    }
}
