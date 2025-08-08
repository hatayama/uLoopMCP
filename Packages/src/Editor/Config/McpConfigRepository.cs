using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Class responsible for persisting MCP settings.
    /// Single Responsibility Principle: Only responsible for reading and writing configuration files.
    /// </summary>
    public class McpConfigRepository
    {
        private readonly McpEditorType _editorType;
        
        // Security: Safe JSON serializer settings
        private static readonly JsonSerializerSettings SafeJsonSettings = new()
        {
            TypeNameHandling = TypeNameHandling.None, // Disable type information
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double
        };

        public McpConfigRepository(McpEditorType editorType = McpEditorType.Cursor)
        {
            _editorType = editorType;
        }

        /// <summary>
        /// Checks if the configuration file exists.
        /// </summary>
        public bool Exists(string configPath)
        {
            return File.Exists(configPath);
        }

        /// <summary>
        /// Checks if the configuration file exists (with automatic editor type resolution).
        /// </summary>
        public bool Exists()
        {
            string configPath = UnityMcpPathResolver.GetConfigPath(_editorType);
            return Exists(configPath);
        }

        /// <summary>
        /// Creates the configuration directory.
        /// </summary>
        public void CreateConfigDirectory(string configPath)
        {
            string configDir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
        }

        /// <summary>
        /// Creates the configuration directory (with automatic editor type resolution).
        /// </summary>
        public void CreateConfigDirectory()
        {
            string configDirectory = UnityMcpPathResolver.GetConfigDirectory(_editorType);
            if (!string.IsNullOrEmpty(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }
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
        if (rootObject != null && rootObject.ContainsKey(McpConstants.JSON_KEY_MCP_SERVERS))
        {
            // Get mcpServers as a dictionary with safe settings.
            string mcpServersJson = JsonConvert.SerializeObject(rootObject[McpConstants.JSON_KEY_MCP_SERVERS], SafeJsonSettings);
            Dictionary<string, object> mcpServersObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(mcpServersJson, SafeJsonSettings);
            
            if (mcpServersObject != null)
            {
                foreach (KeyValuePair<string, object> serverEntry in mcpServersObject)
                {
                    string serverName = serverEntry.Key;
                    
                    // Get each server's settings as a dictionary with safe settings.
                    string serverConfigJson = JsonConvert.SerializeObject(serverEntry.Value, SafeJsonSettings);
                    Dictionary<string, object> serverConfigObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(serverConfigJson, SafeJsonSettings);
                    
                    if (serverConfigObject != null)
                    {
                        string command = serverConfigObject.ContainsKey(McpConstants.JSON_KEY_COMMAND) ? serverConfigObject[McpConstants.JSON_KEY_COMMAND]?.ToString() ?? "" : "";
                        
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
                }
            }
        }
        
        return new McpConfig(servers);
        }

        /// <summary>
        /// Loads the mcp.json settings (with automatic editor type resolution).
        /// </summary>
        public McpConfig Load()
        {
            string configPath = UnityMcpPathResolver.GetConfigPath(_editorType);
            return Load(configPath);
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
            
            // Update only the mcpServers section.
            jsonStructure[McpConstants.JSON_KEY_MCP_SERVERS] = config.mcpServers.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    command = kvp.Value.command,
                    args = kvp.Value.args,
                    env = kvp.Value.env
                }
            );

            // Security: Use safe settings for serialization
            string newJsonContent = JsonConvert.SerializeObject(jsonStructure, Formatting.Indented, SafeJsonSettings);
            
            // Only write if file doesn't exist or content has actually changed
            if (!fileExists || !IsConfigurationEqual(existingContent, config))
            {
                File.WriteAllText(configPath, newJsonContent);
            }
        }

        /// <summary>
        /// Saves the mcp.json settings (with automatic editor type resolution).
        /// </summary>
        public void Save(McpConfig config)
        {
            string configPath = UnityMcpPathResolver.GetConfigPath(_editorType);
            
            // Create the directory if it's needed.
            CreateConfigDirectory(configPath);
            
            Save(configPath, config);
        }
        
        /// <summary>
        /// Compares existing configuration content with new configuration to detect actual changes.
        /// </summary>
        /// <param name="existingContent">Existing file content</param>
        /// <param name="newConfig">New configuration to compare</param>
        /// <returns>True if configurations are equal, false if different</returns>
        private bool IsConfigurationEqual(string existingContent, McpConfig newConfig)
        {
            if (string.IsNullOrWhiteSpace(existingContent))
            {
                return false;
            }

            try
            {
                // Parse existing configuration
                McpConfig existingConfig = ParseConfigurationFromJson(existingContent);
                
                // Compare configurations
                return AreConfigurationsEqual(existingConfig, newConfig);
            }
            catch (System.Exception)
            {
                // If parsing fails, assume content is different
                return false;
            }
        }
        
        /// <summary>
        /// Parses configuration from JSON content.
        /// </summary>
        /// <param name="jsonContent">JSON content</param>
        /// <returns>Parsed configuration</returns>
        private McpConfig ParseConfigurationFromJson(string jsonContent)
        {
            Dictionary<string, object> rootObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonContent, SafeJsonSettings);
            Dictionary<string, McpServerConfigData> servers = new();
            
            if (rootObject == null || !rootObject.ContainsKey(McpConstants.JSON_KEY_MCP_SERVERS))
            {
                return new McpConfig(servers);
            }

            string mcpServersJson = JsonConvert.SerializeObject(rootObject[McpConstants.JSON_KEY_MCP_SERVERS], SafeJsonSettings);
            Dictionary<string, object> mcpServersObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(mcpServersJson, SafeJsonSettings);
            
            if (mcpServersObject == null)
            {
                return new McpConfig(servers);
            }

            foreach (KeyValuePair<string, object> serverEntry in mcpServersObject)
            {
                string serverName = serverEntry.Key;
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
        /// Compares two configurations for equality.
        /// </summary>
        /// <param name="config1">First configuration</param>
        /// <param name="config2">Second configuration</param>
        /// <returns>True if configurations are equal</returns>
        private bool AreConfigurationsEqual(McpConfig config1, McpConfig config2)
        {
            if (config1.mcpServers.Count != config2.mcpServers.Count)
            {
                return false;
            }
            
            foreach (KeyValuePair<string, McpServerConfigData> server1 in config1.mcpServers)
            {
                if (!config2.mcpServers.ContainsKey(server1.Key))
                {
                    return false;
                }
                
                McpServerConfigData server2 = config2.mcpServers[server1.Key];
                
                if (server1.Value.command != server2.command)
                {
                    return false;
                }
                
                if (!server1.Value.args.SequenceEqual(server2.args))
                {
                    return false;
                }
                
                if (server1.Value.env.Count != server2.env.Count)
                {
                    return false;
                }
                
                foreach (KeyValuePair<string, string> env1 in server1.Value.env)
                {
                    if (!server2.env.ContainsKey(env1.Key) || server2.env[env1.Key] != env1.Value)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
    }

} 