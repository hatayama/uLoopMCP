using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    [McpTool(Description = "Capture Unity EditorWindow and save as PNG image")]
    public class CaptureUnityWindowTool : AbstractUnityTool<CaptureUnityWindowSchema, CaptureUnityWindowResponse>
    {
        public override string ToolName => "capture-unity-window";

        private const string OUTPUT_DIRECTORY_NAME = "UnityWindowCaptures";

        protected override Task<CaptureUnityWindowResponse> ExecuteAsync(
            CaptureUnityWindowSchema parameters,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string correlationId = McpConstants.GenerateCorrelationId();

            VibeLogger.LogInfo(
                "capture_unity_window_start",
                "Unity window capture started",
                new { WindowName = parameters.WindowName, ResolutionScale = parameters.ResolutionScale },
                correlationId: correlationId,
                humanNote: "User requested Unity window screenshot",
                aiTodo: "Monitor capture performance and file size"
            );

            ValidateParameters(parameters);

            EditorWindow[] windows = EditorWindowCaptureUtility.FindWindowsByName(parameters.WindowName);
            if (windows.Length == 0)
            {
                VibeLogger.LogError(
                    "capture_window_not_found",
                    $"Window '{parameters.WindowName}' not found",
                    correlationId: correlationId
                );
                return Task.FromResult(new CaptureUnityWindowResponse());
            }

            string outputDirectory = EnsureOutputDirectoryExists();
            string safeWindowName = SanitizeFileName(parameters.WindowName);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            List<CapturedWindowInfo> capturedWindows = new List<CapturedWindowInfo>();

            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                Texture2D texture = EditorWindowCaptureUtility.CaptureWindow(window, parameters.ResolutionScale);
                if (texture == null)
                {
                    VibeLogger.LogWarning(
                        "capture_window_failed",
                        $"Failed to capture window index {i}",
                        correlationId: correlationId
                    );
                    continue;
                }

                string fileName = windows.Length == 1
                    ? $"{safeWindowName}_{timestamp}.png"
                    : $"{safeWindowName}_{i + 1}_{timestamp}.png";
                string savedPath = Path.Combine(outputDirectory, fileName);

                int width = texture.width;
                int height = texture.height;

                try
                {
                    SaveTextureAsPng(texture, savedPath);

                    FileInfo savedFileInfo = new FileInfo(savedPath);
                    capturedWindows.Add(new CapturedWindowInfo(savedPath, savedFileInfo.Length, width, height));
                }
                catch (Exception ex)
                {
                    // File I/O is external resource access; catch to continue processing remaining windows
                    VibeLogger.LogWarning(
                        "capture_save_exception",
                        $"Exception saving window index {i}: {ex.Message}",
                        correlationId: correlationId
                    );
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            VibeLogger.LogInfo(
                "capture_window_success",
                $"Captured {capturedWindows.Count} window(s)",
                new { WindowName = parameters.WindowName, CapturedCount = capturedWindows.Count },
                correlationId: correlationId
            );

            return Task.FromResult(new CaptureUnityWindowResponse(capturedWindows));
        }

        private void ValidateParameters(CaptureUnityWindowSchema parameters)
        {
            if (string.IsNullOrEmpty(parameters.WindowName))
            {
                throw new ArgumentException("WindowName cannot be null or empty");
            }

            if (parameters.ResolutionScale < 0.1f || parameters.ResolutionScale > 1.0f)
            {
                throw new ArgumentException(
                    $"ResolutionScale must be between 0.1 and 1.0, got: {parameters.ResolutionScale}");
            }
        }

        private string EnsureOutputDirectoryExists()
        {
            string projectRoot = Application.dataPath.Replace("/Assets", "");
            string outputDirectory = Path.Combine(projectRoot, "uLoopMCPOutputs", OUTPUT_DIRECTORY_NAME);

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            return outputDirectory;
        }

        private string SanitizeFileName(string name)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = name;
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            return sanitized;
        }

        private void SaveTextureAsPng(Texture2D texture, string fullPath)
        {
            byte[] pngData = texture.EncodeToPNG();
            if (pngData == null)
            {
                throw new InvalidOperationException($"Failed to encode texture to PNG. Format: {texture.format}, Size: {texture.width}x{texture.height}");
            }
            File.WriteAllBytes(fullPath, pngData);
        }
    }
}
