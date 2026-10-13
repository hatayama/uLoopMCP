using NUnit.Framework;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies which screen-to-panel functions can be inverted, and that the inversion maps panel points
    /// back to the screen points they came from.
    /// </summary>
    public sealed class UIToolkitScreenMappingTests
    {
        /// <summary>
        /// Verifies a pure scale is inverted by multiplying panel points back up.
        /// </summary>
        [Test]
        public void TryCreate_WithHalfScale_ShouldDoublePanelPoints()
        {
            UIToolkitScreenMapping mapping = UIToolkitScreenMapping.TryCreate(screen => screen / 2f);

            Assert.That(mapping, Is.Not.Null);
            Vector2 screen = mapping.PanelToScreenTopLeft(new Vector2(50f, 20f));
            Assert.That(screen.x, Is.EqualTo(100f).Within(0.001f));
            Assert.That(screen.y, Is.EqualTo(40f).Within(0.001f));
        }

        /// <summary>
        /// Verifies an offset plus a scale is inverted so that several screen points round-trip.
        /// </summary>
        [Test]
        public void TryCreate_WithOffsetAndScale_ShouldRoundTripScreenPoints()
        {
            Vector2 offset = new Vector2(10f, 20f);
            UIToolkitScreenMapping mapping = UIToolkitScreenMapping.TryCreate(screen => (screen - offset) / 2f);

            Assert.That(mapping, Is.Not.Null);
            Vector2[] screenPoints = { new Vector2(0f, 0f), new Vector2(130f, 75f), new Vector2(1920f, 1080f) };
            foreach (Vector2 screenPoint in screenPoints)
            {
                Vector2 roundTrip = mapping.PanelToScreenTopLeft((screenPoint - offset) / 2f);
                Assert.That(roundTrip.x, Is.EqualTo(screenPoint.x).Within(0.01f));
                Assert.That(roundTrip.y, Is.EqualTo(screenPoint.y).Within(0.01f));
            }
        }

        /// <summary>
        /// Verifies a function that is not linear along an axis is rejected.
        /// </summary>
        [Test]
        public void TryCreate_WithNonLinearFunction_ShouldReturnNull()
        {
            UIToolkitScreenMapping mapping = UIToolkitScreenMapping.TryCreate(
                screen => new Vector2(screen.x * screen.x / 1000f, screen.y));

            Assert.That(mapping, Is.Null);
        }

        /// <summary>
        /// Verifies a shear, which mixes the axes, is rejected.
        /// </summary>
        [Test]
        public void TryCreate_WithShear_ShouldReturnNull()
        {
            UIToolkitScreenMapping mapping = UIToolkitScreenMapping.TryCreate(
                screen => new Vector2(screen.x + 0.5f * screen.y, screen.y));

            Assert.That(mapping, Is.Null);
        }

        /// <summary>
        /// Verifies a function that swaps the axes is rejected as degenerate.
        /// </summary>
        [Test]
        public void TryCreate_WithSwappedAxes_ShouldReturnNull()
        {
            UIToolkitScreenMapping mapping = UIToolkitScreenMapping.TryCreate(screen => new Vector2(screen.y, screen.x));

            Assert.That(mapping, Is.Null);
        }

        /// <summary>
        /// Verifies a function that mirrors an axis is rejected, since inverting it would yield bounds whose
        /// minimum exceeds their maximum.
        /// </summary>
        [Test]
        public void TryCreate_WithMirroredAxis_ShouldReturnNull()
        {
            UIToolkitScreenMapping mapping = UIToolkitScreenMapping.TryCreate(
                screen => new Vector2(1000f - screen.x, screen.y));

            Assert.That(mapping, Is.Null);
        }

        /// <summary>
        /// Verifies a constant function, which has no inverse, is rejected.
        /// </summary>
        [Test]
        public void TryCreate_WithConstantFunction_ShouldReturnNull()
        {
            UIToolkitScreenMapping mapping = UIToolkitScreenMapping.TryCreate(screen => new Vector2(5f, 5f));

            Assert.That(mapping, Is.Null);
        }

        /// <summary>
        /// Verifies functions returning NaN or infinity, as a zero panel scale does, are rejected.
        /// </summary>
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TryCreate_WithNonFiniteResult_ShouldReturnNull(float nonFinite)
        {
            UIToolkitScreenMapping mapping = UIToolkitScreenMapping.TryCreate(screen => new Vector2(nonFinite, screen.y));

            Assert.That(mapping, Is.Null);
        }
    }
}
