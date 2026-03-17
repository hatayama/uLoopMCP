#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
#if ULOOPMCP_HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace io.github.hatayama.uLoopMCP
{
    [McpTool(Description = "Simulate keyboard key input in PlayMode via Input System. Supports one-shot press, key-down hold, and key-up release for game controls (WASD, Space, etc.). Requires the Input System package (com.unity.inputsystem).")]
    public class SimulateKeyboardTool : AbstractUnityTool<SimulateKeyboardSchema, SimulateKeyboardResponse>
    {
        public override string ToolName => "simulate-keyboard";

        protected override
#if !ULOOPMCP_HAS_INPUT_SYSTEM
#pragma warning disable CS1998
#endif
            async Task<SimulateKeyboardResponse> ExecuteAsync(
            SimulateKeyboardSchema parameters,
            CancellationToken ct)
#if !ULOOPMCP_HAS_INPUT_SYSTEM
#pragma warning restore CS1998
#endif
        {
            ct.ThrowIfCancellationRequested();

#if !ULOOPMCP_HAS_INPUT_SYSTEM
            return new SimulateKeyboardResponse
            {
                Success = false,
                Message = "simulate-keyboard requires the Input System package (com.unity.inputsystem). Install it via Package Manager and set Active Input Handling to 'Input System Package (New)' or 'Both' in Player Settings.",
                Action = parameters.Action.ToString()
            };
#else
            string correlationId = McpConstants.GenerateCorrelationId();

            if (!EditorApplication.isPlaying)
            {
                return new SimulateKeyboardResponse
                {
                    Success = false,
                    Message = "PlayMode is not active. Use control-play-mode tool to start PlayMode first.",
                    Action = parameters.Action.ToString()
                };
            }

            if (EditorApplication.isPaused)
            {
                return new SimulateKeyboardResponse
                {
                    Success = false,
                    Message = "PlayMode is paused. Resume PlayMode before simulating keyboard input.",
                    Action = parameters.Action.ToString()
                };
            }

            if (string.IsNullOrEmpty(parameters.Key))
            {
                return new SimulateKeyboardResponse
                {
                    Success = false,
                    Message = "Key parameter is required. Examples: \"W\", \"Space\", \"LeftShift\", \"A\", \"Enter\".",
                    Action = parameters.Action.ToString()
                };
            }

            string normalizedKey = NormalizeKeyName(parameters.Key);
            if (!Enum.TryParse<Key>(normalizedKey, ignoreCase: true, out Key key) || key == Key.None)
            {
                return new SimulateKeyboardResponse
                {
                    Success = false,
                    Message = $"Invalid key name: \"{parameters.Key}\". Use Input System Key enum names (e.g. \"W\", \"Space\", \"LeftShift\", \"A\", \"Enter\").",
                    Action = parameters.Action.ToString()
                };
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return new SimulateKeyboardResponse
                {
                    Success = false,
                    Message = "No keyboard device found in Input System. Ensure the Input System package is properly configured.",
                    Action = parameters.Action.ToString()
                };
            }

            VibeLogger.LogInfo(
                "simulate_keyboard_start",
                "Keyboard simulation started",
                new { Action = parameters.Action.ToString(), Key = parameters.Key },
                correlationId: correlationId
            );

            EnsureOverlayExists();

            SimulateKeyboardResponse response;

            switch (parameters.Action)
            {
                case KeyboardAction.Press:
                    response = await ExecutePress(keyboard, key, parameters.Duration, ct);
                    break;

                case KeyboardAction.KeyDown:
                    response = await ExecuteKeyDown(keyboard, key, ct);
                    break;

                case KeyboardAction.KeyUp:
                    response = await ExecuteKeyUp(keyboard, key, ct);
                    break;

                default:
                    throw new ArgumentException($"Unknown keyboard action: {parameters.Action}");
            }

            VibeLogger.LogInfo(
                "simulate_keyboard_complete",
                $"Keyboard simulation completed: {response.Message}",
                new { Action = parameters.Action.ToString(), Success = response.Success },
                correlationId: correlationId
            );

            return response;
#endif
        }

#if ULOOPMCP_HAS_INPUT_SYSTEM
        private static void EnsureOverlayExists()
        {
            if (SimulateKeyboardOverlay.Instance != null)
            {
                return;
            }

            GameObject overlayGo = new GameObject("SimulateKeyboardOverlay");
            overlayGo.hideFlags = HideFlags.HideAndDontSave;
            overlayGo.AddComponent<SimulateKeyboardOverlay>();
        }

        private async Task<SimulateKeyboardResponse> ExecutePress(
            Keyboard keyboard, Key key, float duration, CancellationToken ct)
        {
            if (duration < 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                return new SimulateKeyboardResponse
                {
                    Success = false,
                    Message = $"Duration must be non-negative, got: {duration}",
                    Action = KeyboardAction.Press.ToString(),
                    KeyName = key.ToString()
                };
            }

            string keyName = key.ToString();
            if (KeyboardKeyState.IsKeyHeld(key))
            {
                return new SimulateKeyboardResponse
                {
                    Success = false,
                    Message = $"Key '{keyName}' is already held down. Call KeyUp first.",
                    Action = KeyboardAction.Press.ToString(),
                    KeyName = keyName
                };
            }

            SimulateKeyboardOverlayState.ShowPress(keyName);
            KeyboardKeyState.RegisterTransientKey(key);

            try
            {
                await ApplyOnNextConfiguredUpdate(() => KeyboardKeyState.SetKeyState(keyboard, key, true), ct);

                if (duration > 0f)
                {
                    float startTime = Time.realtimeSinceStartup;
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        await EditorDelay.DelayFrame(1, ct);
                        elapsed = Time.realtimeSinceStartup - startTime;
                    }
                }
            }
            finally
            {
                if (EditorApplication.isPlaying && !EditorApplication.isPaused)
                {
                    await ApplyOnNextConfiguredUpdate(() => KeyboardKeyState.SetKeyState(keyboard, key, false), CancellationToken.None);
                    await EditorDelay.DelayFrame(1, CancellationToken.None);
                }

                KeyboardKeyState.UnregisterTransientKey(key);
                SimulateKeyboardOverlayState.ClearPress();
            }

            string durationText = duration > 0f ? $" for {duration:F1}s" : "";
            return new SimulateKeyboardResponse
            {
                Success = true,
                Message = $"Pressed '{keyName}'{durationText}",
                Action = KeyboardAction.Press.ToString(),
                KeyName = keyName
            };
        }

        private async Task<SimulateKeyboardResponse> ExecuteKeyDown(Keyboard keyboard, Key key, CancellationToken ct)
        {
            string keyName = key.ToString();

            if (KeyboardKeyState.IsKeyHeld(key))
            {
                return new SimulateKeyboardResponse
                {
                    Success = false,
                    Message = $"Key '{keyName}' is already held down. Call KeyUp first.",
                    Action = KeyboardAction.KeyDown.ToString(),
                    KeyName = keyName
                };
            }

            await ApplyOnNextConfiguredUpdate(() => KeyboardKeyState.SetKeyState(keyboard, key, true), ct);
            KeyboardKeyState.SetKeyDown(key);
            SimulateKeyboardOverlayState.AddHeldKey(keyName);
            await EditorDelay.DelayFrame(1, ct);

            return new SimulateKeyboardResponse
            {
                Success = true,
                Message = $"Key '{keyName}' held down",
                Action = KeyboardAction.KeyDown.ToString(),
                KeyName = keyName
            };
        }

        private async Task<SimulateKeyboardResponse> ExecuteKeyUp(Keyboard keyboard, Key key, CancellationToken ct)
        {
            string keyName = key.ToString();

            if (!KeyboardKeyState.IsKeyHeld(key))
            {
                return new SimulateKeyboardResponse
                {
                    Success = false,
                    Message = $"Key '{keyName}' is not currently held. Call KeyDown first.",
                    Action = KeyboardAction.KeyUp.ToString(),
                    KeyName = keyName
                };
            }

            await ApplyOnNextConfiguredUpdate(() => KeyboardKeyState.SetKeyState(keyboard, key, false), ct);
            KeyboardKeyState.SetKeyUp(key);
            SimulateKeyboardOverlayState.RemoveHeldKey(keyName);
            await EditorDelay.DelayFrame(1, ct);

            return new SimulateKeyboardResponse
            {
                Success = true,
                Message = $"Key '{keyName}' released",
                Action = KeyboardAction.KeyUp.ToString(),
                KeyName = keyName
            };
        }

        private static string NormalizeKeyName(string keyName)
        {
            if (string.Equals(keyName, "Return", StringComparison.OrdinalIgnoreCase))
            {
                return Key.Enter.ToString();
            }
            return keyName;
        }

        private static Task ApplyOnNextConfiguredUpdate(Action apply, CancellationToken ct)
        {
            InputUpdateType targetUpdateType = KeyboardInputUpdateTypeResolver.Resolve();
            if (KeyboardInputUpdateTypeResolver.RequiresExplicitUpdate())
            {
                return ApplyOnExplicitUpdate(apply, targetUpdateType, ct);
            }

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            CancellationTokenRegistration registration = default;
            Action? callback = null;

            callback = () =>
            {
                InputUpdateType currentUpdateType = InputState.currentUpdateType;
                if (!KeyboardInputUpdateTypeResolver.IsMatch(currentUpdateType, targetUpdateType))
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

        private static Task ApplyOnExplicitUpdate(Action apply, InputUpdateType targetUpdateType, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            CancellationTokenRegistration registration = default;
            Action? callback = null;

            callback = () =>
            {
                InputUpdateType currentUpdateType = InputState.currentUpdateType;
                if (!KeyboardInputUpdateTypeResolver.IsMatch(currentUpdateType, targetUpdateType))
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

        private static void RunExplicitUpdate(InputUpdateType targetUpdateType)
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
#endif
    }
}
