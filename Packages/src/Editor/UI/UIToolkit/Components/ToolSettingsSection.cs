using System;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// UI section for per-tool enable/disable toggles.
    /// Groups tools into Built-in and Third Party categories.
    /// </summary>
    public class ToolSettingsSection
    {
        private readonly Foldout _foldout;
        private readonly ScrollView _toolListContainer;

        public event Action<bool> OnFoldoutChanged;
        public event Action<string, bool> OnToolToggled;

        public ToolSettingsSection(VisualElement root)
        {
            _foldout = root.Q<Foldout>("tool-settings-foldout");
            _toolListContainer = root.Q<ScrollView>("tool-list-container");

            SetupBindings();
        }

        private void SetupBindings()
        {
            _foldout.RegisterValueChangedCallback(evt => OnFoldoutChanged?.Invoke(evt.newValue));
        }

        public void Update(ToolSettingsSectionData data)
        {
            ViewDataBinder.UpdateFoldout(_foldout, data.ShowToolSettings);

            _toolListContainer.Clear();

            if (!data.IsRegistryAvailable)
            {
                Label unavailableLabel = new Label("Tool registry not yet initialized. Start the server first.");
                unavailableLabel.AddToClassList("mcp-tool-registry-unavailable");
                _toolListContainer.Add(unavailableLabel);
                return;
            }

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
        }
    }
}
