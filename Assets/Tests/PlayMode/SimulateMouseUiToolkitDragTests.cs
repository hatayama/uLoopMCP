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
using Slider = UnityEngine.UIElements.Slider;
using UIToolkitButton = UnityEngine.UIElements.Button;

namespace io.github.hatayama.UnityCliLoop.Tests.PlayMode
{
    /// <summary>
    /// Verifies simulate-mouse-ui drags reach UI Toolkit controls in one shot and split into steps, work without
    /// an EventSystem for UI Toolkit, refuse drop targets on UI Toolkit, and release the pointer on every exit.
    /// The UI Toolkit control sits at the centre of the Game View. Each test decides whether an EventSystem
    /// exists.
    /// </summary>
    public sealed class SimulateMouseUiToolkitDragTests
    {
        private const string RuntimeThemePath = "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss";
        private const string DocumentName = "SimulateMouseUiToolkitDragTests_Document";
        private const float SliderWidth = 300f;
        private const float SliderHeight = 40f;
        private const float EdgeInset = 5f;
        private const string NoEventSystemMessage = "No EventSystem found in the scene. Ensure an EventSystem GameObject exists.";
        private const string NoDragMessage = "No drag in progress. Call DragStart first.";

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
        /// Verifies a one-shot drag across a UI Toolkit slider moves its value to the end, reports the slider,
        /// and leaves no pointer capture (D1).
        /// </summary>
        [UnityTest]
        public IEnumerator Drag_OnUIToolkitSlider_MovesValue()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider slider);
            yield return WaitForLayout();

            yield return RunTool(DragParameters(SliderLeft(), SliderRight(), 0f));

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.GreaterOrEqual(slider.value, 0.9f);
            Assert.AreEqual("volume", _lastResponse.HitGameObjectName);
            Assert.AreEqual("UIToolkit", _lastResponse.UiSystem);
            Assert.AreEqual(DocumentName + "#volume", _lastResponse.HitPath);
            Assert.IsNull(CapturingElement(document));
        }

        /// <summary>
        /// Verifies a one-shot drag on a UI Toolkit slider works without an EventSystem (D2).
        /// </summary>
        [UnityTest]
        public IEnumerator Drag_OnUIToolkitSliderWithoutEventSystem_MovesValue()
        {
            CreateDocumentWithSlider(out Slider slider);
            yield return WaitForLayout();

            yield return RunTool(DragParameters(SliderLeft(), SliderRight(), 0f));

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.GreaterOrEqual(slider.value, 0.9f);
        }

        /// <summary>
        /// Verifies a drag with neither an EventSystem nor a UI Toolkit control at the start fails naming both
        /// (D5).
        /// </summary>
        [UnityTest]
        public IEnumerator Drag_WithoutEventSystemOnEmptyPoint_Fails()
        {
            yield return null;
            Vector2 start = new Vector2(10f, 10f);

            yield return RunTool(DragParameters(start, new Vector2(60f, 10f), 0f));

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(
                "No EventSystem found in the scene, and no UI Toolkit element is at (10.0, 10.0). Ensure an EventSystem GameObject exists for uGUI.",
                _lastResponse.Message);
        }

        /// <summary>
        /// Verifies a drag on UI Toolkit with a drop target that exists fails before anything is sent (D6).
        /// </summary>
        [UnityTest]
        public IEnumerator Drag_OnUIToolkitWithDropTargetPath_FailsBeforeDispatch()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider slider);
            GameObject dropZone = CreateCanvasElement("DropZone", new Vector2(0f, 300f));
            yield return WaitForLayout();
            JObject parameters = DragParameters(SliderLeft(), SliderRight(), 0f);
            parameters["dropTargetPath"] = GameObjectPathUtility.GetFullPath(dropZone);

            yield return RunTool(parameters);

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(
                "DropTargetPath applies to uGUI drags only; this drag starts on UI Toolkit element 'volume'.",
                _lastResponse.Message);
            Assert.AreEqual(0f, slider.value);
            Assert.IsNull(CapturingElement(document));
        }

        /// <summary>
        /// Verifies a drag on UI Toolkit with a drop target that does not exist reports the UI Toolkit
        /// restriction rather than the missing path (D20).
        /// </summary>
        [UnityTest]
        public IEnumerator Drag_OnUIToolkitWithMissingDropTargetPath_ReportsUIToolkitRestriction()
        {
            CreateEventSystem();
            CreateDocumentWithSlider(out Slider slider);
            yield return WaitForLayout();
            JObject parameters = DragParameters(SliderLeft(), SliderRight(), 0f);
            parameters["dropTargetPath"] = "Missing/DropZone";

            yield return RunTool(parameters);

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(
                "DropTargetPath applies to uGUI drags only; this drag starts on UI Toolkit element 'volume'.",
                _lastResponse.Message);
            Assert.AreEqual(0f, slider.value);
        }

        /// <summary>
        /// Verifies a split drag on a UI Toolkit slider moves the value at each step and ends with no drag in
        /// progress and no pointer capture (D7).
        /// </summary>
        [UnityTest]
        public IEnumerator SplitDrag_OnUIToolkitSlider_MovesStepwiseAndClears()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider slider);
            yield return WaitForLayout();

            yield return RunSplitDrag(slider);

            Assert.LessOrEqual(slider.value, 0.1f);
            Assert.IsFalse(MouseDragState.IsDragging);
            Assert.IsNull(CapturingElement(document));
        }

        /// <summary>
        /// Verifies a split drag on UI Toolkit runs every step without an EventSystem (D8).
        /// </summary>
        [UnityTest]
        public IEnumerator SplitDrag_OnUIToolkitWithoutEventSystem_Completes()
        {
            UIDocument document = CreateDocumentWithSlider(out Slider slider);
            yield return WaitForLayout();

            yield return RunSplitDrag(slider);

            Assert.LessOrEqual(slider.value, 0.1f);
            Assert.IsFalse(MouseDragState.IsDragging);
            Assert.IsNull(CapturingElement(document));
        }

        /// <summary>
        /// Verifies a uGUI split drag whose EventSystem goes away fails DragMove as before and keeps the drag
        /// (D9).
        /// </summary>
        [UnityTest]
        public IEnumerator SplitDrag_OnUguiWhenEventSystemRemoved_FailsAsBefore()
        {
            GameObject eventSystem = CreateEventSystem();
            GameObject dragTarget = CreateCanvasElement("UguiDragTarget", Vector2.zero);
            dragTarget.AddComponent<Image>();
            dragTarget.AddComponent<DragTracker>();
            yield return null;
            Vector2 center = GameViewCenter();
            yield return RunTool(SplitParameters(MouseAction.DragStart, center));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);

            eventSystem.SetActive(false);
            yield return RunTool(SplitParameters(MouseAction.DragMove, center + new Vector2(50f, 0f)));

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(NoEventSystemMessage, _lastResponse.Message);
            Assert.IsTrue(MouseDragState.IsDragging, "the drag must stay in progress");
        }

        /// <summary>
        /// Verifies a split drag whose UI Toolkit document was disabled fails the next step and ends the drag
        /// (D10).
        /// </summary>
        [UnityTest]
        public IEnumerator SplitDrag_WhenUIToolkitDocumentDisabled_FailsAndClears()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider _);
            yield return WaitForLayout();
            yield return RunTool(SplitParameters(MouseAction.DragStart, SliderLeft()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);

            document.enabled = false;
            yield return RunTool(SplitParameters(MouseAction.DragMove, GameViewCenter()));

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(
                "Drag target UI Toolkit element is no longer available (its document was disabled or destroyed).",
                _lastResponse.Message);
            Assert.IsFalse(MouseDragState.IsDragging);
        }

        /// <summary>
        /// Verifies a click and a one-shot drag are refused while a UI Toolkit split drag is in progress (D11).
        /// </summary>
        [UnityTest]
        public IEnumerator Click_WhileUIToolkitSplitDragActive_IsRejected()
        {
            CreateEventSystem();
            CreateDocumentWithSlider(out Slider _);
            yield return WaitForLayout();
            yield return RunTool(SplitParameters(MouseAction.DragStart, SliderLeft()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);

            yield return RunTool(SplitParameters(MouseAction.Click, GameViewCenter()));
            Assert.AreEqual("Cannot Click while a split drag is active. Call DragEnd first.", _lastResponse.Message);

            yield return RunTool(DragParameters(SliderLeft(), SliderRight(), 0f));
            Assert.AreEqual("Cannot Drag while a split drag is active. Call DragEnd first.", _lastResponse.Message);
        }

        /// <summary>
        /// Verifies DragEnd with a drop target on a UI Toolkit drag fails without ending the drag, and DragEnd
        /// without it then succeeds (D12).
        /// </summary>
        [UnityTest]
        public IEnumerator DragEnd_OnUIToolkitWithDropTargetPath_FailsAndKeepsDrag()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider _);
            GameObject dropZone = CreateCanvasElement("DropZone", new Vector2(0f, 300f));
            yield return WaitForLayout();
            yield return RunTool(SplitParameters(MouseAction.DragStart, SliderLeft()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);

            JObject dragEnd = SplitParameters(MouseAction.DragEnd, SliderRight());
            dragEnd["dropTargetPath"] = GameObjectPathUtility.GetFullPath(dropZone);
            yield return RunTool(dragEnd);

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(
                "DropTargetPath applies to uGUI drags only; this drag starts on UI Toolkit element 'volume'.",
                _lastResponse.Message);
            Assert.IsTrue(MouseDragState.IsDragging, "the drag must stay in progress");

            yield return RunTool(SplitParameters(MouseAction.DragEnd, SliderRight()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.IsNull(CapturingElement(document));
        }

        /// <summary>
        /// Verifies a split drag whose UI Toolkit document was destroyed counts as ended, as a destroyed uGUI
        /// target does (D17).
        /// </summary>
        [UnityTest]
        public IEnumerator SplitDrag_WhenUIToolkitDocumentDestroyed_TreatsDragAsEnded()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider _);
            yield return WaitForLayout();
            yield return RunTool(SplitParameters(MouseAction.DragStart, SliderLeft()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);

            Object.Destroy(document.gameObject);
            yield return null;

            yield return RunTool(SplitParameters(MouseAction.DragMove, GameViewCenter()));
            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(NoDragMessage, _lastResponse.Message);

            yield return RunTool(SplitParameters(MouseAction.Click, GameViewCenter()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
        }

        /// <summary>
        /// Verifies a drag from a UI Toolkit button to a point outside it sends the press and release without
        /// clicking, as real input would (D18).
        /// </summary>
        [UnityTest]
        public IEnumerator Drag_FromUIToolkitButtonToOutside_DoesNotClick()
        {
            CreateEventSystem();
            UIToolkitButton button = CreateDocumentWithButton(out int[] clicks);
            yield return WaitForLayout();
            Vector2 center = GameViewCenter();

            yield return RunTool(DragParameters(center, center + new Vector2(0f, 200f), 0f));

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual(0, clicks[0]);
            Assert.AreEqual(DocumentName + "#" + button.name, _lastResponse.HitPath);
        }

        /// <summary>
        /// Verifies DragMove without an EventSystem and without a drag reports that no drag is in progress (D19).
        /// </summary>
        [UnityTest]
        public IEnumerator DragMove_WithoutEventSystemAndNoDrag_ReportsNoDrag()
        {
            yield return null;

            yield return RunTool(SplitParameters(MouseAction.DragMove, GameViewCenter()));

            Assert.IsFalse(_lastResponse.Success);
            Assert.AreEqual(NoDragMessage, _lastResponse.Message);
        }

        /// <summary>
        /// Verifies cancelling a one-shot UI Toolkit drag while it moves releases the pointer capture (D21).
        /// </summary>
        [UnityTest]
        public IEnumerator Drag_OnUIToolkitSlider_WhenCancelledMidway_ReleasesCapture()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider slider);
            yield return WaitForLayout();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task<UnityCliLoopToolResponse> task = _tool.ExecuteAsync(DragParameters(SliderLeft(), SliderRight(), 50f), cancellation.Token);
            yield return WaitUntilSliderMoves(slider);

            cancellation.Cancel();
            yield return WaitForCancelled(task);

            Assert.IsNull(CapturingElement(document));
        }

        /// <summary>
        /// Verifies cancelling a UI Toolkit DragEnd while it moves releases the pointer and ends the drag (D22).
        /// </summary>
        [UnityTest]
        public IEnumerator DragEnd_OnUIToolkit_WhenCancelledMidway_ReleasesAndClears()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider slider);
            yield return WaitForLayout();
            yield return RunTool(SplitParameters(MouseAction.DragStart, SliderLeft()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task<UnityCliLoopToolResponse> task = _tool.ExecuteAsync(SlowSplitParameters(MouseAction.DragEnd, SliderRight()), cancellation.Token);
            yield return WaitUntilSliderMoves(slider);

            cancellation.Cancel();
            yield return WaitForCancelled(task);

            Assert.IsNull(CapturingElement(document));
            Assert.IsFalse(MouseDragState.IsDragging);
        }

        /// <summary>
        /// Verifies cancelling a UI Toolkit DragMove keeps the button held and the drag in progress, and a later
        /// DragEnd releases it (D23).
        /// </summary>
        [UnityTest]
        public IEnumerator DragMove_OnUIToolkit_WhenCancelled_KeepsDrag()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider slider);
            yield return WaitForLayout();
            yield return RunTool(SplitParameters(MouseAction.DragStart, SliderLeft()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task<UnityCliLoopToolResponse> task = _tool.ExecuteAsync(SlowSplitParameters(MouseAction.DragMove, SliderRight()), cancellation.Token);
            yield return WaitUntilSliderMoves(slider);

            cancellation.Cancel();
            yield return WaitForCancelled(task);

            Assert.IsTrue(MouseDragState.IsDragging, "the drag must stay in progress");
            Assert.IsNotNull(CapturingElement(document), "the button must stay held");

            yield return RunTool(SplitParameters(MouseAction.DragEnd, SliderRight()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.IsNull(CapturingElement(document));
        }

        /// <summary>
        /// Verifies a one-shot UI Toolkit drag whose document is disabled while it moves completes without error
        /// and reports the element it started on (D24).
        /// </summary>
        [UnityTest]
        public IEnumerator Drag_OnUIToolkitSlider_WhenDocumentDisabledMidway_CompletesWithoutError()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider slider);
            yield return WaitForLayout();
            // Only logs during the drag count: creating panel settings warns before the test sets their theme.
            List<string> errors = new List<string>();
            UnityEngine.Application.LogCallback recordErrors = (condition, stackTrace, type) =>
            {
                if (type != LogType.Log && type != LogType.Warning)
                {
                    errors.Add($"{type}: {condition}");
                }
            };
            UnityEngine.Application.logMessageReceived += recordErrors;
            Task<UnityCliLoopToolResponse> task = _tool.ExecuteAsync(DragParameters(SliderLeft(), SliderRight(), 200f), CancellationToken.None);
            yield return WaitUntilSliderMoves(slider);

            document.enabled = false;
            yield return WaitForTask(task);
            UnityEngine.Application.logMessageReceived -= recordErrors;

            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual(DocumentName + "#volume", _lastResponse.HitPath);
            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// Verifies cancelling a UI Toolkit DragStart during its start animation releases the pointer and leaves
        /// no drag in progress (D25).
        /// </summary>
        [UnityTest]
        public IEnumerator DragStart_OnUIToolkitSlider_WhenCancelledDuringExpand_ReleasesAndClears()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider _);
            yield return WaitForLayout();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task<UnityCliLoopToolResponse> task = _tool.ExecuteAsync(SplitParameters(MouseAction.DragStart, SliderLeft()), cancellation.Token);

            yield return null;
            Assert.IsFalse(task.IsCompleted, "Setup: DragStart must still be animating when cancelled.");
            Assert.IsNotNull(CapturingElement(document), "Setup: the press must have arrived before cancelling.");
            cancellation.Cancel();
            yield return WaitForCancelled(task);

            Assert.IsNull(CapturingElement(document));
            Assert.IsFalse(MouseDragState.IsDragging);
        }

        /// <summary>
        /// Verifies a DragStart whose UI Toolkit press handler throws releases the pointer, leaves no drag in
        /// progress, and ends with the exception.
        /// </summary>
        [UnityTest]
        public IEnumerator DragStart_WhenUIToolkitPressHandlerThrows_ReleasesAndLeavesNoDrag()
        {
            CreateEventSystem();
            CreateDocumentWithSlider(out Slider slider);
            int pointerUps = 0;
            slider.RegisterCallback<PointerDownEvent>(_ => throw new System.InvalidOperationException("press failed"), TrickleDown.TrickleDown);
            slider.RegisterCallback<PointerUpEvent>(_ => pointerUps++, TrickleDown.TrickleDown);
            yield return WaitForLayout();

            yield return WaitForFaulted(_tool.ExecuteAsync(SplitParameters(MouseAction.DragStart, SliderLeft()), CancellationToken.None));

            Assert.AreEqual(1, pointerUps, "the release must still be sent");
            Assert.IsFalse(MouseDragState.IsDragging);
        }

        /// <summary>
        /// Verifies a DragEnd whose UI Toolkit release handler throws still ends the drag and ends with the
        /// exception.
        /// </summary>
        [UnityTest]
        public IEnumerator DragEnd_WhenUIToolkitReleaseHandlerThrows_StillEndsDrag()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider _);
            yield return WaitForLayout();
            yield return RunTool(SplitParameters(MouseAction.DragStart, SliderLeft()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            CapturingControl(document).RegisterCallback<PointerUpEvent>(_ => throw new System.InvalidOperationException("release failed"));

            yield return WaitForFaulted(_tool.ExecuteAsync(SplitParameters(MouseAction.DragEnd, SliderRight()), CancellationToken.None));

            Assert.IsFalse(MouseDragState.IsDragging);
        }

        /// <summary>
        /// Verifies a DragMove whose UI Toolkit move handler throws keeps the drag in progress and the button
        /// held, as a cancelled DragMove does, so a later DragEnd releases it.
        /// </summary>
        [UnityTest]
        public IEnumerator DragMove_WhenUIToolkitMoveHandlerThrows_KeepsDrag()
        {
            CreateEventSystem();
            UIDocument document = CreateDocumentWithSlider(out Slider _);
            yield return WaitForLayout();
            yield return RunTool(SplitParameters(MouseAction.DragStart, SliderLeft()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            bool throwOnMove = false;
            CapturingControl(document).RegisterCallback<PointerMoveEvent>(_ =>
            {
                if (throwOnMove)
                {
                    throw new System.InvalidOperationException("move failed");
                }
            });

            throwOnMove = true;
            yield return WaitForFaulted(_tool.ExecuteAsync(SplitParameters(MouseAction.DragMove, GameViewCenter()), CancellationToken.None));

            Assert.IsTrue(MouseDragState.IsDragging, "the drag must stay in progress");
            Assert.IsNotNull(CapturingElement(document), "the button must stay held");
            throwOnMove = false;
            yield return RunTool(SplitParameters(MouseAction.DragEnd, SliderRight()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.IsNull(CapturingElement(document));
        }

        // Steps right to the centre, then back to the left edge, checking the value after each step.
        private IEnumerator RunSplitDrag(Slider slider)
        {
            yield return RunTool(SplitParameters(MouseAction.DragStart, SliderLeft()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual("UIToolkit", _lastResponse.UiSystem);

            yield return RunTool(SplitParameters(MouseAction.DragMove, GameViewCenter()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.That(slider.value, Is.InRange(0.3f, 0.7f));

            yield return RunTool(SplitParameters(MouseAction.DragEnd, SliderLeft()));
            Assert.IsTrue(_lastResponse.Success, _lastResponse.Message);
            Assert.AreEqual("UIToolkit", _lastResponse.UiSystem);
        }

        private GameObject CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("TestEventSystem");
            _createdObjects.Add(eventSystemObject);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
            return eventSystemObject;
        }

        // Offset from the Canvas centre, which is the Game View centre in PlayMode; Canvas y grows upwards.
        private GameObject CreateCanvasElement(string name, Vector2 offset)
        {
            GameObject element = new GameObject(name);
            element.transform.SetParent(_canvasObject.transform, false);
            RectTransform rect = element.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(SliderWidth, SliderHeight);
            return element;
        }

        private UIDocument CreateDocumentWithSlider(out Slider slider)
        {
            slider = new Slider(0f, 1f) { name = "volume" };
            UIDocument document = CreateDocument();
            PlaceAtCenter(document, slider);
            return document;
        }

        // The counter is an array so the click callback can update it after this method returns.
        private UIToolkitButton CreateDocumentWithButton(out int[] clicks)
        {
            int[] counter = new int[1];
            UIToolkitButton button = new UIToolkitButton { name = "play" };
            button.clicked += () => counter[0]++;
            PlaceAtCenter(CreateDocument(), button);
            clicks = counter;
            return button;
        }

        // The object starts inactive so the document sees its panel settings when it first enables.
        private UIDocument CreateDocument()
        {
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(RuntimeThemePath);
            Assert.IsNotNull(settings.themeStyleSheet, "the runtime theme must exist at " + RuntimeThemePath);
            _createdObjects.Add(settings);

            GameObject documentObject = new GameObject(DocumentName);
            documentObject.SetActive(false);
            _createdObjects.Add(documentObject);
            UIDocument document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            documentObject.SetActive(true);
            return document;
        }

        private static void PlaceAtCenter(UIDocument document, VisualElement element)
        {
            Vector2 center = GameViewCenter();
            element.style.position = Position.Absolute;
            element.style.left = center.x - SliderWidth / 2f;
            element.style.top = center.y - SliderHeight / 2f;
            element.style.width = SliderWidth;
            element.style.height = SliderHeight;
            element.style.marginLeft = 0f;
            element.style.marginTop = 0f;
            element.style.marginRight = 0f;
            element.style.marginBottom = 0f;
            document.rootVisualElement.Add(element);
        }

        // While the pointer is captured, its events go to the capturing element only, so handlers that must see
        // them are registered there.
        private static VisualElement CapturingControl(UIDocument document)
        {
            VisualElement? capturing = CapturingElement(document) as VisualElement;
            Assert.IsNotNull(capturing, "Setup: the drag must have captured the pointer.");
            return capturing!;
        }

        private static IEventHandler? CapturingElement(UIDocument document)
        {
            return document.rootVisualElement.panel.GetCapturingElement(PointerId.mousePointerId);
        }

        private static Vector2 GameViewCenter()
        {
            Vector2 size = Handles.GetMainGameViewSize();
            return new Vector2(Mathf.Round(size.x / 2f), Mathf.Round(size.y / 2f));
        }

        private static Vector2 SliderLeft()
        {
            return GameViewCenter() + new Vector2(-SliderWidth / 2f + EdgeInset, 0f);
        }

        private static Vector2 SliderRight()
        {
            return GameViewCenter() + new Vector2(SliderWidth / 2f - EdgeInset, 0f);
        }

        private static JObject DragParameters(Vector2 from, Vector2 to, float dragSpeed)
        {
            return new JObject
            {
                ["action"] = MouseAction.Drag.ToString(),
                ["fromX"] = from.x,
                ["fromY"] = from.y,
                ["x"] = to.x,
                ["y"] = to.y,
                ["dragSpeed"] = dragSpeed
            };
        }

        private static JObject SplitParameters(MouseAction action, Vector2 point)
        {
            return new JObject
            {
                ["action"] = action.ToString(),
                ["x"] = point.x,
                ["y"] = point.y,
                ["dragSpeed"] = 0f
            };
        }

        private static JObject SlowSplitParameters(MouseAction action, Vector2 point)
        {
            JObject parameters = SplitParameters(action, point);
            parameters["dragSpeed"] = 50f;
            return parameters;
        }

        private IEnumerator RunTool(JObject parameters)
        {
            yield return WaitForTask(_tool.ExecuteAsync(parameters, CancellationToken.None));
        }

        private IEnumerator WaitForTask(Task<UnityCliLoopToolResponse> task)
        {
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => task.IsCompleted || Time.realtimeSinceStartup >= timeoutAt);
            Assert.IsTrue(task.IsCompleted, "Tool execution timed out.");
            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status, $"Tool execution should complete: {task.Exception}");
            _lastResponse = (SimulateMouseUiResponse)task.Result;
        }

        private static IEnumerator WaitForFaulted(Task<UnityCliLoopToolResponse> task)
        {
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => task.IsCompleted || Time.realtimeSinceStartup >= timeoutAt);
            Assert.IsTrue(task.IsCompleted, "Tool execution timed out.");
            Assert.IsTrue(task.IsFaulted, $"the handler's exception must end the command, got {task.Status}");
            Assert.IsInstanceOf<System.InvalidOperationException>(task.Exception!.InnerException);
        }

        private static IEnumerator WaitForCancelled(Task<UnityCliLoopToolResponse> task)
        {
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => task.IsCompleted || Time.realtimeSinceStartup >= timeoutAt);
            yield return null;
            Assert.IsTrue(task.IsCompleted, "the drag must end after cancelling");
            Assert.IsTrue(task.IsCanceled || task.Exception?.InnerException is System.OperationCanceledException,
                $"the drag must end by cancellation, got {task.Status}: {task.Exception}");
        }

        // A press near the left edge leaves the value near 0, so reaching 0.2 means the interpolation is running.
        private static IEnumerator WaitUntilSliderMoves(Slider slider)
        {
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => slider.value >= 0.2f || Time.realtimeSinceStartup >= timeoutAt);
            Assert.GreaterOrEqual(slider.value, 0.2f, "Setup: the drag must be moving before the step.");
        }

        // The root's size is set by the runtime panel update in the player loop, so frames must pass.
        private static IEnumerator WaitForLayout()
        {
            yield return null;
            yield return null;
        }
    }
}
