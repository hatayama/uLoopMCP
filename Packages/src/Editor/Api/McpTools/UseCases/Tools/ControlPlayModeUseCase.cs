using System.Threading.Tasks;
using System.Threading;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    public class ControlPlayModeUseCase : AbstractUseCase<ControlPlayModeSchema, ControlPlayModeResponse>
    {
        public override async Task<ControlPlayModeResponse> ExecuteAsync(ControlPlayModeSchema parameters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            string message;
            bool wasPaused = EditorApplication.isPaused;

            switch (parameters.Action)
            {
                case PlayModeAction.Play:
                    if (wasPaused)
                    {
                        EditorApplication.isPaused = false;
                        await EditorDelay.DelayFrame(1, cancellationToken);
                    }
                    EditorApplication.isPlaying = true;
                    message = wasPaused ? "Play mode resumed" : "Play mode started";
                    break;

                case PlayModeAction.Stop:
                    if (wasPaused)
                    {
                        EditorApplication.isPaused = false;
                        await EditorDelay.DelayFrame(1, cancellationToken);
                    }
                    EditorApplication.isPlaying = false;
                    message = "Play mode stopped";
                    break;

                case PlayModeAction.Pause:
                    EditorApplication.isPaused = true;
                    message = "Play mode paused";
                    break;

                default:
                    message = $"Unknown action: {parameters.Action}";
                    break;
            }

            ControlPlayModeResponse response = new ControlPlayModeResponse
            {
                IsPlaying = EditorApplication.isPlaying,
                IsPaused = EditorApplication.isPaused,
                Message = message
            };

            return response;
        }
    }
}

