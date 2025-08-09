using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// PlayUnity tool handler - Starts Unity play mode
    /// 
    /// Design Reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// 
    /// This Tool class directly handles Unity play mode start using EditorApplication API.
    /// Simplified implementation without UseCase for basic operations.
    /// 
    /// Related classes:
    /// - EmptyToolSchema: Empty parameter schema (no custom parameters needed)
    /// - PlayStopUnityResponse: Type-safe response structure
    /// </summary>
    [McpTool(
        RequiredSecuritySetting = SecuritySettings.AllowPlayModeControl,
        Description = "Start Unity play mode"
    )]
    public class PlayUnityTool : AbstractUnityTool<EmptyToolSchema, PlayStopUnityResponse>
    {
        public override string ToolName => "play-unity";

        protected override Task<PlayStopUnityResponse> ExecuteAsync(EmptyToolSchema parameters, CancellationToken cancellationToken)
        {
            var response = new PlayStopUnityResponse();
            
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

            return Task.FromResult(response);
        }
    }
}