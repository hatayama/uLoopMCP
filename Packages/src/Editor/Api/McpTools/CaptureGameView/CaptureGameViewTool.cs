using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// MCP Tool for capturing Unity Game View and saving it as a file
    /// Related Classes: CaptureGameViewSchema, CaptureGameViewResponse
    /// </summary>
    [McpTool(Description = "Capture Unity Game View and save as PNG image")]
    public class CaptureGameViewTool : AbstractUnityTool<CaptureGameViewSchema, CaptureGameViewResponse>
    {
        public override string ToolName => "capture-gameview";

        private const string OUTPUT_DIRECTORY_NAME = "GameViewCaptures";

        protected override async Task<CaptureGameViewResponse> ExecuteAsync(
            CaptureGameViewSchema parameters, 
            CancellationToken cancellationToken)
        {
            string correlationId = McpConstants.GenerateCorrelationId();
            
            // VibeLogger start log
            VibeLogger.LogInfo(
                "capture_gameview_start",
                "Game view capture started",
                new { ResolutionScale = parameters.ResolutionScale },
                correlationId: correlationId,
                humanNote: "User requested Game View screenshot",
                aiTodo: "Monitor capture performance and file size"
            );

            try
            {
                // Validate parameters
                ValidateParameters(parameters);
                
                // Create output directory
                string outputDirectory = EnsureOutputDirectoryExists();
                
                // Direct call to Unity ScreenCapture API
                Texture2D texture = await CaptureGameViewAsync(parameters.ResolutionScale, cancellationToken);
                
                try
                {
                    // Save file
                    string savedPath = SaveTextureAsPng(texture, outputDirectory);
                    
                    // Generate response
                    var fileInfo = new FileInfo(savedPath);
                    var response = new CaptureGameViewResponse(savedPath, fileInfo.Length);
                    
                    // VibeLogger success log
                    VibeLogger.LogInfo(
                        "capture_gameview_success",
                        "Game view captured successfully",
                        new { 
                            ImagePath = response.ImagePath,
                            FileSizeBytes = response.FileSizeBytes,
                            ResolutionScale = parameters.ResolutionScale
                        },
                        correlationId: correlationId,
                        humanNote: "Screenshot saved successfully",
                        aiTodo: "Analyze capture quality if needed"
                    );
                    
                    return response;
                }
                finally
                {
                    // Release texture memory
                    if (texture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                    texture = null;
                }
            }
            catch (Exception ex)
            {
                // VibeLogger error log
                VibeLogger.LogError(
                    "capture_gameview_error",
                    "Game view capture failed",
                    new { Error = ex.Message, ResolutionScale = parameters.ResolutionScale },
                    correlationId: correlationId,
                    humanNote: "Screenshot capture encountered an error",
                    aiTodo: "Investigate capture failure cause"
                );
                
                // Return failure response without throwing an exception
                return new CaptureGameViewResponse(failure: true);
            }
            finally
            {
                // VibeLogger completion log
                VibeLogger.LogInfo(
                    "capture_gameview_complete", 
                    "GameView capture operation completed",
                    correlationId: correlationId
                );
            }
        }

        /// <summary>
        /// Validate parameters
        /// </summary>
        private void ValidateParameters(CaptureGameViewSchema parameters)
        {
            if (parameters.ResolutionScale < 0.1f || parameters.ResolutionScale > 1.0f)
            {
                throw new ArgumentException(
                    $"ResolutionScale must be between 0.1 and 1.0, got: {parameters.ResolutionScale}");
            }
        }

        /// <summary>
        /// Verify that the output directory exists, and create it if it does not
        /// </summary>
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

        /// <summary>
        /// Capture Game View and return as a texture
        /// </summary>
        private async Task<Texture2D> CaptureGameViewAsync(float resolutionScale, CancellationToken cancellationToken)
        {
            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();
            
            string tempFileName = $"temp_screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), tempFileName);
            
            try
            {
                // Support for both playing and stopped Unity states (universal capture processing)
                ScreenCapture.CaptureScreenshot(tempFileName, 1);
                
                if (!Application.isPlaying)
                {
                    // When stopped: Force repaint GameView for one frame
                    Assembly asm = typeof(EditorWindow).Assembly;
                    EditorWindow gameView = EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
                    gameView.Repaint();
                }
                else
                {
                    // When playing: Queue update to complete saving in the next frame
                    EditorApplication.QueuePlayerLoopUpdate();
                }
                
                // Wait for file to be created (maximum 5 seconds)
                int maxAttempts = 50;
                int attempts = 0;
                while (!File.Exists(fullPath) && attempts < maxAttempts)
                {
                    await TimerDelay.Wait(100, cancellationToken);
                    attempts++;
                }
                
                if (!File.Exists(fullPath))
                {
                    throw new InvalidOperationException($"Failed to capture Game View - screenshot file was not created at: {fullPath}");
                }
                
                // Check file size
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length == 0)
                {
                    throw new InvalidOperationException("Failed to capture Game View - screenshot file is empty");
                }
                
                // Load texture from PNG file
                byte[] fileData = File.ReadAllBytes(fullPath);
                Texture2D texture = new(2, 2);
                
                if (!texture.LoadImage(fileData))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    throw new InvalidOperationException("Failed to load captured screenshot as texture");
                }
                
                // Apply resolution scaling
                if (!Mathf.Approximately(resolutionScale, 1.0f))
                {
                    texture = ApplyResolutionScaling(texture, resolutionScale);
                }
                
                return texture;
            }
            finally
            {
                // Delete temporary file
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
        }

        /// <summary>
        /// Apply resolution scaling
        /// </summary>
        private Texture2D ApplyResolutionScaling(Texture2D originalTexture, float scale)
        {
            if (Mathf.Approximately(scale, 1.0f))
                return originalTexture;
            
            int newWidth = Mathf.RoundToInt(originalTexture.width * scale);
            int newHeight = Mathf.RoundToInt(originalTexture.height * scale);
            
            Texture2D scaledTexture = new(newWidth, newHeight, originalTexture.format, false);
            
            // Scale using RenderTexture
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            try
            {
                Graphics.Blit(originalTexture, rt);
                
                RenderTexture.active = rt;
                scaledTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
                scaledTexture.Apply();
                RenderTexture.active = null;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }
            
            // Destroy original texture
            UnityEngine.Object.DestroyImmediate(originalTexture);
            
            return scaledTexture;
        }

        /// <summary>
        /// Save texture as PNG file
        /// </summary>
        private string SaveTextureAsPng(Texture2D texture, string outputDirectory)
        {
            // Generate filename
            string fileName = GenerateFileName();
            string fullPath = Path.Combine(outputDirectory, fileName);
            
            // PNG encoding
            byte[] pngData = texture.EncodeToPNG();
            
            // Write file
            File.WriteAllBytes(fullPath, pngData);
            
            return fullPath;
        }

        /// <summary>
        /// Generate a unique filename
        /// </summary>
        private string GenerateFileName()
        {
            DateTime now = DateTime.Now;
            string baseFileName = $"gameview_{now:yyyyMMdd_HHmmss_fff}.png";
            
            return baseFileName;
        }
    }
}