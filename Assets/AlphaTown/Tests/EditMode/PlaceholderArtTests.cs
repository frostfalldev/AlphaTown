using AlphaTown.UI.View;
using NUnit.Framework;
using UnityEngine;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// The sickle is drawn in code, and drawn art fails silently: one wrong angle and the texture
    /// comes out empty, which on a device looks exactly like the tool not arming. These check that
    /// something is actually on the canvas.
    /// </summary>
    public sealed class PlaceholderArtTests
    {
        [Test]
        public void SolidIsOpaqueWhite()
        {
            var sprite = PlaceholderArt.Solid();

            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.texture.GetPixel(0, 0), Is.EqualTo(Color.white));
        }

        [Test]
        public void TheSickleActuallyDrawsSomething()
        {
            var sprite = PlaceholderArt.Sickle();
            Assert.That(sprite, Is.Not.Null);

            var pixels = sprite.texture.GetPixels();
            var opaque = 0;
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0.5f) opaque++;
            }

            // A blade and a handle across a 64x64 canvas. Far too few means the arc drew nothing;
            // far too many means the shape test inverted and filled the square.
            Assert.That(opaque, Is.InRange(pixels.Length / 20, pixels.Length / 2));
        }

        [Test]
        public void TheSickleSwingsFromItsHandle()
        {
            var sprite = PlaceholderArt.Sickle();

            // Pivot low and to the left — the grip, not the middle. Rotating about the centre
            // would spin the blade rather than swing it.
            Assert.That(sprite.pivot.x / sprite.rect.width, Is.LessThan(0.4f));
            Assert.That(sprite.pivot.y / sprite.rect.height, Is.LessThan(0.4f));
        }

        [Test]
        public void GeneratedSpritesAreReused()
        {
            Assert.That(PlaceholderArt.Sickle(), Is.SameAs(PlaceholderArt.Sickle()));
            Assert.That(PlaceholderArt.Solid(), Is.SameAs(PlaceholderArt.Solid()));
        }
    }
}
