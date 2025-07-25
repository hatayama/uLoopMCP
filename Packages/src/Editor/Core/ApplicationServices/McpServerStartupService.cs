namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Application service responsible for MCP server startup operations.
    /// Handles server instance creation, startup, and lifecycle management.
    /// 
    /// Related classes:
    /// - McpBridgeServer: The actual server instance being managed
    /// - McpSessionManager: Manages server session state
    /// - McpServerController: Coordinates overall server management
    /// </summary>
    public class McpServerStartupService
    {
        /// <summary>
        /// Creates and starts a new MCP server instance.
        /// </summary>
        /// <param name="port">Port number to start the server on</param>
        /// <returns>The created server instance</returns>
        public ServiceResult<McpBridgeServer> StartServer(int port)
        {
            try
            {
                McpBridgeServer server = new();
                server.StartServer(port);
                return ServiceResult<McpBridgeServer>.SuccessResult(server);
            }
            catch (System.Exception ex)
            {
                return ServiceResult<McpBridgeServer>.FailureResult($"Failed to start server: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops and disposes of the given server instance.
        /// </summary>
        /// <param name="server">Server instance to stop</param>
        /// <returns>Success indicator</returns>
        public ServiceResult<bool> StopServer(McpBridgeServer server)
        {
            try
            {
                if (server != null)
                {
                    server.Dispose();
                }
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (System.Exception ex)
            {
                return ServiceResult<bool>.FailureResult($"Failed to stop server: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates session manager with server state.
        /// </summary>
        /// <param name="isRunning">Whether the server is running</param>
        /// <param name="port">Server port number</param>
        /// <returns>Success indicator</returns>
        public ServiceResult<bool> UpdateSessionState(bool isRunning, int port)
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            sessionManager.IsServerRunning = isRunning;
            sessionManager.ServerPort = port;
            return ServiceResult<bool>.SuccessResult(true);
        }
    }
}