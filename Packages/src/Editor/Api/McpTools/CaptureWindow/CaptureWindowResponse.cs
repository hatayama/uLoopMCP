#nullable enable

using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    public class CapturedWindowInfo
    {
        public string ImagePath { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public CapturedWindowInfo(string imagePath, long fileSizeBytes, int width, int height)
        {
            ImagePath = imagePath;
            FileSizeBytes = fileSizeBytes;
            Width = width;
            Height = height;
        }

        public CapturedWindowInfo()
        {
        }
    }

    public class CaptureWindowResponse : BaseToolResponse
    {
        public List<CapturedWindowInfo> CapturedWindows { get; set; } = new();

        public int CapturedCount => CapturedWindows.Count;

        public CaptureWindowResponse(List<CapturedWindowInfo> capturedWindows)
        {
            CapturedWindows = capturedWindows;
        }

        public CaptureWindowResponse()
        {
        }
    }
}
