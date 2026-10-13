#nullable enable
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    /// <summary>
    /// Executes incremental mouse UI drag start, move, and end requests.
    /// </summary>
    internal static class MouseUiIncrementalDragExecutor
    {
        internal static async Task<SimulateMouseUiResponse> ExecuteDragStart(
            MouseUiSimulationCommand parameters,
            EventSystem eventSystem,
            MouseUiMainThreadCleanupScheduler cleanupScheduler,
            CancellationToken ct)
        {
            if (MouseDragState.IsDragging)
            {
                return new SimulateMouseUiResponse
                {
                    Success = false,
                    Message = "A drag is already in progress. Call DragEnd first.",
                    Action = MouseAction.DragStart.ToString(),
                    PositionX = parameters.X,
                    PositionY = parameters.Y
                };
            }

            Vector2 inputPos = new(parameters.X, parameters.Y);
            Vector2 screenPos = MouseUiCoordinateConverter.InputToScreen(inputPos);
            (RaycastResult startRaycast, GameObject? target, SimulateMouseUiResponse? targetFailureResponse) =
                MouseUiDragTargetResolver.Resolve(
                    parameters,
                    eventSystem,
                    MouseAction.DragStart,
                    inputPos,
                    screenPos);
            if (targetFailureResponse != null)
            {
                return targetFailureResponse;
            }

            if (target == null)
            {
                SimulateMouseUiOverlayState.Update(
                    MouseAction.DragStart, inputPos, null, Handles.GetMainGameViewSize());
                MouseUiFrameWaitOutcome noTargetExpandOutcome = await MouseUiOverlayAnimator.PlayExpandAnimation(ct).ConfigureAwait(false);
                if (noTargetExpandOutcome == MouseUiFrameWaitOutcome.TimedOut)
                {
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(MouseAction.DragStart, inputPos, null, null);
                }
                if (noTargetExpandOutcome == MouseUiFrameWaitOutcome.Paused)
                {
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                        MouseAction.DragStart, inputPos, null,
                        "DragStart stopped because Unity paused during Pause Point inspection. No draggable target was found at the position, so no drag was initiated.");
                }
                await MainThreadSwitcher.SwitchToMainThread(ct);

                MouseUiFrameWaitOutcome noTargetDissipateOutcome = await MouseUiOverlayAnimator.PlayDissipateAnimation(ct).ConfigureAwait(false);
                if (noTargetDissipateOutcome == MouseUiFrameWaitOutcome.TimedOut)
                {
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(MouseAction.DragStart, inputPos, null, null);
                }
                if (noTargetDissipateOutcome == MouseUiFrameWaitOutcome.Paused)
                {
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                        MouseAction.DragStart, inputPos, null,
                        "DragStart stopped because Unity paused during Pause Point inspection. No draggable target was found at the position, so no drag was initiated.");
                }
                await MainThreadSwitcher.SwitchToMainThread(ct);

                return new SimulateMouseUiResponse
                {
                    Success = false,
                    Message = parameters.BypassRaycast
                        ? $"TargetPath '{parameters.TargetPath}' has no drag handler."
                        : $"No draggable UI element at ({inputPos.x:F1}, {inputPos.y:F1}). Use find-game-objects or screenshot to verify positions.",
                    Action = MouseAction.DragStart.ToString(),
                    PositionX = inputPos.x,
                    PositionY = inputPos.y
                };
            }

            IMouseUiDragSession session = UguiDragSession.Begin(eventSystem, screenPos, startRaycast, target);
            MouseDragState.Session = session;

            string targetName = session.Report.Name!;
            SimulateMouseUiOverlayState.Update(
                MouseAction.DragStart, inputPos, inputPos, Handles.GetMainGameViewSize());

            bool animationCompleted = false;
            try
            {
                MouseUiFrameWaitOutcome expandOutcome = await MouseUiOverlayAnimator.PlayExpandAnimation(ct).ConfigureAwait(false);
                if (expandOutcome == MouseUiFrameWaitOutcome.TimedOut)
                {
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(MouseAction.DragStart, inputPos, null, targetName);
                }
                if (expandOutcome == MouseUiFrameWaitOutcome.Paused)
                {
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                        MouseAction.DragStart, inputPos, targetName,
                        "DragStart was finalized early (pointerUp/drop/endDrag dispatched via cleanup) because Unity paused during Pause Point inspection before the start animation finished. No drag session is active; call DragStart again to retry.");
                }
                await MainThreadSwitcher.SwitchToMainThread(ct);
                animationCompleted = true;
            }
            finally
            {
                // Cancellation during animation leaves beginDrag dispatched; clean up
                if (!animationCompleted)
                {
                    cleanupScheduler.ExecuteCleanupOnMainThread(() =>
                    {
                        session.Finish(null);
                        MouseDragState.Clear();
                    });
                }
            }

            return new SimulateMouseUiResponse
            {
                Success = true,
                Message = $"Drag started on '{targetName}' at ({inputPos.x:F1}, {inputPos.y:F1})",
                Action = MouseAction.DragStart.ToString(),
                HitGameObjectName = targetName,
                PositionX = inputPos.x,
                PositionY = inputPos.y
            };
        }

        internal static async Task<SimulateMouseUiResponse> ExecuteDragMove(
            MouseUiSimulationCommand parameters,
            MouseUiMainThreadCleanupScheduler cleanupScheduler,
            CancellationToken ct)
        {
            if (!MouseDragState.IsDragging)
            {
                return new SimulateMouseUiResponse
                {
                    Success = false,
                    Message = "No drag in progress. Call DragStart first.",
                    Action = MouseAction.DragMove.ToString(),
                    PositionX = parameters.X,
                    PositionY = parameters.Y
                };
            }

            Debug.Assert(MouseDragState.Session != null, "Session must not be null when IsDragging is true");

            SimulateMouseUiResponse? invalidResponse = ValidateDragStillActive(parameters.Action);
            if (invalidResponse != null)
            {
                return invalidResponse;
            }

            Vector2 inputEnd = new(parameters.X, parameters.Y);
            IMouseUiDragSession session = MouseDragState.Session!;
            string targetName = session.Report.Name!;

            SimulateMouseUiOverlayState.Update(
                MouseAction.DragMove,
                session.CurrentInputPosition,
                SimulateMouseUiOverlayState.DragStartPosition,
                Handles.GetMainGameViewSize());

            // Cancellation leaves drag state intact so the user can continue with DragMove/DragEnd
            MouseUiFrameWaitOutcome dragOutcome = await MouseUiDragEventExecutor.InterpolateDragPosition(
                session, inputEnd,
                parameters.DragSpeed, ct).ConfigureAwait(false);
            if (dragOutcome == MouseUiFrameWaitOutcome.TimedOut)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(MouseAction.DragMove, inputEnd, null, targetName);
            }
            if (dragOutcome == MouseUiFrameWaitOutcome.Paused)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                    MouseAction.DragMove, inputEnd, targetName,
                    "DragMove was interrupted because Unity paused during Pause Point inspection while interpolating. The drag session is still active (not finalized); the pointer may not have reached the requested position. Call DragMove or DragEnd to continue.");
            }
            await MainThreadSwitcher.SwitchToMainThread(ct);

            SimulateMouseUiOverlayState.AddWaypoint(inputEnd);

            return new SimulateMouseUiResponse
            {
                Success = true,
                Message = $"Drag moved on '{targetName}' to ({inputEnd.x:F1}, {inputEnd.y:F1}) at {parameters.DragSpeed:F0} px/s",
                Action = MouseAction.DragMove.ToString(),
                HitGameObjectName = targetName,
                PositionX = inputEnd.x,
                PositionY = inputEnd.y
            };
        }

        internal static async Task<SimulateMouseUiResponse> ExecuteDragEnd(
            MouseUiSimulationCommand parameters,
            MouseUiMainThreadCleanupScheduler cleanupScheduler,
            CancellationToken ct)
        {
            if (!MouseDragState.IsDragging)
            {
                return new SimulateMouseUiResponse
                {
                    Success = false,
                    Message = "No drag in progress. Call DragStart first.",
                    Action = MouseAction.DragEnd.ToString(),
                    PositionX = parameters.X,
                    PositionY = parameters.Y
                };
            }

            Debug.Assert(MouseDragState.Session != null, "Session must not be null when IsDragging is true");

            SimulateMouseUiResponse? invalidResponse = ValidateDragStillActive(parameters.Action);
            if (invalidResponse != null)
            {
                return invalidResponse;
            }

            Vector2 inputEnd = new(parameters.X, parameters.Y);
            IMouseUiDragSession session = MouseDragState.Session!;
            string targetName = session.Report.Name!;
            (GameObject? explicitDropTarget, SimulateMouseUiResponse? dropFailureResponse) =
                MouseUiPointerTargetResolver.ResolveDropTargetPath(
                    parameters,
                    MouseAction.DragEnd,
                    inputEnd);
            if (dropFailureResponse != null)
            {
                return dropFailureResponse;
            }

            SimulateMouseUiOverlayState.Update(
                MouseAction.DragEnd,
                session.CurrentInputPosition,
                SimulateMouseUiOverlayState.DragStartPosition,
                Handles.GetMainGameViewSize());

            // Any Paused exit inside this try still runs FinalizeDrag + MouseDragState.Clear()
            // in the finally below, so every in-try branch reports the drag as finalized early.
            const string DragEndInterruptedMessage =
                "DragEnd was finalized early (pointerUp/drop/endDrag dispatched via cleanup, drag state cleared) because Unity paused during Pause Point inspection before the drag motion finished. The drag may have stopped short of the target position.";

            try
            {
                MouseUiFrameWaitOutcome dragOutcome = await MouseUiDragEventExecutor.InterpolateDragPosition(
                    session, inputEnd,
                    parameters.DragSpeed, ct).ConfigureAwait(false);
                if (dragOutcome == MouseUiFrameWaitOutcome.TimedOut)
                {
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(MouseAction.DragEnd, inputEnd, null, targetName);
                }
                if (dragOutcome == MouseUiFrameWaitOutcome.Paused)
                {
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                        MouseAction.DragEnd, inputEnd, targetName, DragEndInterruptedMessage);
                }
                await MainThreadSwitcher.SwitchToMainThread(ct);

                MouseUiFrameWaitOutcome settleOutcome = await MouseUiEditorFrameWaiter.WaitForEditorFrameAndSwitchToMainThreadAsync(ct).ConfigureAwait(false);
                if (settleOutcome == MouseUiFrameWaitOutcome.TimedOut)
                {
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(MouseAction.DragEnd, inputEnd, null, targetName);
                }
                if (settleOutcome == MouseUiFrameWaitOutcome.Paused)
                {
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                        MouseAction.DragEnd, inputEnd, targetName, DragEndInterruptedMessage);
                }
                await MainThreadSwitcher.SwitchToMainThread(ct);
            }
            finally
            {
                cleanupScheduler.ExecuteCleanupOnMainThread(() =>
                {
                    session.Finish(explicitDropTarget);
                    MouseDragState.Clear();
                });
            }

            SimulateMouseUiOverlayState.Update(
                MouseAction.DragEnd, inputEnd, null, Handles.GetMainGameViewSize());

            MouseUiFrameWaitOutcome dissipateOutcome = await MouseUiOverlayAnimator.PlayDissipateAnimation(ct).ConfigureAwait(false);
            if (dissipateOutcome == MouseUiFrameWaitOutcome.TimedOut)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateDragEndResult(parameters, inputEnd, targetName);
            }
            if (dissipateOutcome == MouseUiFrameWaitOutcome.Paused)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                    MouseAction.DragEnd, inputEnd, targetName,
                    "DragEnd was already completed (target position reached, pointerUp/drop/endDrag dispatched, drag state cleared). Unity paused during Pause Point inspection while the overlay animation was still playing; only the animation was interrupted.");
            }
            await MainThreadSwitcher.SwitchToMainThread(ct);

            return MouseUiSimulationResponseFactory.CreateDragEndResult(parameters, inputEnd, targetName);
        }

        private static SimulateMouseUiResponse? ValidateDragStillActive(MouseAction action)
        {
            string? reason = MouseDragState.Session!.CheckStillActive();
            if (reason == null)
            {
                return null;
            }

            MouseDragState.Clear();
            SimulateMouseUiOverlayState.Clear();
            return new SimulateMouseUiResponse
            {
                Success = false,
                Message = reason,
                Action = action.ToString()
            };
        }
    }
}
