using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    /// <summary>
    /// Verifies how EventSystem results that come from uGUI's UI Toolkit bridge are handled when choosing a hit.
    /// The results are built by hand with real raycaster components as their module, because in EditMode the
    /// bridge raycaster never registers and returns nothing. No Canvas-space hit is passed, since comparing one
    /// reads Screen.
    /// </summary>
    public sealed class UiRaycastHelperPanelSkipTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        /// <summary>
        /// Verifies a bridge hit in first place is skipped and the next hit is chosen.
        /// </summary>
        [Test]
        public void ChooseHitSkippingUIToolkitPanels_WhenFirstHitIsPanelRaycaster_ReturnsNextHit()
        {
            RaycastResult panelHit = CreatePanelHit();
            RaycastResult graphicHit = CreateGraphicHit();

            RaycastResult? chosen = UiRaycastHelper.ChooseHitSkippingUIToolkitPanels(
                new List<RaycastResult> { panelHit, graphicHit }, null);

            Assert.That(chosen.HasValue, Is.True);
            Assert.That(chosen.Value.gameObject, Is.SameAs(graphicHit.gameObject));
        }

        /// <summary>
        /// Verifies only bridge hits leave nothing to choose.
        /// </summary>
        [Test]
        public void ChooseHitSkippingUIToolkitPanels_WhenOnlyPanelRaycaster_ReturnsNull()
        {
            RaycastResult? chosen = UiRaycastHelper.ChooseHitSkippingUIToolkitPanels(
                new List<RaycastResult> { CreatePanelHit() }, null);

            Assert.That(chosen.HasValue, Is.False);
        }

        /// <summary>
        /// Verifies the unfiltered choice still returns a bridge hit in first place, so other callers keep their
        /// current results.
        /// </summary>
        [Test]
        public void ChooseHit_WhenFirstHitIsPanelRaycaster_ReturnsIt()
        {
            RaycastResult panelHit = CreatePanelHit();

            RaycastResult? chosen = UiRaycastHelper.ChooseHit(
                new List<RaycastResult> { panelHit, CreateGraphicHit() }, null);

            Assert.That(chosen.HasValue, Is.True);
            Assert.That(chosen.Value.gameObject, Is.SameAs(panelHit.gameObject));
        }

        /// <summary>
        /// Verifies the type name the bridge results are recognised by matches the uGUI type.
        /// </summary>
        [Test]
        public void PanelRaycasterTypeFullName_MatchesUguiType()
        {
            Assert.That(UiRaycastHelper.PanelRaycasterTypeFullName, Is.EqualTo(typeof(PanelRaycaster).FullName));
        }

        private RaycastResult CreatePanelHit()
        {
            GameObject panelObject = CreateGameObject("Panel");
            return new RaycastResult { gameObject = panelObject, module = panelObject.AddComponent<PanelRaycaster>() };
        }

        private RaycastResult CreateGraphicHit()
        {
            GameObject canvasObject = CreateGameObject("Canvas");
            return new RaycastResult { gameObject = canvasObject, module = canvasObject.AddComponent<GraphicRaycaster>() };
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject("UiRaycastHelperPanelSkipTests_" + name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }
    }
}
