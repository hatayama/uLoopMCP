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
    /// - McpEditorWindow: UI display control and LLM tool data management with backup/restore mechanism
    /// - McpSessionManager: Domain reload state management
    /// - McpServerController: Server lifecycle management
    /// </summary>
    public class DomainReloadReconnectionManager
    {
        private static DomainReloadReconnectionManager _instance;

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
    }
}