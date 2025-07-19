using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Threading;

namespace io.github.hatayama.uLoopMCP
{
    // Related classes:
    // - IUnityTool: The interface that this class implements.
    // - UnityToolRegistry: Registers and manages instances of tool implementations.
    // - ToolParameterSchemaGenerator: Generates the JSON schema for tool parameters.
    /// <summary>
    /// Abstract base class for type-safe Unity tools using Schema and Response types
    /// </summary>
    /// <typeparam name="TSchema">Schema type for tool parameters</typeparam>
    /// <typeparam name="TResponse">Response type for tool results</typeparam>
    public abstract class AbstractUnityTool<TSchema, TResponse> : IUnityTool
        where TSchema : BaseToolSchema, new()
        where TResponse : BaseToolResponse
    {
        public abstract string ToolName { get; }

        /// <summary>
        /// Automatically generates parameter schema from TSchema type
        /// </summary>
        public virtual ToolParameterSchema ParameterSchema =>
            ToolParameterSchemaGenerator.FromDto<TSchema>();

        /// <summary>
        /// Execute tool with type-safe Schema parameters
        /// Note: This method is called from the main thread context. 
        /// MainThreadSwitcher.SwitchToMainThread() is already handled by the upper layer (JsonRpcProcessor),
        /// so individual tools do not need to call it again.
        /// </summary>
        /// <param name="parameters">Strongly typed parameters</param>
        /// <param name="cancellationToken">Cancellation token for timeout control</param>
        /// <returns>Strongly typed tool execution result</returns>
        protected abstract Task<TResponse> ExecuteAsync(TSchema parameters, CancellationToken cancellationToken);

        /// <summary>
        /// IUnityTool implementation - converts JToken to Schema and returns BaseToolResponse
        /// </summary>
        public async Task<BaseToolResponse> ExecuteAsync(JToken paramsToken)
        {
            DateTime startTime = DateTime.UtcNow;

            try
            {
                // Convert JToken to strongly typed Schema
                TSchema parameters = ConvertToSchema(paramsToken);

                // Create CancellationTokenSource with timeout from parameters
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(parameters.TimeoutSeconds)))
                {
                    try
                    {
                        // Execute with type-safe parameters and cancellation token
                        TResponse response = await ExecuteAsync(parameters, cts.Token);

                        DateTime endTime = DateTime.UtcNow;

                        // Set timing information if response inherits from BaseToolResponse
                        if (response is BaseToolResponse baseResponse)
                        {
                            baseResponse.SetTimingInfo(startTime, endTime);
                        }

                        // Return as BaseToolResponse for IUnityTool interface compatibility
                        return response;
                    }
                    catch (OperationCanceledException)
                    {
                        throw new TimeoutException($"Tool {ToolName} timed out after {parameters.TimeoutSeconds} seconds");
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Convert JToken to strongly typed Schema with default value fallback
        /// </summary>
        private TSchema ConvertToSchema(JToken paramsToken)
        {
            if (paramsToken == null || paramsToken.Type == JTokenType.Null)
            {
                // Return default instance if no parameters provided
                return new TSchema();
            }
            
            // Create JsonSerializerSettings with CamelCasePropertyNamesContractResolver
            // This allows TypeScript side to use camelCase while C# uses PascalCase
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
            };
            
            // Create JsonSerializer with custom settings
            JsonSerializer serializer = JsonSerializer.Create(settings);
            
            // Try to deserialize from JToken with custom serializer
            TSchema schema = paramsToken.ToObject<TSchema>(serializer);

            // If deserialization returns null, create default instance
            if (schema == null)
            {
                schema = new TSchema();
            }

            // Apply default values for null properties
            return ApplyDefaultValues(schema);
        }

        /// <summary>
        /// Apply default values to Schema properties if they are null
        /// Override this method to provide custom default value logic
        /// </summary>
        protected virtual TSchema ApplyDefaultValues(TSchema schema)
        {
            // Default implementation - return as is
            // Subclasses can override to apply specific default values
            return schema;
        }
    }
}