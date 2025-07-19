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
            
            // If the file exists, retain its existing structure.
            if (File.Exists(configPath))
            {
                string existingContent = File.ReadAllText(configPath);
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
            string jsonContent = JsonConvert.SerializeObject(jsonStructure, Formatting.Indented, SafeJsonSettings);
            File.WriteAllText(configPath, jsonContent);
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
    }


} 