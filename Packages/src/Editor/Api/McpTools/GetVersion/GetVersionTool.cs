using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace io.github.hatayama.UnityCliLoop
{
    [UnityCliLoopTool(DisplayDevelopmentOnly = true)]
    public class GetVersionTool : UnityCliLoopTool<GetVersionSchema, GetVersionResponse>
    {
        public override string ToolName => "get-version";

        protected override Task<GetVersionResponse> ExecuteAsync(
            GetVersionSchema parameters, CancellationToken ct)
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
                CompanyName = Application.companyName
            };

            return Task.FromResult(response);
        }
    }
}
