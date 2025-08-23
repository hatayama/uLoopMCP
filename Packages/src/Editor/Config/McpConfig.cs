using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Data structure for MCP settings.
    /// An immutable class representing the structure of the mcp.json file.
    /// </summary>
    public class McpConfig
    {
        public readonly Dictionary<string, McpServerConfigData> mcpServers;

        public McpConfig(Dictionary<string, McpServerConfigData> mcpServers)
        {
            this.mcpServers = mcpServers;
        }
    }

    /// <summary>
    /// Data structure for MCP server settings.
    /// An immutable class representing the configuration information for each server.
    /// </summary>
    public class McpServerConfigData
    {
        public readonly string command;
        public readonly string[] args;
        public readonly Dictionary<string, string> env;

        public McpServerConfigData(string command, string[] args, Dictionary<string, string> env)
        {
            this.command = command;
            this.args = args;
            this.env = env;
        }
    }

} 