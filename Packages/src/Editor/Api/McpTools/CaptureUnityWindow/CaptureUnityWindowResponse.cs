#nullable enable

namespace io.github.hatayama.uLoopMCP
{
    public class CaptureUnityWindowResponse : BaseToolResponse
    {
        public string? ImagePath { get; set; }

        public long? FileSizeBytes { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public CaptureUnityWindowResponse(string imagePath, long fileSizeBytes, int width, int height)
        {
            ImagePath = imagePath;
            FileSizeBytes = fileSizeBytes;
            Width = width;
            Height = height;
        }

        public CaptureUnityWindowResponse(bool failure)
        {
            ImagePath = null;
            FileSizeBytes = null;
            Width = null;
            Height = null;
        }

        public CaptureUnityWindowResponse()
        {
        }
    }
}
