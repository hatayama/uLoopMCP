#nullable enable

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Response for the GameView capture tool
    /// Related classes: CaptureGameViewTool, CaptureGameViewSchema
    /// </summary>
    public class CaptureGameViewResponse : BaseToolResponse
    {
        /// <summary>
        /// Absolute path of the saved image file (null if capture failed)
        /// </summary>
        public string? ImagePath { get; set; }

        /// <summary>
        /// Size of the saved file in bytes (null if capture failed)
        /// </summary>
        public long? FileSizeBytes { get; set; }

        /// <summary>
        /// Constructor for generating a successful response
        /// </summary>
        public CaptureGameViewResponse(string imagePath, long fileSizeBytes)
        {
            ImagePath = imagePath;
            FileSizeBytes = fileSizeBytes;
        }

        /// <summary>
        /// Constructor for generating a failure response
        /// </summary>
        public CaptureGameViewResponse(bool failure)
        {
            ImagePath = null;
            FileSizeBytes = null;
        }

        /// <summary>
        /// Default constructor (for JSON deserialization)
        /// </summary>
        public CaptureGameViewResponse()
        {
        }
    }
}