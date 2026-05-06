using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    public interface IUnityCliLoopScreenshotService
    {
        Task<UnityCliLoopScreenshotResult> CaptureAsync(UnityCliLoopScreenshotRequest request, CancellationToken ct);
    }

    public enum WindowMatchMode
    {
        exact = 0,
        prefix = 1,
        contains = 2
    }

    public enum CaptureMode
    {
        window = 0,
        rendering = 1
    }

    public static class UnityCliLoopScreenshotCoordinateSystem
    {
        public const string Window = "window";
        public const string GameView = "gameView";
    }

    public sealed class UnityCliLoopScreenshotRequest
    {
        public string WindowName { get; set; } = "Game";
        public float ResolutionScale { get; set; } = 1.0f;
        public WindowMatchMode MatchMode { get; set; } = WindowMatchMode.exact;
        public string OutputDirectory { get; set; } = "";
        public CaptureMode CaptureMode { get; set; } = CaptureMode.window;
        public bool AnnotateElements { get; set; }
        public bool ElementsOnly { get; set; }
    }

    public sealed class UnityCliLoopScreenshotInfo
    {
        public string ImagePath { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string CoordinateSystem { get; set; } = UnityCliLoopScreenshotCoordinateSystem.Window;
        public float ResolutionScale { get; set; } = 1.0f;
        public int YOffset { get; set; }
        public List<UIElementInfo> AnnotatedElements { get; set; } = new List<UIElementInfo>();
    }

    public sealed class UnityCliLoopScreenshotResult
    {
        public List<UnityCliLoopScreenshotInfo> Screenshots { get; set; } = new List<UnityCliLoopScreenshotInfo>();
    }
}
