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
    /// Executes a complete mouse UI drag in one request.
    /// </summary>
    internal static class MouseUiOneShotDragExecutor
    {
        internal static async Task<SimulateMouseUiResponse> ExecuteDragOneShot(
            MouseUiSimulationCommand parameters,
            EventSystem? eventSystem,
            MouseUiMainThreadCleanupScheduler cleanupScheduler,
            CancellationToken ct)
        {
            Vector2 inputStart = new(parameters.FromX, parameters.FromY);
            Vector2 inputEnd = new(parameters.X, parameters.Y);
            Vector2 screenStart = MouseUiCoordinateConverter.InputToScreen(inputStart);
            (MouseUiDragStartTarget start, SimulateMouseUiResponse? targetFailureResponse) =
                MouseUiDragTargetResolver.Resolve(
                    parameters,
                    eventSystem,
                    MouseAction.Drag,
                    inputStart,
                    screenStart);
            if (targetFailureResponse != null)
            {
                return targetFailureResponse;
            }

            // Ahead of the drop target lookup, so a missing drop target does not hide why the option is refused.
            SimulateMouseUiResponse? dropRefusal =
                MouseUiDragTargetResolver.RefuseDropTargetOnUIToolkit(parameters, start.UIToolkitElementName);
            if (dropRefusal != null)
            {
                return dropRefusal;
            }

            (GameObject? explicitDropTarget, SimulateMouseUiResponse? dropFailureResponse) =
                MouseUiPointerTargetResolver.ResolveDropTargetPath(
                    parameters,
                    MouseAction.Drag,
                    inputEnd);
            if (dropFailureResponse != null)
            {
                return dropFailureResponse;
            }

            if (!start.HasTarget)
            {
                return await ExecuteNoTargetDragAsync(
                    parameters, inputStart, inputEnd, cleanupScheduler, ct).ConfigureAwait(false);
            }

            IMouseUiDragSession session = start.Begin(inputStart, screenStart);

            string targetName = session.Report.Name!;
            SimulateMouseUiOverlayState.Update(
                MouseAction.Drag, inputStart, inputStart, Handles.GetMainGameViewSize());

            // Any Paused exit inside this try still runs FinalizeDrag in the finally below
            // (pointerUp/drop/endDrag), so every branch reports the drag as finalized early
            // rather than merely "not yet dispatched" like Click/LongPress's pre-input pause.
            const string DragInterruptedDuringMotionMessage =
                "Drag was finalized early (pointerUp/drop/endDrag dispatched via cleanup) because Unity paused during Pause Point inspection before the drag motion finished. The drag may have stopped short of the target position.";

            try
            {
                SimulateMouseUiResponse? motionAbort = await AnimateDragMotionOrAbortAsync(
                    parameters,
                    session,
                    inputStart,
                    inputEnd,
                    targetName,
                    DragInterruptedDuringMotionMessage,
                    cleanupScheduler,
                    ct).ConfigureAwait(false);
                if (motionAbort != null)
                {
                    return motionAbort;
                }
            }
            finally
            {
                cleanupScheduler.ExecuteCleanupOnMainThread(() => session.Finish(explicitDropTarget));
            }

            // Why switch after the helper: AnimateDragMotionOrAbortAsync ends on the main
            // thread, but ConfigureAwait(false) can resume this method off-main before
            // OverlayState.Update / PlayDissipateAnimation.
            await MainThreadSwitcher.SwitchToMainThread(ct);

            SimulateMouseUiOverlayState.Update(
                MouseAction.Drag, inputEnd, inputStart, Handles.GetMainGameViewSize());

            MouseUiFrameWaitOutcome dissipateOutcome = await MouseUiOverlayAnimator.PlayDissipateAnimation(ct).ConfigureAwait(false);
            if (dissipateOutcome == MouseUiFrameWaitOutcome.TimedOut)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateDragResult(parameters, inputStart, inputEnd, session.Report);
            }
            if (dissipateOutcome == MouseUiFrameWaitOutcome.Paused)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                    MouseAction.Drag, inputStart, targetName,
                    "Drag was already completed (target position reached, pointerUp/drop/endDrag dispatched). Unity paused during Pause Point inspection while the overlay animation was still playing; only the animation was interrupted.");
            }
            await MainThreadSwitcher.SwitchToMainThread(ct);

            return MouseUiSimulationResponseFactory.CreateDragResult(parameters, inputStart, inputEnd, session.Report);
        }

        private static async Task<SimulateMouseUiResponse> ExecuteNoTargetDragAsync(
            MouseUiSimulationCommand parameters,
            Vector2 inputStart,
            Vector2 inputEnd,
            MouseUiMainThreadCleanupScheduler cleanupScheduler,
            CancellationToken ct)
        {
            const string NoTargetInterruptedMessage =
                "Drag stopped because Unity paused during Pause Point inspection. No draggable target was found at the start position, so no drag was initiated.";
            SimulateMouseUiOverlayState.Update(
                MouseAction.Drag, inputStart, null, Handles.GetMainGameViewSize());
            SimulateMouseUiResponse? expandAbort = AbortDragOnOverlayOutcome(
                await MouseUiOverlayAnimator.PlayExpandAnimation(ct).ConfigureAwait(false),
                inputStart,
                inputEnd,
                null,
                NoTargetInterruptedMessage,
                cleanupScheduler);
            if (expandAbort != null)
            {
                return expandAbort;
            }

            await MainThreadSwitcher.SwitchToMainThread(ct);

            SimulateMouseUiResponse? dissipateAbort = AbortDragOnOverlayOutcome(
                await MouseUiOverlayAnimator.PlayDissipateAnimation(ct).ConfigureAwait(false),
                inputStart,
                inputEnd,
                null,
                NoTargetInterruptedMessage,
                cleanupScheduler);
            if (dissipateAbort != null)
            {
                return dissipateAbort;
            }

            await MainThreadSwitcher.SwitchToMainThread(ct);

            return new SimulateMouseUiResponse
            {
                Success = false,
                Message = parameters.BypassRaycast
                    ? $"TargetPath '{parameters.TargetPath}' has no drag handler."
                    : $"No draggable UI element at ({inputStart.x:F1}, {inputStart.y:F1}). Use find-game-objects or screenshot to verify positions.",
                Action = MouseAction.Drag.ToString(),
                PositionX = inputStart.x,
                PositionY = inputStart.y,
                EndPositionX = inputEnd.x,
                EndPositionY = inputEnd.y
            };
        }

        private static async Task<SimulateMouseUiResponse?> AnimateDragMotionOrAbortAsync(
            MouseUiSimulationCommand parameters,
            IMouseUiDragSession session,
            Vector2 inputStart,
            Vector2 inputEnd,
            string targetName,
            string interruptedMessage,
            MouseUiMainThreadCleanupScheduler cleanupScheduler,
            CancellationToken ct)
        {
            SimulateMouseUiResponse? expandAbort = AbortDragOnOverlayOutcome(
                await MouseUiOverlayAnimator.PlayExpandAnimation(ct).ConfigureAwait(false),
                inputStart,
                inputEnd,
                targetName,
                interruptedMessage,
                cleanupScheduler);
            if (expandAbort != null)
            {
                return expandAbort;
            }

            await MainThreadSwitcher.SwitchToMainThread(ct);

            SimulateMouseUiResponse? dragAbort = AbortDragOnOverlayOutcome(
                await MouseUiDragEventExecutor.InterpolateDragPosition(session, inputEnd, parameters.DragSpeed, ct)
                    .ConfigureAwait(false),
                inputStart,
                inputEnd,
                targetName,
                interruptedMessage,
                cleanupScheduler);
            if (dragAbort != null)
            {
                return dragAbort;
            }

            await MainThreadSwitcher.SwitchToMainThread(ct);

            SimulateMouseUiResponse? settleAbort = AbortDragOnOverlayOutcome(
                await MouseUiEditorFrameWaiter.WaitForEditorFrameAndSwitchToMainThreadAsync(ct).ConfigureAwait(false),
                inputStart,
                inputEnd,
                targetName,
                interruptedMessage,
                cleanupScheduler);
            if (settleAbort != null)
            {
                return settleAbort;
            }

            await MainThreadSwitcher.SwitchToMainThread(ct);
            return null;
        }

        private static SimulateMouseUiResponse? AbortDragOnOverlayOutcome(
            MouseUiFrameWaitOutcome outcome,
            Vector2 inputStart,
            Vector2 inputEnd,
            string? targetName,
            string interruptedMessage,
            MouseUiMainThreadCleanupScheduler cleanupScheduler)
        {
            if (outcome == MouseUiFrameWaitOutcome.TimedOut)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(MouseAction.Drag, inputStart, inputEnd, targetName);
            }

            if (outcome == MouseUiFrameWaitOutcome.Paused)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                    MouseAction.Drag, inputStart, targetName, interruptedMessage);
            }

            return null;
        }
    }
}
