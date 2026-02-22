using System;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    public class CliSetupSection
    {
        private readonly Foldout _foldout;
        private readonly VisualElement _cliStatusIcon;
        private readonly Label _cliStatusLabel;
        private readonly Button _installCliButton;
        private readonly VisualElement _skillsStatusIcon;
        private readonly Label _skillsStatusLabel;
        private readonly Toggle _targetClaudeToggle;
        private readonly Toggle _targetCodexToggle;
        private readonly Button _installSkillsButton;

        private CliSetupData _lastData;

        public event Action OnInstallCli;
        public event Action OnInstallSkills;
        public event Action<bool> OnTargetClaudeChanged;
        public event Action<bool> OnTargetCodexChanged;
        public event Action<bool> OnFoldoutChanged;

        public CliSetupSection(VisualElement root)
        {
            _foldout = root.Q<Foldout>("cli-setup-foldout");
            _cliStatusIcon = root.Q<VisualElement>("cli-status-icon");
            _cliStatusLabel = root.Q<Label>("cli-status-label");
            _installCliButton = root.Q<Button>("install-cli-button");
            _skillsStatusIcon = root.Q<VisualElement>("skills-status-icon");
            _skillsStatusLabel = root.Q<Label>("skills-status-label");
            _targetClaudeToggle = root.Q<Toggle>("target-claude-toggle");
            _targetCodexToggle = root.Q<Toggle>("target-codex-toggle");
            _installSkillsButton = root.Q<Button>("install-skills-button");
        }

        public void SetupBindings()
        {
            _foldout.RegisterValueChangedCallback(evt => OnFoldoutChanged?.Invoke(evt.newValue));
            _installCliButton.clicked += () => OnInstallCli?.Invoke();
            _installSkillsButton.clicked += () => OnInstallSkills?.Invoke();
            _targetClaudeToggle.RegisterValueChangedCallback(evt => OnTargetClaudeChanged?.Invoke(evt.newValue));
            _targetCodexToggle.RegisterValueChangedCallback(evt => OnTargetCodexChanged?.Invoke(evt.newValue));
        }

        public void Update(CliSetupData data)
        {
            if (_lastData != null && _lastData.Equals(data))
            {
                return;
            }

            _lastData = data;

            ViewDataBinder.UpdateFoldout(_foldout, data.ShowFoldout);
            UpdateCliStatus(data);
            UpdateSkillsStatus(data);
            UpdateInstallCliButton(data);
            UpdateInstallSkillsButton(data);
            ViewDataBinder.UpdateToggle(_targetClaudeToggle, data.TargetClaude);
            ViewDataBinder.UpdateToggle(_targetCodexToggle, data.TargetCodex);
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
            bool anyInstalled = data.IsClaudeSkillsInstalled || data.IsCodexSkillsInstalled;
            ViewDataBinder.ToggleClass(_skillsStatusIcon, "mcp-cli-status-icon--installed", anyInstalled);
            ViewDataBinder.ToggleClass(_skillsStatusIcon, "mcp-cli-status-icon--not-installed", !anyInstalled);

            if (data.IsClaudeSkillsInstalled && data.IsCodexSkillsInstalled)
            {
                _skillsStatusLabel.text = "Skills: Installed (Claude, Codex)";
            }
            else if (data.IsClaudeSkillsInstalled)
            {
                _skillsStatusLabel.text = "Skills: Installed (Claude)";
            }
            else if (data.IsCodexSkillsInstalled)
            {
                _skillsStatusLabel.text = "Skills: Installed (Codex)";
            }
            else
            {
                _skillsStatusLabel.text = "Skills: Not installed";
            }
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

            bool noTargetSelected = !data.TargetClaude && !data.TargetCodex;
            if (noTargetSelected)
            {
                SetSkillsButton("Install Skills", false);
                return;
            }

            bool allSelectedInstalled =
                (!data.TargetClaude || data.IsClaudeSkillsInstalled) &&
                (!data.TargetCodex || data.IsCodexSkillsInstalled);

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
