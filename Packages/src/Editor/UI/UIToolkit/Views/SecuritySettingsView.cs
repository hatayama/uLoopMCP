using UnityEngine.UIElements;
using System;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Security settings component for UI Toolkit
    /// Handles dangerous MCP operations settings
    /// </summary>
    public class SecuritySettingsView
    {
        private readonly Foldout _foldout;
        private readonly VisualElement _contentContainer;
        private Toggle _enableTestsToggle;
        private Label _enableTestsLabel;
        private Toggle _allowMenuToggle;
        private Label _allowMenuLabel;
        private Toggle _allowThirdPartyToggle;
        private Label _allowThirdPartyLabel;
        
        // Callbacks
        private Action<bool> _foldoutCallback;
        private Action<bool> _enableTestsCallback;
        private Action<bool> _allowMenuCallback;
        private Action<bool> _allowThirdPartyCallback;
        
        public SecuritySettingsView(Foldout foldout)
        {
            _foldout = foldout;
            _foldout.text = "Security Settings";
            
            // Get or create content container
            _contentContainer = _foldout.Q<VisualElement>(McpUIToolkitConstants.ELEMENT_SECURITY_SETTINGS_CONTENT);
            if (_contentContainer == null)
            {
                _contentContainer = new();
                _contentContainer.name = McpUIToolkitConstants.ELEMENT_SECURITY_SETTINGS_CONTENT;
                _foldout.Add(_contentContainer);
            }
            
            BuildUI();
            
            // Register foldout change callback
            _foldout.RegisterValueChangedCallback(OnFoldoutChanged);
        }
        
        private void BuildUI()
        {
            // UXMLで定義された要素を取得（動的生成から変更）
            _enableTestsToggle = _contentContainer.Q<Toggle>(McpUIToolkitConstants.ELEMENT_ENABLE_TESTS_TOGGLE);
            _enableTestsLabel = _contentContainer.Q<Label>(McpUIToolkitConstants.ELEMENT_ENABLE_TESTS_LABEL);
            _allowMenuToggle = _contentContainer.Q<Toggle>(McpUIToolkitConstants.ELEMENT_ALLOW_MENU_TOGGLE);
            _allowMenuLabel = _contentContainer.Q<Label>(McpUIToolkitConstants.ELEMENT_ALLOW_MENU_LABEL);
            _allowThirdPartyToggle = _contentContainer.Q<Toggle>(McpUIToolkitConstants.ELEMENT_ALLOW_THIRD_PARTY_TOGGLE);
            _allowThirdPartyLabel = _contentContainer.Q<Label>(McpUIToolkitConstants.ELEMENT_ALLOW_THIRD_PARTY_LABEL);
            
            // イベントハンドラーの登録
            if (_enableTestsToggle != null)
                _enableTestsToggle.RegisterValueChangedCallback(OnEnableTestsChanged);
                
            if (_allowMenuToggle != null)
                _allowMenuToggle.RegisterValueChangedCallback(OnAllowMenuChanged);
                
            if (_allowThirdPartyToggle != null)
                _allowThirdPartyToggle.RegisterValueChangedCallback(OnAllowThirdPartyChanged);
                
            // ラベルクリックでトグルを切り替える機能
            RegisterLabelClickHandler(_enableTestsLabel, _enableTestsToggle);
            RegisterLabelClickHandler(_allowMenuLabel, _allowMenuToggle);
            RegisterLabelClickHandler(_allowThirdPartyLabel, _allowThirdPartyToggle);
        }
        
        private void RegisterLabelClickHandler(Label label, Toggle toggle)
        {
            if (label != null && toggle != null)
            {
                label.RegisterCallback<ClickEvent>(evt => {
                    toggle.value = !toggle.value;
                });
            }
        }
        
        public void Update(SecuritySettingsData data, Action<bool> foldoutCallback,
            Action<bool> enableTestsCallback, Action<bool> allowMenuCallback, 
            Action<bool> allowThirdPartyCallback)
        {
            // Store callbacks
            _foldoutCallback = foldoutCallback;
            _enableTestsCallback = enableTestsCallback;
            _allowMenuCallback = allowMenuCallback;
            _allowThirdPartyCallback = allowThirdPartyCallback;
            
            // Update foldout state
            _foldout.SetValueWithoutNotify(data.ShowSecuritySettings);
            
            if (!data.ShowSecuritySettings)
            {
                return;
            }
            
            // Update toggle values
            _enableTestsToggle.SetValueWithoutNotify(data.EnableTestsExecution);
            _allowMenuToggle.SetValueWithoutNotify(data.AllowMenuItemExecution);
            _allowThirdPartyToggle.SetValueWithoutNotify(data.AllowThirdPartyTools);
        }
        
        private void OnFoldoutChanged(ChangeEvent<bool> evt)
        {
            // Only process events from the foldout itself, not from child elements
            if (evt.target == _foldout)
            {
                _foldoutCallback?.Invoke(evt.newValue);
            }
        }
        
        private void OnEnableTestsChanged(ChangeEvent<bool> evt)
        {
            _enableTestsCallback?.Invoke(evt.newValue);
        }
        
        private void OnAllowMenuChanged(ChangeEvent<bool> evt)
        {
            _allowMenuCallback?.Invoke(evt.newValue);
        }
        
        private void OnAllowThirdPartyChanged(ChangeEvent<bool> evt)
        {
            _allowThirdPartyCallback?.Invoke(evt.newValue);
        }
    }
}