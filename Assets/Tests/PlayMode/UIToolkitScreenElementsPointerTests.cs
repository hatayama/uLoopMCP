using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.PlayMode
{
    /// <summary>
    /// Verifies which UI Toolkit control a Game View point resolves to on live runtime panels, and that presses,
    /// moves and releases sent through the result reach the panel like real mouse input. Points are top-left
    /// Game View pixels; panels use a scale of 1, so they equal panel coordinates. Every object is destroyed in
    /// TearDown.
    /// </summary>
    public sealed class UIToolkitScreenElementsPointerTests
    {
        private const string Prefix = "UIToolkitScreenElementsPointerTests_";
        private const float Left = 100f;
        private const float Top = 50f;
        private const float Width = 200f;
        private const float Height = 40f;
        private const string RuntimeThemePath = "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss";

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
        /// Verifies that where two panels overlap, the control on the panel with the higher sorting order is
        /// returned and the one behind it is not.
        /// </summary>
        [UnityTest]
        public IEnumerator FindPointerTarget_WherePanelsOverlap_ReturnsFrontPanelControl()
        {
            UIDocument front = CreateDocument("Front", CreatePanelSettings(10f));
            AddPlaced(front.rootVisualElement, new Button { name = "front" }, Left, Top, Width, Height);
            UIDocument back = CreateDocument("Back", CreatePanelSettings(0f));
            AddPlaced(back.rootVisualElement, new Button { name = "back" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            UIToolkitPointerTarget target = UIToolkitScreenElements.FindPointerTarget(Center(Left, Top, Width, Height));

            Assert.That(target, Is.Not.Null);
            Assert.That(target.Name, Is.EqualTo("front"));
            Assert.That(target.Path, Is.EqualTo(Prefix + "Front#front"));
            Assert.That(target.SortingOrder, Is.EqualTo(10f));
        }

        /// <summary>
        /// Verifies a point on a button reports the button's name and its annotation-style path.
        /// </summary>
        [UnityTest]
        public IEnumerator FindPointerTarget_OnButton_ReportsNameAndPath()
        {
            UIDocument document = CreateDocument("Doc", CreatePanelSettings(0f));
            AddPlaced(document.rootVisualElement, new Button { name = "play" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            UIToolkitPointerTarget target = UIToolkitScreenElements.FindPointerTarget(Center(Left, Top, Width, Height));

            Assert.That(target, Is.Not.Null);
            Assert.That(target.Name, Is.EqualTo("play"));
            Assert.That(target.Path, Is.EqualTo(Prefix + "Doc#play"));
        }

        /// <summary>
        /// Verifies a point on a part inside a toggle still reports the toggle.
        /// </summary>
        [UnityTest]
        public IEnumerator FindPointerTarget_OnPartInsideToggle_ReportsToggle()
        {
            UIDocument document = CreateDocument("Toggle", CreatePanelSettings(0f));
            Toggle toggle = new Toggle { name = "sound" };
            AddPlaced(document.rootVisualElement, toggle, Left, Top, Width, Height);
            yield return WaitForLayout();
            VisualElement checkmark = toggle.Q(className: Toggle.checkmarkUssClassName);
            Vector2 point = checkmark.worldBound.center;
            Assert.That(document.rootVisualElement.panel.Pick(point), Is.Not.SameAs(toggle), "the point must hit a part inside the toggle");

            UIToolkitPointerTarget target = UIToolkitScreenElements.FindPointerTarget(point);

            Assert.That(target, Is.Not.Null);
            Assert.That(target.Name, Is.EqualTo("sound"));
            Assert.That(target.Path, Is.EqualTo(Prefix + "Toggle#sound"));
        }

        /// <summary>
        /// Verifies points on the dragger and both arrow buttons of a scroll view's scroller all report the
        /// scroller with the same path screenshot annotations list for it, not the slider or buttons inside.
        /// </summary>
        [UnityTest]
        public IEnumerator FindPointerTarget_OnScrollerParts_ReportsScrollerWithAnnotationPath()
        {
            UIDocument document = CreateDocument("Scroll", CreatePanelSettings(0f));
            ScrollView scrollView = CreateScrollView(document.rootVisualElement);
            yield return WaitForLayout();
            Scroller scroller = scrollView.verticalScroller;
            UIElementInfo annotated = FindAnnotated("Scroll", "Scrollbar");
            VisualElement[] parts = { scroller.slider.Q("unity-dragger"), scroller.lowButton, scroller.highButton };

            foreach (VisualElement part in parts)
            {
                Vector2 point = part.worldBound.center;
                Assert.That(document.rootVisualElement.panel.Pick(point), Is.Not.SameAs(scroller), "the point must hit a part inside the scroller");

                UIToolkitPointerTarget target = UIToolkitScreenElements.FindPointerTarget(point);

                Assert.That(target, Is.Not.Null, part.name);
                Assert.That(target.Path, Is.EqualTo(annotated.Path), part.name);
                Assert.That(target.Name, Is.EqualTo(annotated.Name), part.name);
            }
        }

        /// <summary>
        /// Verifies a point on the input field of a slider that shows one reports the slider.
        /// </summary>
        [UnityTest]
        public IEnumerator FindPointerTarget_OnSliderInputField_ReportsSlider()
        {
            UIDocument document = CreateDocument("Slider", CreatePanelSettings(0f));
            Slider slider = new Slider(0f, 1f) { name = "volume", showInputField = true };
            AddPlaced(document.rootVisualElement, slider, Left, Top, 300f, 30f);
            yield return WaitForLayout();
            TextField inputField = slider.Q<TextField>();
            Vector2 point = inputField.worldBound.center;
            VisualElement picked = document.rootVisualElement.panel.Pick(point);
            Assert.That(picked == inputField || inputField.Contains(picked), Is.True, "the point must hit the input field");

            UIToolkitPointerTarget target = UIToolkitScreenElements.FindPointerTarget(point);

            Assert.That(target, Is.Not.Null);
            Assert.That(target.Path, Is.EqualTo(Prefix + "Slider#volume"));
        }

        /// <summary>
        /// Verifies a point with no element under it returns null.
        /// </summary>
        [UnityTest]
        public IEnumerator FindPointerTarget_OnEmptyPoint_ReturnsNull()
        {
            UIDocument document = CreateDocument("Empty", CreatePanelSettings(0f));
            AddPlaced(document.rootVisualElement, new Button { name = "play" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            Assert.That(UIToolkitScreenElements.FindPointerTarget(new Vector2(Left + Width + 50f, Top + Height + 50f)), Is.Null);
        }

        /// <summary>
        /// Verifies a front panel that renders into a texture still takes the point, as Unity still routes
        /// screen input to it.
        /// </summary>
        [UnityTest]
        public IEnumerator FindPointerTarget_FrontPanelWithTargetTexture_ReturnsItsControl()
        {
            UIDocument back = CreateDocument("TextureBack", CreatePanelSettings(0f));
            AddPlaced(back.rootVisualElement, new Button { name = "target" }, Left, Top, Width, Height);
            PanelSettings frontSettings = CreatePanelSettings(10f);
            frontSettings.targetTexture = CreateScreenSizedTexture();
            UIDocument front = CreateDocument("TextureFront", frontSettings);
            AddPlaced(front.rootVisualElement, new Button { name = "cover" }, Left, Top, Width, Height);
            yield return WaitForLayout();

            UIToolkitPointerTarget target = UIToolkitScreenElements.FindPointerTarget(Center(Left, Top, Width, Height));

            Assert.That(target, Is.Not.Null);
            Assert.That(target.Name, Is.EqualTo("cover"));
        }

        /// <summary>
        /// Verifies a left press and release click a button once and leave no element holding the pointer.
        /// </summary>
        [UnityTest]
        public IEnumerator PressDownThenRelease_OnButton_ClicksOnceAndLeavesNoCapture()
        {
            UIDocument document = CreateDocument("Click", CreatePanelSettings(0f));
            Button button = new Button { name = "play" };
            int clicks = 0;
            button.clicked += () => clicks++;
            AddPlaced(document.rootVisualElement, button, Left, Top, Width, Height);
            yield return WaitForLayout();
            Vector2 point = Center(Left, Top, Width, Height);
            UIToolkitPointerTarget target = UIToolkitScreenElements.FindPointerTarget(point);
            Assert.That(target, Is.Not.Null);

            target.PressDown(point, 0);
            target.Release(point, 0);

            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(document.rootVisualElement.panel.GetCapturingElement(PointerId.mousePointerId), Is.Null);
        }

        /// <summary>
        /// Verifies that once the target's document is disabled the target reports itself unavailable, and
        /// presses, moves and releases throw nothing and send nothing to the panel, which another document
        /// still shows.
        /// </summary>
        [UnityTest]
        public IEnumerator PointerCalls_AfterDocumentDisabled_SendNothing()
        {
            PanelSettings sharedSettings = CreatePanelSettings(0f);
            UIDocument disabled = CreateDocument("Disabled", sharedSettings);
            AddPlaced(disabled.rootVisualElement, new Button { name = "gone" }, Left, Top, Width, Height);
            UIDocument remaining = CreateDocument("Remaining", sharedSettings);
            Button witness = new Button { name = "witness" };
            AddPlaced(remaining.rootVisualElement, witness, Left, Top + 100f, Width, Height);
            int received = 0;
            witness.RegisterCallback<PointerDownEvent>(_ => received++, TrickleDown.TrickleDown);
            witness.RegisterCallback<PointerMoveEvent>(_ => received++, TrickleDown.TrickleDown);
            witness.RegisterCallback<PointerUpEvent>(_ => received++, TrickleDown.TrickleDown);
            yield return WaitForLayout();
            UIToolkitPointerTarget target = UIToolkitScreenElements.FindPointerTarget(Center(Left, Top, Width, Height));
            Assert.That(target, Is.Not.Null);
            Vector2 witnessPoint = Center(Left, Top + 100f, Width, Height);

            disabled.enabled = false;
            yield return null;

            Assert.That(target.IsAvailable, Is.False);
            Assert.DoesNotThrow(() => target.PressDown(witnessPoint, 0));
            Assert.DoesNotThrow(() => target.MoveTo(witnessPoint));
            Assert.DoesNotThrow(() => target.Release(witnessPoint, 0));
            Assert.That(received, Is.EqualTo(0));

            // The callbacks do fire when a target that is still available sends to the same point.
            UIToolkitPointerTarget witnessTarget = UIToolkitScreenElements.FindPointerTarget(witnessPoint);
            witnessTarget.PressDown(witnessPoint, 0);
            witnessTarget.Release(witnessPoint, 0);
            Assert.That(received, Is.GreaterThan(0));
        }

        /// <summary>
        /// Verifies a right press and release reach the button as button 1 and do not click it.
        /// </summary>
        [UnityTest]
        public IEnumerator PressDownThenRelease_WithRightButton_DoesNotClick()
        {
            UIDocument document = CreateDocument("Right", CreatePanelSettings(0f));
            Button button = new Button { name = "play" };
            int clicks = 0;
            int pressedButton = -1;
            button.clicked += () => clicks++;
            button.RegisterCallback<PointerDownEvent>(evt => pressedButton = evt.button, TrickleDown.TrickleDown);
            AddPlaced(document.rootVisualElement, button, Left, Top, Width, Height);
            yield return WaitForLayout();
            Vector2 point = Center(Left, Top, Width, Height);
            UIToolkitPointerTarget target = UIToolkitScreenElements.FindPointerTarget(point);
            Assert.That(target, Is.Not.Null);

            target.PressDown(point, 1);
            target.Release(point, 1);

            Assert.That(pressedButton, Is.EqualTo(1));
            Assert.That(clicks, Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies an item of an open dropdown menu, which belongs to no document, resolves with a null path
        /// and pressing it changes the dropdown's value.
        /// </summary>
        [UnityTest]
        public IEnumerator PressOnOpenDropdownMenuItem_ChangesValueAndHasNoPath()
        {
            UIDocument document = CreateDocument("Dropdown", CreatePanelSettings(0f));
            DropdownField dropdown = new DropdownField(new List<string> { "low", "high" }, 0) { name = "quality" };
            AddPlaced(document.rootVisualElement, dropdown, Left, Top, Width, 30f);
            yield return WaitForLayout();
            Vector2 dropdownPoint = Center(Left, Top, Width, 30f);
            UIToolkitPointerTarget opener = UIToolkitScreenElements.FindPointerTarget(dropdownPoint);
            Assert.That(opener, Is.Not.Null);
            Assert.That(opener.Path, Is.EqualTo(Prefix + "Dropdown#quality"));
            opener.PressDown(dropdownPoint, 0);
            opener.Release(dropdownPoint, 0);
            yield return WaitForLayout();
            List<VisualElement> items = document.rootVisualElement.panel.visualTree
                .Query(className: GenericDropdownMenu.itemUssClassName).ToList();
            Assert.That(items.Count, Is.EqualTo(2), "the dropdown menu must be open");
            Vector2 itemPoint = items[1].worldBound.center;

            UIToolkitPointerTarget item = UIToolkitScreenElements.FindPointerTarget(itemPoint);

            Assert.That(item, Is.Not.Null);
            Assert.That(item.Path, Is.Null);
            item.PressDown(itemPoint, 0);
            item.Release(itemPoint, 0);
            Assert.That(dropdown.value, Is.EqualTo("high"));
        }

        private ScrollView CreateScrollView(VisualElement root)
        {
            ScrollView scrollView = new ScrollView { name = "list" };
            scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            AddPlaced(root, scrollView, Left, Top, Width, 100f);
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
            return scrollView;
        }

        private static UIElementInfo FindAnnotated(string documentName, string type)
        {
            List<UIElementInfo> elements = new List<UIElementInfo>();
            UIToolkitElementCollector.Collect(elements, Handles.GetMainGameViewSize().y);
            List<UIElementInfo> matches = elements.FindAll(
                element => element.Type == type && element.Path.StartsWith(Prefix + documentName + "#"));
            Assert.That(matches.Count, Is.EqualTo(1), "the annotation must list exactly one " + type);
            return matches[0];
        }

        private PanelSettings CreatePanelSettings(float sortingOrder)
        {
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.sortingOrder = sortingOrder;

            // Without a theme, the parts inside controls get no size and the dropdown menu finds no root
            // container to open in, so the points these tests aim at would not exist.
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(RuntimeThemePath);
            Assert.That(settings.themeStyleSheet, Is.Not.Null, "the runtime theme must exist at " + RuntimeThemePath);
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

        private static Vector2 Center(float left, float top, float width, float height)
        {
            return new Vector2(left + width / 2f, top + height / 2f);
        }

        // The root's size is set by the runtime panel update in the player loop, so frames must pass.
        private static IEnumerator WaitForLayout()
        {
            yield return null;
            yield return null;
        }
    }
}
