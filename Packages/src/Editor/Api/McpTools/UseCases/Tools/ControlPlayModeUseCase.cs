using System.Threading.Tasks;
using System.Threading;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    public class ControlPlayModeUseCase : AbstractUseCase<ControlPlayModeSchema, ControlPlayModeResponse>
    {
        public override Task<ControlPlayModeResponse> ExecuteAsync(ControlPlayModeSchema parameters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            string message;

            switch (parameters.Action)
            {
                case PlayModeAction.Play:
                    EditorApplication.isPaused = false;
                    EditorApplication.isPlaying = true;
                    message = EditorApplication.isPlaying ? "Play mode started" : "Play mode resumed";
                    break;

                case PlayModeAction.Stop:
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

            return Task.FromResult(response);
        }
    }
}

