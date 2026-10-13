#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.UIElements;

using Image = UnityEngine.UI.Image;
using MouseButton = io.github.hatayama.UnityCliLoop.Runtime.MouseButton;
using UIToolkitButton = UnityEngine.UIElements.Button;

namespace io.github.hatayama.UnityCliLoop.Tests.PlayMode
{
    /// <summary>
    /// Verifies simulate-mouse-ui clicks and long presses reach UI Toolkit controls, choose between uGUI and UI
    /// Toolkit where both are under the point as the EventSystem would, work without an EventSystem for UI
    /// Toolkit, and report which UI system and path were hit. The UI Toolkit button sits at the centre of the
    /// Game View, where the Canvas elements are anchored too. Each test decides whether an EventSystem exists.
    /// </summary>
    public sealed class SimulateMouseUiToolkitTests
    {
        private const string RuntimeThemePath = "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss";
        private const string DocumentName = "SimulateMouseUiToolkitTests_Document";
        private const float ButtonWidth = 200f;
        private const float ButtonHeight = 100f;
        private const string NoEventSystemMessage = "No EventSystem found in the scene. Ensure an EventSystem GameObject exists.";

        private readonly List<Object> _createdObjects = new List<Object>();
        private ExistingEventSystemDisableScope _eventSystemDisableScope = null!;
        private GameObject _canvasObject = null!;
        private SimulateMouseUiTool _tool = null!;
        private SimulateMouseUiResponse _lastResponse = null!;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _eventSystemDisableScope = new ExistingEventSystemDisableScope();

            _canvasObject = new GameObject("TestCanvas");
            _createdObjects.Add(_canvasObject);
            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasObject.AddComponent<CanvasScaler>();
            _canvasObject.AddComponent<GraphicRaycaster>();

            _tool = new SimulateMouseUiTool();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MouseDragState.Clear();
            foreach (Object createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.Destroy(createdObject);
                }
            }

            _createdObjects.Clear();
            yield return null;

            _eventSystemDisableScope.Restore();
        }

        /// <summary>
        /// Verifies a uGUI click reports the UGUI system and the target's hierarchy path (#2).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_OnUgui_ReportsUguiSystemAndPath()
        {
            CreateEventSystem();
            ClickTracker tracker = CreateCanvasClickTarget("UguiTarget");
            yield return null;

            yield return RunClick(GameViewCenter());

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.IsTrue(tracker.PointerClickCalled);
            Assert.AreEqual("UguiTarget", _lastResponse.HitGameObjectName);
            Assert.AreEqual("UGUI", _lastResponse.UiSystem);
            Assert.AreEqual("TestCanvas/UguiTarget", _lastResponse.HitPath);
        }

        /// <summary>
        /// Verifies a click on a UI Toolkit button clicks it once, reports it, and leaves no pointer capture (#4).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_OnUIToolkitButtonWithEventSystem_ClicksIt()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithCenteredButton(0f, out ClickCounter counter);
            yield return WaitForLayout();

            yield return RunClick(GameViewCenter());

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual(1, counter.Clicks);
            Assert.AreEqual("play", _lastResponse.HitGameObjectName);
            Assert.AreEqual("UIToolkit", _lastResponse.UiSystem);
            Assert.AreEqual(DocumentName + "#play", _lastResponse.HitPath);
            Assert.AreEqual(
                $"Clicked UI Toolkit element 'play' at ({GameViewCenter().x:F1}, {GameViewCenter().y:F1})",
                _lastResponse.Message);
            Assert.IsNull(document.rootVisualElement.panel.GetCapturingElement(PointerId.mousePointerId));
        }

        /// <summary>
        /// Verifies a Canvas sorted above the panel takes the click (#5).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_WhereUguiCanvasSortsAbovePanel_ClicksUgui()
        {
            yield return ClickOverlap(canvasSortingOrder: 11, panelSortingOrder: 10f);

            Assert.IsTrue(_lastTracker.PointerClickCalled, "the Canvas element must be clicked");
            Assert.AreEqual(0, _lastCounter.Clicks, "the UI Toolkit button must not be clicked");
            Assert.AreEqual("UGUI", _lastResponse.UiSystem);
        }

        /// <summary>
        /// Verifies a Canvas and a panel with the same sorting order leave UI Toolkit in front (#6).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_WhereCanvasAndPanelShareSortingOrder_ClicksUIToolkit()
        {
            yield return ClickOverlap(canvasSortingOrder: 10, panelSortingOrder: 10f);

            Assert.IsFalse(_lastTracker.PointerClickCalled, "the Canvas element must not be clicked");
            Assert.AreEqual(1, _lastCounter.Clicks, "the UI Toolkit button must be clicked");
            Assert.AreEqual("UIToolkit", _lastResponse.UiSystem);
        }

        /// <summary>
        /// Verifies a panel sorted above the Canvas takes the click (#7).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_WherePanelSortsAboveCanvas_ClicksUIToolkit()
        {
            yield return ClickOverlap(canvasSortingOrder: 9, panelSortingOrder: 10f);

            Assert.IsFalse(_lastTracker.PointerClickCalled, "the Canvas element must not be clicked");
            Assert.AreEqual(1, _lastCounter.Clicks, "the UI Toolkit button must be clicked");
            Assert.AreEqual("UIToolkit", _lastResponse.UiSystem);
        }

        /// <summary>
        /// Verifies a UI Toolkit button is clicked when the scene has no EventSystem (#9).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_OnUIToolkitButtonWithoutEventSystem_ClicksIt()
        {
            Assert.IsNull(EventSystem.current, "Setup: no EventSystem must be active.");
            CreateDocumentWithCenteredButton(0f, out ClickCounter counter);
            yield return WaitForLayout();

            yield return RunClick(GameViewCenter());

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual(1, counter.Clicks);
            Assert.AreEqual("UIToolkit", _lastResponse.UiSystem);
        }

        /// <summary>
        /// Verifies a click with no EventSystem and no UI Toolkit element under the point fails and names both
        /// UI systems (#10).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_WithoutEventSystemOnEmptyPoint_FailsNamingBothSystems()
        {
            Vector2 point = GameViewCenter();

            yield return RunClick(point);

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(
                $"No EventSystem found in the scene, and no UI Toolkit element is at ({point.x:F1}, {point.y:F1}). Ensure an EventSystem GameObject exists for uGUI.",
                _lastResponse.Message);
        }

        /// <summary>
        /// Verifies a bypass click given a UI Toolkit path explains that such paths are not accepted (#15).
        /// </summary>
        [UnityTest]
        public IEnumerator BypassClick_WithUIToolkitPath_ExplainsPathIsNotAccepted()
        {
            CreateEventSystem();
            CreateDocumentWithCenteredButton(0f, out ClickCounter counter);
            yield return WaitForLayout();
            string path = DocumentName + "#play";

            yield return RunTool(new JObject
            {
                ["action"] = MouseAction.Click.ToString(),
                ["bypassRaycast"] = true,
                ["targetPath"] = path
            });

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(
                $"TargetPath '{path}' was not found. UI Toolkit element paths are not accepted here; click the element at its SimX/SimY without --bypass-raycast.",
                _lastResponse.Message);
            Assert.AreEqual(0, counter.Clicks);
        }

        /// <summary>
        /// Verifies a bypass click still needs an EventSystem and fails with the same message as before (#17).
        /// </summary>
        [UnityTest]
        public IEnumerator BypassClick_WithoutEventSystem_FailsAsBefore()
        {
            CreateCanvasClickTarget("BypassTarget");
            yield return null;

            yield return RunTool(new JObject
            {
                ["action"] = MouseAction.Click.ToString(),
                ["bypassRaycast"] = true,
                ["targetPath"] = "TestCanvas/BypassTarget"
            });

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(NoEventSystemMessage, _lastResponse.Message);
        }

        /// <summary>
        /// Verifies option errors are reported before a missing EventSystem (#18).
        /// </summary>
        [UnityTest]
        public IEnumerator Drag_WithNegativeDragSpeedAndNoEventSystem_ReportsDragSpeedError()
        {
            yield return RunTool(new JObject
            {
                ["action"] = MouseAction.Drag.ToString(),
                ["dragSpeed"] = -1f
            });

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual("DragSpeed must be non-negative, got: -1", _lastResponse.Message);
        }

        /// <summary>
        /// Verifies a drag with no EventSystem still fails with the existing message.
        /// </summary>
        [UnityTest]
        public IEnumerator Drag_WithoutEventSystem_FailsAsBefore()
        {
            CreateDocumentWithCenteredButton(0f, out ClickCounter _);
            yield return WaitForLayout();
            Vector2 point = GameViewCenter();

            yield return RunTool(new JObject
            {
                ["action"] = MouseAction.Drag.ToString(),
                ["fromX"] = point.x,
                ["fromY"] = point.y,
                ["x"] = point.x + 10f,
                ["y"] = point.y
            });

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(NoEventSystemMessage, _lastResponse.Message);
        }

        /// <summary>
        /// Verifies a right click reaches the UI Toolkit button as button 1 and does not click it (#20).
        /// </summary>
        [UnityTest]
        public IEnumerator RightClick_OnUIToolkitButton_DoesNotClick()
        {
            CreateEventSystem();
            CreateDocumentWithCenteredButton(0f, out ClickCounter counter);
            yield return WaitForLayout();

            yield return RunTool(new JObject
            {
                ["action"] = MouseAction.Click.ToString(),
                ["x"] = GameViewCenter().x,
                ["y"] = GameViewCenter().y,
                ["button"] = MouseButton.Right.ToString()
            });

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual(1, counter.LastPressedButton, "the press must arrive as the right button");
            Assert.AreEqual(0, counter.Clicks);
        }

        /// <summary>
        /// Verifies a click during a uGUI split drag is rejected as drag-active even after the EventSystem is
        /// removed (#23).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_WhileSplitDragActiveWithoutEventSystem_IsRejectedAsDragActive()
        {
            GameObject eventSystemObject = CreateEventSystem();
            GameObject dragObject = CreateCanvasElement("DragTarget");
            dragObject.AddComponent<Image>();
            dragObject.AddComponent<DragTracker>();
            yield return null;
            Vector2 point = GameViewCenter();
            yield return RunTool(new JObject
            {
                ["action"] = MouseAction.DragStart.ToString(),
                ["x"] = point.x,
                ["y"] = point.y
            });
            Assert.IsTrue(_lastResponse.Success, "Setup: DragStart must succeed. " + _lastResponse.Message);
            eventSystemObject.SetActive(false);
            yield return null;

            yield return RunClick(point);

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual("Cannot Click while a split drag is active. Call DragEnd first.", _lastResponse.Message);
        }

        /// <summary>
        /// Verifies a handler-less uGUI image sorted above the panel stops the click from reaching UI Toolkit,
        /// as it would for real input (#24).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_WhereHandlerlessUguiImageSortsAbovePanel_BlocksUIToolkit()
        {
            CreateEventSystem();
            _canvasObject.GetComponent<Canvas>().sortingOrder = 11;
            CreateCanvasElement("Blocker").AddComponent<Image>();
            CreateDocumentWithCenteredButton(10f, out ClickCounter counter);
            yield return WaitForLayout();

            yield return RunClick(GameViewCenter());

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual(0, counter.Clicks, "the UI Toolkit button must not be clicked");
            Assert.IsNull(_lastResponse.HitGameObjectName);
            Assert.IsNull(_lastResponse.UiSystem);
            Assert.IsNull(_lastResponse.HitPath);
        }

        /// <summary>
        /// Verifies a click still succeeds without error when the UI Toolkit document is disabled after the
        /// target is resolved and before the press is sent, and nothing is clicked (#25).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_WhenUIToolkitDocumentDisabledBeforeDispatch_SkipsWithoutError()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithCenteredButton(0f, out ClickCounter counter);
            yield return WaitForLayout();
            Task<UnityCliLoopToolResponse> task = StartTool(ClickParameters(GameViewCenter()));

            yield return null;
            Assert.IsFalse(task.IsCompleted, "Setup: the click must still be animating when the document is disabled.");
            document.enabled = false;
            yield return WaitForTask(task);

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual(0, counter.Clicks);
            Assert.AreEqual("play", _lastResponse.HitGameObjectName);
        }

        /// <summary>
        /// Verifies a Canvas image with no pointer handler reports no hit (#26).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_OnUguiImageWithoutHandler_ReportsNoHit()
        {
            CreateEventSystem();
            CreateCanvasElement("PlainImage").AddComponent<Image>();
            yield return null;
            Vector2 point = GameViewCenter();

            yield return RunClick(point);

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual($"Clicked at ({point.x:F1}, {point.y:F1}) - no UI element hit", _lastResponse.Message);
            Assert.IsNull(_lastResponse.HitGameObjectName);
            Assert.IsNull(_lastResponse.UiSystem);
            Assert.IsNull(_lastResponse.HitPath);
        }

        /// <summary>
        /// Verifies a click on a physics target outside any Canvas reports an empty UI system and its path (#27).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_OnPhysics2DTarget_ReportsEmptyUiSystem()
        {
            CreateEventSystem();
            GameObject physicsRoot = new GameObject("Physics2DRaycasterRoot");
            _createdObjects.Add(physicsRoot);
            GameObject cameraObject = new GameObject("Physics2DCamera");
            cameraObject.transform.SetParent(physicsRoot.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera physicsCamera = cameraObject.AddComponent<Camera>();
            physicsCamera.orthographic = true;
            physicsCamera.orthographicSize = 5f;
            cameraObject.AddComponent<HighPriorityPhysics2DRaycaster>();
            GameObject physicsTarget = new GameObject("Physics2DTarget");
            physicsTarget.transform.SetParent(physicsRoot.transform, false);
            Vector3 screenPoint = new Vector3(Screen.width / 2f, Screen.height / 2f, 10f);
            Vector3 worldPoint = physicsCamera.ScreenToWorldPoint(screenPoint);
            physicsTarget.transform.position = new Vector3(worldPoint.x, worldPoint.y, 0f);
            physicsTarget.AddComponent<BoxCollider2D>().size = new Vector2(2f, 2f);
            ClickTracker tracker = physicsTarget.AddComponent<ClickTracker>();
            yield return null;

            yield return RunClick(GameViewCenter());

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.IsTrue(tracker.PointerClickCalled);
            Assert.AreEqual("Physics2DTarget", _lastResponse.HitGameObjectName);
            Assert.AreEqual("", _lastResponse.UiSystem);
            Assert.AreEqual("Physics2DRaycasterRoot/Physics2DTarget", _lastResponse.HitPath);
        }

        /// <summary>
        /// Verifies a bypass click on a Canvas element reports the UGUI system and the target path (#28).
        /// </summary>
        [UnityTest]
        public IEnumerator BypassClick_OnCanvasButton_ReportsUguiSystem()
        {
            CreateEventSystem();
            ClickTracker tracker = CreateCanvasClickTarget("BypassTarget");
            yield return null;

            yield return RunTool(new JObject
            {
                ["action"] = MouseAction.Click.ToString(),
                ["bypassRaycast"] = true,
                ["targetPath"] = "TestCanvas/BypassTarget"
            });

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.IsTrue(tracker.PointerClickCalled);
            Assert.AreEqual("UGUI", _lastResponse.UiSystem);
            Assert.AreEqual("TestCanvas/BypassTarget", _lastResponse.HitPath);
        }

        /// <summary>
        /// Verifies a long press on a UI Toolkit button clicks it once on release, as UI Toolkit buttons do, and
        /// leaves no pointer capture.
        /// </summary>
        [UnityTest]
        public IEnumerator LongPress_OnUIToolkitButton_ClicksOnReleaseAndLeavesNoCapture()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithCenteredButton(0f, out ClickCounter counter);
            yield return WaitForLayout();

            yield return RunTool(LongPressParameters(GameViewCenter(), 0.2f));

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual(1, counter.Clicks);
            Assert.AreEqual("UIToolkit", _lastResponse.UiSystem);
            Assert.AreEqual(DocumentName + "#play", _lastResponse.HitPath);
            Assert.IsNull(document.rootVisualElement.panel.GetCapturingElement(PointerId.mousePointerId));
        }

        /// <summary>
        /// Verifies cancelling a long press while it holds still releases the UI Toolkit press, so no element is
        /// left holding the pointer.
        /// </summary>
        [UnityTest]
        public IEnumerator LongPress_OnUIToolkitButton_WhenCancelledDuringHold_ReleasesCapture()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithCenteredButton(0f, out ClickCounter counter);
            yield return WaitForLayout();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task<UnityCliLoopToolResponse> task = _tool.ExecuteAsync(LongPressParameters(GameViewCenter(), 5f), cancellation.Token);
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => counter.PointerDowns > 0 || Time.realtimeSinceStartup >= timeoutAt);
            Assert.AreEqual(1, counter.PointerDowns, "Setup: the press must have arrived before cancelling.");

            cancellation.Cancel();
            timeoutAt = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => task.IsCompleted || Time.realtimeSinceStartup >= timeoutAt);
            yield return null;

            Assert.IsTrue(task.IsCompleted, "the long press must end after cancelling");
            Assert.IsTrue(task.IsCanceled || task.Exception?.InnerException is System.OperationCanceledException,
                $"the long press must end by cancellation, got {task.Status}: {task.Exception}");
            Assert.IsNull(document.rootVisualElement.panel.GetCapturingElement(PointerId.mousePointerId));
        }

        /// <summary>
        /// Verifies a long press whose UI Toolkit document is disabled while it holds ends successfully without
        /// error.
        /// </summary>
        [UnityTest]
        public IEnumerator LongPress_WhenDocumentDisabledDuringHold_ReleasesWithoutError()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithCenteredButton(0f, out ClickCounter counter);
            yield return WaitForLayout();
            Task<UnityCliLoopToolResponse> task = StartTool(LongPressParameters(GameViewCenter(), 0.5f));
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => counter.PointerDowns > 0 || Time.realtimeSinceStartup >= timeoutAt);
            Assert.AreEqual(1, counter.PointerDowns, "Setup: the press must have arrived before disabling.");

            document.enabled = false;
            yield return WaitForTask(task);

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual("play", _lastResponse.HitGameObjectName);
        }

        private ClickTracker _lastTracker = null!;
        private ClickCounter _lastCounter = null!;

        private IEnumerator ClickOverlap(int canvasSortingOrder, float panelSortingOrder)
        {
            CreateEventSystem();
            _canvasObject.GetComponent<Canvas>().sortingOrder = canvasSortingOrder;
            _lastTracker = CreateCanvasClickTarget("UguiTarget");
            CreateDocumentWithCenteredButton(panelSortingOrder, out _lastCounter);
            yield return WaitForLayout();

            yield return RunClick(GameViewCenter());

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
        }

        private GameObject CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("TestEventSystem");
            _createdObjects.Add(eventSystemObject);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
            return eventSystemObject;
        }

        private ClickTracker CreateCanvasClickTarget(string name)
        {
            GameObject element = CreateCanvasElement(name);
            element.AddComponent<Image>();
            return element.AddComponent<ClickTracker>();
        }

        // Anchored at the Canvas centre, which is the Game View centre in PlayMode.
        private GameObject CreateCanvasElement(string name)
        {
            GameObject element = new GameObject(name);
            element.transform.SetParent(_canvasObject.transform, false);
            RectTransform rect = element.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            return element;
        }

        // The object starts inactive so the document sees its panel settings when it first enables.
        private UIDocument CreateDocumentWithCenteredButton(float sortingOrder, out ClickCounter counter)
        {
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.sortingOrder = sortingOrder;
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(RuntimeThemePath);
            Assert.IsNotNull(settings.themeStyleSheet, "the runtime theme must exist at " + RuntimeThemePath);
            _createdObjects.Add(settings);

            GameObject documentObject = new GameObject(DocumentName);
            documentObject.SetActive(false);
            _createdObjects.Add(documentObject);
            UIDocument document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            documentObject.SetActive(true);

            UIToolkitButton button = new UIToolkitButton { name = "play" };
            Vector2 center = GameViewCenter();
            button.style.position = Position.Absolute;
            button.style.left = center.x - ButtonWidth / 2f;
            button.style.top = center.y - ButtonHeight / 2f;
            button.style.width = ButtonWidth;
            button.style.height = ButtonHeight;
            button.style.marginLeft = 0f;
            button.style.marginTop = 0f;
            counter = new ClickCounter(button);
            document.rootVisualElement.Add(button);
            return document;
        }

        private static Vector2 GameViewCenter()
        {
            Vector2 size = Handles.GetMainGameViewSize();
            return new Vector2(Mathf.Round(size.x / 2f), Mathf.Round(size.y / 2f));
        }

        private static JObject ClickParameters(Vector2 point)
        {
            return new JObject
            {
                ["action"] = MouseAction.Click.ToString(),
                ["x"] = point.x,
                ["y"] = point.y
            };
        }

        private static JObject LongPressParameters(Vector2 point, float duration)
        {
            return new JObject
            {
                ["action"] = MouseAction.LongPress.ToString(),
                ["x"] = point.x,
                ["y"] = point.y,
                ["duration"] = duration
            };
        }

        private IEnumerator RunClick(Vector2 point)
        {
            yield return RunTool(ClickParameters(point));
        }

        private IEnumerator RunTool(JObject parameters)
        {
            yield return WaitForTask(StartTool(parameters));
        }

        private Task<UnityCliLoopToolResponse> StartTool(JObject parameters)
        {
            return _tool.ExecuteAsync(parameters, CancellationToken.None);
        }

        private IEnumerator WaitForTask(Task<UnityCliLoopToolResponse> task)
        {
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => task.IsCompleted || Time.realtimeSinceStartup >= timeoutAt);
            Assert.IsTrue(task.IsCompleted, "Tool execution timed out.");
            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status, $"Tool execution should complete: {task.Exception}");
            _lastResponse = (SimulateMouseUiResponse)task.Result;
        }

        // The root's size is set by the runtime panel update in the player loop, so frames must pass.
        private static IEnumerator WaitForLayout()
        {
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Counts clicks and presses on a UI Toolkit button.
        /// </summary>
        private sealed class ClickCounter
        {
            internal ClickCounter(UIToolkitButton button)
            {
                button.clicked += () => Clicks++;
                button.RegisterCallback<PointerDownEvent>(
                    evt =>
                    {
                        PointerDowns++;
                        LastPressedButton = evt.button;
                    },
                    TrickleDown.TrickleDown);
            }

            internal int Clicks { get; private set; }
            internal int PointerDowns { get; private set; }
            internal int LastPressedButton { get; private set; } = -1;
        }
    }
}
