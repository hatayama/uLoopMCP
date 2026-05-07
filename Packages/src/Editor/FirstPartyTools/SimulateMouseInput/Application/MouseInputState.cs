#if ULOOP_HAS_INPUT_SYSTEM
#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace io.github.hatayama.UnityCliLoop
{
    internal sealed class MouseInputStateService
    {
        private readonly HashSet<MouseButton> _heldButtons = new HashSet<MouseButton>();

        // Pending reset callbacks for per-frame values (delta, scroll).
        // Tracked so we can unsubscribe on PlayMode exit to prevent leaks.
        private Action? _pendingDeltaReset;
        private Action? _pendingScrollReset;

        public bool IsButtonHeld(MouseButton button) => _heldButtons.Contains(button);

        public void RegisterPlayModeCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public void SetButtonDown(MouseButton button)
        {
            _heldButtons.Add(button);
        }

        public void SetButtonUp(MouseButton button)
        {
            _heldButtons.Remove(button);
        }

        // StateEvent.From captures the full mouse state snapshot.
        // All currently held buttons must be written into every event
        // to avoid accidentally releasing them.
        public void SetButtonState(Mouse mouse, MouseButton button, bool pressed)
        {
            Debug.Assert(mouse != null, "mouse must not be null");
            ApplyStateEvent(mouse!, eventPtr =>
            {
                GetButtonControl(mouse!, button).WriteValueIntoEvent(pressed ? 1f : 0f, eventPtr);
            });
        }

        public void SetPositionState(Mouse mouse, Vector2 position)
        {
            Debug.Assert(mouse != null, "mouse must not be null");
            ApplyStateEvent(mouse!, eventPtr =>
            {
                mouse!.position.WriteValueIntoEvent(position, eventPtr);
            });
        }

        public void SetDeltaState(Mouse mouse, Vector2 delta)
        {
            InjectDelta(mouse, delta);
            SchedulePerFrameReset(mouse!, mouse!.delta, isDelta: true);
        }

        // Inject delta without scheduling a reset. Used by SmoothDelta which
        // manages its own lifecycle — resetting only after the final frame.
        public void InjectDelta(Mouse mouse, Vector2 delta)
        {
            Debug.Assert(mouse != null, "mouse must not be null");
            ApplyStateEvent(mouse!, eventPtr =>
            {
                mouse!.delta.WriteValueIntoEvent(delta, eventPtr);
            });
        }

        public void SetScrollState(Mouse mouse, Vector2 scroll)
        {
            Debug.Assert(mouse != null, "mouse must not be null");
            ApplyStateEvent(mouse!, eventPtr =>
            {
                mouse!.scroll.WriteValueIntoEvent(scroll, eventPtr);
            });

            SchedulePerFrameReset(mouse!, mouse!.scroll, isDelta: false);
        }

        public void ReleaseAllButtons()
        {
            Mouse? mouse = Mouse.current;
            if (mouse == null)
            {
                _heldButtons.Clear();
                ClearPendingResets();
                return;
            }

            using (StateEvent.From(mouse, out InputEventPtr eventPtr))
            {
                foreach (MouseButton button in _heldButtons)
                {
                    GetButtonControl(mouse, button).WriteValueIntoEvent(0f, eventPtr);
                }

                InputUpdateType updateType = InputUpdateTypeResolver.Resolve();
                InputState.Change(mouse, eventPtr, updateType);
            }

            _heldButtons.Clear();
            ClearPendingResets();
        }

        private void ApplyStateEvent(Mouse mouse, Action<InputEventPtr> writePayload)
        {
            InputUpdateType updateType = InputUpdateTypeResolver.Resolve();
            using (StateEvent.From(mouse, out InputEventPtr eventPtr))
            {
                foreach (MouseButton heldButton in _heldButtons)
                {
                    GetButtonControl(mouse, heldButton).WriteValueIntoEvent(1f, eventPtr);
                }

                writePayload(eventPtr);
                InputState.Change(mouse, eventPtr, updateType);
            }
        }

        // delta and scroll are per-frame values; leaving them non-zero causes
        // accumulation across frames. Reset on the next input update.
        private void SchedulePerFrameReset(
            Mouse mouse,
            InputControl<Vector2> control,
            bool isDelta)
        {
            // Remove previous pending reset to avoid stacking callbacks
            Action? previousReset = isDelta ? _pendingDeltaReset : _pendingScrollReset;
            if (previousReset != null)
            {
                InputSystem.onBeforeUpdate -= previousReset;
            }

            // Capture the target update type so the reset only fires in the same
            // Input System update phase that gameplay code reads, preventing an
            // Editor or BeforeRender update from clearing the value prematurely.
            InputUpdateType targetUpdateType = InputUpdateTypeResolver.Resolve();

            Action? resetCallback = null;
            resetCallback = () =>
            {
                InputUpdateType currentUpdateType = InputState.currentUpdateType;
                if (!InputUpdateTypeResolver.IsMatch(currentUpdateType, targetUpdateType))
                {
                    return;
                }

                Debug.Assert(resetCallback != null, "resetCallback must be assigned before subscription");
                InputSystem.onBeforeUpdate -= resetCallback;

                if (isDelta && _pendingDeltaReset == resetCallback)
                {
                    _pendingDeltaReset = null;
                }
                else if (!isDelta && _pendingScrollReset == resetCallback)
                {
                    _pendingScrollReset = null;
                }

                if (Mouse.current != mouse)
                {
                    return;
                }

                ApplyStateEvent(mouse, eventPtr =>
                {
                    control.WriteValueIntoEvent(Vector2.zero, eventPtr);
                });
            };

            if (isDelta)
            {
                _pendingDeltaReset = resetCallback;
            }
            else
            {
                _pendingScrollReset = resetCallback;
            }

            InputSystem.onBeforeUpdate += resetCallback;
        }

        private void ClearPendingResets()
        {
            if (_pendingDeltaReset != null)
            {
                InputSystem.onBeforeUpdate -= _pendingDeltaReset;
                _pendingDeltaReset = null;
            }

            if (_pendingScrollReset != null)
            {
                InputSystem.onBeforeUpdate -= _pendingScrollReset;
                _pendingScrollReset = null;
            }
        }

        internal ButtonControl GetButtonControl(Mouse mouse, MouseButton button)
        {
            return MouseButtonControlResolver.GetButtonControl(mouse, button);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                ReleaseAllButtons();
                SimulateMouseInputOverlayState.Clear();
            }
        }
    }

    internal static class MouseInputState
    {
        private static readonly MouseInputStateService ServiceValue = new MouseInputStateService();

        internal static void InitializeForEditorStartup()
        {
            ServiceValue.RegisterPlayModeCallbacks();
        }

        public static bool IsButtonHeld(MouseButton button) => ServiceValue.IsButtonHeld(button);

        public static void SetButtonDown(MouseButton button)
        {
            ServiceValue.SetButtonDown(button);
        }

        public static void SetButtonUp(MouseButton button)
        {
            ServiceValue.SetButtonUp(button);
        }

        public static void SetButtonState(Mouse mouse, MouseButton button, bool pressed)
        {
            ServiceValue.SetButtonState(mouse, button, pressed);
        }

        public static void SetPositionState(Mouse mouse, Vector2 position)
        {
            ServiceValue.SetPositionState(mouse, position);
        }

        public static void SetDeltaState(Mouse mouse, Vector2 delta)
        {
            ServiceValue.SetDeltaState(mouse, delta);
        }

        public static void InjectDelta(Mouse mouse, Vector2 delta)
        {
            ServiceValue.InjectDelta(mouse, delta);
        }

        public static void SetScrollState(Mouse mouse, Vector2 scroll)
        {
            ServiceValue.SetScrollState(mouse, scroll);
        }

        public static void ReleaseAllButtons()
        {
            ServiceValue.ReleaseAllButtons();
        }

        internal static ButtonControl GetButtonControl(Mouse mouse, MouseButton button)
        {
            return ServiceValue.GetButtonControl(mouse, button);
        }
    }
}
#endif
