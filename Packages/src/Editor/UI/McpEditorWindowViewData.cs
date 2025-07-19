using UnityEngine;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Data structures for McpEditorWindow View rendering
    /// Related classes: McpEditorWindow, McpEditorWindowView
    /// </summary>
    
    public record ServerStatusData
    {
        public readonly bool IsRunning;
        public readonly int Port;
        public readonly string Status;
        public readonly Color StatusColor;

        public ServerStatusData(bool isRunning, int port, string status, Color statusColor)
        {
            IsRunning = isRunning;
            Port = port;
            Status = status;
            StatusColor = statusColor;
        }
    }
    
    public record ServerControlsData
    {
        public readonly int CustomPort;
        public readonly bool AutoStartServer;
        public readonly bool IsServerRunning;
        public readonly bool PortEditable;
        public readonly bool HasPortWarning;
        public readonly string PortWarningMessage;

        public ServerControlsData(int customPort, bool autoStartServer, bool isServerRunning, bool portEditable, bool hasPortWarning = false, string portWarningMessage = null)
        {
            CustomPort = customPort;
            AutoStartServer = autoStartServer;
            IsServerRunning = isServerRunning;
            PortEditable = portEditable;
            HasPortWarning = hasPortWarning;
            PortWarningMessage = portWarningMessage;
        }
    }
    
    public record ConnectedToolsData
    {
        public readonly IReadOnlyCollection<ConnectedClient> Clients;
        public readonly bool ShowFoldout;
        public readonly bool IsServerRunning;
        public readonly bool ShowReconnectingUI;

        public ConnectedToolsData(IReadOnlyCollection<ConnectedClient> clients, bool showFoldout, bool isServerRunning, bool showReconnectingUI)
        {
            Clients = clients;
            ShowFoldout = showFoldout;
            IsServerRunning = isServerRunning;
            ShowReconnectingUI = showReconnectingUI;
        }
    }
    
    public record EditorConfigData
    {
        public readonly McpEditorType SelectedEditor;
        public readonly bool ShowFoldout;
        public readonly bool IsServerRunning;
        public readonly int CurrentPort;
        public readonly bool IsConfigured;
        public readonly bool HasPortMismatch;
        public readonly string ConfigurationError;

        public EditorConfigData(McpEditorType selectedEditor, bool showFoldout, bool isServerRunning, int currentPort, bool isConfigured = false, bool hasPortMismatch = false, string configurationError = null)
        {
            SelectedEditor = selectedEditor;
            ShowFoldout = showFoldout;
            IsServerRunning = isServerRunning;
            CurrentPort = currentPort;
            IsConfigured = isConfigured;
            HasPortMismatch = hasPortMismatch;
            ConfigurationError = configurationError;
        }
    }

    /// <summary>
    /// Security settings section data for view rendering
    /// </summary>
    public record SecuritySettingsData
    {
        public readonly bool ShowSecuritySettings;
        public readonly bool EnableTestsExecution;
        public readonly bool AllowMenuItemExecution;
        public readonly bool AllowThirdPartyTools;

        public SecuritySettingsData(bool showSecuritySettings, bool enableTestsExecution, bool allowMenuItemExecution, bool allowThirdPartyTools)
        {
            ShowSecuritySettings = showSecuritySettings;
            EnableTestsExecution = enableTestsExecution;
            AllowMenuItemExecution = allowMenuItemExecution;
            AllowThirdPartyTools = allowThirdPartyTools;
        }
    }

} 