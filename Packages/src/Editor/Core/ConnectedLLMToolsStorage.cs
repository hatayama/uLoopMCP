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
    public record ConnectedLLMToolData
    {
        public readonly string Name;
        public readonly string Endpoint;
        public readonly DateTime ConnectedAt;

        public ConnectedLLMToolData(string name, string endpoint, DateTime connectedAt)
        {
            Name = name;
            Endpoint = endpoint;
            ConnectedAt = connectedAt;
        }
    }

    /// <summary>
    /// Class that serializes Connected LLM Tools information and stores it in a Scriptable Singleton
    /// Saves tool information when Unity connects and deletes it when disconnected
    /// </summary>
    [System.Serializable]
    public class ConnectedLLMToolsStorage : ScriptableObject
    {
        private static ConnectedLLMToolsStorage _instance;
        
        [SerializeField]
        private List<ConnectedLLMToolData> _connectedTools = new();

        public static ConnectedLLMToolsStorage Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = CreateInstance<ConnectedLLMToolsStorage>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Get read-only list of connected tools
        /// </summary>
        public IReadOnlyList<ConnectedLLMToolData> ConnectedTools => _connectedTools.AsReadOnly();

        /// <summary>
        /// Get the number of connected tools
        /// </summary>
        public int Count => _connectedTools.Count;

        /// <summary>
        /// Check if a tool with the specified name is connected
        /// </summary>
        public bool HasTool(string name)
        {
            return _connectedTools.Any(tool => tool.Name == name);
        }

        /// <summary>
        /// Get a tool with the specified name
        /// </summary>
        public ConnectedLLMToolData GetTool(string name)
        {
            return _connectedTools.FirstOrDefault(tool => tool.Name == name);
        }

        /// <summary>
        /// Save tool information when Unity connects
        /// </summary>
        public void SaveConnectedTools(IEnumerable<ConnectedClient> clients)
        {
            _connectedTools.Clear();
            
            foreach (ConnectedClient client in clients)
            {
                if (client.ClientName != McpConstants.UNKNOWN_CLIENT_NAME)
                {
                    ConnectedLLMToolData toolData = new(
                        client.ClientName, 
                        client.Endpoint, 
                        client.ConnectedAt
                    );
                    _connectedTools.Add(toolData);
                }
            }
            
            EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Delete tool information when Unity disconnects
        /// </summary>
        public void ClearConnectedTools()
        {
            _connectedTools.Clear();
            EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Remove a specific tool
        /// </summary>
        public void RemoveTool(string name)
        {
            int removedCount = _connectedTools.RemoveAll(tool => tool.Name == name);
            
            if (removedCount > 0)
            {
                EditorUtility.SetDirty(this);
            }
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
            
            EditorUtility.SetDirty(this);
        }
    }
}