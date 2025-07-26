namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Server initialization response
    /// </summary>
    public class ServerInitializationResponse : BaseToolResponse
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Actually used server port
        /// </summary>
        public int ServerPort { get; set; }

        /// <summary>
        /// Whether the server started successfully
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Result message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Created server instance
        /// </summary>
        public McpBridgeServer ServerInstance { get; set; }
    }
}