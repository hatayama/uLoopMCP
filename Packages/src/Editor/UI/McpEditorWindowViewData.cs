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
        public readonly bool ShowSection;

        public ConnectedToolsData(IReadOnlyCollection<ConnectedClient> clients, bool showFoldout, bool isServerRunning, bool showReconnectingUI, bool showSection)
        {
            Clients = clients;
            ShowFoldout = showFoldout;
            IsServerRunning = isServerRunning;
            ShowReconnectingUI = showReconnectingUI;
            ShowSection = showSection;
        }
    }
    
    public record EditorConfigData
    {
        public readonly McpEditorType SelectedEditor;
        public readonly bool IsServerRunning;
        public readonly int CurrentPort;
        public readonly bool IsConfigured;
        public readonly bool HasPortMismatch;
        public readonly string ConfigurationError;
        public readonly bool IsUpdateNeeded;
        public readonly bool AddRepositoryRoot;
        public readonly bool SupportsRepositoryRootToggle;
        public readonly bool ShowRepositoryRootToggle;

        public EditorConfigData(McpEditorType selectedEditor, bool isServerRunning, int currentPort, bool isConfigured = false, bool hasPortMismatch = false, string configurationError = null, bool isUpdateNeeded = true, bool addRepositoryRoot = false, bool supportsRepositoryRootToggle = false, bool showRepositoryRootToggle = false)
        {
            SelectedEditor = selectedEditor;
            IsServerRunning = isServerRunning;
            CurrentPort = currentPort;
            IsConfigured = isConfigured;
            HasPortMismatch = hasPortMismatch;
            ConfigurationError = configurationError;
            IsUpdateNeeded = isUpdateNeeded;
            AddRepositoryRoot = addRepositoryRoot;
            SupportsRepositoryRootToggle = supportsRepositoryRootToggle;
            ShowRepositoryRootToggle = showRepositoryRootToggle;
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

    public record ConnectionModeData
    {
        public readonly ConnectionMode Mode;

        public ConnectionModeData(ConnectionMode mode)
        {
            Mode = mode;
        }
    }

    public record CliSetupData
    {
        public readonly bool IsCliInstalled;
        public readonly string CliVersion;
        public readonly string PackageVersion;
        public readonly bool NeedsUpdate;
        public readonly bool IsInstallingCli;
        public readonly bool IsChecking;
        public readonly bool IsClaudeSkillsInstalled;
        public readonly bool IsCodexSkillsInstalled;
        public readonly bool IsCursorSkillsInstalled;
        public readonly bool IsGeminiSkillsInstalled;
        public readonly bool IsWindsurfSkillsInstalled;
        public readonly SkillsTarget SelectedTarget;
        public readonly bool IsInstallingSkills;

        public CliSetupData(
            bool isCliInstalled,
            string cliVersion,
            string packageVersion,
            bool needsUpdate,
            bool isInstallingCli,
            bool isChecking,
            bool isClaudeSkillsInstalled,
            bool isCodexSkillsInstalled,
            bool isCursorSkillsInstalled,
            bool isGeminiSkillsInstalled,
            bool isWindsurfSkillsInstalled,
            SkillsTarget selectedTarget,
            bool isInstallingSkills)
        {
            IsCliInstalled = isCliInstalled;
            CliVersion = cliVersion;
            PackageVersion = packageVersion;
            NeedsUpdate = needsUpdate;
            IsInstallingCli = isInstallingCli;
            IsChecking = isChecking;
            IsClaudeSkillsInstalled = isClaudeSkillsInstalled;
            IsCodexSkillsInstalled = isCodexSkillsInstalled;
            IsCursorSkillsInstalled = isCursorSkillsInstalled;
            IsGeminiSkillsInstalled = isGeminiSkillsInstalled;
            IsWindsurfSkillsInstalled = isWindsurfSkillsInstalled;
            SelectedTarget = selectedTarget;
            IsInstallingSkills = isInstallingSkills;
        }
    }

}