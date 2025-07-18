using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Connected LLM Tools storage specification
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
    /// Class that serializes Connected LLM Tools information and stores it using ScriptableSingleton
    /// Saves tool information when Unity connects and deletes it when disconnected
    /// </summary>
    public class ConnectedLLMToolsStorage : ScriptableSingleton<ConnectedLLMToolsStorage>
    {
        [SerializeField]
        private List<ConnectedLLMToolData> _connectedTools = new();

        /// <summary>
        /// Get read-only list of connected tools
        /// </summary>
        public IReadOnlyList<ConnectedLLMToolData> ConnectedTools => _connectedTools.AsReadOnly();

        /// <summary>
        /// Delete tool information when Unity disconnects
        /// </summary>
        public void ClearConnectedTools()
        {
            _connectedTools.Clear();
        }

        /// <summary>
        /// Remove a specific tool
        /// </summary>
        public void RemoveTool(string toolName)
        {
            _connectedTools.RemoveAll(tool => tool.Name == toolName);
        }

        /// <summary>
        /// Convert stored tool data to ConnectedClient for UI display
        /// </summary>
        public ConnectedClient ConvertToConnectedClient(ConnectedLLMToolData toolData)
        {
            return new ConnectedClient(toolData.Endpoint, null, toolData.Name);
        }

        /// <summary>
        /// Get all stored tools as ConnectedClient objects for UI display, sorted by name
        /// </summary>
        public IEnumerable<ConnectedClient> GetStoredToolsAsConnectedClients()
        {
            return _connectedTools.OrderBy(tool => tool.Name).Select(tool => ConvertToConnectedClient(tool));
        }

        /// <summary>
        /// Add a new tool
        /// </summary>
        public void AddTool(ConnectedClient client)
        {
            if (client.ClientName == McpConstants.UNKNOWN_CLIENT_NAME)
            {
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
        }
    }
}