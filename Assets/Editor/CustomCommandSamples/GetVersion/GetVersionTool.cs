using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using io.github.hatayama.uLoopMCP;

namespace Samples
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

        protected override Task<GetVersionResponse> ExecuteAsync(GetVersionSchema parameters, CancellationToken cancellationToken)
        {
            
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
            
            
            return Task.FromResult(response);
        }
    }
} 