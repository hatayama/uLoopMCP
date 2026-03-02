namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Base response class for all Unity MCP tool responses
    /// </summary>
    public abstract class BaseToolResponse
    {
        /// <summary>
        /// uLoopMCP server version for CLI version compatibility check
        /// </summary>
        public string Ver => McpVersion.VERSION;
    }
}
