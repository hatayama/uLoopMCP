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
            _contentContainer = _foldout.Q<VisualElement>("security-settings-content");
            if (_contentContainer == null)
            {
                _contentContainer = new();
                _contentContainer.name = "security-settings-content";
                _foldout.Add(_contentContainer);
            }
            
            BuildUI();
            
            // Register foldout change callback
            _foldout.RegisterValueChangedCallback(OnFoldoutChanged);
        }
        
        private void BuildUI()
        {
            _contentContainer.Clear();
            
            // Security warning
            VisualElement warningBox = new();
            warningBox.AddToClassList("mcp-helpbox");
            warningBox.AddToClassList("mcp-helpbox--error");
            
            Label warningLabel = new("These settings control dangerous MCP operations. Only enable if you trust the AI system.\n\nFor safer operation, consider using sandbox environments or containers.\n\nChanges take effect immediately - no server restart required.");
            warningBox.Add(warningLabel);
            
            _contentContainer.Add(warningBox);
            
            // Space after warning
            VisualElement spacer = new();
            spacer.style.height = 8;
            _contentContainer.Add(spacer);
            
            // Enable Tests Execution
            VisualElement testsRow = CreateToggleRow(
                out _enableTestsToggle,
                out _enableTestsLabel,
                "Allow Tests Execution",
                OnEnableTestsChanged
            );
            _contentContainer.Add(testsRow);
            
            // Allow Menu Item Execution
            VisualElement menuRow = CreateToggleRow(
                out _allowMenuToggle,
                out _allowMenuLabel,
                "Allow Menu Item Execution",
                OnAllowMenuChanged
            );
            _contentContainer.Add(menuRow);
            
            // Allow Third Party Tools
            VisualElement thirdPartyRow = CreateToggleRow(
                out _allowThirdPartyToggle,
                out _allowThirdPartyLabel,
                "Allow Third Party Tools",
                OnAllowThirdPartyChanged
            );
            _contentContainer.Add(thirdPartyRow);
        }
        
        private VisualElement CreateToggleRow(out Toggle toggle, out Label label, 
            string labelText, EventCallback<ChangeEvent<bool>> callback)
        {
            VisualElement row = new();
            row.AddToClassList("mcp-security-settings__toggle-row");
            
            toggle = new Toggle();
            toggle.AddToClassList("mcp-security-settings__toggle");
            toggle.RegisterValueChangedCallback(callback);
            row.Add(toggle);
            
            label = new Label(labelText);
            label.AddToClassList("mcp-security-settings__label");
            
            // Create local reference to avoid out parameter in lambda
            Toggle toggleRef = toggle;
            label.RegisterCallback<ClickEvent>(evt => {
                toggleRef.value = !toggleRef.value;
            });
            row.Add(label);
            
            return row;
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
            _foldoutCallback?.Invoke(evt.newValue);
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