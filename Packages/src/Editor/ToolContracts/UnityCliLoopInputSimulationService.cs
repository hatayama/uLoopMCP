#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    public interface IUnityCliLoopKeyboardSimulationService
    {
        Task<UnityCliLoopKeyboardSimulationResult> SimulateKeyboardAsync(
            UnityCliLoopKeyboardSimulationRequest request,
            CancellationToken ct);
    }

    public interface IUnityCliLoopMouseInputSimulationService
    {
        Task<UnityCliLoopMouseInputSimulationResult> SimulateMouseInputAsync(
            UnityCliLoopMouseInputSimulationRequest request,
            CancellationToken ct);
    }

    public enum UnityCliLoopKeyboardAction
    {
        Press = 0,
        KeyDown = 1,
        KeyUp = 2
    }

    public enum UnityCliLoopMouseInputAction
    {
        Click = 0,
        LongPress = 1,
        MoveDelta = 2,
        Scroll = 3,
        SmoothDelta = 4
    }

    public enum UnityCliLoopMouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    public sealed class UnityCliLoopKeyboardSimulationRequest
    {
        public UnityCliLoopKeyboardAction Action { get; set; } = UnityCliLoopKeyboardAction.Press;
        public string Key { get; set; } = "";
        public float Duration { get; set; }
    }

    public sealed class UnityCliLoopKeyboardSimulationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Action { get; set; } = "";
        public string? KeyName { get; set; }
    }

    public sealed class UnityCliLoopMouseInputSimulationRequest
    {
        public UnityCliLoopMouseInputAction Action { get; set; } = UnityCliLoopMouseInputAction.Click;
        public float X { get; set; }
        public float Y { get; set; }
        public UnityCliLoopMouseButton Button { get; set; } = UnityCliLoopMouseButton.Left;
        public float Duration { get; set; }
        public float DeltaX { get; set; }
        public float DeltaY { get; set; }
        public float ScrollX { get; set; }
        public float ScrollY { get; set; }
    }

    public sealed class UnityCliLoopMouseInputSimulationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Action { get; set; } = "";
        public string? Button { get; set; }
        public float? PositionX { get; set; }
        public float? PositionY { get; set; }
    }
}
