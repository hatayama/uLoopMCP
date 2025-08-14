using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Response schema for SetUIToolkitText tool
    /// Provides type-safe response structure
    /// </summary>
    public class SetUIToolkitTextResponse : BaseToolResponse
    {
        /// <summary>
        /// Whether the text was successfully set
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The text that was set
        /// </summary>
        public string SetText { get; set; }

        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Whether the window was opened (if it wasn't already)
        /// </summary>
        public bool WindowOpened { get; set; }

        /// <summary>
        /// Timestamp when the text was set
        /// </summary>
        public string Timestamp { get; set; }

        /// <summary>
        /// Create a new SetUIToolkitTextResponse
        /// </summary>
        /// <param name="success">Whether the operation was successful</param>
        /// <param name="setText">The text that was set</param>
        /// <param name="message">Result message</param>
        /// <param name="windowOpened">Whether the window was opened</param>
        /// <param name="timestamp">Optional timestamp</param>
        public SetUIToolkitTextResponse(bool success, string setText, string message, bool windowOpened, string timestamp = null)
        {
            Success = success;
            SetText = setText;
            Message = message;
            WindowOpened = windowOpened;
            Timestamp = timestamp;
        }

        /// <summary>
        /// Parameterless constructor for JSON deserialization
        /// </summary>
        public SetUIToolkitTextResponse()
        {
        }
    }
}