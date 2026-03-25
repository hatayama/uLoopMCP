using NUnit.Framework;
using UnityEngine.UIElements;

namespace io.github.hatayama.uLoopMCP
{
    [TestFixture]
    public class ToolSettingsSectionTests
    {
        [Test]
        public void Update_SameLayout_DoesNotReplaceToggleElements()
        {
            VisualElement root = CreateRootElement();
            ToolSettingsSection section = new ToolSettingsSection(root);
            ToolSettingsSectionData data = CreateData(compileEnabled: true, includeGetLogs: false);
            VisualElement container = root.Q<VisualElement>("tool-list-container");

            section.Update(data);
            Toggle beforeToggle = FindToggleByToolName(container, "compile");

            section.Update(data);
            Toggle afterToggle = FindToggleByToolName(container, "compile");

            Assert.IsNotNull(beforeToggle);
            Assert.AreSame(beforeToggle, afterToggle);
        }

        [Test]
        public void Update_SameLayout_UpdatesToggleStateWithoutRebuild()
        {
            VisualElement root = CreateRootElement();
            ToolSettingsSection section = new ToolSettingsSection(root);
            ToolSettingsSectionData enabledData = CreateData(compileEnabled: true, includeGetLogs: false);
            ToolSettingsSectionData disabledData = CreateData(compileEnabled: false, includeGetLogs: false);
            VisualElement container = root.Q<VisualElement>("tool-list-container");

            section.Update(enabledData);
            Toggle beforeToggle = FindToggleByToolName(container, "compile");
            Assert.IsNotNull(beforeToggle);
            Assert.IsTrue(beforeToggle.value);

            section.Update(disabledData);
            Toggle afterToggle = FindToggleByToolName(container, "compile");

            Assert.AreSame(beforeToggle, afterToggle);
            Assert.IsFalse(afterToggle.value);
        }

        [Test]
        public void Update_LayoutChanged_RebuildsToggleElements()
        {
            VisualElement root = CreateRootElement();
            ToolSettingsSection section = new ToolSettingsSection(root);
            ToolSettingsSectionData initialData = CreateData(compileEnabled: true, includeGetLogs: false);
            ToolSettingsSectionData changedLayoutData = CreateData(compileEnabled: true, includeGetLogs: true);
            VisualElement container = root.Q<VisualElement>("tool-list-container");

            section.Update(initialData);
            Toggle beforeToggle = FindToggleByToolName(container, "compile");

            section.Update(changedLayoutData);
            Toggle afterToggle = FindToggleByToolName(container, "compile");
            Toggle getLogsToggle = FindToggleByToolName(container, "get-logs");

            Assert.IsNotNull(beforeToggle);
            Assert.IsNotNull(afterToggle);
            Assert.AreNotSame(beforeToggle, afterToggle);
            Assert.IsNotNull(getLogsToggle);
        }

        [Test]
        public void Update_ThirdPartyToolsDisabled_GraysOutThirdPartyGroupOnly()
        {
            VisualElement root = CreateRootElement();
            ToolSettingsSection section = new ToolSettingsSection(root);
            ToolSettingsSectionData data = CreateData(
                compileEnabled: true,
                includeGetLogs: false,
                allowThirdPartyTools: false,
                includeThirdPartyTool: true);
            VisualElement container = root.Q<VisualElement>("tool-list-container");

            section.Update(data);

            VisualElement builtInGroup = FindGroupByHeader(container, "Built-in Tools");
            VisualElement thirdPartyGroup = FindGroupByHeader(container, "Third Party Tools");

            Assert.IsNotNull(builtInGroup);
            Assert.IsNotNull(thirdPartyGroup);
            Assert.IsTrue(builtInGroup.enabledSelf);
            Assert.IsFalse(thirdPartyGroup.enabledSelf);
        }

        [Test]
        public void Update_RestrictedLevel_MarksRestrictedButtonAsActive()
        {
            VisualElement root = CreateRootElement();
            ToolSettingsSection section = new ToolSettingsSection(root);
            ToolSettingsSectionData data = CreateData(
                compileEnabled: true,
                includeGetLogs: false,
                dynamicCodeSecurityLevel: DynamicCodeSecurityLevel.Restricted);

            section.Update(data);

            Button restrictedButton = root.Q<Button>("security-level-restricted-button");
            Button fullAccessButton = root.Q<Button>("security-level-full-access-button");

            Assert.IsTrue(restrictedButton.ClassListContains("mcp-segmented-control__button--active"));
            Assert.IsFalse(fullAccessButton.ClassListContains("mcp-segmented-control__button--active"));
        }

        [Test]
        public void Update_FullAccessLevel_MarksFullAccessButtonAsWarningActive()
        {
            VisualElement root = CreateRootElement();
            ToolSettingsSection section = new ToolSettingsSection(root);
            ToolSettingsSectionData data = CreateData(
                compileEnabled: true,
                includeGetLogs: false,
                dynamicCodeSecurityLevel: DynamicCodeSecurityLevel.FullAccess);

            section.Update(data);

            Button restrictedButton = root.Q<Button>("security-level-restricted-button");
            Button fullAccessButton = root.Q<Button>("security-level-full-access-button");
            Label description = root.Q<Label>("security-level-description");

            Assert.IsFalse(restrictedButton.ClassListContains("mcp-segmented-control__button--active"));
            Assert.IsTrue(fullAccessButton.ClassListContains("mcp-segmented-control__button--active"));
            Assert.IsTrue(fullAccessButton.ClassListContains("mcp-segmented-control__button--warning-active"));
            Assert.IsTrue(description.ClassListContains("mcp-security-level-description--warning"));
        }

        private static VisualElement CreateRootElement()
        {
            VisualElement root = new VisualElement();
            Foldout foldout = new Foldout
            {
                name = "tool-settings-foldout"
            };
            VisualElement container = new VisualElement
            {
                name = "tool-list-container"
            };

            Label cliReferenceLink = new Label
            {
                name = "cli-reference-link"
            };
            Toggle allowThirdPartyToggle = new Toggle
            {
                name = "allow-third-party-toggle"
            };
            Label allowThirdPartyLabel = new Label
            {
                name = "allow-third-party-label"
            };
            Button securityLevelRestrictedButton = new Button
            {
                name = "security-level-restricted-button"
            };
            Button securityLevelFullAccessButton = new Button
            {
                name = "security-level-full-access-button"
            };
            Label securityLevelDescription = new Label
            {
                name = "security-level-description"
            };

            foldout.Add(allowThirdPartyToggle);
            foldout.Add(allowThirdPartyLabel);
            foldout.Add(securityLevelRestrictedButton);
            foldout.Add(securityLevelFullAccessButton);
            foldout.Add(securityLevelDescription);
            foldout.Add(container);
            root.Add(foldout);
            root.Add(cliReferenceLink);
            return root;
        }

        private static ToolSettingsSectionData CreateData(
            bool compileEnabled,
            bool includeGetLogs,
            bool allowThirdPartyTools = true,
            bool includeThirdPartyTool = false,
            DynamicCodeSecurityLevel dynamicCodeSecurityLevel = DynamicCodeSecurityLevel.Restricted)
        {
            ToolToggleItem compile = new ToolToggleItem(
                toolName: "compile",
                description: "Compile project",
                isEnabled: compileEnabled,
                isThirdParty: false);
            ToolToggleItem[] thirdPartyTools = includeThirdPartyTool
                ? new[]
                {
                    new ToolToggleItem(
                        toolName: "sample-third-party",
                        description: "Third-party sample",
                        isEnabled: true,
                        isThirdParty: true)
                }
                : System.Array.Empty<ToolToggleItem>();

            if (!includeGetLogs)
            {
                return new ToolSettingsSectionData(
                    showToolSettings: true,
                    allowThirdPartyTools: allowThirdPartyTools,
                    dynamicCodeSecurityLevel: dynamicCodeSecurityLevel,
                    builtInTools: new[] { compile },
                    thirdPartyTools: thirdPartyTools,
                    isRegistryAvailable: true);
            }

            ToolToggleItem getLogs = new ToolToggleItem(
                toolName: "get-logs",
                description: "Read Unity logs",
                isEnabled: true,
                isThirdParty: false);

            return new ToolSettingsSectionData(
                showToolSettings: true,
                allowThirdPartyTools: allowThirdPartyTools,
                dynamicCodeSecurityLevel: dynamicCodeSecurityLevel,
                builtInTools: new[] { compile, getLogs },
                thirdPartyTools: thirdPartyTools,
                isRegistryAvailable: true);
        }

        private static VisualElement FindGroupByHeader(VisualElement container, string headerText)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                VisualElement group = container[i];
                if (!group.ClassListContains("mcp-tool-group") || group.childCount == 0)
                {
                    continue;
                }

                if (group[0] is Label header && header.text == headerText)
                {
                    return group;
                }
            }

            return null;
        }

        private static Toggle FindToggleByToolName(VisualElement container, string toolName)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                VisualElement group = container[i];
                for (int j = 0; j < group.childCount; j++)
                {
                    VisualElement row = group[j];
                    bool isToggleRow = row.ClassListContains("mcp-tool-toggle-row");
                    if (!isToggleRow || row.childCount < 2)
                    {
                        continue;
                    }

                    Toggle toggle = row[0] as Toggle;
                    Label label = row[1] as Label;
                    bool isTargetRow = label != null && label.text == toolName;

                    if (isTargetRow)
                    {
                        return toggle;
                    }
                }
            }

            return null;
        }
    }
}
