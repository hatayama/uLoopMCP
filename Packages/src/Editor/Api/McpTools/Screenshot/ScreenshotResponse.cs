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

        public ScreenshotInfo(string imagePath, long fileSizeBytes, int width, int height)
        {
            ImagePath = imagePath;
            FileSizeBytes = fileSizeBytes;
            Width = width;
            Height = height;
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
