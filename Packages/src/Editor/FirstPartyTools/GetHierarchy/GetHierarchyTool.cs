using System.Threading.Tasks;
using System.Threading;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Bundled tool entry point for hierarchy export. The platform supplies hierarchy access through host services.
    /// </summary>
    [UnityCliLoopTool]
    public class GetHierarchyTool : UnityCliLoopTool<GetHierarchySchema, GetHierarchyResponse>, IUnityCliLoopToolHostServicesReceiver
    {
        private IUnityCliLoopHierarchyService _hierarchy;

        public override string ToolName => "get-hierarchy";

        public void InitializeHostServices(IUnityCliLoopToolHostServices services)
        {
            if (services == null)
            {
                throw new System.ArgumentNullException(nameof(services));
            }

            _hierarchy = services.Hierarchy ?? throw new System.ArgumentNullException(nameof(services.Hierarchy));
        }
        
        protected override async Task<GetHierarchyResponse> ExecuteAsync(GetHierarchySchema parameters, CancellationToken ct)
        {
            if (_hierarchy == null)
            {
                throw new System.InvalidOperationException("Host services were not initialized.");
            }

            UnityCliLoopHierarchyResult result = await _hierarchy.GetHierarchyAsync(ToRequest(parameters), ct);
            return new GetHierarchyResponse(result.FilePath, result.Message);
        }

        private static UnityCliLoopHierarchyRequest ToRequest(GetHierarchySchema parameters)
        {
            if (parameters == null)
            {
                throw new System.ArgumentNullException(nameof(parameters));
            }

            return new UnityCliLoopHierarchyRequest
            {
                IncludeInactive = parameters.IncludeInactive,
                MaxDepth = parameters.MaxDepth,
                RootPath = parameters.RootPath,
                IncludeComponents = parameters.IncludeComponents,
                IncludePaths = parameters.IncludePaths,
                UseComponentsLut = parameters.UseComponentsLut,
                UseSelection = parameters.UseSelection,
            };
        }
    }
}
