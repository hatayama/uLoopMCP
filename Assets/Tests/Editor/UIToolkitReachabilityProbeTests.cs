using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies which probe point, if any, reaches a UI Toolkit element given what the panels in front of it
    /// and its own panel pick at each point.
    /// </summary>
    public sealed class UIToolkitReachabilityProbeTests
    {
        private static readonly Vector2 Center = new Vector2(50f, 50f);
        private static readonly Vector2 Second = new Vector2(25f, 25f);

        private static readonly Vector2[] ProbePoints =
        {
            Center,
            Second,
            new Vector2(75f, 25f),
            new Vector2(25f, 75f),
            new Vector2(75f, 75f)
        };

        /// <summary>
        /// Verifies the center is returned when the own panel picks the target there.
        /// </summary>
        [Test]
        public void FindReachablePoint_TargetPickedAtCenter_ShouldReturnCenter()
        {
            VisualElement target = new VisualElement();

            Vector2? point = UIToolkitReachabilityProbe.FindReachablePoint(
                ProbePoints, target, NoFrontPanels(), _ => target);

            Assert.That(point, Is.EqualTo(Center));
        }

        /// <summary>
        /// Verifies a point taken by a front panel is skipped and the next point the target owns is returned.
        /// </summary>
        [Test]
        public void FindReachablePoint_FrontPanelTakesCenter_ShouldReturnSecondPoint()
        {
            VisualElement target = new VisualElement();
            VisualElement cover = new VisualElement();
            Func<Vector2, VisualElement> frontPanel = point => point == Center ? cover : null;

            Vector2? point = UIToolkitReachabilityProbe.FindReachablePoint(
                ProbePoints, target, new List<Func<Vector2, VisualElement>> { frontPanel }, _ => target);

            Assert.That(point, Is.EqualTo(Second));
        }

        /// <summary>
        /// Verifies picking a descendant of the target counts as reaching the target.
        /// </summary>
        [Test]
        public void FindReachablePoint_DescendantPicked_ShouldReturnCenter()
        {
            VisualElement target = new VisualElement();
            VisualElement child = new VisualElement();
            VisualElement grandChild = new VisualElement();
            target.hierarchy.Add(child);
            child.hierarchy.Add(grandChild);
            Func<Vector2, VisualElement> frontPanel = _ => null;

            Vector2? point = UIToolkitReachabilityProbe.FindReachablePoint(
                ProbePoints, target, new List<Func<Vector2, VisualElement>> { frontPanel }, _ => grandChild);

            Assert.That(point, Is.EqualTo(Center));
        }

        /// <summary>
        /// Verifies the target is unreachable when its own panel always picks another element, such as a sibling
        /// drawn on top or the target's parent.
        /// </summary>
        [Test]
        public void FindReachablePoint_OtherElementPickedEverywhere_ShouldReturnNull()
        {
            VisualElement parent = new VisualElement();
            VisualElement target = new VisualElement();
            parent.hierarchy.Add(target);

            Vector2? point = UIToolkitReachabilityProbe.FindReachablePoint(
                ProbePoints, target, NoFrontPanels(), _ => parent);

            Assert.That(point, Is.Null);
        }

        /// <summary>
        /// Verifies the target is unreachable when its own panel picks nothing at any point.
        /// </summary>
        [Test]
        public void FindReachablePoint_NothingPicked_ShouldReturnNull()
        {
            VisualElement target = new VisualElement();

            Vector2? point = UIToolkitReachabilityProbe.FindReachablePoint(
                ProbePoints, target, NoFrontPanels(), _ => null);

            Assert.That(point, Is.Null);
        }

        /// <summary>
        /// Verifies a front panel that picks something at every point hides the target even though its own
        /// panel would pick it.
        /// </summary>
        [Test]
        public void FindReachablePoint_FrontPanelTakesEveryPoint_ShouldReturnNull()
        {
            VisualElement target = new VisualElement();
            VisualElement cover = new VisualElement();
            Func<Vector2, VisualElement> frontPanel = _ => cover;

            Vector2? point = UIToolkitReachabilityProbe.FindReachablePoint(
                ProbePoints, target, new List<Func<Vector2, VisualElement>> { frontPanel }, _ => target);

            Assert.That(point, Is.Null);
        }

        private static List<Func<Vector2, VisualElement>> NoFrontPanels()
        {
            return new List<Func<Vector2, VisualElement>>();
        }
    }
}
