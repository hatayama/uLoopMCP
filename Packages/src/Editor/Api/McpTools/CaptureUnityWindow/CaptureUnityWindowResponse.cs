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

    public class CaptureUnityWindowResponse : BaseToolResponse
    {
        public List<CapturedWindowInfo> CapturedWindows { get; set; } = new();

        public int CapturedCount => CapturedWindows.Count;

        public CaptureUnityWindowResponse(List<CapturedWindowInfo> capturedWindows)
        {
            CapturedWindows = capturedWindows;
        }

        public CaptureUnityWindowResponse(bool failure)
        {
            CapturedWindows = new List<CapturedWindowInfo>();
        }

        public CaptureUnityWindowResponse()
        {
        }
    }
}
