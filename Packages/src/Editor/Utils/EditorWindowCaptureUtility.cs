using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Utility class for capturing any EditorWindow to a Texture2D.
    /// Uses InternalEditorUtilityBridge to access Unity internal GrabPixels method.
    /// </summary>
    public static class EditorWindowCaptureUtility
    {
        // Shortcut mapping for common window names to their internal Unity types.
        // Windows not in this list can still be found by title matching in FindWindowByName.
        private static readonly Dictionary<string, string> WindowTypeMapping = new()
        {
            { "game", "UnityEditor.GameView" },
            { "scene", "UnityEditor.SceneView" },
            { "console", "UnityEditor.ConsoleWindow" },
            { "inspector", "UnityEditor.InspectorWindow" },
            { "project", "UnityEditor.ProjectBrowser" },
            { "hierarchy", "UnityEditor.SceneHierarchyWindow" },
            { "animation", "UnityEditor.AnimationWindow" },
            { "animator", "UnityEditor.Graphs.AnimatorControllerTool" },
            { "profiler", "UnityEditor.ProfilerWindow" },
            { "audio mixer", "UnityEditor.Audio.AudioMixerWindow" },
        };

        /// <summary>
        /// Find all EditorWindows matching the given name.
        /// </summary>
        /// <param name="windowName">Window name (e.g., "Console", "Inspector")</param>
        /// <returns>Array of matching EditorWindows (empty if none found)</returns>
        public static EditorWindow[] FindWindowsByName(string windowName)
        {
            if (string.IsNullOrEmpty(windowName))
            {
                return Array.Empty<EditorWindow>();
            }

            string lowerName = windowName.ToLowerInvariant();

            if (WindowTypeMapping.TryGetValue(lowerName, out string typeName))
            {
                Type windowType = typeof(EditorWindow).Assembly.GetType(typeName);
                if (windowType != null)
                {
                    EditorWindow[] windows = Resources.FindObjectsOfTypeAll(windowType) as EditorWindow[];
                    if (windows != null && windows.Length > 0)
                    {
                        return windows;
                    }
                }
            }

            List<EditorWindow> matchingWindows = new List<EditorWindow>();
            EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (EditorWindow window in allWindows)
            {
                if (window.titleContent == null)
                {
                    continue;
                }

                string title = window.titleContent.text;
                if (title.Equals(windowName, StringComparison.OrdinalIgnoreCase) ||
                    title.Contains(windowName, StringComparison.OrdinalIgnoreCase))
                {
                    matchingWindows.Add(window);
                }
            }

            return matchingWindows.ToArray();
        }

        /// <summary>
        /// Capture an EditorWindow to a Texture2D.
        /// </summary>
        /// <param name="window">The EditorWindow to capture</param>
        /// <param name="resolutionScale">Resolution scale (0.1 to 1.0)</param>
        /// <returns>Captured Texture2D, or null if capture failed</returns>
        public static Texture2D CaptureWindow(EditorWindow window, float resolutionScale = 1.0f)
        {
            if (window == null)
            {
                return null;
            }

            window.ShowTab();
            window.Focus();
            window.Repaint();

            float scale = EditorGUIUtility.pixelsPerPoint;
            int width = Mathf.RoundToInt(window.position.width * scale);
            int height = Mathf.RoundToInt(window.position.height * scale);

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

            InternalEditorUtilityBridge.CaptureEditorWindow(window, rt);

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);

            if (!Mathf.Approximately(resolutionScale, 1.0f))
            {
                texture = ApplyResolutionScaling(texture, resolutionScale);
            }

            return texture;
        }

        /// <summary>
        /// Get a list of all open EditorWindow names.
        /// </summary>
        /// <returns>Array of window names</returns>
        public static string[] GetOpenWindowNames()
        {
            EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            List<string> names = new List<string>();

            foreach (EditorWindow window in allWindows)
            {
                if (window.titleContent != null && !string.IsNullOrEmpty(window.titleContent.text))
                {
                    names.Add(window.titleContent.text);
                }
            }

            return names.ToArray();
        }

        private static Texture2D ApplyResolutionScaling(Texture2D originalTexture, float scale)
        {
            int newWidth = Mathf.RoundToInt(originalTexture.width * scale);
            int newHeight = Mathf.RoundToInt(originalTexture.height * scale);

            Texture2D scaledTexture = new Texture2D(newWidth, newHeight, originalTexture.format, false);

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            Graphics.Blit(originalTexture, rt);

            RenderTexture.active = rt;
            scaledTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            scaledTexture.Apply();
            RenderTexture.active = null;

            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(originalTexture);

            return scaledTexture;
        }
    }
}

