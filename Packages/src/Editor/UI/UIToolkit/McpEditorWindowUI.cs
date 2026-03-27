using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// View layer for McpEditorWindow in MVP architecture.
    /// Owns UI sections and forwards user interactions to presenter via events.
    /// Related: McpEditorWindow (presenter), McpEditorModel (model)
    /// </summary>
    public class McpEditorWindowUI : IDisposable
    {
        private const string UXML_RELATIVE_PATH = "Editor/UI/UIToolkit/McpEditorWindow.uxml";
        private const string USS_RELATIVE_PATH = "Editor/UI/UIToolkit/McpEditorWindow.uss";

        private readonly VisualElement _root;

        private ConnectionModeSection _connectionModeSection;
        private CliSetupSection _cliSetupSection;
        private ConnectedToolsSection _connectedToolsSection;
        private EditorConfigSection _editorConfigSection;
        private ToolSettingsSection _toolSettingsSection;

        private VisualElement _cliContent;
        private VisualElement _mcpContent;

        public event Action<ConnectionMode> OnConnectionModeChanged;
        public event Action OnRefreshCliVersion;
        public event Action OnInstallCli;
        public event Action OnInstallSkills;
        public event Action<SkillsTarget> OnSkillsTargetChanged;
        public event Action<bool> OnConfigurationFoldoutChanged;
        public event Action<bool> OnConnectedToolsFoldoutChanged;
        public event Action<McpEditorType> OnEditorTypeChanged;
        public event Action<bool> OnRepositoryRootChanged;
        public event Action OnConfigureClicked;
        public event Action OnDeleteConfigClicked;
        public event Action OnOpenSettingsClicked;
        public event Action<bool> OnToolSettingsFoldoutChanged;
        public event Action<string, bool> OnToolToggled;
        public event Action<bool> OnAllowThirdPartyChanged;
        public event Action<DynamicCodeSecurityLevel> OnSecurityLevelChanged;

        public McpEditorWindowUI(VisualElement root)
        {
            _root = root;
            LoadLayout();
            ApplyDebugStyle();
            InitializeSections();
        }

        private void ApplyDebugStyle()
        {
#if ULOOPMCP_DEBUG
            VisualElement mainContainer = _root.Q<VisualElement>("main-scroll-view");
            mainContainer?.AddToClassList("mcp-main-container--debug");
#endif
        }

        private void LoadLayout()
        {
            string uxmlPath = $"{McpConstants.PackageAssetPath}/{UXML_RELATIVE_PATH}";
            string ussPath = $"{McpConstants.PackageAssetPath}/{USS_RELATIVE_PATH}";

            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (visualTree == null)
            {
                Debug.LogError($"Failed to load UXML from: {uxmlPath}");
                return;
            }

            visualTree.CloneTree(_root);

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (styleSheet == null)
            {
                Debug.LogError($"Failed to load USS from: {ussPath}");
                return;
            }

            _root.styleSheets.Add(styleSheet);
        }

        private void InitializeSections()
        {
            _connectionModeSection = new ConnectionModeSection(_root);
            _connectionModeSection.OnModeChanged += mode => OnConnectionModeChanged?.Invoke(mode);
            _connectionModeSection.OnFoldoutChanged += value => OnConfigurationFoldoutChanged?.Invoke(value);

            _cliSetupSection = new CliSetupSection(_root);
            _cliSetupSection.SetupBindings();
            _cliSetupSection.OnRefreshCliVersion += () => OnRefreshCliVersion?.Invoke();
            _cliSetupSection.OnInstallCli += () => OnInstallCli?.Invoke();
            _cliSetupSection.OnInstallSkills += () => OnInstallSkills?.Invoke();
            _cliSetupSection.OnSkillsTargetChanged += value => OnSkillsTargetChanged?.Invoke(value);

            _cliContent = _root.Q<VisualElement>("cli-content");
            _mcpContent = _root.Q<VisualElement>("mcp-content");

            _connectedToolsSection = new ConnectedToolsSection(_root);
            _connectedToolsSection.OnFoldoutChanged += value => OnConnectedToolsFoldoutChanged?.Invoke(value);

            _editorConfigSection = new EditorConfigSection(_root);
            _editorConfigSection.OnEditorTypeChanged += value => OnEditorTypeChanged?.Invoke(value);
            _editorConfigSection.OnRepositoryRootChanged += value => OnRepositoryRootChanged?.Invoke(value);
            _editorConfigSection.OnConfigureClicked += () => OnConfigureClicked?.Invoke();
            _editorConfigSection.OnDeleteConfigClicked += () => OnDeleteConfigClicked?.Invoke();
            _editorConfigSection.OnOpenSettingsClicked += () => OnOpenSettingsClicked?.Invoke();

            _toolSettingsSection = new ToolSettingsSection(_root);
            _toolSettingsSection.OnFoldoutChanged += value => OnToolSettingsFoldoutChanged?.Invoke(value);
            _toolSettingsSection.OnToolToggled += (toolName, enabled) => OnToolToggled?.Invoke(toolName, enabled);
            _toolSettingsSection.OnAllowThirdPartyChanged += value => OnAllowThirdPartyChanged?.Invoke(value);
            _toolSettingsSection.OnSecurityLevelChanged += value => OnSecurityLevelChanged?.Invoke(value);
        }

        public void UpdateConnectedTools(ConnectedToolsData data)
        {
            _connectedToolsSection?.Update(data);
        }

        public void UpdateEditorConfig(EditorConfigData data)
        {
            _editorConfigSection?.Update(data);
        }

        public void UpdateToolSettings(ToolSettingsSectionData data)
        {
            _toolSettingsSection?.Update(data);
        }

        public void UpdateSingleToolToggle(string toolName, bool enabled)
        {
            _toolSettingsSection?.UpdateSingleToggle(toolName, enabled);
        }

        public void UpdateConnectionMode(ConnectionModeData data)
        {
            _connectionModeSection?.Update(data);
        }

        public void UpdateConfigurationFoldout(bool show)
        {
            _connectionModeSection?.UpdateFoldout(show);
        }

        public void UpdateCliSetup(CliSetupData data)
        {
            _cliSetupSection?.Update(data);
        }

        public void UpdateSectionVisibility(ConnectionMode mode)
        {
            bool isMcp = mode == ConnectionMode.MCP;

            ViewDataBinder.SetVisible(_mcpContent, isMcp);
            ViewDataBinder.SetVisible(_cliContent, !isMcp);
        }

        public void Dispose()
        {
            _connectionModeSection = null;
            _cliSetupSection = null;
            _connectedToolsSection = null;
            _editorConfigSection = null;
            _toolSettingsSection = null;
        }
    }
}
