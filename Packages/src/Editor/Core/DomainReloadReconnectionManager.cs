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
        private const float GRACE_PERIOD_SECONDS = 10f;

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
        /// Get remaining seconds in grace period
        /// </summary>
        public float RemainingGracePeriodSeconds
        {
            get
            {
                if (!_isInGracePeriod)
                {
                    return 0f;
                }

                float elapsedSeconds = (float)(DateTime.Now - _domainReloadCompletedAt).TotalSeconds;
                return Mathf.Max(0f, GRACE_PERIOD_SECONDS - elapsedSeconds);
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
            
            EditorApplication.update += CheckGracePeriodExpiration;
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
            EditorApplication.update -= CheckGracePeriodExpiration;

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

        /// <summary>
        /// Check if grace period has expired (called from EditorApplication.update)
        /// </summary>
        private void CheckGracePeriodExpiration()
        {
            if (!IsInGracePeriod)
            {
                // Grace period ended, this will trigger cleanup
                return;
            }
        }

        /// <summary>
        /// Force end grace period (for testing or manual control)
        /// </summary>
        public void ForceEndGracePeriod()
        {
            EndGracePeriod();
        }

        /// <summary>
        /// Get list of tools that haven't reconnected yet
        /// </summary>
        public IEnumerable<string> GetPendingReconnectionTools()
        {
            ConnectedLLMToolsStorage storage = ConnectedLLMToolsStorage.instance;
            return storage.ConnectedTools
                .Where(tool => !_reconnectedToolNames.Contains(tool.Name))
                .Select(tool => tool.Name);
        }

        /// <summary>
        /// Reset manager state (for testing)
        /// </summary>
        public void Reset()
        {
            _isInGracePeriod = false;
            _reconnectedToolNames.Clear();
            EditorApplication.update -= CheckGracePeriodExpiration;
        }
    }
}