using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.InternalAPIBridge;
using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Application
{
    /// <summary>
    /// Server shutdown response
    /// </summary>
    public class ServerShutdownResponse : UnityCliLoopToolResponse
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Result message
        /// </summary>
        public string Message { get; set; }
    }
}