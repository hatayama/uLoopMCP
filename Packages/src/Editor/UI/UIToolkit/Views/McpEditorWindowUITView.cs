using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// UI Toolkit implementation of McpEditorWindow View
    /// Design document: ARCHITECTURE.md - UI Toolkit migration pattern
    /// Related classes:
    /// - McpEditorWindow: Presenter layer that uses this view
    /// - ServerStatusView, ServerControlsView, etc.: Component views
    /// </summary>
    public class McpEditorWindowUITView
    {
        private VisualElement _root;
        private ScrollView _scrollView;
        
        // Section containers
        private VisualElement _serverControlsContainer;
        private Foldout _connectedToolsFoldout;
        private Foldout _editorConfigFoldout;
        private Foldout _securitySettingsFoldout;
        
        // View components
        private ServerControlsView _serverControlsView;
        private ConnectedToolsView _connectedToolsView;
        private EditorConfigView _editorConfigView;
        private SecuritySettingsView _securitySettingsView;
        
        public VisualElement Root => _root;
        
        public void Initialize()
        {
            // Load UXML from Resources (USSはUXML内で自動的に読み込まれる)
            VisualTreeAsset visualTree = Resources.Load<VisualTreeAsset>("McpEditorWindow");
            
            if (visualTree == null)
            {
                Debug.LogError("Failed to load McpEditorWindow.uxml from Resources folder");
                return;
            }

            _root = visualTree.CloneTree();
            if (_root == null)
            {
                Debug.LogError("Failed to clone visual tree");
                return;
            }
            
            // Query containers
            _scrollView = _root.Q<ScrollView>(McpUIToolkitCommonConstants.ELEMENT_MAIN_SCROLL_VIEW);
            _serverControlsContainer = _root.Q<VisualElement>(McpUIToolkitCommonConstants.ELEMENT_SERVER_CONTROLS);
            _connectedToolsFoldout = _root.Q<Foldout>(McpUIToolkitCommonConstants.ELEMENT_CONNECTED_TOOLS);
            _editorConfigFoldout = _root.Q<Foldout>(McpUIToolkitCommonConstants.ELEMENT_EDITOR_CONFIG);
            _securitySettingsFoldout = _root.Q<Foldout>(McpUIToolkitCommonConstants.ELEMENT_SECURITY_SETTINGS);
            
            // Initialize sub-views
            InitializeSubViews();
        }
        
        private void InitializeSubViews()
        {
            if (_serverControlsContainer != null)
                _serverControlsView = new(_serverControlsContainer);
            
            if (_connectedToolsFoldout != null)
                _connectedToolsView = new(_connectedToolsFoldout);
            
            if (_editorConfigFoldout != null)
                _editorConfigView = new(_editorConfigFoldout);
            
            if (_securitySettingsFoldout != null)
                _securitySettingsView = new(_securitySettingsFoldout);
        }
        
        public void UpdateServerControls(ServerControlsData data, Action toggleCallback, 
            Action<bool> autoStartCallback, Action<int> portCallback)
        {
            _serverControlsView?.Update(data, toggleCallback, autoStartCallback, portCallback);
        }
        
        public void UpdateConnectedTools(ConnectedToolsData data, Action<bool> foldoutCallback)
        {
            _connectedToolsView?.Update(data, foldoutCallback);
        }
        
        public void UpdateEditorConfig(EditorConfigData data, Action<McpEditorType> editorCallback,
            Action<string> configureCallback, Action<bool> foldoutCallback)
        {
            _editorConfigView?.Update(data, editorCallback, configureCallback, foldoutCallback);
        }
        
        public void UpdateSecuritySettings(SecuritySettingsData data, Action<bool> foldoutCallback,
            Action<bool> enableTestsCallback, Action<bool> allowMenuCallback, Action<bool> allowThirdPartyCallback)
        {
            _securitySettingsView?.Update(data, foldoutCallback, enableTestsCallback, 
                allowMenuCallback, allowThirdPartyCallback);
        }
        
        public void UpdateScrollPosition(Vector2 position)
        {
            if (_scrollView != null)
            {
                _scrollView.scrollOffset = position;
            }
        }
        
        public Vector2 GetScrollPosition()
        {
            return _scrollView?.scrollOffset ?? Vector2.zero;
        }
    }
}