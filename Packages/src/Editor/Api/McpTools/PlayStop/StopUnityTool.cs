using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// StopUnity tool handler - Stops Unity play mode
    /// 
    /// Design Reference: @Packages/docs/ARCHITECTURE_Unity.md - UseCase + Tool Pattern (DDD Integration)
    /// 
    /// This Tool class directly handles Unity play mode stop using EditorApplication API.
    /// Simplified implementation without UseCase for basic operations.
    /// 
    /// Related classes:
    /// - EmptyToolSchema: Empty parameter schema (no custom parameters needed)
    /// - PlayStopUnityResponse: Type-safe response structure
    /// </summary>
    [McpTool(
        RequiredSecuritySetting = SecuritySettings.AllowPlayModeControl,
        Description = "Stop Unity play mode"
    )]
    public class StopUnityTool : AbstractUnityTool<EmptyToolSchema, PlayStopUnityResponse>
    {
        public override string ToolName => "stop-unity";

        protected override Task<PlayStopUnityResponse> ExecuteAsync(EmptyToolSchema parameters, CancellationToken cancellationToken)
        {
            var response = new PlayStopUnityResponse();
            
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

            return Task.FromResult(response);
        }
    }
}