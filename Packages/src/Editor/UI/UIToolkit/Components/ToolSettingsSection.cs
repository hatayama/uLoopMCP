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
        private readonly VisualElement _toolListContainer;
        private readonly Dictionary<string, Toggle> _togglesByToolName = new();
        private bool _isRegistryAvailable;
        private bool _isUnavailableStateShown;
        private string _layoutSignature = string.Empty;

        public event Action<bool> OnFoldoutChanged;
        public event Action<string, bool> OnToolToggled;

        public ToolSettingsSection(VisualElement root)
        {
            _foldout = root.Q<Foldout>("tool-settings-foldout");
            _toolListContainer = root.Q<VisualElement>("tool-list-container");

            Label toolReferenceLink = root.Q<Label>("tool-reference-link");
            toolReferenceLink.RegisterCallback<ClickEvent>(_ => Application.OpenURL(McpUIConstants.TOOL_REFERENCE_URL));

            SetupBindings();
        }

        private void SetupBindings()
        {
            _foldout.RegisterValueChangedCallback(evt => OnFoldoutChanged?.Invoke(evt.newValue));
        }

        public void Update(ToolSettingsSectionData data)
        {
            ViewDataBinder.UpdateFoldout(_foldout, data.ShowToolSettings);

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

            _isRegistryAvailable = true;
            _isUnavailableStateShown = false;
        }

        private void RebuildUnavailable()
        {
            _toolListContainer.Clear();
            _togglesByToolName.Clear();
            Label unavailableLabel = new Label("Tool registry not yet initialized. Start the server first.");
            unavailableLabel.AddToClassList("mcp-tool-registry-unavailable");
            _toolListContainer.Add(unavailableLabel);
        }

        private void Rebuild(ToolSettingsSectionData data)
        {
            _toolListContainer.Clear();
            _togglesByToolName.Clear();

            if (data.BuiltInTools.Length > 0)
            {
                AddGroupHeader("Built-in Tools");
                foreach (ToolToggleItem item in data.BuiltInTools)
                {
                    AddToolToggle(item);
                }
            }

            if (data.ThirdPartyTools.Length > 0)
            {
                AddGroupHeader("Third Party Tools");
                foreach (ToolToggleItem item in data.ThirdPartyTools)
                {
                    AddToolToggle(item);
                }
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

        private void AddGroupHeader(string text)
        {
            Label header = new Label(text);
            header.AddToClassList("mcp-tool-group-header");
            _toolListContainer.Add(header);
        }

        private void AddToolToggle(ToolToggleItem item)
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
            _toolListContainer.Add(row);
            _togglesByToolName[toolName] = toggle;
        }
    }
}
