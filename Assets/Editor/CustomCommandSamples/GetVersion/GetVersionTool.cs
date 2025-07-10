using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.uMCP
{
    /// <summary>
    /// GetVersion tool handler
    /// Get Unity version information
    /// Created as an example of adding new tools
    /// </summary>
    [McpTool(Description = "Get Unity version and project information")]
    public class GetVersionTool : AbstractUnityTool<GetVersionSchema, GetVersionResponse>
    {
        public override string ToolName => "get-version";

        protected override Task<GetVersionResponse> ExecuteAsync(GetVersionSchema parameters)
        {
            McpLogger.LogDebug("GetVersion request received");
            
            GetVersionResponse response = new GetVersionResponse
            {
                UnityVersion = Application.unityVersion,
                Platform = Application.platform.ToString(),
                DataPath = Application.dataPath,
                PersistentDataPath = Application.persistentDataPath,
                TemporaryCachePath = Application.temporaryCachePath,
                IsEditor = Application.isEditor,
                ProductName = Application.productName,
                CompanyName = Application.companyName,
                Version = Application.version
            };
            
            McpLogger.LogDebug($"GetVersion completed: Unity {Application.unityVersion}");
            
            return Task.FromResult(response);
        }
    }
} 