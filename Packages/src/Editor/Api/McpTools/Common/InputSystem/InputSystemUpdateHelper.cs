#nullable enable
#if ULOOPMCP_HAS_INPUT_SYSTEM
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace io.github.hatayama.uLoopMCP
{
    // Shared helper for applying Input System state changes at the correct update phase.
    // Both keyboard and mouse simulation need frame-precise timing so that
    // wasPressedThisFrame / wasReleasedThisFrame detect the injected state.
    internal static class InputSystemUpdateHelper
    {
        public static Task ApplyOnNextConfiguredUpdate(Action apply, CancellationToken ct)
        {
            InputUpdateType targetUpdateType = InputUpdateTypeResolver.Resolve();
            if (InputUpdateTypeResolver.RequiresExplicitUpdate())
            {
                return ApplyOnExplicitUpdate(apply, targetUpdateType, ct);
            }

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            CancellationTokenRegistration registration = default;
            Action? callback = null;

            callback = () =>
            {
                InputUpdateType currentUpdateType = InputState.currentUpdateType;
                if (!InputUpdateTypeResolver.IsMatch(currentUpdateType, targetUpdateType))
                {
                    return;
                }

                Debug.Assert(callback != null, "callback must be assigned before subscription");
                InputSystem.onBeforeUpdate -= callback;
                registration.Dispose();
                apply();
                tcs.TrySetResult(true);
            };

            InputSystem.onBeforeUpdate += callback;
            if (ct.CanBeCanceled)
            {
                registration = ct.Register(() =>
                {
                    Debug.Assert(callback != null, "callback must be assigned before cancellation");
                    InputSystem.onBeforeUpdate -= callback;
                    tcs.TrySetCanceled(ct);
                });
            }

            return tcs.Task;
        }

        public static int GetMinimumObservationFrameCount()
        {
            return InputUpdateTypeResolver.RequiresExplicitUpdate() ? 2 : 1;
        }

        public static async Task WaitForObservationFrames(CancellationToken ct)
        {
            await EditorDelay.DelayFrame(GetMinimumObservationFrameCount(), ct);
        }

        public static async Task WaitForPressLifetime(float duration, CancellationToken ct)
        {
            int minimumObservationFrames = GetMinimumObservationFrameCount();
            int observedFrames = 0;
            float startTime = Time.realtimeSinceStartup;
            float elapsed = 0f;

            while (observedFrames < minimumObservationFrames || elapsed < duration)
            {
                await EditorDelay.DelayFrame(1, ct);
                observedFrames++;
                elapsed = Time.realtimeSinceStartup - startTime;
            }
        }

        public static void RunExplicitUpdate(InputUpdateType targetUpdateType)
        {
            InputSettings? settings = InputSystem.settings;
            if (settings == null)
            {
                InputSystem.Update();
                return;
            }

            InputSettings.UpdateMode originalUpdateMode = settings.updateMode;
            InputSettings.UpdateMode targetUpdateMode = GetExplicitUpdateMode(targetUpdateType, originalUpdateMode);
            if (targetUpdateMode == originalUpdateMode)
            {
                InputSystem.Update();
                return;
            }

            settings.updateMode = targetUpdateMode;
            try
            {
                InputSystem.Update();
            }
            finally
            {
                settings.updateMode = originalUpdateMode;
            }
        }

        private static Task ApplyOnExplicitUpdate(Action apply, InputUpdateType targetUpdateType, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            CancellationTokenRegistration registration = default;
            Action? callback = null;

            callback = () =>
            {
                InputUpdateType currentUpdateType = InputState.currentUpdateType;
                if (!InputUpdateTypeResolver.IsMatch(currentUpdateType, targetUpdateType))
                {
                    return;
                }

                Debug.Assert(callback != null, "callback must be assigned before subscription");
                InputSystem.onBeforeUpdate -= callback;
                registration.Dispose();
                apply();
                tcs.TrySetResult(true);
            };

            InputSystem.onBeforeUpdate += callback;
            if (ct.CanBeCanceled)
            {
                registration = ct.Register(() =>
                {
                    Debug.Assert(callback != null, "callback must be assigned before cancellation");
                    InputSystem.onBeforeUpdate -= callback;
                    tcs.TrySetCanceled(ct);
                });
            }

            RunExplicitUpdate(targetUpdateType);
            if (!tcs.Task.IsCompleted)
            {
                Debug.Assert(callback != null, "callback must be assigned before explicit update fallback");
                InputSystem.onBeforeUpdate -= callback;
                registration.Dispose();
                apply();
                RunExplicitUpdate(targetUpdateType);
                tcs.TrySetResult(true);
            }

            return tcs.Task;
        }

        private static InputSettings.UpdateMode GetExplicitUpdateMode(
            InputUpdateType targetUpdateType,
            InputSettings.UpdateMode fallbackUpdateMode)
        {
            if (targetUpdateType == InputUpdateType.Dynamic)
            {
                return InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            }

            if (targetUpdateType == InputUpdateType.Fixed)
            {
                return InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
            }

            if (targetUpdateType == InputUpdateType.Manual)
            {
                return InputSettings.UpdateMode.ProcessEventsManually;
            }

            return fallbackUpdateMode;
        }
    }
}
#endif
