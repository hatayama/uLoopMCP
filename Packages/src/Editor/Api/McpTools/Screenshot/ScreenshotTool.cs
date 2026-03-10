using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    [McpTool(Description = "Take a screenshot of Unity EditorWindow and save as PNG")]
    public class ScreenshotTool : AbstractUnityTool<ScreenshotSchema, ScreenshotResponse>
    {
        public override string ToolName => "screenshot";

        protected override async Task<ScreenshotResponse> ExecuteAsync(
            ScreenshotSchema parameters,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string correlationId = McpConstants.GenerateCorrelationId();

            VibeLogger.LogInfo(
                "screenshot_start",
                "Unity window screenshot started",
                new { WindowName = parameters.WindowName, ResolutionScale = parameters.ResolutionScale, MatchMode = parameters.MatchMode.ToString(), OutputDirectory = parameters.OutputDirectory },
                correlationId: correlationId,
                humanNote: "User requested Unity window screenshot",
                aiTodo: "Monitor capture performance and file size"
            );

            ValidateParameters(parameters);

            if (parameters.CaptureMode == CaptureMode.rendering)
            {
                return await CaptureRenderingAsync(parameters, correlationId, ct);
            }

            return await CaptureWindowsAsync(parameters, correlationId, ct);
        }

        private async Task<ScreenshotResponse> CaptureRenderingAsync(
            ScreenshotSchema parameters, string correlationId, CancellationToken ct)
        {
            if (!EditorApplication.isPlaying)
            {
                VibeLogger.LogError(
                    "screenshot_rendering_requires_playmode",
                    "CaptureMode.rendering requires PlayMode",
                    correlationId: correlationId
                );
                return new ScreenshotResponse();
            }

            Texture2D texture = await EditorWindowCaptureUtility.CaptureGameRenderingAsync(
                parameters.ResolutionScale, ct);

            string outputDirectory = EnsureOutputDirectoryExists(parameters.OutputDirectory);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string savedPath = Path.Combine(outputDirectory, $"Rendering_{timestamp}.png");

            int width = texture.width;
            int height = texture.height;
            List<ScreenshotInfo> screenshots = new List<ScreenshotInfo>();

            try
            {
                SaveTextureAsPng(texture, savedPath);

                FileInfo savedFileInfo = new FileInfo(savedPath);
                screenshots.Add(new ScreenshotInfo(
                    savedPath, savedFileInfo.Length, width, height,
                    McpConstants.COORDINATE_SYSTEM_GAME_VIEW, parameters.ResolutionScale));
            }
            catch (Exception ex)
            {
                // File I/O is external resource access; catch to report save failure
                VibeLogger.LogWarning(
                    "screenshot_save_exception",
                    $"Exception saving rendering screenshot: {ex.Message}",
                    correlationId: correlationId
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            if (screenshots.Count > 0)
            {
                VibeLogger.LogInfo(
                    "screenshot_success",
                    $"Captured game rendering ({width}x{height})",
                    new { CaptureMode = "rendering", ScreenshotCount = screenshots.Count },
                    correlationId: correlationId
                );
            }

            return new ScreenshotResponse(screenshots);
        }

        private async Task<ScreenshotResponse> CaptureWindowsAsync(
            ScreenshotSchema parameters, string correlationId, CancellationToken ct)
        {
            EditorWindow[] windows = EditorWindowCaptureUtility.FindWindowsByName(parameters.WindowName, parameters.MatchMode);
            if (windows.Length == 0)
            {
                VibeLogger.LogError(
                    "screenshot_window_not_found",
                    $"Window '{parameters.WindowName}' not found (MatchMode: {parameters.MatchMode})",
                    correlationId: correlationId
                );
                return new ScreenshotResponse();
            }

            string outputDirectory = EnsureOutputDirectoryExists(parameters.OutputDirectory);
            string safeWindowName = SanitizeFileName(parameters.WindowName);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            List<ScreenshotInfo> screenshots = new List<ScreenshotInfo>();

            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                Texture2D texture = await EditorWindowCaptureUtility.CaptureWindowAsync(window, parameters.ResolutionScale, ct);
                if (texture == null)
                {
                    VibeLogger.LogWarning(
                        "screenshot_failed",
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
                    screenshots.Add(new ScreenshotInfo(savedPath, savedFileInfo.Length, width, height));
                }
                catch (Exception ex)
                {
                    // File I/O is external resource access; catch to continue processing remaining windows
                    VibeLogger.LogWarning(
                        "screenshot_save_exception",
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
                "screenshot_success",
                $"Captured {screenshots.Count} window(s)",
                new { WindowName = parameters.WindowName, ScreenshotCount = screenshots.Count },
                correlationId: correlationId
            );

            return new ScreenshotResponse(screenshots);
        }

        private void ValidateParameters(ScreenshotSchema parameters)
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

        private string EnsureOutputDirectoryExists(string outputDirectory)
        {
            string resolvedDirectory;

            if (string.IsNullOrEmpty(outputDirectory))
            {
                string projectRoot = UnityMcpPathResolver.GetProjectRoot();
                resolvedDirectory = Path.Combine(projectRoot, McpConstants.OUTPUT_ROOT_DIR, McpConstants.SCREENSHOTS_DIR);
            }
            else
            {
                resolvedDirectory = Path.GetFullPath(outputDirectory);
            }

            Directory.CreateDirectory(resolvedDirectory);

            return resolvedDirectory;
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
