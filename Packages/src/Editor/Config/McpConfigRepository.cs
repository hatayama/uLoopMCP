using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Class responsible for persisting MCP settings.
    /// Single Responsibility Principle: Only responsible for reading and writing configuration files.
    /// </summary>
    public class McpConfigRepository
    {
        // Security: Safe JSON serializer settings
        private static readonly JsonSerializerSettings SafeJsonSettings = new()
        {
            TypeNameHandling = TypeNameHandling.None, // Disable type information
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double
        };

        /// <summary>
        /// Checks if the configuration file exists.
        /// </summary>
        public bool Exists(string configPath)
        {
            return File.Exists(configPath);
        }

        /// <summary>
        /// Creates the configuration directory.
        /// </summary>
        public void CreateConfigDirectory(string configPath)
        {
            string configDir = Path.GetDirectoryName(configPath);
            if (string.IsNullOrEmpty(configDir) || Directory.Exists(configDir))
            {
                return;
            }

            Directory.CreateDirectory(configDir);
        }

        /// <summary>
        /// Loads the mcp.json settings.
        /// </summary>
        public McpConfig Load(string configPath)
        {
            if (!File.Exists(configPath))
            {
                return new McpConfig(new Dictionary<string, McpServerConfigData>());
            }

            string jsonContent = File.ReadAllText(configPath);

            // Security: Validate JSON content before deserialization
            if (string.IsNullOrWhiteSpace(jsonContent) || jsonContent.Length > McpConstants.MAX_JSON_SIZE_BYTES)
            {
                return new McpConfig(new Dictionary<string, McpServerConfigData>());
            }

            // First, load the existing JSON as a dictionary with safe settings.
            Dictionary<string, object> rootObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonContent, SafeJsonSettings);
            Dictionary<string, McpServerConfigData> servers = new();

            // Check if the mcpServers section exists.
            if (rootObject == null || !rootObject.ContainsKey(McpConstants.JSON_KEY_MCP_SERVERS))
            {
                return new McpConfig(servers);
            }

            // Get mcpServers as a dictionary with safe settings.
            string mcpServersJson = JsonConvert.SerializeObject(rootObject[McpConstants.JSON_KEY_MCP_SERVERS], SafeJsonSettings);
            Dictionary<string, object> mcpServersObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(mcpServersJson, SafeJsonSettings);

            if (mcpServersObject == null)
            {
                return new McpConfig(servers);
            }

            foreach (KeyValuePair<string, object> serverEntry in mcpServersObject)
            {
                string serverName = serverEntry.Key;

                // Get each server's settings as a dictionary with safe settings.
                string serverConfigJson = JsonConvert.SerializeObject(serverEntry.Value, SafeJsonSettings);
                Dictionary<string, object> serverConfigObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(serverConfigJson, SafeJsonSettings);

                if (serverConfigObject == null)
                {
                    continue;
                }

                string command = serverConfigObject.ContainsKey(McpConstants.JSON_KEY_COMMAND) 
                    ? serverConfigObject[McpConstants.JSON_KEY_COMMAND]?.ToString() ?? "" 
                    : "";

                string[] args = new string[0];
                if (serverConfigObject.ContainsKey(McpConstants.JSON_KEY_ARGS))
                {
                    string argsJson = JsonConvert.SerializeObject(serverConfigObject[McpConstants.JSON_KEY_ARGS], SafeJsonSettings);
                    args = JsonConvert.DeserializeObject<string[]>(argsJson, SafeJsonSettings) ?? new string[0];
                }

                Dictionary<string, string> env = new();
                if (serverConfigObject.ContainsKey(McpConstants.JSON_KEY_ENV))
                {
                    string envJson = JsonConvert.SerializeObject(serverConfigObject[McpConstants.JSON_KEY_ENV], SafeJsonSettings);
                    env = JsonConvert.DeserializeObject<Dictionary<string, string>>(envJson, SafeJsonSettings) ?? new Dictionary<string, string>();
                }

                servers[serverName] = new McpServerConfigData(command, args, env);
            }

            return new McpConfig(servers);
        }


        /// <summary>
        /// Saves the mcp.json settings.
        /// </summary>
        public void Save(string configPath, McpConfig config)
        {
            Dictionary<string, object> jsonStructure;
            bool fileExists = File.Exists(configPath);
            string existingContent = string.Empty;

            // If the file exists, retain its existing structure.
            if (fileExists)
            {
                existingContent = File.ReadAllText(configPath);
                // Security: Use safe settings for deserialization
                jsonStructure = JsonConvert.DeserializeObject<Dictionary<string, object>>(existingContent, SafeJsonSettings) ?? new Dictionary<string, object>();
            }
            else
            {
                jsonStructure = new Dictionary<string, object>();
            }

            Dictionary<string, object> mergedServers = GetExistingNonULoopMCPServers(jsonStructure);

            foreach (KeyValuePair<string, McpServerConfigData> kvp in config.mcpServers)
            {
                mergedServers[kvp.Key] = new
                {
                    command = kvp.Value.command,
                    args = kvp.Value.args,
                    env = kvp.Value.env
                };
            }

            jsonStructure[McpConstants.JSON_KEY_MCP_SERVERS] = mergedServers;

            // Security: Use safe settings for serialization
            string newJsonContent = JsonConvert.SerializeObject(jsonStructure, Formatting.Indented, SafeJsonSettings);

            // Only write if file doesn't exist or content has actually changed
            if (!fileExists || !IsConfigurationEqual(existingContent, config))
            {
                File.WriteAllText(configPath, newJsonContent);
            }
        }

        /// <summary>
        /// Compares existing configuration content with new configuration to detect actual changes.
        /// Only compares uLoopMCP-related entries to avoid false positives from other MCP servers.
        /// </summary>
        /// <param name="existingContent">Existing file content</param>
        /// <param name="newConfig">New configuration to compare</param>
        /// <returns>True if uLoopMCP configurations are equal, false if different</returns>
        private bool IsConfigurationEqual(string existingContent, McpConfig newConfig)
        {
            if (string.IsNullOrWhiteSpace(existingContent))
            {
                return false;
            }

            try
            {
                // Parse existing JSON structure
                Dictionary<string, object> existingStructure = JsonConvert.DeserializeObject<Dictionary<string, object>>(existingContent, SafeJsonSettings);
                if (existingStructure == null || !existingStructure.ContainsKey(McpConstants.JSON_KEY_MCP_SERVERS))
                {
                    return false;
                }

                // Extract existing uLoopMCP servers
                Dictionary<string, object> existingULoopServers = ExtractULoopMCPServers(existingStructure);

                // Extract new uLoopMCP servers
                Dictionary<string, object> newULoopServers = newConfig.mcpServers
                    .Where(kvp => IsULoopMCPServer(kvp.Key, kvp.Value))
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => (object)new
                        {
                            command = kvp.Value.command,
                            args = kvp.Value.args,
                            env = kvp.Value.env
                        }
                    );

                // Convert to JToken and normalize object property order recursively
                JToken existingToken = NormalizeJToken(JToken.FromObject(existingULoopServers));
                JToken newToken = NormalizeJToken(JToken.FromObject(newULoopServers));

                // Order-insensitive deep equality comparison
                return JToken.DeepEquals(existingToken, newToken);
            }
            catch (System.Exception)
            {
                // If parsing fails, assume content is different
                return false;
            }
        }

        /// <summary>
        /// Recursively normalizes a JToken by sorting JObject properties by name.
        /// Array element order is preserved.
        /// </summary>
        private static JToken NormalizeJToken(JToken token)
        {
            if (token is JObject obj)
            {
                JObject sorted = new JObject();
                foreach (JProperty prop in obj.Properties().OrderBy(p => p.Name, System.StringComparer.Ordinal))
                {
                    sorted.Add(prop.Name, NormalizeJToken(prop.Value));
                }
                return sorted;
            }

            if (token is JArray arr)
            {
                JArray normalizedArray = new JArray();
                foreach (JToken item in arr)
                {
                    normalizedArray.Add(NormalizeJToken(item));
                }
                return normalizedArray;
            }

            return token; // JValue and others
        }

        /// <summary>
        /// Extracts uLoopMCP server entries from existing JSON structure.
        /// </summary>
        /// <param name="jsonStructure">Parsed JSON structure</param>
        /// <returns>Dictionary containing only uLoopMCP server entries</returns>
        private Dictionary<string, object> ExtractULoopMCPServers(Dictionary<string, object> jsonStructure)
        {
            Dictionary<string, object> uloopServers = new();

            if (!jsonStructure.ContainsKey(McpConstants.JSON_KEY_MCP_SERVERS))
            {
                return uloopServers;
            }

            string mcpServersJson = JsonConvert.SerializeObject(jsonStructure[McpConstants.JSON_KEY_MCP_SERVERS], SafeJsonSettings);
            Dictionary<string, object> mcpServers = JsonConvert.DeserializeObject<Dictionary<string, object>>(mcpServersJson, SafeJsonSettings);

            if (mcpServers == null)
            {
                return uloopServers;
            }

            foreach (KeyValuePair<string, object> serverEntry in mcpServers)
            {
                if (!serverEntry.Key.StartsWith(McpConstants.PROJECT_NAME))
                {
                    continue;
                }

                // Check if it has UNITY_TCP_PORT environment variable (uLoopMCP marker)
                string serverJson = JsonConvert.SerializeObject(serverEntry.Value, SafeJsonSettings);
                Dictionary<string, object> serverData = JsonConvert.DeserializeObject<Dictionary<string, object>>(serverJson, SafeJsonSettings);

                if (serverData?.ContainsKey(McpConstants.JSON_KEY_ENV) == true)
                {
                    string envJson = JsonConvert.SerializeObject(serverData[McpConstants.JSON_KEY_ENV], SafeJsonSettings);
                    Dictionary<string, string> env = JsonConvert.DeserializeObject<Dictionary<string, string>>(envJson, SafeJsonSettings);

                    if (env?.ContainsKey(McpConstants.UNITY_TCP_PORT_ENV_KEY) == true)
                    {
                        uloopServers[serverEntry.Key] = serverEntry.Value;
                    }
                }
            }

            return uloopServers;
        }

        /// <summary>
        /// Allows other MCP servers (e.g., context7) to coexist with uLoopMCP by preserving their original structure.
        /// </summary>
        private Dictionary<string, object> GetExistingNonULoopMCPServers(Dictionary<string, object> jsonStructure)
        {
            Dictionary<string, object> nonULoopServers = new();

            if (!jsonStructure.ContainsKey(McpConstants.JSON_KEY_MCP_SERVERS))
            {
                return nonULoopServers;
            }

            string mcpServersJson = JsonConvert.SerializeObject(jsonStructure[McpConstants.JSON_KEY_MCP_SERVERS], SafeJsonSettings);
            Dictionary<string, object> existingServers = JsonConvert.DeserializeObject<Dictionary<string, object>>(mcpServersJson, SafeJsonSettings);

            if (existingServers == null)
            {
                return nonULoopServers;
            }

            foreach (KeyValuePair<string, object> serverEntry in existingServers)
            {
                if (serverEntry.Key.StartsWith(McpConstants.PROJECT_NAME))
                {
                    continue;
                }

                nonULoopServers[serverEntry.Key] = serverEntry.Value;
            }

            return nonULoopServers;
        }

        /// <summary>
        /// Determines if a server entry is a uLoopMCP server.
        /// </summary>
        /// <param name="serverKey">Server key</param>
        /// <param name="serverConfig">Server configuration</param>
        /// <returns>True if it's a uLoopMCP server</returns>
        private bool IsULoopMCPServer(string serverKey, McpServerConfigData serverConfig)
        {
            return serverKey.StartsWith(McpConstants.PROJECT_NAME)
                   && serverConfig.env.ContainsKey(McpConstants.UNITY_TCP_PORT_ENV_KEY);
        }
    }
}