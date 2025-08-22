using System;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Factory for creating and managing MCP configuration services
    /// Centralizes configuration service creation and caching
    /// Related classes:
    /// - McpConfigService: Configuration service for specific editor types
    /// - McpConfigRepository: Repository for configuration data
    /// - McpEditorType: Enumeration of supported editor types
    /// </summary>
    public class McpConfigServiceFactory
    {
        private readonly Dictionary<McpEditorType, McpConfigService> _configServices;

        public McpConfigServiceFactory()
        {
            _configServices = new Dictionary<McpEditorType, McpConfigService>();
            InitializeAllServices();
        }

        /// <summary>
        /// Get configuration service for specified editor type
        /// </summary>
        /// <param name="editorType">Editor type</param>
        /// <returns>Configuration service</returns>
        /// <exception cref="ArgumentException">Thrown when unsupported editor type is specified</exception>
        public McpConfigService GetConfigService(McpEditorType editorType)
        {
            if (_configServices.TryGetValue(editorType, out McpConfigService service))
            {
                return service;
            }

            throw new ArgumentException($"Unsupported editor type: {editorType}");
        }

        /// <summary>
        /// Get all available configuration services
        /// </summary>
        /// <returns>Read-only collection of all configuration services</returns>
        public IReadOnlyDictionary<McpEditorType, McpConfigService> GetAllConfigServices()
        {
            return _configServices;
        }

        /// <summary>
        /// Initialize all configuration services
        /// Automatically creates services for all defined McpEditorType enum values
        /// </summary>
        private void InitializeAllServices()
        {
            McpEditorType[] allEditorTypes = (McpEditorType[])Enum.GetValues(typeof(McpEditorType));

            foreach (McpEditorType editorType in allEditorTypes)
            {
                McpConfigRepository repository = new();
                McpConfigService service = new(repository, editorType);
                _configServices[editorType] = service;
            }
        }
    }
} 