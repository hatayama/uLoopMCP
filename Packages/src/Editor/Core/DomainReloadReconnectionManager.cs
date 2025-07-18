using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Design documentation reference: /CLAUDE.md - Domain reload reconnection management
    /// Related classes:
    /// - ConnectedLLMToolsStorage: Stores LLM tool data during domain reload
    /// - McpEditorWindow: UI display control during reconnection
    /// - McpSessionManager: Domain reload state management
    /// - McpServerController: Server lifecycle management
    /// </summary>
    public class DomainReloadReconnectionManager
    {
        private static DomainReloadReconnectionManager _instance;
        private readonly HashSet<string> _reconnectedToolNames = new();
        private DateTime _domainReloadCompletedAt;
        private bool _isInGracePeriod = false;
        private const float GRACE_PERIOD_SECONDS = 3f;

        public static DomainReloadReconnectionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DomainReloadReconnectionManager();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Check if currently in grace period after domain reload
        /// </summary>
        public bool IsInGracePeriod
        {
            get
            {
                if (!_isInGracePeriod)
                {
                    return false;
                }

                float elapsedSeconds = (float)(DateTime.Now - _domainReloadCompletedAt).TotalSeconds;
                if (elapsedSeconds >= GRACE_PERIOD_SECONDS)
                {
                    EndGracePeriod();
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Start grace period after domain reload completion
        /// </summary>
        public void StartGracePeriod()
        {
            _domainReloadCompletedAt = DateTime.Now;
            _isInGracePeriod = true;
            _reconnectedToolNames.Clear();
        }

        /// <summary>
        /// End grace period and clean up disconnected tools
        /// </summary>
        public void EndGracePeriod()
        {
            if (!_isInGracePeriod)
            {
                return;
            }

            _isInGracePeriod = false;

            // Remove tools that didn't reconnect during grace period
            ConnectedLLMToolsStorage storage = ConnectedLLMToolsStorage.instance;
            List<string> toolsToRemove = new();

            foreach (ConnectedLLMToolData tool in storage.ConnectedTools)
            {
                if (!_reconnectedToolNames.Contains(tool.Name))
                {
                    toolsToRemove.Add(tool.Name);
                }
            }

            foreach (string toolName in toolsToRemove)
            {
                storage.RemoveTool(toolName);
            }

            _reconnectedToolNames.Clear();
        }

        /// <summary>
        /// Register a tool as reconnected during grace period
        /// </summary>
        public void RegisterReconnectedTool(string toolName)
        {
            if (_isInGracePeriod)
            {
                _reconnectedToolNames.Add(toolName);
            }
        }

    }
}