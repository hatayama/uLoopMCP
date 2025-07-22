using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Schema for SetClientName command parameters
    /// Allows TypeScript clients to register their name and provide push notification endpoint
    /// </summary>
    public class SetClientNameSchema : BaseToolSchema
    {
        /// <summary>
        /// Name of the MCP client tool (e.g., "Claude Code", "Cursor")
        /// </summary>
        [Description("Name of the MCP client tool")]
        public string ClientName { get; set; } = McpConstants.UNKNOWN_CLIENT_NAME;

        /// <summary>
        /// Push notification server endpoint from TypeScript side
        /// Format: "localhost:port"
        /// </summary>
        [Description("Push notification server endpoint for Unity to connect to")]
        public string PushNotificationEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Client port number for unique identification
        /// Used in combination with ClientName to create unique key for multiple client instances
        /// </summary>
        [Description("Client port number for unique client identification")]
        public int ClientPort { get; set; } = 0;
    }
}