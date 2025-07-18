using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Design documentation reference: /CLAUDE.md - Connected LLM Tools storage specification
    /// Related classes:
    /// - ConnectedClient: Connected client information
    /// - McpEditorWindow: UI tool display control
    /// - McpServerController: Server lifecycle management
    /// - McpBridgeServer: Actual client connection management
    /// </summary>
    [Serializable]
    public class ConnectedLLMToolData
    {
        [SerializeField] public string Name;
        [SerializeField] public string Endpoint;
        [SerializeField] public string ConnectedAtString;
        
        // Unity doesn't serialize DateTime directly, so we use a string representation
        public DateTime ConnectedAt 
        { 
            get => DateTime.Parse(ConnectedAtString);
            set => ConnectedAtString = value.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
        }

        public ConnectedLLMToolData()
        {
            // Default constructor for Unity serialization
        }

        public ConnectedLLMToolData(string name, string endpoint, DateTime connectedAt)
        {
            Name = name;
            Endpoint = endpoint;
            ConnectedAt = connectedAt;
        }
    }

    /// <summary>
    /// Class that serializes Connected LLM Tools information and stores it using EditorPrefs
    /// Saves tool information when Unity connects and deletes it when disconnected
    /// </summary>
    public class ConnectedLLMToolsStorage
    {
        private const string EDITOR_PREFS_KEY = "uLoopMCP_ConnectedTools";
        private static ConnectedLLMToolsStorage _instance;
        private List<ConnectedLLMToolData> _connectedTools;

        public static ConnectedLLMToolsStorage Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ConnectedLLMToolsStorage();
                }
                return _instance;
            }
        }



        /// <summary>
        /// Wrapper class for JSON serialization
        /// </summary>
        [System.Serializable]
        public class ConnectedLLMToolsData
        {
            public List<ConnectedLLMToolData> tools = new();
        }

        /// <summary>
        /// Constructor - load from EditorPrefs
        /// </summary>
        private ConnectedLLMToolsStorage()
        {
            LoadFromEditorPrefs();
        }

        /// <summary>
        /// Load data from EditorPrefs
        /// </summary>
        private void LoadFromEditorPrefs()
        {
            string jsonData = EditorPrefs.GetString(EDITOR_PREFS_KEY, "");
            VibeLogger.LogInfo("storage_prefs_raw_data", $"Raw EditorPrefs data: {jsonData}", new { key = EDITOR_PREFS_KEY, raw_data = jsonData });
            
            if (!string.IsNullOrEmpty(jsonData))
            {
                try
                {
                    ConnectedLLMToolsData data = JsonUtility.FromJson<ConnectedLLMToolsData>(jsonData);
                    _connectedTools = data.tools ?? new List<ConnectedLLMToolData>();
                    VibeLogger.LogInfo("storage_loaded_from_prefs", $"Loaded {_connectedTools.Count} tools from EditorPrefs", new { count = _connectedTools.Count });
                }
                catch (System.Exception ex)
                {
                    VibeLogger.LogInfo("storage_load_failed", $"Failed to load from EditorPrefs: {ex.Message}", new { error = ex.Message });
                    _connectedTools = new List<ConnectedLLMToolData>();
                }
            }
            else
            {
                _connectedTools = new List<ConnectedLLMToolData>();
                VibeLogger.LogInfo("storage_no_prefs_data", "No data found in EditorPrefs", new { });
            }
        }

        /// <summary>
        /// Save data to EditorPrefs
        /// </summary>
        private void SaveToEditorPrefs()
        {
            ConnectedLLMToolsData data = new ConnectedLLMToolsData { tools = _connectedTools };
            string jsonData = JsonUtility.ToJson(data);
            VibeLogger.LogInfo("storage_save_debug", $"Saving to EditorPrefs: {jsonData}", new { key = EDITOR_PREFS_KEY, json_data = jsonData, tool_count = _connectedTools.Count });
            EditorPrefs.SetString(EDITOR_PREFS_KEY, jsonData);
            VibeLogger.LogInfo("storage_saved_to_prefs", $"Saved {_connectedTools.Count} tools to EditorPrefs", new { count = _connectedTools.Count });
        }

        /// <summary>
        /// Ensure list is initialized
        /// </summary>
        private void EnsureInitialized()
        {
            if (_connectedTools == null)
            {
                LoadFromEditorPrefs();
            }
        }




        /// <summary>
        /// Get read-only list of connected tools
        /// </summary>
        public IReadOnlyList<ConnectedLLMToolData> ConnectedTools 
        {
            get
            {
                EnsureInitialized();
                return _connectedTools.AsReadOnly();
            }
        }

        /// <summary>
        /// Get the number of connected tools
        /// </summary>
        public int Count 
        {
            get
            {
                EnsureInitialized();
                return _connectedTools.Count;
            }
        }

        /// <summary>
        /// Check if a tool with the specified name is connected
        /// </summary>
        public bool HasTool(string toolName)
        {
            EnsureInitialized();
            return _connectedTools.Any(tool => tool.Name == toolName);
        }

        /// <summary>
        /// Get a tool with the specified name
        /// </summary>
        public ConnectedLLMToolData GetTool(string toolName)
        {
            EnsureInitialized();
            return _connectedTools.FirstOrDefault(tool => tool.Name == toolName);
        }

        /// <summary>
        /// Save tool information when Unity connects
        /// </summary>
        public void SaveConnectedTools(IEnumerable<ConnectedClient> clients)
        {
            EnsureInitialized();
            ConnectedClient[] clientArray = clients.ToArray();
            VibeLogger.LogInfo("storage_save_connected_tools", $"SaveConnectedTools called with {clientArray.Length} clients", new { client_count = clientArray.Length });
            
            _connectedTools.Clear();
            
            foreach (ConnectedClient client in clientArray)
            {
                if (client.ClientName != McpConstants.UNKNOWN_CLIENT_NAME)
                {
                    ConnectedLLMToolData toolData = new(
                        client.ClientName, 
                        client.Endpoint, 
                        client.ConnectedAt
                    );
                    _connectedTools.Add(toolData);
                    VibeLogger.LogInfo("storage_saved_tool", $"Saved tool: {client.ClientName} -> {client.Endpoint}", new { name = client.ClientName, endpoint = client.Endpoint });
                }
                else
                {
                    VibeLogger.LogInfo("storage_skipped_unknown_client", $"Skipped unknown client: {client.Endpoint}", new { endpoint = client.Endpoint });
                }
            }
            
            VibeLogger.LogInfo("storage_total_stored_tools", $"Total stored tools: {_connectedTools.Count}", new { total_count = _connectedTools.Count });
            SaveToEditorPrefs();
        }

        /// <summary>
        /// Delete tool information when Unity disconnects
        /// </summary>
        public void ClearConnectedTools()
        {
            EnsureInitialized();
            _connectedTools.Clear();
            SaveToEditorPrefs();
        }

        /// <summary>
        /// Remove a specific tool
        /// </summary>
        public void RemoveTool(string toolName)
        {
            EnsureInitialized();
            int removedCount = _connectedTools.RemoveAll(tool => tool.Name == toolName);
            
            if (removedCount > 0)
            {
                SaveToEditorPrefs();
            }
        }

        /// <summary>
        /// Convert stored tool data to ConnectedClient for UI display
        /// </summary>
        public ConnectedClient ConvertToConnectedClient(ConnectedLLMToolData toolData)
        {
            // Create a mock NetworkStream for UI display purposes
            return new ConnectedClient(toolData.Endpoint, null, toolData.Name);
        }

        /// <summary>
        /// Get all stored tools as ConnectedClient objects for UI display, sorted by name
        /// </summary>
        public IEnumerable<ConnectedClient> GetStoredToolsAsConnectedClients()
        {
            EnsureInitialized();
            VibeLogger.LogInfo("storage_get_stored_tools", $"GetStoredToolsAsConnectedClients called. Available tools: {_connectedTools.Count}", new { available_count = _connectedTools.Count });
            foreach (ConnectedLLMToolData tool in _connectedTools)
            {
                VibeLogger.LogInfo("storage_available_tool", $"Tool: {tool.Name} -> {tool.Endpoint}", new { name = tool.Name, endpoint = tool.Endpoint });
            }
            return _connectedTools.OrderBy(tool => tool.Name).Select(tool => ConvertToConnectedClient(tool));
        }

        /// <summary>
        /// Add a new tool
        /// </summary>
        public void AddTool(ConnectedClient client)
        {
            EnsureInitialized();
            VibeLogger.LogInfo("storage_add_tool", $"AddTool called for: {client.ClientName} -> {client.Endpoint}", new { name = client.ClientName, endpoint = client.Endpoint });
            
            if (client.ClientName == McpConstants.UNKNOWN_CLIENT_NAME)
            {
                VibeLogger.LogInfo("storage_add_tool_skipped_unknown", "Skipping unknown client", new { endpoint = client.Endpoint });
                return;
            }

            // Remove existing tool if present, then add
            RemoveTool(client.ClientName);
            
            ConnectedLLMToolData toolData = new(
                client.ClientName, 
                client.Endpoint, 
                client.ConnectedAt
            );
            _connectedTools.Add(toolData);
            
            VibeLogger.LogInfo("storage_add_tool_completed", $"Added tool: {client.ClientName}. Total tools: {_connectedTools.Count}", new { name = client.ClientName, total_count = _connectedTools.Count });
            SaveToEditorPrefs();
        }
    }
}