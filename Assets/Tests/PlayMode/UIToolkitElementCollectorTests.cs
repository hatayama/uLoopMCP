using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.PlayMode
{
    /// <summary>
    /// Verifies which UI Toolkit controls the annotator collects from live runtime panels and where it places
    /// them, in bottom-left screen pixels. PanelSettings.Reset gives every panel the project's default theme, so
    /// every element size is set explicitly to keep results independent of theme values. Every object is
    /// destroyed in TearDown.
    /// </summary>
    public sealed class UIToolkitElementCollectorTests
    {
        private const string Prefix = "UIToolkitElementCollectorTests_";
        private const float Tolerance = 1f;
        private const float Left = 100f;
        private const float Top = 50f;
        private const float Width = 200f;
        private const float Height = 40f;

        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.Destroy(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        /// <summary>
        /// Verifies a button on an overlay panel is reported with its bounds, its center as the click point,
        /// and the UI Toolkit tags.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_ButtonOnOverlayPanel_ShouldReportBoundsAndCenter()
        {
            UIDocument document = CreateDocument("Overlay", CreatePanelSettings(0f));
            AddPlaced(document.rootVisualElement, new Button { name = "play" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            List<UIElementInfo> matches = CollectFrom("Overlay");

            Assert.That(matches.Count, Is.EqualTo(1));
            UIElementInfo element = matches[0];
            float screenHeight = Screen.height;
            Assert.That(element.Name, Is.EqualTo("play"));
            Assert.That(element.Path, Is.EqualTo(Prefix + "Overlay#play"));
            Assert.That(element.Type, Is.EqualTo("Button"));
            Assert.That(element.Interaction, Is.EqualTo("Click"));
            Assert.That(element.UiSystem, Is.EqualTo("UIToolkit"));
            AssertBounds(element, Left, screenHeight - (Top + Height), Left + Width, screenHeight - Top);
            Assert.That(element.SimX, Is.EqualTo(Left + Width / 2f).Within(Tolerance));
            Assert.That(element.SimY, Is.EqualTo(screenHeight - (Top + Height / 2f)).Within(Tolerance));
        }

        /// <summary>
        /// Verifies a panel scale multiplies the reported screen bounds and click point.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_PanelScale_ShouldScaleBounds()
        {
            PanelSettings settings = CreatePanelSettings(0f);
            settings.scale = 2f;
            UIDocument document = CreateDocument("Scaled", settings);
            AddPlaced(document.rootVisualElement, new Button { name = "play" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            List<UIElementInfo> matches = CollectFrom("Scaled");

            Assert.That(matches.Count, Is.EqualTo(1));
            float screenHeight = Screen.height;
            AssertBounds(matches[0], Left * 2f, screenHeight - (Top + Height) * 2f, (Left + Width) * 2f, screenHeight - Top * 2f);
            Assert.That(matches[0].SimX, Is.EqualTo((Left + Width / 2f) * 2f).Within(Tolerance));
            Assert.That(matches[0].SimY, Is.EqualTo(screenHeight - (Top + Height / 2f) * 2f).Within(Tolerance));
        }

        /// <summary>
        /// Verifies a front panel covering the left 60% moves the click point to the first uncovered probe,
        /// the three-quarter point on the lower quarter line.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_FrontPanelCoversCenter_ShouldUseUncoveredProbe()
        {
            UIDocument back = CreateDocument("PartlyCovered", CreatePanelSettings(0f));
            AddPlaced(back.rootVisualElement, new Button { name = "target" }, Left, Top, Width, Height);
            UIDocument front = CreateDocument("PartlyCoveredFront", CreatePanelSettings(10f));
            AddPlaced(front.rootVisualElement, new VisualElement { name = "cover" }, Left, Top, Width * 0.6f, Height);
            yield return WaitForLayout();

            List<UIElementInfo> matches = CollectFrom("PartlyCovered#");

            Assert.That(matches.Count, Is.EqualTo(1));
            float screenHeight = Screen.height;
            Assert.That(matches[0].SimX, Is.EqualTo(Left + Width * 0.75f).Within(Tolerance));
            Assert.That(matches[0].SimY, Is.EqualTo(screenHeight - (Top + Height) + Height * 0.25f).Within(Tolerance));
        }

        /// <summary>
        /// Verifies a button fully covered by a front panel is left out.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_FrontPanelCoversAll_ShouldExclude()
        {
            UIDocument back = CreateDocument("Covered", CreatePanelSettings(0f));
            AddPlaced(back.rootVisualElement, new Button { name = "target" }, Left, Top, Width, Height);
            UIDocument front = CreateDocument("CoveredFront", CreatePanelSettings(10f));
            AddPlaced(front.rootVisualElement, new VisualElement { name = "cover" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            Assert.That(CollectFrom("Covered#").Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies a front panel that renders into a texture still covers a button, because Unity still routes
        /// screen input to it.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_FrontTargetTexturePanelCoversAll_ShouldExclude()
        {
            UIDocument back = CreateDocument("TextureCovered", CreatePanelSettings(0f));
            AddPlaced(back.rootVisualElement, new Button { name = "target" }, Left, Top, Width, Height);
            PanelSettings frontSettings = CreatePanelSettings(10f);
            frontSettings.targetTexture = CreateScreenSizedTexture();
            UIDocument front = CreateDocument("TextureCoveredFront", frontSettings);
            AddPlaced(front.rootVisualElement, new VisualElement { name = "cover" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            Assert.That(CollectFrom("TextureCovered#").Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies panels with the same sorting order do not hide each other's controls, since their real order
        /// is not public.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_SameSortingOrderPanelCovers_ShouldKeepBoth()
        {
            UIDocument first = CreateDocument("SameOrderFirst", CreatePanelSettings(5f));
            AddPlaced(first.rootVisualElement, new Button { name = "first" }, Left, Top, Width, Height);
            UIDocument second = CreateDocument("SameOrderSecond", CreatePanelSettings(5f));
            AddPlaced(second.rootVisualElement, new Button { name = "second" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            Assert.That(CollectFrom("SameOrderFirst").Count, Is.EqualTo(1));
            Assert.That(CollectFrom("SameOrderSecond").Count, Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies a button fully covered by a later sibling on the same panel is left out.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_SiblingCoversAll_ShouldExclude()
        {
            UIDocument document = CreateDocument("SiblingCovered", CreatePanelSettings(0f));
            AddPlaced(document.rootVisualElement, new Button { name = "target" }, Left, Top, Width, Height);
            AddPlaced(document.rootVisualElement, new VisualElement { name = "cover" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            Assert.That(CollectFrom("SiblingCovered").Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies a button placed entirely outside a parent with hidden overflow is left out, as it is clipped.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_ButtonOutsideClippingParent_ShouldExclude()
        {
            UIDocument document = CreateDocument("Clipped", CreatePanelSettings(0f));
            VisualElement clip = new VisualElement { name = "clip" };
            clip.style.overflow = Overflow.Hidden;
            AddPlaced(document.rootVisualElement, clip, Left, Top, Width, Height);
            AddPlaced(clip, new Button { name = "target" }, 300f, 0f, Width, Height);
            yield return WaitForLayout();

            Assert.That(CollectFrom("Clipped").Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies a disabled button is left out.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_DisabledButton_ShouldExclude()
        {
            UIDocument document = CreateDocument("Disabled", CreatePanelSettings(0f));
            Button button = new Button { name = "target" };
            button.SetEnabled(false);
            AddPlaced(document.rootVisualElement, button, Left, Top, Width, Height);
            yield return WaitForLayout();

            Assert.That(CollectFrom("Disabled").Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies a button under a display:none ancestor is left out.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_DisplayNoneAncestor_ShouldExclude()
        {
            UIDocument document = CreateDocument("DisplayNone", CreatePanelSettings(0f));
            VisualElement hiddenParent = new VisualElement { name = "hidden-parent" };
            AddPlaced(document.rootVisualElement, hiddenParent, Left, Top, Width, Height);
            hiddenParent.style.display = DisplayStyle.None;
            AddPlaced(hiddenParent, new Button { name = "target" }, 0f, 0f, Width, Height);
            yield return WaitForLayout();

            Assert.That(CollectFrom("DisplayNone").Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies a button with hidden visibility is left out.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_HiddenButton_ShouldExclude()
        {
            UIDocument document = CreateDocument("Hidden", CreatePanelSettings(0f));
            Button button = new Button { name = "target" };
            button.style.visibility = Visibility.Hidden;
            AddPlaced(document.rootVisualElement, button, Left, Top, Width, Height);
            yield return WaitForLayout();

            Assert.That(CollectFrom("Hidden").Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies a button added in the same frame as the collection is reported with laid-out bounds.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_ButtonAddedInSameFrame_ShouldReportLaidOutBounds()
        {
            UIDocument document = CreateDocument("SameFrame", CreatePanelSettings(0f));
            yield return WaitForLayout();
            AddPlaced(document.rootVisualElement, new Button { name = "late" }, Left, Top, Width, Height);

            List<UIElementInfo> matches = CollectFrom("SameFrame");

            Assert.That(matches.Count, Is.EqualTo(1));
            float screenHeight = Screen.height;
            AssertBounds(matches[0], Left, screenHeight - (Top + Height), Left + Width, screenHeight - Top);
        }

        /// <summary>
        /// Verifies a scroll view yields its scroller once, without the slider and buttons inside the scroller.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_ScrollView_ShouldReportScrollerOnly()
        {
            UIDocument document = CreateDocument("Scroll", CreatePanelSettings(0f));
            ScrollView scrollView = new ScrollView { name = "list" };
            scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            AddPlaced(document.rootVisualElement, scrollView, Left, Top, Width, 100f);
            Scroller scroller = scrollView.verticalScroller;
            SetSize(scroller, 20f, 100f);
            SetSize(scroller.lowButton, 20f, 20f);
            SetSize(scroller.highButton, 20f, 20f);
            SetSize(scroller.slider, 20f, 60f);
            // The scroll view disables a scroller whose content fits, so the viewport is sized and given
            // content taller than itself that does not shrink to fit.
            SetSize(scrollView.contentViewport, 180f, 100f);
            VisualElement content = new VisualElement { name = "content" };
            SetSize(content, 180f, 500f);
            content.style.flexShrink = 0f;
            scrollView.Add(content);
            yield return WaitForLayout();
            Assert.That(scroller.worldBound.width, Is.GreaterThan(0f));
            Assert.That(scroller.worldBound.height, Is.GreaterThan(0f));
            Assert.That(scroller.enabledInHierarchy, Is.True);

            List<UIElementInfo> matches = CollectFrom("Scroll");

            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].Type, Is.EqualTo("Scrollbar"));
        }

        /// <summary>
        /// Verifies controls on a panel that renders into a texture are not listed, as they are not on screen.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_TargetTexturePanel_ShouldNotListElements()
        {
            PanelSettings settings = CreatePanelSettings(0f);
            settings.targetTexture = CreateScreenSizedTexture();
            UIDocument document = CreateDocument("Texture", settings);
            AddPlaced(document.rootVisualElement, new Button { name = "target" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            Assert.That(CollectFrom("Texture").Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies controls on a panel shown on a display other than the Game View's are left out.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_PanelOnOtherDisplay_ShouldExclude()
        {
            PanelSettings settings = CreatePanelSettings(0f);
            settings.targetDisplay = 1;
            UIDocument document = CreateDocument("OtherDisplay", settings);
            AddPlaced(document.rootVisualElement, new Button { name = "target" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            Assert.That(CollectFrom("OtherDisplay").Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies two documents sharing one panel each contribute their button.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_DocumentsSharingPanel_ShouldReportBoth()
        {
            PanelSettings settings = CreatePanelSettings(0f);
            UIDocument first = CreateDocument("SharedFirst", settings);
            AddPlaced(first.rootVisualElement, new Button { name = "first" }, Left, Top, Width, Height);
            UIDocument second = CreateDocument("SharedSecond", settings);
            AddPlaced(second.rootVisualElement, new Button { name = "second" }, Left, Top + 100f, Width, Height);
            yield return WaitForLayout();

            Assert.That(CollectFrom("SharedFirst").Count, Is.EqualTo(1));
            Assert.That(CollectFrom("SharedSecond").Count, Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies a button in a nested document is reported once, through the parent document.
        /// </summary>
        [UnityTest]
        public IEnumerator Collect_NestedDocument_ShouldReportOnce()
        {
            UIDocument parent = CreateDocument("NestedParent", CreatePanelSettings(0f));
            GameObject childObject = new GameObject(Prefix + "NestedChild");
            childObject.SetActive(false);
            childObject.transform.SetParent(parent.transform, false);
            UIDocument child = childObject.AddComponent<UIDocument>();
            childObject.SetActive(true);
            Assume.That(child.parentUI, Is.EqualTo(parent));
            AddPlaced(child.rootVisualElement, new Button { name = "nested" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            List<UIElementInfo> matches = UIElementAnnotator.CollectInteractiveElements()
                .FindAll(element => element.Name == "nested" && element.UiSystem == "UIToolkit");

            Assert.That(matches.Count, Is.EqualTo(1));
        }

        private PanelSettings CreatePanelSettings(float sortingOrder)
        {
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.sortingOrder = sortingOrder;
            _createdObjects.Add(settings);
            return settings;
        }

        private RenderTexture CreateScreenSizedTexture()
        {
            RenderTexture texture = new RenderTexture(Screen.width, Screen.height, 0);
            _createdObjects.Add(texture);
            return texture;
        }

        // The object starts inactive so the document sees its panel settings when it first enables.
        private UIDocument CreateDocument(string name, PanelSettings settings)
        {
            GameObject documentObject = new GameObject(Prefix + name);
            documentObject.SetActive(false);
            _createdObjects.Add(documentObject);
            UIDocument document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            documentObject.SetActive(true);
            return document;
        }

        private static void AddPlaced(VisualElement parent, VisualElement element, float left, float top, float width, float height)
        {
            element.style.position = Position.Absolute;
            element.style.left = left;
            element.style.top = top;
            SetSize(element, width, height);
            parent.Add(element);
        }

        private static void SetSize(VisualElement element, float width, float height)
        {
            element.style.width = width;
            element.style.height = height;
        }

        // The root's size is set by the runtime panel update in the player loop, so frames must pass.
        private static IEnumerator WaitForLayout()
        {
            yield return null;
            yield return null;
        }

        private static List<UIElementInfo> CollectFrom(string pathStart)
        {
            return UIElementAnnotator.CollectInteractiveElements()
                .FindAll(element => element.UiSystem == "UIToolkit" && element.Path.StartsWith(Prefix + pathStart));
        }

        private static void AssertBounds(UIElementInfo element, float minX, float minY, float maxX, float maxY)
        {
            Assert.That(element.BoundsMinX, Is.EqualTo(minX).Within(Tolerance));
            Assert.That(element.BoundsMinY, Is.EqualTo(minY).Within(Tolerance));
            Assert.That(element.BoundsMaxX, Is.EqualTo(maxX).Within(Tolerance));
            Assert.That(element.BoundsMaxY, Is.EqualTo(maxY).Within(Tolerance));
        }
    }
}
