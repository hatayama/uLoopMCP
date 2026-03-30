using System.Collections.Generic;
using System.Linq;
using System.Threading;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    [InitializeOnLoad]
    public class SetupWizardWindow : EditorWindow
    {
        private const string UXML_RELATIVE_PATH = "Editor/UI/Setup/SetupWizardWindow.uxml";
        private const string USS_RELATIVE_PATH = "Editor/UI/Setup/SetupWizardWindow.uss";

        static SetupWizardWindow()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            if (Application.isBatchMode) return;

            string lastVersion = McpEditorSettings.GetSettings().lastSkillPromptVersion;
            if (lastVersion == McpVersion.VERSION) return;

            // Static constructors cannot run async I/O, but CLI version check requires
            // spawning a process. Defer to delayCall so we can await the result.
            if (string.IsNullOrEmpty(lastVersion))
            {
                EditorApplication.delayCall += CheckLegacySetupAndMaybeShowWindow;
                return;
            }

            EditorApplication.delayCall += ShowWindow;
        }

        private static async void CheckLegacySetupAndMaybeShowWindow()
        {
            await CliInstallationDetector.ForceRefreshCliVersionAsync(CancellationToken.None);

            if (IsSetupComplete())
            {
                SavePromptVersion();
                return;
            }

            ShowWindow();
        }

        [MenuItem("Window/Unity CLI Loop/Setup Wizard", priority = 3)]
        public static void ShowWindow()
        {
            SetupWizardWindow window = GetWindow<SetupWizardWindow>(true, "Unity CLI Loop Setup");
            window.minSize = new Vector2(400, 350);
            window.ShowUtility();
        }

        // Prerequisite
        private VisualElement _nodejsWarning;
        private VisualElement _nodejsOk;
        private Button _refreshButton;

        // Step 1
        private VisualElement _cliStatusIcon;
        private Label _cliStatusLabel;
        private Button _installCliButton;

        // Step 2
        private VisualElement _skillsTargetList;
        private Label _skillsStatusLabel;
        private Button _installSkillsButton;

        // Footer
        private Button _skipButton;
        private Button _openSettingsButton;

        // State
        private bool _isInstallingCli;
        private bool _isInstallingSkills;
        private bool _isSkipped;

        private void CreateGUI()
        {
            LoadLayout();
            BindElements();
            BindEvents();
            RefreshUI();
        }

        private void LoadLayout()
        {
            string uxmlPath = $"{McpConstants.PackageAssetPath}/{UXML_RELATIVE_PATH}";
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            Debug.Assert(visualTree != null, $"UXML not found at {uxmlPath}");
            visualTree.CloneTree(rootVisualElement);

            string ussPath = $"{McpConstants.PackageAssetPath}/{USS_RELATIVE_PATH}";
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            Debug.Assert(styleSheet != null, $"USS not found at {ussPath}");
            rootVisualElement.styleSheets.Add(styleSheet);
        }

        private void BindElements()
        {
            _nodejsWarning = rootVisualElement.Q<VisualElement>("nodejs-warning");
            _nodejsOk = rootVisualElement.Q<VisualElement>("nodejs-ok");
            _refreshButton = rootVisualElement.Q<Button>("refresh-button");

            _cliStatusIcon = rootVisualElement.Q<VisualElement>("cli-status-icon");
            _cliStatusLabel = rootVisualElement.Q<Label>("cli-status-label");
            _installCliButton = rootVisualElement.Q<Button>("install-cli-button");

            _skillsTargetList = rootVisualElement.Q<VisualElement>("skills-target-list");
            _skillsStatusLabel = rootVisualElement.Q<Label>("skills-status-label");
            _installSkillsButton = rootVisualElement.Q<Button>("install-skills-button");

            _skipButton = rootVisualElement.Q<Button>("skip-button");
            _openSettingsButton = rootVisualElement.Q<Button>("open-settings-button");
        }

        private void BindEvents()
        {
            _refreshButton.clicked += () => RefreshUI();
            _installCliButton.clicked += HandleInstallCli;
            _installSkillsButton.clicked += HandleInstallSkills;
            _skipButton.clicked += HandleSkip;
            _openSettingsButton.clicked += HandleOpenSettings;
        }

        private async void RefreshUI()
        {
            string nodePath = NodeEnvironmentResolver.FindNodePath();
            bool nodeDetected = !string.IsNullOrEmpty(nodePath);

            ViewDataBinder.SetVisible(_nodejsWarning, !nodeDetected);
            ViewDataBinder.SetVisible(_nodejsOk, nodeDetected);

            if (!nodeDetected)
            {
                _cliStatusLabel.text = "Requires Node.js";
                ViewDataBinder.ToggleClass(_cliStatusIcon, "setup-status-icon--success", false);
                ViewDataBinder.ToggleClass(_cliStatusIcon, "setup-status-icon--pending", true);
                _installCliButton.SetEnabled(false);
                _installSkillsButton.SetEnabled(false);
                _skillsStatusLabel.text = "";
                _skillsTargetList.Clear();
                _skipButton.SetEnabled(false);
                _openSettingsButton.SetEnabled(false);
                return;
            }

            await CliInstallationDetector.ForceRefreshCliVersionAsync(CancellationToken.None);
            string cliVersion = CliInstallationDetector.GetCachedCliVersion();
            bool cliInstalled = cliVersion != null;
            bool cliVersionMatched = IsCliVersionMatched(cliVersion);

            UpdateCliStep(cliInstalled, cliVersion, cliVersionMatched);

            List<ToolSkillSynchronizer.SkillTargetInfo> targets = ToolSkillSynchronizer.DetectTargets();
            UpdateSkillsStep(cliVersionMatched, targets);

            bool noTargets = targets.Count == 0;
            bool allSkillsInstalled = targets.Count > 0
                && targets.All(t => t.HasExistingSkills);
            bool step2Done = allSkillsInstalled || noTargets || _isSkipped;
            _openSettingsButton.SetEnabled(cliVersionMatched && step2Done);
        }

        private void UpdateCliStep(bool cliInstalled, string cliVersion, bool cliVersionMatched)
        {
            if (cliInstalled && cliVersionMatched)
            {
                _cliStatusLabel.text = $"v{cliVersion}";
                ViewDataBinder.ToggleClass(_cliStatusIcon, "setup-status-icon--success", true);
                ViewDataBinder.ToggleClass(_cliStatusIcon, "setup-status-icon--pending", false);
                _installCliButton.SetEnabled(false);
                _installCliButton.text = "Installed";
                return;
            }

            if (cliInstalled)
            {
                string requiredVersion = McpConstants.PackageInfo.version;
                _cliStatusLabel.text = $"v{cliVersion} (requires v{requiredVersion})";
            }
            else
            {
                _cliStatusLabel.text = "Not installed";
            }

            ViewDataBinder.ToggleClass(_cliStatusIcon, "setup-status-icon--success", false);
            ViewDataBinder.ToggleClass(_cliStatusIcon, "setup-status-icon--pending", true);
            _installCliButton.SetEnabled(!_isInstallingCli);
            _installCliButton.text = _isInstallingCli ? "Installing..." : "Install CLI";
        }

        private static bool IsSetupComplete()
        {
            string cliVersion = CliInstallationDetector.GetCachedCliVersion();
            if (!IsCliVersionMatched(cliVersion)) return false;

            List<ToolSkillSynchronizer.SkillTargetInfo> targets = ToolSkillSynchronizer.DetectTargets();
            if (targets.Count == 0) return true;

            return targets.All(t => t.HasExistingSkills);
        }

        private static bool IsCliVersionMatched(string cliVersion)
        {
            if (string.IsNullOrEmpty(cliVersion)) return false;

            string normalized = cliVersion.Trim().TrimStart('v', 'V');
            if (!System.Version.TryParse(normalized, out System.Version installed)) return false;
            if (!System.Version.TryParse(McpConstants.PackageInfo.version, out System.Version required)) return false;

            return installed.CompareTo(required) == 0;
        }

        private void UpdateSkillsStep(
            bool cliInstalled,
            List<ToolSkillSynchronizer.SkillTargetInfo> targets)
        {
            _skillsTargetList.Clear();

            if (!cliInstalled)
            {
                _skillsStatusLabel.text = "";
                _installSkillsButton.SetEnabled(false);
                _skipButton.SetEnabled(false);
                return;
            }

            if (targets.Count == 0)
            {
                _skillsStatusLabel.text = "No AI tool directories detected (.claude/, .agents/, etc.)";
                _installSkillsButton.SetEnabled(false);
                return;
            }

            foreach (ToolSkillSynchronizer.SkillTargetInfo target in targets)
            {
                VisualElement item = new VisualElement();
                item.AddToClassList("setup-target-item");

                string prefix = target.HasExistingSkills ? "✓" : "○";
                Label label = new Label($"  {prefix} {target.DisplayName} ({target.DirName}/)");
                label.AddToClassList("setup-target-item__label");
                item.Add(label);

                _skillsTargetList.Add(item);
            }

            bool allSkillsInstalled = targets.All(t => t.HasExistingSkills);
            if (allSkillsInstalled)
            {
                _skillsStatusLabel.text = $"Installed for {targets.Count} targets";
                _installSkillsButton.SetEnabled(false);
                _installSkillsButton.text = "Installed";
                _skipButton.SetEnabled(false);
            }
            else if (_isSkipped)
            {
                _skillsStatusLabel.text = "";
                _installSkillsButton.SetEnabled(false);
                _installSkillsButton.text = "Skipped";
                _skipButton.SetEnabled(false);
            }
            else
            {
                _skillsStatusLabel.text = "";
                _installSkillsButton.SetEnabled(!_isInstallingSkills);
                _installSkillsButton.text = _isInstallingSkills ? "Installing..." : "Install Skills";
                _skipButton.SetEnabled(true);
            }
        }

        private async void HandleInstallCli()
        {
            string npmPath = NodeEnvironmentResolver.FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                EditorUtility.DisplayDialog(
                    "npm Not Found",
                    "npm was not found on this system.\nPlease install Node.js first, then try again.",
                    "OK");
                return;
            }

            string packageVersion = McpConstants.PackageInfo.version;
            string installTarget = $"{CliConstants.NPM_PACKAGE_NAME}@{packageVersion}";

            bool permissionOk = CliInstaller.CheckWindowsPermissions(
                npmPath, installTarget, out string globalPrefix, out string manualCommand);
            if (!permissionOk)
            {
                EditorUtility.DisplayDialog(
                    "Permission Issue",
                    $"npm's global directory ({globalPrefix}) requires elevated permissions.\n\n"
                    + NpmInstallDiagnostics.BuildPermissionSolutions(manualCommand),
                    "OK");
                return;
            }

            _isInstallingCli = true;
            UpdateCliStep(false, null, false);

            try
            {
                string nodePath = NodeEnvironmentResolver.FindNodePath();
                CliInstallResult result = await CliInstaller.InstallAsync(npmPath, installTarget, nodePath);

                if (!result.Success)
                {
                    EditorUtility.DisplayDialog(
                        "Installation Failed",
                        $"Failed to install uloop-cli.\n\n{result.ErrorOutput}\n\n"
                        + $"You can install manually:\n  npm install -g {installTarget}",
                        "OK");
                }
            }
            finally
            {
                _isInstallingCli = false;
                RefreshUI();
            }
        }

        private async void HandleInstallSkills()
        {
            List<ToolSkillSynchronizer.SkillTargetInfo> targets = ToolSkillSynchronizer.DetectTargets();
            if (targets.Count == 0) return;

            _isInstallingSkills = true;
            UpdateSkillsStep(true, targets);

            try
            {
                ToolSkillSynchronizer.SkillInstallResult result = await ToolSkillSynchronizer.InstallSkillFiles(targets);

                if (!result.IsSuccessful)
                {
                    EditorUtility.DisplayDialog(
                        "Installation Partially Failed",
                        $"{result.SucceededTargets}/{result.AttemptedTargets} targets succeeded.\n"
                        + "Run 'uloop skills install' to retry failed targets.",
                        "OK");
                }
            }
            finally
            {
                _isInstallingSkills = false;
                RefreshUI();
            }
        }

        private void HandleSkip()
        {
            string cliVersion = CliInstallationDetector.GetCachedCliVersion();
            Debug.Assert(IsCliVersionMatched(cliVersion), "HandleSkip requires CLI version match");

            _isSkipped = true;
            SavePromptVersion();
            List<ToolSkillSynchronizer.SkillTargetInfo> targets = ToolSkillSynchronizer.DetectTargets();
            UpdateSkillsStep(true, targets);
            _openSettingsButton.SetEnabled(true);
        }

        private void HandleOpenSettings()
        {
            SavePromptVersion();
            McpEditorWindow.ShowWindow();
            Close();
        }

        private void OnDestroy()
        {
            if (_isSkipped) return;
            if (!IsSetupComplete()) return;

            SavePromptVersion();
        }

        private static void SavePromptVersion()
        {
            McpEditorSettings.UpdateSettings(s => s with
            {
                lastSkillPromptVersion = McpVersion.VERSION
            });
        }

    }
}
