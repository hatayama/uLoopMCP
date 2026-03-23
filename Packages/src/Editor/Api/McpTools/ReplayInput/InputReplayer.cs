#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace io.github.hatayama.uLoopMCP
{
    [InitializeOnLoad]
    internal static class InputReplayer
    {
        private static readonly Dictionary<string, Key> _keyLookup = BuildKeyLookup();
        private static readonly Key[] _allKeys = BuildAllKeys();
        private static readonly Dictionary<string, MouseButton> _buttonLookup = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Left", MouseButton.Left },
            { "Right", MouseButton.Right },
            { "Middle", MouseButton.Middle }
        };
        private static readonly Key[] _emptyKeys = Array.Empty<Key>();
        private static readonly MouseButton[] _emptyButtons = Array.Empty<MouseButton>();

        private static bool _isReplaying;
        private static InputRecordingData? _data;
        private static int _eventIndex;
        private static int _currentFrame;
        private static bool _loop;
        private static bool _showOverlay;
        private static readonly HashSet<Key> _replayHeldKeys = new();
        private static readonly HashSet<MouseButton> _replayHeldButtons = new();
        private static Vector2? _replayMousePosition;

        public static event Action? ReplayStarted;
        public static event Action? ReplayCompleted;

        public static bool IsReplaying => _isReplaying;
        public static int CurrentFrame => _currentFrame;
        public static int TotalFrames => _data?.Metadata.TotalFrames ?? 0;

        public static float Progress
        {
            get
            {
                int total = TotalFrames;
                return total > 0 ? (float)_currentFrame / total : 0f;
            }
        }

        static InputReplayer()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void StartReplay(InputRecordingData data, bool loop, bool showOverlay)
        {
            Debug.Assert(!_isReplaying, "Cannot start replay while already replaying");
            Debug.Assert(EditorApplication.isPlaying, "PlayMode must be active to start replay");
            Debug.Assert(data != null, "Recording data must not be null");

            _data = data;
            _eventIndex = 0;
            _currentFrame = 0;
            _loop = loop;
            _showOverlay = showOverlay;
            _replayHeldKeys.Clear();
            _replayHeldButtons.Clear();
            _replayMousePosition = null;
            _isReplaying = true;

            InputSystem.onAfterUpdate -= OnAfterUpdate;
            InputSystem.onAfterUpdate += OnAfterUpdate;

            ReplayStarted?.Invoke();
        }

        public static void StopReplay()
        {
            if (!_isReplaying)
            {
                return;
            }

            InputSystem.onAfterUpdate -= OnAfterUpdate;
            _isReplaying = false;

            ReleaseAllHeldInputs();

            _data = null;
            _eventIndex = 0;
            _currentFrame = 0;
            _replayHeldKeys.Clear();
            _replayHeldButtons.Clear();
            _replayMousePosition = null;

            ReplayInputOverlayState.Clear();
        }

        private static void OnAfterUpdate()
        {
            if (!_isReplaying || _data == null)
            {
                return;
            }

            InputUpdateType currentUpdateType = InputState.currentUpdateType;
            InputUpdateType targetUpdateType = InputUpdateTypeResolver.Resolve();
            if (!InputUpdateTypeResolver.IsMatch(currentUpdateType, targetUpdateType))
            {
                return;
            }

            Vector2 frameDelta = Vector2.zero;
            Vector2 frameScroll = Vector2.zero;

            CollectFrameState(ref frameDelta, ref frameScroll);
            ApplyCurrentFrameSnapshot(Keyboard.current, Mouse.current, frameDelta, frameScroll);

            if (_showOverlay)
            {
                ReplayInputOverlayState.Update(_currentFrame, _data.Metadata.TotalFrames, _loop);
            }

            _currentFrame++;

            if (_eventIndex >= _data.Frames.Count && _currentFrame > _data.Metadata.TotalFrames)
            {
                if (_loop)
                {
                    ReleaseAllHeldInputs();
                    _eventIndex = 0;
                    _currentFrame = 0;
                    _replayMousePosition = null;
                }
                else
                {
                    StopReplay();
                    ReplayCompleted?.Invoke();
                }
            }
        }

        private static void CollectFrameState(ref Vector2 frameDelta, ref Vector2 frameScroll)
        {
            Debug.Assert(_data != null, "_data must not be null while replaying");

            while (_eventIndex < _data!.Frames.Count && _data.Frames[_eventIndex].Frame <= _currentFrame)
            {
                InputFrameEvents frameEvents = _data.Frames[_eventIndex];
                for (int i = 0; i < frameEvents.Events.Count; i++)
                {
                    ProcessEvent(frameEvents.Events[i], ref frameDelta, ref frameScroll);
                }

                _eventIndex++;
            }
        }

        private static void ApplyCurrentFrameSnapshot(
            Keyboard? keyboard,
            Mouse? mouse,
            Vector2 frameDelta,
            Vector2 frameScroll)
        {
            if (keyboard != null)
            {
                ApplyKeyboardSnapshot(keyboard, _replayHeldKeys);
            }

            if (mouse != null)
            {
                ApplyMouseSnapshot(mouse, _replayHeldButtons, frameDelta, frameScroll, _replayMousePosition);
            }
        }

        private static void ProcessEvent(
            RecordedInputEvent evt,
            ref Vector2 frameDelta,
            ref Vector2 frameScroll)
        {
            switch (evt.Type)
            {
                case InputEventTypes.KEY_DOWN:
                    ProcessKeyDown(evt.Data);
                    break;
                case InputEventTypes.KEY_UP:
                    ProcessKeyUp(evt.Data);
                    break;
                case InputEventTypes.MOUSE_CLICK:
                    ProcessMouseClick(evt.Data);
                    break;
                case InputEventTypes.MOUSE_RELEASE:
                    ProcessMouseRelease(evt.Data);
                    break;
                case InputEventTypes.MOUSE_DELTA:
                    ProcessMouseDelta(evt.Data, ref frameDelta);
                    break;
                case InputEventTypes.MOUSE_SCROLL:
                    ProcessMouseScroll(evt.Data, ref frameScroll);
                    break;
                case InputEventTypes.MOUSE_POSITION:
                    ProcessMousePosition(evt.Data);
                    break;
            }
        }

        private static void ProcessKeyDown(string keyName)
        {
            if (!_keyLookup.TryGetValue(keyName, out Key key))
            {
                return;
            }

            _replayHeldKeys.Add(key);
            SimulateKeyboardOverlayState.AddHeldKey(keyName);
        }

        private static void ProcessKeyUp(string keyName)
        {
            if (!_keyLookup.TryGetValue(keyName, out Key key))
            {
                return;
            }

            _replayHeldKeys.Remove(key);
            SimulateKeyboardOverlayState.RemoveHeldKey(keyName);
        }

        private static void ProcessMouseClick(string buttonName)
        {
            if (!_buttonLookup.TryGetValue(buttonName, out MouseButton button))
            {
                return;
            }

            _replayHeldButtons.Add(button);
            SimulateMouseInputOverlayState.SetButtonHeld(button, true);
        }

        private static void ProcessMouseRelease(string buttonName)
        {
            if (!_buttonLookup.TryGetValue(buttonName, out MouseButton button))
            {
                return;
            }

            _replayHeldButtons.Remove(button);
            SimulateMouseInputOverlayState.SetButtonHeld(button, false);
        }

        private static void ProcessMouseDelta(string data, ref Vector2 frameDelta)
        {
            frameDelta = InputRecorder.ParseVector2(data);
            SimulateMouseInputOverlayState.SetMoveDelta(frameDelta);
        }

        private static void ProcessMousePosition(string data)
        {
            _replayMousePosition = InputRecorder.ParseVector2(data);
        }

        private static void ProcessMouseScroll(string data, ref Vector2 frameScroll)
        {
            if (!float.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out float scrollY))
            {
                return;
            }

            frameScroll = new Vector2(0f, scrollY);
            int direction = scrollY > 0f ? 1 : scrollY < 0f ? -1 : 0;
            SimulateMouseInputOverlayState.SetScrollDirection(direction);
        }

        internal static void ApplyKeyboardSnapshot(Keyboard keyboard, IReadOnlyCollection<Key> heldKeys)
        {
            InputUpdateType updateType = InputUpdateTypeResolver.Resolve();
            using (StateEvent.From(keyboard, out InputEventPtr eventPtr))
            {
                // StateEvent carries the previous frame's state; without zeroing first,
                // released keys would remain pressed until explicitly cleared.
                for (int i = 0; i < _allKeys.Length; i++)
                {
                    KeyControl? control = keyboard[_allKeys[i]];
                    if (control != null)
                    {
                        control.WriteValueIntoEvent(0f, eventPtr);
                    }
                }

                foreach (Key key in heldKeys)
                {
                    KeyControl? control = keyboard[key];
                    if (control != null)
                    {
                        control.WriteValueIntoEvent(1f, eventPtr);
                    }
                }

                InputState.Change(keyboard, eventPtr, updateType);
            }
        }

        internal static void ApplyMouseSnapshot(
            Mouse mouse,
            IReadOnlyCollection<MouseButton> heldButtons,
            Vector2 delta,
            Vector2 scroll,
            Vector2? position)
        {
            InputUpdateType updateType = InputUpdateTypeResolver.Resolve();
            using (StateEvent.From(mouse, out InputEventPtr eventPtr))
            {
                mouse.leftButton.WriteValueIntoEvent(0f, eventPtr);
                mouse.rightButton.WriteValueIntoEvent(0f, eventPtr);
                mouse.middleButton.WriteValueIntoEvent(0f, eventPtr);
                mouse.delta.WriteValueIntoEvent(delta, eventPtr);
                mouse.scroll.WriteValueIntoEvent(scroll, eventPtr);

                if (position.HasValue)
                {
                    mouse.position.WriteValueIntoEvent(position.Value, eventPtr);
                }

                foreach (MouseButton button in heldButtons)
                {
                    MouseInputState.GetButtonControl(mouse, button).WriteValueIntoEvent(1f, eventPtr);
                }

                InputState.Change(mouse, eventPtr, updateType);
            }
        }

        private static void ReleaseAllHeldInputs()
        {
            Keyboard? keyboard = Keyboard.current;
            if (keyboard != null)
            {
                ApplyKeyboardSnapshot(keyboard, _emptyKeys);
            }

            Mouse? mouse = Mouse.current;
            if (mouse != null)
            {
                ApplyMouseSnapshot(mouse, _emptyButtons, Vector2.zero, Vector2.zero, null);
            }

            foreach (Key key in _replayHeldKeys)
            {
                SimulateKeyboardOverlayState.RemoveHeldKey(key.ToString());
            }

            foreach (MouseButton button in _replayHeldButtons)
            {
                SimulateMouseInputOverlayState.SetButtonHeld(button, false);
            }

            SimulateMouseInputOverlayState.SetMoveDelta(Vector2.zero);
            SimulateMouseInputOverlayState.SetScrollDirection(0);
            _replayHeldKeys.Clear();
            _replayHeldButtons.Clear();
        }

        private static Key[] BuildAllKeys()
        {
            List<Key> keys = new List<Key>();
            foreach (Key key in Enum.GetValues(typeof(Key)))
            {
                if (key == Key.None)
                {
                    continue;
                }

                keys.Add(key);
            }

            return keys.ToArray();
        }

        private static Dictionary<string, Key> BuildKeyLookup()
        {
            Dictionary<string, Key> lookup = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase);
            foreach (Key key in Enum.GetValues(typeof(Key)))
            {
                if (key == Key.None)
                {
                    continue;
                }

                string name = key.ToString();
                if (!lookup.ContainsKey(name))
                {
                    lookup[name] = key;
                }
            }

            return lookup;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopReplay();
            }
        }
    }
}
