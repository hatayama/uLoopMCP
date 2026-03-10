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

            VibeLogger.LogInfo(
                "simulate_mouse_start",
                "Mouse simulation started",
                new { Action = parameters.Action.ToString(), X = parameters.X, Y = parameters.Y },
                correlationId: correlationId
            );

            SimulateMouseResponse response;

            switch (parameters.Action)
            {
                case MouseAction.Click:
                    response = ExecuteClick(parameters, eventSystem);
                    break;

                case MouseAction.Drag:
                    response = await ExecuteDragOneShot(parameters, eventSystem, ct);
                    break;

                case MouseAction.DragStart:
                    response = ExecuteDragStart(parameters, eventSystem);
                    break;

                case MouseAction.DragMove:
                    response = ExecuteDragMove(parameters);
                    break;

                case MouseAction.DragEnd:
                    response = ExecuteDragEnd(parameters);
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

        private SimulateMouseResponse ExecuteClick(SimulateMouseSchema parameters, EventSystem eventSystem)
        {
            Vector2 screenPos = new Vector2(parameters.X, parameters.Y);
            RaycastResult? hit = RaycastUI(screenPos, eventSystem);

            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = screenPos,
                pressPosition = screenPos,
                button = PointerEventData.InputButton.Left
            };

            GameObject? target = null;

            if (hit != null)
            {
                GameObject rawTarget = hit.Value.gameObject;
                pointerData.pointerCurrentRaycast = hit.Value;
                pointerData.pointerPressRaycast = hit.Value;

                // Execute dispatches only to the exact target; composite controls (Button with Text child) need hierarchy traversal
                GameObject? pressTarget = ExecuteEvents.ExecuteHierarchy(
                    rawTarget, pointerData, ExecuteEvents.pointerDownHandler);
                GameObject? clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(rawTarget);

                target = pressTarget ?? clickTarget;

                if (target != null)
                {
                    pointerData.pointerPress = target;
                    pointerData.rawPointerPress = rawTarget;
                }

                if (pressTarget != null)
                {
                    ExecuteEvents.Execute(pressTarget, pointerData, ExecuteEvents.pointerUpHandler);
                }

                if (clickTarget != null)
                {
                    ExecuteEvents.Execute(clickTarget, pointerData, ExecuteEvents.pointerClickHandler);
                }
            }

            return new SimulateMouseResponse
            {
                Success = true,
                Message = target != null
                    ? $"Clicked '{target.name}' at ({screenPos.x:F1}, {screenPos.y:F1})"
                    : $"Clicked at ({screenPos.x:F1}, {screenPos.y:F1}) - no UI element hit",
                Action = MouseAction.Click.ToString(),
                HitGameObjectName = target != null ? target.name : null,
                PositionX = screenPos.x,
                PositionY = screenPos.y
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
            Debug.Assert(parameters.DragSpeed >= 0f, "DragSpeed must be non-negative");

            Vector2 startPos = new Vector2(parameters.X, parameters.Y);
            Vector2 endPos = new Vector2(parameters.EndX, parameters.EndY);
            RaycastResult? hit = RaycastUI(startPos, eventSystem);

            // Execute dispatches only to the exact target; resolve the actual drag handler up the hierarchy
            GameObject? target = hit != null
                ? ExecuteEvents.GetEventHandler<IDragHandler>(hit.Value.gameObject)
                : null;

            if (target == null)
            {
                return new SimulateMouseResponse
                {
                    Success = true,
                    Message = $"No draggable UI element at ({startPos.x:F1}, {startPos.y:F1}) - drag not performed",
                    Action = MouseAction.Drag.ToString(),
                    PositionX = startPos.x,
                    PositionY = startPos.y,
                    EndPositionX = endPos.x,
                    EndPositionY = endPos.y
                };
            }

            PointerEventData pointerData = InitiateDrag(eventSystem, startPos, hit!.Value, target);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.beginDragHandler);

            float distance = Vector2.Distance(startPos, endPos);
            float duration = parameters.DragSpeed > 0f ? distance / parameters.DragSpeed : 0f;

            if (duration <= 0f)
            {
                // Speed=0 or distance=0: skip interpolation but still fire one Drag event on a separate frame
                await DelayFrameWithDragSafety(pointerData, target, ct);

                Vector2 previousPosition = pointerData.position;
                pointerData.position = endPos;
                pointerData.delta = endPos - previousPosition;
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.dragHandler);
            }
            else
            {
                float startTime = Time.realtimeSinceStartup;
                float t;

                do
                {
                    await DelayFrameWithDragSafety(pointerData, target, ct);

                    float elapsed = Time.realtimeSinceStartup - startTime;
                    t = Mathf.Clamp01(elapsed / duration);
                    Vector2 previousPosition = pointerData.position;
                    Vector2 currentPosition = Vector2.Lerp(startPos, endPos, t);

                    pointerData.position = currentPosition;
                    pointerData.delta = currentPosition - previousPosition;

                    ExecuteEvents.Execute(target, pointerData, ExecuteEvents.dragHandler);
                }
                while (t < 1.0f);
            }

            await EditorDelay.DelayFrame(1, ct);

            FinalizeDrag(pointerData, target);

            return new SimulateMouseResponse
            {
                Success = true,
                Message = $"Dragged '{target.name}' from ({startPos.x:F1}, {startPos.y:F1}) to ({endPos.x:F1}, {endPos.y:F1}) at {parameters.DragSpeed:F0} px/s",
                Action = MouseAction.Drag.ToString(),
                HitGameObjectName = target.name,
                PositionX = startPos.x,
                PositionY = startPos.y,
                EndPositionX = endPos.x,
                EndPositionY = endPos.y
            };
        }

        // pointerUp must fire before endDrag to match StandaloneInputModule lifecycle
        private void FinalizeDrag(PointerEventData pointerData, GameObject target)
        {
            if (pointerData.pointerPress != null)
            {
                ExecuteEvents.Execute(pointerData.pointerPress, pointerData, ExecuteEvents.pointerUpHandler);
            }

            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.endDragHandler);
        }

        // Cancellation mid-drag must still complete the lifecycle to avoid leaving UI in a half-dragged state
        private async Task DelayFrameWithDragSafety(
            PointerEventData pointerData, GameObject target, CancellationToken ct)
        {
            try
            {
                await EditorDelay.DelayFrame(1, ct);
            }
            catch (OperationCanceledException)
            {
                FinalizeDrag(pointerData, target);
                throw;
            }
        }

        private SimulateMouseResponse ExecuteDragStart(SimulateMouseSchema parameters, EventSystem eventSystem)
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

            Vector2 screenPos = new Vector2(parameters.X, parameters.Y);
            RaycastResult? hit = RaycastUI(screenPos, eventSystem);

            GameObject? target = hit != null
                ? ExecuteEvents.GetEventHandler<IDragHandler>(hit.Value.gameObject)
                : null;

            if (target == null)
            {
                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = $"No draggable UI element at ({screenPos.x:F1}, {screenPos.y:F1}). Use find-game-objects or screenshot to verify positions.",
                    Action = MouseAction.DragStart.ToString(),
                    PositionX = screenPos.x,
                    PositionY = screenPos.y
                };
            }

            PointerEventData pointerData = InitiateDrag(eventSystem, screenPos, hit!.Value, target);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.beginDragHandler);

            MouseDragState.Target = target;
            MouseDragState.PointerData = pointerData;

            return new SimulateMouseResponse
            {
                Success = true,
                Message = $"Drag started on '{target.name}' at ({screenPos.x:F1}, {screenPos.y:F1})",
                Action = MouseAction.DragStart.ToString(),
                HitGameObjectName = target.name,
                PositionX = screenPos.x,
                PositionY = screenPos.y
            };
        }

        private SimulateMouseResponse ExecuteDragMove(SimulateMouseSchema parameters)
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

            Vector2 newPos = new Vector2(parameters.X, parameters.Y);
            Vector2 previousPosition = MouseDragState.PointerData!.position;

            MouseDragState.PointerData.position = newPos;
            MouseDragState.PointerData.delta = newPos - previousPosition;

            ExecuteEvents.Execute(MouseDragState.Target!, MouseDragState.PointerData, ExecuteEvents.dragHandler);

            return new SimulateMouseResponse
            {
                Success = true,
                Message = $"Drag moved on '{MouseDragState.Target!.name}' to ({newPos.x:F1}, {newPos.y:F1})",
                Action = MouseAction.DragMove.ToString(),
                HitGameObjectName = MouseDragState.Target.name,
                PositionX = newPos.x,
                PositionY = newPos.y
            };
        }

        private SimulateMouseResponse ExecuteDragEnd(SimulateMouseSchema parameters)
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

            Vector2 endPos = new Vector2(parameters.X, parameters.Y);
            MouseDragState.PointerData!.position = endPos;

            string targetName = MouseDragState.Target!.name;

            FinalizeDrag(MouseDragState.PointerData, MouseDragState.Target);

            MouseDragState.Clear();

            return new SimulateMouseResponse
            {
                Success = true,
                Message = $"Drag ended on '{targetName}' at ({endPos.x:F1}, {endPos.y:F1})",
                Action = MouseAction.DragEnd.ToString(),
                HitGameObjectName = targetName,
                PositionX = endPos.x,
                PositionY = endPos.y
            };
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
