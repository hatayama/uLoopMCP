using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Base class for Device Agent tools.
    /// Provides type-safe parameter deserialization (same pattern as Editor's AbstractUnityTool).
    /// </summary>
    public abstract class AbstractDeviceTool<TSchema, TResponse> : IUnityTool
        where TSchema : BaseToolSchema, new()
        where TResponse : BaseToolResponse
    {
        public abstract string ToolName { get; }

        public ToolParameterSchema ParameterSchema => null; // Not used in Device Agent

        protected abstract Task<TResponse> ExecuteAsync(TSchema parameters, CancellationToken ct);

        public async Task<BaseToolResponse> ExecuteAsync(JToken paramsToken)
        {
            TSchema parameters = DeserializeParams(paramsToken);
            return await ExecuteAsync(parameters, CancellationToken.None);
        }

        private static TSchema DeserializeParams(JToken paramsToken)
        {
            if (paramsToken == null || paramsToken.Type == JTokenType.Null)
            {
                return new TSchema();
            }

            JsonSerializer serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
            });

            return paramsToken.ToObject<TSchema>(serializer) ?? new TSchema();
        }
    }
}
