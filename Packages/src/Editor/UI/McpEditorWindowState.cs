using UnityEngine;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// State management objects for McpEditorWindow
    /// Implements State Object pattern as Model layer in MVP architecture
    /// Uses get-only properties for with expression compatibility
    /// Related classes:
    /// - McpEditorWindow: Presenter layer that uses these state objects
    /// - McpEditorWindowView: View layer for UI rendering
    /// - McpEditorModel: Model layer service for managing state transitions
    /// </summary>

    /// <summary>
    /// UI state data for McpEditorWindow
    /// </summary>
    public record UIState
    {
        public int CustomPort { get; }
        public bool AutoStartServer { get; }
        public bool ShowLLMToolSettings { get; }
        public bool ShowConnectedTools { get; }
        public McpEditorType SelectedEditorType { get; }
        public Vector2 MainScrollPosition { get; }
        public bool ShowSecuritySettings { get; }

        public UIState(
            int customPort = McpServerConfig.DEFAULT_PORT,
            bool autoStartServer = false,
            bool showLLMToolSettings = true,
            bool showConnectedTools = true,
            McpEditorType selectedEditorType = McpEditorType.Cursor,
            Vector2 mainScrollPosition = default,
            bool showSecuritySettings = false)
        {
            CustomPort = customPort;
            AutoStartServer = autoStartServer;
            ShowLLMToolSettings = showLLMToolSettings;
            ShowConnectedTools = showConnectedTools;
            SelectedEditorType = selectedEditorType;
            MainScrollPosition = mainScrollPosition;
            ShowSecuritySettings = showSecuritySettings;
        }
    }

    /// <summary>
    /// Runtime state data for McpEditorWindow
    /// Tracks dynamic state during editor window operation
    /// </summary>
    public record RuntimeState
    {
        public bool NeedsRepaint { get; }
        public bool IsPostCompileMode { get; }
        public bool LastServerRunning { get; }
        public int LastServerPort { get; }
        public int LastConnectedClientsCount { get; }
        public string LastClientsInfoHash { get; }

        public RuntimeState(
            bool needsRepaint = false,
            bool isPostCompileMode = false,
            bool lastServerRunning = false,
            int lastServerPort = 0,
            int lastConnectedClientsCount = 0,
            string lastClientsInfoHash = "")
        {
            NeedsRepaint = needsRepaint;
            IsPostCompileMode = isPostCompileMode;
            LastServerRunning = lastServerRunning;
            LastServerPort = lastServerPort;
            LastConnectedClientsCount = lastConnectedClientsCount;
            LastClientsInfoHash = lastClientsInfoHash;
        }
    }

} 