#nullable enable

using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public class ReplayInputResponse : UnityCliLoopToolResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Action { get; set; } = "";
        public string? InputPath { get; set; }
        public int? CurrentFrame { get; set; }
        public int? TotalFrames { get; set; }
        public float? Progress { get; set; }
        public bool? IsReplaying { get; set; }
    }
}
