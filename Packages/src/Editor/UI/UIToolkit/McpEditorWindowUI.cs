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

        private ServerStatusSection _serverStatusSection;
        private ServerControlsSection _serverControlsSection;
        private ConnectedToolsSection _connectedToolsSection;
        private EditorConfigSection _editorConfigSection;
        private SecuritySettingsSection _securitySettingsSection;

        public event Action OnToggleServer;
        public event Action<bool> OnAutoStartChanged;
        public event Action<int> OnPortChanged;
        public event Action<bool> OnConnectedToolsFoldoutChanged;
        public event Action<McpEditorType> OnEditorTypeChanged;
        public event Action<bool> OnLLMSettingsFoldoutChanged;
        public event Action<bool> OnRepositoryRootChanged;
        public event Action OnConfigureClicked;
        public event Action OnOpenSettingsClicked;
        public event Action<bool> OnSecurityFoldoutChanged;
        public event Action<bool> OnEnableTestsChanged;
        public event Action<bool> OnAllowMenuChanged;
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
            _serverStatusSection = new ServerStatusSection(_root);

            _serverControlsSection = new ServerControlsSection(_root);
            _serverControlsSection.OnToggleServer += () => OnToggleServer?.Invoke();
            _serverControlsSection.OnAutoStartChanged += value => OnAutoStartChanged?.Invoke(value);
            _serverControlsSection.OnPortChanged += value => OnPortChanged?.Invoke(value);

            _connectedToolsSection = new ConnectedToolsSection(_root);
            _connectedToolsSection.OnFoldoutChanged += value => OnConnectedToolsFoldoutChanged?.Invoke(value);

            _editorConfigSection = new EditorConfigSection(_root);
            _editorConfigSection.OnEditorTypeChanged += value => OnEditorTypeChanged?.Invoke(value);
            _editorConfigSection.OnFoldoutChanged += value => OnLLMSettingsFoldoutChanged?.Invoke(value);
            _editorConfigSection.OnRepositoryRootChanged += value => OnRepositoryRootChanged?.Invoke(value);
            _editorConfigSection.OnConfigureClicked += () => OnConfigureClicked?.Invoke();
            _editorConfigSection.OnOpenSettingsClicked += () => OnOpenSettingsClicked?.Invoke();

            _securitySettingsSection = new SecuritySettingsSection(_root);
            _securitySettingsSection.OnFoldoutChanged += value => OnSecurityFoldoutChanged?.Invoke(value);
            _securitySettingsSection.OnEnableTestsChanged += value => OnEnableTestsChanged?.Invoke(value);
            _securitySettingsSection.OnAllowMenuChanged += value => OnAllowMenuChanged?.Invoke(value);
            _securitySettingsSection.OnAllowThirdPartyChanged += value => OnAllowThirdPartyChanged?.Invoke(value);
            _securitySettingsSection.OnSecurityLevelChanged += value => OnSecurityLevelChanged?.Invoke(value);
        }

        public void UpdateServerStatus(ServerStatusData data)
        {
            _serverStatusSection?.Update(data);
        }

        public void UpdateServerControls(ServerControlsData data)
        {
            _serverControlsSection?.Update(data);
        }

        public void UpdateConnectedTools(ConnectedToolsData data)
        {
            _connectedToolsSection?.Update(data);
        }

        public void UpdateEditorConfig(EditorConfigData data)
        {
            _editorConfigSection?.Update(data);
        }

        public void UpdateSecuritySettings(SecuritySettingsData data)
        {
            _securitySettingsSection?.Update(data);
        }

        public void Dispose()
        {
            _serverStatusSection = null;
            _serverControlsSection = null;
            _connectedToolsSection = null;
            _editorConfigSection = null;
            _securitySettingsSection = null;
        }
    }
}
