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

            foldout.Add(container);
            root.Add(foldout);
            return root;
        }

        private static ToolSettingsSectionData CreateData(bool compileEnabled, bool includeGetLogs)
        {
            ToolToggleItem compile = new ToolToggleItem(
                toolName: "compile",
                description: "Compile project",
                isEnabled: compileEnabled,
                isThirdParty: false);

            if (!includeGetLogs)
            {
                return new ToolSettingsSectionData(
                    showToolSettings: true,
                    builtInTools: new[] { compile },
                    thirdPartyTools: System.Array.Empty<ToolToggleItem>(),
                    isRegistryAvailable: true);
            }

            ToolToggleItem getLogs = new ToolToggleItem(
                toolName: "get-logs",
                description: "Read Unity logs",
                isEnabled: true,
                isThirdParty: false);

            return new ToolSettingsSectionData(
                showToolSettings: true,
                builtInTools: new[] { compile, getLogs },
                thirdPartyTools: System.Array.Empty<ToolToggleItem>(),
                isRegistryAvailable: true);
        }

        private static Toggle FindToggleByToolName(VisualElement container, string toolName)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                VisualElement row = container[i];
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

            return null;
        }
    }
}
