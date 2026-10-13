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
    /// Executes click and long-press mouse UI actions.
    /// </summary>
    internal static class MouseUiPressActionExecutor
    {
        internal static async Task<SimulateMouseUiResponse> ExecuteClick(
            MouseUiSimulationCommand parameters,
            EventSystem eventSystem,
            MouseUiMainThreadCleanupScheduler cleanupScheduler,
            CancellationToken ct)
        {
            Vector2 inputPos = new(parameters.X, parameters.Y);
            (IMouseUiPressTarget? resolvedTarget, SimulateMouseUiResponse? failure) =
                ResolvePressTarget(parameters, eventSystem, inputPos, MouseAction.Click);
            if (failure != null)
            {
                return failure;
            }

            IMouseUiPressTarget pressTarget = resolvedTarget!;
            MouseUiHitReport report = pressTarget.Report;
            if (parameters.BypassRaycast && !report.HasHit)
            {
                return new SimulateMouseUiResponse
                {
                    Success = false,
                    Message = $"TargetPath '{parameters.TargetPath}' has no pointer click or pointer down handler.",
                    Action = MouseAction.Click.ToString(),
                    PositionX = inputPos.x,
                    PositionY = inputPos.y
                };
            }

            string? targetName = report.Name;
            SimulateMouseUiOverlayState.Update(
                MouseAction.Click, inputPos, null,
                Handles.GetMainGameViewSize());

            MouseUiFrameWaitOutcome expandOutcome = await MouseUiOverlayAnimator.PlayExpandAnimation(ct).ConfigureAwait(false);
            if (expandOutcome == MouseUiFrameWaitOutcome.TimedOut)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(MouseAction.Click, inputPos, null, targetName);
            }
            if (expandOutcome == MouseUiFrameWaitOutcome.Paused)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                    MouseAction.Click, inputPos, targetName,
                    "Click stopped because Unity paused during Pause Point inspection before the click was dispatched. No pointer event was fired.");
            }
            await MainThreadSwitcher.SwitchToMainThread(ct);

            // Fire click events after expand animation so the user sees where the click lands
            pressTarget.Click();

            MouseUiFrameWaitOutcome dissipateOutcome = await MouseUiOverlayAnimator.PlayDissipateAnimation(ct).ConfigureAwait(false);
            if (dissipateOutcome == MouseUiFrameWaitOutcome.TimedOut)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateClickResult(parameters, inputPos, report);
            }
            if (dissipateOutcome == MouseUiFrameWaitOutcome.Paused)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                    MouseAction.Click, inputPos, targetName,
                    "Click was already dispatched. Unity paused during Pause Point inspection while the click overlay animation was still playing; only the animation was interrupted.");
            }
            await MainThreadSwitcher.SwitchToMainThread(ct);

            return MouseUiSimulationResponseFactory.CreateClickResult(parameters, inputPos, report);
        }

        internal static async Task<SimulateMouseUiResponse> ExecuteLongPress(
            MouseUiSimulationCommand parameters,
            EventSystem eventSystem,
            MouseUiMainThreadCleanupScheduler cleanupScheduler,
            CancellationToken ct)
        {
            SimulateMouseUiResponse? invalidDuration = CreateInvalidLongPressDurationResponse(parameters.Duration);
            if (invalidDuration != null)
            {
                return invalidDuration;
            }

            Vector2 inputPos = new(parameters.X, parameters.Y);
            (IMouseUiPressTarget? resolvedTarget, SimulateMouseUiResponse? failure) =
                ResolvePressTarget(parameters, eventSystem, inputPos, MouseAction.LongPress);
            if (failure != null)
            {
                return failure;
            }

            IMouseUiPressTarget pressTarget = resolvedTarget!;
            MouseUiHitReport report = pressTarget.Report;
            if (parameters.BypassRaycast && !report.HasHit)
            {
                return new SimulateMouseUiResponse
                {
                    Success = false,
                    Message = $"TargetPath '{parameters.TargetPath}' has no pointer down or pointer click handler.",
                    Action = MouseAction.LongPress.ToString(),
                    PositionX = inputPos.x,
                    PositionY = inputPos.y
                };
            }

            string? targetName = report.Name;
            SimulateMouseUiOverlayState.Update(
                MouseAction.LongPress, inputPos, null,
                Handles.GetMainGameViewSize());

            MouseUiFrameWaitOutcome expandOutcome = await MouseUiOverlayAnimator.PlayExpandAnimation(ct).ConfigureAwait(false);
            if (expandOutcome == MouseUiFrameWaitOutcome.TimedOut)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(MouseAction.LongPress, inputPos, null, targetName);
            }
            if (expandOutcome == MouseUiFrameWaitOutcome.Paused)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                    MouseAction.LongPress, inputPos, targetName,
                    "Long-press stopped because Unity paused during Pause Point inspection before pointerDown was dispatched. No pointer event was fired.");
            }
            await MainThreadSwitcher.SwitchToMainThread(ct);

            pressTarget.PressDown();

            try
            {
                SimulateMouseUiResponse? holdAbort = await HoldLongPressOrAbortAsync(
                    parameters.Duration, inputPos, targetName, cleanupScheduler, ct).ConfigureAwait(false);
                if (holdAbort != null)
                {
                    return holdAbort;
                }
            }
            finally
            {
                // Ensure pointerUp fires even if the hold loop is cancelled
                cleanupScheduler.ExecuteCleanupOnMainThread(pressTarget.Release);
            }

            // Why switch after the helper: HoldLongPressOrAbortAsync ends on the main thread,
            // but ConfigureAwait(false) can resume this method off-main before dissipate.
            await MainThreadSwitcher.SwitchToMainThread(ct);

            MouseUiFrameWaitOutcome dissipateOutcome = await MouseUiOverlayAnimator.PlayDissipateAnimation(ct).ConfigureAwait(false);
            if (dissipateOutcome == MouseUiFrameWaitOutcome.TimedOut)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateLongPressResult(parameters, inputPos, report);
            }
            if (dissipateOutcome == MouseUiFrameWaitOutcome.Paused)
            {
                cleanupScheduler.QueueOverlayClear();
                return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                    MouseAction.LongPress, inputPos, targetName,
                    "Long-press was already completed (pointerDown and pointerUp both dispatched). Unity paused during Pause Point inspection while the overlay animation was still playing; only the animation was interrupted.");
            }
            await MainThreadSwitcher.SwitchToMainThread(ct);

            return MouseUiSimulationResponseFactory.CreateLongPressResult(parameters, inputPos, report);
        }

        private static SimulateMouseUiResponse? CreateInvalidLongPressDurationResponse(float duration)
        {
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                return new SimulateMouseUiResponse
                {
                    Success = false,
                    Message = $"Duration must be positive, got: {duration}",
                    Action = MouseAction.LongPress.ToString()
                };
            }

            if (duration > SimulateInputConstants.MaxDurationSeconds)
            {
                return new SimulateMouseUiResponse
                {
                    Success = false,
                    Message =
                        $"Duration must be {SimulateInputConstants.MaxDurationSeconds} seconds or less, got: {duration}. The unit is seconds, not milliseconds.",
                    Action = MouseAction.LongPress.ToString()
                };
            }

            return null;
        }

        private static async Task<SimulateMouseUiResponse?> HoldLongPressOrAbortAsync(
            float duration,
            Vector2 inputPos,
            string? targetName,
            MouseUiMainThreadCleanupScheduler cleanupScheduler,
            CancellationToken ct)
        {
            // Hold for Duration seconds, updating elapsed time each frame for overlay display
            float startTime = Time.realtimeSinceStartup;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                SimulateMouseUiOverlayState.UpdateLongPressElapsed(elapsed);
                MouseUiFrameWaitOutcome frameOutcome = await MouseUiEditorFrameWaiter.WaitForEditorFrameAndSwitchToMainThreadAsync(ct).ConfigureAwait(false);
                if (frameOutcome == MouseUiFrameWaitOutcome.TimedOut)
                {
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateFrameTimeoutResult(MouseAction.LongPress, inputPos, null, targetName);
                }

                if (frameOutcome == MouseUiFrameWaitOutcome.Paused)
                {
                    // Returning here still runs the finally below, which releases pointerUp early.
                    cleanupScheduler.QueueOverlayClear();
                    return MouseUiSimulationResponseFactory.CreateInterruptedResult(
                        MouseAction.LongPress, inputPos, targetName,
                        "Long-press pointerDown was already dispatched. Unity paused during Pause Point inspection while holding; pointerUp was released early and the press duration was cut short.");
                }

                await MainThreadSwitcher.SwitchToMainThread(ct);
                elapsed = Time.realtimeSinceStartup - startTime;
            }

            SimulateMouseUiOverlayState.UpdateLongPressElapsed(duration);
            return null;
        }

        private static (IMouseUiPressTarget? Target, SimulateMouseUiResponse? Failure) ResolvePressTarget(
            MouseUiSimulationCommand parameters,
            EventSystem eventSystem,
            Vector2 inputPos,
            MouseAction action)
        {
            Vector2 screenPos = MouseUiCoordinateConverter.InputToScreen(inputPos);
            PointerEventData pointerData = MouseUiPointerTargetResolver.CreatePointerPressData(eventSystem, screenPos, parameters.Button);
            MouseUiInputSystemSync.SyncMousePosition(screenPos);
            ResolvedPointerTargets resolvedTargets =
                MouseUiPointerTargetResolver.ResolvePressablePointerTargets(parameters, eventSystem, inputPos, screenPos, pointerData, action);
            if (resolvedTargets.FailureResponse != null)
            {
                return (null, resolvedTargets.FailureResponse);
            }

            return (new UguiPressTarget(resolvedTargets, pointerData), null);
        }
    }
}
