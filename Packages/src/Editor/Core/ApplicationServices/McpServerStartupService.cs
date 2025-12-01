namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Application service responsible for MCP server startup operations.
    /// Handles server instance creation, startup, and lifecycle management.
    /// Also manages the Node.js TypeScript server process.
    ///
    /// Related classes:
    /// - McpBridgeServer: The actual TCP server instance being managed
    /// - NodeProcessService: Manages Node.js process lifecycle
    /// - McpSessionManager: Manages server session state
    /// - McpServerController: Coordinates overall server management
    /// </summary>
    public class McpServerStartupService
    {
        private NodeProcessService _nodeProcessService;

        /// <summary>
        /// Gets a value indicating whether the Node.js process is running.
        /// </summary>
        public bool IsNodeProcessRunning => _nodeProcessService?.IsRunning ?? false;

        /// <summary>
        /// Creates and starts a new MCP server instance.
        /// TCP port is automatically assigned by the OS.
        /// After TCP server starts, launches the Node.js TypeScript server.
        /// </summary>
        /// <returns>The created server instance</returns>
        public ServiceResult<McpBridgeServer> StartServer()
        {
            try
            {
                // 1. Start TCP server (port auto-assigned by OS)
                McpBridgeServer server = new();
                server.StartServer();
                int tcpPort = server.Port;

                // 2. Get HTTP port from settings
                int httpPort = McpEditorSettings.GetHttpPort();

                // 3. Start Node.js process with environment variables
                _nodeProcessService = new NodeProcessService();
                ServiceResult<int> nodeResult = _nodeProcessService.StartProcess(tcpPort, httpPort);
                if (!nodeResult.Success)
                {
                    // Node.js failed to start, cleanup TCP server
                    server.Dispose();
                    return ServiceResult<McpBridgeServer>.FailureResult(
                        $"TCP server started but Node.js failed: {nodeResult.ErrorMessage}");
                }

                VibeLogger.LogInfo("server_startup_complete",
                    $"tcp_port={tcpPort} http_port={httpPort} node_pid={nodeResult.Data}");

                return ServiceResult<McpBridgeServer>.SuccessResult(server);
            }
            catch (System.Exception ex)
            {
                // Cleanup Node.js if it was started
                _nodeProcessService?.Dispose();
                _nodeProcessService = null;
                return ServiceResult<McpBridgeServer>.FailureResult($"Failed to start server: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops and disposes of the given server instance and Node.js process.
        /// </summary>
        /// <param name="server">Server instance to stop</param>
        /// <returns>Success indicator</returns>
        public ServiceResult<bool> StopServer(McpBridgeServer server)
        {
            try
            {
                // 1. Stop Node.js process first
                _nodeProcessService?.StopProcess();
                _nodeProcessService?.Dispose();
                _nodeProcessService = null;

                // 2. Stop TCP server
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
            McpEditorSettings.SetIsServerRunning(isRunning);
            McpEditorSettings.SetServerPort(port);
            return ServiceResult<bool>.SuccessResult(true);
        }
    }
}