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
            GameObject? target = RaycastUI(screenPos, eventSystem);

            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = screenPos,
                button = PointerEventData.InputButton.Left
            };

            if (target != null)
            {
                // ExecuteEvents dispatches only the specified handler; Button requires all three to transition visual state correctly
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);
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

        private async Task<SimulateMouseResponse> ExecuteDragOneShot(
            SimulateMouseSchema parameters, EventSystem eventSystem, CancellationToken ct)
        {
            Debug.Assert(parameters.DragSteps > 0, "DragSteps must be positive");

            Vector2 startPos = new Vector2(parameters.X, parameters.Y);
            Vector2 endPos = new Vector2(parameters.EndX, parameters.EndY);
            GameObject? target = RaycastUI(startPos, eventSystem);

            if (target == null)
            {
                return new SimulateMouseResponse
                {
                    Success = true,
                    Message = $"No UI element at ({startPos.x:F1}, {startPos.y:F1}) - drag not performed",
                    Action = MouseAction.Drag.ToString(),
                    PositionX = startPos.x,
                    PositionY = startPos.y,
                    EndPositionX = endPos.x,
                    EndPositionY = endPos.y
                };
            }

            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = startPos,
                button = PointerEventData.InputButton.Left,
                pressPosition = startPos,
                // Some UI components (e.g. ScrollRect) reference pointerDrag internally
                pointerDrag = target
            };

            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.beginDragHandler);

            // Each drag step and EndDrag run on separate frames so Unity processes layout/physics between events
            for (int i = 1; i <= parameters.DragSteps; i++)
            {
                try
                {
                    await EditorDelay.DelayFrame(1, ct);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation mid-drag must still fire EndDrag to avoid leaving UI in a half-dragged state
                    ExecuteEvents.Execute(target, pointerData, ExecuteEvents.endDragHandler);
                    throw;
                }

                float t = (float)i / parameters.DragSteps;
                Vector2 previousPosition = pointerData.position;
                Vector2 currentPosition = Vector2.Lerp(startPos, endPos, t);

                pointerData.position = currentPosition;
                pointerData.delta = currentPosition - previousPosition;

                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.dragHandler);
            }

            await EditorDelay.DelayFrame(1, ct);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.endDragHandler);

            return new SimulateMouseResponse
            {
                Success = true,
                Message = $"Dragged '{target.name}' from ({startPos.x:F1}, {startPos.y:F1}) to ({endPos.x:F1}, {endPos.y:F1}) in {parameters.DragSteps} steps",
                Action = MouseAction.Drag.ToString(),
                HitGameObjectName = target.name,
                PositionX = startPos.x,
                PositionY = startPos.y,
                EndPositionX = endPos.x,
                EndPositionY = endPos.y
            };
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
            GameObject? target = RaycastUI(screenPos, eventSystem);

            if (target == null)
            {
                return new SimulateMouseResponse
                {
                    Success = false,
                    Message = $"No UI element at ({screenPos.x:F1}, {screenPos.y:F1}). Use find-game-objects or screenshot to verify positions.",
                    Action = MouseAction.DragStart.ToString(),
                    PositionX = screenPos.x,
                    PositionY = screenPos.y
                };
            }

            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = screenPos,
                button = PointerEventData.InputButton.Left,
                pressPosition = screenPos,
                // Some UI components (e.g. ScrollRect) reference pointerDrag internally
                pointerDrag = target
            };

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

            ExecuteEvents.Execute(MouseDragState.Target, MouseDragState.PointerData, ExecuteEvents.endDragHandler);

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

        private GameObject? RaycastUI(Vector2 screenPosition, EventSystem eventSystem)
        {
            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };
            List<RaycastResult> results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);
            return results.Count > 0 ? results[0].gameObject : null;
        }
    }
}
