using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Model layer for McpEditorWindow in MVP architecture
    /// Handles state management and business logic using immutable state objects
    /// Related classes:
    /// - McpEditorWindowState: State objects managed by this model
    /// - McpEditorWindow: Presenter that uses this model
    /// - McpEditorWindowView: View layer for UI rendering
    /// - McpEditorSettings: Persistent settings storage
    /// </summary>
    public class McpEditorModel
    {
        public UIState UI { get; private set; }
        public RuntimeState Runtime { get; private set; }
#if ULOOPMCP_DEBUG
        public DebugState Debug { get; private set; }
#endif

        public McpEditorModel()
        {
            UI = new UIState();
            Runtime = new RuntimeState();
#if ULOOPMCP_DEBUG
            Debug = new DebugState();
#endif
        }

        /// <summary>
        /// Update UI state with new values
        /// </summary>
        /// <param name="updater">Function to update UI state</param>
        public void UpdateUIState(Func<UIState, UIState> updater)
        {
            UI = updater(UI);
        }

        /// <summary>
        /// Update runtime state with new values
        /// </summary>
        /// <param name="updater">Function to update runtime state</param>
        public void UpdateRuntimeState(Func<RuntimeState, RuntimeState> updater)
        {
            Runtime = updater(Runtime);
        }

#if ULOOPMCP_DEBUG
        /// <summary>
        /// Update debug state with new values
        /// </summary>
        /// <param name="updater">Function to update debug state</param>
        public void UpdateDebugState(Func<DebugState, DebugState> updater)
        {
            Debug = updater(Debug);
        }
#endif

        /// <summary>
        /// Load state from persistent settings
        /// </summary>
        public void LoadFromSettings()
        {
            McpEditorSettingsData settings = McpEditorSettings.GetSettings();
            
            UpdateUIState(ui => new UIState(
                customPort: settings.customPort,
                autoStartServer: settings.autoStartServer,
                showLLMToolSettings: ui.ShowLLMToolSettings,
                showConnectedTools: ui.ShowConnectedTools,
                selectedEditorType: ui.SelectedEditorType,
                mainScrollPosition: ui.MainScrollPosition,
                showSecuritySettings: settings.showSecuritySettings));

#if ULOOPMCP_DEBUG
            UpdateDebugState(debug => new DebugState(
                showDeveloperTools: settings.showDeveloperTools,
                enableMcpLogs: settings.enableMcpLogs,
                enableCommunicationLogs: settings.enableCommunicationLogs,
                enableDevelopmentMode: settings.enableDevelopmentMode,
                showCommunicationLogs: debug.ShowCommunicationLogs,
                communicationLogScrollPosition: debug.CommunicationLogScrollPosition,
                communicationLogHeight: debug.CommunicationLogHeight,
                requestScrollPositions: debug.RequestScrollPositions,
                responseScrollPositions: debug.ResponseScrollPositions));

            // Synchronize McpLogger settings
            McpLogger.EnableDebugLog = Debug.EnableMcpLogs;
#endif
        }

        /// <summary>
        /// Save current UI state to persistent settings
        /// </summary>
        public void SaveToSettings()
        {
            McpEditorSettings.SetCustomPort(UI.CustomPort);
            McpEditorSettings.SetAutoStartServer(UI.AutoStartServer);

#if ULOOPMCP_DEBUG
            McpEditorSettings.SetShowDeveloperTools(Debug.ShowDeveloperTools);
            McpEditorSettings.SetEnableMcpLogs(Debug.EnableMcpLogs);
            McpEditorSettings.SetEnableCommunicationLogs(Debug.EnableCommunicationLogs);
            McpEditorSettings.SetEnableDevelopmentMode(Debug.EnableDevelopmentMode);
#endif
        }

        /// <summary>
        /// Load state from Unity SessionState
        /// </summary>
        public void LoadFromSessionState()
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            McpEditorType selectedEditor = sessionManager.SelectedEditorType;

            UpdateUIState(ui => new UIState(
                customPort: ui.CustomPort,
                autoStartServer: ui.AutoStartServer,
                showLLMToolSettings: ui.ShowLLMToolSettings,
                showConnectedTools: ui.ShowConnectedTools,
                selectedEditorType: selectedEditor,
                mainScrollPosition: ui.MainScrollPosition,
                showSecuritySettings: ui.ShowSecuritySettings));

#if ULOOPMCP_DEBUG
            float communicationLogHeight = sessionManager.CommunicationLogHeight;

            UpdateDebugState(debug => new DebugState(
                showDeveloperTools: debug.ShowDeveloperTools,
                enableMcpLogs: debug.EnableMcpLogs,
                enableCommunicationLogs: debug.EnableCommunicationLogs,
                enableDevelopmentMode: debug.EnableDevelopmentMode,
                showCommunicationLogs: debug.ShowCommunicationLogs,
                communicationLogScrollPosition: debug.CommunicationLogScrollPosition,
                communicationLogHeight: communicationLogHeight,
                requestScrollPositions: debug.RequestScrollPositions,
                responseScrollPositions: debug.ResponseScrollPositions));
#endif
        }

        /// <summary>
        /// Save current state to Unity SessionState
        /// </summary>
        public void SaveToSessionState()
        {
            McpSessionManager sessionManager = McpSessionManager.instance;
            sessionManager.SelectedEditorType = UI.SelectedEditorType;

#if ULOOPMCP_DEBUG
            sessionManager.CommunicationLogHeight = Debug.CommunicationLogHeight;
#endif
        }

        /// <summary>
        /// Initialize post-compile mode
        /// </summary>
        public void EnablePostCompileMode()
        {
            UpdateRuntimeState(runtime => new RuntimeState(
                isPostCompileMode: true,
                needsRepaint: true,
                lastServerRunning: runtime.LastServerRunning,
                lastServerPort: runtime.LastServerPort,
                lastConnectedClientsCount: runtime.LastConnectedClientsCount,
                lastClientsInfoHash: runtime.LastClientsInfoHash));
        }

        /// <summary>
        /// Exit post-compile mode
        /// </summary>
        public void DisablePostCompileMode()
        {
            UpdateRuntimeState(runtime => new RuntimeState(
                isPostCompileMode: false,
                needsRepaint: runtime.NeedsRepaint,
                lastServerRunning: runtime.LastServerRunning,
                lastServerPort: runtime.LastServerPort,
                lastConnectedClientsCount: runtime.LastConnectedClientsCount,
                lastClientsInfoHash: runtime.LastClientsInfoHash));
        }

        /// <summary>
        /// Mark that UI repaint is needed
        /// </summary>
        public void RequestRepaint()
        {
            UpdateRuntimeState(runtime => new RuntimeState(
                isPostCompileMode: runtime.IsPostCompileMode,
                needsRepaint: true,
                lastServerRunning: runtime.LastServerRunning,
                lastServerPort: runtime.LastServerPort,
                lastConnectedClientsCount: runtime.LastConnectedClientsCount,
                lastClientsInfoHash: runtime.LastClientsInfoHash));
        }

        /// <summary>
        /// Clear repaint request
        /// </summary>
        public void ClearRepaintRequest()
        {
            UpdateRuntimeState(runtime => new RuntimeState(
                isPostCompileMode: runtime.IsPostCompileMode,
                needsRepaint: false,
                lastServerRunning: runtime.LastServerRunning,
                lastServerPort: runtime.LastServerPort,
                lastConnectedClientsCount: runtime.LastConnectedClientsCount,
                lastClientsInfoHash: runtime.LastClientsInfoHash));
        }

        /// <summary>
        /// Update server state tracking for change detection
        /// </summary>
        public void UpdateServerStateTracking(bool isRunning, int port, int clientCount, string clientsHash)
        {
            UpdateRuntimeState(runtime => new RuntimeState(
                isPostCompileMode: runtime.IsPostCompileMode,
                needsRepaint: runtime.NeedsRepaint,
                lastServerRunning: isRunning,
                lastServerPort: port,
                lastConnectedClientsCount: clientCount,
                lastClientsInfoHash: clientsHash));
        }

        // UIState-specific update methods with persistence

        /// <summary>
        /// Update AutoStartServer setting with persistence
        /// </summary>
        public void UpdateAutoStartServer(bool autoStart)
        {
            UpdateUIState(ui => new UIState(
                customPort: ui.CustomPort,
                autoStartServer: autoStart,
                showLLMToolSettings: ui.ShowLLMToolSettings,
                showConnectedTools: ui.ShowConnectedTools,
                selectedEditorType: ui.SelectedEditorType,
                mainScrollPosition: ui.MainScrollPosition,
                showSecuritySettings: ui.ShowSecuritySettings));
            McpEditorSettings.SetAutoStartServer(autoStart);
        }

        /// <summary>
        /// Update CustomPort setting with persistence
        /// </summary>
        public void UpdateCustomPort(int port)
        {
            UpdateUIState(ui => new UIState(
                customPort: port,
                autoStartServer: ui.AutoStartServer,
                showLLMToolSettings: ui.ShowLLMToolSettings,
                showConnectedTools: ui.ShowConnectedTools,
                selectedEditorType: ui.SelectedEditorType,
                mainScrollPosition: ui.MainScrollPosition,
                showSecuritySettings: ui.ShowSecuritySettings));
            McpEditorSettings.SetCustomPort(port);
        }

        /// <summary>
        /// Update ShowConnectedTools setting
        /// </summary>
        public void UpdateShowConnectedTools(bool show)
        {
            UpdateUIState(ui => new UIState(
                customPort: ui.CustomPort,
                autoStartServer: ui.AutoStartServer,
                showLLMToolSettings: ui.ShowLLMToolSettings,
                showConnectedTools: show,
                selectedEditorType: ui.SelectedEditorType,
                mainScrollPosition: ui.MainScrollPosition,
                showSecuritySettings: ui.ShowSecuritySettings));
        }

        /// <summary>
        /// Update ShowLLMToolSettings setting
        /// </summary>
        public void UpdateShowLLMToolSettings(bool show)
        {
            UpdateUIState(ui => new UIState(
                customPort: ui.CustomPort,
                autoStartServer: ui.AutoStartServer,
                showLLMToolSettings: show,
                showConnectedTools: ui.ShowConnectedTools,
                selectedEditorType: ui.SelectedEditorType,
                mainScrollPosition: ui.MainScrollPosition,
                showSecuritySettings: ui.ShowSecuritySettings));
        }

        /// <summary>
        /// Update SelectedEditorType setting with persistence
        /// </summary>
        public void UpdateSelectedEditorType(McpEditorType type)
        {
            UpdateUIState(ui => new UIState(
                customPort: ui.CustomPort,
                autoStartServer: ui.AutoStartServer,
                showLLMToolSettings: ui.ShowLLMToolSettings,
                showConnectedTools: ui.ShowConnectedTools,
                selectedEditorType: type,
                mainScrollPosition: ui.MainScrollPosition,
                showSecuritySettings: ui.ShowSecuritySettings));
            McpSessionManager.instance.SelectedEditorType = type;
        }

        /// <summary>
        /// Update MainScrollPosition setting
        /// </summary>
        public void UpdateMainScrollPosition(Vector2 position)
        {
            UpdateUIState(ui => new UIState(
                customPort: ui.CustomPort,
                autoStartServer: ui.AutoStartServer,
                showLLMToolSettings: ui.ShowLLMToolSettings,
                showConnectedTools: ui.ShowConnectedTools,
                selectedEditorType: ui.SelectedEditorType,
                mainScrollPosition: position,
                showSecuritySettings: ui.ShowSecuritySettings));
        }

        /// <summary>
        /// Update ShowSecuritySettings setting with persistence
        /// </summary>
        public void UpdateShowSecuritySettings(bool show)
        {
            UpdateUIState(ui => new UIState(
                customPort: ui.CustomPort,
                autoStartServer: ui.AutoStartServer,
                showLLMToolSettings: ui.ShowLLMToolSettings,
                showConnectedTools: ui.ShowConnectedTools,
                selectedEditorType: ui.SelectedEditorType,
                mainScrollPosition: ui.MainScrollPosition,
                showSecuritySettings: show));
            McpEditorSettings.SetShowSecuritySettings(show);
        }

        /// <summary>
        /// Update EnableTestsExecution setting with persistence
        /// </summary>
        public void UpdateEnableTestsExecution(bool enable)
        {
            McpEditorSettings.SetEnableTestsExecution(enable);
        }

        /// <summary>
        /// Update AllowMenuItemExecution setting with persistence
        /// </summary>
        public void UpdateAllowMenuItemExecution(bool allow)
        {
            McpEditorSettings.SetAllowMenuItemExecution(allow);
        }

        /// <summary>
        /// Update AllowThirdPartyTools setting with persistence
        /// </summary>
        public void UpdateAllowThirdPartyTools(bool allow)
        {
            McpEditorSettings.SetAllowThirdPartyTools(allow);
        }


#if ULOOPMCP_DEBUG
        /// <summary>
        /// Update communication log scroll position
        /// </summary>
        public void UpdateCommunicationLogScrollPosition(Vector2 scrollPosition)
        {
            UpdateDebugState(debug => new DebugState(
                showDeveloperTools: debug.ShowDeveloperTools,
                enableCommunicationLogs: debug.EnableCommunicationLogs,
                showCommunicationLogs: debug.ShowCommunicationLogs,
                enableMcpLogs: debug.EnableMcpLogs,
                enableDevelopmentMode: debug.EnableDevelopmentMode,
                communicationLogScrollPosition: scrollPosition,
                communicationLogHeight: debug.CommunicationLogHeight,
                requestScrollPositions: debug.RequestScrollPositions,
                responseScrollPositions: debug.ResponseScrollPositions));
        }

        /// <summary>
        /// Update communication log height
        /// </summary>
        public void UpdateCommunicationLogHeight(float height)
        {
            UpdateDebugState(debug => new DebugState(
                showDeveloperTools: debug.ShowDeveloperTools,
                enableCommunicationLogs: debug.EnableCommunicationLogs,
                showCommunicationLogs: debug.ShowCommunicationLogs,
                enableMcpLogs: debug.EnableMcpLogs,
                enableDevelopmentMode: debug.EnableDevelopmentMode,
                communicationLogScrollPosition: debug.CommunicationLogScrollPosition,
                communicationLogHeight: height,
                requestScrollPositions: debug.RequestScrollPositions,
                responseScrollPositions: debug.ResponseScrollPositions));
        }

        /// <summary>
        /// Update request scroll position for specific log ID
        /// </summary>
        public void UpdateRequestScrollPosition(string logId, Vector2 scrollPosition)
        {
            Dictionary<string, Vector2> newRequestScrollPositions = new(Debug.RequestScrollPositions);
            newRequestScrollPositions[logId] = scrollPosition;

            UpdateDebugState(debug => new DebugState(
                showDeveloperTools: debug.ShowDeveloperTools,
                enableCommunicationLogs: debug.EnableCommunicationLogs,
                showCommunicationLogs: debug.ShowCommunicationLogs,
                enableMcpLogs: debug.EnableMcpLogs,
                enableDevelopmentMode: debug.EnableDevelopmentMode,
                communicationLogScrollPosition: debug.CommunicationLogScrollPosition,
                communicationLogHeight: debug.CommunicationLogHeight,
                requestScrollPositions: newRequestScrollPositions,
                responseScrollPositions: debug.ResponseScrollPositions));
        }

        /// <summary>
        /// Update response scroll position for specific log ID
        /// </summary>
        public void UpdateResponseScrollPosition(string logId, Vector2 scrollPosition)
        {
            Dictionary<string, Vector2> newResponseScrollPositions = new(Debug.ResponseScrollPositions);
            newResponseScrollPositions[logId] = scrollPosition;

            UpdateDebugState(debug => new DebugState(
                showDeveloperTools: debug.ShowDeveloperTools,
                enableCommunicationLogs: debug.EnableCommunicationLogs,
                showCommunicationLogs: debug.ShowCommunicationLogs,
                enableMcpLogs: debug.EnableMcpLogs,
                enableDevelopmentMode: debug.EnableDevelopmentMode,
                communicationLogScrollPosition: debug.CommunicationLogScrollPosition,
                communicationLogHeight: debug.CommunicationLogHeight,
                requestScrollPositions: debug.RequestScrollPositions,
                responseScrollPositions: newResponseScrollPositions));
        }

        // DebugState-specific update methods with persistence

        /// <summary>
        /// Update ShowDeveloperTools setting with persistence
        /// </summary>
        public void UpdateShowDeveloperTools(bool show)
        {
            UpdateDebugState(debug => new DebugState(
                showDeveloperTools: show,
                enableCommunicationLogs: debug.EnableCommunicationLogs,
                showCommunicationLogs: debug.ShowCommunicationLogs,
                enableMcpLogs: debug.EnableMcpLogs,
                enableDevelopmentMode: debug.EnableDevelopmentMode,
                communicationLogScrollPosition: debug.CommunicationLogScrollPosition,
                communicationLogHeight: debug.CommunicationLogHeight,
                requestScrollPositions: debug.RequestScrollPositions,
                responseScrollPositions: debug.ResponseScrollPositions));
            McpEditorSettings.SetShowDeveloperTools(show);
        }

        /// <summary>
        /// Update EnableDevelopmentMode setting with persistence
        /// </summary>
        public void UpdateEnableDevelopmentMode(bool enable)
        {
            UpdateDebugState(debug => new DebugState(
                showDeveloperTools: debug.ShowDeveloperTools,
                enableCommunicationLogs: debug.EnableCommunicationLogs,
                showCommunicationLogs: debug.ShowCommunicationLogs,
                enableMcpLogs: debug.EnableMcpLogs,
                enableDevelopmentMode: enable,
                communicationLogScrollPosition: debug.CommunicationLogScrollPosition,
                communicationLogHeight: debug.CommunicationLogHeight,
                requestScrollPositions: debug.RequestScrollPositions,
                responseScrollPositions: debug.ResponseScrollPositions));
            McpEditorSettings.SetEnableDevelopmentMode(enable);
        }

        /// <summary>
        /// Update EnableMcpLogs setting with persistence
        /// </summary>
        public void UpdateEnableMcpLogs(bool enable)
        {
            UpdateDebugState(debug => new DebugState(
                showDeveloperTools: debug.ShowDeveloperTools,
                enableCommunicationLogs: debug.EnableCommunicationLogs,
                showCommunicationLogs: debug.ShowCommunicationLogs,
                enableMcpLogs: enable,
                enableDevelopmentMode: debug.EnableDevelopmentMode,
                communicationLogScrollPosition: debug.CommunicationLogScrollPosition,
                communicationLogHeight: debug.CommunicationLogHeight,
                requestScrollPositions: debug.RequestScrollPositions,
                responseScrollPositions: debug.ResponseScrollPositions));
            McpEditorSettings.SetEnableMcpLogs(enable);
            
            // Synchronize McpLogger settings
            McpLogger.EnableDebugLog = enable;
        }

        /// <summary>
        /// Update EnableCommunicationLogs setting with persistence and log clearing
        /// </summary>
        public void UpdateEnableCommunicationLogs(bool enable)
        {
            UpdateDebugState(debug => new DebugState(
                showDeveloperTools: debug.ShowDeveloperTools,
                enableCommunicationLogs: enable,
                showCommunicationLogs: debug.ShowCommunicationLogs,
                enableMcpLogs: debug.EnableMcpLogs,
                enableDevelopmentMode: debug.EnableDevelopmentMode,
                communicationLogScrollPosition: debug.CommunicationLogScrollPosition,
                communicationLogHeight: debug.CommunicationLogHeight,
                requestScrollPositions: debug.RequestScrollPositions,
                responseScrollPositions: debug.ResponseScrollPositions));
            McpEditorSettings.SetEnableCommunicationLogs(enable);
            if (!enable)
            {
                McpCommunicationLogger.ClearLogs();
            }
        }

        /// <summary>
        /// Update ShowCommunicationLogs setting (foldout state)
        /// </summary>
        public void UpdateShowCommunicationLogs(bool show)
        {
            UpdateDebugState(debug => new DebugState(
                showDeveloperTools: debug.ShowDeveloperTools,
                enableCommunicationLogs: debug.EnableCommunicationLogs,
                showCommunicationLogs: show,
                enableMcpLogs: debug.EnableMcpLogs,
                enableDevelopmentMode: debug.EnableDevelopmentMode,
                communicationLogScrollPosition: debug.CommunicationLogScrollPosition,
                communicationLogHeight: debug.CommunicationLogHeight,
                requestScrollPositions: debug.RequestScrollPositions,
                responseScrollPositions: debug.ResponseScrollPositions));
        }
#endif
    }
} 