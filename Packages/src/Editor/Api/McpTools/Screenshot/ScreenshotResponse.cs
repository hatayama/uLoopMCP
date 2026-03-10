#nullable enable

using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    public class ScreenshotInfo
    {
        public string ImagePath { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        // "gameView": pixel coordinates match simulate-mouse input (divide by ResolutionScale if != 1.0)
        // "window": EditorWindow capture including toolbar
        public string CoordinateSystem { get; set; } = McpConstants.COORDINATE_SYSTEM_WINDOW;

        public float ResolutionScale { get; set; } = 1.0f;

        public ScreenshotInfo(string imagePath, long fileSizeBytes, int width, int height,
            string coordinateSystem = McpConstants.COORDINATE_SYSTEM_WINDOW, float resolutionScale = 1.0f)
        {
            ImagePath = imagePath;
            FileSizeBytes = fileSizeBytes;
            Width = width;
            Height = height;
            CoordinateSystem = coordinateSystem;
            ResolutionScale = resolutionScale;
        }

        public ScreenshotInfo()
        {
        }
    }

    public class ScreenshotResponse : BaseToolResponse
    {
        public List<ScreenshotInfo> Screenshots { get; set; } = new();

        public int ScreenshotCount => Screenshots.Count;

        public ScreenshotResponse(List<ScreenshotInfo> screenshots)
        {
            Screenshots = screenshots;
        }

        public ScreenshotResponse()
        {
        }
    }
}
