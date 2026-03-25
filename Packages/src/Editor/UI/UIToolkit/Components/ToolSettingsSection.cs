using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// UI section for per-tool enable/disable toggles.
    /// Groups tools into Built-in and Third Party categories.
    /// Uses differential updates to avoid full rebuild on each toggle change.
    /// </summary>
    public class ToolSettingsSection
    {
        private readonly Foldout _foldout;
        private readonly Toggle _allowThirdPartyToggle;
        private readonly Label _allowThirdPartyLabel;
        private readonly Button _securityLevelRestrictedButton;
        private readonly Button _securityLevelFullAccessButton;
        private readonly Label _securityLevelDescription;
        private readonly VisualElement _toolListContainer;
        private readonly Dictionary<string, Toggle> _togglesByToolName = new();
        private VisualElement _thirdPartyGroupContainer;
        private bool _isRegistryAvailable;
        private bool _isUnavailableStateShown;
        private string _layoutSignature = string.Empty;

        public event Action<bool> OnFoldoutChanged;
        public event Action<string, bool> OnToolToggled;
        public event Action<bool> OnAllowThirdPartyChanged;
        public event Action<DynamicCodeSecurityLevel> OnSecurityLevelChanged;

        public ToolSettingsSection(VisualElement root)
        {
            _foldout = root.Q<Foldout>("tool-settings-foldout");
            _allowThirdPartyToggle = root.Q<Toggle>("allow-third-party-toggle");
            _allowThirdPartyLabel = root.Q<Label>("allow-third-party-label");
            _securityLevelRestrictedButton = root.Q<Button>("security-level-restricted-button");
            _securityLevelFullAccessButton = root.Q<Button>("security-level-full-access-button");
            _securityLevelDescription = root.Q<Label>("security-level-description");
            _toolListContainer = root.Q<VisualElement>("tool-list-container");

            Label cliReferenceLink = root.Q<Label>("cli-reference-link");
            cliReferenceLink.RegisterCallback<ClickEvent>(_ => Application.OpenURL(McpUIConstants.CLI_COMMAND_REFERENCE_URL));

            SetupBindings();
        }

        private void SetupBindings()
        {
            _foldout.RegisterValueChangedCallback(evt => OnFoldoutChanged?.Invoke(evt.newValue));

            _allowThirdPartyToggle.RegisterValueChangedCallback(evt =>
            {
                evt.StopPropagation();
                OnAllowThirdPartyChanged?.Invoke(evt.newValue);
            });

            _allowThirdPartyLabel.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                bool newValue = !_allowThirdPartyToggle.value;
                _allowThirdPartyToggle.SetValueWithoutNotify(newValue);
                OnAllowThirdPartyChanged?.Invoke(newValue);
            });

            _securityLevelRestrictedButton.clicked += () => UpdateSecurityLevel(DynamicCodeSecurityLevel.Restricted);
            _securityLevelFullAccessButton.clicked += () => UpdateSecurityLevel(DynamicCodeSecurityLevel.FullAccess);
        }

        public void Update(ToolSettingsSectionData data)
        {
            ViewDataBinder.UpdateFoldout(_foldout, data.ShowToolSettings);
            ViewDataBinder.UpdateToggle(_allowThirdPartyToggle, data.AllowThirdPartyTools);
            UpdateSecurityLevelSelection(data.DynamicCodeSecurityLevel);
            UpdateSecurityLevelDescription(data.DynamicCodeSecurityLevel);

            if (!data.IsRegistryAvailable)
            {
                UpdateUnavailableState();
                return;
            }

            UpdateToolList(data);
        }

        /// <summary>
        /// Update a single toggle value without rebuilding the entire list.
        /// </summary>
        public void UpdateSingleToggle(string toolName, bool enabled)
        {
            if (_togglesByToolName.TryGetValue(toolName, out Toggle toggle))
            {
                toggle.SetValueWithoutNotify(enabled);
            }
        }

        private void UpdateUnavailableState()
        {
            if (_isRegistryAvailable || !_isUnavailableStateShown)
            {
                RebuildUnavailable();
            }

            _isRegistryAvailable = false;
            _isUnavailableStateShown = true;
            _layoutSignature = string.Empty;
        }

        private void UpdateToolList(ToolSettingsSectionData data)
        {
            string layoutSignature = CreateLayoutSignature(data);
            bool shouldRebuild = !_isRegistryAvailable || _layoutSignature != layoutSignature;

            if (shouldRebuild)
            {
                Rebuild(data);
                _layoutSignature = layoutSignature;
            }
            else
            {
                UpdateToggleStates(data.BuiltInTools);
                UpdateToggleStates(data.ThirdPartyTools);
            }

            UpdateThirdPartyGroupState(data.AllowThirdPartyTools);

            _isRegistryAvailable = true;
            _isUnavailableStateShown = false;
        }

        private void RebuildUnavailable()
        {
            _toolListContainer.Clear();
            _togglesByToolName.Clear();
            _thirdPartyGroupContainer = null;
            Label unavailableLabel = new Label("Tool registry not yet initialized. Start the server first.");
            unavailableLabel.AddToClassList("mcp-tool-registry-unavailable");
            _toolListContainer.Add(unavailableLabel);
        }

        private void UpdateSecurityLevelDescription(DynamicCodeSecurityLevel currentLevel)
        {
            string description = currentLevel switch
            {
                DynamicCodeSecurityLevel.Restricted => "Dangerous APIs blocked (recommended)",
                DynamicCodeSecurityLevel.FullAccess => "All APIs available (use with caution)",
                _ => "Unknown level"
            };

            _securityLevelDescription.text = description;
            _securityLevelDescription.RemoveFromClassList("mcp-security-level-description--warning");

            if (currentLevel == DynamicCodeSecurityLevel.FullAccess)
            {
                _securityLevelDescription.AddToClassList("mcp-security-level-description--warning");
            }
        }

        private void UpdateSecurityLevel(DynamicCodeSecurityLevel newLevel)
        {
            UpdateSecurityLevelSelection(newLevel);
            UpdateSecurityLevelDescription(newLevel);
            OnSecurityLevelChanged?.Invoke(newLevel);
        }

        private void UpdateSecurityLevelSelection(DynamicCodeSecurityLevel currentLevel)
        {
            bool isRestricted = currentLevel == DynamicCodeSecurityLevel.Restricted;
            bool isFullAccess = currentLevel == DynamicCodeSecurityLevel.FullAccess;

            ViewDataBinder.ToggleClass(_securityLevelRestrictedButton, "mcp-segmented-control__button--active", isRestricted);
            ViewDataBinder.ToggleClass(_securityLevelRestrictedButton, "mcp-segmented-control__button--warning-active", false);
            ViewDataBinder.ToggleClass(_securityLevelFullAccessButton, "mcp-segmented-control__button--active", isFullAccess);
            ViewDataBinder.ToggleClass(_securityLevelFullAccessButton, "mcp-segmented-control__button--warning-active", isFullAccess);
        }

        private void Rebuild(ToolSettingsSectionData data)
        {
            _toolListContainer.Clear();
            _togglesByToolName.Clear();
            _thirdPartyGroupContainer = null;

            if (data.BuiltInTools.Length > 0)
            {
                VisualElement builtInGroup = CreateGroupContainer();
                AddGroupHeader(builtInGroup, "Built-in Tools");
                foreach (ToolToggleItem item in data.BuiltInTools)
                {
                    AddToolToggle(builtInGroup, item);
                }
                _toolListContainer.Add(builtInGroup);
            }

            if (data.ThirdPartyTools.Length > 0)
            {
                _thirdPartyGroupContainer = CreateGroupContainer();
                AddGroupHeader(_thirdPartyGroupContainer, "Third Party Tools");
                foreach (ToolToggleItem item in data.ThirdPartyTools)
                {
                    AddToolToggle(_thirdPartyGroupContainer, item);
                }
                _toolListContainer.Add(_thirdPartyGroupContainer);
            }
        }

        private void UpdateToggleStates(IReadOnlyList<ToolToggleItem> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ToolToggleItem item = items[i];
                UpdateSingleToggle(item.ToolName, item.IsEnabled);
            }
        }

        private static string CreateLayoutSignature(ToolSettingsSectionData data)
        {
            StringBuilder builder = new StringBuilder();
            AppendGroupSignature(builder, data.BuiltInTools, "B");
            AppendGroupSignature(builder, data.ThirdPartyTools, "T");
            return builder.ToString();
        }

        private static void AppendGroupSignature(StringBuilder builder, IReadOnlyList<ToolToggleItem> items, string group)
        {
            builder.Append(group);
            builder.Append(':');

            for (int i = 0; i < items.Count; i++)
            {
                ToolToggleItem item = items[i];
                builder.Append(item.ToolName);
                builder.Append('|');
            }

            builder.Append(';');
        }

        private void UpdateThirdPartyGroupState(bool allowThirdPartyTools)
        {
            if (_thirdPartyGroupContainer == null)
            {
                return;
            }

            ViewDataBinder.SetEnabled(_thirdPartyGroupContainer, allowThirdPartyTools);
            ViewDataBinder.ToggleClass(_thirdPartyGroupContainer, "mcp-tool-group--disabled", !allowThirdPartyTools);
        }

        private static VisualElement CreateGroupContainer()
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("mcp-tool-group");
            return container;
        }

        private static void AddGroupHeader(VisualElement container, string text)
        {
            Label header = new Label(text);
            header.AddToClassList("mcp-tool-group-header");
            container.Add(header);
        }

        private void AddToolToggle(VisualElement container, ToolToggleItem item)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("mcp-tool-toggle-row");

            Toggle toggle = new Toggle();
            toggle.AddToClassList("mcp-tool-toggle-row__toggle");
            toggle.SetValueWithoutNotify(item.IsEnabled);

            string toolName = item.ToolName;
            toggle.RegisterValueChangedCallback(evt =>
            {
                // Foldout uses an internal Toggle that listens for ChangeEvent<bool>.
                // Without StopPropagation, this event bubbles up and collapses the Foldout.
                evt.StopPropagation();
                OnToolToggled?.Invoke(toolName, evt.newValue);
            });

            Label label = new Label(item.ToolName);
            label.AddToClassList("mcp-tool-toggle-row__label");
            label.tooltip = item.Description;
            label.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                bool newValue = !toggle.value;
                toggle.SetValueWithoutNotify(newValue);
                OnToolToggled?.Invoke(toolName, newValue);
            });

            row.Add(toggle);
            row.Add(label);
            container.Add(row);
            _togglesByToolName[toolName] = toggle;
        }
    }
}
