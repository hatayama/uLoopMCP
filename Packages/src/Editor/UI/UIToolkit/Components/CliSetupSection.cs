using System;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    public class CliSetupSection
    {
        private readonly VisualElement _cliStatusIcon;
        private readonly Label _cliStatusLabel;
        private readonly Button _installCliButton;
        private readonly VisualElement _skillsStatusIcon;
        private readonly Label _skillsStatusLabel;
        private readonly EnumField _skillsTargetField;
        private readonly Button _installSkillsButton;

        private CliSetupData _lastData;
        private bool _isTargetFieldInitialized;

        public event Action OnInstallCli;
        public event Action OnInstallSkills;
        public event Action<SkillsTarget> OnSkillsTargetChanged;

        public CliSetupSection(VisualElement root)
        {
            _cliStatusIcon = root.Q<VisualElement>("cli-status-icon");
            _cliStatusLabel = root.Q<Label>("cli-status-label");
            _installCliButton = root.Q<Button>("install-cli-button");
            _skillsStatusIcon = root.Q<VisualElement>("skills-status-icon");
            _skillsStatusLabel = root.Q<Label>("skills-status-label");
            _skillsTargetField = root.Q<EnumField>("skills-target-field");
            _installSkillsButton = root.Q<Button>("install-skills-button");
        }

        public void SetupBindings()
        {
            _installCliButton.clicked += () => OnInstallCli?.Invoke();
            _installSkillsButton.clicked += () => OnInstallSkills?.Invoke();
        }

        public void Update(CliSetupData data)
        {
            if (_lastData != null && _lastData.Equals(data))
            {
                return;
            }

            _lastData = data;

            UpdateCliStatus(data);
            UpdateSkillsStatus(data);
            UpdateInstallCliButton(data);
            InitializeTargetFieldIfNeeded(data);
            UpdateInstallSkillsButton(data);
        }

        private void UpdateCliStatus(CliSetupData data)
        {
            ViewDataBinder.ToggleClass(_cliStatusIcon, "mcp-cli-status-icon--installed", data.IsCliInstalled);
            ViewDataBinder.ToggleClass(_cliStatusIcon, "mcp-cli-status-icon--not-installed", !data.IsCliInstalled);

            if (data.IsCliInstalled && data.CliVersion != null)
            {
                _cliStatusLabel.text = $"uLoop CLI: v{data.CliVersion}";
            }
            else
            {
                _cliStatusLabel.text = "uLoop CLI: Not installed";
            }
        }

        private void UpdateSkillsStatus(CliSetupData data)
        {
            bool anyInstalled = data.IsClaudeSkillsInstalled || data.IsCodexSkillsInstalled
                || data.IsCursorSkillsInstalled || data.IsGeminiSkillsInstalled || data.IsWindsurfSkillsInstalled;
            ViewDataBinder.ToggleClass(_skillsStatusIcon, "mcp-cli-status-icon--installed", anyInstalled);
            ViewDataBinder.ToggleClass(_skillsStatusIcon, "mcp-cli-status-icon--not-installed", !anyInstalled);

            System.Collections.Generic.List<string> installed = new System.Collections.Generic.List<string>();
            if (data.IsClaudeSkillsInstalled) installed.Add("Claude");
            if (data.IsCodexSkillsInstalled) installed.Add("Codex");
            if (data.IsCursorSkillsInstalled) installed.Add("Cursor");
            if (data.IsGeminiSkillsInstalled) installed.Add("Gemini");
            if (data.IsWindsurfSkillsInstalled) installed.Add("Windsurf");

            _skillsStatusLabel.text = installed.Count > 0
                ? $"Skills: Installed ({string.Join(", ", installed)})"
                : "Skills: Not installed";
        }

        private void UpdateInstallCliButton(CliSetupData data)
        {
            if (data.IsInstallingCli)
            {
                SetCliButton("Installing...", false);
                return;
            }

            if (!data.IsCliInstalled)
            {
                SetCliButton("Install CLI", true);
                return;
            }

            if (data.NeedsUpdate)
            {
                SetCliButton($"Update CLI (v{data.CliVersion} \u2192 v{data.PackageVersion})", true);
                return;
            }

            SetCliButton("Up to date", false);
        }

        private void SetCliButton(string text, bool enabled)
        {
            _installCliButton.text = text;
            _installCliButton.SetEnabled(enabled);
            ViewDataBinder.ToggleClass(_installCliButton, "mcp-button--disabled", !enabled);
        }

        private void InitializeTargetFieldIfNeeded(CliSetupData data)
        {
            if (!_isTargetFieldInitialized)
            {
                _skillsTargetField.Init(data.SelectedTarget);
                _skillsTargetField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue is SkillsTarget newValue)
                    {
                        OnSkillsTargetChanged?.Invoke(newValue);
                    }
                });
                _isTargetFieldInitialized = true;
            }
            else
            {
                ViewDataBinder.UpdateEnumField(_skillsTargetField, data.SelectedTarget);
            }
        }

        private void UpdateInstallSkillsButton(CliSetupData data)
        {
            if (data.IsInstallingSkills)
            {
                SetSkillsButton("Installing...", false);
                return;
            }

            if (!data.IsCliInstalled)
            {
                SetSkillsButton("Install Skills", false);
                return;
            }

            bool allSelectedInstalled = data.SelectedTarget switch
            {
                SkillsTarget.Claude => data.IsClaudeSkillsInstalled,
                SkillsTarget.Codex => data.IsCodexSkillsInstalled,
                SkillsTarget.Cursor => data.IsCursorSkillsInstalled,
                SkillsTarget.Gemini => data.IsGeminiSkillsInstalled,
                SkillsTarget.Windsurf => data.IsWindsurfSkillsInstalled,
                _ => false
            };

            if (allSelectedInstalled)
            {
                SetSkillsButton("Skills Installed", false);
            }
            else
            {
                SetSkillsButton("Install Skills", true);
            }
        }

        private void SetSkillsButton(string text, bool enabled)
        {
            _installSkillsButton.text = text;
            _installSkillsButton.SetEnabled(enabled);
            ViewDataBinder.ToggleClass(_installSkillsButton, "mcp-button--disabled", !enabled);
        }
    }
}
