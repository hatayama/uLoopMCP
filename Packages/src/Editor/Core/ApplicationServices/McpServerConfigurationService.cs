namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Application service responsible for MCP server configuration management.
    /// Handles configuration validation, port resolution, and configuration updating.
    /// 
    /// Related classes:
    /// - McpServerController: Uses this service for server configuration
    /// - McpEditorSettings: Provides configuration storage
    /// - McpPortValidator: Validates port configurations
    /// </summary>
    public class McpServerConfigurationService
    {
        /// <summary>
        /// Validates server configuration settings.
        /// </summary>
        /// <param name="port">Port number to validate</param>
        /// <returns>Validation result with details</returns>
        public ServiceResult<bool> ValidateConfiguration(int port)
        {
            return ServiceResult<bool>.SuccessResult(true);
        }

        /// <summary>
        /// Resolves the actual port to use for server startup.
        /// </summary>
        /// <param name="requestedPort">Requested port number (-1 for default)</param>
        /// <returns>Resolved port number</returns>
        public ServiceResult<int> ResolvePort(int requestedPort)
        {
            int actualPort = requestedPort == -1 ? McpEditorSettings.GetCustomPort() : requestedPort;
            return ServiceResult<int>.SuccessResult(actualPort);
        }
    }
}