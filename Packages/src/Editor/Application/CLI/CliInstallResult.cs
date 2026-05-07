using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.InternalAPIBridge;
using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Application
{
    public readonly struct CliInstallResult
    {
        public readonly bool Success;
        public readonly string ErrorOutput;

        public CliInstallResult(bool success, string errorOutput)
        {
            Success = success;
            ErrorOutput = errorOutput;
        }
    }
}
