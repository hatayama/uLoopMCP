using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Bundled tool entry point for Unity Editor screenshots. The platform supplies capture work through host services.
    /// </summary>
    [UnityCliLoopTool]
    public class ScreenshotTool : UnityCliLoopTool<ScreenshotSchema, ScreenshotResponse>, IUnityCliLoopToolHostServicesReceiver
    {
        private IUnityCliLoopScreenshotService _screenshot;

        public override string ToolName => "screenshot";

        public void InitializeHostServices(IUnityCliLoopToolHostServices services)
        {
            if (services == null)
            {
                throw new System.ArgumentNullException(nameof(services));
            }

            _screenshot = services.Screenshot ?? throw new System.ArgumentNullException(nameof(services.Screenshot));
        }

        protected override async Task<ScreenshotResponse> ExecuteAsync(ScreenshotSchema parameters, CancellationToken ct)
        {
            if (_screenshot == null)
            {
                throw new System.InvalidOperationException("Host services were not initialized.");
            }

            UnityCliLoopScreenshotResult result = await _screenshot.CaptureAsync(ToRequest(parameters), ct);
            return ToResponse(result);
        }

        private static UnityCliLoopScreenshotRequest ToRequest(ScreenshotSchema parameters)
        {
            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            return new UnityCliLoopScreenshotRequest
            {
                WindowName = parameters.WindowName,
                ResolutionScale = parameters.ResolutionScale,
                MatchMode = parameters.MatchMode,
                OutputDirectory = parameters.OutputDirectory,
                CaptureMode = parameters.CaptureMode,
                AnnotateElements = parameters.AnnotateElements,
                ElementsOnly = parameters.ElementsOnly,
            };
        }

        private static ScreenshotResponse ToResponse(UnityCliLoopScreenshotResult result)
        {
            if (result == null)
            {
                throw new System.ArgumentNullException(nameof(result));
            }

            return new ScreenshotResponse
            {
                Screenshots = ToScreenshotInfos(result.Screenshots),
            };
        }

        private static List<ScreenshotInfo> ToScreenshotInfos(List<UnityCliLoopScreenshotInfo> sourceInfos)
        {
            if (sourceInfos == null)
            {
                return new List<ScreenshotInfo>();
            }

            List<ScreenshotInfo> screenshotInfos = new List<ScreenshotInfo>();
            foreach (UnityCliLoopScreenshotInfo sourceInfo in sourceInfos)
            {
                screenshotInfos.Add(ToScreenshotInfo(sourceInfo));
            }

            return screenshotInfos;
        }

        private static ScreenshotInfo ToScreenshotInfo(UnityCliLoopScreenshotInfo sourceInfo)
        {
            if (sourceInfo == null)
            {
                throw new System.ArgumentNullException(nameof(sourceInfo));
            }

            return new ScreenshotInfo
            {
                ImagePath = sourceInfo.ImagePath,
                FileSizeBytes = sourceInfo.FileSizeBytes,
                Width = sourceInfo.Width,
                Height = sourceInfo.Height,
                CoordinateSystem = sourceInfo.CoordinateSystem,
                ResolutionScale = sourceInfo.ResolutionScale,
                YOffset = sourceInfo.YOffset,
                AnnotatedElements = sourceInfo.AnnotatedElements,
            };
        }
    }
}
