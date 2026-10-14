using NUnit.Framework;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies which of an overlapping uGUI hit and UI Toolkit hit takes the input, following the EventSystem's
    /// raycast ordering.
    /// </summary>
    public sealed class MouseUiHitPriorityTests
    {
        /// <summary>
        /// Verifies a Canvas sorted above the panel puts uGUI in front.
        /// </summary>
        [Test]
        public void IsUguiInFront_WhenCanvasSortsAbovePanel_ReturnsTrue()
        {
            Assert.That(MouseUiHitPriority.IsUguiInFront(11, 10f), Is.True);
        }

        /// <summary>
        /// Verifies equal sort priorities leave UI Toolkit in front, since the panel's render order priority is
        /// nearly int.MaxValue.
        /// </summary>
        [TestCase(10, 10f)]
        [TestCase(10, 10.5f)]
        [TestCase(-1, -0.5f)]
        public void IsUguiInFront_WhenSortPrioritiesAreEqual_ReturnsFalse(int uguiSortOrderPriority, float panelSortingOrder)
        {
            Assert.That(MouseUiHitPriority.IsUguiInFront(uguiSortOrderPriority, panelSortingOrder), Is.False);
        }

        /// <summary>
        /// Verifies a fractional panel sorting order is rounded down, as PanelRaycaster does, so a Canvas just
        /// above the rounded value comes in front; truncating or rounding up or to nearest would not.
        /// </summary>
        [TestCase(0, -0.5f)]
        [TestCase(10, 9.5f)]
        public void IsUguiInFront_WhenPanelSortingOrderIsFractional_RoundsDown(int uguiSortOrderPriority, float panelSortingOrder)
        {
            Assert.That(MouseUiHitPriority.IsUguiInFront(uguiSortOrderPriority, panelSortingOrder), Is.True);
        }

        /// <summary>
        /// Verifies a panel sorted above the Canvas stays in front.
        /// </summary>
        [Test]
        public void IsUguiInFront_WhenPanelSortsAboveCanvas_ReturnsFalse()
        {
            Assert.That(MouseUiHitPriority.IsUguiInFront(9, 10f), Is.False);
        }

        /// <summary>
        /// Verifies raycasters that keep the base priority, such as physics raycasters and non-overlay Canvases,
        /// never come in front of a panel.
        /// </summary>
        [Test]
        public void IsUguiInFront_WithBaseRaycasterPriority_ReturnsFalse()
        {
            Assert.That(MouseUiHitPriority.IsUguiInFront(int.MinValue, -1000f), Is.False);
        }
    }
}
