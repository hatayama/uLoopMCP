using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// PlayStopUnity tool handler - Controls Unity play mode (play/stop)
    /// 
    /// Design Reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// 
    /// This Tool class directly handles Unity play mode control using EditorApplication API.
    /// Simplified implementation without UseCase for basic operations.
    /// 
    /// Related classes:
    /// - PlayStopUnitySchema: Type-safe parameter schema
    /// - PlayStopUnityResponse: Type-safe response structure
    /// </summary>
    [McpTool(
        RequiredSecuritySetting = SecuritySettings.AllowPlayModeControl,
        Description = "Control Unity play mode - start or stop play mode"
    )]
    public class PlayStopUnityTool : AbstractUnityTool<PlayStopUnitySchema, PlayStopUnityResponse>
    {
        public override string ToolName => "play-stop-unity";

        protected override Task<PlayStopUnityResponse> ExecuteAsync(PlayStopUnitySchema parameters, CancellationToken cancellationToken)
        {
            var response = new PlayStopUnityResponse();
            var action = (parameters.Action ?? "").ToLowerInvariant();
            
            switch (action)
            {
                case "play":
                    if (EditorApplication.isPlaying)
                    {
                        response.Success = true;
                        response.Message = "Unity is already in play mode";
                        response.ActionPerformed = "play (already playing)";
                        response.IsPlaying = true;
                    }
                    else
                    {
                        EditorApplication.EnterPlaymode();
                        response.Success = true;
                        response.Message = "Unity play mode started";
                        response.ActionPerformed = "play";
                        response.IsPlaying = true;
                    }
                    break;

                case "stop":
                    if (!EditorApplication.isPlaying)
                    {
                        response.Success = true;
                        response.Message = "Unity is already stopped";
                        response.ActionPerformed = "stop (already stopped)";
                        response.IsPlaying = false;
                    }
                    else
                    {
                        EditorApplication.ExitPlaymode();
                        response.Success = true;
                        response.Message = "Unity play mode stopped";
                        response.ActionPerformed = "stop";
                        response.IsPlaying = false;
                    }
                    break;

                default:
                    response.Success = false;
                    response.Message = string.IsNullOrEmpty(parameters.Action) 
                        ? "Action parameter is required. Use 'play' or 'stop'."
                        : $"Invalid action '{parameters.Action}'. Use 'play' or 'stop'.";
                    response.ActionPerformed = "none";
                    response.IsPlaying = EditorApplication.isPlaying;
                    break;
            }

            return Task.FromResult(response);
        }
    }
}