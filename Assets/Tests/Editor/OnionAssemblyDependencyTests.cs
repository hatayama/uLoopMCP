using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop
{
    [TestFixture]
    public sealed class OnionAssemblyDependencyTests
    {
        private const string ApplicationAssemblyName = "UnityCLILoop.Application";
        private const string CompositionRootAssemblyName = "UnityCLILoop.CompositionRoot.Editor";
        private const string DomainAssemblyName = "UnityCLILoop.Domain";
        private const string FirstPartyToolsAssemblyName = "UnityCLILoop.FirstPartyTools.Editor";
        private const string InfrastructureAssemblyName = "UnityCLILoop.Infrastructure";
        private const string PresentationAssemblyName = "UnityCLILoop.Presentation";
        private const string SharedAssemblyName = "uLoopMCP.Editor.Shared";
        private const string ToolContractsAssemblyName = "UnityCLILoop.ToolContracts";

        [Test]
        public void DomainAsmdef_WhenLoaded_HasNoProjectAssemblyReferences()
        {
            // Tests that the domain layer stays independent from outer project assemblies.
            string[] references = ReadResolvedReferences("Packages/src/Editor/Domain/UnityCLILoop.Domain.asmdef");

            Assert.That(references, Is.Empty);
        }

        [Test]
        public void PlatformResultTypes_WhenLoaded_CompileUnderDomainAssembly()
        {
            // Tests that pure platform result values live in the domain layer.
            string validationResultAssemblyName = typeof(ValidationResult).Assembly.GetName().Name;
            string serviceResultAssemblyName = typeof(ServiceResult<int>).Assembly.GetName().Name;

            Assert.That(validationResultAssemblyName, Is.EqualTo(DomainAssemblyName));
            Assert.That(serviceResultAssemblyName, Is.EqualTo(DomainAssemblyName));
        }

        [Test]
        public void ToolContractsAsmdef_WhenLoaded_HasNoProjectAssemblyReferences()
        {
            // Tests that the public tool contract assembly stays independent from implementation assemblies.
            string[] references = ReadResolvedReferences("Packages/src/Editor/ToolContracts/UnityCLILoop.ToolContracts.asmdef");

            Assert.That(references, Is.Empty);
        }

        [Test]
        public void ApplicationAsmdef_WhenLoaded_ReferencesInnerContractsButNotOuterLayers()
        {
            // Tests that application code can depend inward without depending on presentation or infrastructure.
            string[] references = ReadResolvedReferences("Packages/src/Editor/UnityCLILoop.Application.asmdef");

            Assert.That(references, Does.Contain(DomainAssemblyName));
            Assert.That(references, Does.Contain(ToolContractsAssemblyName));
            Assert.That(references, Does.Not.Contain(PresentationAssemblyName));
            Assert.That(references, Does.Not.Contain(InfrastructureAssemblyName));
            Assert.That(references, Does.Not.Contain(CompositionRootAssemblyName));
            Assert.That(references, Does.Not.Contain(FirstPartyToolsAssemblyName));
        }

        [Test]
        public void PresentationAsmdef_WhenLoaded_DependsOnApplicationAndDoesNotReferenceInfrastructure()
        {
            // Tests that presentation and infrastructure remain sibling outer layers.
            string[] references = ReadResolvedReferences("Packages/src/Editor/Presentation/UnityCLILoop.Presentation.asmdef");

            Assert.That(references, Is.EquivalentTo(new[]
            {
                ApplicationAssemblyName,
                DomainAssemblyName,
                SharedAssemblyName,
                ToolContractsAssemblyName
            }));
        }

        [Test]
        public void InfrastructureAsmdef_WhenLoaded_DependsOnApplicationAndDoesNotReferencePresentation()
        {
            // Tests that infrastructure and presentation remain sibling outer layers.
            string[] references = ReadResolvedReferences("Packages/src/Editor/Infrastructure/UnityCLILoop.Infrastructure.asmdef");

            Assert.That(references, Is.EquivalentTo(new[]
            {
                ApplicationAssemblyName,
                DomainAssemblyName,
                ToolContractsAssemblyName
            }));
        }

        [Test]
        public void FirstPartyToolsAsmdef_WhenLoaded_ReferencesOnlyToolContracts()
        {
            // Tests that first-party tools use the same public contract boundary as extension tools.
            string[] references = ReadResolvedReferences("Packages/src/Editor/FirstPartyTools/UnityCLILoop.FirstPartyTools.Editor.asmdef");

            Assert.That(references, Is.EquivalentTo(new[] { ToolContractsAssemblyName }));
        }

        [Test]
        public void CompositionRootAsmdef_WhenLoaded_ReferencesAllOnionAssemblies()
        {
            // Tests that the composition root is the assembly allowed to wire every layer together.
            string[] references = ReadResolvedReferences("Packages/src/Editor/CompositionRoot/UnityCLILoop.CompositionRoot.Editor.asmdef");

            Assert.That(references, Is.EquivalentTo(new[]
            {
                ApplicationAssemblyName,
                DomainAssemblyName,
                FirstPartyToolsAssemblyName,
                InfrastructureAssemblyName,
                PresentationAssemblyName,
                ToolContractsAssemblyName,
                SharedAssemblyName
            }));
        }

        [Test]
        public void DynamicCodeCompilerRegistration_WhenLoaded_CompilesUnderCompositionRootAssembly()
        {
            // Tests that dynamic-code service registration is owned by the composition root.
            string registrationAssemblyName = typeof(DynamicCodeCompilationServiceRegistration).Assembly.GetName().Name;

            Assert.That(registrationAssemblyName, Is.EqualTo(CompositionRootAssemblyName));
        }

        [Test]
        public void ProductionAsmdefs_WhenLoaded_DoNotReferenceCompositionRootExceptCompositionRootItself()
        {
            // Tests that composition root dependencies do not leak back into production assemblies.
            string[] offendingAssemblyNames = ReadProductionAsmdefPaths()
                .Where(path => ReadAsmdefName(path) != CompositionRootAssemblyName)
                .Where(path => ReadResolvedReferencesFromAbsolutePath(path).Contains(CompositionRootAssemblyName))
                .Select(ReadAsmdefName)
                .OrderBy(assemblyName => assemblyName)
                .ToArray();

            Assert.That(offendingAssemblyNames, Is.Empty);
        }

        [Test]
        public void EditorUiFiles_WhenLoaded_DoNotReferenceFacadeInternalsDirectly()
        {
            // Tests that presentation-facing UI code uses Application facades for setup and tool settings workflows.
            string[] forbiddenReferences =
            {
                "CliInstallationDetector",
                "ProjectLocalCliAutoInstaller",
                "ProjectLocalCliInstaller",
                "NativeCliInstaller",
                "CliVersionComparer",
                "ToolSkillSynchronizer",
                "ULoopSettings.",
                "ToolSettings.",
                "UnityCliLoopToolRegistrar",
                "UnityCliLoopToolRegistry",
                "ToolSettingsCatalogItem",
                "InputRecorder",
                "InputReplayer",
                "InputRecordingFileHelper",
                "InputRecordingData",
                "RecordInputConstants",
                "RecordInputOverlayState",
                "OverlayCanvasFactory",
                "RecordReplayOverlayFactory"
            };
            string[] offendingReferences = ReadPresentationSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, forbiddenReferences))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void EditorUiFiles_WhenLoaded_ContainNoEditorWindowImplementations()
        {
            // Tests that EditorWindow implementations compile under the presentation assembly.
            string uiRoot = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), "Packages", "src", "Editor", "UI");
            if (!Directory.Exists(uiRoot))
            {
                Assert.Pass();
            }

            string[] offendingReferences = Directory.GetFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("EditorWindow"))
                .Select(path => Path.GetRelativePath(UnityCliLoopPathResolver.GetProjectRoot(), path))
                .OrderBy(path => path)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpCommunicationLogTypes()
        {
            // Tests that the removed MCP communication log utility does not return under a legacy public name.
            string[] legacyNames =
            {
                "Mcp" + "CommunicationLog",
                "Mcp" + "CommunicationLogger"
            };
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, legacyNames))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpUiConstantsName()
        {
            // Tests that shared UI constants use UnityCLILoop naming instead of legacy MCP naming.
            string legacyUiConstantsName = "Mcp" + "UIConstants";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyUiConstantsName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpEditorSettingsName()
        {
            // Tests that editor settings use UnityCLILoop naming outside protocol-specific code.
            string legacyEditorSettingsName = "Mcp" + "EditorSettings";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyEditorSettingsName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpEditorDomainReloadProviderName()
        {
            // Tests that editor domain reload state code uses UnityCLILoop naming outside protocol-specific code.
            string[] legacyNames =
            {
                "Mcp" + "EditorDomainReloadStateProvider",
                "Mcp" + "EditorDomainReloadStateRegistration"
            };
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, legacyNames))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyUnityMcpPathResolverName()
        {
            // Tests that path resolution code uses UnityCLILoop naming outside protocol-specific code.
            string legacyPathResolverName = "Unity" + "McpPathResolver";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyPathResolverName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpVersionName()
        {
            // Tests that package version code uses UnityCLILoop naming outside protocol-specific code.
            string legacyVersionName = "Mcp" + "Version";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyVersionName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpSecurityNames()
        {
            // Tests that tool execution security code uses UnityCLILoop naming outside protocol-specific code.
            string[] legacyNames =
            {
                "Mcp" + "SecurityChecker",
                "Mcp" + "SecurityException"
            };
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, legacyNames))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpLogTypeName()
        {
            // Tests that GetLogs parameter values use UnityCLILoop naming outside protocol-specific code.
            string legacyLogTypeName = "Mcp" + "LogType";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyLogTypeName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpConstantsName()
        {
            // Tests that shared constants use UnityCLILoop naming outside protocol-specific code.
            string legacyConstantsName = "Mcp" + "Constants";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyConstantsName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpServerConfigName()
        {
            // Tests that bridge server settings use UnityCLILoop naming outside protocol-specific code.
            string legacyConfigName = "Mcp" + "ServerConfig";
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyConfigName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotReferenceRemovedLegacyMcpTypes()
        {
            // Tests that comments and docs in production source do not point to removed legacy types.
            string[] legacyNames =
            {
                "Mcp" + "SessionManager",
                "Mcp" + "Logger",
                "Unity" + "McpServer"
            };
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, legacyNames))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void ProductionSources_WhenLoaded_DoNotUseLegacyMcpServerLifecycleNames()
        {
            // Tests that project IPC server lifecycle types use UnityCLILoop naming outside protocol-specific code.
            string[] legacyNames =
            {
                "Mcp" + "BridgeServer",
                "Mcp" + "ServerController",
                "Mcp" + "ServerStartupService",
                "Mcp" + "ServerInitializationUseCase",
                "Mcp" + "ServerShutdownUseCase"
            };
            string[] offendingReferences = ReadProductionSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, legacyNames))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void EditorUiFiles_WhenLoaded_DoNotUseLegacyMcpSettingsWindowNames()
        {
            // Tests that settings-window UI code uses UnityCLILoop naming instead of legacy MCP naming.
            string legacySettingsWindowName = "Mcp" + "EditorWindow";
            string[] offendingReferences = ReadPresentationSourcePaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacySettingsWindowName }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        [Test]
        public void PresentationAssets_WhenLoaded_DoNotUseLegacyMcpStylePrefix()
        {
            // Tests that presentation USS, UXML, and C# class names do not keep the legacy MCP style prefix.
            string legacyStylePrefix = "mcp" + "-";
            string[] offendingReferences = ReadPresentationAssetPaths()
                .SelectMany(path => FindForbiddenReferences(path, new[] { legacyStylePrefix }))
                .OrderBy(reference => reference)
                .ToArray();

            Assert.That(offendingReferences, Is.Empty);
        }

        private static string[] ReadResolvedReferences(string relativeAsmdefPath)
        {
            string asmdefPath = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), relativeAsmdefPath);
            return ReadResolvedReferencesFromAbsolutePath(asmdefPath);
        }

        private static string[] ReadResolvedReferencesFromAbsolutePath(string asmdefPath)
        {
            Dictionary<string, string> guidToAssemblyNameMap = BuildGuidToAssemblyNameMap();
            string[] rawReferences = ReadRawReferences(asmdefPath);
            List<string> resolvedReferences = new List<string>();

            foreach (string rawReference in rawReferences)
            {
                resolvedReferences.Add(ResolveReference(rawReference, guidToAssemblyNameMap));
            }

            return resolvedReferences
                .OrderBy(reference => reference)
                .ToArray();
        }

        private static string[] ReadRawReferences(string asmdefPath)
        {
            JObject asmdef = JObject.Parse(File.ReadAllText(asmdefPath));
            return asmdef["references"]?.Values<string>().ToArray() ?? new string[0];
        }

        private static string[] ReadProductionAsmdefPaths()
        {
            string editorRoot = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), "Packages", "src", "Editor");
            return Directory.GetFiles(editorRoot, "*.asmdef", SearchOption.AllDirectories);
        }

        private static string[] ReadPresentationSourcePaths()
        {
            string presentationRoot = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), "Packages", "src", "Editor", "Presentation");
            return Directory.GetFiles(presentationRoot, "*.cs", SearchOption.AllDirectories);
        }

        private static string[] ReadPresentationAssetPaths()
        {
            string presentationRoot = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), "Packages", "src", "Editor", "Presentation");
            return Directory.GetFiles(presentationRoot, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.Ordinal)
                    || path.EndsWith(".uxml", StringComparison.Ordinal)
                    || path.EndsWith(".uss", StringComparison.Ordinal))
                .ToArray();
        }

        private static string[] ReadProductionSourcePaths()
        {
            string editorRoot = Path.Combine(UnityCliLoopPathResolver.GetProjectRoot(), "Packages", "src", "Editor");
            return Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories);
        }

        private static string[] FindForbiddenReferences(string path, string[] forbiddenReferences)
        {
            string source = File.ReadAllText(path);
            List<string> violations = new List<string>();

            foreach (string forbiddenReference in forbiddenReferences)
            {
                if (!source.Contains(forbiddenReference))
                {
                    continue;
                }

                violations.Add($"{Path.GetFileName(path)} references {forbiddenReference}");
            }

            return violations.ToArray();
        }

        private static Dictionary<string, string> BuildGuidToAssemblyNameMap()
        {
            string[] asmdefPaths = ReadProjectAsmdefPaths();
            Dictionary<string, string> guidToAssemblyNameMap = new Dictionary<string, string>();

            foreach (string asmdefPath in asmdefPaths)
            {
                string metaPath = asmdefPath + ".meta";
                if (!File.Exists(metaPath))
                {
                    continue;
                }

                string guid = ReadMetaGuid(metaPath);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                guidToAssemblyNameMap[guid] = ReadAsmdefName(asmdefPath);
            }

            return guidToAssemblyNameMap;
        }

        private static string[] ReadProjectAsmdefPaths()
        {
            string projectRoot = UnityCliLoopPathResolver.GetProjectRoot();
            List<string> asmdefPaths = new List<string>();
            string assetsPath = Path.Combine(projectRoot, "Assets");
            string packagesSrcPath = Path.Combine(projectRoot, "Packages", "src");

            if (Directory.Exists(assetsPath))
            {
                asmdefPaths.AddRange(Directory.GetFiles(assetsPath, "*.asmdef", SearchOption.AllDirectories));
            }

            if (Directory.Exists(packagesSrcPath))
            {
                asmdefPaths.AddRange(Directory.GetFiles(packagesSrcPath, "*.asmdef", SearchOption.AllDirectories));
            }

            return asmdefPaths.ToArray();
        }

        private static string ResolveReference(string reference, Dictionary<string, string> guidToAssemblyNameMap)
        {
            const string guidReferencePrefix = "GUID:";
            if (!reference.StartsWith(guidReferencePrefix))
            {
                return reference;
            }

            string guid = reference.Substring(guidReferencePrefix.Length);
            if (!guidToAssemblyNameMap.ContainsKey(guid))
            {
                return reference;
            }

            return guidToAssemblyNameMap[guid];
        }

        private static string ReadMetaGuid(string metaPath)
        {
            string[] lines = File.ReadAllLines(metaPath);

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (!trimmedLine.StartsWith("guid:"))
                {
                    continue;
                }

                return trimmedLine.Substring("guid:".Length).Trim();
            }

            return string.Empty;
        }

        private static string ReadAsmdefName(string asmdefPath)
        {
            JObject asmdef = JObject.Parse(File.ReadAllText(asmdefPath));
            return asmdef["name"]?.Value<string>() ?? string.Empty;
        }
    }
}
