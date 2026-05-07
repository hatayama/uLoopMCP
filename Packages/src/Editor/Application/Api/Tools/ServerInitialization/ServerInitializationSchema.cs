using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.InternalAPIBridge;
using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Application
{
    /// <summary>
    /// Schema for server initialization request
    /// </summary>
    public class ServerInitializationSchema : UnityCliLoopToolSchema
    {
        public bool PreserveStartupLockUntilExplicitRelease { get; set; }
    }
}
