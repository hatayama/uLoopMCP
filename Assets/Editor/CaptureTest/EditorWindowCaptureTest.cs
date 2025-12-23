using UnityEditor;
using UnityEngine;
using System.IO;

namespace io.github.hatayama.uLoopMCP
{
    public class EditorWindowCaptureTest : EditorWindow
    {
        private string _windowName = "Console";
        private string _lastResult = "";
        private Texture2D _previewTexture;

        [MenuItem("uLoopMCP/Windows/EditorWindow Capture Test")]
        public static void ShowWindow()
        {
            GetWindow<EditorWindowCaptureTest>("Capture Test");
        }

        private void OnGUI()
        {
            GUILayout.Label("EditorWindow Capture Test", EditorStyles.boldLabel);
            GUILayout.Space(10);

            _windowName = EditorGUILayout.TextField("Window Name:", _windowName);

            GUILayout.Space(10);

            if (GUILayout.Button("Find Window"))
            {
                EditorWindow[] windows = EditorWindowCaptureUtility.FindWindowsByName(_windowName);
                if (windows.Length > 0)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    sb.AppendLine($"Found {windows.Length} window(s):");
                    for (int i = 0; i < windows.Length; i++)
                    {
                        sb.AppendLine($"  [{i + 1}] {windows[i].titleContent.text} ({windows[i].GetType().FullName})");
                    }
                    _lastResult = sb.ToString();
                }
                else
                {
                    _lastResult = $"Window '{_windowName}' not found";
                }
            }

            if (GUILayout.Button("Capture Window"))
            {
                CaptureWindow();
            }

            if (GUILayout.Button("List All Open Windows"))
            {
                string[] names = EditorWindowCaptureUtility.GetOpenWindowNames();
                _lastResult = "Open windows:\n" + string.Join("\n", names);
            }

            GUILayout.Space(10);
            GUILayout.Label("Result:", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_lastResult, GUILayout.Height(100));

            if (_previewTexture != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Preview:", EditorStyles.boldLabel);

                float maxWidth = position.width - 20;
                float maxHeight = 300;
                float aspectRatio = (float)_previewTexture.width / _previewTexture.height;

                float displayWidth = Mathf.Min(maxWidth, _previewTexture.width);
                float displayHeight = displayWidth / aspectRatio;

                if (displayHeight > maxHeight)
                {
                    displayHeight = maxHeight;
                    displayWidth = displayHeight * aspectRatio;
                }

                Rect previewRect = GUILayoutUtility.GetRect(displayWidth, displayHeight);
                EditorGUI.DrawPreviewTexture(previewRect, _previewTexture, null, ScaleMode.ScaleToFit);
            }
        }

        private void CaptureWindow()
        {
            EditorWindow[] windows = EditorWindowCaptureUtility.FindWindowsByName(_windowName);
            if (windows.Length == 0)
            {
                _lastResult = $"Window '{_windowName}' not found";
                return;
            }

            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
            }

            string outputDir = Path.Combine(Application.dataPath.Replace("/Assets", ""), "uLoopMCPOutputs", "UnityWindowCaptures");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"Captured {windows.Length} window(s):");

            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                Texture2D texture = EditorWindowCaptureUtility.CaptureWindow(window);

                if (texture == null)
                {
                    sb.AppendLine($"  [{i + 1}] Failed to capture");
                    continue;
                }

                string fileName = windows.Length == 1
                    ? $"{_windowName}_{timestamp}.png"
                    : $"{_windowName}_{i + 1}_{timestamp}.png";
                string filePath = Path.Combine(outputDir, fileName);

                byte[] pngData = texture.EncodeToPNG();
                File.WriteAllBytes(filePath, pngData);

                sb.AppendLine($"  [{i + 1}] {texture.width}x{texture.height} -> {filePath}");

                if (i == 0)
                {
                    _previewTexture = texture;
                }
                else
                {
                    DestroyImmediate(texture);
                }
            }

            _lastResult = sb.ToString();
            Repaint();
        }

        private void OnDestroy()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
            }
        }
    }
}

