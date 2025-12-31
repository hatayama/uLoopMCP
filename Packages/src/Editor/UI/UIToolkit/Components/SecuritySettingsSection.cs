using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// UI section for security settings (test execution, menu items, dynamic code).
    /// Controls dangerous MCP operations and Roslyn integration.
    /// </summary>
    public class SecuritySettingsSection
    {
        private readonly Foldout _foldout;
        private readonly Toggle _enableTestsToggle;
        private readonly Toggle _allowMenuToggle;
        private readonly Toggle _allowThirdPartyToggle;
        private readonly Label _enableTestsLabel;
        private readonly Label _allowMenuLabel;
        private readonly Label _allowThirdPartyLabel;
        private readonly EnumField _securityLevelField;
        private readonly Label _securityLevelDescription;

        private SecuritySettingsData _lastData;
        private bool _isInitialized;

        public event Action<bool> OnFoldoutChanged;
        public event Action<bool> OnEnableTestsChanged;
        public event Action<bool> OnAllowMenuChanged;
        public event Action<bool> OnAllowThirdPartyChanged;
        public event Action<DynamicCodeSecurityLevel> OnSecurityLevelChanged;

        public SecuritySettingsSection(VisualElement root)
        {
            _foldout = root.Q<Foldout>("security-foldout");
            _enableTestsToggle = root.Q<Toggle>("enable-tests-toggle");
            _allowMenuToggle = root.Q<Toggle>("allow-menu-toggle");
            _allowThirdPartyToggle = root.Q<Toggle>("allow-third-party-toggle");
            _enableTestsLabel = root.Q<Label>("enable-tests-label");
            _allowMenuLabel = root.Q<Label>("allow-menu-label");
            _allowThirdPartyLabel = root.Q<Label>("allow-third-party-label");
            _securityLevelField = root.Q<EnumField>("security-level-field");
            _securityLevelDescription = root.Q<Label>("security-level-description");

            SetupBindings();
        }

        private void SetupBindings()
        {
            _foldout.RegisterValueChangedCallback(evt => OnFoldoutChanged?.Invoke(evt.newValue));
            _enableTestsToggle.RegisterValueChangedCallback(evt => OnEnableTestsChanged?.Invoke(evt.newValue));
            _allowMenuToggle.RegisterValueChangedCallback(evt => OnAllowMenuChanged?.Invoke(evt.newValue));
            _allowThirdPartyToggle.RegisterValueChangedCallback(evt => OnAllowThirdPartyChanged?.Invoke(evt.newValue));

            // Labels need click handlers because UI Toolkit toggles don't natively support label clicks
            _enableTestsLabel.RegisterCallback<ClickEvent>(evt => ToggleValue(_enableTestsToggle, OnEnableTestsChanged));
            _allowMenuLabel.RegisterCallback<ClickEvent>(evt => ToggleValue(_allowMenuToggle, OnAllowMenuChanged));
            _allowThirdPartyLabel.RegisterCallback<ClickEvent>(evt => ToggleValue(_allowThirdPartyToggle, OnAllowThirdPartyChanged));
        }

        /// <summary>
        /// Manually toggle and notify because SetValueWithoutNotify doesn't fire change events.
        /// </summary>
        private void ToggleValue(Toggle toggle, Action<bool> callback)
        {
            bool newValue = !toggle.value;
            toggle.SetValueWithoutNotify(newValue);
            callback?.Invoke(newValue);
        }

        public void Update(SecuritySettingsData data)
        {
            ViewDataBinder.UpdateFoldout(_foldout, data.ShowSecuritySettings);
            ViewDataBinder.UpdateToggle(_enableTestsToggle, data.EnableTestsExecution);
            ViewDataBinder.UpdateToggle(_allowMenuToggle, data.AllowMenuItemExecution);
            ViewDataBinder.UpdateToggle(_allowThirdPartyToggle, data.AllowThirdPartyTools);

            InitializeSecurityLevelFieldIfNeeded();
            UpdateSecurityLevelDescription();

            _lastData = data;
        }

        /// <summary>
        /// EnumField.Init() can only be called once; subsequent calls reset the field and cause visual glitches.
        /// </summary>
        private void InitializeSecurityLevelFieldIfNeeded()
        {
            if (_isInitialized)
            {
                return;
            }

            DynamicCodeSecurityLevel currentLevel = McpEditorSettings.GetDynamicCodeSecurityLevel();
            _securityLevelField.Init(currentLevel);

            _securityLevelField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is DynamicCodeSecurityLevel newLevel)
                {
                    HandleSecurityLevelChange(evt.previousValue as DynamicCodeSecurityLevel? ?? DynamicCodeSecurityLevel.Disabled, newLevel);
                }
            });

            _isInitialized = true;
        }

        private void HandleSecurityLevelChange(DynamicCodeSecurityLevel previousLevel, DynamicCodeSecurityLevel newLevel)
        {
            if (previousLevel == DynamicCodeSecurityLevel.Disabled && newLevel != DynamicCodeSecurityLevel.Disabled)
            {
                bool roslynAvailable = RoslynAssemblyChecker.IsRoslynAvailable();

                if (!roslynAvailable)
                {
                    EditorUtility.DisplayDialog(
                        "Roslyn Not Installed",
                        RoslynAssemblyChecker.GetInstallationMessage(),
                        "OK"
                    );
                    ViewDataBinder.UpdateEnumField(_securityLevelField, previousLevel);
                    return;
                }

                string version = RoslynAssemblyChecker.GetRoslynVersion();
                bool confirmed = EditorUtility.DisplayDialog(
                    "Enable Roslyn Features",
                    $"Microsoft.CodeAnalysis.CSharp is installed (version: {version}).\n\n" +
                    "This will enable advanced code analysis and execution features.\n\n" +
                    "Continue?",
                    "Enable",
                    "Cancel"
                );

                if (!confirmed)
                {
                    ViewDataBinder.UpdateEnumField(_securityLevelField, previousLevel);
                    return;
                }
            }

            OnSecurityLevelChanged?.Invoke(newLevel);
            UpdateSecurityLevelDescription();
        }

        private void UpdateSecurityLevelDescription()
        {
            DynamicCodeSecurityLevel currentLevel = McpEditorSettings.GetDynamicCodeSecurityLevel();

            string description = currentLevel switch
            {
                DynamicCodeSecurityLevel.Disabled => "Level 0: Code execution completely disabled (safest)",
                DynamicCodeSecurityLevel.Restricted => "Level 1: Dangerous APIs blocked (recommended)",
                DynamicCodeSecurityLevel.FullAccess => "Level 2: All APIs available (use with caution)",
                _ => "Unknown level"
            };

            _securityLevelDescription.text = description;

            _securityLevelDescription.RemoveFromClassList("mcp-security-level-description--warning");

            if (currentLevel == DynamicCodeSecurityLevel.FullAccess)
            {
                _securityLevelDescription.AddToClassList("mcp-security-level-description--warning");
            }
        }
    }
}
