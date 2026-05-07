#nullable enable
using System.Threading;
using System.Threading.Tasks;

using io.github.hatayama.UnityCliLoop.Runtime;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public interface IUnityCliLoopRecordInputService
    {
        Task<UnityCliLoopRecordInputResult> RecordInputAsync(UnityCliLoopRecordInputRequest request, CancellationToken ct);
    }

    public interface IUnityCliLoopReplayInputService
    {
        Task<UnityCliLoopReplayInputResult> ReplayInputAsync(UnityCliLoopReplayInputRequest request, CancellationToken ct);
    }

    public sealed class UnityCliLoopRecordInputRequest
    {
        public RecordInputAction Action { get; set; } = RecordInputAction.Start;
        public string OutputPath { get; set; } = "";
        public string Keys { get; set; } = "";
        public int DelaySeconds { get; set; } = 3;
        public bool ShowOverlay { get; set; } = true;
    }

    public sealed class UnityCliLoopRecordInputResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Action { get; set; } = "";
        public string? OutputPath { get; set; }
        public int? TotalFrames { get; set; }
        public float? DurationSeconds { get; set; }
    }

    public sealed class UnityCliLoopReplayInputRequest
    {
        public ReplayInputAction Action { get; set; } = ReplayInputAction.Start;
        public string InputPath { get; set; } = "";
        public bool ShowOverlay { get; set; } = true;
        public bool Loop { get; set; }
    }

    public sealed class UnityCliLoopReplayInputResult
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
