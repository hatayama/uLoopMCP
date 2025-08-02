using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
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
        private VisualElement _serverStatusContainer;
        private VisualElement _serverControlsContainer;
        private Foldout _connectedToolsFoldout;
        private Foldout _editorConfigFoldout;
        private Foldout _securitySettingsFoldout;
        
        // View components
        private ServerStatusView _serverStatusView;
        private ServerControlsView _serverControlsView;
        private ConnectedToolsView _connectedToolsView;
        private EditorConfigView _editorConfigView;
        private SecuritySettingsView _securitySettingsView;
        
        public VisualElement Root => _root;
        
        public void Initialize()
        {
            UnityEngine.Debug.Log("=== McpEditorWindowUITView.Initialize() START ===");
            
            // Load UXML (USSはUXML内で自動的に読み込まれる)
            string uxmlPath = "Packages/io.github.hatayama.uLoopMCP/src/Editor/UI/UIToolkit/McpEditorWindow.uxml";
            UnityEngine.Debug.Log($"Loading UXML from: {uxmlPath}");
            
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            
            if (visualTree == null)
            {
                UnityEngine.Debug.LogError($"Failed to load UXML file at: {uxmlPath}");
                // Try alternative paths
                string[] alternativePaths = {
                    "Assets/uLoopMCP/src/Editor/UI/UIToolkit/McpEditorWindow.uxml",
                    "Packages/src/Editor/UI/UIToolkit/McpEditorWindow.uxml",
                    AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("McpEditorWindow t:VisualTreeAsset").FirstOrDefault())
                };
                
                foreach (var path in alternativePaths)
                {
                    UnityEngine.Debug.Log($"Trying alternative path: {path}");
                    visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                    if (visualTree != null)
                    {
                        UnityEngine.Debug.Log($"Success! Found UXML at: {path}");
                        break;
                    }
                }
                
                if (visualTree == null)
                {
                    UnityEngine.Debug.LogError("Could not find UXML file in any location!");
                    return;
                }
            }
            else
            {
                UnityEngine.Debug.Log("UXML file loaded successfully");
            }
            
            _root = visualTree.CloneTree();
            
            if (_root == null)
            {
                UnityEngine.Debug.LogError("Failed to clone visual tree");
                return;
            }
            
            UnityEngine.Debug.Log($"Visual tree cloned. Root has {_root.childCount} children");
            UnityEngine.Debug.Log($"Root classes: {string.Join(", ", _root.GetClasses())}");
            
            // Try to load USS manually if not loaded
            string ussPath = "Packages/io.github.hatayama.uLoopMCP/src/Editor/UI/UIToolkit/McpEditorWindow.uss";
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (styleSheet != null)
            {
                _root.styleSheets.Add(styleSheet);
                UnityEngine.Debug.Log($"USS loaded manually from: {ussPath}");
            }
            else
            {
                UnityEngine.Debug.LogError($"Failed to load USS from: {ussPath}");
            }
            
            // Print hierarchy
            PrintVisualElementHierarchy(_root, 0);
            
            // Query containers
            _scrollView = _root.Q<ScrollView>("main-scroll-view");
            _serverStatusContainer = _root.Q<VisualElement>("server-status");
            _serverControlsContainer = _root.Q<VisualElement>("server-controls");
            _connectedToolsFoldout = _root.Q<Foldout>("connected-tools");
            _editorConfigFoldout = _root.Q<Foldout>("editor-config");
            _securitySettingsFoldout = _root.Q<Foldout>("security-settings");
            
            // Debug log for container queries
            UnityEngine.Debug.Log($"ScrollView found: {_scrollView != null}");
            UnityEngine.Debug.Log($"ServerStatus found: {_serverStatusContainer != null}");
            UnityEngine.Debug.Log($"ServerControls found: {_serverControlsContainer != null}");
            UnityEngine.Debug.Log($"ConnectedTools found: {_connectedToolsFoldout != null}");
            UnityEngine.Debug.Log($"EditorConfig found: {_editorConfigFoldout != null}");
            UnityEngine.Debug.Log($"SecuritySettings found: {_securitySettingsFoldout != null}");
            
            // Initialize sub-views
            InitializeSubViews();
            
            UnityEngine.Debug.Log("=== McpEditorWindowUITView.Initialize() END ===");
        }
        
        private void PrintVisualElementHierarchy(VisualElement element, int depth)
        {
            string indent = new string(' ', depth * 2);
            string name = string.IsNullOrEmpty(element.name) ? "<unnamed>" : element.name;
            string classes = element.GetClasses().Count() > 0 ? $" [{string.Join(", ", element.GetClasses())}]" : "";
            UnityEngine.Debug.Log($"{indent}{element.GetType().Name} - {name}{classes}");
            
            foreach (var child in element.Children())
            {
                PrintVisualElementHierarchy(child, depth + 1);
            }
        }
        
        private void InitializeSubViews()
        {
            if (_serverStatusContainer != null)
                _serverStatusView = new(_serverStatusContainer);
            
            if (_serverControlsContainer != null)
                _serverControlsView = new(_serverControlsContainer);
            
            if (_connectedToolsFoldout != null)
                _connectedToolsView = new(_connectedToolsFoldout);
            
            if (_editorConfigFoldout != null)
                _editorConfigView = new(_editorConfigFoldout);
            
            if (_securitySettingsFoldout != null)
                _securitySettingsView = new(_securitySettingsFoldout);
        }
        
        public void UpdateServerStatus(ServerStatusData data)
        {
            _serverStatusView?.Update(data);
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