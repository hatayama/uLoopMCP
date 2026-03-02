using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public sealed class GetScreenshotSchema : BaseToolSchema
    {
        public string Format { get; set; } = "png";
        public int Quality { get; set; } = 75;
        public int MaxLongSide { get; set; } = 1568;
    }

    public sealed class GetScreenshotResponse : BaseToolResponse
    {
        public string ImageBase64 { get; set; }
        public string Format { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public sealed class GetScreenshotTool : AbstractDeviceTool<GetScreenshotSchema, GetScreenshotResponse>
    {
        public override string ToolName => "get-screenshot";

        protected override async Task<GetScreenshotResponse> ExecuteAsync(GetScreenshotSchema parameters, CancellationToken ct)
        {
            // Wait for end of frame to capture the screen
            await Task.Yield();

            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            // Calculate resize dimensions
            float scale = 1f;
            int longSide = Mathf.Max(screenWidth, screenHeight);
            if (longSide > parameters.MaxLongSide)
            {
                scale = (float)parameters.MaxLongSide / longSide;
            }

            int captureWidth = Mathf.RoundToInt(screenWidth * scale);
            int captureHeight = Mathf.RoundToInt(screenHeight * scale);

            // Capture screen
            Texture2D screenshot = new(screenWidth, screenHeight, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, screenWidth, screenHeight), 0, 0);
            screenshot.Apply();

            // Resize if needed
            if (scale < 1f)
            {
                RenderTexture rt = RenderTexture.GetTemporary(captureWidth, captureHeight);
                Graphics.Blit(screenshot, rt);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;

                Texture2D resized = new(captureWidth, captureHeight, TextureFormat.RGB24, false);
                resized.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                resized.Apply();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                UnityEngine.Object.Destroy(screenshot);
                screenshot = resized;
            }

            // Encode
            string format = parameters.Format?.ToLowerInvariant() == "jpg" ? "jpg" : "png";
            byte[] imageBytes = format == "jpg"
                ? screenshot.EncodeToJPG(parameters.Quality)
                : screenshot.EncodeToPNG();

            UnityEngine.Object.Destroy(screenshot);

            string base64 = Convert.ToBase64String(imageBytes);

            // Check payload size
            if (base64.Length > DeviceAgentConstants.MAX_REQUEST_BYTES)
            {
                // Return error-like response instead of throwing
                return new GetScreenshotResponse
                {
                    ImageBase64 = null,
                    Format = format,
                    Width = captureWidth,
                    Height = captureHeight
                };
            }

            return new GetScreenshotResponse
            {
                ImageBase64 = base64,
                Format = format,
                Width = captureWidth,
                Height = captureHeight
            };
        }
    }
}
