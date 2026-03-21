#if ULOOPMCP_HAS_INPUT_SYSTEM
#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace io.github.hatayama.uLoopMCP
{
    // Deterministic controller for verifying record/replay accuracy.
    // Uses fixed per-frame movement (no deltaTime) to ensure identical
    // results between recording and replay at the same frame rate.
    public class InputReplayVerificationController : MonoBehaviour
    {
        private const float MOVE_SPEED = 0.1f;
        private const float ROTATE_SENSITIVITY = 0.5f;
        private const float SCALE_STEP = 0.1f;
        private const int TARGET_FRAME_RATE = 60;

        [SerializeField] private Text? _frameText;
        [SerializeField] private Text? _positionText;
        [SerializeField] private Text? _rotationText;
        [SerializeField] private Text? _scaleText;
        [SerializeField] private Text? _inputText;
        [SerializeField] private MeshRenderer? _cubeRenderer;

        private int _startFrame;
        private readonly List<string> _eventLog = new();
        private Vector3 _lastLoggedPosition;
        private bool _colorToggleRed;
        private bool _colorToggleBlue;

        private void Start()
        {
            Application.targetFrameRate = TARGET_FRAME_RATE;
            _startFrame = Time.frameCount;
            _lastLoggedPosition = transform.position;
        }

        private void Update()
        {
            Keyboard? keyboard = Keyboard.current;
            Mouse? mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                return;
            }

            int relativeFrame = Time.frameCount - _startFrame;

            ProcessMovement(keyboard, relativeFrame);
            ProcessRotation(mouse, relativeFrame);
            ProcessClicks(mouse, relativeFrame);
            ProcessScroll(mouse, relativeFrame);
            UpdateUI(keyboard, mouse, relativeFrame);
        }

        private void ProcessMovement(Keyboard keyboard, int frame)
        {
            Vector3 movement = Vector3.zero;

            if (keyboard[Key.W].isPressed) movement.z += MOVE_SPEED;
            if (keyboard[Key.S].isPressed) movement.z -= MOVE_SPEED;
            if (keyboard[Key.A].isPressed) movement.x -= MOVE_SPEED;
            if (keyboard[Key.D].isPressed) movement.x += MOVE_SPEED;

            if (movement == Vector3.zero)
            {
                return;
            }

            transform.Translate(movement, Space.World);

            // Rounding avoids float noise that would make logs differ between runs
            Vector3 rounded = RoundVector3(transform.position, 4);
            if (rounded != _lastLoggedPosition)
            {
                _eventLog.Add($"Frame {frame}: Position {FormatVector3(rounded)}");
                _lastLoggedPosition = rounded;
            }
        }

        private void ProcessRotation(Mouse mouse, int frame)
        {
            Vector2 delta = mouse.delta.ReadValue();
            if (delta == Vector2.zero)
            {
                return;
            }

            float rotationY = delta.x * ROTATE_SENSITIVITY;
            Vector3 euler = transform.eulerAngles;
            euler.y += rotationY;
            transform.eulerAngles = euler;

            _eventLog.Add($"Frame {frame}: Rotation Y={euler.y.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        private void ProcessClicks(Mouse mouse, int frame)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _colorToggleRed = !_colorToggleRed;
                UpdateCubeColor();
                _eventLog.Add($"Frame {frame}: LeftClick color={GetColorName()}");
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _colorToggleBlue = !_colorToggleBlue;
                UpdateCubeColor();
                _eventLog.Add($"Frame {frame}: RightClick color={GetColorName()}");
            }
        }

        private void ProcessScroll(Mouse mouse, int frame)
        {
            float scrollY = mouse.scroll.y.ReadValue();
            if (scrollY == 0f)
            {
                return;
            }

            float direction = scrollY > 0f ? SCALE_STEP : -SCALE_STEP;
            Vector3 scale = transform.localScale;
            float newScale = Mathf.Max(0.1f, scale.x + direction);
            transform.localScale = Vector3.one * newScale;

            _eventLog.Add($"Frame {frame}: Scroll scale={newScale.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        private void UpdateCubeColor()
        {
            if (_cubeRenderer == null)
            {
                return;
            }

            Color color = Color.white;
            if (_colorToggleRed) color = Color.red;
            if (_colorToggleBlue) color = Color.blue;
            if (_colorToggleRed && _colorToggleBlue) color = Color.magenta;

            _cubeRenderer.material.color = color;
        }

        private string GetColorName()
        {
            if (_colorToggleRed && _colorToggleBlue) return "Magenta";
            if (_colorToggleRed) return "Red";
            if (_colorToggleBlue) return "Blue";
            return "White";
        }

        private void UpdateUI(Keyboard keyboard, Mouse mouse, int frame)
        {
            if (_frameText != null)
            {
                _frameText.text = $"Frame: {frame}";
            }

            if (_positionText != null)
            {
                _positionText.text = $"Pos: {FormatVector3(transform.position)}";
            }

            if (_rotationText != null)
            {
                _rotationText.text = $"Rot Y: {transform.eulerAngles.y:F2}";
            }

            if (_scaleText != null)
            {
                _scaleText.text = $"Scale: {transform.localScale.x:F2}";
            }

            if (_inputText != null)
            {
                _inputText.text = BuildInputStateText(keyboard, mouse);
            }
        }

        private static string BuildInputStateText(Keyboard keyboard, Mouse mouse)
        {
            List<string> held = new List<string>();
            if (keyboard[Key.W].isPressed) held.Add("W");
            if (keyboard[Key.A].isPressed) held.Add("A");
            if (keyboard[Key.S].isPressed) held.Add("S");
            if (keyboard[Key.D].isPressed) held.Add("D");
            if (mouse.leftButton.isPressed) held.Add("LMB");
            if (mouse.rightButton.isPressed) held.Add("RMB");

            return held.Count > 0 ? $"Input: [{string.Join(", ", held)}]" : "Input: [none]";
        }

        public void SaveLog(string path)
        {
            string directory = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(path, _eventLog);
            Debug.Log($"[InputReplayVerification] Event log saved to {path} ({_eventLog.Count} entries)");
        }

        public void ClearLog()
        {
            _eventLog.Clear();
            _lastLoggedPosition = transform.position;
            _startFrame = Time.frameCount;
            _colorToggleRed = false;
            _colorToggleBlue = false;
            UpdateCubeColor();
        }

        private static Vector3 RoundVector3(Vector3 v, int decimals)
        {
            float multiplier = Mathf.Pow(10f, decimals);
            return new Vector3(
                Mathf.Round(v.x * multiplier) / multiplier,
                Mathf.Round(v.y * multiplier) / multiplier,
                Mathf.Round(v.z * multiplier) / multiplier
            );
        }

        private static string FormatVector3(Vector3 v)
        {
            return $"({v.x.ToString("F4", CultureInfo.InvariantCulture)}, {v.y.ToString("F4", CultureInfo.InvariantCulture)}, {v.z.ToString("F4", CultureInfo.InvariantCulture)})";
        }
    }
}
#endif
