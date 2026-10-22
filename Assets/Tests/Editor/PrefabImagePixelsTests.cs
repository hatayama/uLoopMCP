using System;
using NUnit.Framework;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies how a rendered prefab image is judged empty and how an opaque background is composited under it.
    /// </summary>
    public sealed class PrefabImagePixelsTests
    {
        private static readonly Color32 Gray = new(128, 128, 128, 255);
        private static readonly Color32 Transparent = new(0, 0, 0, 0);

        /// <summary>
        /// Verifies an image whose pixels are all fully transparent counts as nothing drawn.
        /// </summary>
        [Test]
        public void HasDrawnPixel_WithOnlyTransparentPixels_ReturnsFalse()
        {
            Color32[] pixels = Filled(Transparent, 4);

            Assert.That(PrefabImagePixels.HasDrawnPixel(pixels, Gray), Is.False);
        }

        /// <summary>
        /// Verifies pixels within one step of the clear color count as background, not as drawn content.
        /// </summary>
        [Test]
        public void HasDrawnPixel_WithOnlyTheClearColorWithinOneStep_ReturnsFalse()
        {
            Color32[] pixels = Filled(Gray, 4);
            pixels[1] = new Color32(129, 128, 128, 255);

            Assert.That(PrefabImagePixels.HasDrawnPixel(pixels, Gray), Is.False);
        }

        /// <summary>
        /// Verifies one opaque pixel that differs from the clear color counts as drawn.
        /// </summary>
        [Test]
        public void HasDrawnPixel_WithOneDrawnPixel_ReturnsTrue()
        {
            Color32[] pixels = Filled(Transparent, 4);
            pixels[2] = new Color32(255, 0, 0, 255);

            Assert.That(PrefabImagePixels.HasDrawnPixel(pixels, Gray), Is.True);
        }

        /// <summary>
        /// Verifies a fully transparent pixel becomes the background color in gamma blending.
        /// </summary>
        [Test]
        public void FillBackground_WithATransparentPixel_UsesTheBackground()
        {
            Color32[] pixels = { Transparent };

            PrefabImagePixels.FillBackground(pixels, Gray, false);

            AssertColor32Near(pixels[0], Gray, 0);
        }

        /// <summary>
        /// Verifies an opaque pixel is left unchanged.
        /// </summary>
        [Test]
        public void FillBackground_WithAnOpaquePixel_LeavesItUnchanged()
        {
            Color32 red = new(255, 0, 0, 255);
            Color32[] pixels = { red };

            PrefabImagePixels.FillBackground(pixels, Gray, false);

            AssertColor32Near(pixels[0], red, 0);
        }

        /// <summary>
        /// Verifies a premultiplied translucent pixel is composited over the background in gamma space.
        /// </summary>
        [Test]
        public void FillBackground_WithATranslucentPixelInGamma_CompositesPremultipliedOver()
        {
            Color32[] pixels = { new Color32(100, 50, 25, 128) };

            PrefabImagePixels.FillBackground(pixels, Gray, false);

            AssertColor32Near(pixels[0], new Color32(164, 114, 89, 255), 0);
        }

        /// <summary>
        /// Verifies linear blending reproduces what the GPU draws for translucent white over opaque gray in a Linear project.
        /// </summary>
        [Test]
        public void FillBackground_WithATranslucentPixelInLinear_MatchesTheGpuComposite()
        {
            Color32[] pixels = { new Color32(188, 188, 188, 128) };

            PrefabImagePixels.FillBackground(pixels, Gray, true);

            AssertColor32Near(pixels[0], new Color32(205, 205, 205, 255), 1);
        }

        /// <summary>
        /// Verifies a fully transparent pixel becomes the background color in linear blending.
        /// </summary>
        [Test]
        public void FillBackground_WithATransparentPixelInLinear_UsesTheBackground()
        {
            Color32[] pixels = { Transparent };

            PrefabImagePixels.FillBackground(pixels, Gray, true);

            AssertColor32Near(pixels[0], Gray, 1);
        }

        /// <summary>
        /// Verifies a background that is not fully opaque is refused, since compositing it would double a pipeline-drawn background.
        /// </summary>
        [Test]
        public void FillBackground_WithATranslucentBackground_Throws()
        {
            Color32[] pixels = { Transparent };

            Assert.Throws<ArgumentException>(
                () => PrefabImagePixels.FillBackground(pixels, new Color32(128, 128, 128, 254), false));
        }

        private static Color32[] Filled(Color32 color, int count)
        {
            Color32[] pixels = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                pixels[i] = color;
            }

            return pixels;
        }

        private static void AssertColor32Near(Color32 actual, Color32 expected, int tolerance)
        {
            Assert.That(actual.r, Is.InRange(expected.r - tolerance, expected.r + tolerance), "r");
            Assert.That(actual.g, Is.InRange(expected.g - tolerance, expected.g + tolerance), "g");
            Assert.That(actual.b, Is.InRange(expected.b - tolerance, expected.b + tolerance), "b");
            Assert.That(actual.a, Is.InRange(expected.a - tolerance, expected.a + tolerance), "a");
        }
    }
}
