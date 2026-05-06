using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace io.github.hatayama.UnityCliLoop
{
    public interface IUnityCliLoopGameObjectSearchService
    {
        Task<UnityCliLoopGameObjectSearchResult> FindGameObjectsAsync(
            UnityCliLoopGameObjectSearchRequest request,
            CancellationToken ct);
    }

    public enum SearchMode
    {
        Exact = 0,
        Path = 1,
        Regex = 2,
        Contains = 3,
        Selected = 4
    }

    public sealed class UnityCliLoopGameObjectSearchRequest
    {
        public string NamePattern { get; set; } = "";
        public SearchMode SearchMode { get; set; } = SearchMode.Exact;
        public string[] RequiredComponents { get; set; } = new string[0];
        public string Tag { get; set; } = "";
        public int? Layer { get; set; }
        public bool IncludeInactive { get; set; }
        public int MaxResults { get; set; } = 20;
        public bool IncludeInheritedProperties { get; set; }
    }

    public sealed class UnityCliLoopGameObjectSearchResult
    {
        public UnityCliLoopGameObjectResult[] Results { get; set; }
        public int TotalFound { get; set; }
        public string ErrorMessage { get; set; }
        public string ResultsFilePath { get; set; }
        public string Message { get; set; }
        public UnityCliLoopGameObjectProcessingError[] ProcessingErrors { get; set; }
    }

    public sealed class UnityCliLoopGameObjectResult
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("layer")]
        public int Layer { get; set; }

        [JsonProperty("components")]
        public ComponentInfo[] Components { get; set; }
    }

    public sealed class UnityCliLoopGameObjectProcessingError
    {
        [JsonProperty("gameObjectName")]
        public string GameObjectName { get; set; }

        [JsonProperty("gameObjectPath")]
        public string GameObjectPath { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }
    }
}
