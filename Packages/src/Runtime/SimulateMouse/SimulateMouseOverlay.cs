#nullable enable
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    // OnGUI overlay that visualizes SimulateMouse cursor position and drag state on Game View.
    public class SimulateMouseOverlay : MonoBehaviour
    {
        public static SimulateMouseOverlay? Instance { get; private set; }

        private const float OVERLAY_TIMEOUT_SECONDS = 2.0f;
        private const float CROSSHAIR_SIZE = 15f;
        private const float CROSSHAIR_THICKNESS = 2f;
        private const float START_MARKER_SIZE = 8f;
        private const float LABEL_PADDING = 8f;

        private static readonly Color CLICK_COLOR = new Color(0f, 1f, 0.4f, 0.9f);
        private static readonly Color DRAG_COLOR = new Color(1f, 0.6f, 0f, 0.9f);
        private static readonly Color LABEL_BG_COLOR = new Color(0f, 0f, 0f, 0.7f);

        private GUIStyle? _labelStyle;
        private Texture2D? _bgTexture;
        private Material? _lineMaterial;

        private void Awake()
        {
            Debug.Assert(Instance == null, "SimulateMouseOverlay instance already exists");
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_bgTexture != null)
            {
                DestroyImmediate(_bgTexture);
            }

            if (_lineMaterial != null)
            {
                DestroyImmediate(_lineMaterial);
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (!SimulateMouseOverlayState.IsActive)
            {
                return;
            }

            if (SimulateMouseOverlayState.IsExpired(OVERLAY_TIMEOUT_SECONDS))
            {
                SimulateMouseOverlayState.Clear();
                return;
            }

            EnsureStyles();

            Color activeColor = GetActiveColor();

            DrawDragLine(activeColor);
            DrawDragStartMarker(activeColor);
            DrawCrosshair(SimulateMouseOverlayState.CurrentPosition, activeColor);
            DrawStateLabel();
        }

        private Color GetActiveColor()
        {
            return SimulateMouseOverlayState.Action == MouseAction.Click
                ? CLICK_COLOR
                : DRAG_COLOR;
        }

        private void DrawCrosshair(Vector2 guiPos, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;

            GUI.DrawTexture(
                new Rect(guiPos.x - CROSSHAIR_SIZE, guiPos.y - CROSSHAIR_THICKNESS / 2f,
                    CROSSHAIR_SIZE * 2f, CROSSHAIR_THICKNESS),
                Texture2D.whiteTexture);

            GUI.DrawTexture(
                new Rect(guiPos.x - CROSSHAIR_THICKNESS / 2f, guiPos.y - CROSSHAIR_SIZE,
                    CROSSHAIR_THICKNESS, CROSSHAIR_SIZE * 2f),
                Texture2D.whiteTexture);

            GUI.color = previousColor;
        }

        private void DrawDragStartMarker(Color color)
        {
            if (SimulateMouseOverlayState.DragStartPosition == null)
            {
                return;
            }

            Vector2 startPos = SimulateMouseOverlayState.DragStartPosition.Value;
            Color previousColor = GUI.color;
            GUI.color = color;

            GUI.DrawTexture(
                new Rect(startPos.x - START_MARKER_SIZE / 2f, startPos.y - START_MARKER_SIZE / 2f,
                    START_MARKER_SIZE, START_MARKER_SIZE),
                Texture2D.whiteTexture);

            GUI.color = previousColor;
        }

        private void DrawDragLine(Color color)
        {
            if (SimulateMouseOverlayState.DragStartPosition == null)
            {
                return;
            }

            EnsureLineMaterial();

            Vector2 startPos = SimulateMouseOverlayState.DragStartPosition.Value;
            Vector2 endPos = SimulateMouseOverlayState.CurrentPosition;

            GL.PushMatrix();
            _lineMaterial!.SetPass(0);
            // GL.LoadPixelMatrix() defaults to bottom-left origin, but OnGUI positions use top-left origin
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex3(startPos.x, startPos.y, 0f);
            GL.Vertex3(endPos.x, endPos.y, 0f);
            GL.End();
            GL.PopMatrix();
        }

        private void DrawStateLabel()
        {
            string label = BuildLabel();
            Vector2 labelSize = _labelStyle!.CalcSize(new GUIContent(label));

            Rect labelRect = new Rect(
                LABEL_PADDING,
                LABEL_PADDING,
                labelSize.x + LABEL_PADDING * 2f,
                labelSize.y + LABEL_PADDING);

            GUI.Label(labelRect, label, _labelStyle);
        }

        private string BuildLabel()
        {
            Vector2 pos = SimulateMouseOverlayState.CurrentPosition;
            string posText = $"({pos.x:F0}, {pos.y:F0})";

            string? targetName = SimulateMouseOverlayState.HitGameObjectName;
            string targetText = targetName != null ? $" -> \"{targetName}\"" : "";

            return $"[SimulateMouse] {SimulateMouseOverlayState.Action} {posText}{targetText}";
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null)
            {
                return;
            }

            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, LABEL_BG_COLOR);
            _bgTexture.Apply();

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(
                    (int)LABEL_PADDING, (int)LABEL_PADDING,
                    (int)(LABEL_PADDING / 2f), (int)(LABEL_PADDING / 2f))
            };
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.normal.background = _bgTexture;
        }

        private void EnsureLineMaterial()
        {
            if (_lineMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            Debug.Assert(shader != null, "Hidden/Internal-Colored shader must exist in Unity");
            _lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

    }
}
