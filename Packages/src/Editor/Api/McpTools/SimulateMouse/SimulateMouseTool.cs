#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace io.github.hatayama.uLoopMCP
{
    [McpTool(Description = "Simulate mouse click and drag on PlayMode UI elements via screen coordinates")]
    public class SimulateMouseTool : AbstractUnityTool<SimulateMouseSchema, SimulateMouseResponse>
    {
        public override string ToolName => "simulate-mouse";

        private const float EXPAND_DURATION = 0.1f;
        private const float EXPAND_START_SCALE = 1.5f;
        private const float DISSIPATE_DURATION = 0.1f;

        protected override async Task<SimulateMouseResponse> ExecuteAsync(
            SimulateMouseSchema parameters,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string correlationId = McpConstants.GenerateCorrelationId();

            if (!EditorApplication.isPlaying)
            {
                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = "PlayMode is not active. Use control-play-mode tool to start PlayMode first.",
                    Action = parameters.Action.ToString()
                };
            }

            EventSystem? eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = "No EventSystem found in the scene. Ensure an EventSystem GameObject exists.",
                    Action = parameters.Action.ToString()
                };
            }

            if (parameters.Action != MouseAction.Click && parameters.DragSpeed < 0f)
            {
                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = $"DragSpeed must be non-negative, got: {parameters.DragSpeed}",
                    Action = parameters.Action.ToString()
                };
            }

            VibeLogger.LogInfo(
                "simulate_mouse_start",
                "Mouse simulation started",
                new { Action = parameters.Action.ToString(), X = parameters.X, Y = parameters.Y },
                correlationId: correlationId
            );

            EnsureOverlayExists();

            // Single-pointer model: Click and one-shot Drag are invalid while a split drag is held
            if (MouseDragState.IsDragging &&
                (parameters.Action == MouseAction.Click || parameters.Action == MouseAction.Drag))
            {
                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = $"Cannot {parameters.Action.ToString()} while a split drag is active. Call DragEnd first.",
                    Action = parameters.Action.ToString()
                };
            }

            SimulateMouseResponse response;

            switch (parameters.Action)
            {
                case MouseAction.Click:
                    response = await ExecuteClick(parameters, eventSystem, ct);
                    break;

                case MouseAction.Drag:
                    response = await ExecuteDragOneShot(parameters, eventSystem, ct);
                    break;

                case MouseAction.DragStart:
                    response = await ExecuteDragStart(parameters, eventSystem, ct);
                    break;

                case MouseAction.DragMove:
                    response = await ExecuteDragMove(parameters, ct);
                    break;

                case MouseAction.DragEnd:
                    response = await ExecuteDragEnd(parameters, ct);
                    break;

                default:
                    throw new ArgumentException($"Unknown mouse action: {parameters.Action}");
            }

            VibeLogger.LogInfo(
                "simulate_mouse_complete",
                $"Mouse simulation completed: {response.Message}",
                new { Action = parameters.Action.ToString(), Success = response.Success },
                correlationId: correlationId
            );

            return response;
        }

        private static void EnsureOverlayExists()
        {
            if (SimulateMouseOverlay.Instance != null)
            {
                return;
            }

            GameObject overlayGo = new GameObject("SimulateMouseOverlay");
            overlayGo.hideFlags = HideFlags.HideAndDontSave;
            overlayGo.AddComponent<SimulateMouseOverlay>();
        }

        // Input coordinates use top-left origin; Unity Screen space uses bottom-left origin
        private static Vector2 InputToScreen(Vector2 inputPos)
        {
            return new Vector2(inputPos.x, Screen.height - inputPos.y);
        }

        private static Vector2 ScreenToInput(Vector2 screenPos)
        {
            return new Vector2(screenPos.x, Screen.height - screenPos.y);
        }

        private async Task<SimulateMouseResponse> ExecuteClick(
            SimulateMouseSchema parameters, EventSystem eventSystem, CancellationToken ct)
        {
            Vector2 inputPos = new Vector2(parameters.X, parameters.Y);
            Vector2 screenPos = InputToScreen(inputPos);
            RaycastResult? hit = RaycastUI(screenPos, eventSystem);

            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = screenPos,
                pressPosition = screenPos,
                button = PointerEventData.InputButton.Left
            };

            GameObject? target = null;
            GameObject? pressTarget = null;
            GameObject? clickTarget = null;

            if (hit != null)
            {
                GameObject rawTarget = hit.Value.gameObject;
                pointerData.pointerCurrentRaycast = hit.Value;
                pointerData.pointerPressRaycast = hit.Value;

                // Execute dispatches only to the exact target; composite controls (Button with Text child) need hierarchy traversal
                pressTarget = ExecuteEvents.GetEventHandler<IPointerDownHandler>(rawTarget);
                clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(rawTarget);
                target = pressTarget ?? clickTarget;

                if (target != null)
                {
                    pointerData.pointerPress = target;
                    pointerData.rawPointerPress = rawTarget;
                }
            }

            SimulateMouseOverlayState.Update(
                MouseAction.Click, inputPos, null,
                target?.name);

            await PlayExpandAnimation(ct);

            // Fire click events after expand animation so the user sees where the click lands
            if (hit != null)
            {
                if (pressTarget != null)
                {
                    ExecuteEvents.ExecuteHierarchy(
                        hit.Value.gameObject, pointerData, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(pressTarget, pointerData, ExecuteEvents.pointerUpHandler);
                }

                if (clickTarget != null)
                {
                    ExecuteEvents.Execute(clickTarget, pointerData, ExecuteEvents.pointerClickHandler);
                }
            }

            await PlayDissipateAnimation(ct);

            return new SimulateMouseResponse
            {
                Success = true,
                Message = target != null
                    ? $"Clicked '{target.name}' at ({inputPos.x:F1}, {inputPos.y:F1})"
                    : $"Clicked at ({inputPos.x:F1}, {inputPos.y:F1}) - no UI element hit",
                Action = MouseAction.Click.ToString(),
                HitGameObjectName = target?.name,
                PositionX = inputPos.x,
                PositionY = inputPos.y
            };
        }

        private PointerEventData InitiateDrag(
            EventSystem eventSystem,
            Vector2 screenPos,
            RaycastResult raycastResult,
            GameObject dragTarget)
        {
            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = screenPos,
                pressPosition = screenPos,
                button = PointerEventData.InputButton.Left,
                pointerCurrentRaycast = raycastResult,
                pointerPressRaycast = raycastResult,
                pointerDrag = dragTarget,
                rawPointerPress = raycastResult.gameObject
            };

            // Slider.OnPointerDown initializes m_Offset for handle positioning
            GameObject? pressTarget = ExecuteEvents.ExecuteHierarchy(
                raycastResult.gameObject, pointerData, ExecuteEvents.pointerDownHandler);
            pointerData.pointerPress = pressTarget;

            // ScrollRect.OnInitializePotentialDrag clears inertia, Slider sets useDragThreshold=false
            ExecuteEvents.Execute(dragTarget, pointerData, ExecuteEvents.initializePotentialDrag);

            return pointerData;
        }

        private async Task<SimulateMouseResponse> ExecuteDragOneShot(
            SimulateMouseSchema parameters, EventSystem eventSystem, CancellationToken ct)
        {
            Vector2 inputStart = new Vector2(parameters.FromX, parameters.FromY);
            Vector2 inputEnd = new Vector2(parameters.X, parameters.Y);
            Vector2 screenStart = InputToScreen(inputStart);
            Vector2 screenEnd = InputToScreen(inputEnd);
            RaycastResult? hit = RaycastUI(screenStart, eventSystem);

            // Execute dispatches only to the exact target; resolve the actual drag handler up the hierarchy
            GameObject? target = hit != null
                ? ExecuteEvents.GetEventHandler<IDragHandler>(hit.Value.gameObject)
                : null;

            if (target == null)
            {
                SimulateMouseOverlayState.Update(
                    MouseAction.Drag, inputStart, null, null);
                await PlayExpandAnimation(ct);
                await PlayDissipateAnimation(ct);

                return new SimulateMouseResponse
                {
                    Success = true,
                    Message = $"Dragged from ({inputStart.x:F1}, {inputStart.y:F1}) to ({inputEnd.x:F1}, {inputEnd.y:F1}) - no draggable UI element hit",
                    Action = MouseAction.Drag.ToString(),
                    PositionX = inputStart.x,
                    PositionY = inputStart.y,
                    EndPositionX = inputEnd.x,
                    EndPositionY = inputEnd.y
                };
            }

            PointerEventData pointerData = InitiateDrag(eventSystem, screenStart, hit!.Value, target);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.beginDragHandler);
            pointerData.dragging = true;

            SimulateMouseOverlayState.Update(
                MouseAction.Drag, inputStart, inputStart, target.name);

            try
            {
                await PlayExpandAnimation(ct);
                await InterpolateDragPosition(pointerData, target, screenEnd, parameters.DragSpeed, ct);
                await EditorDelay.DelayFrame(1, ct);
            }
            finally
            {
                FinalizeDrag(pointerData, target);
            }

            SimulateMouseOverlayState.Update(
                MouseAction.Drag, inputEnd, inputStart, target.name);

            await PlayDissipateAnimation(ct);

            return new SimulateMouseResponse
            {
                Success = true,
                Message = $"Dragged '{target.name}' from ({inputStart.x:F1}, {inputStart.y:F1}) to ({inputEnd.x:F1}, {inputEnd.y:F1}) at {parameters.DragSpeed:F0} px/s",
                Action = MouseAction.Drag.ToString(),
                HitGameObjectName = target.name,
                PositionX = inputStart.x,
                PositionY = inputStart.y,
                EndPositionX = inputEnd.x,
                EndPositionY = inputEnd.y
            };
        }

        // Lifecycle must match StandaloneInputModule: raycast → pointerUp → drop → endDrag
        private void FinalizeDrag(PointerEventData pointerData, GameObject target)
        {
            UpdatePointerRaycast(pointerData);

            if (pointerData.pointerPress != null)
            {
                ExecuteEvents.Execute(pointerData.pointerPress, pointerData, ExecuteEvents.pointerUpHandler);
            }

            // Standard IDropHandler dispatch so Unity drop targets respond without manual workarounds
            GameObject? dropTarget = pointerData.pointerCurrentRaycast.gameObject;
            if (dropTarget != null)
            {
                ExecuteEvents.ExecuteHierarchy(dropTarget, pointerData, ExecuteEvents.dropHandler);
            }

            pointerData.dragging = false;
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.endDragHandler);
        }

        private void UpdatePointerRaycast(PointerEventData pointerData)
        {
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            pointerData.pointerCurrentRaycast = results.Count > 0
                ? results[0]
                : new RaycastResult();
        }

        private async Task InterpolateDragPosition(
            PointerEventData pointerData,
            GameObject target,
            Vector2 endPos,
            float dragSpeed,
            CancellationToken ct)
        {
            Debug.Assert(dragSpeed >= 0f, "dragSpeed must be non-negative");

            Vector2 startPos = pointerData.position;
            float distance = Vector2.Distance(startPos, endPos);
            float duration = dragSpeed > 0f ? distance / dragSpeed : 0f;

            if (duration <= 0f)
            {
                await EditorDelay.DelayFrame(1, ct);

                Vector2 previousPosition = pointerData.position;
                pointerData.position = endPos;
                pointerData.delta = endPos - previousPosition;
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.dragHandler);

                SimulateMouseOverlayState.UpdatePosition(ScreenToInput(endPos));
            }
            else
            {
                float startTime = Time.realtimeSinceStartup;
                float t;

                do
                {
                    await EditorDelay.DelayFrame(1, ct);

                    float elapsed = Time.realtimeSinceStartup - startTime;
                    t = Mathf.Clamp01(elapsed / duration);
                    Vector2 previousPosition = pointerData.position;
                    Vector2 currentPosition = Vector2.Lerp(startPos, endPos, t);

                    pointerData.position = currentPosition;
                    pointerData.delta = currentPosition - previousPosition;

                    ExecuteEvents.Execute(target, pointerData, ExecuteEvents.dragHandler);

                    SimulateMouseOverlayState.UpdatePosition(ScreenToInput(currentPosition));
                }
                while (t < 1.0f);
            }
        }

        private async Task<SimulateMouseResponse> ExecuteDragStart(
            SimulateMouseSchema parameters, EventSystem eventSystem, CancellationToken ct)
        {
            if (MouseDragState.IsDragging)
            {
                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = "A drag is already in progress. Call DragEnd first.",
                    Action = MouseAction.DragStart.ToString(),
                    PositionX = parameters.X,
                    PositionY = parameters.Y
                };
            }

            Vector2 inputPos = new Vector2(parameters.X, parameters.Y);
            Vector2 screenPos = InputToScreen(inputPos);
            RaycastResult? hit = RaycastUI(screenPos, eventSystem);

            GameObject? target = hit != null
                ? ExecuteEvents.GetEventHandler<IDragHandler>(hit.Value.gameObject)
                : null;

            if (target == null)
            {
                SimulateMouseOverlayState.Update(
                    MouseAction.DragStart, inputPos, null, null);
                await PlayExpandAnimation(ct);
                await PlayDissipateAnimation(ct);

                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = $"No draggable UI element at ({inputPos.x:F1}, {inputPos.y:F1}). Use find-game-objects or screenshot to verify positions.",
                    Action = MouseAction.DragStart.ToString(),
                    PositionX = inputPos.x,
                    PositionY = inputPos.y
                };
            }

            PointerEventData pointerData = InitiateDrag(eventSystem, screenPos, hit!.Value, target);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.beginDragHandler);
            pointerData.dragging = true;

            MouseDragState.Target = target;
            MouseDragState.PointerData = pointerData;

            SimulateMouseOverlayState.Update(
                MouseAction.DragStart, inputPos, inputPos, target.name);

            bool animationCompleted = false;
            try
            {
                await PlayExpandAnimation(ct);
                animationCompleted = true;
            }
            finally
            {
                // Cancellation during animation leaves beginDrag dispatched; clean up
                if (!animationCompleted)
                {
                    FinalizeDrag(pointerData, target);
                    MouseDragState.Clear();
                }
            }

            return new SimulateMouseResponse
            {
                Success = true,
                Message = $"Drag started on '{target.name}' at ({inputPos.x:F1}, {inputPos.y:F1})",
                Action = MouseAction.DragStart.ToString(),
                HitGameObjectName = target.name,
                PositionX = inputPos.x,
                PositionY = inputPos.y
            };
        }

        private async Task<SimulateMouseResponse> ExecuteDragMove(
            SimulateMouseSchema parameters, CancellationToken ct)
        {
            if (!MouseDragState.IsDragging)
            {
                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = "No drag in progress. Call DragStart first.",
                    Action = MouseAction.DragMove.ToString(),
                    PositionX = parameters.X,
                    PositionY = parameters.Y
                };
            }

            Debug.Assert(MouseDragState.Target != null, "Target must not be null when IsDragging is true");
            Debug.Assert(MouseDragState.PointerData != null, "PointerData must not be null when IsDragging is true");

            SimulateMouseResponse? invalidResponse = ValidateDragStillActive(parameters.Action);
            if (invalidResponse != null)
            {
                return invalidResponse;
            }

            Vector2 inputEnd = new Vector2(parameters.X, parameters.Y);
            Vector2 screenEnd = InputToScreen(inputEnd);

            SimulateMouseOverlayState.Update(
                MouseAction.DragMove,
                ScreenToInput(MouseDragState.PointerData!.position),
                SimulateMouseOverlayState.DragStartPosition,
                MouseDragState.Target!.name);

            // Cancellation leaves drag state intact so the user can continue with DragMove/DragEnd
            await InterpolateDragPosition(
                MouseDragState.PointerData!, MouseDragState.Target!, screenEnd,
                parameters.DragSpeed, ct);

            SimulateMouseOverlayState.AddWaypoint(inputEnd);

            return new SimulateMouseResponse
            {
                Success = true,
                Message = $"Drag moved on '{MouseDragState.Target!.name}' to ({inputEnd.x:F1}, {inputEnd.y:F1}) at {parameters.DragSpeed:F0} px/s",
                Action = MouseAction.DragMove.ToString(),
                HitGameObjectName = MouseDragState.Target.name,
                PositionX = inputEnd.x,
                PositionY = inputEnd.y
            };
        }

        private async Task<SimulateMouseResponse> ExecuteDragEnd(
            SimulateMouseSchema parameters, CancellationToken ct)
        {
            if (!MouseDragState.IsDragging)
            {
                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = "No drag in progress. Call DragStart first.",
                    Action = MouseAction.DragEnd.ToString(),
                    PositionX = parameters.X,
                    PositionY = parameters.Y
                };
            }

            Debug.Assert(MouseDragState.Target != null, "Target must not be null when IsDragging is true");
            Debug.Assert(MouseDragState.PointerData != null, "PointerData must not be null when IsDragging is true");

            SimulateMouseResponse? invalidResponse = ValidateDragStillActive(parameters.Action);
            if (invalidResponse != null)
            {
                return invalidResponse;
            }

            Vector2 inputEnd = new Vector2(parameters.X, parameters.Y);
            Vector2 screenEnd = InputToScreen(inputEnd);
            string targetName = MouseDragState.Target!.name;

            SimulateMouseOverlayState.Update(
                MouseAction.DragEnd,
                ScreenToInput(MouseDragState.PointerData!.position),
                SimulateMouseOverlayState.DragStartPosition,
                targetName);

            try
            {
                await InterpolateDragPosition(
                    MouseDragState.PointerData!, MouseDragState.Target!, screenEnd,
                    parameters.DragSpeed, ct);
                await EditorDelay.DelayFrame(1, ct);
            }
            finally
            {
                FinalizeDrag(MouseDragState.PointerData!, MouseDragState.Target!);
                MouseDragState.Clear();
            }

            SimulateMouseOverlayState.Update(
                MouseAction.DragEnd, inputEnd, null, targetName);

            await PlayDissipateAnimation(ct);

            return new SimulateMouseResponse
            {
                Success = true,
                Message = $"Drag ended on '{targetName}' at ({inputEnd.x:F1}, {inputEnd.y:F1}) at {parameters.DragSpeed:F0} px/s",
                Action = MouseAction.DragEnd.ToString(),
                HitGameObjectName = targetName,
                PositionX = inputEnd.x,
                PositionY = inputEnd.y
            };
        }

        // User input during a CLI drag can cause Unity's StandaloneInputModule to
        // release or reassign the drag, leaving MouseDragState stale.
        private SimulateMouseResponse? ValidateDragStillActive(MouseAction action)
        {
            if (!MouseDragState.Target!.activeInHierarchy)
            {
                MouseDragState.Clear();
                SimulateMouseOverlayState.Clear();
                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = "Drag target was destroyed or deactivated during drag.",
                    Action = action.ToString()
                };
            }

            if (!MouseDragState.PointerData!.dragging ||
                MouseDragState.PointerData.pointerDrag != MouseDragState.Target)
            {
                MouseDragState.Clear();
                SimulateMouseOverlayState.Clear();
                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = "Drag was interrupted by user input or system event.",
                    Action = action.ToString()
                };
            }

            return null;
        }

        private static async Task PlayExpandAnimation(CancellationToken ct)
        {
            SimulateMouseOverlay? overlay = SimulateMouseOverlay.Instance;
            Debug.Assert(overlay != null, "Overlay must exist before playing animation");

            // Previous dissipate sets alpha to 0; restore before expand starts
            overlay!.SetAlpha(1f);

            float startTime = Time.realtimeSinceStartup;
            float elapsed = 0f;
            while (elapsed < EXPAND_DURATION)
            {
                float t = elapsed / EXPAND_DURATION;
                overlay.SetCursorScale(Mathf.Lerp(EXPAND_START_SCALE, 1f, t));
                await EditorDelay.DelayFrame(1, ct);
                elapsed = Time.realtimeSinceStartup - startTime;
            }
            overlay.SetCursorScale(1f);
        }

        private static async Task PlayDissipateAnimation(CancellationToken ct)
        {
            SimulateMouseOverlay? overlay = SimulateMouseOverlay.Instance;
            Debug.Assert(overlay != null, "Overlay must exist before playing animation");

            float startTime = Time.realtimeSinceStartup;
            float elapsed = 0f;
            while (elapsed < DISSIPATE_DURATION)
            {
                float t = elapsed / DISSIPATE_DURATION;
                overlay!.SetCursorScale(Mathf.Lerp(1f, 0f, t));
                overlay!.SetAlpha(Mathf.Lerp(1f, 0f, t));
                await EditorDelay.DelayFrame(1, ct);
                elapsed = Time.realtimeSinceStartup - startTime;
            }
            overlay!.SetCursorScale(0f);
            overlay!.SetAlpha(0f);
            SimulateMouseOverlayState.Clear();
        }

        private RaycastResult? RaycastUI(Vector2 screenPosition, EventSystem eventSystem)
        {
            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };
            List<RaycastResult> results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);
            return results.Count > 0 ? results[0] : (RaycastResult?)null;
        }
    }
}
